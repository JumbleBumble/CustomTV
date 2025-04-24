using CustomTV.Utils.YoutubeUtils;
using Il2CppScheduleOne.EntityFramework;
using MelonLoader;
using MelonLoader.Utils;
using System.Collections;
using UnityEngine;
using UnityEngine.Video;

namespace CustomTV.Utils
{
    class Video
    {
        public static IEnumerator DelayedSetupVideoPlayer(bool isFirst = false)
        {
            yield return new WaitForSeconds(isFirst ? 7f : 1.5f);

            if (!CustomTV.customTVState.SettingUp)
            {
                MelonCoroutines.Start(SetupVideoPlayer(isFirst));
            }
        }

        public static IEnumerator HandleUpdate()
        {
            if (CustomTV.customTVState.SettingUpdate) yield break;
            CustomTV.customTVState.SettingUpdate = true;
            try
            {
                if (CustomTV.customTVState.PassiveVideoPlayers.Count == 0)
                {
                    CustomTV.customTVState.SettingUpdate = false;
                    yield break;
                }
                var vp = CustomTV.customTVState.PassiveVideoPlayers[0];
                if (vp == null)
                {
                    CustomTV.customTVState.SettingUpdate = false;
                    yield break;
                }

                if (!CustomTV.customTVState.WasPausedByTimescale && !CustomTV.customTVState.WasPaused && vp.isPlaying)
                {
                    double currentTime = vp.time;
                    double videoLength = vp.length;
                    if (currentTime > 0.1 || videoLength > 0 && (currentTime >= videoLength - 0.5 || CustomTV.customTVState.SavedPlaybackTime >= videoLength - 0.5))
                    {
                        CustomTV.customTVState.SavedPlaybackTime = currentTime;
                    }
                }

                if (Time.timeScale == 0f && vp.isPlaying)
                {
                    vp.Pause();
                    CustomTV.customTVState.WasPausedByTimescale = true;
                    for (int i = 0; i < CustomTV.customTVState.PassiveVideoPlayers.Count; i++)
                    {
                        if (i != 0)
                        {
                            CustomTV.customTVState.PassiveVideoPlayers[i].Pause();
                        }
                    }
                }
                else if (Time.timeScale > 0.1f && !CustomTV.customTVState.WasPaused && (!vp.isPlaying || CustomTV.customTVState.WasPausedByTimescale))
                {
                    vp.time = CustomTV.customTVState.SavedPlaybackTime;
                    vp.Play();
                    CustomTV.customTVState.WasPausedByTimescale = false;
                    for (int i = 0; i < CustomTV.customTVState.PassiveVideoPlayers.Count; i++)
                    {
                        if (i != 0)
                        {
                            CustomTV.customTVState.PassiveVideoPlayers[i].Play();
                        }
                    }
                }

                for (int i = 0; i < CustomTV.customTVState.TVInterfaces.Count; i++)
                {
                    var tvInterface = CustomTV.customTVState.TVInterfaces[i];
                    if (tvInterface == null) continue;
                    var timeChild = CustomTV.customTVState.TVInterfaces[i].Find("Time") ?? CustomTV.customTVState.TVInterfaces[i].Find("Model")?.Find("Name");
                    if (timeChild == null) continue;
                    bool needsUpdate = false;
                    var renderer = timeChild.GetComponent<MeshRenderer>();
                    if (renderer == null || renderer.material == null)
                    {
                        needsUpdate = true;
                    }
                    else
                    {
                        Material mat = renderer.material;
                        needsUpdate = mat.shader == null || mat.shader.name != "UI/Default" || mat.mainTexture != CustomTV.customTVState.SharedRenderTexture;
                    }

                    if (needsUpdate)
                    {
                        MelonCoroutines.Start(SetupPassiveDisplay(timeChild, i == 0));
                    }
                }
            }
            catch { }
            CustomTV.customTVState.SettingUpdate = false;

        }

