using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace VideoDL_m3u8.Parser
{
    public class BaseParser
    {
        //internal Dictionary<string, string> ParseAttributes(string attributes)
        //{
        //    var result = new Dictionary<string, string>();
        //    var attrs = attributes.Split(",", StringSplitOptions.RemoveEmptyEntries);
        //    foreach (var attr in attrs)
        //    {
        //        var match = Regex.Match(attr.Trim(), @"([^=]*)=(.*)");
        //        var key = match.Groups[1].Value.Trim();
        //        var val = match.Groups[2].Value.Trim();
        //        val = Regex.Replace(val, @"^['""](.*)['""]$", "$1");
        //        result[key] = val;
        //    }
        //    return result;
        //}

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
