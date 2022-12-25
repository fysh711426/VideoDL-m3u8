using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VideoDL_m3u8.Extensions
{
    internal static class RegexExtension
    {
        public static IEnumerable<TResult> Select<TResult>(this MatchCollection matches, Func<Match, TResult> selector)
        {
            foreach (Match match in matches)
            {
                yield return selector(match);
            }
        }
    }
}
