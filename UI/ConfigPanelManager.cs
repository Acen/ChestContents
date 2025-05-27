using UnityEngine;
using UnityEngine.UI;
using BepInEx;
using ChestContents.Managers;

namespace ChestContents.UI
{
    public class ConfigPanelManager : MonoBehaviour
    {
        private GameObject panel;
        private InputField radiusInputField;
        private Toggle verticalMarkerToggle;
        private InputField markerHeightInputField;
        private bool isOpen = false;
        private GameObject previewPanel;
        private Toggle previewToggle;
        private bool previewEnabled = false;

        // Use a unique ID for the player preview effect
        private const int PlayerPreviewEffectId = -999;
        private ChestContents.Effects.ActionableEffect previewEffect;
        private Vector3? previewEffectOffset = null;

        // Store reference to the player vertical indicator
        private GameObject playerVerticalIndicator;

        private void Awake()
        {
            Debug.Log("ConfigPanelManager Awake called");
            previewEffect = new ChestContents.Effects.ActionableEffect("vfx_ExtensionConnection");
        }

        public void ShowPanel()
        {
            if (panel == null)
            {
                CreatePanel();
            }
            panel.SetActive(true);
            // Sync UI with config values
            radiusInputField.text = ChestContentsPlugin.ChestSearchRadius.Value.ToString();
            if (verticalMarkerToggle != null)
                verticalMarkerToggle.isOn = ChestContentsPlugin.EnableVerticalMarker.Value;
            if (markerHeightInputField != null)
                markerHeightInputField.text = ChestContentsPlugin.VerticalMarkerHeight.Value.ToString();
            // Always show preview panel when config panel is shown
            ShowPreviewPanel();
        }

        public void HidePanel()
        {
            if (panel != null)
                panel.SetActive(false);
            // Always hide preview panel when config panel is hidden
            HidePreviewPanel();
            // Remove the preview marker from the player when the panel is closed
            previewEffect.ClearEffectForTarget(PlayerPreviewEffectId);
            previewEffectOffset = null;
            // Destroy the player vertical indicator if it exists
            if (playerVerticalIndicator != null)
            {
                Object.Destroy(playerVerticalIndicator);
                playerVerticalIndicator = null;
            }
        }

        public void ShowPreviewPanel()
        {
            if (previewPanel == null)
            {
                CreatePreviewPanel();
            }
            previewPanel.SetActive(true);
            // Sync toggle with state
            if (previewToggle != null)
                previewToggle.isOn = previewEnabled;
        }

        public void HidePreviewPanel()
        {
            if (previewPanel != null)
            {
                previewPanel.SetActive(false);
            }
        }

        private void Update()
        {
            // Update preview marker position if enabled and player exists
            if (previewEnabled && Player.m_localPlayer != null && previewEffect != null)
            {
                // Use a static height for the ActionableEffect (ring)
                float ringEffectHeight = 1f;
                Vector3 markerPos = Player.m_localPlayer.transform.position + new Vector3(0, ringEffectHeight, 0);
                if (previewEffectOffset == null || (markerPos - previewEffectOffset.Value).sqrMagnitude > 0.01f)
                {
                    previewEffect.ShowEffectForTarget(markerPos, Quaternion.identity, PlayerPreviewEffectId);
                    previewEffectOffset = markerPos;
                }
                // Create or update the vertical indicator above the player
                if (ChestContentsPlugin.EnableVerticalMarker.Value)
                {
                    if (playerVerticalIndicator == null)
                    {
                        playerVerticalIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        playerVerticalIndicator.name = "PlayerVerticalIndicator";
                        Object.Destroy(playerVerticalIndicator.GetComponent<Collider>());
                        var shader = Shader.Find("Sprites/Default");
                        var mat = new Material(shader);
                        mat.color = new Color(1f, 0.5f, 0f, 0.5f);
                        playerVerticalIndicator.GetComponent<Renderer>().material = mat;
                    }
                    float yOffset = ChestContentsPlugin.VerticalMarkerHeight.Value;
                    playerVerticalIndicator.transform.position = Player.m_localPlayer.transform.position + new Vector3(0, yOffset / 2f, 0);
                    playerVerticalIndicator.transform.localScale = new Vector3(0.15f, yOffset / 2f, 0.15f);
                    playerVerticalIndicator.transform.rotation = Quaternion.identity;
                    playerVerticalIndicator.SetActive(true);
                }
                else if (playerVerticalIndicator != null)
                {
                    playerVerticalIndicator.SetActive(false);
                }
            }
            else if (playerVerticalIndicator != null)
            {
                playerVerticalIndicator.SetActive(false);
            }
        }

