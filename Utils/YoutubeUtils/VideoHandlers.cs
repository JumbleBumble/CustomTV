using MelonLoader;
using UnityEngine.Video;

namespace CustomTV.Utils.YoutubeUtils
{
    class VideoHandlers
    {
        public static VideoPlayer.EventHandler videoEndDelegate;
        public static VideoPlayer.ErrorEventHandler errorEventHandler;

        public static void AddVideoEndHandler(VideoPlayer vp)
        {
            if (vp == null) return;

            try
            {
#if MELONLOADER_IL2CPP
                vp.loopPointReached = videoEndDelegate;
#else
                vp.loopPointReached += videoEndDelegate;
#endif
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to add video end handler: {ex.Message}");
            }
        }

        public static void RemoveVideoEndHandler(VideoPlayer vp)
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

        public static void AddErrorHandler(VideoPlayer vp)
        {
            if (vp == null) return;

            try
            {
#if MELONLOADER_IL2CPP
                vp.errorReceived = errorEventHandler;
#else
                vp.errorReceived += errorEventHandler;
#endif
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to add error handler: {ex.Message}");
            }
        }

        public static void RemoveErrorHandler(VideoPlayer vp)
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

        public static void VideoEndEventHandler(VideoPlayer source)
        {
            OnVideoEnd(source);
        }

        public static void OnVideoEnd(VideoPlayer vp)
        {
            MelonLogger.Msg("Video ended, checking for next video");

            VideoHandlers.RemoveVideoEndHandler(vp);

            if (CustomTV.customTVState.PlaylistVideoQueue.Count > 0)
            {
                MelonLogger.Msg($"Found {CustomTV.customTVState.PlaylistVideoQueue.Count} videos in playlist queue, starting next one");
                MelonCoroutines.Start(Playlist.StartPlaylistFromLocalFiles());
                return;
            }

            bool wasYoutubePlaylist = CustomTV.customTVState.IsYoutubeMode && CustomTV.customTVState.VideoFilePath.StartsWith(Config.YoutubeTempFolder);

            if (wasYoutubePlaylist)
            {
                MelonLogger.Msg("Playlist finished - stopping playback");
                CustomTV.customTVState.VideoFiles.RemoveAll(x => x.StartsWith(Config.YoutubeTempFolder));

                CustomTV.customTVState.IsYoutubeMode = false;

                MelonLogger.Msg("End of playlist - video will not auto-repeat");
                return;
            }

            if (CustomTV.customTVState.VideoFiles.Count <= 1)
            {
                VideoHandlers.AddVideoEndHandler(vp);
                MelonLogger.Msg("Single video will loop - added event handler back");
                return;
            }

            MelonLogger.Msg("No playlist videos, proceeding with regular random playback");
            int newIndex;
            do
            {
                newIndex = CustomTV.customTVState.Rng.Next(CustomTV.customTVState.VideoFiles.Count);
            } while (CustomTV.customTVState.VideoFiles.Count > 1 && newIndex == CustomTV.customTVState.CurrentVideoIndex);

            CustomTV.customTVState.CurrentVideoIndex = newIndex;
            CustomTV.customTVState.VideoFilePath = CustomTV.customTVState.VideoFiles[CustomTV.customTVState.CurrentVideoIndex];
            if (CustomTV.customTVState.SavedPlaybackTime > 0.2) CustomTV.customTVState.SavedPlaybackTime = 0;
            MelonCoroutines.Start(Video.SetupVideoPlayer());
        }

        public static void OnErrorReceived(VideoPlayer source, string message)
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
                else if (source == CustomTV.customTVState.PassiveVideoPlayers[0])
                {
                    MelonCoroutines.Start(Video.SetupVideoPlayer());
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in error handler: {ex.Message}");
            }
        }

    }
}
