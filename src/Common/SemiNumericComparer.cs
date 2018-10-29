using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Miniblog.Core
{
    public class SemiNumericComparer : IComparer<string>
    {

        static int ExtractNumber(string text)
        {
            Match match = Regex.Match(text, @"(\d+)");
            if (match == null)
            {
                return 0;
            }

            int value;
            if (!int.TryParse(match.Value, out value))
            {
                return 0;
            }

            return value;
        }

        public int Compare(string s1, string s2)
        {
            var s1Val = ExtractNumber(s1);
            var s2Val = ExtractNumber(s2);
            if (s1Val > s2Val) return 1;
            if (s1Val < s2Val) return -1;
            if (s1Val == s2Val) return 0;
            return string.Compare(s1, s2, true);
        }
    }
}
