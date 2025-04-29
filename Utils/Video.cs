using CustomTV.Utils.YoutubeUtils;
using MelonLoader;
using MelonLoader.Utils;
using System.Collections;
using UnityEngine;
using UnityEngine.Video;
#if MELONLOADER_IL2CPP
using Il2CppScheduleOne.EntityFramework;
#elif MELONLOADER_MONO
using ScheduleOne.EntityFramework;
#endif

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
                    else if (currentTime < 0.1 && CustomTV.customTVState.SavedPlaybackTime > 0 && CustomTV.customTVState.SavedPlaybackTime < videoLength - 0.5 && vp.time != CustomTV.customTVState.SavedPlaybackTime)
                    {
                        vp.time = CustomTV.customTVState.SavedPlaybackTime;
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

                bool needsFullUpdate = false;
                bool inactiveMaster = false;
                for (int i = 0; i < CustomTV.customTVState.TVInterfaces.Count; i++)
                {
                    var tvInterface = CustomTV.customTVState.TVInterfaces[i];
                    if (tvInterface == null) continue;
                    Transform timeChild = tvInterface.Find("Time") ?? tvInterface.Find("Model")?.Find("Name") ?? CustomTV.customTVState.PassiveVideoPlayers.Find(x => x.transform?.parent == tvInterface || x.transform?.parent?.parent == tvInterface)?.transform;
                    if (timeChild == null) continue;
                    bool needsUpdate = false;
                    MeshRenderer renderer = timeChild.GetComponent<MeshRenderer>();
                    VideoPlayer videoPlayer = timeChild.GetComponent<VideoPlayer>();
                    if (renderer == null || videoPlayer == null || renderer.material == null)
                    {
                        needsUpdate = true;
                    }
                    else
                    {
                        Material mat = renderer.material;
                        needsUpdate = mat.shader == null || mat.shader.name != "UI/Default" || mat.mainTexture != CustomTV.customTVState.SharedRenderTexture;
                        if (!timeChild.gameObject.activeInHierarchy || !videoPlayer.isPrepared) needsUpdate = false;
                        if (timeChild.gameObject.activeInHierarchy && CustomTV.customTVState.WillNeedUpdate.Contains(timeChild))
                        {
                            needsUpdate = true;
                            CustomTV.customTVState.WillNeedUpdate.Remove(timeChild);
                            if (inactiveMaster && !needsFullUpdate)
                            {
                                needsFullUpdate = true;
                            }
                        }
                        if (!timeChild.gameObject.activeInHierarchy && !CustomTV.customTVState.WillNeedUpdate.Contains(timeChild))
                        {
                            CustomTV.customTVState.WillNeedUpdate.Add(timeChild);
                            if (i == 0 && CustomTV.customTVState.TVInterfaces.Count > 1)
                            {
                                needsFullUpdate = true;
                                break;
                            }
                        }
                        else if (!timeChild.gameObject.activeInHierarchy && i == 0 && CustomTV.customTVState.TVInterfaces.Count > 1)
                        {
                            inactiveMaster = true;
                        }
                    }

                    if (!needsFullUpdate && needsUpdate && !CustomTV.customTVState.UpdatingVideoPlayers.Contains(timeChild))
                    {
                        CustomTV.customTVState.UpdatingVideoPlayers.Add(timeChild);
                        MelonCoroutines.Start(SetupPassiveDisplay(timeChild, i == 0));
                    }
                }

                if ((needsFullUpdate || CustomTV.customTVState.waitToUpdate) && !CustomTV.customTVState.SettingUp)
                {
                    CustomTV.customTVState.waitToUpdate = false;
                    MelonCoroutines.Start(SetupVideoPlayer(false, false, CustomTV.customTVState.VideoFilePath, true));
                }
                else if (needsFullUpdate && CustomTV.customTVState.SettingUp)
                {
                    CustomTV.customTVState.waitToUpdate = true;
                }
            }
            catch { }
            CustomTV.customTVState.SettingUpdate = false;

        }

        public static IEnumerator SetupVideoPlayer(bool first = false, bool forward = true, string directVideoPath = null, bool reset = false)
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
                    return labelledSurfaceItem != null && (labelledSurfaceItem.Message?.ToLower().Contains("customtv") ?? false);
                })
                .OrderByDescending(t =>
                {
                    if (t.name == "TVInterface") return 2;
                    bool? modelActive = t.Find("Model")?.gameObject.activeInHierarchy;
                    return (modelActive ?? false) ? 1 : 0;
                })
            ];

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
                    if (newVideoFiles.Count != CustomTV.customTVState.VideoFiles.Count && !reset)
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

                if (!reset)
                {
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
                else
                {
                    videoToPlay = CustomTV.customTVState.VideoFiles[CustomTV.customTVState.CurrentVideoIndex];
                }
            }

            CustomTV.customTVState.VideoFilePath = videoToPlay;

            if (CustomTV.customTVState.TVInterfaces.Count > 1 && CustomTV.customTVState.TVInterfaces[0])
            {
                Transform master = CustomTV.customTVState.TVInterfaces[0];
                Transform timeChild = CustomTV.customTVState.TVInterfaces[0].Find("Time") ?? CustomTV.customTVState.TVInterfaces[0].Find("Model")?.Find("Name");
                if (timeChild == null || !timeChild.gameObject.activeInHierarchy)
                {
                    CustomTV.customTVState.TVInterfaces.RemoveAt(0);
                    CustomTV.customTVState.TVInterfaces.Add(master);
                    MelonLogger.Msg("Swapped Master in failsafe");
                }
            }
            int activeCount = 0;
            for (int i = 0; i < CustomTV.customTVState.TVInterfaces.Count; i++)
            {
                bool isMaster = i == 0;
                if (CustomTV.customTVState.TVInterfaces[i] == null) continue;
                Transform timeChild = CustomTV.customTVState.TVInterfaces[i].Find("Time") ?? CustomTV.customTVState.TVInterfaces[i].Find("Model")?.Find("Name");
                if (timeChild == null || (!isMaster && !timeChild.gameObject.activeInHierarchy)) continue;
                activeCount++;
                CustomTV.customTVState.UpdatingVideoPlayers.Add(timeChild);
                MelonCoroutines.Start(SetupPassiveDisplay(timeChild, isMaster));
            }

            float timeoutStart = Time.time;
            while (CustomTV.customTVState.PassiveVideoPlayers.Count != activeCount)
            {
                if (Time.time - timeoutStart > 15f)
                {
                    MelonLogger.Warning($"Timeout waiting for video players to be set up. Expected {activeCount}, got {CustomTV.customTVState.PassiveVideoPlayers.Count}");
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
                if (vp)
                {
                    CustomTV.customTVState.PassiveVideoPlayers.Remove(vp);
                    UnityEngine.Object.Destroy(vp);
                }
                MelonLogger.Msg("Creating new VideoPlayer component");
                vp = timeChild.gameObject.AddComponent<VideoPlayer>();


                audioSrc = timeChild.GetComponent<AudioSource>();
                if (audioSrc)
                {
                    UnityEngine.Object.Destroy(audioSrc);
                }

                MelonLogger.Msg("Creating new AudioSource component");
                audioSrc = timeChild.gameObject.AddComponent<AudioSource>();
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
                    CustomTV.customTVState.UpdatingVideoPlayers.Remove(timeChild);
                    yield break;
                }
            }
            vp.Pause();
            yield return new WaitForSeconds(0.5f);
            try
            {
                vp.time = CustomTV.customTVState.SavedPlaybackTime;
                if (!CustomTV.customTVState.WasPaused && !CustomTV.customTVState.WasPausedByTimescale)
                {
                    MelonLogger.Msg("Starting video playback");
                    vp.Play();
                }
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
            yield return new WaitForSeconds(0.6f);
            vp.Pause();
            try
            {
                vp.time = CustomTV.customTVState.SavedPlaybackTime;
                if (!CustomTV.customTVState.WasPaused && !CustomTV.customTVState.WasPausedByTimescale)
                {
                    MelonLogger.Msg("Starting video playback");
                    vp.Play();
                }
                vp.time = CustomTV.customTVState.SavedPlaybackTime;
            }
            catch { }
            CustomTV.customTVState.UpdatingVideoPlayers.Remove(timeChild);
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
