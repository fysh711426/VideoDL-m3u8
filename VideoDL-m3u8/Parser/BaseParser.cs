using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VideoDL_m3u8.Parser
{
    internal class BaseParser
    {
        protected Dictionary<string, string> ParseAttributes(string attributes)
        {
            var result = new Dictionary<string, string>();
            var matches = Regex.Matches(attributes,
                @"([^=]*)=((?:"".*?"",)|(?:.*?,)|(?:.*?$))");
            foreach (Match match in matches)
            {
                var key = match.Groups[1].Value.Trim();
                var val = match.Groups[2].Value.Trim();
                val = Regex.Replace(val, @"^['""]?(.*?)['""]?[,]?$", "$1");
                result[key] = val;
            }
            return result;
        }
    }
}
