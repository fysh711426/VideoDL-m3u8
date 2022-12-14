using System.Linq;
using VideoDL_m3u8.DashParser;

namespace VideoDL_m3u8.Extensions
{
    public static class MpdExtension
    {
        public static Representation? GetWithHighestQualityVideo(
            this Period source, int? maxHeight = null)
        {
            var _maxHeight = maxHeight ?? int.MaxValue;
            return source.AdaptationSets
                .SelectMany(it => it.Representations)
                .Where(it => it.MimeType.StartsWith("video"))
                .Select(it => new
                {
                    quality = it.Height ?? 0,
                    bandwidth = it.Bandwidth ?? 0,
                    item = it
                })
                .Where(it => it.quality <= _maxHeight)
                .OrderByDescending(it => it.quality)
                .ThenByDescending(it => it.bandwidth)
                .FirstOrDefault()?
                .item;
        }

        public static Representation? GetWithHighestQualityAudio(
            this Period source, string? lang = null)
        {
            return source.AdaptationSets
                .SelectMany(it =>
                {
                    return it.Representations
                        .Select(itt => new
                        {
                            lang = it.Lang,
                            bandwidth = itt.Bandwidth ?? 0,
                            mimeType = itt.MimeType,
                            item = itt
                        });
                })
                .Where(it => it.mimeType.StartsWith("audio"))
                .Where(it => lang != null ? it.lang == lang : true)
                .OrderByDescending(it => it.bandwidth)
                .FirstOrDefault()?
                .item;
        }
    }
}
