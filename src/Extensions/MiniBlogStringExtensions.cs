using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Miniblog.Core
{
    public static class MiniBlogStringExtensions
    {
        private static bool toLowerInvariant = true;
        public static bool GetToLowerInvariant() => toLowerInvariant;
        public static void SetToLowerInvariant(bool value) => toLowerInvariant = value;

        public static string MiniBlogToLowerInvariant(this string content)
        {
            if (toLowerInvariant)
                return content.ToLowerInvariant();
            else
                return content;
        }
    }
}