        public static IEnumerator SetupVideoPlayer(bool first = false, bool forward = true, string directVideoPath = null)
        {
            if (CustomTV.customTVState.SettingUp) yield break;
            CustomTV.customTVState.SettingUp = true;

            string videoToPlay = "";

            CustomTV.customTVState.TVInterfaces.Clear();
            CustomTV.customTVState.PassiveVideoPlayers.Clear();
            CustomTV.customTVState.TVInterfaces = [.. UnityEngine.Object.FindObjectsOfType<Transform>()
               .Where(t => t.name == "TVInterface" || t.name == "MetalSign_Built(Clone)" || t.name == "WoodlSign_Built(Clone)")
               .Where(t =>
               {
                   if (t.name == "TVInterface") return true;
                   var labelledSurfaceItem = t.GetComponent<LabelledSurfaceItem>();
                   return labelledSurfaceItem != null && labelledSurfaceItem.Message?.ToLower() == "customtv";
               })];

            CustomTV.customTVState.SharedRenderTexture = new RenderTexture(1920, 1080, 0);

            if (CustomTV.customTVState.TVInterfaces.Count == 0)
            {
                CustomTV.customTVState.SettingUp = false;
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

                if (newVideoFiles.Count == 0 && CustomTV.customTVState.PlaylistVideoQueue.Count == 0)
                {
                    CustomTV.customTVState.SettingUp = false;
                    MelonLogger.Error("Could not find any videos to play (no MP4s in TV folder and playlist queue is empty).");
                    yield break;
                }

                if (CustomTV.customTVState.PlaylistVideoQueue.Count == 0)
                {
                    if (newVideoFiles.Count != CustomTV.customTVState.VideoFiles.Count)
                    {
                        CustomTV.customTVState.VideoFiles = newVideoFiles;
                        if (Config.Shuffle)
                        {
                            CustomTV.customTVState.VideoFiles.Shuffle(CustomTV.customTVState.Rng);
                        }
                        else
                        {
                            CustomTV.customTVState.VideoFiles = [.. newVideoFiles.OrderBy(f => f, new SmartEpisodeComparer())];
                        }
                    }
                }

                if (CustomTV.customTVState.PlaylistVideoQueue.Count > 0)
                {
                    lock (CustomTV.customTVState.PlaylistVideoQueue)
                    {
                        videoToPlay = CustomTV.customTVState.PlaylistVideoQueue.Dequeue();
                    }
                }
                else
                {
                    int newIndex = first ? 0 : forward ? (CustomTV.customTVState.CurrentVideoIndex + 1) % CustomTV.customTVState.VideoFiles.Count : (CustomTV.customTVState.CurrentVideoIndex - 1 + CustomTV.customTVState.VideoFiles.Count) % CustomTV.customTVState.VideoFiles.Count;
                    CustomTV.customTVState.CurrentVideoIndex = newIndex;
                    videoToPlay = CustomTV.customTVState.VideoFiles[CustomTV.customTVState.CurrentVideoIndex];
                }
            }

            CustomTV.customTVState.VideoFilePath = videoToPlay;

            for (int i = 0; i < CustomTV.customTVState.TVInterfaces.Count; i++)
            {
                bool isMaster = i == 0;
                if (CustomTV.customTVState.TVInterfaces[i] == null) continue;
                var timeChild = CustomTV.customTVState.TVInterfaces[i].Find("Time") ?? CustomTV.customTVState.TVInterfaces[i].Find("Model")?.Find("Name");
                MelonCoroutines.Start(SetupPassiveDisplay(timeChild, isMaster));
            }

            float timeoutStart = Time.time;
            while (CustomTV.customTVState.PassiveVideoPlayers.Count != CustomTV.customTVState.TVInterfaces.Count)
            {
                if (Time.time - timeoutStart > 15f)
                {
                    MelonLogger.Warning($"Timeout waiting for video players to be set up. Expected {CustomTV.customTVState.TVInterfaces.Count}, got {CustomTV.customTVState.PassiveVideoPlayers.Count}");
                    break;
                }
                yield return null;
            }
            CustomTV.customTVState.SettingUp = false;
        }

