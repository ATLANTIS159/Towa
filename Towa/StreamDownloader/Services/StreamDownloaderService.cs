using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Towa.Settings;
using Towa.StreamDownloader.Logger.Interfaces;
using Towa.StreamDownloader.Services.Interfaces;
using Towa.Twitch.Api.Services.Interfaces;

namespace Towa.StreamDownloader.Services;

public class StreamDownloaderService : IStreamDownloaderService
{
    private const string GqlUrl = "https://gql.twitch.tv/gql";
    private const string PlaylistsUrl = "https://usher.ttvnw.net/api/channel/hls/{0}.m3u8?{1}";

    private static readonly string AppPath = $"{AppContext.BaseDirectory}";
    private static readonly string DownloaderFolder = Path.Combine(AppPath, "Downloader");
    private static readonly string TempFfmpegFolder = Path.Combine(DownloaderFolder, "Temp");
    private readonly IStreamDownloaderLogger _logger;
    private readonly IStreamDownloaderWoConsoleLogger _loggerWoConsole;
    private readonly string _outputFolder = Path.Combine(DownloaderFolder, "Output");
    private readonly string _partsFolder = Path.Combine(DownloaderFolder, "Parts");
    private readonly Regex _rxResolution = new("RESOLUTION\\=(?<resolution>.*?),");
    private readonly IOptionsMonitor<CoreSettings> _settings;
    private readonly ITwitchApiService _twitchApiService;

    public StreamDownloaderService(IOptionsMonitor<CoreSettings> settings, ITwitchApiService twitchApiService,
        IStreamDownloaderLogger logger, IStreamDownloaderWoConsoleLogger loggerWoConsole)
    {
        _settings = settings;
        _twitchApiService = twitchApiService;
        _logger = logger;
        _loggerWoConsole = loggerWoConsole;
    }

    public async Task StartDownload()
    {
        _logger.Log.Information("Запущена загрузка прямой трансляции стрима");
        var dateTime = $"{DateTime.Now:yyyy.MM.dd HH.mm.ss}";
        var date = $"{DateTime.Now:dd.MM.yyyy HH.mm.ss}";

        if (!Directory.Exists(DownloaderFolder)) Directory.CreateDirectory(DownloaderFolder);

        if (!Directory.Exists(_partsFolder)) Directory.CreateDirectory(_partsFolder);

        if (!Directory.Exists(_outputFolder)) Directory.CreateDirectory(_outputFolder);

        var tempFolder = Path.Combine(_partsFolder, dateTime);

        if (!Directory.Exists(tempFolder)) Directory.CreateDirectory(tempFolder);

        var streamInfo = _twitchApiService.GetChannelInfo(_settings.CurrentValue.Twitch.JoinChannel.ToLower());

        if (!streamInfo.isSuccess || streamInfo.channelInfo is null)
        {
            _logger.Log.Error(
                "Загрузка прямой трансляции стрима прервана. Не найдена информация о стриме или произошла ошибка при получении данных");
            return;
        }

        var streamTitle = Regex.Replace(streamInfo.channelInfo.Title, "[\\/:*?\"<>|]", "_");

        _logger.Log.Information("Название стрима | {StreamTitle}", streamTitle);

        var tempFilename = ConvertFilenameToDefaultEncoding($"{dateTime}_{streamTitle}.ts");
        var outputFilename = ConvertFilenameToDefaultEncoding($"{date} {streamTitle}.mp4");

        var streamAuthInfo = GetStreamAuthInfo();

        if (!streamAuthInfo.isSuccess)
        {
            _logger.Log.Error(
                "Загрузка прямой трансляции стрима прервана. Не найдены данные авторизации для получения информации о стриме или произошла ошибка при получении данных");
            return;
        }

        var playlistLink = await GetM3U8Link(streamAuthInfo);
        await DownloadStreamFiles(tempFolder, playlistLink);
        await ConnectFiles(tempFolder, tempFilename);
        await ConvertFile(tempFolder, tempFilename, _outputFolder, outputFilename);

        _logger.Log.Information("Загрузка прямой трансляции стрима завершена");
    }

