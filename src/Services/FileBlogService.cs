using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Miniblog.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;


namespace Miniblog.Core.Services
{
    public class FileBlogService : InMemoryBlogServiceBase
    {
        public FileBlogService(IHostingEnvironment env, IHttpContextAccessor contextAccessor)
        {
            _folder = Path.Combine(env.WebRootPath, POSTS);
            _contextAccessor = contextAccessor;

            Initialize();
        }

        #region 改寫部分(override)
        public override async Task SavePost(Post post)
        {
            string filePath = GetFilePath(post);
            post.LastModified = DateTime.UtcNow;

            XDocument doc = new XDocument(
                            new XElement("post",
                                new XElement("title", post.Title),
                                new XElement("slug", post.Slug),
                                new XElement("pubDate", FormatDateTime(post.PubDate)),
                                new XElement("lastModified", FormatDateTime(post.LastModified)),
                                new XElement("excerpt", post.Excerpt),
                                new XElement("content", post.Content),
                                new XElement("markDownContent", post.MarkDownContent),
                                new XElement("isMarkDown", post.IsMarkDown),
                                new XElement("ispublished", post.IsPublished),
                                new XElement("categories", string.Empty),
                                new XElement("comments", string.Empty)
                            ));

            XElement categories = doc.XPathSelectElement("post/categories");
            foreach (string category in post.Categories)
            {
                categories.Add(new XElement("category", category));
            }

            XElement comments = doc.XPathSelectElement("post/comments");
            foreach (Comment comment in post.Comments)
            {
                comments.Add(
                    new XElement("comment",
                        new XElement("author", comment.Author),
                        new XElement("email", comment.Email),
                        new XElement("date", FormatDateTime(comment.PubDate)),
                        new XElement("content", comment.Content),
                        new XAttribute("isAdmin", comment.IsAdmin),
                        new XAttribute("id", comment.ID)
                    ));
            }

            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite))
            {
                await doc.SaveAsync(fs, SaveOptions.None, CancellationToken.None).ConfigureAwait(false);
            }

            if (!_cache.Contains(post))
            {
                _cache.Add(post);
                SortCache();
            }
            ReloadCacheData();
        }

        public override Task DeletePost(Post post)
        {
            string filePath = GetFilePath(post);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            if (_cache.Contains(post))
            {
                _cache.Remove(post);
            }
            ReloadCacheData();

            return Task.CompletedTask;
        }

        public override async Task<string> SaveFileAsync(byte[] bytes, string fileName, string suffix = null)
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

        #region 不屬於介面方法(資料存取)
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
            .GroupBy(g => g.CatName).ToList().ForEach(p=>
            {
                _cachePostGroupByCat.Add(p);
            });
        }

        private void LoadPosts()
        {
            if (!Directory.Exists(_folder))
            {
                Directory.CreateDirectory(_folder);
            }

            // Can this be done in parallel to speed it up?
            foreach (string file in Directory.EnumerateFiles(_folder, "*.xml", SearchOption.TopDirectoryOnly))
            {
                XElement doc = XElement.Load(file);

                Post post = new Post
                {
                    ID = Path.GetFileNameWithoutExtension(file),
                    Title = ReadValue(doc, "title"),
                    Excerpt = ReadValue(doc, "excerpt"),
                    Content = ReadValue(doc, "content"),
                    MarkDownContent = ReadValue(doc, "markDownContent"),
                    Slug = ReadValue(doc, "slug").MiniBlogToLowerInvariant(),
                    PubDate = DateTime.Parse(ReadValue(doc, "pubDate")).ToUniversalTime(),
                    LastModified = DateTime.Parse(ReadValue(doc, "lastModified", DateTime.UtcNow.ToString(CultureInfo.InvariantCulture))).ToUniversalTime(),
                    IsPublished = bool.Parse(ReadValue(doc, "ispublished", "true")),
                    IsMarkDown = bool.Parse(ReadValue(doc, "isMarkDown", "false")),
                };

                LoadCategories(post, doc);
                LoadComments(post, doc);
                _cache.Add(post);
            }
        }

        private static void LoadCategories(Post post, XElement doc)
        {
            XElement categories = doc.Element("categories");
            if (categories == null)
            {
                return;
            }

            List<string> list = new List<string>();

            foreach (var node in categories.Elements("category"))
            {
                list.Add(node.Value);
            }

            post.Categories = list.ToArray();
        }

        private static void LoadComments(Post post, XElement doc)
        {
            var comments = doc.Element("comments");

            if (comments == null)
            {
                return;
            }

            foreach (var node in comments.Elements("comment"))
            {
                Comment comment = new Comment()
                {
                    ID = ReadAttribute(node, "id"),
                    Author = ReadValue(node, "author"),
                    Email = ReadValue(node, "email"),
                    IsAdmin = bool.Parse(ReadAttribute(node, "isAdmin", "false")),
                    Content = ReadValue(node, "content"),
                    PubDate = DateTime.Parse(ReadValue(node, "date", "2000-01-01")),
                };

                post.Comments.Add(comment);
            }
        }
        #endregion

        #region 不屬於介面方法(輔助)
        private string GetFilePath(Post post)
        {
            return Path.Combine(_folder, post.ID + ".xml");
        }

        private static string ReadValue(XElement doc, XName name, string defaultValue = "")
        {
            if (doc.Element(name) != null)
            {
                return doc.Element(name)?.Value;
            }

            return defaultValue;
        }

        private static string ReadAttribute(XElement element, XName name, string defaultValue = "")
        {
            if (element.Attribute(name) != null)
            {
                return element.Attribute(name)?.Value;
            }

            return defaultValue;
        }

        private static string CleanFromInvalidChars(string input)
        {
            // ToDo: what we are doing here if we switch the blog from windows
            // to unix system or vice versa? we should remove all invalid chars for both systems

            var regexSearch = Regex.Escape(new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars()));
            var r = new Regex($"[{regexSearch}]");
            return r.Replace(input, "");
        }

        private static string FormatDateTime(DateTime dateTime)
        {
            const string UTC = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'";

            return dateTime.Kind == DateTimeKind.Utc
                ? dateTime.ToString(UTC)
                : dateTime.ToUniversalTime().ToString(UTC);
        }
        #endregion
    }
}