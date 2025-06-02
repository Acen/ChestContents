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
using ChestContents.UI;

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
        public static BepInEx.Configuration.ConfigEntry<int> ChestSearchRadius;
        public static BepInEx.Configuration.ConfigEntry<bool> EnableVerticalMarker;
        public static BepInEx.Configuration.ConfigEntry<float> VerticalMarkerHeight;

        public static ConfigPanelManager ConfigPanelManagerInstance;

        private bool _configPanelInitialized;

        private IChestScanner _chestScanner;
        private IChestIndexer _chestIndexer;

        private void Awake()
        {
            // Config setup
            ChestSearchRadius = Config.Bind("General", "ChestSearchRadius", 30, "Radius (in meters) to search for chests.");
            EnableVerticalMarker = Config.Bind("General", "EnableVerticalMarker", true, "Enable or disable the vertical marker.");
            VerticalMarkerHeight = Config.Bind("General", "VerticalMarkerHeight", 8f, "Height of the vertical marker.");

            _harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
            AddStatusEffect();
            // Register commands with dependency injection
            CommandManager.Instance.AddConsoleCommand(new SearchChestsCommand(
                () => IndicatedList,
                () => ChestInfoDict,
                () => ItemNameIndex,
                () => LastTotalChestCount
            ));
            CommandManager.Instance.AddConsoleCommand(new SearchChestsCommand(
                () => IndicatedList,
                () => ChestInfoDict,
                () => ItemNameIndex,
                () => LastTotalChestCount,
                "cs"
            ));
            CommandManager.Instance.AddConsoleCommand(new SearchChestsCommand(
                () => IndicatedList,
                () => ChestInfoDict,
                () => ItemNameIndex,
                () => LastTotalChestCount,
                "sc"
            ));
            CommandManager.Instance.AddConsoleCommand(new ConfigPanelCommand());
            CommandManager.Instance.AddConsoleCommand(new ConfigPanelCommand("cc"));

            _chestScanner = new ChestScanner();
            _chestIndexer = new ChestIndexer();
        }

        private void Update()
        {
            if (!_configPanelInitialized && Player.m_localPlayer != null)
            {
                var configPanelManagerObj = new GameObject("ConfigPanelManager");
                ConfigPanelManagerInstance = configPanelManagerObj.AddComponent<ChestContents.UI.ConfigPanelManager>();
                DontDestroyOnLoad(configPanelManagerObj);
                _configPanelInitialized = true;
            }

            if (Player.m_localPlayer == null) return;
            if (IndicatedList == null)
            {
                IndicatedList = new IndicatedChestList(
                    () => EnableVerticalMarker != null && EnableVerticalMarker.Value,
                    () => VerticalMarkerHeight != null ? VerticalMarkerHeight.Value : 3f
                );
            }

            if (Time.time - _lastCheckTime > InventoryCheckInterval)
            {
                _lastCheckTime = Time.time;
                var chestCount = _chestScanner.GetNearbyChestCount(Player.m_localPlayer.transform.position, 30f, Colliders, Player.m_localPlayer);
                var inventoryHash = _chestScanner.GetNearbyInventoryHash(Player.m_localPlayer.transform.position, 30f, Colliders, Player.m_localPlayer);
                var totalChestCount = _chestScanner.GetAllChestCount();
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

        private void PopulateContainers()
        {
            Chests.Clear();
            var containers = _chestScanner.ScanNearbyChests(Player.m_localPlayer.transform.position, 30f, Colliders, Player.m_localPlayer);
            foreach (var container in containers)
            {
                if (container == null || container.GetInventory() == null) continue;
                var instanceID = container.GetInstanceID();
                if (Chests.Find(x => x.GetInstanceID() == instanceID) == null)
                    Chests.Add(container);
            }
        }

        private void ParseChests()
        {
            Chests.RemoveAll(chest => chest == null || chest.transform == null || chest.GetInventory() == null);
            var chestsList = Chests.ToList();
            _chestIndexer.IndexChests(chestsList, ChestInfoDict, ItemNameIndex);
            IndicatedList?.PurgeInvalid(new HashSet<int>(chestsList.Select(c => c.GetInstanceID())));
        }

        public static bool CheckContainerAccess(Container container, Player player)
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
    }

    public interface IChestScanner
    {
        List<Container> ScanNearbyChests(Vector3 position, float radius, Collider[] colliders, Player player);
        int GetNearbyChestCount(Vector3 position, float radius, Collider[] colliders, Player player);
        int GetNearbyInventoryHash(Vector3 position, float radius, Collider[] colliders, Player player);
        int GetAllChestCount();
    }

    public class ChestScanner : IChestScanner
    {
        public List<Container> ScanNearbyChests(Vector3 position, float radius, Collider[] colliders, Player player)
        {
            var containers = new List<Container>();
            var colliderCount = Physics.OverlapSphereNonAlloc(position, radius, colliders);
            for (var i = 0; i < colliderCount; i++)
            {
                var collider = colliders[i];
                if (collider.transform.parent == null) continue;
                var container = collider.transform.gameObject.GetComponentInParent<Container>();
                if (container && container.GetInventory() != null &&
                    ChestContentsPlugin.CheckContainerAccess(container, player))
                {
                    containers.Add(container);
                }
            }
            return containers;
        }

        public int GetNearbyChestCount(Vector3 position, float radius, Collider[] colliders, Player player)
        {
            var colliderCount = Physics.OverlapSphereNonAlloc(position, radius, colliders);
            var count = 0;
            for (var i = 0; i < colliderCount; i++)
            {
                var collider = colliders[i];
                if (collider.transform.parent == null) continue;
                var container = colliders[i].transform.gameObject.GetComponentInParent<Container>();
                if (container && container.GetInventory() != null &&
                    ChestContentsPlugin.CheckContainerAccess(container, player)) count++;
            }
            return count;
        }

        public int GetNearbyInventoryHash(Vector3 position, float radius, Collider[] colliders, Player player)
        {
            var colliderCount = Physics.OverlapSphereNonAlloc(position, radius, colliders);
            var hash = 17;
            for (var i = 0; i < colliderCount; i++)
            {
                var collider = colliders[i];
                if (collider.transform.parent == null) continue;
                var container = collider.transform.gameObject.GetComponentInParent<Container>();
                if (container && container.GetInventory() != null &&
                    ChestContentsPlugin.CheckContainerAccess(container, player))
                {
                    var inv = container.GetInventory();
                    var items = inv.GetAllItemsInGridOrder();
                    foreach (var item in items)
                    {
                        hash = hash * 31 + (item.m_shared.m_name?.GetHashCode() ?? 0);
                        hash = hash * 31 + item.m_stack;
                    }
                }
            }
            return hash;
        }

        public int GetAllChestCount()
        {
            return UnityEngine.Object.FindObjectsOfType<Container>().Length;
        }
    }

    public interface IChestIndexer
    {
        void IndexChests(IEnumerable<Container> chests, Dictionary<int, ChestInfo> chestInfoDict, Dictionary<string, List<ItemLocationInfo>> itemNameIndex);
    }

    public class ChestIndexer : IChestIndexer
    {
        public void IndexChests(IEnumerable<Container> chests, Dictionary<int, ChestInfo> chestInfoDict, Dictionary<string, List<ItemLocationInfo>> itemNameIndex)
        {
            var validInstanceIds = new HashSet<int>(chests.Select(c => c.GetInstanceID()));
            var toRemove = chestInfoDict.Keys.Where(id => !validInstanceIds.Contains(id)).ToList();
            foreach (var id in toRemove) chestInfoDict.Remove(id);
            itemNameIndex.Clear();
            foreach (var chest in chests.Where(chest => chest != null && chest.transform != null && chest.GetInventory() != null))
            {
                int currentRevision = (int)Traverse.Create(chest).Field("m_lastRevision").GetValue<uint>();
                int instanceId = chest.GetInstanceID();
                bool needsUpdate = false;
                if (!chestInfoDict.ContainsKey(instanceId))
                {
                    needsUpdate = true;
                }
                else if (chestInfoDict[instanceId].LastRevision != currentRevision)
                {
                    needsUpdate = true;
                }
                if (needsUpdate)
                {
                    var ci = new ChestInfo(chest);
                    chestInfoDict[ci.InstanceID] = ci;
                }
                var ciRef = chestInfoDict[instanceId];
                foreach (var item in ciRef.Contents)
                {
                    var itemName = item.m_shared.m_name.ToLowerInvariant();
                    if (!itemNameIndex.TryGetValue(itemName, out var list))
                    {
                        list = new List<ItemLocationInfo>();
                        itemNameIndex[itemName] = list;
                    }
                    list.Add(new ItemLocationInfo
                    {
                        ItemName = item.m_shared.m_name,
                        Stack = item.m_stack,
                        ChestId = ciRef.InstanceID,
                        Position = ciRef.Position
                    });
                }
            }
        }
    }
}
