using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using ChestContents.Commands;
using ChestContents.Effects;
using ChestContents.Models;
using ChestContents.Patches;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
// ReSharper disable Unity.PerformanceCriticalCodeInvocation

namespace ChestContents.Managers
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class ChestContentsPlugin : BaseUnityPlugin
    {
        private const string PluginGuid = "sticky.chestcontents";
        private const string PluginName = "ChestContents";
        private const string PluginVersion = "1.1.0";
        private const float InventoryCheckInterval = 0.5f; // seconds
        public new static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource(PluginName);

        private static readonly List<Container> Chests = new List<Container>();
        public static readonly Dictionary<int, ChestInfo> ChestInfoDict = new Dictionary<int, ChestInfo>();

        public static readonly Dictionary<string, List<ItemLocationInfo>> ItemNameIndex =
            new Dictionary<string, List<ItemLocationInfo>>();

        private static readonly Collider[] Colliders = new Collider[10240];
        public static int LastTotalChestCount;
        private readonly Harmony _harmonyInstance = new Harmony(PluginGuid);
        private CustomStatusEffect _chestIndexEffect;
        private float _lastCheckTime;
        private int _lastChestCount = -1;
        private int _lastInventoryHash;

        public static IndicatedChestList IndicatedList { get; private set; }

        // Config entries
        public static BepInEx.Configuration.ConfigEntry<bool> EnableChestHighlighting;
        public static BepInEx.Configuration.ConfigEntry<int> ChestSearchRadius;
        public static BepInEx.Configuration.ConfigEntry<string> HighlightColor;
        public static BepInEx.Configuration.ConfigEntry<bool> EnableVerticalMarker;
        public static BepInEx.Configuration.ConfigEntry<float> VerticalMarkerHeight;

        public static ChestContents.UI.ConfigPanelManager ConfigPanelManagerInstance;

        private bool configPanelInitialized = false;

        private void Awake()
        {
            // Config setup
            EnableChestHighlighting = Config.Bind("General", "EnableChestHighlighting", true, "Enable or disable chest highlighting.");
            ChestSearchRadius = Config.Bind("General", "ChestSearchRadius", 30, "Radius (in meters) to search for chests.");
            HighlightColor = Config.Bind("General", "HighlightColor", "yellow", "Color to use for highlighting chests.");
            EnableVerticalMarker = Config.Bind("General", "EnableVerticalMarker", true, "Enable or disable the vertical marker.");
            VerticalMarkerHeight = Config.Bind("General", "VerticalMarkerHeight", 8f, "Height of the vertical marker.");

            _harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
            AddStatusEffect();
            CommandManager.Instance.AddConsoleCommand(new SearchChestsCommand());
            CommandManager.Instance.AddConsoleCommand(new SearchChestsCommand("cs"));
            CommandManager.Instance.AddConsoleCommand(new SearchChestsCommand("sc"));
            CommandManager.Instance.AddConsoleCommand(new ConfigPanelCommand());
        }

        private void Update()
        {
            if (!configPanelInitialized && Player.m_localPlayer != null)
            {
                var configPanelManagerObj = new GameObject("ConfigPanelManager");
                ConfigPanelManagerInstance = configPanelManagerObj.AddComponent<ChestContents.UI.ConfigPanelManager>();
                DontDestroyOnLoad(configPanelManagerObj);
                configPanelInitialized = true;
            }

            if (Player.m_localPlayer == null) return;
            if (IndicatedList == null) IndicatedList = new IndicatedChestList();

            if (Time.time - _lastCheckTime > InventoryCheckInterval)
            {
                _lastCheckTime = Time.time;
                var chestCount = GetNearbyChestCount();
                var inventoryHash = GetNearbyInventoryHash();
                var totalChestCount = GetAllChestCount();
                LastTotalChestCount = totalChestCount;
                if (chestCount != _lastChestCount || inventoryHash != _lastInventoryHash ||
                    totalChestCount != _lastChestCount)
                {
                    PopulateContainers();
                    ParseChests();
                    _lastChestCount = chestCount;
                    _lastInventoryHash = inventoryHash;
                }
            }

            IndicatedList.RunEffects();

            // Always ensure the status effect is present and updated
            var seMan = Player.m_localPlayer.GetSEMan();
            var chestIndex = _chestIndexEffect.StatusEffect;
            var currentChestCount = ChestInfoDict.Count;
            if (chestIndex is SeChestIndex) chestIndex.m_tooltip = currentChestCount.ToString();
            if (!seMan.GetStatusEffect(_chestIndexEffect.StatusEffect.NameHash()))
                seMan.AddStatusEffect(_chestIndexEffect.StatusEffect, true);
        }

        private int GetNearbyChestCount()
        {
            var colliderCount = Physics.OverlapSphereNonAlloc(Player.m_localPlayer.transform.position, 30f, Colliders);
            var count = 0;
            for (var i = 0; i < colliderCount; i++)
            {
                var collider = Colliders[i];
                if (collider.transform.parent == null) continue;
                var container = collider.transform.gameObject.GetComponentInParent<Container>();
                if (container && container.GetInventory() != null &&
                    CheckContainerAccess(container, Player.m_localPlayer)) count++;
            }

            return count;
        }

        private int GetNearbyInventoryHash()
        {
            var colliderCount = Physics.OverlapSphereNonAlloc(Player.m_localPlayer.transform.position, 30f, Colliders);
            var hash = 17;
            for (var i = 0; i < colliderCount; i++)
            {
                var collider = Colliders[i];
                if (collider.transform.parent == null) continue;
                var container = collider.transform.gameObject.GetComponentInParent<Container>();
                if (container && container.GetInventory() != null &&
                    CheckContainerAccess(container, Player.m_localPlayer))
                {
                    var inv = container.GetInventory();
                    foreach (var item in inv.GetAllItemsInGridOrder())
                    {
                        hash = hash * 31 + (item.m_shared.m_name?.GetHashCode() ?? 0);
                        hash = hash * 31 + item.m_stack;
                    }
                }
            }

            return hash;
        }

        private void PopulateContainers()
        {
            Chests.Clear();
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
                    if (CheckContainerAccess(container, Player.m_localPlayer))
                        Chests.Add(container);
            }
        }

        private void ParseChests()
        {
            Chests.RemoveAll(chest => chest == null || chest.transform == null || chest.GetInventory() == null);
            var validInstanceIds = new HashSet<int>(Chests.Select(c => c.GetInstanceID()));
            var toRemove = ChestInfoDict.Keys.Where(id => !validInstanceIds.Contains(id)).ToList();
            foreach (var id in toRemove) ChestInfoDict.Remove(id);
            IndicatedList?.PurgeInvalid(validInstanceIds);
            ItemNameIndex.Clear();
            foreach (var chest in Chests.Where(chest =>
                         chest != null && chest.transform != null && chest.GetInventory() != null))
            {
                var ci = new ChestInfo(chest);
                if (!ChestInfoDict.ContainsKey(ci.InstanceID))
                    ChestInfoDict.Add(ci.InstanceID, ci);
                else if (ChestInfoDict[ci.InstanceID].LastUpdated < DateTime.Now.Subtract(TimeSpan.FromSeconds(5)))
                    ChestInfoDict[ci.InstanceID] = ci;
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

        private int GetAllChestCount()
        {
            return FindObjectsOfType<Container>().Length;
        }
    }
}
