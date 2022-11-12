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
using VideoDL_m3u8.Parser;
using VideoDL_m3u8.Utils;

namespace VideoDL_m3u8.DL
{
    public class HlsDL: BaseDL
    {
        protected readonly HttpClient _httpClient;
        public HlsDL(int timeout = 6000) 
            : this(Http.Client, timeout)
        {
        }
        public HlsDL(HttpClient httpClient, int timeout = 6000)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromMilliseconds(timeout);
        }

        public async Task<(string data, string url)> GetManifest(string url, string header, CancellationToken token = default)
        {
            return await GetStringAsync(_httpClient, url, header, token);
        }

        public async Task<MasterPlaylist> GetMasterPlaylist(string url, string header, CancellationToken token = default)
        {
            var manifest = await GetManifest(url, header, token);
            var parser = new MasterPlaylistParser();
            return parser.Parse(manifest.data, manifest.url);
        }

        public async Task<MediaPlaylist> GetMediaPlaylist(string url, string header, CancellationToken token = default)
        {
            var manifest = await GetManifest(url, header, token);
            var parser = new MediaPlaylistParser();
            return parser.Parse(manifest.data, manifest.url);
        }

        public List<string> GetKeyUrls(List<Part> parts)
        {
            return parts
                .SelectMany(it => it.Segments)
                .Select(it => it.Key)
                .Where(it => it.Method != "NONE")
                .Select(it => it.Uri)
                .Distinct()
                .ToList();
        }

        public async Task<Dictionary<string, string>> GetKeys(
            string header, List<string> keyUrls, 
            CancellationToken token = default)
        {
            var result = new Dictionary<string, string>();
            foreach (var url in keyUrls)
            {
                var data =  await GetBytesAsync(_httpClient, url, header, token);
                var key = Convert.ToBase64String(data);
                result.Add(url, key);
            }
            return result;
        }

        public async Task Download(
            string workDir, string saveName, string header, 
            List<Part> parts, Dictionary<string, string>? keys = null,
            int maxThreads = 1, int delay = 1, int maxRetry = 20,
            long? maxSpeed = null, int interval = 1000,
            IProgress<ProgressEventArgs>? progress = null,
            CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(saveName))
                throw new Exception("Not found saveName.");
            if (!Directory.Exists(workDir))
                throw new Exception("Not found workDir.");
            var tempDir = Path.Combine(workDir, saveName);
            if (!Directory.Exists(tempDir))
                Directory.CreateDirectory(tempDir);

            var works = 
                new List<(int index, string filePath, Segment segment)>();

            var partIndex = 0;
            foreach (var part in parts)
            {
                var index = 0;
                var count = part.Segments.Count;
                var partName = $"Part_{partIndex}".PadLeft($"{parts.Count}".Length, '0');
                var partDir = Path.Combine(tempDir, partName);
                if (!Directory.Exists(partDir))
                    Directory.CreateDirectory(partDir);

                foreach (var item in part.Segments)
                {
                    var fileName = $"{index}".PadLeft($"{count}".Length, '0');
                    var filePath = Path.Combine(partDir, $"{fileName}");
                    works.Add((index, filePath, item));
                    index++;
                }
                partIndex++;
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
                var buffer = new byte[1024];
                var size = 0;
                var limit = 0L;
                if (maxSpeed != null)
                    limit = (long)(0.001 * interval * maxSpeed.Value - maxThreads * 1024);
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
                var c = 0;
                await ParallelTask.Run(works, async (it, _token) =>
                {
                    if (c > 10)
                    {
                        throw new Exception("xxx");
                    }
                    c++;

                    var index = it.index;
                    var filePath = it.filePath;
                    var segment = it.segment;

                    var rangeFrom = null as long?;
                    var rangeTo = null as long?;
                    if (segment.ByteRange != null)
                    {
                        rangeFrom = segment.ByteRange.Offset ?? 0;
                        rangeTo = rangeFrom + segment.ByteRange.Length - 1;
                    }

                    var tempPath = $"{filePath}.downloading";
                    var savePath = $"{filePath}.ts";

                    if (File.Exists(savePath))
                    {
                        var info = new FileInfo(savePath);
                        Interlocked.Add(ref downloadBytes, info.Length);
                        finish++;
                        return;
                    }

                    await LoadStreamAsync(_httpClient, segment.Uri, header,
                        async (stream) =>
                        {
                            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                            {
                                if (segment.Key.Method != "NONE")
                                {
                                    if (keys?.TryGetValue(segment.Key.Uri, out var key) != true ||
                                        string.IsNullOrEmpty(key))
                                        throw new Exception("Not found segment key.");
                                    var iv = segment.Key.IV;
                                    var ms = new MemoryStream();
                                    var size = await copyToAsync(stream, ms, _token);
                                    Interlocked.Add(ref downloadBytes, size);
                                    ms.Position = 0;
                                    var cryptor = new Cryptor();
                                    await cryptor.AES128Decrypt(ms, key, iv, fs, _token);
                                }
                                else
                                {
                                    var size = await copyToAsync(stream, fs, _token);
                                    Interlocked.Add(ref downloadBytes, size);
                                }
                            }
                            File.Move(tempPath, savePath);
                        }, rangeFrom, rangeTo, _token);
                    finish++;
                }, maxThreads, delay, maxRetry, (c) => retry = c, token);
            }

