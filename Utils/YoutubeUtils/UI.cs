using MelonLoader;
using UnityEngine.UI;
using UnityEngine;
using System.Collections;

namespace CustomTV.Utils.YoutubeUtils
{
    class UI
    {
        private static void DontDestroyOnLoad(GameObject obj)
        {
            obj.transform.parent = null;
            UnityEngine.Object.DontDestroyOnLoad(obj);
        }

        public static IEnumerator CreateDownloadProgressUI()
        {
            if (CustomTV.customTVState.DownloadProgressUI != null)
            {
                UnityEngine.Object.Destroy(CustomTV.customTVState.DownloadProgressUI);
            }

            CustomTV.customTVState.DownloadProgressUI = new GameObject("DownloadProgressUI");
            DontDestroyOnLoad(CustomTV.customTVState.DownloadProgressUI);

            Canvas canvas = CustomTV.customTVState.DownloadProgressUI.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = CustomTV.customTVState.DownloadProgressUI.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            CustomTV.customTVState.DownloadProgressUI.AddComponent<GraphicRaycaster>();

            GameObject panel = new("Panel");
            panel.transform.SetParent(CustomTV.customTVState.DownloadProgressUI.transform, false);

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0);
            panelRect.anchorMax = new Vector2(0.5f, 0);
            panelRect.pivot = new Vector2(0.5f, 0);
            panelRect.sizeDelta = new Vector2(600, 100);
            panelRect.anchoredPosition = new Vector2(0, 150);

            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            GameObject titleObj = new("TitleText");
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

            GameObject progressBgObj = new("ProgressBarBg");
            progressBgObj.transform.SetParent(panel.transform, false);

            RectTransform progressBgRect = progressBgObj.AddComponent<RectTransform>();
            progressBgRect.anchorMin = new Vector2(0.05f, 0.5f);
            progressBgRect.anchorMax = new Vector2(0.95f, 0.5f);
            progressBgRect.pivot = new Vector2(0.5f, 0.5f);
            progressBgRect.sizeDelta = new Vector2(0, 20);
            progressBgRect.anchoredPosition = new Vector2(0, 0);

            Image progressBgImage = progressBgObj.AddComponent<Image>();
            progressBgImage.color = new Color(0.2f, 0.2f, 0.2f, 1);

            GameObject progressFillObj = new("ProgressBarFill");
            progressFillObj.transform.SetParent(progressBgObj.transform, false);

            RectTransform progressFillRect = progressFillObj.AddComponent<RectTransform>();
            progressFillRect.anchorMin = new Vector2(0, 0);
            progressFillRect.anchorMax = new Vector2(0, 1);
            progressFillRect.pivot = new Vector2(0, 0.5f);
            progressFillRect.sizeDelta = new Vector2(0, 0);

            Image progressFillImage = progressFillObj.AddComponent<Image>();
            progressFillImage.color = new Color(0.2f, 0.7f, 0.2f, 1);

            GameObject statusObj = new("StatusText");
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

        public static void UpdateDownloadProgressUI(float progress, string status)
        {
            if (CustomTV.customTVState.DownloadProgressUI == null || !CustomTV.customTVState.ShowDownloadProgress)
                return;

            try
            {
                progress = Mathf.Clamp01(progress);

                Transform panel = CustomTV.customTVState.DownloadProgressUI.transform.Find("Panel");
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

        public static void DestroyDownloadProgressUI()
        {
            if (CustomTV.customTVState.DownloadProgressUI != null)
            {
                UnityEngine.Object.Destroy(CustomTV.customTVState.DownloadProgressUI);
                CustomTV.customTVState.DownloadProgressUI = null;
            }
        }



        public static IEnumerator CreatePlaylistProgressUI(string message)
        {
            if (CustomTV.customTVState.DownloadProgressUI != null)
            {
                UnityEngine.Object.Destroy(CustomTV.customTVState.DownloadProgressUI);
            }

            CustomTV.customTVState.DownloadProgressUI = new GameObject("PlaylistProgressUI");
            DontDestroyOnLoad(CustomTV.customTVState.DownloadProgressUI);

            Canvas canvas = CustomTV.customTVState.DownloadProgressUI.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = CustomTV.customTVState.DownloadProgressUI.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            CustomTV.customTVState.DownloadProgressUI.AddComponent<GraphicRaycaster>();

            GameObject panel = new("Panel");
            panel.transform.SetParent(CustomTV.customTVState.DownloadProgressUI.transform, false);

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0);
            panelRect.anchorMax = new Vector2(0.5f, 0);
            panelRect.pivot = new Vector2(0.5f, 0);
            panelRect.sizeDelta = new Vector2(600, 100);
            panelRect.anchoredPosition = new Vector2(0, 150);

            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            GameObject statusObj = new("StatusText");
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

        public static void UpdatePlaylistProgressUI(string message)
        {
            if (CustomTV.customTVState.DownloadProgressUI == null)
                return;

            try
            {
                Transform panel = CustomTV.customTVState.DownloadProgressUI.transform.Find("Panel");
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

        public static void DestroyPlaylistProgressUI()
        {
            if (CustomTV.customTVState.DownloadProgressUI != null)
            {
                UnityEngine.Object.Destroy(CustomTV.customTVState.DownloadProgressUI);
                CustomTV.customTVState.DownloadProgressUI = null;
            }
        }
    }
}
