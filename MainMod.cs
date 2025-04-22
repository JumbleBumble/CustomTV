using Il2CppScheduleOne.EntityFramework;
using MelonLoader;
using MelonLoader.Utils;
using System.Collections;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[assembly: MelonInfo(typeof(CustomTV.CustomTV), CustomTV.BuildInfo.Name, CustomTV.BuildInfo.Version, CustomTV.BuildInfo.Author, CustomTV.BuildInfo.DownloadLink)]
[assembly: MelonColor()]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace CustomTV
{
    public static class BuildInfo
    {
        public const string Name = "CustomTV";
        public const string Description = "Lets you play your own MP4 videos on the TV.";
        public const string Author = "Jumble & Bars";
        public const string Company = null;
        public const string Version = "1.6.1";
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

        public static KeyCode PauseKey => pauseKeyEntry?.Value ?? KeyCode.Minus;
        public static KeyCode ResumeKey => resumeKeyEntry?.Value ?? KeyCode.Equals;
        public static KeyCode NextVideoKey => nextVideoKeyEntry?.Value ?? KeyCode.RightBracket;
        public static KeyCode PreviousVideoKey => previousVideoKeyEntry?.Value ?? KeyCode.LeftBracket;
        public static KeyCode Seekforward => seekForwardKeyEntry?.Value ?? KeyCode.RightArrow;
        public static KeyCode Seekbackward => seekBackwardKeyEntry?.Value ?? KeyCode.LeftArrow;
        public static float AudioVolume => audioVolumePercentEntry != null ? (audioVolumePercentEntry.Value / 100f) : 1.0f;
        public static float SeekAmount => seekAmountEntry?.Value ?? 10.0f;
        public static bool Shuffle => shuffleEntry?.Value ?? true;
        public static KeyCode YoutubeURLKey => youtubeURLKeyEntry?.Value ?? KeyCode.V;
        public static int MaxCachedYoutubeVideos => maxCachedYoutubeVideosEntry?.Value ?? 25;
        public static bool DeleteYoutubeVideosOnExit => deleteYoutubeVideosOnExitEntry?.Value ?? true;
        public static bool UseFirefoxCookies => useFirefoxCookiesEntry?.Value ?? false;

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

            string ytDlpExePath = Path.Combine(YtDlpFolderPath, "yt-dlp.exe");
            if (!File.Exists(ytDlpExePath))
            {
                MelonLogger.Warning("yt-dlp.exe not found. YouTube functionality won't work.");
                MelonLogger.Msg($"Please download yt-dlp.exe manually from https://github.com/yt-dlp/yt-dlp/releases and place it in: {YtDlpFolderPath}");
            }
        }
    }

    public class CustomTV : MelonMod
    {
        private static RenderTexture sharedRenderTexture;
        private static bool wasPausedByTimescale = false;
        private static bool wasPaused = false;
        private static bool settingUp = false;
        private static bool settingUpdate = false;
        private static double savedPlaybackTime = 0;
        private static string videoFilePath = Path.Combine(MelonEnvironment.ModsDirectory, "TV", "video.mp4");
        private static List<string> videoFiles = new();
        private static int currentVideoIndex = 0;
        private static readonly System.Random rng = new();
        private static readonly Action<VideoPlayer> videoEndHandler = OnVideoEnd;
        private static readonly List<VideoPlayer> passiveVideoPlayers = new();
        private static List<Transform> tvInterfaces = new();

        private static bool isYoutubeMode = false;
        private static bool isDownloadingYoutube = false;
        private static string currentYoutubeUrl = "";
        private static Dictionary<string, string> youtubeCache = new Dictionary<string, string>();
        private static Queue<string> ytCacheQueue = new Queue<string>();

        private static bool showDownloadProgress = false;
        private static volatile float downloadProgress = 0f;
        private static volatile string downloadStatus = "";
        private static GameObject downloadProgressUI;
        private static readonly object downloadLock = new object();

        private static Queue<string> playlistVideoQueue = new Queue<string>();
        private static bool isProcessingPlaylist = false;
        private static string currentPlaylistUrl = "";

        private static bool firefoxCookiesEnabled = true;
        private static bool warnedAboutCookies = false;

        private static void VideoEndEventHandler(VideoPlayer source)
        {
            OnVideoEnd(source);
        }

        private static VideoPlayer.EventHandler videoEndDelegate;
        private static VideoPlayer.ErrorEventHandler errorEventHandler;
        private static bool isIL2CPP = false;

        private static void AddVideoEndHandler(VideoPlayer vp)
        {
            if (vp == null) return;

            try
            {
#if MELONLOADER_IL2CPP
                vp.loopPointReached = (VideoPlayer.EventHandler)videoEndDelegate;
#else
                vp.loopPointReached += videoEndDelegate;
#endif
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to add video end handler: {ex.Message}");
            }
        }

        private static void RemoveVideoEndHandler(VideoPlayer vp)
        {
            if (vp == null) return;

            try
            {
#if MELONLOADER_IL2CPP
                vp.loopPointReached = null;
#else
                vp.loopPointReached -= videoEndDelegate;
#endif
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to remove video end handler: {ex.Message}");
            }
        }

        private static void AddErrorHandler(VideoPlayer vp)
        {
            if (vp == null) return;

            try
            {
#if MELONLOADER_IL2CPP
                vp.errorReceived = (VideoPlayer.ErrorEventHandler)errorEventHandler;
#else
                vp.errorReceived += errorEventHandler;
#endif
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to add error handler: {ex.Message}");
            }
        }

        private static void RemoveErrorHandler(VideoPlayer vp)
        {
            if (vp == null) return;

            try
            {
#if MELONLOADER_IL2CPP
                vp.errorReceived = null;
#else
                vp.errorReceived -= errorEventHandler;
#endif
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to remove error handler: {ex.Message}");
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            try
            {
                if (buildIndex == 1 && sceneName == "Main")
                {
                    MelonCoroutines.Start(DelayedSetupVideoPlayer(true));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in OnSceneWasLoaded: {ex.Message}");
            }
        }

        private static IEnumerator DelayedSetupVideoPlayer(bool isFirst = false)
        {
            yield return new WaitForSeconds(isFirst ? 7f : 1.5f);

            if (!settingUp)
            {
                MelonCoroutines.Start(SetupVideoPlayer(isFirst));
            }
        }

        public override void OnInitializeMelon()
        {
            Config.Load();

            try
            {
                string tvFolder = Path.Combine(MelonEnvironment.ModsDirectory, "TV");
                videoFiles = Directory.GetFiles(tvFolder, "*.mp4", SearchOption.TopDirectoryOnly).ToList();
                if (Config.Shuffle)
                {
                    videoFiles.Shuffle(rng);
                }
                else
                {
                    videoFiles = [.. videoFiles.OrderBy(f => f, new SmartEpisodeComparer())];
                }

#if MELONLOADER_IL2CPP
                isIL2CPP = true;
                videoEndDelegate = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<VideoPlayer.EventHandler>(
                    new Action<VideoPlayer>(VideoEndEventHandler));
                errorEventHandler = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<VideoPlayer.ErrorEventHandler>(
                    new Action<VideoPlayer, string>(OnErrorReceived));
#elif MELONLOADER_MONO
                videoEndDelegate = new VideoPlayer.EventHandler(VideoEndEventHandler);
                errorEventHandler = new VideoPlayer.ErrorEventHandler(OnErrorReceived);
#else
                videoEndDelegate = new VideoPlayer.EventHandler(VideoEndEventHandler);
                errorEventHandler = new VideoPlayer.ErrorEventHandler(OnErrorReceived);
#endif

                MelonCoroutines.Start(CleanupYoutubeTempFolder());
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in OnInitializeMelon: {ex.Message}");
            }
        }

        private static void OnErrorReceived(VideoPlayer source, string message)
        {
            MelonLogger.Error($"Video player error: {message}");

            try
            {
                if (source == null) return;

                string url = source.url ?? "null";
                bool isPrepared = source.isPrepared;
                ulong frameCount = source.frameCount;
                float frameRate = source.frameRate;
                bool isPlaying = source.isPlaying;

                if (!string.IsNullOrEmpty(url) && !url.StartsWith("file://") && File.Exists(url))
                {
                    source.url = "file://" + url.Replace('\\', '/');
                    if (!source.isPlaying)
                    {
                        source.Play();
                    }
                }
                else if (source == passiveVideoPlayers[0])
                {
                    MelonCoroutines.Start(SetupVideoPlayer());
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in error handler: {ex.Message}");
            }
        }

        public override void OnApplicationQuit()
        {
            if (Config.DeleteYoutubeVideosOnExit)
            {
                MelonCoroutines.Start(CleanupYoutubeTempFolder(true));
            }
        }

        public override void OnUpdate()
        {
            if (!settingUpdate) MelonCoroutines.Start(HandleUpdate());
            if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                if (Input.GetKeyDown(Config.PauseKey))
                {
                    if (passiveVideoPlayers.Count > 0 && !wasPaused)
                    {
                        var master = passiveVideoPlayers[0];
                        savedPlaybackTime = master.time;
                        master.Pause();
                        wasPaused = true;
                        for (int i = 0; i < passiveVideoPlayers.Count; i++)
                        {
                            if (i != 0)
                            {
                                passiveVideoPlayers[i].Pause();
                            }
                        }
                    }
                }
                else if (Input.GetKeyDown(Config.ResumeKey))
                {
                    if (passiveVideoPlayers.Count > 0 && wasPaused && Time.timeScale > 0.1f)
                    {
                        var master = passiveVideoPlayers[0];
                        master.time = savedPlaybackTime;
                        master.Play();
                        wasPaused = false;
                        wasPausedByTimescale = false;
                        for (int i = 0; i < passiveVideoPlayers.Count; i++)
                        {
                            if (i != 0)
                            {
                                passiveVideoPlayers[i].Play();
                            }
                        }
                    }
                }
                if (Input.GetKeyDown(Config.NextVideoKey))
                {
                    if (playlistVideoQueue.Count > 0)
                    {
                        MelonLogger.Msg("Skipping to next video in playlist queue.");
                        if (passiveVideoPlayers.Count > 0 && passiveVideoPlayers[0] != null)
                        {
                            var master = passiveVideoPlayers[0];
                            master.Stop();
                            RemoveVideoEndHandler(master);
                        }

                        MelonCoroutines.Start(StartPlaylistFromLocalFiles());
                    }
                    else
                    {
                        savedPlaybackTime = 0;
                        MelonCoroutines.Start(SetupVideoPlayer());
                    }
                }
                if (Input.GetKeyDown(Config.PreviousVideoKey))
                {
                    savedPlaybackTime = 0;
                    MelonCoroutines.Start(SetupVideoPlayer(false, false));
                }
                if (Input.GetKeyDown(Config.Seekforward))
                {
                    savedPlaybackTime += Config.SeekAmount;
                    MelonCoroutines.Start(HandleSeek(savedPlaybackTime));
                }
                if (Input.GetKeyDown(Config.Seekbackward))
                {
                    savedPlaybackTime -= Config.SeekAmount;
                    MelonCoroutines.Start(HandleSeek(savedPlaybackTime));
                }

                if (Input.GetKeyDown(Config.YoutubeURLKey))
                {
                    try
                    {
                        string clipboardText = GetClipboardContent();
                        MelonLogger.Msg($"Clipboard content: {(string.IsNullOrEmpty(clipboardText) ? "Empty" : clipboardText)}");

                        if (!isDownloadingYoutube && !isProcessingPlaylist)
                        {
                            if (!string.IsNullOrEmpty(clipboardText) && IsYoutubeUrl(clipboardText))
                            {
                                if (clipboardText.Contains("youtube.com/playlist?list="))
                                {
                                    MelonLogger.Msg($"Processing YouTube playlist: {clipboardText}");
                                    bool isVideoPlaying = passiveVideoPlayers.Count > 0 && passiveVideoPlayers[0] != null && passiveVideoPlayers[0].isPlaying;
                                    MelonCoroutines.Start(ProcessYoutubePlaylist(clipboardText, !isVideoPlaying));
                                }
                                else
                                {
                                    MelonLogger.Msg($"Adding YouTube URL to queue: {clipboardText}");
                                    bool isVideoPlaying = passiveVideoPlayers.Count > 0 && passiveVideoPlayers[0] != null && passiveVideoPlayers[0].isPlaying;
                                    MelonCoroutines.Start(PlayYoutubeVideo(clipboardText, false, !isVideoPlaying));
                                }
                            }
                            else
                            {
                                MelonLogger.Warning($"Clipboard content is not a valid YouTube URL: '{clipboardText}'");
                            }
                        }
                        else if (isProcessingPlaylist)
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
            if (passiveVideoPlayers[0] == null)
                yield break;

            for (int i = 0; i < passiveVideoPlayers.Count; i++)
            {
                passiveVideoPlayers[i].time = newTime;
            }
        }

        private static IEnumerator HandleUpdate()
        {
            if (settingUpdate) yield break;
            settingUpdate = true;
            try
            {
                if (passiveVideoPlayers.Count == 0)
                {
                    settingUpdate = false;
                    yield break;
                }
                var vp = passiveVideoPlayers[0];
                if (vp == null)
                {
                    settingUpdate = false;
                    yield break;
                }

                if (!wasPausedByTimescale && !wasPaused && vp.isPlaying)
                {
                    double currentTime = vp.time;
                    double videoLength = vp.length;
                    if (currentTime > 0.1 || (videoLength > 0 && (currentTime >= videoLength - 0.5 || savedPlaybackTime >= videoLength - 0.5)))
                    {
                        savedPlaybackTime = currentTime;
                    }
                }

                if (Time.timeScale == 0f && vp.isPlaying)
                {
                    vp.Pause();
                    wasPausedByTimescale = true;
                    for (int i = 0; i < passiveVideoPlayers.Count; i++)
                    {
                        if (i != 0)
                        {
                            passiveVideoPlayers[i].Pause();
                        }
                    }
                }
                else if (Time.timeScale > 0.1f && !wasPaused && (!vp.isPlaying || wasPausedByTimescale))
                {
                    vp.time = savedPlaybackTime;
                    vp.Play();
                    wasPausedByTimescale = false;
                    for (int i = 0; i < passiveVideoPlayers.Count; i++)
                    {
                        if (i != 0)
                        {
                            passiveVideoPlayers[i].Play();
                        }
                    }
                }

                for (int i = 0; i < tvInterfaces.Count; i++)
                {
                    var tvInterface = tvInterfaces[i];
                    if (tvInterface == null) continue;
                    var timeChild = tvInterfaces[i].Find("Time") ?? tvInterfaces[i].Find("Model")?.Find("Name");
                    if (timeChild == null) continue;
                    bool needsUpdate = false;
                    var renderer = timeChild.GetComponent<MeshRenderer>();
                    if (renderer == null || renderer.material == null)
                    {
                        needsUpdate = true;
                    } else
                    {
                        Material mat = renderer.material;
                        needsUpdate = (mat.shader == null || mat.shader.name != "UI/Default" || mat.mainTexture != sharedRenderTexture);
                    }
                    
                    if (needsUpdate)
                    {
                        MelonCoroutines.Start(SetupPassiveDisplay(timeChild, i == 0));
                    }
                }
            }
            catch { }
            settingUpdate = false;

        }

        private static IEnumerator SetupVideoPlayer(bool first = false, bool forward = true, string directVideoPath = null)
        {
            if (settingUp) yield break;
            settingUp = true;

            string videoToPlay = "";

            tvInterfaces.Clear();
            passiveVideoPlayers.Clear();
            tvInterfaces = GameObject.FindObjectsOfType<Transform>()
               .Where(t => t.name == "TVInterface" || t.name == "MetalSign_Built(Clone)" || t.name == "WoodlSign_Built(Clone)")
               .Where(t =>
               {
                   if (t.name == "TVInterface") return true;
                   var labelledSurfaceItem = t.GetComponent<LabelledSurfaceItem>();
                   return labelledSurfaceItem != null && labelledSurfaceItem.Message?.ToLower() == "customtv";
               })
               .ToList();

            sharedRenderTexture = new RenderTexture(1920, 1080, 0);

            if (tvInterfaces.Count == 0)
            {
                settingUp = false;
                yield break;
            }

            if (!string.IsNullOrEmpty(directVideoPath))
            {
                videoToPlay = directVideoPath;
            }
            else
            {
                string videoDir = Path.Combine(MelonEnvironment.ModsDirectory, "TV");
                var newVideoFiles = Directory.GetFiles(videoDir, "*.mp4").ToList();

                if (newVideoFiles.Count == 0 && playlistVideoQueue.Count == 0)
                {
                    settingUp = false;
                    MelonLogger.Error("Could not find any videos to play (no MP4s in TV folder and playlist queue is empty).");
                    yield break;
                }

                if (playlistVideoQueue.Count == 0)
                {
                    if (newVideoFiles.Count != videoFiles.Count)
                    {
                        videoFiles = newVideoFiles;
                        if (Config.Shuffle)
                        {
                            videoFiles.Shuffle(rng);
                        }
                        else
                        {
                            videoFiles = [.. newVideoFiles.OrderBy(f => f, new SmartEpisodeComparer())];
                        }
                    }
                }

                if (playlistVideoQueue.Count > 0)
                {
                    lock (playlistVideoQueue)
                    {
                        videoToPlay = playlistVideoQueue.Dequeue();
                    }
                }
                else
                {
                    int newIndex = first ? 0 : forward ? (currentVideoIndex + 1) % videoFiles.Count : (currentVideoIndex - 1 + videoFiles.Count) % videoFiles.Count;
                    currentVideoIndex = newIndex;
                    videoToPlay = videoFiles[currentVideoIndex];
                }
            }

            videoFilePath = videoToPlay;

            for (int i = 0; i < tvInterfaces.Count; i++)
            {
                bool isMaster = (i == 0);
                if (tvInterfaces[i] == null) continue;
                var timeChild = tvInterfaces[i].Find("Time") ?? tvInterfaces[i].Find("Model")?.Find("Name");
                MelonCoroutines.Start(SetupPassiveDisplay(timeChild, isMaster));
            }

            float timeoutStart = Time.time;
            while (passiveVideoPlayers.Count != tvInterfaces.Count)
            {
                if (Time.time - timeoutStart > 15f)
                {
                    MelonLogger.Warning($"Timeout waiting for video players to be set up. Expected {tvInterfaces.Count}, got {passiveVideoPlayers.Count}");
                    break;
                }
                yield return null;
            }
            settingUp = false;
        }

        private static IEnumerator SetupPassiveDisplay(Transform timeChild, bool makePlayer)
        {
            if (timeChild == null) yield break;

            try
            {
                for (int i = timeChild.childCount - 1; i >= 0; i--) GameObject.Destroy(timeChild.GetChild(i).gameObject);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error cleaning up child objects: {ex.Message}");
            }

            try
            {
                MeshRenderer renderer = timeChild.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    renderer = timeChild.gameObject.AddComponent<MeshRenderer>();
                }

                MeshFilter meshFilter = timeChild.GetComponent<MeshFilter>();
                if (meshFilter == null)
                {
                    meshFilter = timeChild.gameObject.AddComponent<MeshFilter>();
                }

                meshFilter.mesh = CreateQuadMesh();

                Shader unlitShader = Shader.Find("UI/Default");
                if (unlitShader == null)
                {
                    MelonLogger.Error("Failed to find UI/Default shader");
                    unlitShader = Shader.Find("Unlit/Texture");
                    if (unlitShader == null)
                    {
                        MelonLogger.Error("Failed to find fallback shader, using default material");
                        yield break;
                    }
                }

                Material cleanMat = new Material(unlitShader)
                {
                    mainTexture = sharedRenderTexture
                };
                renderer.material = cleanMat;

                timeChild.localPosition = Vector3.zero;
                if (timeChild.name == "Name")
                {
                    timeChild.localScale = new Vector3(3f, 2.2f, 0f);
                    timeChild.localPosition = new Vector3(0f, 0f, 0.6f);
                } else
                {
                    timeChild.localScale = new Vector3(680f, 400f, 0f);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error setting up renderer: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
                yield break;
            }

            VideoPlayer vp = null;
            AudioSource audioSrc = null;

            try
            {
                vp = timeChild.GetComponent<VideoPlayer>();
                if (vp == null)
                {
                    MelonLogger.Msg("Creating new VideoPlayer component");
                    vp = timeChild.gameObject.AddComponent<VideoPlayer>();
                }
                else
                {
                    MelonLogger.Msg("Using existing VideoPlayer component");
                    RemoveVideoEndHandler(vp);
                    RemoveErrorHandler(vp);
                    vp.Stop();
                }

                if (makePlayer)
                {
                    RemoveVideoEndHandler(vp);
                }

                audioSrc = timeChild.GetComponent<AudioSource>();
                if (audioSrc == null)
                {
                    MelonLogger.Msg("Creating new AudioSource component");
                    audioSrc = timeChild.gameObject.AddComponent<AudioSource>();
                }
                else
                {
                    MelonLogger.Msg("Using existing AudioSource component");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error creating video player: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
                yield break;
            }

            yield return null;

            try
            {
                audioSrc.spatialBlend = 1.0f;
                audioSrc.minDistance = 1f;
                audioSrc.maxDistance = 10f;
                audioSrc.volume = Config.AudioVolume;
                audioSrc.rolloffMode = AudioRolloffMode.Logarithmic;

                vp.playOnAwake = false;
                vp.isLooping = (videoFiles.Count == 1 && playlistVideoQueue.Count == 0);
                vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
                vp.SetTargetAudioSource(0, audioSrc);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error configuring audio/video components: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
                yield break;
            }

            string normalizedVideoPath = Path.GetFullPath(videoFilePath);
            MelonLogger.Msg($"Setting video player URL to: {normalizedVideoPath}");

            if (!File.Exists(normalizedVideoPath))
            {
                MelonLogger.Error($"Video file does not exist: {normalizedVideoPath}");
                yield break;
            }
            else
            {
                FileInfo fileInfo = new FileInfo(normalizedVideoPath);
                MelonLogger.Msg($"Video file exists, size: {fileInfo.Length / (1024.0 * 1024.0):F2} MB");
            }

            bool prepared = false;
            bool triedFileProtocol = false;
            float prepareStartTime;
            float elapsed;
            const float maxPrepareTime = 15f;
            int dotCount;
            string timeoutErrorMsg = "";

            try
            {
                vp.url = normalizedVideoPath;
                AddErrorHandler(vp);
                if (makePlayer)
                {
                    vp.renderMode = VideoRenderMode.RenderTexture;
                    vp.targetTexture = sharedRenderTexture;
                }
                else
                {
                    vp.renderMode = VideoRenderMode.APIOnly;
                }
                passiveVideoPlayers.Add(vp);
                MelonLogger.Msg($"Added video player to passiveVideoPlayers collection (total: {passiveVideoPlayers.Count})");
                MelonLogger.Msg($"Trying to prepare video with plain path: {vp.url}");
                vp.Prepare();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error setting up video player (plain path): {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
                yield break;
            }

            prepareStartTime = Time.time;
            dotCount = 0;
            while (!vp.isPrepared)
            {
                elapsed = Time.time - prepareStartTime;
                if ((int)elapsed % 3 == 0 && dotCount < (int)elapsed / 3)
                {
                    dotCount = (int)elapsed / 3;
                    string dots = new string('.', (dotCount % 4) + 1);
                    MelonLogger.Msg($"Preparing video{dots} ({elapsed:F1}s / {maxPrepareTime:F1}s)");
                }
                if (elapsed > maxPrepareTime)
                {
                    timeoutErrorMsg = $"Video preparation timed out for: {normalizedVideoPath} (plain path)";
                    break;
                }
                yield return null;
            }

            if (vp.isPrepared)
            {
                MelonLogger.Msg($"Video prepared successfully with plain path");
                prepared = true;
            }
            else
            {
                MelonLogger.Warning(timeoutErrorMsg);
                MelonLogger.Msg("Trying again with file:// protocol as fallback");
                passiveVideoPlayers.Remove(vp);
                triedFileProtocol = true;
                GameObject.Destroy(vp);
                yield return null;
                vp = timeChild.gameObject.AddComponent<VideoPlayer>();
                audioSrc = timeChild.GetComponent<AudioSource>() ?? timeChild.gameObject.AddComponent<AudioSource>();
                audioSrc.spatialBlend = 1.0f;
                audioSrc.minDistance = 1f;
                audioSrc.maxDistance = 10f;
                audioSrc.volume = Config.AudioVolume;
                audioSrc.rolloffMode = AudioRolloffMode.Logarithmic;
                vp.playOnAwake = false;
                vp.isLooping = (videoFiles.Count == 1 && playlistVideoQueue.Count == 0);
                vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
                vp.SetTargetAudioSource(0, audioSrc);
                if (makePlayer)
                {
                    vp.renderMode = VideoRenderMode.RenderTexture;
                    vp.targetTexture = sharedRenderTexture;
                }
                else
                {
                    vp.renderMode = VideoRenderMode.APIOnly;
                }
                string fileUrl = $"file://{normalizedVideoPath.Replace('\\', '/')}";
                vp.url = fileUrl;
                AddErrorHandler(vp);
                passiveVideoPlayers.Add(vp);
                MelonLogger.Msg($"Trying to prepare video with file:// protocol: {vp.url}");
                vp.Prepare();
                prepareStartTime = Time.time;
                dotCount = 0;
                while (!vp.isPrepared)
                {
                    elapsed = Time.time - prepareStartTime;
                    if ((int)elapsed % 3 == 0 && dotCount < (int)elapsed / 3)
                    {
                        dotCount = (int)elapsed / 3;
                        string dots = new string('.', (dotCount % 4) + 1);
                        MelonLogger.Msg($"Preparing video (file://){dots} ({elapsed:F1}s / {maxPrepareTime:F1}s)");
                    }
                    if (elapsed > maxPrepareTime)
                    {
                        timeoutErrorMsg = $"Video preparation timed out for: {fileUrl} (file:// protocol)";
                        break;
                    }
                    yield return null;
                }
                if (vp.isPrepared)
                {
                    MelonLogger.Msg($"Video prepared successfully with file:// protocol");
                    prepared = true;
                }
                else
                {
                    MelonLogger.Error(timeoutErrorMsg);
                    yield break;
                }
            }
            yield return null;

            try
            {
                vp.time = savedPlaybackTime;
                if (makePlayer && !wasPaused && !wasPausedByTimescale || !makePlayer)
                {
                    MelonLogger.Msg("Starting video playback");
                    vp.Play();
                }
                vp.time = savedPlaybackTime;
                if (makePlayer)
                {
                    AddVideoEndHandler(vp);
                    MelonLogger.Msg("Added video end handler to master player");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error starting video playback: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }

        private static void OnVideoEnd(VideoPlayer vp)
        {
            MelonLogger.Msg("Video ended, checking for next video");

            RemoveVideoEndHandler(vp);

            if (playlistVideoQueue.Count > 0)
            {
                MelonLogger.Msg($"Found {playlistVideoQueue.Count} videos in playlist queue, starting next one");
                MelonCoroutines.Start(StartPlaylistFromLocalFiles());
                return;
            }

            bool wasYoutubePlaylist = isYoutubeMode && videoFilePath.StartsWith(Config.YoutubeTempFolder);

            if (wasYoutubePlaylist)
            {
                MelonLogger.Msg("Playlist finished - stopping playback");
                videoFiles.RemoveAll(x => x.StartsWith(Config.YoutubeTempFolder));

                isYoutubeMode = false;

                MelonLogger.Msg("End of playlist - video will not auto-repeat");
                return;
            }

            if (videoFiles.Count <= 1)
            {
                AddVideoEndHandler(vp);
                MelonLogger.Msg("Single video will loop - added event handler back");
                return;
            }

            MelonLogger.Msg("No playlist videos, proceeding with regular random playback");
            int newIndex;
            do
            {
                newIndex = rng.Next(videoFiles.Count);
            } while (videoFiles.Count > 1 && newIndex == currentVideoIndex);

            currentVideoIndex = newIndex;
            videoFilePath = videoFiles[currentVideoIndex];
            if (savedPlaybackTime > 0.2) savedPlaybackTime = 0;
            MelonCoroutines.Start(SetupVideoPlayer());
        }

        private static Mesh CreateQuadMesh()
        {
            Mesh mesh = new()
            {
                vertices = new Vector3[]
                {
                    new(-0.5f, -0.5f, 0),
                    new( 0.5f, -0.5f, 0),
                    new( 0.5f,  0.5f, 0),
                    new(-0.5f,  0.5f, 0)
                },

                uv = new Vector2[]
                {
                    new(0, 0),
                    new(1, 0),
                    new(1, 1),
                    new(0, 1)
                },

                triangles = new int[]
                {
                    0, 2, 1,
                    0, 3, 2
                }
            };

            mesh.RecalculateNormals();
            return mesh;
        }

        private static IEnumerator PlayYoutubeVideo(string url, bool isPartOfPlaylist = false, bool startPlayback = true)
        {
            if (isDownloadingYoutube)
            {
                MelonLogger.Warning("Already downloading a YouTube video. Please wait...");
                yield break;
            }

            isDownloadingYoutube = true;
            currentYoutubeUrl = url;

            if (youtubeCache.TryGetValue(url, out string cachedPath) && File.Exists(cachedPath))
            {
                MelonLogger.Msg($"Using cached YouTube video: {cachedPath}");

                ytCacheQueue.Remove(url);
                ytCacheQueue.Enqueue(url);

                if (playlistVideoQueue.Count == 1 && startPlayback)
                {
                    MelonLogger.Msg("Starting playback of cached video");

                    var playbackCoroutine = PlayCachedYoutubeVideo(cachedPath, isPartOfPlaylist);
                    isDownloadingYoutube = false;
                    yield return playbackCoroutine;
                    yield break;
                }
                else
                {
                    MelonLogger.Msg($"Added cached video to queue ({playlistVideoQueue.Count} videos in queue)");
                }

                isDownloadingYoutube = false;
                yield break;
            }

            MelonLogger.Msg($"Downloading video from: {url}");

            string ytDlpExePath = Path.Combine(Config.YtDlpFolderPath, "yt-dlp.exe");
            if (!File.Exists(ytDlpExePath))
            {
                MelonLogger.Error("yt-dlp.exe not found. Cannot download YouTube videos.");
                MelonLogger.Msg($"Please download yt-dlp.exe manually from https://github.com/yt-dlp/yt-dlp/releases and place it in: {Config.YtDlpFolderPath}");
                isDownloadingYoutube = false;
                yield break;
            }

            DownloadResult result = null;
            yield return DownloadYoutubeVideo(url, 1, 1, (downloadResult) => {
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

                isDownloadingYoutube = false;
                yield break;
            }

            lock (playlistVideoQueue)
            {
                playlistVideoQueue.Enqueue(result.FilePath);

                if (playlistVideoQueue.Count == 1 && startPlayback)
                {
                    MelonLogger.Msg("Starting playback of downloaded video");

                    var playbackCoroutine = PlayCachedYoutubeVideo(result.FilePath, isPartOfPlaylist);
                    isDownloadingYoutube = false;
                    yield return playbackCoroutine;
                    yield break;
                }
                else
                {
                    MelonLogger.Msg($"Added downloaded video to queue ({playlistVideoQueue.Count} videos in queue)");
                }
            }

            isDownloadingYoutube = false;
        }

        private static IEnumerator PlayCachedYoutubeVideo(string filePath, bool isPartOfPlaylist = false)
        {
            string normalizedPath = Path.GetFullPath(filePath).Replace('\\', '/');
            MelonLogger.Msg($"Playing YouTube video from normalized path: {normalizedPath}");

            if (!File.Exists(normalizedPath))
            {
                MelonLogger.Error($"YouTube video file not found at: {normalizedPath}");
                isDownloadingYoutube = false;

                if (isPartOfPlaylist && playlistVideoQueue.Count > 0)
                {
                    MelonLogger.Msg("Skipping missing video and proceeding to next one in playlist");
                    MelonCoroutines.Start(StartPlaylistFromLocalFiles());
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

                FileInfo fileInfo = new FileInfo(normalizedPath);
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

                isDownloadingYoutube = false;

                if (isPartOfPlaylist && playlistVideoQueue.Count > 0)
                {
                    MelonLogger.Msg("Skipping problematic video and proceeding to next one in playlist");
                    MelonCoroutines.Start(StartPlaylistFromLocalFiles());
                }

                yield break;
            }

            if (!canAccessFile)
            {
                MelonLogger.Error("Could not access the video file");
                isDownloadingYoutube = false;

                if (isPartOfPlaylist && playlistVideoQueue.Count > 0)
                {
                    MelonLogger.Msg("Skipping inaccessible video and proceeding to next one in playlist");
                    MelonCoroutines.Start(StartPlaylistFromLocalFiles());
                }

                yield break;
            }

            if (tvInterfaces.Count == 0 || tvInterfaces[0] == null)
            {
                MelonLogger.Msg("No TV interfaces found, looking for them now...");
                tvInterfaces.Clear();
                tvInterfaces = GameObject.FindObjectsOfType<Transform>().Where(t => t.name == "TVInterface").ToList();

                if (tvInterfaces.Count == 0)
                {
                    MelonLogger.Error("No TV interfaces found, cannot play video");
                    isDownloadingYoutube = false;

                    if (isPartOfPlaylist && playlistVideoQueue.Count > 0)
                    {
                        MelonLogger.Msg("Will try again with next video in 5 seconds...");
                        MelonCoroutines.Start(DelayNextPlaylistVideo());
                    }

                    yield break;
                }
            }

            if (sharedRenderTexture == null)
            {
                MelonLogger.Msg("Creating new render texture");
                sharedRenderTexture = new RenderTexture(1920, 1080, 0);
            }

            isYoutubeMode = true;
            videoFilePath = normalizedPath;
            savedPlaybackTime = 0;

            int originalIndex = currentVideoIndex;

            if (!videoFiles.Contains(normalizedPath))
            {
                videoFiles.RemoveAll(x => x.StartsWith(Config.YoutubeTempFolder));

                videoFiles.Add(normalizedPath);
                currentVideoIndex = videoFiles.Count - 1;

                MelonLogger.Msg("Added YouTube video to videoFiles list, starting video player setup");
                MelonLogger.Msg($"Current video index: {currentVideoIndex}, total videos: {videoFiles.Count}");

                if (settingUp)
                {
                    MelonLogger.Msg("Video player setup already in progress, waiting...");
                    float startWaitTime = Time.time;
                    while (settingUp && Time.time - startWaitTime < 10f)
                    {
                        yield return null;
                    }

                    if (settingUp)
                    {
                        MelonLogger.Error("Video player setup is taking too long, forcing it to continue");
                        settingUp = false;
                    }
                }

                yield return SetupVideoPlayer(false, true, normalizedPath);

                if (passiveVideoPlayers.Count == 0 || passiveVideoPlayers[0] == null)
                {
                    MelonLogger.Error("Failed to create video player for YouTube video");
                    MelonLogger.Msg($"Passive video players count: {passiveVideoPlayers.Count}");
                    if (passiveVideoPlayers.Count > 0)
                    {
                        MelonLogger.Msg($"First player is null: {passiveVideoPlayers[0] == null}");
                    }

                    if (currentVideoIndex < videoFiles.Count)
                    {
                        videoFiles.RemoveAt(currentVideoIndex);
                    }
                    currentVideoIndex = originalIndex;

                    if (isPartOfPlaylist && playlistVideoQueue.Count > 0)
                    {
                        MelonLogger.Msg("Player setup failed, proceeding to next video in playlist");
                        MelonCoroutines.Start(StartPlaylistFromLocalFiles());
                    }
                }
                else
                {
                    try
                    {
                        var masterPlayer = passiveVideoPlayers[0];

                        masterPlayer.isLooping = false;
                        MelonLogger.Msg("Disabled looping on video player");

                        RemoveVideoEndHandler(masterPlayer);

                        AddVideoEndHandler(masterPlayer);
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
                currentVideoIndex = videoFiles.IndexOf(normalizedPath);
                MelonLogger.Msg($"Video already in list at index {currentVideoIndex}, setting up player");

                if (settingUp)
                {
                    MelonLogger.Msg("Video player setup already in progress, waiting...");
                    float startWaitTime = Time.time;
                    while (settingUp && Time.time - startWaitTime < 10f)
                    {
                        yield return null;
                    }

                    if (settingUp)
                    {
                        MelonLogger.Error("Video player setup is taking too long, forcing it to continue");
                        settingUp = false;
                    }
                }

                yield return SetupVideoPlayer(false, true, normalizedPath);

                if (passiveVideoPlayers.Count == 0 || passiveVideoPlayers[0] == null)
                {
                    MelonLogger.Error("Failed to create video player for existing YouTube video");
                    currentVideoIndex = originalIndex;

                    if (isPartOfPlaylist && playlistVideoQueue.Count > 0)
                    {
                        MelonLogger.Msg("Player setup failed, proceeding to next video in playlist");
                        MelonCoroutines.Start(StartPlaylistFromLocalFiles());
                    }
                }
                else
                {
                    try
                    {
                        var masterPlayer = passiveVideoPlayers[0];

                        masterPlayer.isLooping = false;
                        MelonLogger.Msg("Disabled looping on video player");

                        RemoveVideoEndHandler(masterPlayer);

                        AddVideoEndHandler(masterPlayer);
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

            isDownloadingYoutube = false;
        }

        private static IEnumerator DelayNextPlaylistVideo()
        {
            yield return new WaitForSeconds(5f);

            if (playlistVideoQueue.Count > 0)
            {
                MelonLogger.Msg("Trying next video in playlist after delay");
                MelonCoroutines.Start(StartPlaylistFromLocalFiles());
            }
        }

        private static IEnumerator CleanupYoutubeTempFolder(bool forceDeleteAll = false)
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

                youtubeCache.Clear();
                ytCacheQueue.Clear();
                yield break;
            }

            string[] files = Directory.GetFiles(Config.YoutubeTempFolder, "*.mp4")
                .OrderByDescending(f => new FileInfo(f).CreationTime)
                .ToArray();

            if (files.Length > Config.MaxCachedYoutubeVideos)
            {
                for (int i = Config.MaxCachedYoutubeVideos; i < files.Length; i++)
                {
                    try
                    {
                        string url = youtubeCache.FirstOrDefault(x => x.Value == files[i]).Key;
                        if (!string.IsNullOrEmpty(url))
                        {
                            youtubeCache.Remove(url);
                            ytCacheQueue.Remove(url);
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

        private static void DontDestroyOnLoad(GameObject obj)
        {
            obj.transform.parent = null;
            GameObject.DontDestroyOnLoad(obj);
        }

        private static bool IsYoutubeUrl(string url)
        {
            return !string.IsNullOrEmpty(url) &&
                  (url.Contains("youtube.com/watch") ||
                   url.Contains("youtu.be/") ||
                   url.Contains("youtube.com/shorts/") ||
                   url.Contains("youtube.com/embed/") ||
                   url.Contains("youtube.com/playlist?list="));
        }

        private static string GetClipboardContent()
        {
            try
            {
                string clipboardText = GUIUtility.systemCopyBuffer;
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    return clipboardText;
                }

                TextEditor te = new TextEditor();
                te.Paste();
                return te.text;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to access clipboard: {ex.Message}");
                return string.Empty;
            }
        }

        private static IEnumerator CreateDownloadProgressUI()
        {
            if (downloadProgressUI != null)
            {
                GameObject.Destroy(downloadProgressUI);
            }

            downloadProgressUI = new GameObject("DownloadProgressUI");
            DontDestroyOnLoad(downloadProgressUI);

            Canvas canvas = downloadProgressUI.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = downloadProgressUI.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            downloadProgressUI.AddComponent<GraphicRaycaster>();

            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(downloadProgressUI.transform, false);

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0);
            panelRect.anchorMax = new Vector2(0.5f, 0);
            panelRect.pivot = new Vector2(0.5f, 0);
            panelRect.sizeDelta = new Vector2(600, 100);
            panelRect.anchoredPosition = new Vector2(0, 150);

            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(panel.transform, false);

            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.sizeDelta = new Vector2(0, 30);
            titleRect.anchoredPosition = new Vector2(0, -5);

            Text titleText = titleObj.AddComponent<Text>();
            titleText.text = "Downloading YouTube Video";
            titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            titleText.fontSize = 18;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;

            GameObject progressBgObj = new GameObject("ProgressBarBg");
            progressBgObj.transform.SetParent(panel.transform, false);

            RectTransform progressBgRect = progressBgObj.AddComponent<RectTransform>();
            progressBgRect.anchorMin = new Vector2(0.05f, 0.5f);
            progressBgRect.anchorMax = new Vector2(0.95f, 0.5f);
            progressBgRect.pivot = new Vector2(0.5f, 0.5f);
            progressBgRect.sizeDelta = new Vector2(0, 20);
            progressBgRect.anchoredPosition = new Vector2(0, 0);

            Image progressBgImage = progressBgObj.AddComponent<Image>();
            progressBgImage.color = new Color(0.2f, 0.2f, 0.2f, 1);

            GameObject progressFillObj = new GameObject("ProgressBarFill");
            progressFillObj.transform.SetParent(progressBgObj.transform, false);

            RectTransform progressFillRect = progressFillObj.AddComponent<RectTransform>();
            progressFillRect.anchorMin = new Vector2(0, 0);
            progressFillRect.anchorMax = new Vector2(0, 1);
            progressFillRect.pivot = new Vector2(0, 0.5f);
            progressFillRect.sizeDelta = new Vector2(0, 0);

            Image progressFillImage = progressFillObj.AddComponent<Image>();
            progressFillImage.color = new Color(0.2f, 0.7f, 0.2f, 1);

            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(panel.transform, false);

            RectTransform statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0, 0);
            statusRect.anchorMax = new Vector2(1, 0);
            statusRect.pivot = new Vector2(0.5f, 0);
            statusRect.sizeDelta = new Vector2(0, 30);
            statusRect.anchoredPosition = new Vector2(0, 10);

            Text statusText = statusObj.AddComponent<Text>();
            statusText.text = "Initializing...";
            statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            statusText.fontSize = 14;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = Color.white;

            yield return null;
        }

        private static void UpdateDownloadProgressUI(float progress, string status)
        {
            if (downloadProgressUI == null || !showDownloadProgress)
                return;

            try
            {
                progress = Mathf.Clamp01(progress);

                Transform panel = downloadProgressUI.transform.Find("Panel");
                if (panel == null)
                    return;

                Transform progressBg = panel.Find("ProgressBarBg");
                if (progressBg != null)
                {
                    Transform progressFill = progressBg.Find("ProgressBarFill");
                    if (progressFill != null)
                    {
                        RectTransform fillRect = progressFill.GetComponent<RectTransform>();
                        if (fillRect != null)
                        {
                            fillRect.anchorMin = new Vector2(0, 0);
                            fillRect.anchorMax = new Vector2(progress, 1);
                            fillRect.sizeDelta = Vector2.zero;
                        }
                    }
                }

                Transform statusObj = panel.Find("StatusText");
                if (statusObj != null)
                {
                    Text statusText = statusObj.GetComponent<Text>();
                    if (statusText != null && !string.IsNullOrEmpty(status))
                    {
                        statusText.text = status;
                    }
                }

                Transform titleObj = panel.Find("TitleText");
                if (titleObj != null)
                {
                    Text titleText = titleObj.GetComponent<Text>();
                    if (titleText != null)
                    {
                        titleText.text = $"Downloading YouTube Video ({progress * 100:F0}%)";
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error updating download UI: {ex.Message}");
            }
        }

        private static void DestroyDownloadProgressUI()
        {
            if (downloadProgressUI != null)
            {
                GameObject.Destroy(downloadProgressUI);
                downloadProgressUI = null;
            }
        }

        private class DownloadResult
        {
            public string FilePath { get; set; }
            public bool IsAgeRestricted { get; set; }
            public bool Success { get; set; }

            public DownloadResult(string filePath = null, bool isAgeRestricted = false)
            {
                FilePath = filePath;
                IsAgeRestricted = isAgeRestricted;
                Success = !string.IsNullOrEmpty(filePath) && File.Exists(filePath);
            }
        }

        private static IEnumerator ProcessYoutubePlaylist(string playlistUrl, bool clearExistingQueue = true)
        {
            if (isProcessingPlaylist)
            {
                MelonLogger.Warning("Already processing a YouTube playlist. Please wait...");
                yield break;
            }

            isProcessingPlaylist = true;
            currentPlaylistUrl = playlistUrl;

            if (clearExistingQueue)
            {
                playlistVideoQueue.Clear();
                MelonLogger.Msg("Cleared existing queue");
            }
            else
            {
                MelonLogger.Msg($"Adding to existing queue ({playlistVideoQueue.Count} videos already in queue)");
            }

            MelonLogger.Msg("Extracting video URLs from playlist...");

            string ytDlpExePath = Path.Combine(Config.YtDlpFolderPath, "yt-dlp.exe");
            if (!File.Exists(ytDlpExePath))
            {
                MelonLogger.Error("yt-dlp.exe not found. Cannot process YouTube playlist.");
                MelonLogger.Msg($"Please download yt-dlp.exe manually from https://github.com/yt-dlp/yt-dlp/releases and place it in: {Config.YtDlpFolderPath}");
                isProcessingPlaylist = false;
                yield break;
            }

            yield return CreatePlaylistProgressUI("Extracting videos from playlist...");

            List<string> videoUrls = new List<string>();
            bool extractionSuccess = false;

            Task extractionTask = Task.Run(() => {
                try
                {
                    using (Process process = new Process())
                    {
                        process.StartInfo.FileName = ytDlpExePath;
                        process.StartInfo.Arguments = $"--flat-playlist --get-id --get-title \"{playlistUrl}\"";
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;

                        StringBuilder outputBuilder = new StringBuilder();

                        process.OutputDataReceived += (sender, args) => {
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
                string dots = new string('.', dotCount + 1);
                UpdatePlaylistProgressUI($"Extracting videos{dots}");
                yield return new WaitForSeconds(0.5f);
            }

            if (!extractionSuccess || videoUrls.Count == 0)
            {
                MelonLogger.Error("Failed to extract videos from playlist or playlist is empty.");
                DestroyPlaylistProgressUI();
                isProcessingPlaylist = false;
                yield break;
            }

            MelonLogger.Msg($"Found {videoUrls.Count} videos in playlist. Starting download...");

            bool isVideoCurrentlyPlaying = false;
            if (passiveVideoPlayers.Count > 0 && passiveVideoPlayers[0] != null)
            {
                var master = passiveVideoPlayers[0];
                isVideoCurrentlyPlaying = master.isPlaying;
            }

            bool queueWasEmpty = playlistVideoQueue.Count == 0;

            if (videoUrls.Count > 0)
            {
                string firstVideoUrl = videoUrls[0];
                bool shouldStartPlaying = queueWasEmpty && !isVideoCurrentlyPlaying;

                MelonLogger.Msg($"Processing first video from playlist ({(shouldStartPlaying ? "will start playback" : "adding to queue")})...");
                UpdatePlaylistProgressUI("Processing first video...");

                string firstVideoPath = null;
                bool firstVideoDownloaded = false;

                if (youtubeCache.TryGetValue(firstVideoUrl, out string cachedPath) && File.Exists(cachedPath))
                {
                    MelonLogger.Msg($"Using cached video for first playlist item: {firstVideoUrl}");
                    firstVideoPath = cachedPath;
                    firstVideoDownloaded = true;
                }
                else
                {
                    DownloadResult result = null;
                    yield return DownloadYoutubeVideo(firstVideoUrl, 1, videoUrls.Count, (downloadResult) => {
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
                            if (youtubeCache.TryGetValue(nextVideoUrl, out cachedPath) && File.Exists(cachedPath))
                            {
                                MelonLogger.Msg($"Using cached video for alternate first playlist item: {nextVideoUrl}");
                                firstVideoPath = cachedPath;
                                firstVideoDownloaded = true;
                                break;
                            }

                            MelonLogger.Msg($"Trying to download alternate video {i + 1}...");

                            DownloadResult alternateResult = null;
                            yield return DownloadYoutubeVideo(nextVideoUrl, i + 1, videoUrls.Count, (downloadResult) => {
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
                    lock (playlistVideoQueue)
                    {
                        playlistVideoQueue.Enqueue(firstVideoPath);
                        MelonLogger.Msg($"Added first video to queue ({playlistVideoQueue.Count} videos in queue)");
                    }

                    List<string> remainingVideos = new List<string>(videoUrls);
                    remainingVideos.RemoveAt(0);

                    if (remainingVideos.Count > 0)
                    {
                        MelonCoroutines.Start(DownloadRemainingPlaylistVideos(remainingVideos, ytDlpExePath));
                    }

                    if (shouldStartPlaying)
                    {
                        MelonLogger.Msg("Starting playback of first video in playlist");
                        MelonCoroutines.Start(StartPlaylistFromLocalFiles());
                    }
                    else
                    {
                        MelonLogger.Msg("Added playlist to queue without interrupting current playback");
                        MelonLogger.Msg($"{playlistVideoQueue.Count} videos in queue");
                    }
                }
                else
                {
                    MelonLogger.Error("Failed to download any videos from the playlist. Aborting playback.");
                    DestroyPlaylistProgressUI();
                }
            }

            isProcessingPlaylist = false;
        }

        private static IEnumerator DownloadYoutubeVideo(string videoUrl, int currentIndex, int totalVideos, Action<DownloadResult> onComplete)
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

                downloadProgress = 0f;
                if (withCookies)
                {
                    downloadStatus = $"Downloading video {currentIndex}/{totalVideos} with Firefox cookies";
                }
                else
                {
                    downloadStatus = $"Downloading video {currentIndex}/{totalVideos}";
                }
                UpdateDownloadProgressUI(0, downloadStatus);

                Task downloadTask = Task.Run(() => {
                    try
                    {
                        using (Process process = new Process())
                        {
                            process.StartInfo.FileName = Path.Combine(Config.YtDlpFolderPath, "yt-dlp.exe");
                            string safeOutputPath = Path.GetFullPath(tempOutputPath).Replace('\\', '/');

                            StringBuilder args = new StringBuilder();
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

                            process.OutputDataReceived += (sender, args) => {
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

                                    lock (downloadLock)
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
                                                        downloadProgress = (float)downloaded / total;

                                                        string eta = parts.Length > 1 ? parts[1] : "";
                                                        string speed = parts.Length > 2 ? parts[2] : "";

                                                        double downloadedMB = downloaded / 1024.0 / 1024.0;
                                                        double totalMB = total / 1024.0 / 1024.0;

                                                        downloadStatus = $"Video {currentIndex}/{totalVideos}: {downloadedMB:F1}/{totalMB:F1} MB - {(downloadProgress * 100):F0}% - {speed}";
                                                    }
                                                }
                                            }
                                        }
                                        else if (line.Contains("Merging formats"))
                                        {
                                            downloadProgress = 0.95f;
                                            downloadStatus = $"Video {currentIndex}/{totalVideos}: Merging video and audio...";
                                        }
                                        else if (line.Contains("Downloading") && line.Contains("destfile"))
                                        {
                                            downloadProgress = 0.05f;
                                            downloadStatus = $"Video {currentIndex}/{totalVideos}: Starting download...";
                                        }
                                        else if (line.StartsWith("[download]") && line.Contains("%"))
                                        {
                                            int percentIndex = line.IndexOf("%");
                                            if (percentIndex > 10)
                                            {
                                                string percentStr = line.Substring(10, percentIndex - 10).Trim();
                                                if (float.TryParse(percentStr, out float percent))
                                                {
                                                    downloadProgress = percent / 100f;
                                                    downloadStatus = $"Video {currentIndex}/{totalVideos}: " + line.Substring(percentIndex + 1).Trim();
                                                }
                                            }
                                        }
                                        else if (line.Contains("destination"))
                                        {
                                            downloadStatus = $"Video {currentIndex}/{totalVideos}: Preparing download...";
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MelonLogger.Warning($"Error parsing progress: {ex.Message}");
                                }
                            };

                            process.ErrorDataReceived += (sender, args) => {
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

                                    FileInfo fileInfo = new FileInfo(tempOutputPath);
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
                                            if (!warnedAboutCookies)
                                            {
                                                MelonLogger.Warning("Failed to access Firefox cookies. Will try without cookies.");
                                                MelonLogger.Warning("Make sure Firefox is installed and you've logged into YouTube in Firefox.");
                                                warnedAboutCookies = true;
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

                            if (withCookies && hasFirefoxCookiesError && !warnedAboutCookies)
                            {
                                MelonLogger.Warning("Failed to access Firefox cookies. Will try without cookies for this video.");
                                MelonLogger.Warning("Make sure Firefox is installed and you've logged into YouTube in Firefox.");
                                warnedAboutCookies = true;
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

                        lock (downloadLock)
                        {
                            currentProgress = downloadProgress;
                            currentStatus = downloadStatus;
                        }

                        if (Math.Abs(currentProgress - lastProgress) > 0.001f || currentStatus != lastStatus)
                        {
                            lastProgress = currentProgress;
                            lastStatus = currentStatus;
                            UpdateDownloadProgressUI(currentProgress, currentStatus);
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

            yield return CreateDownloadProgressUI();
            showDownloadProgress = true;
            downloadProgress = 0f;

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

            showDownloadProgress = false;
            DestroyDownloadProgressUI();

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

            FileInfo fileInfo = new FileInfo(tempOutputPath);
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

            if (youtubeCache.Count >= Config.MaxCachedYoutubeVideos)
            {
                string oldestUrl = ytCacheQueue.Dequeue();
                if (youtubeCache.TryGetValue(oldestUrl, out string oldCachePath))
                {
                    youtubeCache.Remove(oldestUrl);
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

            youtubeCache[videoUrl] = tempOutputPath;
            ytCacheQueue.Enqueue(videoUrl);

            MelonLogger.Msg($"Video {currentIndex}/{totalVideos} downloaded and cached: {tempOutputPath}");
            onComplete?.Invoke(new DownloadResult(tempOutputPath, isAgeRestricted));
        }

        private static IEnumerator DownloadRemainingPlaylistVideos(List<string> videoUrls, string ytDlpExePath)
        {
            MelonLogger.Msg($"Starting background download of {videoUrls.Count} remaining videos...");
            UpdatePlaylistProgressUI($"Downloading {videoUrls.Count} remaining videos in background...");

            List<string> downloadedPaths = new List<string>();
            List<string> failedVideos = new List<string>();
            List<string> ageRestrictedVideos = new List<string>();

            int startIndex = 2;

            for (int i = 0; i < videoUrls.Count; i++)
            {
                string videoUrl = videoUrls[i];
                int currentIndex = startIndex + i;

                if (youtubeCache.TryGetValue(videoUrl, out string cachedPath) && File.Exists(cachedPath))
                {
                    MelonLogger.Msg($"Using cached video for {videoUrl}");
                    downloadedPaths.Add(cachedPath);

                    lock (playlistVideoQueue)
                    {
                        playlistVideoQueue.Enqueue(cachedPath);
                        MelonLogger.Msg($"Added cached video to queue: {cachedPath}");
                    }

                    UpdatePlaylistProgressUI($"Downloading: {i + 1}/{videoUrls.Count} remaining videos");
                    continue;
                }

                DownloadResult result = null;
                yield return DownloadYoutubeVideo(videoUrl, currentIndex, videoUrls.Count + 1, (downloadResult) => {
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

                lock (playlistVideoQueue)
                {
                    playlistVideoQueue.Enqueue(result.FilePath);
                    MelonLogger.Msg($"Added video to queue: {result.FilePath}");
                }

                UpdatePlaylistProgressUI($"Downloaded: {i + 1}/{videoUrls.Count} remaining videos");

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

            UpdatePlaylistProgressUI($"All downloads complete. {playlistVideoQueue.Count} videos in queue.");
        }

        private static IEnumerator StartPlaylistFromLocalFiles()
        {
            if (playlistVideoQueue.Count == 0)
            {
                MelonLogger.Msg("No videos in playlist queue.");
                yield break;
            }

            string filePath;
            lock (playlistVideoQueue)
            {
                filePath = playlistVideoQueue.Peek();
            }

            MelonLogger.Msg($"Starting playlist video from path: {filePath}");

            if (passiveVideoPlayers.Count > 0 && passiveVideoPlayers[0] != null)
            {
                var master = passiveVideoPlayers[0];
                if (master.isPlaying)
                {
                    master.Stop();
                    RemoveVideoEndHandler(master);
                    MelonLogger.Msg("Stopped currently playing video to start the next one");
                }
            }

            yield return PlayCachedYoutubeVideo(filePath, true);

            lock (playlistVideoQueue)
            {
                if (playlistVideoQueue.Count > 0 && playlistVideoQueue.Peek() == filePath)
                {
                    playlistVideoQueue.Dequeue();
                }
            }

            MelonLogger.Msg($"{playlistVideoQueue.Count} downloaded videos remaining in queue");
        }

        private static GameObject playlistProgressDisplay;
        private static Text playlistProgressText;

        private static IEnumerator CreatePlaylistProgressDisplay(int totalVideos)
        {
            yield break;
        }

        private static void UpdatePlaylistProgressUI(int remainingVideos)
        {
            MelonLogger.Msg($"Playlist: {remainingVideos} videos remaining");
        }

        private static void UpdatePlaylistProgressUIText(string message)
        {
            MelonLogger.Msg(message);
        }

        private static void DestroyPlaylistProgressDisplay()
        {
            if (playlistProgressDisplay != null)
            {
                GameObject.Destroy(playlistProgressDisplay);
                playlistProgressDisplay = null;
            }
        }

        private static IEnumerator CreatePlaylistProgressUI(string message)
        {
            if (downloadProgressUI != null)
            {
                GameObject.Destroy(downloadProgressUI);
            }

            downloadProgressUI = new GameObject("PlaylistProgressUI");
            DontDestroyOnLoad(downloadProgressUI);

            Canvas canvas = downloadProgressUI.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = downloadProgressUI.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            downloadProgressUI.AddComponent<GraphicRaycaster>();

            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(downloadProgressUI.transform, false);

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0);
            panelRect.anchorMax = new Vector2(0.5f, 0);
            panelRect.pivot = new Vector2(0.5f, 0);
            panelRect.sizeDelta = new Vector2(600, 100);
            panelRect.anchoredPosition = new Vector2(0, 150);

            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(panel.transform, false);

            RectTransform statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0, 0.5f);
            statusRect.anchorMax = new Vector2(1, 0.5f);
            statusRect.pivot = new Vector2(0.5f, 0.5f);
            statusRect.sizeDelta = new Vector2(0, 60);

            Text statusText = statusObj.AddComponent<Text>();
            statusText.text = message;
            statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            statusText.fontSize = 20;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = Color.white;

            yield return null;
        }

        private static void UpdatePlaylistProgressUI(string message)
        {
            if (downloadProgressUI == null)
                return;

            try
            {
                Transform panel = downloadProgressUI.transform.Find("Panel");
                if (panel == null)
                    return;

                Transform statusObj = panel.Find("StatusText");
                if (statusObj == null)
                    return;

                Text statusText = statusObj.GetComponent<Text>();
                if (statusText != null)
                {
                    statusText.text = message;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error updating playlist UI: {ex.Message}");
            }
        }

        private static void DestroyPlaylistProgressUI()
        {
            if (downloadProgressUI != null)
            {
                GameObject.Destroy(downloadProgressUI);
                downloadProgressUI = null;
            }
        }
    }

    public static class Extensions
    {
        public static void Shuffle<T>(this IList<T> list, System.Random rng)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static void Remove<T>(this Queue<T> queue, T itemToRemove)
        {
            var array = queue.ToArray();
            queue.Clear();
            foreach (var item in array)
            {
                if (!EqualityComparer<T>.Default.Equals(item, itemToRemove))
                {
                    queue.Enqueue(item);
                }
            }
        }

        public static string GetFullPath(this Transform transform)
        {
            if (transform == null)
                return "null";

            string path = transform.name;
            Transform parent = transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }
}


