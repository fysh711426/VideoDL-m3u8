﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VideoDL_m3u8.DL;
using VideoDL_m3u8.Enums;
using VideoDL_m3u8.Extensions;
using VideoDL_m3u8.Parser;

namespace Example
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // m3u8 url
            var url = "";
            // http request header
            var header = "";
            // video save directory
            var workDir = @"D:\Temp";
            // video save name
            var saveName = "Video";

            Console.WriteLine("Start Download...");

            var hlsDL = new HlsDL();

            // Download m3u8 manifest by url
            var (manifest, m3u8Url) = await hlsDL.GetManifestAsync(url, header);

            // Check master
            if (manifest.IsMaster())
            {
                // Parse m3u8 manifest to master playlist
                var masterPlaylist = hlsDL.ParseMasterPlaylist(manifest, m3u8Url);

                // Choose the highest quality resolution
                var highestStreamInfo = masterPlaylist.StreamInfos
                    .GetWithHighestQuality();
                if (highestStreamInfo == null)
                    throw new Exception("Not found stream info.");
                (manifest, m3u8Url) = await hlsDL.GetManifestAsync(
                    highestStreamInfo.Uri, header);
            }

            // Parse m3u8 manifest to media playlist
            var mediaPlaylist = hlsDL.ParseMediaPlaylist(manifest, m3u8Url);

            // Download m3u8 segment key
            var keys = null as Dictionary<string, string>;
            var segmentKeys = hlsDL.GetKeys(mediaPlaylist.Parts);
            if (segmentKeys.Count > 0)
                keys = await hlsDL.GetKeysDataAsync(segmentKeys, header);

            // Download m3u8 ts files
            await hlsDL.DownloadAsync(workDir, saveName,
                mediaPlaylist.Parts, header, keys,
                threads: 4, maxSpeed: 5 * 1024 * 1024,
                onSegment: async (ms, token) =>
                {
                    // Detect and skip png header
                    return await ms.TrySkipPngHeaderAsync(token);
                },
                progress: (args) =>
                {
                    var print = args.Format;
                    var sub = Console.WindowWidth - 2 - print.Length;
                    Console.Write("\r" + print + new string(' ', sub) + "\r");
                });

            Console.WriteLine("\nStart Merge...");

            // Merge m3u8 ts files by FFmpeg
            await hlsDL.MergeAsync(workDir, saveName, 
                clearTempFile: true,
                onMessage: (msg) =>
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.Write(msg);
                    Console.ResetColor();
                });
            Console.WriteLine("Finish.");
            Console.ReadLine();
        }

        public static async Task REC(string[] args)
        {
            // m3u8 url
            var url = "";
            // http request header
            var header = "";
            // video save directory
            var workDir = @"D:\Temp";
            // video save name
            var saveName = "Live";

            Console.WriteLine("Start REC...");

            var hlsDL = new HlsDL();

            // Download m3u8 manifest to media playlist
            var mediaPlaylist = await hlsDL.GetMediaPlaylistAsync(url, header);

            // Is a live stream
            if (mediaPlaylist.IsLive())
            {
                var cts = new CancellationTokenSource();
                // Check REC stop
                CheckStop(cts);

                try
                {
                    await hlsDL.REC(
                        workDir, saveName, url, header, 
                        progress: (args) =>
                        {
                            var print = args.Format;
                            var sub = Console.WindowWidth - 2 - print.Length;
                            Console.Write("\r" + print + new string(' ', sub) + "\r");
                        },
                        token: cts.Token);
                }
                catch { }

                Console.WriteLine("\nStart Merge...");

                // Merge m3u8 ts files by FFmpeg
                await hlsDL.MergeAsync(workDir, saveName,
                    clearTempFile: false,
                    onMessage: (msg) =>
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.Write(msg);
                        Console.ResetColor();
                    });
                Console.WriteLine("Finish.");
                Console.ReadLine();
            }
        }

        public static async Task Muxing(string[] args)
        {
            // m3u8 url
            var url = "";
            // http request header
            var header = "";
            // video save directory
            var workDir = @"D:\Temp";
            // video save name
            var saveName = "Video";
            var videoSaveName = $"{saveName}(Video)";
            var audioSaveName = $"{saveName}(Audio)";

            // Filter special characters
            saveName = saveName.FilterFileName();

            Console.WriteLine("Start Download...");

            var hlsDL = new HlsDL();

            // Download master manifest by url
            var masterPlaylist = await hlsDL.GetMasterPlaylistAsync(url, header);

            // Download video m3u8 manifest by url
            var highestStreamInfo = masterPlaylist.StreamInfos
                .GetWithHighestQuality();
            if (highestStreamInfo == null)
                throw new Exception("Not found stream info.");
            var videoPlaylist = await hlsDL.GetMediaPlaylistAsync(
                highestStreamInfo.Uri, header);

            // Download audio m3u8 manifest by url
            var audioMediaGroup = masterPlaylist.MediaGroups
                .Where(it => it.GroupId == highestStreamInfo.Audio)
                .FirstOrDefault();
            if (audioMediaGroup == null)
                throw new Exception("Not found audio media.");
            var audioPlaylist = await hlsDL.GetMediaPlaylistAsync(
                audioMediaGroup.Uri, header);

            // Download and merge video and audio
            await downloadMerge("video", videoSaveName, videoPlaylist);
            await downloadMerge("audio", audioSaveName, audioPlaylist);

            // Muxing video source and audio source
            await hlsDL.MuxingAsync(workDir, saveName,
                Path.Combine(workDir, $"{videoSaveName}.ts"),
                Path.Combine(workDir, $"{audioSaveName}.ts"),
                outputFormat: MuxOutputFormat.MP4,
                clearSource: false,
                onMessage: (msg) =>
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.Write(msg);
                    Console.ResetColor();
                });
            Console.WriteLine("Finish.");
            Console.ReadLine();

            async Task downloadMerge(string id, string saveName, MediaPlaylist mediaPlaylist)
            {
                // Download m3u8 segment key
                var keys = null as Dictionary<string, string>;
                var segmentKeys = hlsDL.GetKeys(mediaPlaylist.Parts);
                if (segmentKeys.Count > 0)
                    keys = await hlsDL.GetKeysDataAsync(segmentKeys, header);

                Console.WriteLine($"Start {id} Download...");

                // Download m3u8 ts files
                await hlsDL.DownloadAsync(workDir, saveName,
                    mediaPlaylist.Parts, header, keys,
                    threads: 4, maxSpeed: 5 * 1024 * 1024,
                    progress: (args) =>
                    {
                        var print = args.Format;
                        var sub = Console.WindowWidth - 2 - print.Length;
                        Console.Write("\r" + print + new string(' ', sub) + "\r");
                    });

                Console.WriteLine($"\nStart {id} Merge...");

                // Merge m3u8 ts files by FFmpeg
                await hlsDL.MergeAsync(workDir, saveName,
                    clearTempFile: true, binaryMerge: true,
                    outputFormat: OutputFormat.TS,
                    onMessage: (msg) =>
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.Write(msg);
                        Console.ResetColor();
                    });
            }
        }

        private static void CheckStop(CancellationTokenSource cts)
        {
            var task = Task.Run(() =>
            {
                while (true)
                {
                    var input = Console.ReadLine();
                    if (input == "q")
                    {
                        cts.Cancel();
                        break;
                    }
                }
            });
        }
    }
}