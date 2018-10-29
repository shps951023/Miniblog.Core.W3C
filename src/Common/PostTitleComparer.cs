using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Miniblog.Core
{
    //邏輯:
    //	1.實作 `IComparer` 介面
    public class PostTitleComparer : IComparer<string>
    {
        //	2.使用正則抓出`()`裡面的數字值
        static int UseRegexGetTitleNumber(string text)
        {
            Match match = Regex.Match(text, @"(\d+)");
            if (match == null)
                return 0;
            int value;
            if (!int.TryParse(match.Value, out value))
                return 0;
            return value;
        }

        //	3.實作` Compare 比較方法`，如果前者大於後者返回1整數，如果前者等於後者返回0整數，如果前者小於後者返回 - 1。藉此來比較大小作排序。
        public int Compare(string s1, string s2)
        {
            var s1Val = UseRegexGetTitleNumber(s1);
            var s2Val = UseRegexGetTitleNumber(s2);
            if (s1Val > s2Val) return 1;
            if (s1Val < s2Val) return -1;
            if (s1Val == s2Val) return 0;
            return string.Compare(s1, s2, true);
        }
    }
}
