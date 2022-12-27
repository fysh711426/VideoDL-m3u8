using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VideoDL_m3u8.DashParser;
using VideoDL_m3u8.DL;
using VideoDL_m3u8.Enums;
using VideoDL_m3u8.Extensions;
using VideoDL_m3u8.Parser;

namespace VideoDL_m3u8
{
    public class VideoDL : BaseDL
    {
        protected readonly HttpClient _httpClient;

        /// <summary>
        /// HlsDL instance.
        /// </summary>
        public HlsDL Hls { get; set; }

        /// <summary>
        /// DashDL instance.
        /// </summary>
        public DashDL Dash { get; set; }

        /// <summary>
        /// HttpDL instance.
        /// </summary>
        public HttpDL Http { get; set; }

        /// <summary>
        /// Init VideoDL.
        /// </summary>
        /// <param name="timeout">Set http request timeout.(millisecond)</param>
        /// <param name="proxy">Set http or socks5 proxy.
        /// http://{hostname}:{port} or socks5://{hostname}:{port}</param>
        public VideoDL(int timeout = 60000, string? proxy = null)
            : this(CreateHttpClient(timeout, proxy))
        {
        }

        /// <summary>
        /// Init VideoDL.
        /// </summary>
        /// <param name="httpClient">Set http client.</param>
        public VideoDL(HttpClient httpClient)
        {
            _httpClient = httpClient;
            Hls = new HlsDL(httpClient);
            Dash = new DashDL(httpClient);
            Http = new HttpDL(httpClient);
        }

