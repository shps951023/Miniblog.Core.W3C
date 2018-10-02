using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Miniblog.Core.Helper;
using Miniblog.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Transactions;

namespace Miniblog.Core.Services
{
    public class MSSqlBlogService : IBlogService
    {
        private const string POSTS = "Posts";
        private const string FILES = "files";

        private readonly List<Post> _cache = new List<Post>();
        private readonly List<IGrouping<string, PostGroupCatsViewModel>> _cachePostGroupByCat = new List<IGrouping<string, PostGroupCatsViewModel>>();
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly string _folder;

        public MSSqlBlogService(IHostingEnvironment env, IHttpContextAccessor contextAccessor)
        {
            _folder = Path.Combine(env.WebRootPath, POSTS);  /*在SQLBlog，路徑主要給存圖片使用。*/
            _contextAccessor = contextAccessor;
            Initialize();
        }

        #region Cache存取資料方法  /*通常不用動到這邊的Code，因為邏輯幾乎一樣*/
        public virtual Task<IEnumerable<Post>> GetPosts(int count, int skip = 0)
        {
            bool isAdmin = IsAdmin();

            var posts = _cache
                .Where(p => p.PubDate <= DateTime.UtcNow && (p.IsPublished || isAdmin))
                .Skip(skip)
                .Take(count);

            return Task.FromResult(posts);
        }

        public virtual Task<IEnumerable<Post>> GetPostsByCategory(string category)
        {
            bool isAdmin = IsAdmin();

            var posts = from p in _cache
                        where p.PubDate <= DateTime.UtcNow && (p.IsPublished || isAdmin)
                        where p.Categories.Contains(category, StringComparer.OrdinalIgnoreCase)
                        select p;

            return Task.FromResult(posts);
        }

        public virtual Task<IEnumerable<IGrouping<string, PostGroupCatsViewModel>>> GetPostsGroupbyCategory(string category)
        {
            bool isAdmin = IsAdmin();
            var postsGroup = _cachePostGroupByCat
                .Where(w => category == null ? true : w.Key == category)
            ;
            return Task.FromResult(postsGroup);
        }

