﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VideoDL_m3u8.Events;
using VideoDL_m3u8.Extensions;
using VideoDL_m3u8.Parser;
using VideoDL_m3u8.Utils;

namespace VideoDL_m3u8.DL
{
    public class HlsDL : BaseDL
    {
        protected readonly HttpClient _httpClient;

        /// <summary>
        /// Init HlsDL.
        /// </summary>
        /// <param name="timeout">Set http request timeout.(millisecond)</param>
        /// <param name="proxy">Set http or socks5 proxy.
        /// http://{hostname}:{port} or socks5://{hostname}:{port}</param>
        public HlsDL(int timeout = 60000, string? proxy = null)
            : this(CreateHttpClient(proxy), timeout)
        {
        }

        private static HttpClient CreateHttpClient(string? proxy)
        {
            if (proxy == null)
                return Http.Client;
            return Http.GetClient(proxy);
        }

        /// <summary>
        /// Init HlsDL.
        /// </summary>
        /// <param name="httpClient">Set http client.</param>
        /// <param name="timeout">Set http request timeout.(millisecond)</param>
        public HlsDL(HttpClient httpClient, int timeout = 60000)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromMilliseconds(timeout);
        }

        /// <summary>
        /// Get m3u8 manifest by url.
        /// </summary>
        /// <param name="url">Set m3u8 download url.</param>
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
        /// Get m3u8 master playlist by url.
        /// </summary>
        /// <param name="url">Set m3u8 download url.</param>
        /// <param name="header">Set http request header.
        /// format: key1:key1|key2:key2</param>
        /// <param name="token">Set cancellation token.</param>
        /// <returns></returns>
        public async Task<MasterPlaylist> GetMasterPlaylistAsync(
            string url, string header = "", CancellationToken token = default)
        {
            var manifest = await GetManifestAsync(url, header, token);
            var parser = new MasterPlaylistParser();
            return parser.Parse(manifest.data, manifest.url);
        }

        /// <summary>
        /// Get m3u8 media playlist by url.
        /// </summary>
        /// <param name="url">Set m3u8 download url.</param>
        /// <param name="header">Set http request header.
        /// format: key1:key1|key2:key2</param>
        /// <param name="token">Set cancellation token.</param>
        /// <returns></returns>
        public async Task<MediaPlaylist> GetMediaPlaylistAsync(
            string url, string header = "", CancellationToken token = default)
        {
            var manifest = await GetManifestAsync(url, header, token);
            var parser = new MediaPlaylistParser();
            return parser.Parse(manifest.data, manifest.url);
        }

        /// <summary>
        /// Parse m3u8 master playlist by manifest.
        /// </summary>
        /// <param name="manifest">Set m3u8 master manifest.</param>
        /// <param name="url">Set m3u8 url.</param>
        /// <returns></returns>
        public MasterPlaylist ParseMasterPlaylist(string manifest, string url)
        {
            var parser = new MasterPlaylistParser();
            return parser.Parse(manifest, url);
        }

        /// <summary>
        /// Parse m3u8 media playlist by manifest.
        /// </summary>
        /// <param name="manifest">Set m3u8 media manifest.</param>
        /// <param name="url">Set m3u8 url.</param>
        /// <returns></returns>
        public MediaPlaylist ParseMediaPlaylist(string manifest, string url)
        {
            var parser = new MediaPlaylistParser();
            return parser.Parse(manifest, url);
        }

        /// <summary>
        /// Get m3u8 segment keys by parts.
        /// </summary>
        /// <param name="parts">Set m3u8 playlist parts.</param>
        /// <returns></returns>
        public List<SegmentKey> GetKeys(List<Part> parts)
        {
            var keys = new List<SegmentKey>();
            foreach (var part in parts)
            {
                if (part.SegmentMap != null)
                {
                    if (part.SegmentMap.Key.Method != "NONE")
                        keys.Add(part.SegmentMap.Key);
                }
                foreach (var item in part.Segments)
                {
                    if (item.Key.Method != "NONE")
                        keys.Add(item.Key);
                }
            }
            return keys.Distinct(it => it.Uri).ToList();
        }

        /// <summary>
        /// Get m3u8 key base64 data by segment keys.
        /// </summary>
        /// <param name="segmentKeys">Set m3u8 segment keys.</param>
        /// <param name="header">Set http request header.
        /// format: key1:key1|key2:key2</param>
        /// <param name="token">Set cancellation token.</param>
        /// <returns></returns>
        public async Task<Dictionary<string, string>> GetKeysDataAsync(
            List<SegmentKey> segmentKeys, string header = "",
            CancellationToken token = default)
        {
            var result = new Dictionary<string, string>();
            foreach (var item in segmentKeys)
            {
                var key = await GetKeyDataAsync(item, header, token);
                result.Add(item.Uri, key);
            }
            return result;
        }