        /// <summary>
        /// Download m3u8/mpd/http video.
        /// </summary>
        /// <param name="workDir">Set video download directory.</param>
        /// <param name="saveName">Set video save name.</param>
        /// <param name="url">Set m3u8/mpd/http url.</param>
        /// <param name="header">Set http request header.
        /// format: key1:key1|key2:key2</param>
        /// <param name="threads">Set the number of threads to download.</param>
        /// <param name="delay">Set http request delay.(millisecond)</param>
        /// <param name="maxRetry">Set the maximum number of download retries.</param>
        /// <param name="maxSpeed">Set the maximum download speed.(byte)
        /// 1KB = 1024 byte, 1MB = 1024 * 1024 byte</param>
        /// <param name="interval">Set the progress callback time interval.(millisecond)</param>
        /// <param name="checkComplete">Set whether to check file count complete.</param>
        /// <param name="noSegStopTime">Set how long to stop after when there is no segment.(millisecond)</param>
        /// <param name="binaryMerge">Set use binary merge.</param>
        /// <param name="keepFragmented">Set keep fragmented mp4.</param>
        /// <param name="discardcorrupt">Set ffmpeg discard corrupted packets.</param>
        /// <param name="genpts">Set ffmpeg generate missing PTS if DTS is present.</param>
        /// <param name="igndts">Set ffmpeg ignore DTS if PTS is set.</param>
        /// <param name="ignidx">Set ffmpeg ignore index.</param>
        /// <param name="outputFormat">Set video output format.</param>
        /// <param name="clearTempFile">Set whether to clear the temporary file after the merge is completed.</param>
        /// <param name="clearSource">Set whether to clear source file after the muxing is completed.</param>
        /// <param name="partFilter">Set m3u8 part filter.</param>
        /// <param name="periodFilter">Set mpd period filter.</param>
        /// <param name="token">Set cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public virtual async Task DownloadAsync(
            string workDir, string saveName, string url, string header = "",
            Func<List<Part>, List<Part>>? partFilter = null,
            Func<List<Period>, List<Period>>? periodFilter = null,
            int threads = 1, int delay = 200, int maxRetry = 20, long? maxSpeed = null, 
            int interval = 1000, bool checkComplete = true, int? noSegStopTime = null,
            bool binaryMerge = false, bool keepFragmented = false, bool discardcorrupt = false,
            bool genpts = false, bool igndts = false, bool ignidx = false,
            OutputFormat outputFormat = OutputFormat.MP4,
            bool clearTempFile = false, bool clearSource = false,
            CancellationToken token = default)
        {
            var hlsDL = Hls;
            var dashDL = Dash;
            var httpDL = Http;

            saveName = saveName.FilterFileName();

            Console.WriteLine("Start Download...");

            var m3u8Url = "";
            var manifest = "";
            var isM3U8 = false;
            var isMPD = false;

            try
            {
                m3u8Url = await LoadStreamAsync(_httpClient, url, header,
                    async (stream, contentLength, charSet) =>
                    {
                        var buffer = new byte[1024];
                        var size = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                        var check = Encoding.GetEncoding("ISO-8859-1")
                            .GetString(buffer, 0, size);
                        (isM3U8, isMPD) = ((Func<(bool, bool)>)(() =>
                        {
                            if (check.IsM3U8())
                                return (true, false);
                            if (check.IsMPD())
                                return (false, true);
                            return (false, false);
                        }))();
                        if (!isM3U8 && !isMPD)
                            throw new Exception("Stop http stream.");
                        var encoding = Encoding.UTF8;
                        if (!string.IsNullOrWhiteSpace(charSet))
                            encoding = Encoding.GetEncoding(charSet);
                        var builder = new StringBuilder();
                        builder.Append(encoding.GetString(buffer, 0, size));
                        while (true)
                        {
                            size = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                            if (size <= 0)
                                break;
                            builder.Append(encoding.GetString(buffer, 0, size));
                        }
                        manifest = builder.ToString();
                    },
                    token: token);
            }
            catch (Exception ex)
            {
                if (ex.Message != "Stop http stream.")
                    throw;
            }

            if (isM3U8 || isMPD)
            {
                var audioPlaylist = null as MediaPlaylist;
                var mediaPlaylist = null as MediaPlaylist;

                // isM3U8
                if (isM3U8)
                {
                    // isMaster
                    if (manifest.IsMaster())
                    {
                        var masterPlaylist = hlsDL.ParseMasterPlaylist(manifest, m3u8Url);

                        var highestStreamInfo = masterPlaylist.StreamInfos
                            .GetWithHighestQuality();
                        if (highestStreamInfo == null)
                            throw new Exception("Not found stream info.");
                        (manifest, m3u8Url) = await hlsDL.GetManifestAsync(
                            highestStreamInfo.Uri, header, token);

                        var audioMediaGroup = masterPlaylist.MediaGroups
                            .Where(it => it.GroupId == highestStreamInfo.Audio)
                            .FirstOrDefault();
                        if (audioMediaGroup != null)
                            audioPlaylist = await hlsDL.GetMediaPlaylistAsync(
                                audioMediaGroup.Uri, header);
                    }
                    mediaPlaylist = hlsDL.ParseMediaPlaylist(manifest, m3u8Url);
                }

                // isMPD
                if (isMPD)
                {
                    var mpd = dashDL.ParseMpd(manifest, m3u8Url);

                    var periods = mpd.Periods;
                    if (periodFilter != null)
                        periods = periodFilter(mpd.Periods);

                    var period = periods.FirstOrDefault();
                    if (period == null)
                        throw new Exception("Not found mpd period.");
                    var video = period.GetWithHighestQualityVideo();
                    var audio = period.GetWithHighestQualityAudio();

                    if (video != null && audio != null)
                    {
                        await dashDL.ExpandSegmentBase(video, header);
                        mediaPlaylist = dashDL.ToMediaPlaylist(video);

                        await dashDL.ExpandSegmentBase(audio, header);
                        audioPlaylist = dashDL.ToMediaPlaylist(audio);
                    }
                    else if (video != null)
                    {
                        await dashDL.ExpandSegmentBase(video, header);
                        mediaPlaylist = dashDL.ToMediaPlaylist(video);
                    }
                    else if (audio != null)
                    {
                        await dashDL.ExpandSegmentBase(audio, header);
                        mediaPlaylist = dashDL.ToMediaPlaylist(audio);
                    }
                }

                if (mediaPlaylist == null)
                    throw new Exception("Not found media playlist.");

                // isLive
                if (mediaPlaylist.IsLive())
                {
                    try
                    {
                        await hlsDL.REC(
                            workDir, saveName, m3u8Url, header,
                            maxRetry: maxRetry, maxSpeed: maxSpeed,
                            interval: interval, noSegStopTime: noSegStopTime,
                            progress: (args) =>
                            {
                                var print = args.Format;
                                var sub = Console.WindowWidth - 2 - print.Length;
                                Console.Write("\r" + print + new string(' ', sub) + "\r");
                            },
                            token: token);
                    }
                    catch { }

                    Console.WriteLine("\nStart Merge...");

                    await hlsDL.MergeAsync(workDir, saveName,
                        outputFormat: outputFormat, binaryMerge: binaryMerge,
                        keepFragmented: keepFragmented, discardcorrupt: discardcorrupt,
                        genpts: genpts, igndts: igndts, ignidx: ignidx,
                        clearTempFile: clearTempFile,
                        onMessage: (msg) =>
                        {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.Write(msg);
                            Console.ResetColor();
                        },
                        token: token);

                    Console.WriteLine("Finish.");
                    return;
                }

                // hasAudio
                if (audioPlaylist != null)
                {
                    var videoSaveName = $"{saveName}(Video)";
                    var audioSaveName = $"{saveName}(Audio)";

                    var videoPath = await downloadMerge("video", videoSaveName, mediaPlaylist);
                    var audioPath = await downloadMerge("audio", audioSaveName, audioPlaylist);

                    var muxOutputFormat = outputFormat == OutputFormat.TS ?
                        MuxOutputFormat.TS : MuxOutputFormat.MP4;

                    await hlsDL.MuxingAsync(
                        workDir, saveName, videoPath, audioPath,
                        outputFormat: muxOutputFormat,
                        clearSource: clearSource,
                        onMessage: (msg) =>
                        {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.Write(msg);
                            Console.ResetColor();
                        },
                        token: token);

                    Console.WriteLine("Finish.");
                    return;

                    async Task<string> downloadMerge(string id, string saveName, MediaPlaylist mediaPlaylist)
                    {
                        var parts = mediaPlaylist.Parts;
                        if (isM3U8)
                        {
                            if (partFilter != null)
                                parts = partFilter(mediaPlaylist.Parts);
                        }

                        var keys = null as Dictionary<string, string>;
                        var segmentKeys = Hls.GetKeys(parts);
                        if (segmentKeys.Count > 0)
                            keys = await Hls.GetKeysDataAsync(segmentKeys, header, token);

                        Console.WriteLine($"Start {id} Download...");

                        await hlsDL.DownloadAsync(
                            workDir, saveName, parts, header, keys,
                            threads: threads, delay: delay, maxRetry: maxRetry,
                            maxSpeed: maxSpeed, interval: interval,
                            checkComplete: checkComplete,
                            onSegment: async (ms, token) =>
                            {
                                return await ms.TrySkipPngHeaderAsync(token);
                            },
                            progress: (args) =>
                            {
                                var print = args.Format;
                                var sub = Console.WindowWidth - 2 - print.Length;
                                Console.Write("\r" + print + new string(' ', sub) + "\r");
                            },
                            token: token);

                        Console.WriteLine($"\nStart {id} Merge...");

                        var outputPath = await hlsDL.MergeAsync(workDir, saveName,
                            clearTempFile: clearTempFile, binaryMerge: true,
                            outputFormat: OutputFormat.MP4,
                            onMessage: (msg) =>
                            {
                                Console.ForegroundColor = ConsoleColor.DarkYellow;
                                Console.Write(msg);
                                Console.ResetColor();
                            },
                            token: token);
                        return outputPath;
                    }
                }

                // hasVideo
                {
                    var parts = mediaPlaylist.Parts;
                    if (isM3U8)
                    {
                        if (partFilter != null)
                            parts = partFilter(mediaPlaylist.Parts);
                    }

                    var keys = null as Dictionary<string, string>;
                    var segmentKeys = hlsDL.GetKeys(parts);
                    if (segmentKeys.Count > 0)
                        keys = await hlsDL.GetKeysDataAsync(segmentKeys, header);

                    await hlsDL.DownloadAsync(
                        workDir, saveName, parts, header, keys,
                        threads: threads, delay: delay, maxRetry: maxRetry,
                        maxSpeed: maxSpeed, interval: interval,
                        checkComplete: checkComplete,
                        onSegment: async (ms, token) =>
                        {
                            return await ms.TrySkipPngHeaderAsync(token);
                        },
                        progress: (args) =>
                        {
                            var print = args.Format;
                            var sub = Console.WindowWidth - 2 - print.Length;
                            Console.Write("\r" + print + new string(' ', sub) + "\r");
                        },
                        token: token);

                    Console.WriteLine("\nStart Merge...");

                    await hlsDL.MergeAsync(workDir, saveName,
                        outputFormat: outputFormat, binaryMerge: binaryMerge,
                        keepFragmented: keepFragmented, discardcorrupt: discardcorrupt,
                        genpts: genpts, igndts: igndts, ignidx: ignidx,
                        clearTempFile: clearTempFile,
                        onMessage: (msg) =>
                        {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.Write(msg);
                            Console.ResetColor();
                        },
                        token: token);

                    Console.WriteLine("Finish.");
                    return;
                }
            }

            // http
            await httpDL.DownloadAsync(workDir, saveName,
                url, header, threads: threads, delay: delay,
                maxRetry: maxRetry, maxSpeed: maxSpeed, interval: interval,
                progress: (args) =>
                {
                    var print = args.Format;
                    var sub = Console.WindowWidth - 2 - print.Length;
                    Console.Write("\r" + print + new string(' ', sub) + "\r");
                },
                token: token);

            Console.WriteLine("\nFinish.");
        }

