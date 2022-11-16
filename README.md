# VideoDL-m3u8  

This is a m3u8 video downloader which can download ts files and merge to mp4 video using FFmpeg.  

* Support m3u8 manifest parsing  
* Support .ts file download  
* Support AES-128 decryption  
* Support custom http header  
* Support multi-thread download  
* Support download speed limit  
* Support resuming from breakpoint  
* Support FFmpeg to merge .ts files  
* Support png header detection (undone)  
* Support http proxy (undone)  

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

// Download m3u8 encryption key
var keys = null as Dictionary<string, string>;
var keyUrls = hlsDL.GetKeyUrls(mediaPlaylist.Parts);
if (keyUrls.Count > 0)
    keys = await hlsDL.GetKeysAsync(keyUrls, header);

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
　Set m3u8 encryption key.  

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
    bool clearTempFile = true, 
    Action<string>? onMessage = null,
    CancellationToken token = default)
```

* **workDir:** string, required  
　Set video download directory.  

* **saveName:** string, required  
　Set video save name.  

* **clearTempFile:** bool, optional, default: true  
　Set whether to clear the temporary file after the merge is completed.  

* **onMessage:** Action\<string\>, optional, default: null  
　Set callback function for FFmpeg warning or error messages.  

* **token:** CancellationToken, optional, default: default  
　Set cancellation token.  