        /// <summary>
        /// Get m3u8 key base64 data by segment key.
        /// </summary>
        /// <param name="segmentKey">Set m3u8 segment key.</param>
        /// <param name="header">Set http request header.
        /// format: key1:key1|key2:key2</param>
        /// <param name="token">Set cancellation token.</param>
        /// <returns></returns>
        public async Task<string> GetKeyDataAsync(
            SegmentKey segmentKey, string header = "",
            CancellationToken token = default)
        {
            var data = await GetBytesAsync(_httpClient,
                segmentKey.Uri, header, null, null, token);
            return Convert.ToBase64String(data);
        }

        /// <summary>
        /// Download m3u8 ts files.
        /// </summary>
        /// <param name="workDir">Set video download directory.</param>
        /// <param name="saveName">Set video save name.</param>
        /// <param name="parts">Set m3u8 media playlist parts to download.</param>
        /// <param name="header">Set http request header.
        /// format: key1:key1|key2:key2</param>
        /// <param name="keys">Set m3u8 segment keys.</param>
        /// <param name="threads">Set the number of threads to download.</param>
        /// <param name="delay">Set http request delay.(millisecond)</param>
        /// <param name="maxRetry">Set the maximum number of download retries.</param>
        /// <param name="maxSpeed">Set the maximum download speed.(byte)
        /// 1KB = 1024 byte, 1MB = 1024 * 1024 byte</param>
        /// <param name="interval">Set the progress callback time interval.(millisecond)</param>
        /// <param name="checkComplete">Set whether to check file count complete.</param>
        /// <param name="onSegment">Set segment download callback.</param>
        /// <param name="progress">Set progress callback.</param>
        /// <param name="token">Set cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task DownloadAsync(
            string workDir, string saveName,
            List<Part> parts, string header = "",
            Dictionary<string, string>? keys = null,
            int threads = 1, int delay = 200, int maxRetry = 20,
            long? maxSpeed = null, int interval = 1000, bool checkComplete = true,
            Func<Stream, CancellationToken, Task<Stream>>? onSegment = null,
            Action<ProgressEventArgs>? progress = null,
            CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(workDir))
                throw new Exception("Parameter workDir cannot be empty.");
            if (string.IsNullOrWhiteSpace(saveName))
                throw new Exception("Parameter saveName cannot be empty.");
            if (maxSpeed != null && maxSpeed.Value < 1024)
                throw new Exception("Parameter maxSpeed must be greater than or equal to 1024.");

            if (parts == null ||
                parts.Count == 0 || !parts.SelectMany(it => it.Segments).Any())
                throw new Exception("Parameter parts cannot be empty.");

            keys = keys ?? new Dictionary<string, string>();

            saveName = saveName.FilterFileName();

            header = string.IsNullOrWhiteSpace(header) ? "" : header;

            var tempDir = Path.Combine(workDir, saveName);
            if (!Directory.Exists(tempDir))
                Directory.CreateDirectory(tempDir);

            var works = new List<(long index, string filePath, string ext,
                (string Uri, ByteRange? ByteRange, SegmentKey Key) segment)>();

            foreach (var part in parts)
            {
                var count = part.Segments.Count;
                var partName = $"Part_{part.PartIndex}".PadLeft($"{parts.Count}".Length, '0');
                var partDir = Path.Combine(tempDir, partName);
                if (!Directory.Exists(partDir))
                    Directory.CreateDirectory(partDir);

                var hasMap = false;
                if (part.SegmentMap != null)
                {
                    var mapName = "!MAP";
                    var mapPath = Path.Combine(partDir, mapName);
                    var mapIndex = part.Segments.Count == 0 ?
                        0 : part.Segments.First().Index;
                    works.Add((mapIndex, mapPath, ".mp4",
                        (part.SegmentMap.Uri,
                         part.SegmentMap.ByteRange,
                         part.SegmentMap.Key)));
                    hasMap = true;
                }

                foreach (var item in part.Segments)
                {
                    var ext = hasMap ? ".m4s" : ".ts";
                    var fileName = $"{item.Index}".PadLeft($"{count}".Length, '0');
                    var filePath = Path.Combine(partDir, $"{fileName}");
                    works.Add((item.Index, filePath, ext,
                        (item.Uri, item.ByteRange, item.Key)));
                }
            }

            var retry = 0;
            var total = 0;
            var finish = 0;
            var downloadBytes = 0L;
            var intervalDownloadBytes = 0L;
            total = works.Count;

            async Task<long> copyToAsync(Stream s, Stream d,
                CancellationToken token = default)
            {
                var bytes = 0L;
                var buffer = new byte[4096];
                var size = 0;
                var limit = 0L;
                if (maxSpeed != null)
                    limit = (long)(0.001 * interval * maxSpeed.Value - threads * 1024);
                while (true)
                {
                    size = await s.ReadAsync(buffer, 0, buffer.Length, token);
                    if (size <= 0)
                        return bytes;
                    await d.WriteAsync(buffer, 0, size, token);
                    bytes += size;
                    Interlocked.Add(ref intervalDownloadBytes, size);
                    if (maxSpeed != null)
                    {
                        while (intervalDownloadBytes >= limit)
                        {
                            await Task.Delay(1, token);
                        }
                    }
                }
            }

            async Task func()
            {
                await RetryTask.Run(async (r, ex) =>
                {
                    finish = 0;
                    downloadBytes = 0L;
                    intervalDownloadBytes = 0L;
                    retry = r;

                    var todoWorks = works
                        .Where(it =>
                        {
                            var savePath = $"{it.filePath}{it.ext}";
                            if (File.Exists(savePath))
                            {
                                var info = new FileInfo(savePath);
                                downloadBytes += info.Length;
                                finish++;
                                return false;
                            }
                            return true;
                        })
                        .ToList();

                    await ParallelTask.Run(todoWorks, async (it, _token) =>
                    {
                        var index = it.index;
                        var filePath = it.filePath;
                        var ext = it.ext;
                        var segment = it.segment;

                        var rangeFrom = null as long?;
                        var rangeTo = null as long?;
                        if (segment.ByteRange != null)
                        {
                            rangeFrom = segment.ByteRange.Offset ?? 0;
                            rangeTo = rangeFrom + segment.ByteRange.Length - 1;
                        }

                        var tempPath = $"{filePath}.downloading";
                        var savePath = $"{filePath}{ext}";

                        await LoadStreamAsync(_httpClient, segment.Uri, header,
                            async (stream, contentLength) =>
                            {
                                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                                {
                                    async Task<Stream> download()
                                    {
                                        var ms = new MemoryStream();
                                        var size = await copyToAsync(stream, ms, _token);
                                        if (contentLength != null)
                                        {
                                            if (size != contentLength)
                                                throw new Exception("Segment size not match content-length.");
                                        }
                                        Interlocked.Add(ref downloadBytes, size);
                                        ms.Position = 0;
                                        return ms;
                                    }

                                    if (segment.Key.Method != "NONE")
                                    {
                                        if (!keys.TryGetValue(segment.Key.Uri, out var key))
                                            throw new Exception("Not found segment key.");
                                        var iv = segment.Key.IV;

                                        var ms = await download();
                                        if (onSegment != null)
                                        {
                                            var decryptStream = new MemoryStream();
                                            await new Cryptor().AES128Decrypt(
                                                ms, key, iv, decryptStream, _token);
                                            decryptStream.Position = 0;
                                            ms = await onSegment(decryptStream, _token);
                                            await ms.CopyToAsync(fs, 4096, _token);
                                        }
                                        else
                                        {
                                            await new Cryptor().AES128Decrypt(
                                                ms, key, iv, fs, _token);
                                        }
                                    }
                                    else
                                    {
                                        var ms = await download();
                                        if (onSegment != null)
                                        {
                                            ms = await onSegment(ms, _token);
                                            await ms.CopyToAsync(fs, 4096, _token);
                                        }
                                        else
                                        {
                                            await ms.CopyToAsync(fs, 4096, _token);
                                        }
                                    }
                                }
                                File.Move(tempPath, savePath);
                            }, rangeFrom, rangeTo, _token);
                        finish++;
                    }, threads, delay, token);
                }, 10 * 1000, maxRetry, token);
            }

            void progressEvent()
            {
                try
                {
                    if (progress != null)
                    {
                        var percentage = Number.Div(finish, total);
                        var totalBytes = (long)Number.Div(downloadBytes * total, finish);
                        var speed = intervalDownloadBytes / interval * 1000;
                        var eta = (int)Number.Div(totalBytes - downloadBytes, speed);
                        var args = new ProgressEventArgs
                        {
                            Total = total,
                            Finish = finish,
                            DownloadBytes = downloadBytes,
                            MaxRetry = maxRetry,
                            Retry = retry,
                            Percentage = percentage,
                            TotalBytes = totalBytes,
                            Speed = speed,
                            Eta = eta
                        };
                        progress(args);
                    }
                }
                catch { }
            }

            var stop = false;
            var timer = new System.Timers.Timer(interval);
            timer.AutoReset = true;
            timer.Elapsed += delegate
            {
                if (!stop)
                {
                    progressEvent();
                    Interlocked.Exchange(ref intervalDownloadBytes, 0);
                }
            };

            void checkFilesComplete()
            {
                var count = 0;
                var partDirs = Directory.GetDirectories(tempDir);
                foreach (var partDir in partDirs)
                {
                    var partDirName = Path.GetFileName(partDir);
                    if (!partDirName.StartsWith("Part_"))
                        continue;
                    var partFiles = Directory.GetFiles(partDir)
                        .Where(it =>
                            it.EndsWith(".mp4") ||
                            it.EndsWith(".m4s") ||
                            it.EndsWith(".ts"));
                    count += partFiles.Count();
                }
                if (count != works.Count)
                    throw new Exception("Segment count not match.");
            }

            try
            {
                timer.Enabled = true;
                await func();
                if (checkComplete)
                    checkFilesComplete();
                stop = true;
                timer.Enabled = false;
                progressEvent();

            }
            catch
            {
                stop = true;
                timer.Enabled = false;
                progressEvent();
                throw;
            }
        }

        /// <summary>
        /// Merge m3u8 ts files.
        /// </summary>
        /// <param name="workDir">Set video download directory.</param>
        /// <param name="saveName">Set video save name.</param>
        /// <param name="clearTempFile">Set whether to clear the temporary file after the merge is completed.</param>
        /// <param name="onMessage">Set callback function for FFmpeg warning or error messages.</param>
        /// <param name="token">Set cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task MergeAsync(string workDir, string saveName,
            bool clearTempFile = true, Action<string>? onMessage = null,
            bool genpts = false, bool igndts = false,
            CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(workDir))
                throw new Exception("Parameter workDir cannot be empty.");
            if (string.IsNullOrWhiteSpace(saveName))
                throw new Exception("Parameter saveName cannot be empty.");

            saveName = saveName.FilterFileName();

            var tempDir = Path.Combine(workDir, saveName);
            if (!Directory.Exists(tempDir))
                throw new Exception("Not found saveName directory.");

            var parts = Directory.GetDirectories(tempDir)
                .Select(it => new
                {
                    Path = it,
                    Name = Path.GetFileName(it)
                })
                .Where(it => it.Name.StartsWith("Part_"))
                .OrderBy(it => it.Path)
                .ToList();

            if (parts.Count == 0)
                throw new Exception("Directory parts cannot be empty.");

            foreach (var part in parts)
            {
                var hasMap = File.Exists(
                    Path.Combine(part.Path, "!MAP.mp4"));
                // var ext = hasMap ? ".mp4" : ".ts";
                var ext = ".mp4";
                var partConcatPath = Path.Combine(tempDir, $"{part.Name}{ext}");
                var partFiles = Directory.GetFiles(part.Path)
                    .Where(it =>
                        it.EndsWith(".mp4") ||
                        it.EndsWith(".m4s") ||
                        it.EndsWith(".ts"));
                var partFileOrderList = partFiles
                    .Select(it => new
                    {
                        index = Path.GetFileNameWithoutExtension(it)
                                .PadLeft(19 + 4, '_'),
                        item = it
                    })
                    .OrderBy(it => it.index)
                    .Select(it => it.item)
                    .ToList();
                var binaryMerge = hasMap ? true : false;
                if (binaryMerge)
                {
                    using (var fs = new FileStream(
                        partConcatPath, FileMode.Create, FileAccess.Write))
                    {
                        foreach (var partFile in partFileOrderList)
                        {
                            using (var tempFs = new FileStream(
                                partFile, FileMode.Open, FileAccess.Read))
                            {
                                await tempFs.CopyToAsync(fs, 4096, token);
                            }
                        }
                    }
                }
                else
                {
                    var partConcatTextPath = Path.Combine(part.Path, $"concat.txt");
                    var partFileManifest = partFileOrderList
                        .Aggregate(new StringBuilder(),
                            (r, it) => r.AppendLine($@"file '{it}'"))
                        .ToString();
                    File.WriteAllText(partConcatTextPath, partFileManifest);

                    var _useAACFilter = partFiles.Any(it => it.EndsWith(".ts"));
                    var _tAACFilter = _useAACFilter ? "-bsf:a aac_adtstoasc" : "";
                    var _tFFlags = genpts || igndts ? $"-fflags {(genpts ? "+genpts" : "")}{(genpts ? "+igndts" : "")}" : "";
                    var _arguments = $@"{_tFFlags} -f concat -safe 0 -i ""{partConcatTextPath}"" -map 0:v? -map 0:a? -map 0:s? -c copy -y -f mp4 {_tAACFilter} ""{partConcatPath}"" -loglevel warning";
                    await FFmpeg.ExecuteAsync(_arguments, onMessage, token);
                    
                    File.Delete(partConcatTextPath);
                }
            }

            var concatTextPath = Path.Combine(tempDir, $"concat.txt");
            var outputPath = Path.Combine(tempDir, $"output.mp4");
            var files = Directory.GetFiles(tempDir)
                .Where(it =>
                    it.EndsWith(".mp4") ||
                    it.EndsWith(".ts"))
                .OrderBy(it => it)
                .ToList();

            if (files.Count == 1)
            {
                File.Move(files.First(), outputPath);
            }
            if (files.Count > 1)
            {
                var fileManifest = files
                    .Aggregate(new StringBuilder(),
                        (r, it) => r.AppendLine($@"file '{it}'"))
                    .ToString();
                File.WriteAllText(concatTextPath, fileManifest);

                var useAACFilter = files.Any(it => it.EndsWith(".ts"));
                var tAACFilter = useAACFilter ? "-bsf:a aac_adtstoasc" : "";
                var tFFlags = genpts || igndts ? $"-fflags {(genpts ? "+genpts" : "")}{(genpts ? "+igndts" : "")}" : "";

                var arguments = $@"{tFFlags} -f concat -safe 0 -i ""{concatTextPath}"" -map 0:v? -map 0:a? -map 0:s? -c copy -y {tAACFilter} ""{outputPath}"" -loglevel warning";
                await FFmpeg.ExecuteAsync(arguments, onMessage, token);

                File.Delete(concatTextPath);
            }
            
            var finishPath = Path.Combine(workDir, $"{saveName}.mp4");
            if (File.Exists(finishPath))
                finishPath = Path.Combine(workDir,
                    $"{saveName}_{DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss")}.mp4");
            File.Move(outputPath, finishPath);
            foreach (var file in files)
                File.Delete(file);

            if (clearTempFile)
            {
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch { }
            }
        }


        /// <summary>
        /// REC m3u8 live stream.
        /// </summary>
        /// <param name="workDir">Set video download directory.</param>
        /// <param name="saveName">Set video save name.</param>
        /// <param name="url">Set m3u8 live stream url.</param>
        /// <param name="header">Set http request header.
        /// format: key1:key1|key2:key2</param>
        /// <param name="maxRetry">Set the maximum number of download retries.</param>
        /// <param name="maxSpeed">Set the maximum download speed.(byte)
        /// 1KB = 1024 byte, 1MB = 1024 * 1024 byte</param>
        /// <param name="interval">Set the progress callback time interval.(millisecond)</param>
        /// <param name="noSegStopTime">Set how long to stop after when there is no segment.(millisecond)</param>
        /// <param name="onSegment">Set segment download callback.</param>
        /// <param name="progress">Set progress callback.</param>
        /// <param name="token">Set cancellation token.</param>
        /// <returns></returns>
        public async Task REC(
            string workDir, string saveName,
            string url, string header = "", int maxRetry = 20, 
            long? maxSpeed = null, int interval = 1000, int? noSegStopTime = null,
            Func<Stream, CancellationToken, Task<Stream>>? onSegment = null,
            Action<RecProgressEventArgs>? progress = null,
            CancellationToken token = default)
        {
            var finishDict = new Dictionary<long, bool>();
            var keys = new Dictionary<string, string>();

            var retry = 0;
            var now = DateTime.Now;
            var sw = new Stopwatch();
            
            var finish = 0;
            var downloadBytes = 0L;
            var todoFinish = 0;
            var todoDownloadBytes = 0L;
            var speed = 0L;
            var noSegDuration = 0;
            var currentIndex = long.MaxValue * -1;
            var currentPartIndex = int.MaxValue * -1;

            void progressEvent()
            {
                try
                {
                    if (progress != null)
                    {
                        var args = new RecProgressEventArgs
                        {
                            Finish = finish + todoFinish,
                            DownloadBytes = downloadBytes + todoDownloadBytes,
                            MaxRetry = maxRetry,
                            Retry = retry,
                            Speed = speed,
                            RecTime = DateTime.Now - now
                        };
                        progress(args);
                    }
                }
                catch { }
            }

            async Task func()
            {
                await RetryTask.Run(async (r, ex) =>
                {
                    todoFinish = 0;
                    todoDownloadBytes = 0;
                    retry = r;

                    while (true)
                    {
                        sw.Restart();

                        // Download and parse m3u8 manifest
                        var mediaPlaylist = await GetMediaPlaylistAsync(url, header, token);

                        // Download m3u8 segment key
                        var segmentKeys = GetKeys(mediaPlaylist.Parts);
                        foreach (var segmentKey in segmentKeys)
                        {
                            if (!keys.ContainsKey(segmentKey.Uri))
                            {
                                var key = await GetKeyDataAsync(segmentKey, header, token);
                                keys.Add(segmentKey.Uri, key);
                            }
                        }

                        // Get todo part list
                        var todoParts = mediaPlaylist.Parts
                            .Select(it =>
                            {
                                var segs = it.Segments
                                    .Where(itt => !finishDict.ContainsKey(itt.Index))
                                    .ToList();
                                it.Segments = segs;
                                return it;
                            })
                            .Where(it => it.Segments.Count > 0)
                            .ToList();

                        // Check playlist reset
                        bool isReset()
                        {
                            foreach (var part in todoParts)
                            {
                                if (part.PartIndex < currentPartIndex)
                                    return true;
                                if (part.PartIndex == currentPartIndex)
                                {
                                    foreach (var seg in part.Segments)
                                    {
                                        if (seg.Index < currentIndex)
                                            return true;
                                    }
                                }
                            }
                            return false;
                        }
                        if (isReset())
                            break;

                        var hasSeg = false;
                        if (todoParts.Count > 0)
                        {
                            hasSeg = true;
                            noSegDuration = 0;

                            // Download m3u8 ts files
                            await DownloadAsync(workDir,
                                saveName, todoParts, header, keys,
                                threads: 1, delay: 1, maxRetry: 0,
                                maxSpeed: maxSpeed, checkComplete: false,
                                interval: interval, onSegment: onSegment,
                                progress: (args) =>
                                {
                                    speed = args.Speed;
                                    todoFinish = args.Finish;
                                    todoDownloadBytes = args.DownloadBytes;
                                },
                                token: token);
                            sw.Stop();

                            // Update parameters
                            finish += todoFinish;
                            downloadBytes += todoDownloadBytes;
                            todoFinish = 0;
                            todoDownloadBytes = 0;
                            todoParts
                                .SelectMany(it => it.Segments)
                                .ToList()
                                .ForEach(it =>
                                {
                                    if (!finishDict.ContainsKey(it.Index))
                                        finishDict.Add(it.Index, true);
                                });
                        }

                        // Live ends
                        if (mediaPlaylist.EndList)
                            break;

                        if (noSegStopTime != null)
                        {
                            if (noSegDuration >= noSegStopTime.Value)
                                break;
                        }

                        // Waiting for new segment
                        var targetDurationTime = 
                            mediaPlaylist.TargetDuration * 1000;

                        if (!hasSeg)
                        {
                            var half = targetDurationTime / 2;
                            noSegDuration += half;
                            var timespan = half - sw.ElapsedMilliseconds;
                            if (timespan > 0)
                            {
                                await Task.Delay(half, token);
                                continue;
                            }
                        }
                        else
                        {
                            var timespan = targetDurationTime - sw.ElapsedMilliseconds;
                            if (timespan > 0)
                            {
                                await Task.Delay((int)timespan, token);
                                continue;
                            }
                        }
                        await Task.Delay(1);
                    }
                }, 1, maxRetry, token);
            }

            var stop = false;
            var timer = new System.Timers.Timer(interval);
            timer.AutoReset = true;
            timer.Elapsed += delegate
            {
                if (!stop)
                {
                    progressEvent();
                }
            };

            try
            {
                timer.Enabled = true;
                await func();
                stop = true;
                timer.Enabled = false;
                progressEvent();

            }
            catch
            {
                stop = true;
                timer.Enabled = false;
                progressEvent();
                throw;
            }
        }
    }
}
