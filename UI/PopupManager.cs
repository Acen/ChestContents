using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace ChestContents.UI
{
    public static class PopupManager
    {
        public static void ShowSearchResultsPopup(string itemName, Vector3 chestPos, int quantity = -1)
        {
            if (Player.m_localPlayer == null) return;
            var existing = GameObject.Find("ChestContentsSearchPopup");
            if (existing != null) Object.Destroy(existing);
            var distance = Vector3.Distance(Player.m_localPlayer.transform.position, chestPos);
            var readableName = itemName;
            if (readableName.StartsWith("$")) readableName = readableName.Substring(1);
            readableName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(readableName.Replace("_", " "));
            var screenHeight = Screen.height;
            var fontSize = Mathf.Clamp(screenHeight / 24, 18, 48);
            var canvasGo = new GameObject("ChestContentsSearchPopup");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panel = panelGo.AddComponent<Image>();
            panel.color = new Color(0, 0, 0, 0.7f);
            var rect = panelGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(420, fontSize * 2 + 32);
            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(20, 0);
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(panelGo.transform, false);
            var text = textGo.AddComponent<Text>();
            text.text = readableName + (quantity > 0 ? $" x{quantity}" : "");
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(400, fontSize + 8);
            textRect.anchorMin = new Vector2(0, 1);
            textRect.anchorMax = new Vector2(0, 1);
            textRect.pivot = new Vector2(0, 1);
            textRect.anchoredPosition = new Vector2(12, -8);
            var distGo = new GameObject("Distance");
            distGo.transform.SetParent(panelGo.transform, false);
            var distText = distGo.AddComponent<Text>();
            distText.text = $"Distance: {distance:F1} m";
            distText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            distText.fontSize = fontSize - 4;
            distText.color = Color.yellow;
            distText.alignment = TextAnchor.LowerLeft;
            var distRect = distGo.GetComponent<RectTransform>();
            distRect.sizeDelta = new Vector2(400, fontSize + 8);
            distRect.anchorMin = new Vector2(0, 0);
            distRect.anchorMax = new Vector2(0, 0);
            distRect.pivot = new Vector2(0, 0);
            distRect.anchoredPosition = new Vector2(12, 8);
            Object.Destroy(canvasGo, 4f);
        }

        public static void ShowMetaPopup(string meta)
        {
            if (Player.m_localPlayer == null) return;
            var existing = GameObject.Find("ChestContentsMetaPopup");
            if (existing != null) Object.Destroy(existing);
            var screenHeight = Screen.height;
            var fontSize = Mathf.Clamp(screenHeight / 36, 14, 32);
            var metaLines = meta != null ? meta.Split('\n').Length : 1;
            var lines = meta?.Split('\n') ?? new[] { "" };
            var maxLineLength = 0;
            foreach (var l in lines) maxLineLength = Mathf.Max(maxLineLength, l.Length);
            var width = Mathf.Clamp(20 + maxLineLength * (fontSize * 0.6f), 200, 600);
            float height = fontSize * metaLines + 48;
            var canvasGo = new GameObject("ChestContentsMetaPopup");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panel = panelGo.AddComponent<Image>();
            panel.color = new Color(0, 0, 0, 0.7f);
            var rect = panelGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(20, 0);
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(panelGo.transform, false);
            var text = textGo.AddComponent<Text>();
            text.text = meta;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(width - 20, height - 8);
            textRect.anchorMin = new Vector2(0, 1);
            textRect.anchorMax = new Vector2(0, 1);
            textRect.pivot = new Vector2(0, 1);
            textRect.anchoredPosition = new Vector2(12, -8);
            Object.Destroy(canvasGo, 4f);
        }
    }
}