        // @used by ChestContentsPlugin to toggle the panel
        private void TogglePanel()
        {
            isOpen = !isOpen;
            Debug.Log($"Config panel set active: {isOpen}");
            if (isOpen)
            {
                ShowPanel();
            }
            else
            {
                HidePanel();
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
            AddLabelToColumn(leftCol.transform, "Enable Vertical Marker");
            AddLabelToColumn(leftCol.transform, "Vertical Marker Height:");
            AddInputToColumn(rightCol.transform);
            AddVerticalMarkerToggleToColumn(rightCol.transform);
            AddMarkerHeightInputToColumn(rightCol.transform);

            // Wire up config changes to plugin settings
            radiusInputField.onEndEdit.AddListener(val =>
            {
                if (int.TryParse(val, out int result))
                {
                    ChestContentsPlugin.ChestSearchRadius.Value = result;
                    if (previewEnabled) ApplyLivePreview();
                }
                else
                {
                    radiusInputField.text = ChestContentsPlugin.ChestSearchRadius.Value.ToString();
                }
            });
            verticalMarkerToggle.onValueChanged.AddListener(val => {
                ChestContentsPlugin.EnableVerticalMarker.Value = val;
            });
            markerHeightInputField.onEndEdit.AddListener(val => {
                if (float.TryParse(val, out float result))
                    ChestContentsPlugin.VerticalMarkerHeight.Value = result;
            });

            // Add a flexible space to push the close button to the bottom
            var spacer = new GameObject("FlexibleSpacer");
            spacer.transform.SetParent(bg.transform, false);
            var layoutElem = spacer.AddComponent<LayoutElement>();
            layoutElem.flexibleHeight = 1;

            // Add subtext below the main content
            var subtextObj = new GameObject("Subtext");
            subtextObj.transform.SetParent(bg.transform, false);
            var subtext = subtextObj.AddComponent<Text>();
            subtext.text = "Tip: Open your inventory/crafting window to change these options. (This will allow using the mouse)";
            subtext.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            subtext.fontSize = 14;
            subtext.color = new Color(1f, 1f, 1f, 0.7f);
            subtext.alignment = TextAnchor.MiddleCenter;
            var subtextRect = subtextObj.GetComponent<RectTransform>();
            subtextRect.sizeDelta = new Vector2(370, 24);
            subtextRect.anchorMin = new Vector2(0.5f, 0);
            subtextRect.anchorMax = new Vector2(0.5f, 0);
            subtextRect.pivot = new Vector2(0.5f, 0);
            subtextRect.anchoredPosition = new Vector2(0, 10);

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
        }

        private void CreatePreviewPanel()
        {
            previewPanel = new GameObject("ChestContentsPreviewPanel");
            var canvas = previewPanel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1001; // Above config panel
            previewPanel.AddComponent<CanvasScaler>();
            previewPanel.AddComponent<GraphicRaycaster>();

            var bg = new GameObject("BG");
            bg.transform.SetParent(previewPanel.transform, false);
            var img = bg.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.85f);
            var rect = bg.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300, 100);
            // Place below the config panel
            rect.anchoredPosition = new Vector2(0, -220); // Y offset below config panel

            var vLayout = bg.AddComponent<VerticalLayoutGroup>();
            vLayout.childAlignment = TextAnchor.UpperCenter;
            vLayout.spacing = 10;
            vLayout.padding = new RectOffset(15, 15, 15, 15);
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(bg.transform, false);
            var label = labelObj.AddComponent<Text>();
            label.text = "Preview Config Options";
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = 18;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            var labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(220, 24);

