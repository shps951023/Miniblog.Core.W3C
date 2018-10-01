using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Parser.Html;

namespace Miniblog.Core.Helper
{
    public class HtmlHelper
    {
        public static string HtmlInnerText(string content)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }
            var parser = new HtmlParser();
            var document = parser.Parse(content);
            return string.Join("",document.DocumentElement.TextContent.Take(250));
        }
    }
}