    private static string ConvertFilenameToDefaultEncoding(string filename)
    {
        return Encoding.Default.GetString(Encoding.Default.GetBytes(filename));
    }

    private HttpClient CreateGqlHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Client-ID", _settings.CurrentValue.StreamDownloaderSettings.UniqueId);
        client.DefaultRequestHeaders.Add("Authorization",
            $"OAuth {_settings.CurrentValue.StreamDownloaderSettings.AuthToken}");

        return client;
    }

    private string CreateGqlPlaybackAccessToken()
    {
        return
            "{\"operationName\": \"PlaybackAccessToken_Template\", \"query\": \"query PlaybackAccessToken_Template($login: String!, $isLive: Boolean!, $vodID: ID!, $isVod: Boolean!, $playerType: String!) { streamPlaybackAccessToken(channelName: $login, params: {platform: \\\"web\\\", playerBackend: \\\"mediaplayer\\\", playerType: $playerType}) @include(if: $isLive) {    value    signature    __typename  }  videoPlaybackAccessToken(id: $vodID, params: {platform: \\\"web\\\", playerBackend: \\\"mediaplayer\\\", playerType: $playerType}) @include(if: $isVod) {    value    signature    __typename  }}\", \"variables\": { \"isLive\": true, \"login\": \"" +
            _settings.CurrentValue.Twitch.JoinChannel.ToLower() +
            "\", \"isVod\": false, \"vodID\": \"\", \"playerType\": \"site\" }}";
    }

    private (bool isSuccess, string token, string signature) GetStreamAuthInfo()
    {
        using var client = CreateGqlHttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, GqlUrl);
        var query = CreateGqlPlaybackAccessToken();
        request.Content = new StringContent(query, Encoding.UTF8, "application/json");
        var response = client.SendAsync(request).Result;
        var accessTokenString = response.Content.ReadAsStringAsync().Result;

        var accessTokenJson = JObject.Parse(accessTokenString);
        var spaToken = accessTokenJson.SelectToken("$.data.streamPlaybackAccessToken", false);

        if (spaToken == null)
        {
            _logger.Log.Error("Произошла ошибка при получении токена проигрывания (video playback access token)");
            return (false, "", "");
        }

        var token = spaToken.Value<string>("value");
        var signature = spaToken.Value<string>("signature")!;

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.Log.Error("Произошла ошибка при получении токена доступа к стриму (Stream access token)");
            return (false, "", "");
        }

        if (!string.IsNullOrWhiteSpace(signature)) return (true, token, signature);

        _logger.Log.Error("Произошла ошибка при получении сигнатуры стрима (Stream signature)");
        return (false, "", "");
    }

    private async Task<string> GetM3U8Link((bool isSuccess, string token, string signature) streamAuthInfo)
    {
        var loop = true;
        var url = "";

        while (loop)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Accept", "*/*");

            var rand = new Random();

            var query = HttpUtility.ParseQueryString(string.Empty);

            query["acmb"] = "e30=";
            query["allow_source"] = "true";
            query["allow_audio_only"] = "true";
            query["p"] = rand.Next(200000, 800000).ToString();
            query["player_backend"] = "mediaplayer";
            query["playlist_include_framerate"] = "true";
            query["reassignments_supported"] = "true";
            query["sig"] = streamAuthInfo.signature;
            query["supported_codecs"] = "avc1";
            query["token"] = streamAuthInfo.token;
            query["cdm"] = "cdm";
            query["player_version"] = "1.17.0";

            var playlist = await client
                .GetStringAsync(string.Format(PlaylistsUrl, _settings.CurrentValue.Twitch.JoinChannel.ToLower(),
                    query));

            var lines = playlist.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                if (line.StartsWith("#")) continue;

                var streamInfo = lines[i - 1];
                var resolutionMatch = _rxResolution.Match(streamInfo);

                if (resolutionMatch.Success && resolutionMatch.Groups["resolution"].Value == "1920x1080")
                {
                    _logger.Log.Information("Cтрим в качестве 1080p найден");
                    url = line;
                    loop = false;
                    break;
                }

                if (!lines[i - 2].Contains("(source)")) continue;

                _logger.Log.Warning("Внимание! Cтрим закачивается не в 1080p!");
                url = line;
                loop = false;
                break;
            }

            if (loop) await Task.Delay(TimeSpan.FromSeconds(2));
        }

        return url;
    }

    private async Task DownloadStreamFiles(string tempDir, string playlistLink)
    {
        List<(double length, string remoteFile, string localFile)> downloadedChunks = new();

        _logger.Log.Information("Запущена загрузка файлов прямой трансляции стрима");
        var httpClient = new HttpClient();

        var success = false;
        var retryCounter = 0;
        var isEnd = false;
        var counter = 0;

        do
        {
            try
            {
                while (!isEnd)
                {
                    var playlist = await httpClient.GetStringAsync(playlistLink);
                    var chunks = ParsePlaylist(tempDir, playlist, downloadedChunks, counter);

                    isEnd = chunks.isEnd;
                    counter = chunks.counter;

                    var downloadTasks = chunks.chunks
                        .Select(async chunk => await DownloadChunkAsync(httpClient, chunk)).ToList();
                    await Task.WhenAll(downloadTasks);

                    downloadedChunks.AddRange(chunks.chunks);

                    retryCounter = 0;

                    await Task.Delay(TimeSpan.FromSeconds(2));
                }

                success = true;
            }
            catch (Exception)
            {
                if (retryCounter < 10)
                {
                    retryCounter++;
                    await Task.Delay(1000);
                }
                else
                {
                    _logger.Log.Error("Не удалось загрузить файл плейлиста после {RetryCount} попыток", retryCounter);

                    success = true;
                }
            }
        } while (!success);

        _logger.Log.Information("Загрузка файлов прямой трансляции стрима завершена");
    }

    private static (bool isEnd, int counter, List<(double length, string remoteFile, string localFile)> chunks)
        ParsePlaylist(string tempDir, string playlist,
            List<(double length, string remoteFile, string localFile)> downloadedChunks, int counter)
    {
        var lines = playlist.Split('\n').Select(l => l.Trim()).ToList();
        var chunks = new List<(double length, string url)>();
        var isEnd = false;

        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("#EXT-X-ENDLIST")) isEnd = true;

            if (!lines[i].StartsWith("#EXTINF:")) continue;

            var length =
                Math.Max(
                    double.Parse(
                        lines[i].Replace(",live", "")[(lines[i].LastIndexOf(":", StringComparison.Ordinal) + 1)..]
                            .TrimEnd(','),
                        NumberStyles.Any, CultureInfo.InvariantCulture), 0);

            chunks.Add((length, lines[i + 1]));
        }

        var newChunks = chunks.Where(c => !downloadedChunks.Exists(dc => dc.remoteFile == c.url)).ToList();

        var parts = new List<(double length, string remoteFile, string localFile)>();

        foreach (var newChunk in newChunks)
        {
            parts.Add((newChunk.length, newChunk.url, Path.Combine(tempDir, counter.ToString("D8") + ".ts")));

            counter++;
        }

        return (isEnd, counter, parts);
    }

    private async Task DownloadChunkAsync(HttpClient httpClient,
        (double length, string remoteFile, string localFile) chunk)
    {
        var success = false;
        var retryCounter = 0;

        do
        {
            try
            {
                var responseBytes = await httpClient.GetByteArrayAsync(chunk.remoteFile);
                await File.WriteAllBytesAsync(chunk.localFile, responseBytes);

                _loggerWoConsole.Log.Information("Файл {Name} загружен", new FileInfo(chunk.localFile).Name);

                success = true;
            }
            catch (Exception e)
            {
                if (retryCounter < 10)
                {
                    retryCounter++;
                    _logger.Log.Error(
                        "Ошибка при попытке загрузки или сохранении части стрима. Сообщение ошибки: {Message}",
                        e.Message);

                    await Task.Delay(1000);
                }
                else
                {
                    _logger.Log.Error(
                        "Не удалось загрузить файл по ссылке '{RemoteFile}' после {RetryCount} попыток! Локальный файл '{LocalFile}'",
                        chunk.remoteFile, retryCounter, chunk.localFile);

                    success = true;
                }
            }
        } while (!success);
    }

    private async Task ConnectFiles(string tempDir, string tempFilename)
    {
        var list = Directory.GetFiles(tempDir, "*.ts", SearchOption.TopDirectoryOnly).OrderBy(item => item).ToList();
        var lastFile = list[^1];
        // File.Delete(lastFile);
        list.RemoveAt(list.Count - 1);

        _logger.Log.Information("Объединение файлов запущено");

        await using var outputStream =
            new FileStream(Path.Combine(tempDir, tempFilename), FileMode.OpenOrCreate, FileAccess.Write);

        foreach (var item in list)
        {
            await using var partStream = new FileStream(item, FileMode.Open, FileAccess.Read);

            int maxBytes;
            var buffer = new byte[4096];

            while ((maxBytes = partStream.Read(buffer, 0, buffer.Length)) > 0)
                outputStream.Write(buffer, 0, maxBytes);
            _loggerWoConsole.Log.Information("Файл {File} объединён с основным", item);
        }

        _logger.Log.Information("Объединение файлов завершено");
    }

    private async Task ConvertFile(string tempDir, string tempFilename, string outputDir, string outputFilename)
    {
        var isSuccess = false;
        var retryCount = 0;

        _logger.Log.Information("Запущена конвертация файла");

        if (!Directory.Exists(TempFfmpegFolder)) Directory.CreateDirectory(TempFfmpegFolder);

        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();

        string ffmpegResourceName;
        Stream? resourceStream = null;
        var tempFfmpegFile = "";

        switch (Environment.OSVersion.Platform)
        {
            case PlatformID.Win32NT:
                ffmpegResourceName = resourceNames.First(n => n.EndsWith("ffmpeg.exe"));
                resourceStream = assembly.GetManifestResourceStream(ffmpegResourceName);

                tempFfmpegFile = Path.Combine(TempFfmpegFolder, "ffmpeg.exe");
                break;
            case PlatformID.Unix:
                ffmpegResourceName = resourceNames.First(n => n.EndsWith("ffmpeg"));
                resourceStream = assembly.GetManifestResourceStream(ffmpegResourceName);

                tempFfmpegFile = Path.Combine(TempFfmpegFolder, "ffmpeg");
                break;
        }

        await using (var fileStream = new FileStream(tempFfmpegFile, FileMode.Create))
        {
            await resourceStream?.CopyToAsync(fileStream)!;
        }

        var inputFile = Path.Combine(tempDir, tempFilename);
        var outputFile = Path.Combine(outputDir, outputFilename);

        do
        {
            await Process.Start("chmod", $"777 {tempFfmpegFile}").WaitForExitAsync();
            var psi = new ProcessStartInfo
            {
                FileName = tempFfmpegFile,
                Arguments =
                    $"-i \"{inputFile}\" -analyzeduration {int.MaxValue} -probesize {int.MaxValue} -fps_mode 1 -c:v copy -preset p7 -c:a copy \"{outputFile}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                RedirectStandardInput = true
            };

            using var process = new Process();
            DataReceivedEventHandler outputData = OutputConvertData;
            DataReceivedEventHandler errorData = OutputConvertErrors;

            process.OutputDataReceived += outputData;
            process.ErrorDataReceived += errorData;
            process.StartInfo = psi;
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                _logger.Log.Information("Конвертация файла завершена");

                File.Delete(tempFfmpegFile);
                isSuccess = true;
            }
            else
            {
                if (retryCount < 15)
                {
                    retryCount++;
                    _logger.Log.Warning(
                        "Конвертация файла завершена с ошибкой. Попытка повторной конвертации. Количество неудачных попыток: {RetryCount}",
                        retryCount);
                    await Task.Delay(1000);
                }
                else
                {
                    _logger.Log.Error("После {RetryCount} неудачных попыток, конвертация файла прервана!", retryCount);

                    File.Delete(tempFfmpegFile);
                    isSuccess = true;
                }
            }
        } while (!isSuccess);
    }

    private void OutputConvertData(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null && e.Data.StartsWith("frame=")) _loggerWoConsole.Log.Information(e.Data);
    }

    private void OutputConvertErrors(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null) _loggerWoConsole.Log.Information(e.Data);
    }
}