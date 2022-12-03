# VideoDL-m3u8  

This is a m3u8 video downloader which can download ts files and merge to mp4 video using FFmpeg.  

* Support m3u8 manifest parsing  
* Support ts or fmp4 file download  
* Support AES-128 decryption  
* Support m3u8 byte range  
* Support custom http header  
* Support multi-thread download  
* Support download speed limit  
* Support resuming from breakpoint  
* Support FFmpeg merge to mp4 video  
* Support png header detection  
* Support http or socks5 proxy  
* Support live stream record  
* Support output format conversion  
* Support m3u8 EXT-X-MEDIA  
* Support muxing video and audio  
* Support mpd manifest parsing (undone)  

---  

### Nuget install  

```
PM> Install-Package VideoDL-m3u8
```

---  

### FFmpeg  

Download and unzip to your project directory.  

[https://github.com/BtbN/FFmpeg-Builds/releases](https://github.com/BtbN/FFmpeg-Builds/releases)  

* ffmpeg-master-latest-win64-gpl.zip  

![Demo/1668521456402.jpg](Demo/1668521456402.jpg)  

---  

### Demo  

![Demo/1668426801294.jpg](Demo/1668426801294.jpg)  

---  

### Example  

```C#
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
await hlsDL.MergeAsync(workDir, saveName, true,
    onMessage: (msg) =>
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write(msg);
        Console.ResetColor();
    });
Console.WriteLine("Finish.");
Console.ReadLine();
```

---  

### Proxy example  

```C#
var hlsDL = new HlsDL(
    proxy: "http://127.0.0.1:8000");
// or
var hlsDL = new HlsDL(
    proxy: "socks5://127.0.0.1:8000");
```

---  

### REC Example  

```C#
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
}
```

---  

### Muxing Example  

```C#
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
```

---  

### Documentation  

```C#
// Download m3u8 ts files
public async Task DownloadAsync(
    string workDir, 
    string saveName, 
    List<Part> parts,
    string header = "", 
    Dictionary<string, string>? keys = null,
    int threads = 1, 
    int delay = 200, 
    int maxRetry = 20,
    long? maxSpeed = null, 
    int interval = 1000,
    bool checkComplete = false,
    Func<Stream, CancellationToken, Task<Stream>>? onSegment = null,
    Action<ProgressEventArgs>? progress = null,
    CancellationToken token = default)
```

* **workDir:** string, required  
　Set video download directory.  

* **saveName:** string, required  
　Set video save name.  

* **parts:** List\<Part\>, required  
　Set m3u8 media playlist parts to download.  

* **header:** string, optional, default: ""  
　Set http request header.  
　format: key1:key1|key2:key2  
        
* **keys:** Dictionary\<string, string\>, optional, default: null  
　Set m3u8 segment keys.  

* **threads:** int, optional, default: 1  
　Set the number of threads to download.  

* **delay:** int, optional, default: 200  
　Set http request delay. (millisecond)  

* **maxRetry:** int, optional, default: 20  
　Set the maximum number of download retries.  

* **maxSpeed:** long?, optional, default: null  
　Set the maximum download speed. (byte)  
　1KB = 1024 byte, 1MB = 1024 * 1024 byte  
        
* **interval:** int, optional, default: 1000  
　Set the progress callback time interval. (millisecond)  

* **checkComplete:** bool, optional, default: true  
　Set whether to check file count complete.  

* **onSegment:** Func<Stream, CancellationToken, Task\<Stream\>\>, optional, default: null  
　Set segment download callback.  

* **progress:** Action\<ProgressEventArgs\>, optional, default: null  
　Set progress callback.  

* **token:** CancellationToken, optional, default: default  
　Set cancellation token.  

---  

```C#
// Merge m3u8 ts files
public async Task MergeAsync(
    string workDir, 
    string saveName,
    OutputFormat outputFormat = OutputFormat.MP4,
    bool binaryMerge = false,
    bool clearTempFile = true, 
    Action<string>? onMessage = null,
    CancellationToken token = default)
```

* **workDir:** string, required  
　Set video download directory.  

* **saveName:** string, required  
　Set video save name.  

* **outputFormat:** OutputFormat, optional, default: MP4  
　Set video output format.  

* **binaryMerge:** bool, optional, default: false  
　Set use binary merge.  

* **clearTempFile:** bool, optional, default: false  
　Set whether to clear the temporary file after the merge is completed.  

* **onMessage:** Action\<string\>, optional, default: null  
　Set callback function for FFmpeg warning or error messages.  

* **token:** CancellationToken, optional, default: default  
　Set cancellation token.  

---  

```C#
// REC m3u8 live stream
public async Task REC(
    string workDir, 
    string saveName,
    string url, 
    string header = "", 
    int maxRetry = 20, 
    long? maxSpeed = null, 
    int interval = 1000, 
    int? noSegStopTime = null,
    Func<Stream, CancellationToken, Task<Stream>>? onSegment = null,
    Action<RecProgressEventArgs>? progress = null,
    CancellationToken token = default)
```

* **workDir:** string, required  
　Set video download directory.  

* **saveName:** string, required  
　Set video save name.  

* **url:** string, required  
　Set m3u8 live stream url.  

* **header:** string, optional, default: ""  
　Set http request header.  
　format: key1:key1|key2:key2  

* **maxRetry:** int, optional, default: 20  
　Set the maximum number of download retries.  

* **maxSpeed:** long?, optional, default: null  
　Set the maximum download speed. (byte)  
　1KB = 1024 byte, 1MB = 1024 * 1024 byte  

* **interval:** int, optional, default: 1000  
　Set the progress callback time interval. (millisecond)  

* **noSegStopTime:** int?, optional, default: null  
　Set how long to stop after when there is no segment. (millisecond)  

* **onSegment:** Func<Stream, CancellationToken, Task\<Stream\>\>, optional, default: null  
　Set segment download callback.  

* **progress:** Action\<RecProgressEventArgs\>, optional, default: null  
　Set progress callback.  

* **token:** CancellationToken, optional, default: default  
　Set cancellation token.  

---  

```C#
// Muxing video source and audio source
public async Task MuxingAsync(
    string workDir, 
    string saveName,
    string videoSourcePath, 
    string audioSourcePath,
    MuxOutputFormat outputFormat = MuxOutputFormat.MP4,
    bool clearSource = false, 
    Action<string>? onMessage = null,
    CancellationToken token = default)
```

* **workDir:** string, required  
　Set video save directory.  

* **saveName:** string, required  
　Set video save name.  

* **videoSourcePath:** string, required  
　Set video source path.  

* **audioSourcePath:** string, required  
　Set audio source path.  

* **outputFormat:** MuxOutputFormat, optional, default: MP4  
　Set video output format.  

* **clearSource:** bool, optional, default: false  
　Set whether to clear source file after the muxing is completed.  

* **onMessage:** Action\<string\>, optional, default: null  
　Set callback function for FFmpeg warning or error messages.  

* **token:** CancellationToken, optional, default: default  
　Set cancellation token.  
