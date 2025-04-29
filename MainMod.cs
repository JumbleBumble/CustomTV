using MelonLoader;
using MelonLoader.Utils;
using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using CustomTV.Utils;
using CustomTV;
using CustomTV.Utils.YoutubeUtils;

[assembly: MelonInfo(typeof(CustomTV.CustomTV), BuildInfo.Name, BuildInfo.Version, BuildInfo.Author, BuildInfo.DownloadLink)]
[assembly: MelonColor()]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace CustomTV
{
    public static class BuildInfo
    {
        public const string Name = "CustomTV";
        public const string Description = "Lets you play your own MP4 videos on the TV and Signs.";
        public const string Author = "Jumble & Bars";
        public const string Company = null;
        public const string Version = "1.6.2";
        public const string DownloadLink = "www.nexusmods.com/schedule1/mods/603";
    }

    public static class Config
    {
        private static MelonPreferences_Category configCategory;
        private static MelonPreferences_Entry<KeyCode> pauseKeyEntry;
        private static MelonPreferences_Entry<KeyCode> resumeKeyEntry;
        private static MelonPreferences_Entry<KeyCode> nextVideoKeyEntry;
        private static MelonPreferences_Entry<KeyCode> previousVideoKeyEntry;
        private static MelonPreferences_Entry<KeyCode> seekForwardKeyEntry;
        private static MelonPreferences_Entry<KeyCode> seekBackwardKeyEntry;
        private static MelonPreferences_Entry<int> audioVolumePercentEntry;
        private static MelonPreferences_Entry<float> seekAmountEntry;
        private static MelonPreferences_Entry<bool> shuffleEntry;
        private static MelonPreferences_Entry<KeyCode> youtubeURLKeyEntry;
        private static MelonPreferences_Entry<int> maxCachedYoutubeVideosEntry;
        private static MelonPreferences_Entry<bool> deleteYoutubeVideosOnExitEntry;
        private static MelonPreferences_Entry<bool> useFirefoxCookiesEntry;
        private static MelonPreferences_Entry<string> signSizeEntry;

        public static KeyCode PauseKey => pauseKeyEntry?.Value ?? KeyCode.Minus;
        public static KeyCode ResumeKey => resumeKeyEntry?.Value ?? KeyCode.Equals;
        public static KeyCode NextVideoKey => nextVideoKeyEntry?.Value ?? KeyCode.RightBracket;
        public static KeyCode PreviousVideoKey => previousVideoKeyEntry?.Value ?? KeyCode.LeftBracket;
        public static KeyCode Seekforward => seekForwardKeyEntry?.Value ?? KeyCode.RightArrow;
        public static KeyCode Seekbackward => seekBackwardKeyEntry?.Value ?? KeyCode.LeftArrow;
        public static float AudioVolume => audioVolumePercentEntry != null ? audioVolumePercentEntry.Value / 100f : 1.0f;
        public static float SeekAmount => seekAmountEntry?.Value ?? 10.0f;
        public static bool Shuffle => shuffleEntry?.Value ?? true;
        public static KeyCode YoutubeURLKey => youtubeURLKeyEntry?.Value ?? KeyCode.V;
        public static int MaxCachedYoutubeVideos => maxCachedYoutubeVideosEntry?.Value ?? 25;
        public static bool DeleteYoutubeVideosOnExit => deleteYoutubeVideosOnExitEntry?.Value ?? true;
        public static bool UseFirefoxCookies => useFirefoxCookiesEntry?.Value ?? false;
        public static Vector3 SignSize => ParseVector3(signSizeEntry.Value);

        private static readonly string modsFolderPath = MelonEnvironment.ModsDirectory;
        private static readonly string tvFolderPath = Path.Combine(modsFolderPath, "TV");
        public static string YoutubeTempFolder { get; private set; }
        public static string YtDlpFolderPath { get; private set; }

        public static void Load()
        {
            if (!Directory.Exists(tvFolderPath))
            {
                Directory.CreateDirectory(tvFolderPath);
            }

            YoutubeTempFolder = Path.Combine(tvFolderPath, "yt-temp");
            YtDlpFolderPath = Path.Combine(tvFolderPath, "yt-dlp");

            if (!Directory.Exists(YoutubeTempFolder))
            {
                Directory.CreateDirectory(YoutubeTempFolder);
            }

            if (!Directory.Exists(YtDlpFolderPath))
            {
                Directory.CreateDirectory(YtDlpFolderPath);
            }

            configCategory = MelonPreferences.CreateCategory("CustomTV");

            pauseKeyEntry = configCategory.CreateEntry("PauseKey", KeyCode.Minus, "Pause");
            resumeKeyEntry = configCategory.CreateEntry("ResumeKey", KeyCode.Equals, "Resume");
            nextVideoKeyEntry = configCategory.CreateEntry("NextVideoKey", KeyCode.RightBracket, "Skip");
            previousVideoKeyEntry = configCategory.CreateEntry("PreviousVideoKey", KeyCode.LeftBracket, "Previous");
            seekForwardKeyEntry = configCategory.CreateEntry("SeekForwardKey", KeyCode.RightArrow, "Seek Forward");
            seekBackwardKeyEntry = configCategory.CreateEntry("SeekBackwardKey", KeyCode.LeftArrow, "Seek Backward");
            audioVolumePercentEntry = configCategory.CreateEntry("VolumePercent", 100, "Volume (0-100)");
            seekAmountEntry = configCategory.CreateEntry("SeekAmount", 10.0f, "Seek Amount (seconds)");
            shuffleEntry = configCategory.CreateEntry("Shuffle", true, "Shuffle videos");
            youtubeURLKeyEntry = configCategory.CreateEntry("YoutubeURLKey", KeyCode.V, "YouTube URL key");
            maxCachedYoutubeVideosEntry = configCategory.CreateEntry("MaxCachedYoutubeVideos", 25, "Max cached YouTube videos");
            deleteYoutubeVideosOnExitEntry = configCategory.CreateEntry("DeleteYoutubeVideosOnExit", true, "Delete YouTube videos on exit");
            useFirefoxCookiesEntry = configCategory.CreateEntry("UseFirefoxCookies", false, "Use Firefox cookies for age-restricted videos");
            signSizeEntry = configCategory.CreateEntry("signSize", "(3, 2.2, 0)", "Vector3 Sign Size");

            string ytDlpExePath = Path.Combine(YtDlpFolderPath, "yt-dlp.exe");
            if (!File.Exists(ytDlpExePath))
            {
                MelonLogger.Warning("yt-dlp.exe not found. YouTube functionality won't work.");
                MelonLogger.Msg($"Please download yt-dlp.exe manually from https://github.com/yt-dlp/yt-dlp/releases and place it in: {YtDlpFolderPath}");
            }
        }

        private static Vector3 ParseVector3(string vectorString)
        {
            try
            {
                vectorString = vectorString.Trim('(', ')');
                string[] components = vectorString.Split(',');
                if (components.Length == 3 &&
                    float.TryParse(components[0], out float x) &&
                    float.TryParse(components[1], out float y) &&
                    float.TryParse(components[2], out float z))
                {
                    return new Vector3(x, y, z);
                }
            }
            catch
            {
                MelonLogger.Warning($"Failed to parse Vector3 from string: {vectorString}. Using default value.");
            }
            return new Vector3(3f, 2.2f, 0f);
        }
    }

    public class CustomTV : MelonMod
    {
        public static CustomTVState customTVState = new CustomTVState();

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            try
            {
                if (buildIndex == 1 && sceneName == "Main")
                {
                    MelonCoroutines.Start(Video.DelayedSetupVideoPlayer(true));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in OnSceneWasLoaded: {ex.Message}");
            }
        }

        public override void OnInitializeMelon()
        {
            Config.Load();

            try
            {
                string tvFolder = Path.Combine(MelonEnvironment.ModsDirectory, "TV");
                customTVState.VideoFiles = [.. Directory.GetFiles(tvFolder, "*.mp4", SearchOption.TopDirectoryOnly)];
                if (Config.Shuffle)
                {
                    customTVState.VideoFiles.Shuffle(customTVState.Rng);
                }
                else
                {
                    customTVState.VideoFiles = [.. customTVState.VideoFiles.OrderBy(f => f, new SmartEpisodeComparer())];
                }

#if MELONLOADER_IL2CPP
                VideoHandlers.videoEndDelegate = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<VideoPlayer.EventHandler>(
                    new Action<VideoPlayer>(VideoHandlers.VideoEndEventHandler));
                VideoHandlers.errorEventHandler = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<VideoPlayer.ErrorEventHandler>(
                    new Action<VideoPlayer, string>(VideoHandlers.OnErrorReceived));
#elif MELONLOADER_MONO
                VideoHandlers.videoEndDelegate = new VideoPlayer.EventHandler(VideoHandlers.VideoEndEventHandler);
                VideoHandlers.errorEventHandler = new VideoPlayer.ErrorEventHandler(VideoHandlers.OnErrorReceived);
#else
                videoEndDelegate = new VideoPlayer.EventHandler(VideoEndEventHandler);
                errorEventHandler = new VideoPlayer.ErrorEventHandler(OnErrorReceived);
#endif

                MelonCoroutines.Start(Youtube.CleanupYoutubeTempFolder());
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in OnInitializeMelon: {ex.Message}");
            }
        }

        public override void OnApplicationQuit()
        {
            if (Config.DeleteYoutubeVideosOnExit)
            {
                MelonCoroutines.Start(Youtube.CleanupYoutubeTempFolder(true));
            }
        }

        public override void OnUpdate()
        {
            if (!customTVState.SettingUpdate) MelonCoroutines.Start(Video.HandleUpdate());
            if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                if (Input.GetKeyDown(Config.PauseKey))
                {
                    if (customTVState.PassiveVideoPlayers.Count > 0 && !customTVState.WasPaused)
                    {
                        var master = customTVState.PassiveVideoPlayers[0];
                        customTVState.SavedPlaybackTime = master.time;
                        master.Pause();
                        customTVState.WasPaused = true;
                        for (int i = 0; i < customTVState.PassiveVideoPlayers.Count; i++)
                        {
                            if (i != 0)
                            {
                                customTVState.PassiveVideoPlayers[i].Pause();
                                customTVState.PassiveVideoPlayers[i].time = customTVState.SavedPlaybackTime;
                            }
                        }
                    }
                }
                else if (Input.GetKeyDown(Config.ResumeKey))
                {
                    if (customTVState.PassiveVideoPlayers.Count > 0 && customTVState.WasPaused && Time.timeScale > 0.1f)
                    {
                        var master = customTVState.PassiveVideoPlayers[0];
                        master.time = customTVState.SavedPlaybackTime;
                        master.Play();
                        customTVState.WasPaused = false;
                        customTVState.WasPausedByTimescale = false;
                        for (int i = 0; i < customTVState.PassiveVideoPlayers.Count; i++)
                        {
                            if (i != 0)
                            {
                                customTVState.PassiveVideoPlayers[i].Play();
                                customTVState.PassiveVideoPlayers[i].time = customTVState.SavedPlaybackTime;
                            }
                        }
                    }
                }
                if (Input.GetKeyDown(Config.NextVideoKey))
                {
                    if (customTVState.PlaylistVideoQueue.Count > 0)
                    {
                        MelonLogger.Msg("Skipping to next video in playlist queue.");
                        if (customTVState.PassiveVideoPlayers.Count > 0 && customTVState.PassiveVideoPlayers[0] != null)
                        {
                            var master = customTVState.PassiveVideoPlayers[0];
                            master.Stop();
                            VideoHandlers.RemoveVideoEndHandler(master);
                        }

                        MelonCoroutines.Start(Playlist.StartPlaylistFromLocalFiles());
                    }
                    else
                    {
                        customTVState.SavedPlaybackTime = 0;
                        MelonCoroutines.Start(Video.SetupVideoPlayer());
                    }
                }
                if (Input.GetKeyDown(Config.PreviousVideoKey))
                {
                    customTVState.SavedPlaybackTime = 0;
                    MelonCoroutines.Start(Video.SetupVideoPlayer(false, false));
                }
                if (Input.GetKeyDown(Config.Seekforward))
                {
                    customTVState.SavedPlaybackTime += Config.SeekAmount;
                    MelonCoroutines.Start(HandleSeek(customTVState.SavedPlaybackTime));
                }
                if (Input.GetKeyDown(Config.Seekbackward))
                {
                    customTVState.SavedPlaybackTime -= Config.SeekAmount;
                    MelonCoroutines.Start(HandleSeek(customTVState.SavedPlaybackTime));
                }

                if (Input.GetKeyDown(Config.YoutubeURLKey))
                {
                    try
                    {
                        string clipboardText = Youtube.GetClipboardContent();
                        MelonLogger.Msg($"Clipboard content: {(string.IsNullOrEmpty(clipboardText) ? "Empty" : clipboardText)}");

                        if (!customTVState.IsDownloadingYoutube && !customTVState.IsProcessingPlaylist)
                        {
                            if (!string.IsNullOrEmpty(clipboardText) && Youtube.IsYoutubeUrl(clipboardText))
                            {
                                if (clipboardText.Contains("youtube.com/playlist?list="))
                                {
                                    MelonLogger.Msg($"Processing YouTube playlist: {clipboardText}");
                                    bool isVideoPlaying = customTVState.PassiveVideoPlayers.Count > 0 && customTVState.PassiveVideoPlayers[0] != null && customTVState.PassiveVideoPlayers[0].isPlaying;
                                    MelonCoroutines.Start(Playlist.ProcessYoutubePlaylist(clipboardText, !isVideoPlaying));
                                }
                                else
                                {
                                    MelonLogger.Msg($"Adding YouTube URL to queue: {clipboardText}");
                                    bool isVideoPlaying = customTVState.PassiveVideoPlayers.Count > 0 && customTVState.PassiveVideoPlayers[0] != null && customTVState.PassiveVideoPlayers[0].isPlaying;
                                    MelonCoroutines.Start(Youtube.PlayYoutubeVideo(clipboardText, false, !isVideoPlaying));
                                }
                            }
                            else
                            {
                                MelonLogger.Warning($"Clipboard content is not a valid YouTube URL: '{clipboardText}'");
                            }
                        }
                        else if (customTVState.IsProcessingPlaylist)
                        {
                            MelonLogger.Warning("Already processing a YouTube playlist. Please wait...");
                        }
                        else
                        {
                            MelonLogger.Warning("Already downloading a YouTube video. Please wait...");
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"Error accessing clipboard: {ex.Message}");
                    }
                }
            }
        }

        private static IEnumerator HandleSeek(double newTime)
        {
            if (customTVState.PassiveVideoPlayers[0] == null)
                yield break;

            for (int i = 0; i < customTVState.PassiveVideoPlayers.Count; i++)
            {
                customTVState.PassiveVideoPlayers[i].time = newTime;
            }
        }
    }
}


