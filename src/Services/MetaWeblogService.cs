using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using WilderMinds.MetaWeblog;
using Miniblog.Core.Helper;

namespace Miniblog.Core.Services
{
    public class MetaWeblogService : IMetaWeblogProvider
    {
        private readonly IBlogService _blog;
        private readonly IConfiguration _config;
        private readonly IUserServices _userServices;
        private readonly IHttpContextAccessor _context;

        public MetaWeblogService(IBlogService blog, IConfiguration config, IHttpContextAccessor context, IUserServices userServices)
        {
            _blog = blog;
            _config = config;
            _userServices = userServices;
            _context = context;
        }

        public string AddPost(string blogid, string username, string password, WilderMinds.MetaWeblog.Post post, bool publish)
        {
            ValidateUser(username, password);

            var newPost = new Models.Post
            {
                Title = post.title,
                //Done:假如mt_excerpt為null，取文章前N個字
                Excerpt= string.IsNullOrWhiteSpace(post.mt_excerpt)? HtmlHelper.HtmlInnerText(post.description): post.mt_excerpt,     
                Content = post.description,
                IsPublished = publish,
                Categories = post.categories
            };
            //TODO:做成可選擇GUID/時間格式/Title模式
            newPost.Slug = !string.IsNullOrWhiteSpace(post.wp_slug) ? post.wp_slug : newPost.ID;//(Models.Post.CreateSlug(post.title))

            if (post.dateCreated != DateTime.MinValue)
            {
                newPost.PubDate = post.dateCreated.ToUniversalTime();
            }

            _blog.SavePost(newPost).GetAwaiter().GetResult();

            return newPost.ID;
        }

        public bool DeletePost(string key, string postid, string username, string password, bool publish)
        {
            ValidateUser(username, password);

            var post = _blog.GetPostById(postid).GetAwaiter().GetResult();

            if (post != null)
            {
                _blog.DeletePost(post).GetAwaiter().GetResult();
                return true;
            }

            return false;
        }

        public bool EditPost(string postid, string username, string password, WilderMinds.MetaWeblog.Post post, bool publish)
        {
            ValidateUser(username, password);

            var existing = _blog.GetPostById(postid).GetAwaiter().GetResult();

            if (existing != null)
            {
                existing.Title = post.title;
                existing.Slug = post.wp_slug!=null? post.wp_slug : existing.Slug;
                existing.Content = post.description;
                existing.IsPublished = publish;
                existing.Categories = post.categories;
                //DONE:假如mt_excerpt為null，取文章前N個字
                existing.Excerpt = string.IsNullOrWhiteSpace(post.mt_excerpt) ? HtmlHelper.HtmlInnerText(post.description) : post.mt_excerpt;  


                if (post.dateCreated != DateTime.MinValue)
                {
                    //如果日期超過，以現在為準
                    if(post.dateCreated.ToUniversalTime()>DateTime.UtcNow)
                        existing.PubDate = DateTime.UtcNow;
                    else
                        existing.PubDate = post.dateCreated;
                }

                _blog.SavePost(existing).GetAwaiter().GetResult();

                return true;
            }

            return false;
        }

        public CategoryInfo[] GetCategories(string blogid, string username, string password)
        {
            ValidateUser(username, password);

            return _blog.GetCategories().GetAwaiter().GetResult()
                           .Select(cat =>
                               new CategoryInfo
                               {
                                   categoryid = cat,
                                   title = cat
                               })
                           .ToArray();
        }

        public WilderMinds.MetaWeblog.Post GetPost(string postid, string username, string password)
        {
            ValidateUser(username, password);

            var post = _blog.GetPostById(postid).GetAwaiter().GetResult();

            if (post != null)
            {
                return ToMetaWebLogPost(post);
            }

            return null;
        }

        public WilderMinds.MetaWeblog.Post[] GetRecentPosts(string blogid, string username, string password, int numberOfPosts)
        {
            ValidateUser(username, password);

            return _blog.GetPosts(numberOfPosts).GetAwaiter().GetResult().Select(ToMetaWebLogPost).ToArray();
        }

        public BlogInfo[] GetUsersBlogs(string key, string username, string password)
        {
            ValidateUser(username, password);

            var request = _context.HttpContext.Request;
            string url = request.Scheme + "://" + request.Host;

            return new[] { new BlogInfo {
                blogid ="1",
                blogName = _config["blog:name"] ?? nameof(MetaWeblogService),
                url = url
            }};
        }

        public MediaObjectInfo NewMediaObject(string blogid, string username, string password, MediaObject mediaObject)
        {
            ValidateUser(username, password);
            byte[] bytes = Convert.FromBase64String(mediaObject.bits);
            string path = _blog.SaveFileAsync(bytes, mediaObject.name).GetAwaiter().GetResult();

            return new MediaObjectInfo { url = path };
        }

        public UserInfo GetUserInfo(string key, string username, string password)
        {
            ValidateUser(username, password);
            throw new NotImplementedException();
        }

        public int AddCategory(string key, string username, string password, NewCategory category)
        {
            ValidateUser(username, password);
            throw new NotImplementedException();
        }

        private void ValidateUser(string username, string password)
        {
            if (_userServices.ValidateUser(username, password)==false)
            {
                throw new MetaWeblogException("Unauthorized");
            }

            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(ClaimTypes.Name, username));

            _context.HttpContext.User = new ClaimsPrincipal(identity);
        }

        private WilderMinds.MetaWeblog.Post ToMetaWebLogPost(Models.Post post)
        {
            var request = _context.HttpContext.Request;
            string url = request.Scheme + "://" + request.Host;

            return new WilderMinds.MetaWeblog.Post
            {
                postid = post.ID,
                title = post.Title,
                wp_slug = post.ID,
                permalink = url + post.GetLink(),
                dateCreated = post.PubDate,
                description = post.Content,
                categories = post.Categories.ToArray()
            };
        }
    }
}
