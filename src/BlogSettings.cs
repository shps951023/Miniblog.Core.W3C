using Miniblog.Core.Services;

namespace Miniblog.Core
{
    public class Setting
    {
        public static BlogSettings sectionBlog;
        public static IBlogService blogService;
    }

    public class BlogSettings
    {
        public string Owner { get; set; } = "The Owner";
        public int PostsPerPage { get; set; } = 2;
        public int CommentsCloseAfterDays { get; set; } = 10;
        public bool ShowLogin { get; set; } = true;
        public bool MarkDown { get; set; } = false;
        public bool ShowPostTitle { get; set; } = true;
        public string SQLiteConnString { get; set; } = "";
        public string MSSQLConnString { get; set; } = "";
        public string[] ITIronManArticleURI { get; set; }
        public string ITIronManKeyCode { get; set; }="";
        public string[] ITIronManReplaceString { get; set; }
        public bool UseITIronManLocalLoader { get; set; } = false;
        public int ITIronManLocalLoaderInterval { get; set; } = 24;/*預設24小時*/
    }
}