            void progressEvent()
            {
                try
                {
                    if (progress != null)
                    {
                        var time = DateTime.Now;
                        var percentage = (double)finish / total;
                        var totalBytes = (long)Number.Div(downloadBytes * total, finish);
                        var speed = intervalDownloadBytes / interval * 1000;
                        var eta = (int)Number.Div(totalBytes - downloadBytes, speed);
                        var args = new ProgressEventArgs
                        {
                            Time = time,
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
                        progress.Report(args);
                    }
                }
                catch (Exception) { }
            }

            var timer = new System.Timers.Timer(interval);
            timer.AutoReset = true;
            timer.Elapsed += delegate
            {
                progressEvent();
                Interlocked.Exchange(ref intervalDownloadBytes, 0);
            };

            try
            {
                timer.Enabled = true;
                await func();
                timer.Enabled = false;
                progressEvent();
            }
            catch
            {
                timer.Enabled = false;
                throw;
            }
        }

        public async Task Merge(string workDir, string saveName, bool cleanTempFile,
            CancellationToken token = default)
        {
            if(string.IsNullOrWhiteSpace(saveName))
                throw new Exception("Not found saveName.");
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
                throw new Exception("Not found part directory.");

            foreach (var part in parts)
            {
                var partConcat = Path.Combine(tempDir, $"{part.Name}.ts");
                using (var fs = new FileStream(
                    partConcat, FileMode.Create, FileAccess.Write))
                {
                    var partFiles = Directory.GetFiles(part.Path)
                        .Where(it => it.EndsWith(".ts"))
                        .OrderBy(it => it)
                        .ToList();

                    foreach (var partFile in partFiles)
                    {
                        using (var tempFs = new FileStream(
                            partFile, FileMode.Open, FileAccess.Read))
                        {
                            await tempFs.CopyToAsync(fs, 4096, token);
                        }
                    }
                }
            }

            var concatPath = Path.Combine(tempDir, $"concat.txt");
            var outputPath = Path.Combine(tempDir, $"output.mp4");
            var finishPath = Path.Combine(workDir, $"{saveName}.mp4");
            var files = Directory.GetFiles(tempDir)
                .Where(it => it.EndsWith(".ts"))
                .OrderBy(it => it)
                .ToList();
            var fileManifest = files
                .Aggregate(new StringBuilder(), 
                    (r, it)=> r.AppendLine($@"file '{it}'"))
                .ToString();
            File.WriteAllText(concatPath, fileManifest);

            var arguments = $@"-f concat -safe 0 -i ""{concatPath}"" -map 0:v? -map 0:a? -map 0:s? -c copy -y -bsf:a aac_adtstoasc -f mp4 ""{outputPath}"" -loglevel error";
            var info = new ProcessStartInfo("ffmpeg", arguments);
            info.UseShellExecute = false;
            info.RedirectStandardError = true;
            var process = Process.Start(info);
            if (process == null)
                throw new Exception("Process start error.");
            try
            {
                var error = process.StandardError.ReadToEnd();
                await process.WaitForExitAsync(token);
                if (!string.IsNullOrEmpty(error))
                    throw new Exception(error);
            }
            finally
            {
                process.Dispose();
            }

            File.Move(outputPath, finishPath);
            File.Delete(concatPath);
            foreach (var file in files)
                File.Delete(file);
            
            if (cleanTempFile)
            {
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch { }
            }
        }
    }
}