        public virtual Task<Post> GetPostBySlug(string slug)
        {
            var post = _cache.FirstOrDefault(p => p.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
            bool isAdmin = IsAdmin();

            if (post != null && post.PubDate <= DateTime.UtcNow && (post.IsPublished || isAdmin))
            {
                return Task.FromResult(post);
            }

            return Task.FromResult<Post>(null);
        }

        public virtual Task<IEnumerable<Post>> GetPostsByCat(string cat)
        {
            bool isAdmin = IsAdmin();

            var posts = from p in _cache
                        where p.PubDate <= DateTime.Now && (p.IsPublished || isAdmin)
                        where p.Categories.Contains(cat.MiniBlogToLowerInvariant())
                        select p;

            return Task.FromResult(posts);
        }

        public virtual Task<Post> GetPostById(string id)
        {
            var post = _cache.FirstOrDefault(p => p.ID.Equals(id, StringComparison.OrdinalIgnoreCase));
            bool isAdmin = IsAdmin();

            if (post != null && post.PubDate <= DateTime.UtcNow && (post.IsPublished || isAdmin))
            {
                return Task.FromResult(post);
            }

            return Task.FromResult<Post>(null);
        }

        public virtual Task<IEnumerable<string>> GetCategories()
        {
            bool isAdmin = IsAdmin();

            var categories = _cache
                .Where(p => p.IsPublished || isAdmin)
                .SelectMany(post => post.Categories)
                .Select(cat => cat.MiniBlogToLowerInvariant())
                .Distinct();

            return Task.FromResult(categories);
        }
        #endregion /**/

        #region 增刪修改(每個資料庫需要改寫)
        public async Task SavePost(Post post)
        {
            post.LastModified = DateTime.UtcNow;
            using (var conn = SqlHelper.CreateDefaultConnection())
            using (var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                if (conn.Query<bool>("select top 1 1 from post where id = @id", new { @id = post.ID }).SingleOrDefault())
                {
                    await conn.ExecuteAsync(@"
                        UPDATE Post
                        SET Title = @Title,Slug = @Slug,Excerpt = @Excerpt,Content = @Content,PubDate = @PubDate,LastModified = @LastModified,IsPublished = @IsPublished
                        WHERE ID = @ID
                    ", post);
                    await conn.ExecuteAsync(@"
                        Delete Categories where PostID = @ID 
                    ", post);
                    var cats = post.Categories.Select(s => new { PostID = post.ID, Name = s }).ToList();
                    await conn.ExecuteAsync(@"
                       INSERT INTO Categories (PostID ,Name) VALUES (@PostID ,@Name);
                    ", cats);
                }
                else
                {
                    await conn.ExecuteAsync(@"
                        INSERT INTO Post(ID,Title,Slug,Excerpt,Content,PubDate,LastModified,IsPublished)
                        VALUES (@ID,@Title,@Slug,@Excerpt,@Content,@PubDate,@LastModified,@IsPublished)
                    ", post);
                    var cats = post.Categories.Select(s => new { PostID = post.ID, Name = s }).ToList();
                    await conn.ExecuteAsync(@"
                        INSERT INTO Categories (PostID ,Name) 
                        VALUES (@PostID ,@Name)
                    ", cats);
                }
                ts.Complete();
            }

            if (!_cache.Contains(post))
            {
                _cache.Add(post);
                SortCache();
            }
            ReloadCacheData();
        }

        public Task DeletePost(Post post)
        {
            using (var conn = SqlHelper.CreateDefaultConnection())
            {
                conn.Execute(@"
                    begin tran;
                    delete Comment where PostID = @ID;
                    delete Categories where PostID = @ID;
                    delete Post where ID = @ID;
                    commit;
                ", new { ID = post.ID });
            }

            if (_cache.Contains(post))
            {
                _cache.Remove(post);
            }
            ReloadCacheData();

            return Task.CompletedTask;
        }

        public async Task SaveComment(Post post, Comment comment)
        {
            using (var conn = SqlHelper.CreateDefaultConnection())
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO Comment (ID ,Author ,Email ,Content ,PubDate ,IsAdmin ,PostID) 
                    VALUES (@ID ,@Author ,@Email ,@Content ,@PubDate ,@IsAdmin,@PostID)
                ", new
                {
                    @ID = comment.ID,
                    @Author = comment.Author,
                    @Email = comment.Email,
                    @Content = comment.Content,
                    @PubDate = comment.PubDate,
                    @IsAdmin = comment.IsAdmin,
                    @PostID = post.ID
                });
            }
        }

        public async Task DeleteComment(Post post, Comment comment)
        {
            using (var conn = SqlHelper.CreateDefaultConnection())
            {
                await conn.ExecuteAsync(@" Delete Comment where ID = @ID ", new { @ID = comment.ID });
            }
        }

        private static void LoadCategories(Post post, System.Data.IDbConnection conn)
        {
            List<string> list = conn.Query<string>(@"
                select Name from Categories where postid = @ID 
            ", new { @ID = post.ID }).ToList();
            post.Categories = list.ToArray();
        }

        private static void LoadComments(Post post, System.Data.IDbConnection conn)
        {
            var comments = conn.Query<Comment>(@"
                select * from Comment where postid = @ID 
            ", new { @ID = post.ID }).ToList();
            foreach (var comment in comments)
            {
                post.Comments.Add(comment);
            }
        }

        private void LoadPosts()
        {
            using (var conn = SqlHelper.CreateDefaultConnection())
            {
                var posts = conn.Query<Post>("select * from Post");
                foreach (var post in posts)
                {
                    LoadCategories(post, conn);
                    LoadComments(post, conn);
                    _cache.Add(post);
                }
            }
        }
        #endregion


        #region 不屬於介面方法(補助方法)

        protected void SortCache()
        {
            _cache.Sort((p1, p2) => p2.PubDate.CompareTo(p1.PubDate));
        }

        protected bool IsAdmin()
        {
            return _contextAccessor.HttpContext?.User?.Identity.IsAuthenticated == true;
        }

        private void Initialize()
        {
            LoadPosts();
            SortCache();
            LoadPostGroupByCat();
        }

        private void ReloadCacheData()
        {
            LoadPostGroupByCat();
        }

        private void LoadPostGroupByCat()
        {
            _cachePostGroupByCat.Clear();
            _cache
            .SelectMany(cat => cat.Categories,
                (post, cat) => new PostGroupCatsViewModel
                {
                    Title = post.Title,
                    Slug = post.Slug,
                    CatName = cat.MiniBlogToLowerInvariant()
                }
            )
            .GroupBy(g => g.CatName).ToList().ForEach(p =>
            {
                _cachePostGroupByCat.Add(p);
            });
        }

        private static string CleanFromInvalidChars(string input)
        {
            // ToDo: what we are doing here if we switch the blog from windows
            // to unix system or vice versa? we should remove all invalid chars for both systems

            var regexSearch = Regex.Escape(new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars()));
            var r = new Regex($"[{regexSearch}]");
            return r.Replace(input, "");
        }

        public async Task<string> SaveFile(byte[] bytes, string fileName, string suffix = null)
        {
            suffix = CleanFromInvalidChars(suffix ?? DateTime.UtcNow.Ticks.ToString());

            string ext = Path.GetExtension(fileName);
            string name = CleanFromInvalidChars(Path.GetFileNameWithoutExtension(fileName));

            string fileNameWithSuffix = $"{name}_{suffix}{ext}";

            string absolute = Path.Combine(_folder, FILES, fileNameWithSuffix);
            string dir = Path.GetDirectoryName(absolute);

            Directory.CreateDirectory(dir);
            using (var writer = new FileStream(absolute, FileMode.CreateNew))
            {
                await writer.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            }

            return $"/{POSTS}/{FILES}/{fileNameWithSuffix}";
        }

        #endregion


    }
}
