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
    public class MSSqlBlogService : InMemoryBlogServiceBase
    {
        public MSSqlBlogService(IHostingEnvironment env, IHttpContextAccessor contextAccessor)
        {
            _folder = Path.Combine(env.WebRootPath, POSTS);
            _contextAccessor = contextAccessor;

            Initialize();
        }

        #region 改寫部分(override)
        public override async Task SavePost(Post post)
        {
            post.LastModified = DateTime.UtcNow;
            using (var conn = SqlHelper.CreateDefaultConnection())
            using (var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                if (conn.Query<bool>("select top 1 1 from post where id = @id", new { @id = post.ID }).SingleOrDefault())
                {
                    await conn.ExecuteAsync(@"
                        UPDATE Post
                        SET Title = @Title,Slug = @Slug,Excerpt = @Excerpt,Content = @Content
                            ,PubDate = @PubDate,LastModified = @LastModified,IsPublished = @IsPublished
                            ,MarkDownContent = @MarkDownContent,IsMarkDown = @IsMarkDown
                        WHERE ID = @ID
                    ", post);
                }
                else
                {
                    await conn.ExecuteAsync(@"
                        INSERT INTO Post(ID,Title,Slug,Excerpt,Content,PubDate,LastModified,IsPublished,MarkDownContent,IsMarkDown)
                        VALUES (@ID,@Title,@Slug,@Excerpt,@Content,@PubDate,@LastModified,@IsPublished,@MarkDownContent,@IsMarkDown)
                    ", post);
                }
                await conn.ExecuteAsync(@"
                        Delete from Categories where PostID = @ID ;
                    ", post);
                var cats = post.Categories.Select(s => new { PostID = post.ID, Name = s }).ToList();
                await conn.ExecuteAsync(@"
                    INSERT INTO Categories (PostID ,Name) 
                    VALUES (@PostID ,@Name)
                ", cats);

                ts.Complete();
            }

            /* 如果有資料做更新，沒資料才做新增
             * 補充:原本寫法: !_cache.Contains(post)，會造成假如是創立新的物件
             * ，但ID值是一樣的，可以重複新增到Cache
             */
            if (!_cache.Any(w => w.ID == post.ID))
            {
                _cache.Add(post);
                SortCache();
            }
            ReloadCacheData();
        }

        public override Task DeletePost(Post post)
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
        #endregion

        #region 不屬於介面方法(資料存取)

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

        private void LoadCategories(Post post, System.Data.IDbConnection conn)
        {
            List<string> list = conn.Query<string>(@"
                select Name from Categories where postid = @ID 
            ", new { @ID = post.ID }).ToList();
            post.Categories = list.ToArray();
        }

        private void LoadComments(Post post, System.Data.IDbConnection conn)
        {
            var comments = conn.Query<Comment>(@"
                select * from Comment where postid = @ID 
            ", new { @ID = post.ID }).ToList();
            foreach (var comment in comments)
            {
                post.Comments.Add(comment);
            }
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

        #region 不屬於介面方法(輔助)
        #endregion
    }
}