        /// <summary>
        /// Download m3u8 video.
        /// </summary>
        /// <param name="workDir">Set video download directory.</param>
        /// <param name="saveName">Set video save name.</param>
        /// <param name="url">Set m3u8/mpd/http url.</param>
        /// <param name="header">Set http request header.
        /// format: key1:key1|key2:key2</param>
        /// <param name="threads">Set the number of threads to download.</param>
        /// <param name="delay">Set http request delay.(millisecond)</param>
        /// <param name="maxRetry">Set the maximum number of download retries.</param>
        /// <param name="maxSpeed">Set the maximum download speed.(byte)
        /// 1KB = 1024 byte, 1MB = 1024 * 1024 byte</param>
        /// <param name="interval">Set the progress callback time interval.(millisecond)</param>
        /// <param name="checkComplete">Set whether to check file count complete.</param>
        /// <param name="noSegStopTime">Set how long to stop after when there is no segment.(millisecond)</param>
        /// <param name="binaryMerge">Set use binary merge.</param>
        /// <param name="keepFragmented">Set keep fragmented mp4.</param>
        /// <param name="discardcorrupt">Set ffmpeg discard corrupted packets.</param>
        /// <param name="genpts">Set ffmpeg generate missing PTS if DTS is present.</param>
        /// <param name="igndts">Set ffmpeg ignore DTS if PTS is set.</param>
        /// <param name="ignidx">Set ffmpeg ignore index.</param>
        /// <param name="outputFormat">Set video output format.</param>
        /// <param name="clearTempFile">Set whether to clear the temporary file after the merge is completed.</param>
        /// <param name="clearSource">Set whether to clear source file after the muxing is completed.</param>
        /// <param name="partFilter">Set m3u8 part filter.</param>
        /// <param name="token">Set cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public virtual async Task HlsDownloadAsync(
            string workDir, string saveName, string url, string header = "",
            int threads = 1, int delay = 200, int maxRetry = 20, long? maxSpeed = null,
            int interval = 1000, bool checkComplete = true, int? noSegStopTime = null,
            bool binaryMerge = false, bool keepFragmented = false, bool discardcorrupt = false,
            bool genpts = false, bool igndts = false, bool ignidx = false,
            OutputFormat outputFormat = OutputFormat.MP4,
            bool clearTempFile = false, bool clearSource = false,
            Func<List<Part>, List<Part>>? partFilter = null,
            CancellationToken token = default)
        {
            var hlsDL = Hls;
            var dashDL = Dash;
            var httpDL = Http;

            saveName = saveName.FilterFileName();

            Console.WriteLine("Start Download...");

            var (manifest, m3u8Url) = await hlsDL.GetManifestAsync(url, header, token);

            var audioPlaylist = null as MediaPlaylist;
            var mediaPlaylist = null as MediaPlaylist;

            // isMaster
            if (manifest.IsMaster())
            {
                var masterPlaylist = hlsDL.ParseMasterPlaylist(manifest, m3u8Url);

                var highestStreamInfo = masterPlaylist.StreamInfos
                    .GetWithHighestQuality();
                if (highestStreamInfo == null)
                    throw new Exception("Not found stream info.");
                (manifest, m3u8Url) = await hlsDL.GetManifestAsync(
                    highestStreamInfo.Uri, header, token);

                var audioMediaGroup = masterPlaylist.MediaGroups
                    .Where(it => it.GroupId == highestStreamInfo.Audio)
                    .FirstOrDefault();
                if (audioMediaGroup != null)
                    audioPlaylist = await hlsDL.GetMediaPlaylistAsync(
                        audioMediaGroup.Uri, header);
            }
            mediaPlaylist = hlsDL.ParseMediaPlaylist(manifest, m3u8Url);

            if (mediaPlaylist == null)
                throw new Exception("Not found media playlist.");

            // isLive
            if (mediaPlaylist.IsLive())
            {
                try
                {
                    await hlsDL.REC(
                        workDir, saveName, m3u8Url, header,
                        maxRetry: maxRetry, maxSpeed: maxSpeed,
                        interval: interval, noSegStopTime: noSegStopTime,
                        progress: (args) =>
                        {
                            var print = args.Format;
                            var sub = Console.WindowWidth - 2 - print.Length;
                            Console.Write("\r" + print + new string(' ', sub) + "\r");
                        },
                        token: token);
                }
                catch { }

                Console.WriteLine("\nStart Merge...");

                await hlsDL.MergeAsync(workDir, saveName,
                    outputFormat: outputFormat, binaryMerge: binaryMerge,
                    keepFragmented: keepFragmented, discardcorrupt: discardcorrupt,
                    genpts: genpts, igndts: igndts, ignidx: ignidx,
                    clearTempFile: clearTempFile,
                    onMessage: (msg) =>
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.Write(msg);
                        Console.ResetColor();
                    },
                    token: token);

                Console.WriteLine("Finish.");
                return;
            }

            // hasAudio
            if (audioPlaylist != null)
            {
                var videoSaveName = $"{saveName}(Video)";
                var audioSaveName = $"{saveName}(Audio)";

                var videoPath = await downloadMerge("video", videoSaveName, mediaPlaylist);
                var audioPath = await downloadMerge("audio", audioSaveName, audioPlaylist);

                var muxOutputFormat = outputFormat == OutputFormat.TS ?
                    MuxOutputFormat.TS : MuxOutputFormat.MP4;

                await hlsDL.MuxingAsync(
                    workDir, saveName, videoPath, audioPath,
                    outputFormat: muxOutputFormat,
                    clearSource: clearSource,
                    onMessage: (msg) =>
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.Write(msg);
                        Console.ResetColor();
                    },
                    token: token);

                Console.WriteLine("Finish.");
                return;

                async Task<string> downloadMerge(string id, string saveName, MediaPlaylist mediaPlaylist)
                {
                    var parts = mediaPlaylist.Parts;
                    if (partFilter != null)
                        parts = partFilter(mediaPlaylist.Parts);

                    var keys = null as Dictionary<string, string>;
                    var segmentKeys = Hls.GetKeys(parts);
                    if (segmentKeys.Count > 0)
                        keys = await Hls.GetKeysDataAsync(segmentKeys, header, token);

                    Console.WriteLine($"Start {id} Download...");

                    await hlsDL.DownloadAsync(
                        workDir, saveName, parts, header, keys,
                        threads: threads, delay: delay, maxRetry: maxRetry,
                        maxSpeed: maxSpeed, interval: interval,
                        checkComplete: checkComplete,
                        onSegment: async (ms, token) =>
                        {
                            return await ms.TrySkipPngHeaderAsync(token);
                        },
                        progress: (args) =>
                        {
                            var print = args.Format;
                            var sub = Console.WindowWidth - 2 - print.Length;
                            Console.Write("\r" + print + new string(' ', sub) + "\r");
                        },
                        token: token);

                    Console.WriteLine($"\nStart {id} Merge...");

                    var outputPath = await hlsDL.MergeAsync(workDir, saveName,
                        clearTempFile: clearTempFile, binaryMerge: true,
                        outputFormat: OutputFormat.MP4,
                        onMessage: (msg) =>
                        {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.Write(msg);
                            Console.ResetColor();
                        },
                        token: token);
                    return outputPath;
                }
            }

            // hasVideo
            {
                var parts = mediaPlaylist.Parts;
                if (partFilter != null)
                    parts = partFilter(mediaPlaylist.Parts);

                var keys = null as Dictionary<string, string>;
                var segmentKeys = hlsDL.GetKeys(parts);
                if (segmentKeys.Count > 0)
                    keys = await hlsDL.GetKeysDataAsync(segmentKeys, header);

                await hlsDL.DownloadAsync(
                    workDir, saveName, parts, header, keys,
                    threads: threads, delay: delay, maxRetry: maxRetry,
                    maxSpeed: maxSpeed, interval: interval,
                    checkComplete: checkComplete,
                    onSegment: async (ms, token) =>
                    {
                        return await ms.TrySkipPngHeaderAsync(token);
                    },
                    progress: (args) =>
                    {
                        var print = args.Format;
                        var sub = Console.WindowWidth - 2 - print.Length;
                        Console.Write("\r" + print + new string(' ', sub) + "\r");
                    },
                    token: token);

                Console.WriteLine("\nStart Merge...");

                await hlsDL.MergeAsync(workDir, saveName,
                    outputFormat: outputFormat, binaryMerge: binaryMerge,
                    keepFragmented: keepFragmented, discardcorrupt: discardcorrupt,
                    genpts: genpts, igndts: igndts, ignidx: ignidx,
                    clearTempFile: clearTempFile,
                    onMessage: (msg) =>
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.Write(msg);
                        Console.ResetColor();
                    },
                    token: token);

                Console.WriteLine("Finish.");
                return;
            }
        }

        /// <summary>
        /// Download mpd video.
        /// </summary>
        /// <param name="workDir">Set video download directory.</param>
        /// <param name="saveName">Set video save name.</param>
        /// <param name="url">Set m3u8/mpd/http url.</param>
        /// <param name="header">Set http request header.
        /// format: key1:key1|key2:key2</param>
        /// <param name="threads">Set the number of threads to download.</param>
        /// <param name="delay">Set http request delay.(millisecond)</param>
        /// <param name="maxRetry">Set the maximum number of download retries.</param>
        /// <param name="maxSpeed">Set the maximum download speed.(byte)
        /// 1KB = 1024 byte, 1MB = 1024 * 1024 byte</param>
        /// <param name="interval">Set the progress callback time interval.(millisecond)</param>
        /// <param name="checkComplete">Set whether to check file count complete.</param>
        /// <param name="binaryMerge">Set use binary merge.</param>
        /// <param name="keepFragmented">Set keep fragmented mp4.</param>
        /// <param name="discardcorrupt">Set ffmpeg discard corrupted packets.</param>
        /// <param name="genpts">Set ffmpeg generate missing PTS if DTS is present.</param>
        /// <param name="igndts">Set ffmpeg ignore DTS if PTS is set.</param>
        /// <param name="ignidx">Set ffmpeg ignore index.</param>
        /// <param name="outputFormat">Set video output format.</param>
        /// <param name="clearTempFile">Set whether to clear the temporary file after the merge is completed.</param>
        /// <param name="clearSource">Set whether to clear source file after the muxing is completed.</param>
        /// <param name="periodFilter">Set mpd period filter.</param>
        /// <param name="token">Set cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public virtual async Task DashDownloadAsync(
            string workDir, string saveName, string url, string header = "",
            int threads = 1, int delay = 200, int maxRetry = 20, long? maxSpeed = null,
            int interval = 1000, bool checkComplete = true, bool binaryMerge = false,
            bool keepFragmented = false, bool discardcorrupt = false,
            bool genpts = false, bool igndts = false, bool ignidx = false,
            OutputFormat outputFormat = OutputFormat.MP4,
            bool clearTempFile = false, bool clearSource = false,
            Func<List<Period>, List<Period>>? periodFilter = null,
            CancellationToken token = default)
        {
            var hlsDL = Hls;
            var dashDL = Dash;
            var httpDL = Http;

            saveName = saveName.FilterFileName();

            Console.WriteLine("Start Download...");

            var audioPlaylist = null as MediaPlaylist;
            var mediaPlaylist = null as MediaPlaylist;

            var mpd = await dashDL.GetMpdAsync(url, header, token);

            var periods = mpd.Periods;
            if (periodFilter != null)
                periods = periodFilter(mpd.Periods);

            var period = periods.FirstOrDefault();
            if (period == null)
                throw new Exception("Not found mpd period.");
            var video = period.GetWithHighestQualityVideo();
            var audio = period.GetWithHighestQualityAudio();

            if (video != null && audio != null)
            {
                await dashDL.ExpandSegmentBase(video, header);
                mediaPlaylist = dashDL.ToMediaPlaylist(video);

                await dashDL.ExpandSegmentBase(audio, header);
                audioPlaylist = dashDL.ToMediaPlaylist(audio);
            }
            else if (video != null)
            {
                await dashDL.ExpandSegmentBase(video, header);
                mediaPlaylist = dashDL.ToMediaPlaylist(video);
            }
            else if (audio != null)
            {
                await dashDL.ExpandSegmentBase(audio, header);
                mediaPlaylist = dashDL.ToMediaPlaylist(audio);
            }

            if (mediaPlaylist == null)
                throw new Exception("Not found media playlist.");

            // hasAudio
            if (audioPlaylist != null)
            {
                var videoSaveName = $"{saveName}(Video)";
                var audioSaveName = $"{saveName}(Audio)";

                var videoPath = await downloadMerge("video", videoSaveName, mediaPlaylist);
                var audioPath = await downloadMerge("audio", audioSaveName, audioPlaylist);

                var muxOutputFormat = outputFormat == OutputFormat.TS ?
                    MuxOutputFormat.TS : MuxOutputFormat.MP4;

                await hlsDL.MuxingAsync(
                    workDir, saveName, videoPath, audioPath,
                    outputFormat: muxOutputFormat,
                    clearSource: clearSource,
                    onMessage: (msg) =>
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.Write(msg);
                        Console.ResetColor();
                    },
                    token: token);

                Console.WriteLine("Finish.");
                return;

                async Task<string> downloadMerge(string id, string saveName, MediaPlaylist mediaPlaylist)
                {
                    var keys = null as Dictionary<string, string>;
                    var segmentKeys = Hls.GetKeys(mediaPlaylist.Parts);
                    if (segmentKeys.Count > 0)
                        keys = await Hls.GetKeysDataAsync(segmentKeys, header, token);

                    Console.WriteLine($"Start {id} Download...");

                    await hlsDL.DownloadAsync(workDir, saveName,
                        mediaPlaylist.Parts, header, keys,
                        threads: threads, delay: delay, maxRetry: maxRetry,
                        maxSpeed: maxSpeed, interval: interval,
                        checkComplete: checkComplete,
                        onSegment: async (ms, token) =>
                        {
                            return await ms.TrySkipPngHeaderAsync(token);
                        },
                        progress: (args) =>
                        {
                            var print = args.Format;
                            var sub = Console.WindowWidth - 2 - print.Length;
                            Console.Write("\r" + print + new string(' ', sub) + "\r");
                        },
                        token: token);

                    Console.WriteLine($"\nStart {id} Merge...");

                    var outputPath = await hlsDL.MergeAsync(workDir, saveName,
                        clearTempFile: clearTempFile, binaryMerge: true,
                        outputFormat: OutputFormat.MP4,
                        onMessage: (msg) =>
                        {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.Write(msg);
                            Console.ResetColor();
                        },
                        token: token);
                    return outputPath;
                }
            }

            // hasVideo
            {
                var keys = null as Dictionary<string, string>;
                var segmentKeys = hlsDL.GetKeys(mediaPlaylist.Parts);
                if (segmentKeys.Count > 0)
                    keys = await hlsDL.GetKeysDataAsync(segmentKeys, header);

                await hlsDL.DownloadAsync(workDir, saveName,
                    mediaPlaylist.Parts, header, keys,
                    threads: threads, delay: delay, maxRetry: maxRetry,
                    maxSpeed: maxSpeed, interval: interval,
                    checkComplete: checkComplete,
                    onSegment: async (ms, token) =>
                    {
                        return await ms.TrySkipPngHeaderAsync(token);
                    },
                    progress: (args) =>
                    {
                        var print = args.Format;
                        var sub = Console.WindowWidth - 2 - print.Length;
                        Console.Write("\r" + print + new string(' ', sub) + "\r");
                    },
                    token: token);

                Console.WriteLine("\nStart Merge...");

                await hlsDL.MergeAsync(workDir, saveName,
                    outputFormat: outputFormat, binaryMerge: binaryMerge,
                    keepFragmented: keepFragmented, discardcorrupt: discardcorrupt,
                    genpts: genpts, igndts: igndts, ignidx: ignidx,
                    clearTempFile: clearTempFile,
                    onMessage: (msg) =>
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.Write(msg);
                        Console.ResetColor();
                    },
                    token: token);

                Console.WriteLine("Finish.");
                return;
            }
        }

        /// <summary>
        /// Download http video.
        /// </summary>
        /// <param name="workDir">Set video download directory.</param>
        /// <param name="saveName">Set video save name.</param>
        /// <param name="url">Set m3u8/mpd/http url.</param>
        /// <param name="header">Set http request header.
        /// format: key1:key1|key2:key2</param>
        /// <param name="threads">Set the number of threads to download.</param>
        /// <param name="delay">Set http request delay.(millisecond)</param>
        /// <param name="maxRetry">Set the maximum number of download retries.</param>
        /// <param name="maxSpeed">Set the maximum download speed.(byte)
        /// 1KB = 1024 byte, 1MB = 1024 * 1024 byte</param>
        /// <param name="interval">Set the progress callback time interval.(millisecond)</param>
        /// <param name="token">Set cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public virtual async Task HttpDownloadAsync(
            string workDir, string saveName, string url, string header = "", 
            int threads = 1, int delay = 200, int maxRetry = 20, 
            long? maxSpeed = null, int interval = 1000, 
            CancellationToken token = default)
        {
            var hlsDL = Hls;
            var dashDL = Dash;
            var httpDL = Http;

            saveName = saveName.FilterFileName();

            Console.WriteLine("Start Download...");

            await httpDL.DownloadAsync(workDir, saveName,
                url, header, threads: threads, delay: delay,
                maxRetry: maxRetry, maxSpeed: maxSpeed, interval: interval,
                progress: (args) =>
                {
                    var print = args.Format;
                    var sub = Console.WindowWidth - 2 - print.Length;
                    Console.Write("\r" + print + new string(' ', sub) + "\r");
                },
                token: token);

            Console.WriteLine("\nFinish.");
        }
    }
}
