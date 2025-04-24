using MelonLoader;
using System.Collections;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using static CustomTV.CustomTV;

namespace CustomTV.Utils.YoutubeUtils
{
    class Playlist
    {
        public static IEnumerator DelayNextPlaylistVideo()
        {
            yield return new WaitForSeconds(5f);

            if (CustomTV.customTVState.PlaylistVideoQueue.Count > 0)
            {
                MelonLogger.Msg("Trying next video in playlist after delay");
                MelonCoroutines.Start(StartPlaylistFromLocalFiles());
            }
        }

        public static IEnumerator ProcessYoutubePlaylist(string playlistUrl, bool clearExistingQueue = true)
        {
            if (CustomTV.customTVState.IsProcessingPlaylist)
            {
                MelonLogger.Warning("Already processing a YouTube playlist. Please wait...");
                yield break;
            }

            CustomTV.customTVState.IsProcessingPlaylist = true;
            if (clearExistingQueue)
            {
                CustomTV.customTVState.PlaylistVideoQueue.Clear();
                MelonLogger.Msg("Cleared existing queue");
            }
            else
            {
                MelonLogger.Msg($"Adding to existing queue ({CustomTV.customTVState.PlaylistVideoQueue.Count} videos already in queue)");
            }

            MelonLogger.Msg("Extracting video URLs from playlist...");

            string ytDlpExePath = Path.Combine(Config.YtDlpFolderPath, "yt-dlp.exe");
            if (!File.Exists(ytDlpExePath))
            {
                MelonLogger.Error("yt-dlp.exe not found. Cannot process YouTube playlist.");
                MelonLogger.Msg($"Please download yt-dlp.exe manually from https://github.com/yt-dlp/yt-dlp/releases and place it in: {Config.YtDlpFolderPath}");
                CustomTV.customTVState.IsProcessingPlaylist = false;
                yield break;
            }

            yield return UI.CreatePlaylistProgressUI("Extracting videos from playlist...");

            List<string> videoUrls = [];
            bool extractionSuccess = false;

            Task extractionTask = Task.Run(() =>
            {
                try
                {
                    using Process process = new();
                    process.StartInfo.FileName = ytDlpExePath;
                    process.StartInfo.Arguments = $"--flat-playlist --get-id --get-title \"{playlistUrl}\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;

                    StringBuilder outputBuilder = new();

                    process.OutputDataReceived += (sender, args) =>
                    {
                        if (args.Data != null)
                        {
                            lock (outputBuilder)
                            {
                                outputBuilder.AppendLine(args.Data);
                            }
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();

                    bool exited = process.WaitForExit(120000);

                    if (!exited)
                    {
                        process.Kill();
                        MelonLogger.Error("Playlist extraction timeout after 2 minutes");
                    }
                    else if (process.ExitCode != 0)
                    {
                        MelonLogger.Error($"Failed to extract playlist with exit code {process.ExitCode}");
                    }
                    else
                    {
                        string[] lines = outputBuilder.ToString().Split('\n');
                        for (int i = 0; i < lines.Length - 1; i += 2)
                        {
                            string title = lines[i].Trim();
                            string id = i + 1 < lines.Length ? lines[i + 1].Trim() : "";

                            if (!string.IsNullOrEmpty(id))
                            {
                                string videoUrl = $"https://www.youtube.com/watch?v={id}";
                                lock (videoUrls)
                                {
                                    videoUrls.Add(videoUrl);
                                    MelonLogger.Msg($"Found video: {title} ({videoUrl})");
                                }
                            }
                        }

                        extractionSuccess = true;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error extracting playlist: {ex.Message}");
                }
            });

            int dotCount = 0;
            while (!extractionTask.IsCompleted)
            {
                dotCount = (dotCount + 1) % 4;
                string dots = new('.', dotCount + 1);
                UI.UpdatePlaylistProgressUI($"Extracting videos{dots}");
                yield return new WaitForSeconds(0.5f);
            }

            if (!extractionSuccess || videoUrls.Count == 0)
            {
                MelonLogger.Error("Failed to extract videos from playlist or playlist is empty.");
                UI.DestroyPlaylistProgressUI();
                CustomTV.customTVState.IsProcessingPlaylist = false;
                yield break;
            }

            MelonLogger.Msg($"Found {videoUrls.Count} videos in playlist. Starting download...");

            bool isVideoCurrentlyPlaying = false;
            if (CustomTV.customTVState.PassiveVideoPlayers.Count > 0 && CustomTV.customTVState.PassiveVideoPlayers[0] != null)
            {
                var master = CustomTV.customTVState.PassiveVideoPlayers[0];
                isVideoCurrentlyPlaying = master.isPlaying;
            }

            bool queueWasEmpty = CustomTV.customTVState.PlaylistVideoQueue.Count == 0;

            if (videoUrls.Count > 0)
            {
                string firstVideoUrl = videoUrls[0];
                bool shouldStartPlaying = queueWasEmpty && !isVideoCurrentlyPlaying;

                MelonLogger.Msg($"Processing first video from playlist ({(shouldStartPlaying ? "will start playback" : "adding to queue")})...");
                UI.UpdatePlaylistProgressUI("Processing first video...");

                string firstVideoPath = null;
                bool firstVideoDownloaded = false;

                if (CustomTV.customTVState.YoutubeCache.TryGetValue(firstVideoUrl, out string cachedPath) && File.Exists(cachedPath))
                {
                    MelonLogger.Msg($"Using cached video for first playlist item: {firstVideoUrl}");
                    firstVideoPath = cachedPath;
                    firstVideoDownloaded = true;
                }
                else
                {
                    Youtube.DownloadResult result = null;
                    yield return Youtube.DownloadYoutubeVideo(firstVideoUrl, 1, videoUrls.Count, (downloadResult) =>
                    {
                        result = downloadResult;
                    });

                    if (result != null && result.Success)
                    {
                        firstVideoPath = result.FilePath;
                        firstVideoDownloaded = true;
                        MelonLogger.Msg("First video downloaded successfully");
                    }
                    else
                    {
                        bool isAgeRestricted = result != null && result.IsAgeRestricted;
                        if (isAgeRestricted)
                        {
                            MelonLogger.Warning("First video is age-restricted and cannot be played. Trying next video...");
                        }
                        else
                        {
                            MelonLogger.Error("Failed to download first video. Trying next video...");
                        }

                        for (int i = 1; i < Math.Min(5, videoUrls.Count) && !firstVideoDownloaded; i++)
                        {
                            string nextVideoUrl = videoUrls[i];
                            if (CustomTV.customTVState.YoutubeCache.TryGetValue(nextVideoUrl, out cachedPath) && File.Exists(cachedPath))
                            {
                                MelonLogger.Msg($"Using cached video for alternate first playlist item: {nextVideoUrl}");
                                firstVideoPath = cachedPath;
                                firstVideoDownloaded = true;
                                break;
                            }

                            MelonLogger.Msg($"Trying to download alternate video {i + 1}...");

                            Youtube.DownloadResult alternateResult = null;
                            yield return Youtube.DownloadYoutubeVideo(nextVideoUrl, i + 1, videoUrls.Count, (downloadResult) =>
                            {
                                alternateResult = downloadResult;
                            });

                            if (alternateResult != null && alternateResult.Success)
                            {
                                firstVideoPath = alternateResult.FilePath;
                                firstVideoDownloaded = true;
                                MelonLogger.Msg($"Alternate video {i + 1} downloaded successfully");
                                break;
                            }
                        }
                    }
                }

                if (firstVideoDownloaded)
                {
                    lock (CustomTV.customTVState.PlaylistVideoQueue)
                    {
                        CustomTV.customTVState.PlaylistVideoQueue.Enqueue(firstVideoPath);
                        MelonLogger.Msg($"Added first video to queue ({CustomTV.customTVState.PlaylistVideoQueue.Count} videos in queue)");
                    }

                    List<string> remainingVideos = [.. videoUrls];
                    remainingVideos.RemoveAt(0);

                    if (remainingVideos.Count > 0)
                    {
                        MelonCoroutines.Start(DownloadRemainingPlaylistVideos(remainingVideos));
                    }

                    if (shouldStartPlaying)
                    {
                        MelonLogger.Msg("Starting playback of first video in playlist");
                        MelonCoroutines.Start(StartPlaylistFromLocalFiles());
                    }
                    else
                    {
                        MelonLogger.Msg("Added playlist to queue without interrupting current playback");
                        MelonLogger.Msg($"{CustomTV.customTVState.PlaylistVideoQueue.Count} videos in queue");
                    }
                }
                else
                {
                    MelonLogger.Error("Failed to download any videos from the playlist. Aborting playback.");
                    UI.DestroyPlaylistProgressUI();
                }
            }

            CustomTV.customTVState.IsProcessingPlaylist = false;
        }

        private static IEnumerator DownloadRemainingPlaylistVideos(List<string> videoUrls)
        {
            MelonLogger.Msg($"Starting background download of {videoUrls.Count} remaining videos...");
            UI.UpdatePlaylistProgressUI($"Downloading {videoUrls.Count} remaining videos in background...");

            List<string> downloadedPaths = [];
            List<string> failedVideos = [];
            List<string> ageRestrictedVideos = [];

            int startIndex = 2;

            for (int i = 0; i < videoUrls.Count; i++)
            {
                string videoUrl = videoUrls[i];
                int currentIndex = startIndex + i;

                if (customTVState.YoutubeCache.TryGetValue(videoUrl, out string cachedPath) && File.Exists(cachedPath))
                {
                    MelonLogger.Msg($"Using cached video for {videoUrl}");
                    downloadedPaths.Add(cachedPath);

                    lock (customTVState.PlaylistVideoQueue)
                    {
                        customTVState.PlaylistVideoQueue.Enqueue(cachedPath);
                        MelonLogger.Msg($"Added cached video to queue: {cachedPath}");
                    }

                    UI.UpdatePlaylistProgressUI($"Downloading: {i + 1}/{videoUrls.Count} remaining videos");
                    continue;
                }

                Youtube.DownloadResult result = null;
                yield return Youtube.DownloadYoutubeVideo(videoUrl, currentIndex, videoUrls.Count + 1, (downloadResult) =>
                {
                    result = downloadResult;
                });

                if (result == null || !result.Success)
                {
                    if (result != null && result.IsAgeRestricted)
                    {
                        ageRestrictedVideos.Add(videoUrl);
                        MelonLogger.Warning($"Skipping age-restricted video {currentIndex}/{videoUrls.Count + 1}: {videoUrl}");
                    }
                    else
                    {
                        failedVideos.Add(videoUrl);
                        MelonLogger.Error($"Failed to download video {currentIndex}/{videoUrls.Count + 1}: {videoUrl}");
                    }
                    continue;
                }

                downloadedPaths.Add(result.FilePath);

                lock (customTVState.PlaylistVideoQueue)
                {
                    customTVState.PlaylistVideoQueue.Enqueue(result.FilePath);
                    MelonLogger.Msg($"Added video to queue: {result.FilePath}");
                }

                UI.UpdatePlaylistProgressUI($"Downloaded: {i + 1}/{videoUrls.Count} remaining videos");

                yield return new WaitForSeconds(0.5f);
            }

            int successfulVideos = downloadedPaths.Count;
            int failedCount = failedVideos.Count;
            int ageRestrictedCount = ageRestrictedVideos.Count;

            MelonLogger.Msg($"Background download complete. {successfulVideos}/{videoUrls.Count} videos downloaded successfully.");

            if (ageRestrictedCount > 0)
            {
                MelonLogger.Warning($"{ageRestrictedCount} videos were age-restricted and will be skipped.");
            }

            if (failedCount > 0)
            {
                MelonLogger.Error($"{failedCount} videos failed to download due to other errors and will be skipped.");
            }

            UI.UpdatePlaylistProgressUI($"All downloads complete. {customTVState.PlaylistVideoQueue.Count} videos in queue.");
        }

        public static IEnumerator StartPlaylistFromLocalFiles()
        {
            if (customTVState.PlaylistVideoQueue.Count == 0)
            {
                MelonLogger.Msg("No videos in playlist queue.");
                yield break;
            }

            string filePath;
            lock (customTVState.PlaylistVideoQueue)
            {
                filePath = customTVState.PlaylistVideoQueue.Peek();
            }

            MelonLogger.Msg($"Starting playlist video from path: {filePath}");

            if (customTVState.PassiveVideoPlayers.Count > 0 && customTVState.PassiveVideoPlayers[0] != null)
            {
                var master = customTVState.PassiveVideoPlayers[0];
                if (master.isPlaying)
                {
                    master.Stop();
                    VideoHandlers.RemoveVideoEndHandler(master);
                    MelonLogger.Msg("Stopped currently playing video to start the next one");
                }
            }

            yield return Youtube.PlayCachedYoutubeVideo(filePath, true);

            lock (customTVState.PlaylistVideoQueue)
            {
                if (customTVState.PlaylistVideoQueue.Count > 0 && customTVState.PlaylistVideoQueue.Peek() == filePath)
                {
                    customTVState.PlaylistVideoQueue.Dequeue();
                }
            }

            MelonLogger.Msg($"{customTVState.PlaylistVideoQueue.Count} downloaded videos remaining in queue");
        }
    }
}
