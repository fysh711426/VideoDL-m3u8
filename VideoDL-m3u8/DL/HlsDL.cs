using System;
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
            int maxThreads = 1, int delay = 1, int retry = 20,
            int? maxSpeed = null, int? stopSpeed = null,
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
                var total = part.Segments.Count;
                var partName = $"Part_{partIndex}".PadLeft($"{parts.Count}".Length, '0');
                var partDir = Path.Combine(tempDir, partName);
                if (!Directory.Exists(partDir))
                    Directory.CreateDirectory(partDir);

                foreach (var item in part.Segments)
                {
                    var fileName = $"{index}".PadLeft($"{total}".Length, '0');
                    var filePath = Path.Combine(partDir, $"{fileName}");
                    if(!File.Exists($"{filePath}.ts"))
                        works.Add((index, filePath, item));
                    index++;
                }
                partIndex++;
            }

            await ParallelTask.Run(works, async (it, _token) =>
            {
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
                    return;

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
                                await stream.CopyToAsync(ms, 4096, _token);
                                ms.Position = 0;
                                var cryptor = new Cryptor();
                                await cryptor.AES128Decrypt(ms, key, iv, fs, _token);
                            }
                            else
                            {
                                await stream.CopyToAsync(fs, 4096, _token);
                            }
                        }
                        File.Move(tempPath, savePath);
                    }, rangeFrom, rangeTo, _token);
            }, maxThreads, delay, retry, progress, token);
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

        protected async Task Retry(Func<int, Task> func, int delay, int? retry = null)
        {
            var _count = 0;
            var _retry = retry ?? int.MaxValue;
            while (true)
            {
                try
                {
                    await func(_count);
                    break;
                }
                catch (Exception)
                {
                    if (_count >= _retry)
                        break;
                    _count++;
                    await Task.Delay(delay);
                }
            }
        }
    }
}
