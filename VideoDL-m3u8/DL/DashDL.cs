using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VideoDL_m3u8.DashParser;
using VideoDL_m3u8.Extensions;
using VideoDL_m3u8.Parser;

namespace VideoDL_m3u8.DL
{
    public class DashDL : BaseDL
    {
        protected readonly HttpClient _httpClient;

        /// <summary>
        /// Init DashDL.
        /// </summary>
        /// <param name="timeout">Set http request timeout.(millisecond)</param>
        /// <param name="proxy">Set http or socks5 proxy.
        /// http://{hostname}:{port} or socks5://{hostname}:{port}</param>
        public DashDL(int timeout = 60000, string? proxy = null)
            : this(CreateHttpClient(timeout, proxy))
        {
        }

        /// <summary>
        /// Init DashDL.
        /// </summary>
        /// <param name="httpClient">Set http client.</param>
        public DashDL(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Get mpd manifest by url.
        /// </summary>
        /// <param name="url">Set mpd download url.</param>
        /// <param name="header">Set http request header.
        /// format: key1:key1|key2:key2</param>
        /// <param name="token">Set cancellation token.</param>
        /// <returns></returns>
        public async Task<(string data, string url)> GetManifestAsync(
            string url, string header = "", CancellationToken token = default)
        {
            return await GetStringAsync(_httpClient, url, header, token);
        }

        /// <summary>
        /// Get mpd by url.
        /// </summary>
        /// <param name="url">Set mpd download url.</param>
        /// <param name="header">Set http request header.
        /// format: key1:key1|key2:key2</param>
        /// <param name="token">Set cancellation token.</param>
        /// <returns></returns>
        public async Task<Mpd> GetMpdAsync(
            string url, string header = "", CancellationToken token = default)
        {
            var manifest = await GetManifestAsync(url, header, token);
            var parser = new MpdParser();
            return parser.Parse(manifest.data, manifest.url);
        }

        /// <summary>
        /// Parse mpd by manifest.
        /// </summary>
        /// <param name="manifest">Set mpd manifest.</param>
        /// <param name="url">Set mpd url.</param>
        /// <returns></returns>
        public Mpd ParseMpd(string manifest, string url = "")
        {
            var parser = new MpdParser();
            return parser.Parse(manifest, url);
        }

        /// <summary>
        /// Expand segmentBase to segmentList.
        /// </summary>
        /// <param name="mpd">Set mpd.</param>
        /// <param name="url">Set mpd url.</param>
        /// <param name="header">Set http request header.
        /// format: key1:key1|key2:key2</param>
        /// <param name="token">Set cancellation token.</param>
        /// <returns></returns>
        public async Task ExpandSegmentBase(Mpd mpd, 
            string url = "", string header = "", CancellationToken token = default)
        {
            var baseUrl = url;
            if (!string.IsNullOrEmpty(mpd.BaseUrl))
                baseUrl = string.IsNullOrEmpty(baseUrl) ?
                    mpd.BaseUrl : baseUrl.CombineUri(mpd.BaseUrl);

            foreach (var period in mpd.Periods)
            {
                foreach (var adaptationSet in period.AdaptationSets)
                {
                    var adaBaseUrl = baseUrl;
                    if (!string.IsNullOrEmpty(adaptationSet.BaseUrl))
                        adaBaseUrl = string.IsNullOrEmpty(adaBaseUrl) ?
                            adaptationSet.BaseUrl : adaBaseUrl.CombineUri(adaptationSet.BaseUrl);

                    foreach (var representation in adaptationSet.Representations)
                    {
                        var repBaseUrl = adaBaseUrl;
                        if (!string.IsNullOrEmpty(representation.BaseUrl))
                            repBaseUrl = string.IsNullOrEmpty(repBaseUrl) ?
                                representation.BaseUrl : repBaseUrl.CombineUri(representation.BaseUrl);

                        var segmentList = representation.SegmentList;
                        var segmentBase = representation.SegmentBase;
                        if (segmentBase != null)
                        {
                            var sidxUrl = repBaseUrl;
                            var rangeFrom = segmentBase.IndexRange?.From;
                            var rangeTo = segmentBase.IndexRange?.To;

                            if (rangeFrom == null || rangeTo == null)
                                throw new Exception("Not found SegmentBase IndexRange.");

                            var (data, _) = await GetBytesAsync(_httpClient,
                                sidxUrl, header, rangeFrom, rangeTo, token);

                            var parser = new SidxParser();
                            var sidx = parser.Parse(data);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Convert representation to m3u8 manifest.
        /// </summary>
        /// <param name="representation">Set mpd representation.</param>
        /// <returns></returns>
        public string ToM3U8(Representation representation)
        {
            var m3u8 = new StringBuilder();
            var segmentList = representation.SegmentList;

            m3u8.AppendLine("#EXTM3U");
            m3u8.AppendLine("#EXT-X-VERSION:3");
            m3u8.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");

            if (segmentList.Initialization != null)
            {
                var initialization = segmentList.Initialization;
                m3u8.Append($@"#EXT-X-MAP:URI=""{initialization.SourceURL}""");
                if (initialization.Range != null)
                {
                    var from = initialization.Range.From;
                    var to = initialization.Range.To;
                    m3u8.Append($@",BYTERANGE=""{to - from + 1}@{from}""");
                }
                m3u8.AppendLine();
            }

            foreach (var segmentUrl in segmentList.SegmentUrls)
            {
                var duration = (double)segmentUrl.Duration / segmentUrl.Timescale;
                m3u8.AppendLine($"#EXTINF:{duration.ToString("0.00")}");
                if (segmentUrl.MediaRange != null)
                {
                    var from = segmentUrl.MediaRange.From;
                    var to = segmentUrl.MediaRange.To;
                    m3u8.AppendLine($"#EXT-X-BYTERANGE:{to - from + 1}@{from}");
                }
                m3u8.AppendLine(segmentUrl.Media);
            }

            m3u8.AppendLine("#EXT-X-ENDLIST");
            return m3u8.ToString();
        }

        /// <summary>
        /// Convert representation to media playlist.
        /// </summary>
        /// <param name="representation">Set mpd representation.</param>
        /// <param name="url">Set mpd url.</param>
        /// <returns></returns>
        public MediaPlaylist ToMediaPlaylist(Representation representation, string url = "")
        {
            var parser = new MediaPlaylistParser();
            var manifest = ToM3U8(representation);
            return parser.Parse(manifest, url);
        }
    }
}
