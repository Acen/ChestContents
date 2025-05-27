using UnityEngine;
using UnityEngine.UI;
using BepInEx;
using ChestContents.Managers;

namespace ChestContents.UI
{
    public class ConfigPanelManager : MonoBehaviour
    {
        private GameObject panel;
        private Toggle highlightToggle;
        private InputField radiusInputField;
        private bool isOpen = false;

        private void Awake()
        {
            Debug.Log("ConfigPanelManager Awake called");
            // Do not create the panel here!
        }

        public void ShowPanel()
        {
            if (panel == null)
            {
                CreatePanel();
            }
            panel.SetActive(true);
            // Sync UI with config values
            highlightToggle.isOn = ChestContentsPlugin.EnableChestHighlighting.Value;
            radiusInputField.text = ChestContentsPlugin.ChestSearchRadius.Value.ToString();
        }

        private void Update()
        {
            // No hotkey logic here anymore
        }

        // @used by ChestContentsPlugin to toggle the panel
        private void TogglePanel()
        {
            isOpen = !isOpen;
            Debug.Log($"Config panel set active: {isOpen}");
            panel.SetActive(isOpen);
            if (isOpen)
            {
                // Sync UI with config values
                highlightToggle.isOn = ChestContentsPlugin.EnableChestHighlighting.Value;
                radiusInputField.text = ChestContentsPlugin.ChestSearchRadius.Value.ToString();
            }
        }

        private void CreatePanel()
        {
            // Ensure EventSystem exists
            if (GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                DontDestroyOnLoad(es);
            }

            panel = new GameObject("ChestContentsConfigPanel");
            var canvas = panel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000; // Ensure it's above other UI
            panel.AddComponent<CanvasScaler>();
            panel.AddComponent<GraphicRaycaster>();

            var bg = new GameObject("BG");
            bg.transform.SetParent(panel.transform, false);
            var img = bg.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.85f);
            var rect = bg.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400, 200); // Increased height for close button
            rect.anchoredPosition = new Vector2(0, 0);

            // Move BG above border
            bg.transform.SetAsLastSibling();

            // Use VerticalLayoutGroup for vertical stacking
            var vLayout = bg.AddComponent<VerticalLayoutGroup>();
            vLayout.childAlignment = TextAnchor.UpperLeft;
            vLayout.spacing = 10;
            vLayout.padding = new RectOffset(15, 15, 15, 15);
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            // Main content container for options
            var contentContainer = new GameObject("ContentContainer");
            contentContainer.transform.SetParent(bg.transform, false);
            var contentHLayout = contentContainer.AddComponent<HorizontalLayoutGroup>();
            contentHLayout.childAlignment = TextAnchor.UpperLeft;
            contentHLayout.spacing = 10;
            contentHLayout.childForceExpandWidth = true;
            contentHLayout.childForceExpandHeight = false;
            var contentRect = contentContainer.GetComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(370, 100);

            // Left: labels, Right: controls
            var leftCol = new GameObject("LeftCol");
            leftCol.transform.SetParent(contentContainer.transform, false);
            var leftVLayout = leftCol.AddComponent<VerticalLayoutGroup>();
            leftVLayout.childAlignment = TextAnchor.UpperLeft;
            leftVLayout.spacing = 18;
            leftVLayout.childForceExpandWidth = true;
            leftVLayout.childForceExpandHeight = false;
            leftVLayout.padding = new RectOffset(0, 10, 0, 0); // Add right padding to leftCol
            var leftRect = leftCol.GetComponent<RectTransform>();
            leftRect.sizeDelta = new Vector2(220, 100);

            var rightCol = new GameObject("RightCol");
            rightCol.transform.SetParent(contentContainer.transform, false);
            var rightVLayout = rightCol.AddComponent<VerticalLayoutGroup>();
            rightVLayout.childAlignment = TextAnchor.UpperRight; // Ensure right alignment
            rightVLayout.spacing = 18;
            rightVLayout.childForceExpandWidth = false; // Prevent controls from stretching
            rightVLayout.childForceExpandHeight = false;
            rightVLayout.padding = new RectOffset(10, 0, 0, 0); // Add left padding to rightCol
            var rightRect = rightCol.GetComponent<RectTransform>();
            rightRect.sizeDelta = new Vector2(120, 100);

