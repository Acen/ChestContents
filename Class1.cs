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
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class Main : BaseUnityPlugin
    {
        private const string PluginGUID = "sticky.chestcontents";
        private const string PluginName = "ChestContents";
        private const string PluginVersion = "1.0.0";
        public static new ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource(PluginName);

        private static readonly List<Container> Chests = new List<Container>();
        private static IndicatedChestList _indicatedList;
        private static readonly Dictionary<int, ChestInfo> ChestInfoDict = new Dictionary<int, ChestInfo>();
        private static readonly Collider[] Colliders = new Collider[10240];
        private readonly Harmony _harmonyInstance = new Harmony(PluginGUID);
        private CustomStatusEffect _chestIndexEffect;
        private int _lastChestCount = -1;

        private void Awake()
        {
            _harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
            AddStatusEffect();
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
            if (chestIndex is SE_ChestIndex) chestIndex.m_tooltip = currentChestCount.ToString();

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

            foreach (var chest in Chests.Where(chest => chest != null && chest.transform != null && chest.GetInventory() != null))
            {
                var ci = new ChestInfo(chest);
                if (!ChestInfoDict.ContainsKey(ci.InstanceID))
                {
                    // Logger.LogInfo($"Added a chest ({ci.InstanceID}) with {ci.Contents.Count} items.");
                    ChestInfoDict.Add(ci.InstanceID, ci);
                }
                else if (ChestInfoDict[ci.InstanceID].LastUpdated < DateTime.Now.Subtract(TimeSpan.FromSeconds(5)))
                {
                    ChestInfoDict[ci.InstanceID] = ci;
                    // Logger.LogInfo($"Updated a chest ({ci.InstanceID}) with {ChestInfoDict[ci.InstanceID].Contents.Count} items.");
                    _indicatedList.Add(ci);
                    // Logger.LogInfo($"Indicated list has {_indicatedList.ChestList.Count} chests");
                }
            }
        }

        private static bool CheckContainerAccess(Container container, Player player)
        {
            return ContainerPatch.RunCheckAccess(container, player.GetPlayerID());
        }

        private void AddStatusEffect()
        {
            var effect = ScriptableObject.CreateInstance<SE_ChestIndex>();
            effect.GetIconText();
            effect.name = "ChestIndexEffect";
            effect.m_name = "Chest Contents";
            effect.m_icon = AssetUtils.LoadSpriteFromFile("ChestContents/Assets/chest.png");
            _chestIndexEffect = new CustomStatusEffect(effect, false);
            ItemManager.Instance.AddStatusEffect(_chestIndexEffect);
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

    public class IndicatedChestList
    {
        public List<ChestInfo> ChestList { get; }
        private readonly HashSet<int> _chestSet;
        private readonly ActionableEffect _effect;

        public IndicatedChestList()
        {
            ChestList = new List<ChestInfo>();
            _chestSet = new HashSet<int>();
            _effect = new ActionableEffect("vfx_WishbonePing");
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
            _effect = new ActionableEffect("vfx_WishbonePing");
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
        }

        public void PurgeInvalid(HashSet<int> validInstanceIds)
        {
            ChestList.RemoveAll(ci => !validInstanceIds.Contains(ci.InstanceID));
            _chestSet.RemoveWhere(id => !validInstanceIds.Contains(id));
        }

        public void RunEffects()
        {
            if (Game.IsPaused()) return;
            foreach (var chest in ChestList)
                _effect.RunEffect(chest.Position, chest.Rotation, chest.InstanceID);
        }
    }

    public class ActionableEffect
    {
        private readonly TimeSpan _minTimeBetweenRuns;
        private readonly GameObject _prefab;
        private readonly Dictionary<int, DateTime> _lastRunPerChest = new Dictionary<int, DateTime>();

        public ActionableEffect(GameObject prefab, int repeatInSeconds = 1)
        {
            if (prefab.GetType() != typeof(GameObject))
                throw new ArgumentException($"Prefab must be a GameObject, it is {prefab.GetType()}");
            _prefab = Object.Instantiate(prefab);
            _minTimeBetweenRuns = TimeSpan.FromSeconds(repeatInSeconds);
        }

        public ActionableEffect(string prefabName, int repeatInSeconds = 1)
        {
            _prefab = PrefabManager.Cache.GetPrefab<GameObject>(prefabName);
            _minTimeBetweenRuns = TimeSpan.FromSeconds(repeatInSeconds);
        }

        public void RunEffect(Vector3 position, Quaternion rotation, int chestInstanceID)
        {
            if (_lastRunPerChest.TryGetValue(chestInstanceID, out var lastRun) &&
                lastRun + _minTimeBetweenRuns > DateTime.Now)
                return;
            // Main.Logger.LogInfo($"Running effect on chest {chestInstanceID}");
            Object.Instantiate(_prefab, position, rotation);
            _lastRunPerChest[chestInstanceID] = DateTime.Now;
        }
    }

    public class SE_ChestIndex : StatusEffect
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
        public int InstanceID { get; }
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
}