            var toggleObj = new GameObject("PreviewToggle");
            toggleObj.transform.SetParent(bg.transform, false);
            previewToggle = toggleObj.AddComponent<Toggle>();
            var toggleRect = toggleObj.GetComponent<RectTransform>();
            toggleRect.sizeDelta = new Vector2(32, 32);
            var toggleBgObj = new GameObject("Background");
            toggleBgObj.transform.SetParent(toggleObj.transform, false);
            var toggleBgImg = toggleBgObj.AddComponent<Image>();
            toggleBgImg.color = Color.gray;
            var toggleBgRect = toggleBgObj.GetComponent<RectTransform>();
            toggleBgRect.sizeDelta = new Vector2(20, 20);
            var checkmarkObj = new GameObject("Checkmark");
            checkmarkObj.transform.SetParent(toggleBgObj.transform, false);
            var checkmarkImg = checkmarkObj.AddComponent<Image>();
            checkmarkImg.color = Color.green;
            var checkmarkRect = checkmarkObj.GetComponent<RectTransform>();
            checkmarkRect.sizeDelta = new Vector2(16, 16);
            checkmarkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkmarkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkmarkRect.pivot = new Vector2(0.5f, 0.5f);
            checkmarkRect.anchoredPosition = Vector2.zero;
            previewToggle.targetGraphic = toggleBgImg;
            previewToggle.graphic = checkmarkImg;
            previewToggle.isOn = previewEnabled;
            previewToggle.onValueChanged.AddListener(val =>
            {
                previewEnabled = val;
                if (previewEnabled)
                {
                    ApplyLivePreview();
                }
                // Optionally: hide preview if disabled
            });
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
                    if (previewEnabled) ApplyLivePreview();
                }
                else
                {
                    radiusInputField.text = ChestContentsPlugin.ChestSearchRadius.Value.ToString();
                }
            });
        }

        // Add a new helper for the vertical marker toggle
        private void AddVerticalMarkerToggleToColumn(Transform parent)
        {
            var toggleObj = new GameObject("VerticalMarkerToggle");
            toggleObj.transform.SetParent(parent, false);
            verticalMarkerToggle = toggleObj.AddComponent<Toggle>();
            var toggleRect = toggleObj.GetComponent<RectTransform>();
            toggleRect.sizeDelta = new Vector2(32, 32);
            var layoutElem = toggleObj.AddComponent<LayoutElement>();
            layoutElem.minHeight = 32;
            layoutElem.minWidth = 32;
            layoutElem.preferredHeight = 32;
            layoutElem.preferredWidth = 32;
            var toggleBgObj = new GameObject("Background");
            toggleBgObj.transform.SetParent(toggleObj.transform, false);
            var toggleBgImg = toggleBgObj.AddComponent<Image>();
            toggleBgImg.color = Color.gray;
            var toggleBgRect = toggleBgObj.GetComponent<RectTransform>();
            toggleBgRect.sizeDelta = new Vector2(20, 20); // Make the background smaller
            var checkmarkObj = new GameObject("Checkmark");
            checkmarkObj.transform.SetParent(toggleBgObj.transform, false);
            var checkmarkImg = checkmarkObj.AddComponent<Image>();
            checkmarkImg.color = Color.green;
            var checkmarkRect = checkmarkObj.GetComponent<RectTransform>();
            checkmarkRect.sizeDelta = new Vector2(16, 16); // Make the checkmark smaller
            checkmarkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkmarkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkmarkRect.pivot = new Vector2(0.5f, 0.5f);
            checkmarkRect.anchoredPosition = Vector2.zero;
            verticalMarkerToggle.targetGraphic = toggleBgImg;
            verticalMarkerToggle.graphic = checkmarkImg;
            verticalMarkerToggle.isOn = ChestContentsPlugin.EnableVerticalMarker.Value;
            verticalMarkerToggle.onValueChanged.AddListener(val =>
            {
                ChestContentsPlugin.EnableVerticalMarker.Value = val;
            });
        }

        // Add a new helper for the marker height input
        private void AddMarkerHeightInputToColumn(Transform parent)
        {
            var inputObj = new GameObject("MarkerHeightInputField");
            inputObj.transform.SetParent(parent, false);
            var inputBg = inputObj.AddComponent<Image>();
            inputBg.color = Color.white;
            markerHeightInputField = inputObj.AddComponent<InputField>();
            markerHeightInputField.targetGraphic = inputBg;
            var inputRect = inputObj.GetComponent<RectTransform>();
            inputRect.sizeDelta = new Vector2(0, 32);
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
            textRect.SetAsLastSibling();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.offsetMin = new Vector2(4, 0);
            textRect.offsetMax = new Vector2(-4, 0);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            // Placeholder
            var placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(inputObj.transform, false);
            var placeholder = placeholderObj.AddComponent<Text>();
            placeholder.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            placeholder.fontSize = 18;
            placeholder.color = new Color(0.5f, 0.5f, 0.5f, 0.75f);
            placeholder.text = "Height (m)";
            placeholder.alignment = TextAnchor.MiddleLeft;
            var placeholderRect = placeholderObj.GetComponent<RectTransform>();
            placeholderRect.SetAsLastSibling();
            placeholderRect.anchorMin = new Vector2(0, 0);
            placeholderRect.anchorMax = new Vector2(1, 1);
            placeholderRect.offsetMin = new Vector2(4, 0);
            placeholderRect.offsetMax = new Vector2(-4, 0);
            placeholderRect.pivot = new Vector2(0.5f, 0.5f);
            markerHeightInputField.textComponent = text;
            markerHeightInputField.placeholder = placeholder;
            markerHeightInputField.onEndEdit.AddListener(val =>
            {
                if (float.TryParse(val, out float result))
                {
                    ChestContentsPlugin.VerticalMarkerHeight.Value = result;
                }
                else
                {
                    markerHeightInputField.text = ChestContentsPlugin.VerticalMarkerHeight.Value.ToString();
                }
            });
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
                closeBtn.onClick.AddListener(() => HidePanel());
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Exception in CreateCloseButton: {ex}");
            }
        }

        private void ApplyLivePreview()
        {
            // Remove any existing marker effect from player
            previewEffect.ClearEffectForTarget(PlayerPreviewEffectId);
            previewEffectOffset = null;
            // Destroy the player vertical indicator if it exists
            if (playerVerticalIndicator != null)
            {
                Object.Destroy(playerVerticalIndicator);
                playerVerticalIndicator = null;
            }
            if (!previewEnabled) return;
            // Only show marker if local player exists
            if (Player.m_localPlayer != null)
            {
                float markerHeight = ChestContentsPlugin.VerticalMarkerHeight.Value;
                // The vertical indicator should be centered on the player, and the ring effect should be at the top of the vertical indicator
                Vector3 playerPos = Player.m_localPlayer.transform.position;
                if (ChestContentsPlugin.EnableVerticalMarker.Value)
                {
                    playerVerticalIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    playerVerticalIndicator.name = "PlayerVerticalIndicator";
                    Object.Destroy(playerVerticalIndicator.GetComponent<Collider>());
                    var shader = Shader.Find("Sprites/Default");
                    var mat = new Material(shader);
                    mat.color = new Color(1f, 0.5f, 0f, 0.5f);
                    playerVerticalIndicator.GetComponent<Renderer>().material = mat;
                    playerVerticalIndicator.transform.position = playerPos + new Vector3(0, markerHeight / 2f, 0);
                    playerVerticalIndicator.transform.localScale = new Vector3(0.15f, markerHeight / 2f, 0.15f);
                    playerVerticalIndicator.transform.rotation = Quaternion.identity;
                    playerVerticalIndicator.SetActive(true);
                }
                // 2. Place the ring effect at the top of the vertical indicator
                Vector3 ringPos = playerPos + new Vector3(0, markerHeight, 0);
                previewEffect.ShowEffectForTarget(ringPos, Quaternion.identity, PlayerPreviewEffectId);
                previewEffectOffset = ringPos;
            }
            Debug.Log($"[ChestContents] Live preview applied: Radius={ChestContentsPlugin.ChestSearchRadius.Value}, VerticalMarker={ChestContentsPlugin.EnableVerticalMarker.Value}, MarkerHeight={ChestContentsPlugin.VerticalMarkerHeight.Value}");
        }
    }
}