            // Add vertical divider between columns
            var dividerObj = new GameObject("VerticalDivider");
            dividerObj.transform.SetParent(contentContainer.transform, false);
            var dividerImg = dividerObj.AddComponent<Image>();
            dividerImg.color = new Color(1, 1, 1, 0.3f); // Light vertical divider
            var dividerRect = dividerObj.GetComponent<RectTransform>();
            dividerRect.sizeDelta = new Vector2(2, 1000); // Tall enough for panel
            dividerRect.anchorMin = new Vector2(0.5f, 0);
            dividerRect.anchorMax = new Vector2(0.5f, 1);
            dividerRect.anchoredPosition = Vector2.zero;

            // Add labels to left, controls to right
            AddLabelToColumn(leftCol.transform, "Chest Search Radius (m):");
            AddLabelToColumn(leftCol.transform, "Enable Chest Highlighting");
            AddInputToColumn(rightCol.transform);
            AddToggleToColumn(rightCol.transform);

            // Add a flexible space to push the close button to the bottom
            var spacer = new GameObject("FlexibleSpacer");
            spacer.transform.SetParent(bg.transform, false);
            var layoutElem = spacer.AddComponent<LayoutElement>();
            layoutElem.flexibleHeight = 1;

            // Add a container for the close button below, centered
            var closeContainer = new GameObject("CloseContainer");
            closeContainer.transform.SetParent(bg.transform, false);
            var closeLayout = closeContainer.AddComponent<HorizontalLayoutGroup>();
            closeLayout.childAlignment = TextAnchor.MiddleCenter;
            closeLayout.childForceExpandWidth = true;
            closeLayout.childForceExpandHeight = false;
            closeLayout.spacing = 0;
            var closeRect = closeContainer.GetComponent<RectTransform>();
            closeRect.sizeDelta = new Vector2(400, 40);

            CreateCloseButton(closeContainer.transform);

