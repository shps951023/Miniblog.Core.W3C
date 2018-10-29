using AngleSharp.Parser.Html;
using Microsoft.Extensions.Hosting;
using Miniblog.Core.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Miniblog.Core.Services
{

    public class ITIronManSyncPostTimedHostedService : IHostedService, IDisposable
    {
        private Timer _timer;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var time = TimeSpan.FromHours(Setting.sectionBlog.ITIronManLocalLoaderInterval);
            _timer = new Timer(DoWork, null, time, time);
            return Task.CompletedTask;
        }

        private void DoWork(object state)
        {
            if(ITIronManSyncPostService.SyncPostAsync().GetAwaiter().IsCompleted)
                throw new Exception("Excute failed");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }

    public class ITIronManSyncPostService
    {
        private static readonly HtmlParser _parser = new HtmlParser();
        public IList<Post> Posts { get; set; } = new List<Post>();
        private string _url { get; set; }

        public static async Task SyncPostAsync()
        {
            var sectionBlog = Setting.sectionBlog;
            var blogService = Setting.blogService;
            foreach (var item in sectionBlog.ITIronManArticleURI)
            {
                var lstITposts = ITIronManSyncPostService.GetITIronManPosts(item).Result;
                foreach (var itpost in lstITposts)
                {
                    //檢查有沒有資料，如果有資料更新動作
                    var id = itpost.link.Replace("https://ithelp.ithome.com.tw/articles/", "");
                    var post = blogService.GetPostById(id).Result;
                    if (post != null)
                    {
                        post.Content = $"IT鐵人賽連結:<a href='{itpost.link}'>{itpost.link}</a><br/>" + itpost.Content;
                        post.Excerpt = $"IT鐵人賽連結:<a href='{itpost.link}'>{itpost.link}</a><br/>" + HtmlHelper.HtmlInnerText(itpost.Content);
                        var title = itpost.Title;
                        foreach (var replaceString in sectionBlog.ITIronManReplaceString)
                        {
                            title = title.Replace(replaceString, "");
                        }
                        post.Title = title;
                        post.Categories = new string[] { itpost.Article };
                    }
                    //檢查有沒有資料，如果沒有資料做新增動作
                    else
                    {
                        post = new Miniblog.Core.Models.Post()
                        {
                            ID = id,
                            Categories = new string[] { itpost.Article },
                            Content = itpost.Content,
                            IsMarkDown = true,
                            PubDate = itpost.PubDate,
                            Slug = id,
                            IsPublished = true
                        };
                        post.Excerpt = $"IT鐵人賽連結:<a href='{itpost.link}'>{itpost.link}</a><br/>" + HtmlHelper.HtmlInnerText(itpost.Content);
                        var title = itpost.Title;
                        foreach (var replaceString in sectionBlog.ITIronManReplaceString)
                        {
                            title = title.Replace(replaceString, "");
                        }
                        post.Title = title;
                    }
                    await blogService.SavePost(post);
                };
            }
        }

        public async static Task<IList<Post>> GetITIronManPosts(string url)
        {
            var itironman = new ITIronManSyncPostService();
            itironman._url = url;
            await itironman.ExecuteAsync();
            return itironman.Posts;
        }

        private async Task ExecuteAsync()
        {
            //因為IT鐵人賽只需要三十篇文章，每頁10篇文章，抓取頁數取4頁就好
            for (int i = 1; i < 4; i++)
            {
                await GetITIronManPostsAsync(_url + $"?page={i}");
            }
        }

        private async Task GetITIronManPostsAsync(string url)
        {
            var htmlContent = (await GetAsync(url));
            var document = _parser.Parse(htmlContent);

            //獲取鐵人賽主題
            var article = document.QuerySelector(".qa-list__title--ironman");
            article.RemoveChild(article.QuerySelector("span"));/*移除系列文字*/
            var articleText = article.TextContent.Trim();

            //獲取鐵人賽:發布日期、標題、內容、連結
            var allpost = document.QuerySelectorAll(".profile-list__content");
            foreach (var postInfo in allpost)
            {
                var post = new Post();

                var titleAndLinkDom = postInfo.QuerySelector(".qa-list__title>a");
                post.Title = titleAndLinkDom.InnerHtml.Trim();
                post.link = titleAndLinkDom.GetAttribute("href").Trim();
                post.Content = GetPostContentAsync(post.link).Result.Trim();
                post.PubDate = DateTime.Parse(postInfo.QuerySelector(".qa-list__info>.qa-list__info-time").GetAttribute("title").Trim());
                post.Article = articleText;

                Posts.Add(post);
            }
        }

        private async Task<string> GetPostContentAsync(string posturl)
        {
            var htmlContent = (await GetAsync(posturl));
            var document = _parser.Parse(htmlContent);
            return document.QuerySelectorAll(".markdown__style").FirstOrDefault().InnerHtml;
        }

        public async Task<string> GetAsync(string uri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                return await reader.ReadToEndAsync();
            }
        }

        public class Post
        {
            public string Title { get; set; }
            public string link { get; set; }
            public string Content { get; set; }
            public string Article { get; set; }
            public DateTime PubDate { get; set; }
        }
    }
}
