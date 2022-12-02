using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using VideoDL_m3u8.Extensions;

namespace VideoDL_m3u8.Utils
{
    internal class FFmpeg
    {
        public static async Task ExecuteAsync(string arguments, 
            string? workingDir = null, Action<string>? onMessage = null,
            CancellationToken token = default)
        {
            var info = new ProcessStartInfo("ffmpeg", arguments)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
            };
            if (!string.IsNullOrWhiteSpace(workingDir))
                info.WorkingDirectory = workingDir;
            var process = Process.Start(info);
            if (process == null)
                throw new Exception("Process start error.");

            try
            {
                var warning = process.StandardError.ReadToEnd();
                await process.WaitForExitPatchAsync(token);
                if (process.ExitCode != 0)
                    throw new Exception(
                        $"FFmpeg error message. {warning}");
                process.Dispose();
                if (!string.IsNullOrEmpty(warning))
                    onMessage?.Invoke(warning);
            }
            catch
            {
                process.Dispose();
                throw;
            }
        }
    }
}
