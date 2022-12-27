using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VideoDL_m3u8.Extensions;

namespace VideoDL_m3u8.Utils
{
    public class FFprobe
    {
        internal static async Task ExecuteAsync(string arguments,
            string? workingDir = null, Action<string>? onMessage = null,
            CancellationToken token = default)
        {
            var info = new ProcessStartInfo("ffprobe", arguments)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                StandardErrorEncoding = Encoding.UTF8
            };
            if (!string.IsNullOrWhiteSpace(workingDir))
                info.WorkingDirectory = workingDir;
            var process = Process.Start(info);
            if (process == null)
                throw new Exception("Process start error.");

            try
            {
                var message = process.StandardError.ReadToEnd();
                await process.WaitForExitPatchAsync(token);
                if (process.ExitCode != 0)
                    throw new Exception(
                        $"FFprobe error message. {message}");
                process.Dispose();
                if (!string.IsNullOrEmpty(message))
                    onMessage?.Invoke(message);
            }
            catch
            {
                process.Dispose();
                throw;
            }
        }

        // Refer to: https://github.com/nilaoda/N_m3u8DL-CLI
        public static async Task<List<string>> GetVideoInfo(string filePath,
            CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new Exception("Parameter filePath cannot be empty.");
            if (!File.Exists(filePath))
                throw new Exception("Not found filePath.");

            var videoInfo = "";
            var arguments = $@"""{filePath}""";
            await FFprobe.ExecuteAsync(arguments, null,
                (message) => videoInfo = message, token);

            return videoInfo
                .Pipe(it => Regex.Matches(it, @"Stream #.*"))
                .Select(it => it.Value)
                .Select(it =>
                {
                    //var pid = Regex.Match(it, @"\[(0x\d*?)\]")
                    //    .Groups[1].Value;
                    var idx = Regex.Match(it, @"(#.*?)\[")
                        .Groups[1].Value;
                    var info = Regex.Match(it, @": (.*)")
                        .Groups[1].Value;
                    var matrix = Regex.Match(it, @"( \(\[.*?\))")
                        .Groups[1].Value;
                    info = info
                        .Replace(matrix, "")
                        .Replace(": ", " ");
                    return $"Stream {idx}: {info}".Trim();
                })
                .ToList();
        }
    }
}
