using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using VideoDL_m3u8.Parser;

namespace VideoDL_m3u8.Extensions
{
    public static class HlsExtension
    {
        public static bool IsMaster(this string manifest)
        {
            return manifest.Contains("#EXT-X-STREAM-INF");
        }

        public static StreamInfo? GetWithHighestQuality(
            this IEnumerable<StreamInfo> source, int? maxHeight = null)
        {
            var _maxHeight = maxHeight ?? int.MaxValue;
            return source
                .Select(it => new
                {
                    quality = it.Resolution?.Height ?? 0,
                    item = it
                })
                .Where(it => it.quality <= _maxHeight)
                .OrderByDescending(it => it.quality)
                .FirstOrDefault()?
                .item;
        }

        public static string CombineUri(this string m3u8Url, string uri)
        {
            if (uri.Contains("http"))
                return uri;
            m3u8Url = Regex.Match(m3u8Url, @"(.*?\/)+").Value;
            // m3u8Url = m3u8Url.TrimEnd('/');
            return new Uri(new Uri(m3u8Url), uri).ToString();
        }
    }
}
