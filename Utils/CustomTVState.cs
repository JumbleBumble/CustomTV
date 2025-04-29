using MelonLoader.Utils;
using UnityEngine.Video;
using UnityEngine;

namespace CustomTV.Utils
{
    public class CustomTVState
    {
        public RenderTexture SharedRenderTexture { get; set; }
        public bool WasPausedByTimescale { get; set; }
        public bool WasPaused { get; set; }
        public bool SettingUp { get; set; }
        public bool SettingUpdate { get; set; }
        public double SavedPlaybackTime { get; set; }
        public string VideoFilePath { get; set; }
        public List<string> VideoFiles { get; set; }
        public int CurrentVideoIndex { get; set; }
        public System.Random Rng { get; set; }
        public List<VideoPlayer> PassiveVideoPlayers { get; set; }
        public List<Transform> TVInterfaces { get; set; }
        public bool IsYoutubeMode { get; set; }
        public bool IsDownloadingYoutube { get; set; }
        public Dictionary<string, string> YoutubeCache { get; set; }
        public Queue<string> YtCacheQueue { get; set; }
        public bool ShowDownloadProgress { get; set; }
        public float DownloadProgress { get; set; }
        public string DownloadStatus { get; set; }
        public GameObject DownloadProgressUI { get; set; }
        public object DownloadLock { get; set; }
        public Queue<string> PlaylistVideoQueue { get; set; }
        public bool IsProcessingPlaylist { get; set; }
        public bool WarnedAboutCookies { get; set; }
        public HashSet<Transform> UpdatingVideoPlayers { get; set; }
        public HashSet<Transform> WillNeedUpdate { get; set; }
        public bool waitToUpdate { get; set; }

        public CustomTVState()
        {
            VideoFilePath = Path.Combine(MelonEnvironment.ModsDirectory, "TV", "video.mp4");
            VideoFiles = [];
            PassiveVideoPlayers = [];
            TVInterfaces = [];
            YoutubeCache = [];
            YtCacheQueue = new Queue<string>();
            PlaylistVideoQueue = new Queue<string>();
            DownloadLock = new object();
            Rng = new System.Random();
            UpdatingVideoPlayers = [];
            WillNeedUpdate = [];
        }
    }

}