        public static IEnumerator SetupPassiveDisplay(Transform timeChild, bool makePlayer)
        {
            if (timeChild == null) yield break;

            try
            {
                for (int i = timeChild.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(timeChild.GetChild(i).gameObject);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error cleaning up child objects: {ex.Message}");
            }

            try
            {
                MeshRenderer renderer = timeChild.GetComponent<MeshRenderer>() ?? timeChild.gameObject.AddComponent<MeshRenderer>();
                MeshFilter meshFilter = timeChild.GetComponent<MeshFilter>() ?? timeChild.gameObject.AddComponent<MeshFilter>();
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

                Material cleanMat = new(unlitShader)
                {
                    mainTexture = CustomTV.customTVState.SharedRenderTexture
                };
                renderer.material = cleanMat;

                timeChild.localPosition = Vector3.zero;
                if (timeChild.name == "Name")
                {
                    timeChild.localScale = Config.SignSize;
                    timeChild.localPosition = new Vector3(0f, 0f, 0.6f);
                }
                else
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

            VideoPlayer vp;
            AudioSource audioSrc;
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
                    VideoHandlers.RemoveVideoEndHandler(vp);
                    VideoHandlers.RemoveErrorHandler(vp);
                    vp.Stop();
                }

                if (makePlayer)
                {
                    VideoHandlers.RemoveVideoEndHandler(vp);
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
                vp.isLooping = CustomTV.customTVState.VideoFiles.Count == 1 && CustomTV.customTVState.PlaylistVideoQueue.Count == 0;
                vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
                vp.SetTargetAudioSource(0, audioSrc);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error configuring audio/video components: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
                yield break;
            }

            string normalizedVideoPath = Path.GetFullPath(CustomTV.customTVState.VideoFilePath);
            MelonLogger.Msg($"Setting video player URL to: {normalizedVideoPath}");

            if (!File.Exists(normalizedVideoPath))
            {
                MelonLogger.Error($"Video file does not exist: {normalizedVideoPath}");
                yield break;
            }
            else
            {
                FileInfo fileInfo = new(normalizedVideoPath);
                MelonLogger.Msg($"Video file exists, size: {fileInfo.Length / (1024.0 * 1024.0):F2} MB");
            }

            float prepareStartTime;
            float elapsed;
            const float maxPrepareTime = 15f;
            int dotCount;
            string timeoutErrorMsg = "";

            try
            {
                vp.url = normalizedVideoPath;
                VideoHandlers.AddErrorHandler(vp);
                if (makePlayer)
                {
                    vp.renderMode = VideoRenderMode.RenderTexture;
                    vp.targetTexture = CustomTV.customTVState.SharedRenderTexture;
                }
                else
                {
                    vp.renderMode = VideoRenderMode.APIOnly;
                }
                CustomTV.customTVState.PassiveVideoPlayers.Add(vp);
                MelonLogger.Msg($"Added video player to passiveVideoPlayers collection (total: {CustomTV.customTVState.PassiveVideoPlayers.Count})");
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
                    string dots = new('.', dotCount % 4 + 1);
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
            }
            else
            {
                MelonLogger.Warning(timeoutErrorMsg);
                MelonLogger.Msg("Trying again with file:// protocol as fallback");
                CustomTV.customTVState.PassiveVideoPlayers.Remove(vp);
                UnityEngine.Object.Destroy(vp);
                yield return null;
                vp = timeChild.gameObject.AddComponent<VideoPlayer>();
                audioSrc = timeChild.GetComponent<AudioSource>() ?? timeChild.gameObject.AddComponent<AudioSource>();
                audioSrc.spatialBlend = 1.0f;
                audioSrc.minDistance = 1f;
                audioSrc.maxDistance = 10f;
                audioSrc.volume = Config.AudioVolume;
                audioSrc.rolloffMode = AudioRolloffMode.Logarithmic;
                vp.playOnAwake = false;
                vp.isLooping = CustomTV.customTVState.VideoFiles.Count == 1 && CustomTV.customTVState.PlaylistVideoQueue.Count == 0;
                vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
                vp.SetTargetAudioSource(0, audioSrc);
                if (makePlayer)
                {
                    vp.renderMode = VideoRenderMode.RenderTexture;
                    vp.targetTexture = CustomTV.customTVState.SharedRenderTexture;
                }
                else
                {
                    vp.renderMode = VideoRenderMode.APIOnly;
                }
                string fileUrl = $"file://{normalizedVideoPath.Replace('\\', '/')}";
                vp.url = fileUrl;
                VideoHandlers.AddErrorHandler(vp);
                CustomTV.customTVState.PassiveVideoPlayers.Add(vp);
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
                        string dots = new('.', dotCount % 4 + 1);
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
                vp.time = CustomTV.customTVState.SavedPlaybackTime;
                if (makePlayer && !CustomTV.customTVState.WasPaused && !CustomTV.customTVState.WasPausedByTimescale || !makePlayer)
                {
                    MelonLogger.Msg("Starting video playback");
                    vp.Play();
                }
                vp.time = CustomTV.customTVState.SavedPlaybackTime;
                if (makePlayer)
                {
                    VideoHandlers.AddVideoEndHandler(vp);
                    MelonLogger.Msg("Added video end handler to master player");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error starting video playback: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
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
    }
}
