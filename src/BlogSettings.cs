namespace Miniblog.Core
{
    public class BlogSettings
    {
        public string Owner { get; set; } = "The Owner";
        public int PostsPerPage { get; set; } = 2;
        public int CommentsCloseAfterDays { get; set; } = 10;
        public bool ShowLogin { get; set; } = true;
        public bool MarkDown { get; set; } = false;
        public bool ShowPostTitle { get; set; } = true;
        public string[] ITIronManArticleURI { get; set; }
        public string ITIronManKeyCode { get; set; }="";
    }
}
