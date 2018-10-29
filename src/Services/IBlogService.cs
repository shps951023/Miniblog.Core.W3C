using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Miniblog.Core.Models;

namespace Miniblog.Core.Services
{
    public interface IBlogService
    {
        Task<IEnumerable<Post>> GetPosts(int count, int skip = 0);

        Task<IEnumerable<Post>> GetPostsByCategory(string category);

        Task<IEnumerable<IGrouping<string, PostGroupCatsViewModel>>> GetPostsGroupbyCategory(string category);

        Task<IEnumerable<Post>> GetPostsByCat(string book);

        Task<Post> GetPostBySlug(string slug);

        Task<Post> GetPostById(string id);

        Task<IEnumerable<string>> GetCategories();

        Task SavePost(Post post);

        Task DeletePost(Post post);

        Task<string> SaveFileAsync(byte[] bytes, string fileName, string suffix = null);
    }

    public abstract class InMemoryBlogServiceBase : IBlogService
    {
        protected const string POSTS = "Posts";
        protected const string FILES = "files";

        protected  List<Post> _cache = new List<Post>();
        protected  List<IGrouping<string, PostGroupCatsViewModel>> _cachePostGroupByCat = new List<IGrouping<string, PostGroupCatsViewModel>>();
        protected  IHttpContextAccessor _contextAccessor;
        protected  string _folder;

        public InMemoryBlogServiceBase(){}

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

        public Task<IEnumerable<IGrouping<string, PostGroupCatsViewModel>>> GetPostsGroupbyCategory(string category)
        {
            bool isAdmin = IsAdmin();
            var postsGroup = _cachePostGroupByCat
                .Where(w => category == null ? true : w.Key == category.MiniBlogToLowerInvariant())
            ;
            return Task.FromResult(postsGroup);
        }

        public async Task<string> SaveFileAsync(byte[] bytes, string fileName, string suffix = null)
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


        public abstract Task SavePost(Post post);

        public abstract Task DeletePost(Post post);

        #region 不屬於介面方法(輔助)
        public void SortCache()
        {
            _cache.Sort((p1, p2) => p2.PubDate.CompareTo(p1.PubDate));
        }

        protected bool IsAdmin()
        {
            return _contextAccessor.HttpContext?.User?.Identity.IsAuthenticated == true;
        }

        private static string CleanFromInvalidChars(string input)
        {
            // ToDo: what we are doing here if we switch the blog from windows
            // to unix system or vice versa? we should remove all invalid chars for both systems

            var regexSearch = Regex.Escape(new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars()));
            var r = new Regex($"[{regexSearch}]");
            return r.Replace(input, "");
        }

        public async Task<string> UploadImgurImageByBytesAsync(byte[] bytes)
        {
            //TODO:setting添加Imgur上傳功能。
            var client = new Imgur.API.Authentication.Impl.ImgurClient("", "");
            var endpoint = new Imgur.API.Endpoints.Impl.ImageEndpoint(client);
            return (await endpoint.UploadImageBinaryAsync(bytes)).Link;
        }
        #endregion

    }
}
