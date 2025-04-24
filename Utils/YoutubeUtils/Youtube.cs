using MelonLoader;
using System.Collections;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace CustomTV.Utils.YoutubeUtils
{
    class Youtube
    {
        public class DownloadResult(string filePath = null, bool isAgeRestricted = false)
        {
            public string FilePath { get; set; } = filePath;
            public bool IsAgeRestricted { get; set; } = isAgeRestricted;
            public bool Success { get; set; } = !string.IsNullOrEmpty(filePath) && File.Exists(filePath);
        }

        public static IEnumerator DownloadYoutubeVideo(string videoUrl, int currentIndex, int totalVideos, Action<DownloadResult> onComplete)
        {
            string tempOutputPath = null;
            bool isAgeRestricted = false;
            bool downloadSuccess = false;
            bool alreadyTried = false;
            bool useFirefoxCookies = Config.UseFirefoxCookies;

            IEnumerator TryDownloadVideo(bool withCookies)
            {
                string outputFileName = $"yt_{DateTime.Now.Ticks}.mp4";
                tempOutputPath = Path.Combine(Config.YoutubeTempFolder, outputFileName);
                tempOutputPath = Path.GetFullPath(tempOutputPath);

                downloadSuccess = false;
                isAgeRestricted = false;

                CustomTV.customTVState.DownloadProgress = 0f;
                if (withCookies)
                {
                    CustomTV.customTVState.DownloadStatus = $"Downloading video {currentIndex}/{totalVideos} with Firefox cookies";
                }
                else
                {
                    CustomTV.customTVState.DownloadStatus = $"Downloading video {currentIndex}/{totalVideos}";
                }
                UI.UpdateDownloadProgressUI(0, CustomTV.customTVState.DownloadStatus);

                Task downloadTask = Task.Run(() =>
                {
                    try
                    {
                        using (Process process = new())
                        {
                            process.StartInfo.FileName = Path.Combine(Config.YtDlpFolderPath, "yt-dlp.exe");
                            string safeOutputPath = Path.GetFullPath(tempOutputPath).Replace('\\', '/');

                            StringBuilder args = new();
                            args.Append("--newline ");

                            if (withCookies)
                            {
                                args.Append("--cookies-from-browser firefox ");
                            }

                            args.Append("-f \"best[ext=mp4][height<=1080]/best[ext=mp4]/best\" ");
                            args.Append("--merge-output-format mp4 ");
                            args.Append("--progress-template \"%(progress.downloaded_bytes)s/%(progress.total_bytes)s %(progress.eta)s %(progress.speed)s\" ");
                            args.Append($"-o \"{safeOutputPath}\" ");
                            args.Append($"\"{videoUrl}\"");

                            process.StartInfo.Arguments = args.ToString();
                            process.StartInfo.UseShellExecute = false;
                            process.StartInfo.CreateNoWindow = true;
                            process.StartInfo.RedirectStandardOutput = true;
                            process.StartInfo.RedirectStandardError = true;

                            bool hasFirefoxCookiesError = false;

                            process.OutputDataReceived += (sender, args) =>
                            {
                                if (args.Data == null) return;

                                string line = args.Data;
                                try
                                {
                                    if (withCookies && line.Contains("Firefox") &&
                                        (line.Contains("error") || line.Contains("not found") || line.Contains("failed")))
                                    {
                                        hasFirefoxCookiesError = true;
                                        MelonLogger.Warning($"Firefox cookie error: {line}");
                                    }

                                    lock (CustomTV.customTVState.DownloadLock)
                                    {
                                        if (line.Contains("/") && line.Contains(" "))
                                        {
                                            string[] parts = line.Split(' ');
                                            if (parts.Length >= 1)
                                            {
                                                string[] progressParts = parts[0].Split('/');
                                                if (progressParts.Length == 2)
                                                {
                                                    if (long.TryParse(progressParts[0], out long downloaded) &&
                                                        long.TryParse(progressParts[1], out long total) &&
                                                        total > 0)
                                                    {
                                                        CustomTV.customTVState.DownloadProgress = (float)downloaded / total;

                                                        string eta = parts.Length > 1 ? parts[1] : "";
                                                        string speed = parts.Length > 2 ? parts[2] : "";

                                                        double downloadedMB = downloaded / 1024.0 / 1024.0;
                                                        double totalMB = total / 1024.0 / 1024.0;

                                                        CustomTV.customTVState.DownloadStatus = $"Video {currentIndex}/{totalVideos}: {downloadedMB:F1}/{totalMB:F1} MB - {CustomTV.customTVState.DownloadProgress * 100:F0}% - {speed}";
                                                    }
                                                }
                                            }
                                        }
                                        else if (line.Contains("Merging formats"))
                                        {
                                            CustomTV.customTVState.DownloadProgress = 0.95f;
                                            CustomTV.customTVState.DownloadStatus = $"Video {currentIndex}/{totalVideos}: Merging video and audio...";
                                        }
                                        else if (line.Contains("Downloading") && line.Contains("destfile"))
                                        {
                                            CustomTV.customTVState.DownloadProgress = 0.05f;
                                            CustomTV.customTVState.DownloadStatus = $"Video {currentIndex}/{totalVideos}: Starting download...";
                                        }
                                        else if (line.StartsWith("[download]") && line.Contains("%"))
                                        {
                                            int percentIndex = line.IndexOf("%");
                                            if (percentIndex > 10)
                                            {
                                                string percentStr = line.Substring(10, percentIndex - 10).Trim();
                                                if (float.TryParse(percentStr, out float percent))
                                                {
                                                    CustomTV.customTVState.DownloadProgress = percent / 100f;
                                                    CustomTV.customTVState.DownloadStatus = $"Video {currentIndex}/{totalVideos}: " + line.Substring(percentIndex + 1).Trim();
                                                }
                                            }
                                        }
                                        else if (line.Contains("destination"))
                                        {
                                            CustomTV.customTVState.DownloadStatus = $"Video {currentIndex}/{totalVideos}: Preparing download...";
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MelonLogger.Warning($"Error parsing progress: {ex.Message}");
                                }
                            };

                            process.ErrorDataReceived += (sender, args) =>
                            {
                                if (args.Data != null)
                                {
                                    if (args.Data.Contains("Sign in to confirm your age") ||
                                        args.Data.Contains("age-restricted") ||
                                        args.Data.Contains("Age verification"))
                                    {
                                        isAgeRestricted = true;
                                        MelonLogger.Warning($"yt-dlp age restriction: {args.Data}");
                                    }

                                    if (withCookies && args.Data.Contains("Firefox") &&
                                       (args.Data.Contains("error") || args.Data.Contains("not found") || args.Data.Contains("failed")))
                                    {
                                        hasFirefoxCookiesError = true;
                                        MelonLogger.Warning($"Firefox cookie error: {args.Data}");
                                    }
                                    else
                                    {
                                        MelonLogger.Warning($"yt-dlp error: {args.Data}");
                                    }
                                }
                            };

                            MelonLogger.Msg($"Starting download for video {currentIndex}/{totalVideos}" +
                                (withCookies ? " with Firefox cookies" : ""));

                            process.Start();
                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();

                            if (process.WaitForExit(300000))
                            {
                                if (process.ExitCode == 0)
                                {
                                    MelonLogger.Msg($"Download completed for video {currentIndex}/{totalVideos}");
                                    downloadSuccess = true;

                                    FileInfo fileInfo = new(tempOutputPath);
                                    if (!fileInfo.Exists || fileInfo.Length == 0)
                                    {
                                        MelonLogger.Error($"Download reported success but file is empty or missing: {tempOutputPath}");
                                        downloadSuccess = false;
                                    }
                                }
                                else
                                {
                                    if (isAgeRestricted)
                                    {
                                        if (withCookies && hasFirefoxCookiesError)
                                        {
                                            if (!CustomTV.customTVState.WarnedAboutCookies)
                                            {
                                                MelonLogger.Warning("Failed to access Firefox cookies. Will try without cookies.");
                                                MelonLogger.Warning("Make sure Firefox is installed and you've logged into YouTube in Firefox.");
                                                CustomTV.customTVState.WarnedAboutCookies = true;
                                            }
                                        }
                                        else if (withCookies)
                                        {
                                            MelonLogger.Warning($"Video {currentIndex}/{totalVideos} is age-restricted and could not be downloaded even with Firefox cookies.");
                                        }
                                        else
                                        {
                                            MelonLogger.Warning($"Video {currentIndex}/{totalVideos} is age-restricted and could not be downloaded without authentication.");
                                        }
                                    }
                                    else
                                    {
                                        MelonLogger.Error($"Download failed for video {currentIndex}/{totalVideos} with exit code {process.ExitCode}");
                                    }
                                }
                            }
                            else
                            {
                                process.Kill();
                                MelonLogger.Error($"Download timeout for video {currentIndex}/{totalVideos} after 10 minutes");
                            }

                            if (withCookies && hasFirefoxCookiesError && !CustomTV.customTVState.WarnedAboutCookies)
                            {
                                MelonLogger.Warning("Failed to access Firefox cookies. Will try without cookies for this video.");
                                MelonLogger.Warning("Make sure Firefox is installed and you've logged into YouTube in Firefox.");
                                CustomTV.customTVState.WarnedAboutCookies = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"Error running yt-dlp for video {currentIndex}/{totalVideos}: {ex.Message}");
                    }
                });

                float lastProgress = -1f;
                string lastStatus = "";
                float lastUpdateTime = Time.time;
                float updateInterval = 0.1f;

                while (!downloadTask.IsCompleted)
                {
                    float currentTime = Time.time;

                    if (currentTime - lastUpdateTime > updateInterval)
                    {
                        lastUpdateTime = currentTime;

                        float currentProgress;
                        string currentStatus;

                        lock (CustomTV.customTVState.DownloadLock)
                        {
                            currentProgress = CustomTV.customTVState.DownloadProgress;
                            currentStatus = CustomTV.customTVState.DownloadStatus;
                        }

                        if (Math.Abs(currentProgress - lastProgress) > 0.001f || currentStatus != lastStatus)
                        {
                            lastProgress = currentProgress;
                            lastStatus = currentStatus;
                            UI.UpdateDownloadProgressUI(currentProgress, currentStatus);
                        }
                    }

                    yield return null;
                }

                if (!downloadSuccess && File.Exists(tempOutputPath))
                {
                    try
                    {
                        File.Delete(tempOutputPath);
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"Failed to delete partial file: {ex.Message}");
                    }
                }

                yield break;
            }

            string ytDlpExePath = Path.Combine(Config.YtDlpFolderPath, "yt-dlp.exe");
            if (!File.Exists(ytDlpExePath))
            {
                MelonLogger.Error("yt-dlp.exe not found. Cannot download YouTube videos.");
                MelonLogger.Msg($"Please download yt-dlp.exe manually from https://github.com/yt-dlp/yt-dlp/releases and place it in: {Config.YtDlpFolderPath}");
                onComplete?.Invoke(new DownloadResult(null, false));
                yield break;
            }

            yield return UI.CreateDownloadProgressUI();
            CustomTV.customTVState.ShowDownloadProgress = true;
            CustomTV.customTVState.DownloadProgress = 0f;

            if (useFirefoxCookies)
            {
                MelonLogger.Msg("Trying to download with Firefox cookies (enabled in preferences)");
                yield return TryDownloadVideo(true);

                if (!downloadSuccess && isAgeRestricted && !alreadyTried)
                {
                    alreadyTried = true;
                    MelonLogger.Msg("Download failed with cookies. Trying again without cookies...");
                    yield return TryDownloadVideo(false);
                }
            }
            else
            {
                MelonLogger.Msg("Firefox cookies disabled in preferences. Downloading without cookies.");
                yield return TryDownloadVideo(false);
            }

            CustomTV.customTVState.ShowDownloadProgress = false;
            UI.DestroyDownloadProgressUI();

            if (!downloadSuccess)
            {
                onComplete?.Invoke(new DownloadResult(null, isAgeRestricted));
                yield break;
            }

            if (!File.Exists(tempOutputPath))
            {
                MelonLogger.Error($"Output file doesn't exist after successful download: {tempOutputPath}");
                onComplete?.Invoke(new DownloadResult(null, isAgeRestricted));
                yield break;
            }

            FileInfo fileInfo = new(tempOutputPath);
            if (fileInfo.Length < 10240)
            {
                MelonLogger.Error($"Downloaded file is too small ({fileInfo.Length} bytes): {tempOutputPath}");

                try
                {
                    File.Delete(tempOutputPath);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Failed to delete invalid file: {ex.Message}");
                }

                onComplete?.Invoke(new DownloadResult(null, isAgeRestricted));
                yield break;
            }

            if (CustomTV.customTVState.YoutubeCache.Count >= Config.MaxCachedYoutubeVideos)
            {
                string oldestUrl = CustomTV.customTVState.YtCacheQueue.Dequeue();
                if (CustomTV.customTVState.YoutubeCache.TryGetValue(oldestUrl, out string oldCachePath))
                {
                    CustomTV.customTVState.YoutubeCache.Remove(oldestUrl);
                    if (File.Exists(oldCachePath))
                    {
                        try
                        {
                            File.Delete(oldCachePath);
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Warning($"Failed to delete old cache file: {ex.Message}");
                        }
                    }
                }
            }

            CustomTV.customTVState.YoutubeCache[videoUrl] = tempOutputPath;
            CustomTV.customTVState.YtCacheQueue.Enqueue(videoUrl);

            MelonLogger.Msg($"Video {currentIndex}/{totalVideos} downloaded and cached: {tempOutputPath}");
            onComplete?.Invoke(new DownloadResult(tempOutputPath, isAgeRestricted));
        }

        public static IEnumerator PlayYoutubeVideo(string url, bool isPartOfPlaylist = false, bool startPlayback = true)
        {
            if (CustomTV.customTVState.IsDownloadingYoutube)
            {
                MelonLogger.Warning("Already downloading a YouTube video. Please wait...");
                yield break;
            }

            CustomTV.customTVState.IsDownloadingYoutube = true;

            if (CustomTV.customTVState.YoutubeCache.TryGetValue(url, out string cachedPath) && File.Exists(cachedPath))
            {
                MelonLogger.Msg($"Using cached YouTube video: {cachedPath}");

                CustomTV.customTVState.YtCacheQueue.Remove(url);
                CustomTV.customTVState.YtCacheQueue.Enqueue(url);

                if (CustomTV.customTVState.PlaylistVideoQueue.Count == 1 && startPlayback)
                {
                    MelonLogger.Msg("Starting playback of cached video");

                    var playbackCoroutine = PlayCachedYoutubeVideo(cachedPath, isPartOfPlaylist);
                    CustomTV.customTVState.IsDownloadingYoutube = false;
                    yield return playbackCoroutine;
                    yield break;
                }
                else
                {
                    MelonLogger.Msg($"Added cached video to queue ({CustomTV.customTVState.PlaylistVideoQueue.Count} videos in queue)");
                }

                CustomTV.customTVState.IsDownloadingYoutube = false;
                yield break;
            }

            MelonLogger.Msg($"Downloading video from: {url}");

            string ytDlpExePath = Path.Combine(Config.YtDlpFolderPath, "yt-dlp.exe");
            if (!File.Exists(ytDlpExePath))
            {
                MelonLogger.Error("yt-dlp.exe not found. Cannot download YouTube videos.");
                MelonLogger.Msg($"Please download yt-dlp.exe manually from https://github.com/yt-dlp/yt-dlp/releases and place it in: {Config.YtDlpFolderPath}");
                CustomTV.customTVState.IsDownloadingYoutube = false;
                yield break;
            }

            DownloadResult result = null;
            yield return DownloadYoutubeVideo(url, 1, 1, (downloadResult) =>
            {
                result = downloadResult;
            });

            if (result == null || !result.Success)
            {
                if (result != null && result.IsAgeRestricted)
                {
                    MelonLogger.Warning("Video is age-restricted and cannot be downloaded without authentication");
                }
                else
                {
                    MelonLogger.Error("Failed to download YouTube video");
                }

                CustomTV.customTVState.IsDownloadingYoutube = false;
                yield break;
            }

            lock (CustomTV.customTVState.PlaylistVideoQueue)
            {
                CustomTV.customTVState.PlaylistVideoQueue.Enqueue(result.FilePath);

                if (CustomTV.customTVState.PlaylistVideoQueue.Count == 1 && startPlayback)
                {
                    MelonLogger.Msg("Starting playback of downloaded video");

                    var playbackCoroutine = PlayCachedYoutubeVideo(result.FilePath, isPartOfPlaylist);
                    CustomTV.customTVState.IsDownloadingYoutube = false;
                    yield return playbackCoroutine;
                    yield break;
                }
                else
                {
                    MelonLogger.Msg($"Added downloaded video to queue ({CustomTV.customTVState.PlaylistVideoQueue.Count} videos in queue)");
                }
            }

            CustomTV.customTVState.IsDownloadingYoutube = false;
        }

        public static IEnumerator PlayCachedYoutubeVideo(string filePath, bool isPartOfPlaylist = false)
        {
            string normalizedPath = Path.GetFullPath(filePath).Replace('\\', '/');
            MelonLogger.Msg($"Playing YouTube video from normalized path: {normalizedPath}");

            if (!File.Exists(normalizedPath))
            {
                MelonLogger.Error($"YouTube video file not found at: {normalizedPath}");
                CustomTV.customTVState.IsDownloadingYoutube = false;

                if (isPartOfPlaylist && CustomTV.customTVState.PlaylistVideoQueue.Count > 0)
                {
                    MelonLogger.Msg("Skipping missing video and proceeding to next one in playlist");
                    MelonCoroutines.Start(Playlist.StartPlaylistFromLocalFiles());
                }

                yield break;
            }

            bool canAccessFile = false;

            try
            {
                using (FileStream fs = File.Open(normalizedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    canAccessFile = true;
                }

                FileInfo fileInfo = new(normalizedPath);
                MelonLogger.Msg($"Video file size: {fileInfo.Length / 1024.0 / 1024.0:F2} MB");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error accessing YouTube video file: {ex.Message}");

                if (ex is IOException)
                {
                    MelonLogger.Error("File may be locked by another process or have incorrect permissions");
                }
                else if (ex is UnauthorizedAccessException)
                {
                    MelonLogger.Error("No permission to access the file. Try running the game as administrator");
                }

                CustomTV.customTVState.IsDownloadingYoutube = false;

                if (isPartOfPlaylist && CustomTV.customTVState.PlaylistVideoQueue.Count > 0)
                {
                    MelonLogger.Msg("Skipping problematic video and proceeding to next one in playlist");
                    MelonCoroutines.Start(Playlist.StartPlaylistFromLocalFiles());
                }

                yield break;
            }

            if (!canAccessFile)
            {
                MelonLogger.Error("Could not access the video file");
                CustomTV.customTVState.IsDownloadingYoutube = false;

                if (isPartOfPlaylist && CustomTV.customTVState.PlaylistVideoQueue.Count > 0)
                {
                    MelonLogger.Msg("Skipping inaccessible video and proceeding to next one in playlist");
                    MelonCoroutines.Start(Playlist.StartPlaylistFromLocalFiles());
                }

                yield break;
            }

            if (CustomTV.customTVState.TVInterfaces.Count == 0 || CustomTV.customTVState.TVInterfaces[0] == null)
            {
                MelonLogger.Msg("No TV interfaces found, looking for them now...");
                CustomTV.customTVState.TVInterfaces.Clear();
                CustomTV.customTVState.TVInterfaces = [.. UnityEngine.Object.FindObjectsOfType<Transform>().Where(t => t.name == "TVInterface")];

                if (CustomTV.customTVState.TVInterfaces.Count == 0)
                {
                    MelonLogger.Error("No TV interfaces found, cannot play video");
                    CustomTV.customTVState.IsDownloadingYoutube = false;

                    if (isPartOfPlaylist && CustomTV.customTVState.PlaylistVideoQueue.Count > 0)
                    {
                        MelonLogger.Msg("Will try again with next video in 5 seconds...");
                        MelonCoroutines.Start(Playlist.DelayNextPlaylistVideo());
                    }

                    yield break;
                }
            }

            if (CustomTV.customTVState.SharedRenderTexture == null)
            {
                MelonLogger.Msg("Creating new render texture");
                CustomTV.customTVState.SharedRenderTexture = new RenderTexture(1920, 1080, 0);
            }

            CustomTV.customTVState.IsYoutubeMode = true;
            CustomTV.customTVState.VideoFilePath = normalizedPath;
            CustomTV.customTVState.SavedPlaybackTime = 0;

            int originalIndex = CustomTV.customTVState.CurrentVideoIndex;

            if (!CustomTV.customTVState.VideoFiles.Contains(normalizedPath))
            {
                CustomTV.customTVState.VideoFiles.RemoveAll(x => x.StartsWith(Config.YoutubeTempFolder));

                CustomTV.customTVState.VideoFiles.Add(normalizedPath);
                CustomTV.customTVState.CurrentVideoIndex = CustomTV.customTVState.VideoFiles.Count - 1;

                MelonLogger.Msg("Added YouTube video to videoFiles list, starting video player setup");
                MelonLogger.Msg($"Current video index: {CustomTV.customTVState.CurrentVideoIndex}, total videos: {CustomTV.customTVState.VideoFiles.Count}");

                if (CustomTV.customTVState.SettingUp)
                {
                    MelonLogger.Msg("Video player setup already in progress, waiting...");
                    float startWaitTime = Time.time;
                    while (CustomTV.customTVState.SettingUp && Time.time - startWaitTime < 10f)
                    {
                        yield return null;
                    }

                    if (CustomTV.customTVState.SettingUp)
                    {
                        MelonLogger.Error("Video player setup is taking too long, forcing it to continue");
                        CustomTV.customTVState.SettingUp = false;
                    }
                }

                yield return Video.SetupVideoPlayer(false, true, normalizedPath);

                if (CustomTV.customTVState.PassiveVideoPlayers.Count == 0 || CustomTV.customTVState.PassiveVideoPlayers[0] == null)
                {
                    MelonLogger.Error("Failed to create video player for YouTube video");
                    MelonLogger.Msg($"Passive video players count: {CustomTV.customTVState.PassiveVideoPlayers.Count}");
                    if (CustomTV.customTVState.PassiveVideoPlayers.Count > 0)
                    {
                        MelonLogger.Msg($"First player is null: {CustomTV.customTVState.PassiveVideoPlayers[0] == null}");
                    }

                    if (CustomTV.customTVState.CurrentVideoIndex < CustomTV.customTVState.VideoFiles.Count)
                    {
                        CustomTV.customTVState.VideoFiles.RemoveAt(CustomTV.customTVState.CurrentVideoIndex);
                    }
                    CustomTV.customTVState.CurrentVideoIndex = originalIndex;

                    if (isPartOfPlaylist && CustomTV.customTVState.PlaylistVideoQueue.Count > 0)
                    {
                        MelonLogger.Msg("Player setup failed, proceeding to next video in playlist");
                        MelonCoroutines.Start(Playlist.StartPlaylistFromLocalFiles());
                    }
                }
                else
                {
                    try
                    {
                        var masterPlayer = CustomTV.customTVState.PassiveVideoPlayers[0];

                        masterPlayer.isLooping = false;
                        MelonLogger.Msg("Disabled looping on video player");

                        VideoHandlers.RemoveVideoEndHandler(masterPlayer);

                        VideoHandlers.AddVideoEndHandler(masterPlayer);
                        MelonLogger.Msg("Added video end event handler");

                        if (!masterPlayer.isPlaying)
                        {
                            masterPlayer.Play();
                            MelonLogger.Msg("Started video playback");
                        }

                        MelonLogger.Msg("YouTube video loaded successfully");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"Error configuring video player: {ex.Message}");
                    }
                }
            }
            else
            {
                CustomTV.customTVState.CurrentVideoIndex = CustomTV.customTVState.VideoFiles.IndexOf(normalizedPath);
                MelonLogger.Msg($"Video already in list at index {CustomTV.customTVState.CurrentVideoIndex}, setting up player");

                if (CustomTV.customTVState.SettingUp)
                {
                    MelonLogger.Msg("Video player setup already in progress, waiting...");
                    float startWaitTime = Time.time;
                    while (CustomTV.customTVState.SettingUp && Time.time - startWaitTime < 10f)
                    {
                        yield return null;
                    }

                    if (CustomTV.customTVState.SettingUp)
                    {
                        MelonLogger.Error("Video player setup is taking too long, forcing it to continue");
                        CustomTV.customTVState.SettingUp = false;
                    }
                }

                yield return Video.SetupVideoPlayer(false, true, normalizedPath);

                if (CustomTV.customTVState.PassiveVideoPlayers.Count == 0 || CustomTV.customTVState.PassiveVideoPlayers[0] == null)
                {
                    MelonLogger.Error("Failed to create video player for existing YouTube video");
                    CustomTV.customTVState.CurrentVideoIndex = originalIndex;

                    if (isPartOfPlaylist && CustomTV.customTVState.PlaylistVideoQueue.Count > 0)
                    {
                        MelonLogger.Msg("Player setup failed, proceeding to next video in playlist");
                        MelonCoroutines.Start(Playlist.StartPlaylistFromLocalFiles());
                    }
                }
                else
                {
                    try
                    {
                        var masterPlayer = CustomTV.customTVState.PassiveVideoPlayers[0];

                        masterPlayer.isLooping = false;
                        MelonLogger.Msg("Disabled looping on video player");

                        VideoHandlers.RemoveVideoEndHandler(masterPlayer);

                        VideoHandlers.AddVideoEndHandler(masterPlayer);
                        MelonLogger.Msg("Added video end event handler");

                        if (!masterPlayer.isPlaying)
                        {
                            masterPlayer.Play();
                            MelonLogger.Msg("Started video playback");
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"Error configuring video player: {ex.Message}");
                    }
                }
            }

            CustomTV.customTVState.IsDownloadingYoutube = false;
        }

        public static IEnumerator CleanupYoutubeTempFolder(bool forceDeleteAll = false)
        {
            if (!Directory.Exists(Config.YoutubeTempFolder))
            {
                yield break;
            }

            if (forceDeleteAll)
            {
                foreach (string file in Directory.GetFiles(Config.YoutubeTempFolder, "*.mp4"))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"Failed to delete YouTube cache file: {ex.Message}");
                    }
                }

                CustomTV.customTVState.YoutubeCache.Clear();
                CustomTV.customTVState.YtCacheQueue.Clear();
                yield break;
            }

            string[] files = [.. Directory.GetFiles(Config.YoutubeTempFolder, "*.mp4").OrderByDescending(f => new FileInfo(f).CreationTime)];

            if (files.Length > Config.MaxCachedYoutubeVideos)
            {
                for (int i = Config.MaxCachedYoutubeVideos; i < files.Length; i++)
                {
                    try
                    {
                        string url = CustomTV.customTVState.YoutubeCache.FirstOrDefault(x => x.Value == files[i]).Key;
                        if (!string.IsNullOrEmpty(url))
                        {
                            CustomTV.customTVState.YoutubeCache.Remove(url);
                            CustomTV.customTVState.YtCacheQueue.Remove(url);
                        }

                        File.Delete(files[i]);
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"Failed to delete old YouTube cache file: {ex.Message}");
                    }
                }
            }
        }

        public static bool IsYoutubeUrl(string url)
        {
            return !string.IsNullOrEmpty(url) &&
                  (url.Contains("youtube.com/watch") ||
                   url.Contains("youtu.be/") ||
                   url.Contains("youtube.com/shorts/") ||
                   url.Contains("youtube.com/embed/") ||
                   url.Contains("youtube.com/playlist?list="));
        }

        public static string GetClipboardContent()
        {
            try
            {
                string clipboardText = GUIUtility.systemCopyBuffer;
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    return clipboardText;
                }

                TextEditor te = new();
                te.Paste();
                return te.text;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to access clipboard: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
