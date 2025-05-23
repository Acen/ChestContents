using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable Unity.PerformanceCriticalCodeInvocation

namespace ChestContents
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Main : BaseUnityPlugin
    {
        private const string PluginGuid = "sticky.chestcontents";
        private const string PluginName = "ChestContents";
        private const string PluginVersion = "1.0.0";
        public static new ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource(PluginName);

        private static readonly List<Container> Chests = new List<Container>();
        private static IndicatedChestList _indicatedList;
        public static readonly Dictionary<int, ChestInfo> ChestInfoDict = new Dictionary<int, ChestInfo>();
        public static readonly Dictionary<string, List<ItemLocationInfo>> ItemNameIndex = new Dictionary<string, List<ItemLocationInfo>>();
        private static readonly Collider[] Colliders = new Collider[10240];
        private readonly Harmony _harmonyInstance = new Harmony(PluginGuid);
        private CustomStatusEffect _chestIndexEffect;
        private int _lastChestCount = -1;

        public static IndicatedChestList IndicatedList => _indicatedList;

        private void Awake()
        {
            // No need to register vanilla VFX prefab; just fetch from ZNetScene at effect time
            _harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
            AddStatusEffect();
            CommandManager.Instance.AddConsoleCommand(new SearchChestsCommand());
            CommandManager.Instance.AddConsoleCommand(new SearchChestsCommand("cs"));
            CommandManager.Instance.AddConsoleCommand(new SearchChestsCommand("sc"));
        }

        private void Update()
        {
            if (Player.m_localPlayer == null) return;
            if (_indicatedList == null) _indicatedList = new IndicatedChestList();

            PopulateContainers();
            ParseChests();
            _indicatedList.RunEffects();

            var chestIndex = _chestIndexEffect.StatusEffect;
            int currentChestCount = ChestInfoDict.Count;
            if (chestIndex is SeChestIndex) chestIndex.m_tooltip = currentChestCount.ToString();

            if (_lastChestCount != currentChestCount)
            {
                var seMan = Player.m_localPlayer.GetSEMan();
                if (seMan.GetStatusEffect(_chestIndexEffect.StatusEffect.NameHash()))
                {
                    seMan.RemoveStatusEffect(_chestIndexEffect.StatusEffect);
                }
                seMan.AddStatusEffect(_chestIndexEffect.StatusEffect, true);
                _lastChestCount = currentChestCount;
            }
        }

        private void PopulateContainers()
        {
            var colliderCount = Physics.OverlapSphereNonAlloc(Player.m_localPlayer.transform.position, 30f, Colliders);
            var containers = new List<Container>();
            for (var i = 0; i < colliderCount; i++)
            {
                var collider = Colliders[i];
                if (collider.transform.parent == null) continue;
                var container = collider.transform.gameObject.GetComponentInParent<Container>();
                if (container) containers.Add(container);
            }
            foreach (var container in containers)
            {
                if (container == null || container.GetInventory() == null) continue;
                var instanceID = container.GetInstanceID();
                if (Chests.Find(x => x.GetInstanceID() == instanceID) == null)
                {
                    if (CheckContainerAccess(container, Player.m_localPlayer))
                    {
                        Chests.Add(container);
                    }
                }
            }
        }

        private void ParseChests()
        {
            // Remove chests that no longer exist from Chests, ChestInfoDict, and _indicatedList
            Chests.RemoveAll(chest => chest == null || chest.transform == null || chest.GetInventory() == null);
            var validInstanceIds = new HashSet<int>(Chests.Select(c => c.GetInstanceID()));
            // Remove from ChestInfoDict
            var toRemove = ChestInfoDict.Keys.Where(id => !validInstanceIds.Contains(id)).ToList();
            foreach (var id in toRemove)
            {
                ChestInfoDict.Remove(id);
            }
            // Remove from _indicatedList
            _indicatedList?.PurgeInvalid(validInstanceIds);

            // Clear and rebuild the item name index
            ItemNameIndex.Clear();

            foreach (var chest in Chests.Where(chest => chest != null && chest.transform != null && chest.GetInventory() != null))
            {
                var ci = new ChestInfo(chest);
                if (!ChestInfoDict.ContainsKey(ci.InstanceID))
                {
                    ChestInfoDict.Add(ci.InstanceID, ci);
                }
                else if (ChestInfoDict[ci.InstanceID].LastUpdated < DateTime.Now.Subtract(TimeSpan.FromSeconds(5)))
                {
                    ChestInfoDict[ci.InstanceID] = ci;
                }
                // Index items for fuzzy search
                foreach (var item in ci.Contents)
                {
                    var itemName = item.m_shared.m_name.ToLowerInvariant();
                    if (!ItemNameIndex.TryGetValue(itemName, out var list))
                    {
                        list = new List<ItemLocationInfo>();
                        ItemNameIndex[itemName] = list;
                    }
                    list.Add(new ItemLocationInfo
                    {
                        ItemName = item.m_shared.m_name,
                        Stack = item.m_stack,
                        ChestId = ci.InstanceID,
                        Position = ci.Position
                    });
                }
            }
        }

        private static bool CheckContainerAccess(Container container, Player player)
        {
            return ContainerPatch.RunCheckAccess(container, player.GetPlayerID());
        }

        private void AddStatusEffect()
        {
            var effect = ScriptableObject.CreateInstance<SeChestIndex>();
            effect.GetIconText();
            effect.name = "ChestIndexEffect";
            effect.m_name = "Chest Contents";
            effect.m_icon = AssetUtils.LoadSpriteFromFile("ChestContents/Assets/chest.png");
            _chestIndexEffect = new CustomStatusEffect(effect, false);
            ItemManager.Instance.AddStatusEffect(_chestIndexEffect);
        }

        // Shows a popup window on the left side of the screen with the item name and distance
        public static void ShowSearchResultsPopup(string itemName, Vector3 chestPos, int quantity = -1)
        {
            if (Player.m_localPlayer == null) return;
            // Try to find existing popup and destroy it
            var existing = GameObject.Find("ChestContentsSearchPopup");
            if (existing != null)
            {
                UnityEngine.Object.Destroy(existing);
            }

            // Calculate distance
            float distance = Vector3.Distance(Player.m_localPlayer.transform.position, chestPos);
            string readableName = itemName;
            if (readableName.StartsWith("$")) readableName = readableName.Substring(1);
            readableName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(readableName.Replace("_", " "));

            int screenHeight = Screen.height;
            int fontSize = Mathf.Clamp(screenHeight / 24, 18, 48);

            var canvasGo = new GameObject("ChestContentsSearchPopup");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panel = panelGo.AddComponent<UnityEngine.UI.Image>();
            panel.color = new Color(0, 0, 0, 0.7f);
            var rect = panelGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(420, fontSize * 2 + 32);
            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(20, 0);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(panelGo.transform, false);
            var text = textGo.AddComponent<UnityEngine.UI.Text>();
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
            // Create distance text
            var distGo = new GameObject("Distance");
            distGo.transform.SetParent(panelGo.transform, false);
            var distText = distGo.AddComponent<UnityEngine.UI.Text>();
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

            UnityEngine.Object.Destroy(canvasGo, 4f);
        }

        // Shows a popup window for meta data (e.g. chest index summary)
        public static void ShowMetaPopup(string meta)
        {
            if (Player.m_localPlayer == null) return;
            var existing = GameObject.Find("ChestContentsMetaPopup");
            if (existing != null)
            {
                UnityEngine.Object.Destroy(existing);
            }
            int screenHeight = Screen.height;
            int fontSize = Mathf.Clamp(screenHeight / 36, 14, 32);
            int metaLines = meta != null ? meta.Split('\n').Length : 1;
            // Estimate width and height based on longest line and number of lines
            string[] lines = meta?.Split('\n') ?? new string[] { "" };
            int maxLineLength = lines.Max(l => l.Length);
            // Estimate width: 10px per character, min 200, max 600
            float width = Mathf.Clamp(20 + maxLineLength * (fontSize * 0.6f), 200, 600);
            // Height: fontSize * lines + padding
            float height = fontSize * metaLines + 32;
            var canvasGo = new GameObject("ChestContentsMetaPopup");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panel = panelGo.AddComponent<UnityEngine.UI.Image>();
            panel.color = new Color(0, 0, 0, 0.7f);
            var rect = panelGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(20, 0);
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(panelGo.transform, false);
            var text = textGo.AddComponent<UnityEngine.UI.Text>();
            text.text = meta;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(width - 20, height - 16);
            textRect.anchorMin = new Vector2(0, 1);
            textRect.anchorMax = new Vector2(0, 1);
            textRect.pivot = new Vector2(0, 1);
            textRect.anchoredPosition = new Vector2(12, -8);
            UnityEngine.Object.Destroy(canvasGo, 4f);
        }
    }

    [HarmonyPatch]
    public static class ContainerPatch
    {
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(Container), "CheckAccess")]
        public static bool RunCheckAccess(Container container, long playerID)
        {
            throw new NotImplementedException("This is a reverse patch, please use the original method instead.");
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
    public static class ContainerInteractPatch
    {
        public static void Postfix(Container __instance, Humanoid character, bool hold, bool alt, bool __result)
        {
            if (!__result || Main.IndicatedList == null || Main.IndicatedList.ChestList.Count == 0)
                return;
            var indicated = Main.IndicatedList.ChestList[0];
            if (__instance.GetInstanceID() == indicated.InstanceID)
            {
                Main.IndicatedList.Clear();
            }
        }
    }

    public class IndicatedChestList
    {
        public List<ChestInfo> ChestList { get; }
        private readonly HashSet<int> _chestSet;
        private readonly ActionableEffect _effect;
        private GameObject _activeConnectionVfx;
        private readonly Dictionary<int, GameObject> _verticalIndicators = new Dictionary<int, GameObject>();

        public IndicatedChestList()
        {
            ChestList = new List<ChestInfo>();
            _chestSet = new HashSet<int>();
            _effect = new ActionableEffect("vfx_ExtensionConnection");
        }

        public IndicatedChestList(List<ChestInfo> chestList, ActionableEffect effect)
        {
            ChestList = chestList;
            _chestSet = new HashSet<int>(chestList.Select(c => c.InstanceID));
            _effect = effect;
        }

        public IndicatedChestList(List<ChestInfo> chestList)
        {
            ChestList = chestList;
            _chestSet = new HashSet<int>(chestList.Select(c => c.InstanceID));
            _effect = new ActionableEffect("vfx_ExtensionConnection");
        }

        public void Add(ChestInfo chest, bool unique = true)
        {
            if (unique)
            {
                if (_chestSet.Add(chest.InstanceID))
                {
                    ChestList.Add(chest);
                }
            }
            else
            {
                ChestList.Add(chest);
                _chestSet.Add(chest.InstanceID);
            }
        }

        public void Clear()
        {
            ChestList.Clear();
            _chestSet.Clear();
            _effect.PurgeInvalid(new HashSet<int>()); // Also clear all VFX
        }

        public void PurgeInvalid(HashSet<int> validInstanceIds)
        {
            ChestList.RemoveAll(ci => !validInstanceIds.Contains(ci.InstanceID));
            _chestSet.RemoveWhere(id => !validInstanceIds.Contains(id));
            _effect.PurgeInvalid(validInstanceIds); // Clean up VFX for removed chests
        }

        public void RunEffects()
        {
            if (Game.IsPaused()) return;
            float time = (float)DateTime.Now.TimeOfDay.TotalSeconds;
            foreach (var chest in ChestList)
            {
                // Make the VFX dance in a circle around the chest
                float radius = 0.5f;
                float speed = 2f;
                Vector3 offset = new Vector3(Mathf.Cos(time * speed), 0, Mathf.Sin(time * speed)) * radius;
                Vector3 vfxPos = chest.Position + offset;
                _effect.RunEffect(vfxPos, chest.Rotation, chest.InstanceID);

                // --- Vertical Indicator ---
                if (!_verticalIndicators.ContainsKey(chest.InstanceID) || _verticalIndicators[chest.InstanceID] == null)
                {
                    var indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    indicator.name = $"ChestVerticalIndicator_{chest.InstanceID}";
                    Object.Destroy(indicator.GetComponent<Collider>()); // Remove collider
                    indicator.transform.localScale = new Vector3(0.15f, 3f, 0.15f); // Tall and thin
                    var shader = Shader.Find("Sprites/Default");
                    var mat = new Material(shader);
                    // Match color to vfx_ExtensionConnection (orange)
                    mat.color = new Color(1f, 0.5f, 0f, 0.5f); // Semi-transparent orange
                    //mat.color = new Color(0f, 1f, 1f, 1f);
                    indicator.GetComponent<Renderer>().material = mat;
                    _verticalIndicators[chest.InstanceID] = indicator;
                }
                var ind = _verticalIndicators[chest.InstanceID];
                ind.transform.position = chest.Position + new Vector3(0, 3f, 0); // Raise above chest
                ind.transform.rotation = Quaternion.identity;
                ind.SetActive(true);
            }
            // Remove indicators for chests no longer indicated
            var validIds = new HashSet<int>(ChestList.Select(c => c.InstanceID));
            var toRemove = _verticalIndicators.Keys.Where(id => !validIds.Contains(id)).ToList();
            foreach (var id in toRemove)
            {
                if (_verticalIndicators[id] != null)
                    Object.Destroy(_verticalIndicators[id]);
                _verticalIndicators.Remove(id);
            }

            // Add a LineRenderer between player and the first indicated chest, and update it every frame
            if (ChestList.Count > 0 && Player.m_localPlayer != null)
            {
                var playerPos = Player.m_localPlayer.transform.position;
                var chestPos = ChestList[0].Position;
                playerPos.y += 1.5f;
                chestPos.y += 1.5f;

                if (_activeConnectionVfx == null)
                {
                    _activeConnectionVfx = new GameObject("ChestConnectionLine");
                    var line = _activeConnectionVfx.AddComponent<LineRenderer>();
                    line.material = new Material(Shader.Find("Sprites/Default"));
                    line.widthMultiplier = 0.1f;
                    line.positionCount = 2;
                    line.useWorldSpace = true;
                    line.startColor = Color.cyan;
                    line.endColor = Color.yellow;
                }
                if (_activeConnectionVfx != null)
                {
                    var line = _activeConnectionVfx.GetComponent<LineRenderer>();
                    if (line != null)
                    {
                        line.SetPosition(0, playerPos);
                        line.SetPosition(1, chestPos);
                    }
                }
            }
            else if (_activeConnectionVfx != null)
            {
                Object.Destroy(_activeConnectionVfx);
                _activeConnectionVfx = null;
            }
        }
    }

    public class ActionableEffect
    {
        private readonly string _prefabName;
        private readonly Dictionary<int, List<GameObject>> _activeEffects = new Dictionary<int, List<GameObject>>();
        private const int EffectsPerChest = 12;

        private class EffectInstance
        {
            public GameObject Obj;
            public Vector3 Offset;
        }
        private readonly Dictionary<int, List<EffectInstance>> _activeEffectInstances = new Dictionary<int, List<EffectInstance>>();

        public ActionableEffect(string prefabName)
        {
            _prefabName = prefabName;
        }

        public void RunEffect(Vector3 position, Quaternion rotation, int chestInstanceID)
        {
            position.y += 1.5f;
            // Star pattern: 12 VFX evenly spaced in a circle, all radiating from the center
            int starCount = 12;
            float starRadius = 0.8f;
            Vector3[] starOffsets = new Vector3[starCount];
            for (int i = 0; i < starCount; i++)
            {
                float angle = i * Mathf.PI * 2f / starCount;
                starOffsets[i] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * starRadius;
            }
            int effectsToUse = Mathf.Min(EffectsPerChest, starOffsets.Length);
            if (_activeEffectInstances.TryGetValue(chestInstanceID, out var effectInstances) && effectInstances != null && effectInstances.Count == effectsToUse)
            {
                for (int i = 0; i < effectsToUse; i++)
                {
                    var inst = effectInstances[i];
                    Vector3 offset = starOffsets[i];
                    // Rotate each VFX to face outward from the center
                    Quaternion vfxRotation = Quaternion.LookRotation(offset.normalized, Vector3.up);
                    inst.Obj.transform.position = position + offset;
                    inst.Obj.transform.rotation = vfxRotation;
                }
                return;
            }
            if (effectInstances != null)
            {
                foreach (var inst in effectInstances)
                {
                    if (inst.Obj != null) Object.Destroy(inst.Obj);
                }
                _activeEffectInstances.Remove(chestInstanceID);
            }
            GameObject prefab = null;
            if (ZNetScene.instance != null)
            {
                prefab = ZNetScene.instance.GetPrefab(_prefabName);
                if (prefab == null)
                {
                    Main.Logger.LogWarning($"[ActionableEffect] '{_prefabName}' not found in ZNetScene. Trying PrefabManager.Cache.");
                }
            }
            else
            {
                Main.Logger.LogWarning($"[ActionableEffect] ZNetScene.instance is null at effect time. Trying PrefabManager.Cache for '{_prefabName}'.");
            }
            if (prefab == null)
            {
                prefab = PrefabManager.Cache.GetPrefab<GameObject>(_prefabName);
                if (prefab == null)
                {
                    Main.Logger.LogWarning($"[ActionableEffect] '{_prefabName}' not found in PrefabManager.Cache either. Effect will not play.");
                    return;
                }
                else
                {
                    Main.Logger.LogInfo($"[ActionableEffect] Found '{_prefabName}' in PrefabManager.Cache.");
                }
            }
            var newInstances = new List<EffectInstance>();
            for (int i = 0; i < effectsToUse; i++)
            {
                Vector3 offset = starOffsets[i];
                Quaternion vfxRotation = Quaternion.LookRotation(offset.normalized, Vector3.up);
                var obj = Object.Instantiate(prefab, position + offset, vfxRotation);
                var ps = obj.GetComponentInChildren<ParticleSystem>();
                if (ps != null)
                {
                    var psr = ps.GetComponent<ParticleSystemRenderer>();
                    if (psr != null)
                    {
                        psr.renderMode = ParticleSystemRenderMode.Billboard;
                    }
                }
                newInstances.Add(new EffectInstance { Obj = obj, Offset = offset });
                Main.Logger.LogInfo($"[ActionableEffect] Instantiated object name: {obj.name}, components: {string.Join(", ", obj.GetComponents<Component>().Select(c => c.GetType().Name))}");
            }
            _activeEffectInstances[chestInstanceID] = newInstances;
        }

        public void PurgeInvalid(HashSet<int> validInstanceIds)
        {
            var toRemove = _activeEffectInstances.Keys.Where(id => !validInstanceIds.Contains(id)).ToList();
            foreach (var id in toRemove)
            {
                foreach (var inst in _activeEffectInstances[id])
                {
                    if (inst.Obj != null) Object.Destroy(inst.Obj);
                }
                _activeEffectInstances.Remove(id);
            }
        }
    }

    public class SeChestIndex : StatusEffect
    {
        public override string GetIconText()
        {
            if (string.IsNullOrEmpty(m_tooltip)) return string.Empty;
            if (!int.TryParse(m_tooltip, out var tt)) return string.Empty;
            return tt == 1 ? $"{tt} chest" : $"{tt} chests";
        }
    }

    public struct ChestInfo
    {
        public Vector3 Position;
        public int InstanceID;
        public Quaternion Rotation;
        public List<ItemDrop.ItemData> Contents;
        public DateTime LastUpdated;

        public ChestInfo(Container container)
        {
            if (!container.enabled)
                throw new Exception("Container is not enabled");
            Position = container.transform.position;
            Rotation = container.transform.rotation;
            InstanceID = container.GetInstanceID();
            Contents = container.GetInventory().GetAllItemsInGridOrder();
            LastUpdated = DateTime.Now;
        }
    }

    public struct ItemLocationInfo
    {
        public string ItemName;
        public int Stack;
        public int ChestId;
        public Vector3 Position;
    }

    public class SearchChestsCommand : ConsoleCommand
    {
        private readonly string _name;
        public SearchChestsCommand(string name = "searchchests") { _name = name; }
        public override string Help => "Search Chests for an item";
        public override string Name => _name;
        public override bool IsNetwork => true;


        public override void Run(string[] args)
        {
            if (args.Length == 0)
            {
                // Clear indicators and show meta info
                Main.IndicatedList?.Clear();
                int chestCount = Main.ChestInfoDict.Count;
                int itemTypes = Main.ItemNameIndex.Count;
                int totalItems = Main.ItemNameIndex.Values.SelectMany(x => x).Sum(x => x.Stack);
                string meta = $"Chests indexed: {chestCount}\nUnique item types: {itemTypes}\nTotal items: {totalItems}";
                Main.ShowMetaPopup(meta);
                return;
            }

            var partialItemName = args[0].ToLowerInvariant();
            var foundItems = new List<ItemLocationInfo>();
            foreach (var kvp in Main.ItemNameIndex)
            {
                if (kvp.Key.Contains(partialItemName))
                {
                    foundItems.AddRange(kvp.Value);
                }
            }
            if (foundItems.Count == 0)
            {
                Main.Logger.LogInfo($"No items found matching '{partialItemName}'.");
                return;
            }
            // Find the entry with the highest stack
            var topEntry = foundItems.OrderByDescending(x => x.Stack).First();
            Main.Logger.LogInfo($"Found '{topEntry.ItemName}' x{topEntry.Stack} in chest {topEntry.ChestId} at {topEntry.Position}");
            // Use the translation system for the display name
            string displayName = topEntry.ItemName;
            ChestInfo chestInfo;
            if (Main.ChestInfoDict.TryGetValue(topEntry.ChestId, out chestInfo) && chestInfo.Contents != null)
            {
                var item = chestInfo.Contents.FirstOrDefault(i => i.m_shared.m_name == topEntry.ItemName);
                if (item != null && !string.IsNullOrEmpty(item.m_shared.m_name))
                {
                    var translated = LocalizationManager.Instance.TryTranslate(item.m_shared.m_name);
                    displayName = string.IsNullOrEmpty(translated) ? item.m_shared.m_name : translated;
                }
            }
            else if (displayName.StartsWith("$"))
            {
                var translated = LocalizationManager.Instance.TryTranslate(displayName);
                displayName = string.IsNullOrEmpty(translated) ? displayName.TrimStart('$') : translated;
            }
            Main.ShowSearchResultsPopup(displayName, topEntry.Position, topEntry.Stack);
            // Always update and activate the indicated chest list for effects
            if (Main.IndicatedList != null)
            {
                Main.IndicatedList.Clear();
                // If we have chestInfo, use it; otherwise, create a fallback
                if (chestInfo.Position != Vector3.zero)
                {
                    Main.IndicatedList.Add(chestInfo);
                }
                else
                {
                    Main.IndicatedList.Add(new ChestInfo {
                        Position = topEntry.Position,
                        InstanceID = topEntry.ChestId,
                        Rotation = Quaternion.identity,
                        Contents = new List<ItemDrop.ItemData>(),
                        LastUpdated = DateTime.Now
                    });
                }
            }
        }
    }
}