            // Removed border creation for a cleaner look
            // float borderThickness = 4f;
            // float panelWidth = 400f;
            // float panelHeight = 200f;
            // // Top border
            // var borderTop = new GameObject("BorderTop");
            // borderTop.transform.SetParent(bg.transform, false);
            // var borderTopImg = borderTop.AddComponent<Image>();
            // borderTopImg.color = Color.white;
            // var borderTopRect = borderTop.GetComponent<RectTransform>();
            // borderTopRect.anchorMin = new Vector2(0, 1);
            // borderTopRect.anchorMax = new Vector2(1, 1);
            // borderTopRect.pivot = new Vector2(0.5f, 1);
            // borderTopRect.sizeDelta = new Vector2(0, borderThickness);
            // borderTopRect.anchoredPosition = new Vector2(0, borderThickness / 2);
            // // Bottom border
            // var borderBottom = new GameObject("BorderBottom");
            // borderBottom.transform.SetParent(bg.transform, false);
            // var borderBottomImg = borderBottom.AddComponent<Image>();
            // borderBottomImg.color = Color.white;
            // var borderBottomRect = borderBottom.GetComponent<RectTransform>();
            // borderBottomRect.anchorMin = new Vector2(0, 0);
            // borderBottomRect.anchorMax = new Vector2(1, 0);
            // borderBottomRect.pivot = new Vector2(0.5f, 0);
            // borderBottomRect.sizeDelta = new Vector2(0, borderThickness);
            // borderBottomRect.anchoredPosition = new Vector2(0, -borderThickness / 2);
            // // Left border
            // var borderLeft = new GameObject("BorderLeft");
            // borderLeft.transform.SetParent(bg.transform, false);
            // var borderLeftImg = borderLeft.AddComponent<Image>();
            // borderLeftImg.color = Color.white;
            // var borderLeftRect = borderLeft.GetComponent<RectTransform>();
            // borderLeftRect.anchorMin = new Vector2(0, 0);
            // borderLeftRect.anchorMax = new Vector2(0, 1);
            // borderLeftRect.pivot = new Vector2(0, 0.5f);
            // borderLeftRect.sizeDelta = new Vector2(borderThickness, 0);
            // borderLeftRect.anchoredPosition = new Vector2(-borderThickness / 2, 0);
            // // Right border
            // var borderRight = new GameObject("BorderRight");
            // borderRight.transform.SetParent(bg.transform, false);
            // var borderRightImg = borderRight.AddComponent<Image>();
            // borderRightImg.color = Color.white;
            // var borderRightRect = borderRight.GetComponent<RectTransform>();
            // borderRightRect.anchorMin = new Vector2(1, 0);
            // borderRightRect.anchorMax = new Vector2(1, 1);
            // borderRightRect.pivot = new Vector2(1, 0.5f);
            // borderRightRect.sizeDelta = new Vector2(borderThickness, 0);
            // borderRightRect.anchoredPosition = new Vector2(borderThickness / 2, 0);
        }

        // Helper to add a label to a column
        private void AddLabelToColumn(Transform parent, string textValue)
        {
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(parent, false);
            var label = labelObj.AddComponent<Text>();
            label.text = textValue;
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = 18;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            var labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(220, 24);
        }

        // Helper to add the input field to the right column
        private void AddInputToColumn(Transform parent)
        {
            var inputObj = new GameObject("RadiusInputField");
            inputObj.transform.SetParent(parent, false);
            var inputBg = inputObj.AddComponent<Image>();
            inputBg.color = Color.white;
            radiusInputField = inputObj.AddComponent<InputField>();
            radiusInputField.targetGraphic = inputBg;
            var inputRect = inputObj.GetComponent<RectTransform>();
            inputRect.sizeDelta = new Vector2(0, 32); // Match checkbox height
            inputRect.anchorMin = new Vector2(0, 0);
            inputRect.anchorMax = new Vector2(1, 1);
            inputRect.pivot = new Vector2(1, 0.5f);
            inputRect.anchoredPosition = Vector2.zero;
            var layoutElem = inputObj.AddComponent<LayoutElement>();
            layoutElem.minWidth = 100;
            layoutElem.flexibleWidth = 1;
            // Text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(inputObj.transform, false);
            var text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 18;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleLeft;
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.SetAsLastSibling(); // Ensure text is rendered above background
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.offsetMin = new Vector2(4, 0); // Small left padding only
            textRect.offsetMax = new Vector2(-4, 0);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            // Placeholder
            var placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(inputObj.transform, false);
            var placeholder = placeholderObj.AddComponent<Text>();
            placeholder.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            placeholder.fontSize = 18;
            placeholder.color = new Color(0.5f, 0.5f, 0.5f, 0.75f);
            placeholder.text = "Radius (m)";
            placeholder.alignment = TextAnchor.MiddleLeft;
            var placeholderRect = placeholderObj.GetComponent<RectTransform>();
            placeholderRect.SetAsLastSibling(); // Ensure placeholder is above background
            placeholderRect.anchorMin = new Vector2(0, 0);
            placeholderRect.anchorMax = new Vector2(1, 1);
            placeholderRect.offsetMin = new Vector2(4, 0);
            placeholderRect.offsetMax = new Vector2(-4, 0);
            placeholderRect.pivot = new Vector2(0.5f, 0.5f);
            radiusInputField.textComponent = text;
            radiusInputField.placeholder = placeholder;
            radiusInputField.onEndEdit.AddListener(val =>
            {
                if (int.TryParse(val, out int result))
                {
                    ChestContentsPlugin.ChestSearchRadius.Value = result;
                }
                else
                {
                    radiusInputField.text = ChestContentsPlugin.ChestSearchRadius.Value.ToString();
                }
            });
        }

        // Helper to add the toggle to the right column
        private void AddToggleToColumn(Transform parent)
        {
            var toggleObj = new GameObject("HighlightToggle");
            toggleObj.transform.SetParent(parent, false);
            highlightToggle = toggleObj.AddComponent<Toggle>();
            var toggleRect = toggleObj.GetComponent<RectTransform>();
            toggleRect.sizeDelta = new Vector2(32, 32); // Larger checkbox
            toggleRect.anchorMin = new Vector2(0.5f, 0.5f);
            toggleRect.anchorMax = new Vector2(0.5f, 0.5f);
            toggleRect.pivot = new Vector2(0.5f, 0.5f);
            toggleRect.anchoredPosition = Vector2.zero;
            var layoutElem = toggleObj.AddComponent<LayoutElement>();
            layoutElem.minHeight = 32;
            layoutElem.minWidth = 32;
            layoutElem.preferredHeight = 32;
            layoutElem.preferredWidth = 32;
            // Center align in parent
            var toggleBgObj = new GameObject("Background");
            toggleBgObj.transform.SetParent(toggleObj.transform, false);
            var toggleBgImg = toggleBgObj.AddComponent<Image>();
            toggleBgImg.color = Color.gray;
            var toggleBgRect = toggleBgObj.GetComponent<RectTransform>();
            toggleBgRect.sizeDelta = new Vector2(32, 32); // Larger background
            toggleBgRect.anchorMin = new Vector2(0.5f, 0.5f);
            toggleBgRect.anchorMax = new Vector2(0.5f, 0.5f);
            toggleBgRect.pivot = new Vector2(0.5f, 0.5f);
            toggleBgRect.anchoredPosition = Vector2.zero;
            var checkmarkObj = new GameObject("Checkmark");
            checkmarkObj.transform.SetParent(toggleBgObj.transform, false);
            var checkmarkImg = checkmarkObj.AddComponent<Image>();
            checkmarkImg.color = Color.green;
            var checkmarkRect = checkmarkObj.GetComponent<RectTransform>();
            checkmarkRect.sizeDelta = new Vector2(28, 28); // Larger checkmark
            checkmarkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkmarkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkmarkRect.pivot = new Vector2(0.5f, 0.5f);
            checkmarkRect.anchoredPosition = Vector2.zero;
            highlightToggle.targetGraphic = toggleBgImg;
            highlightToggle.graphic = checkmarkImg;
            highlightToggle.isOn = ChestContentsPlugin.EnableChestHighlighting?.Value ?? true;
            highlightToggle.onValueChanged.AddListener(val => ChestContentsPlugin.EnableChestHighlighting.Value = val);
        }

        private void CreateCloseButton(Transform parent)
        {
            Debug.Log("Creating Close Button...");
            try
            {
                var closeObj = new GameObject("CloseButton");
                closeObj.transform.SetParent(parent, false);
                var closeImg = closeObj.AddComponent<Image>();
                closeImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
                var closeBtn = closeObj.AddComponent<Button>();
                closeBtn.targetGraphic = closeImg;
                var closeRect = closeObj.GetComponent<RectTransform>();
                closeRect.sizeDelta = new Vector2(120, 36);
                closeRect.anchorMin = new Vector2(0.5f, 0.5f);
                closeRect.anchorMax = new Vector2(0.5f, 0.5f);
                closeRect.pivot = new Vector2(0.5f, 0.5f);
                closeRect.anchoredPosition = Vector2.zero;
                var closeTextObj = new GameObject("Text");
                closeTextObj.transform.SetParent(closeObj.transform, false);
                var closeText = closeTextObj.AddComponent<Text>();
                closeText.text = "Close";
                closeText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                closeText.fontSize = 18;
                closeText.color = Color.white;
                closeText.alignment = TextAnchor.MiddleCenter;
                var closeTextRect = closeTextObj.GetComponent<RectTransform>();
                if (closeTextRect == null)
                {
                    Debug.LogError("closeTextRect is null!");
                }
                else
                {
                    closeTextRect.sizeDelta = new Vector2(120, 36);
                    closeTextRect.anchoredPosition = Vector2.zero;
                }
                closeBtn.onClick.AddListener(() => panel.SetActive(false));
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Exception in CreateCloseButton: {ex}");
            }
        }
    }
}
