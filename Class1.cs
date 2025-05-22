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
    [BepInPlugin(pluginGUID, pluginName, pluginVersion)]
    public class Main : BaseUnityPlugin
    {
        private const string pluginGUID = "sticky.chestcontents";
        private const string pluginName = "ChestContents";
        private const string pluginVersion = "1.0.0";
        public static ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(pluginName);

        private static readonly List<Container> _chests = new List<Container>();
        private static IndicatedChestList _indicatedList;
        private static readonly Dictionary<int, ChestInfo> _chestInfo = new Dictionary<int, ChestInfo>();

        private static readonly Collider[] _colliders = new Collider[10240];
        private readonly Harmony HarmonyInstance = new Harmony(pluginGUID);
        private CustomStatusEffect ChestIndexEffect;

        private int _lastChestCount = -1; // Track last chest count

        public void Awake()
        {
            var assembly = Assembly.GetExecutingAssembly();
            HarmonyInstance.PatchAll(assembly);

            AddStatusEffect();
        }

        private void Update()
        {
            // Check player is in game before running
            if (Player.m_localPlayer != null)
            {
                if (_indicatedList == null) _indicatedList = new IndicatedChestList();

                PopulateContainers();
                ParseChests();
                _indicatedList.RunEffects();

                // Meta
                var chestIndex = ChestIndexEffect.StatusEffect;
                int currentChestCount = _chestInfo.Count;
                if (chestIndex is SE_ChestIndex) chestIndex.m_tooltip = currentChestCount.ToString();

                // Only replace the StatusEffect if the chest count has changed
                if (_lastChestCount != currentChestCount)
                {
                    var SEMan = Player.m_localPlayer.GetSEMan();
                    if(SEMan.GetStatusEffect(ChestIndexEffect.StatusEffect.NameHash()))
                    {
                        SEMan.RemoveStatusEffect(ChestIndexEffect.StatusEffect);
                    };
                    SEMan.AddStatusEffect(ChestIndexEffect.StatusEffect, true);
                    _lastChestCount = currentChestCount;
                }

                // Object.Instantiate(ping, ic.Chest.Position, ic.Chest.Rotation);
            }
        }

        public void PopulateContainers()
        {
            // Get all chests in the world
            var colliderCount = Physics.OverlapSphereNonAlloc(Player.m_localPlayer.transform.position, 30f, _colliders);
            List<Container> containers = new List<Container>();
            for (var i = 0; i < colliderCount; i++)
            {
                var collider = _colliders[i];

                // get collider parent, then check if it has a container component
                if (collider.transform.parent == null) continue;

                var container = collider.transform.gameObject.GetComponentInParent<Container>();
                if (container)
                {
                    containers.Add(container);
                }

            }
            // Loop through containers efficiently:
            foreach (var container in containers)
            {
                if (container == null || container.GetInventory() == null) continue;

                // Check if the container is already in the list
                var instanceID = container.GetInstanceID();
                if (_chests.Find(x => x.GetInstanceID() == instanceID) == null)
                {
                    // Check if the player has access to the container
                    if (CheckContainerAccess(container, Player.m_localPlayer))
                    {
                        _chests.Add(container);
                    }
                }
            }
        }

        public void ParseChests()
        {
            foreach (var chest in _chests.Where(chest =>
                         chest != null && chest.transform != null && chest.GetInventory() != null))
            {
                var ci = new ChestInfo(chest);
                if (!_chestInfo.ContainsKey(ci.InstanceID))
                {
                    logger.LogInfo("Added a chest (" + ci.InstanceID + ") with " + ci.Contents.Count +
                                   " items.");
                    _chestInfo.Add(ci.InstanceID, ci);
                }
                else if (_chestInfo[ci.InstanceID].LastUpdated <
                         DateTime.Now.Subtract(TimeSpan.FromSeconds(5)))
                {
                    // Update the chest info if it has changed
                    _chestInfo[ci.InstanceID] = ci;
                    logger.LogInfo("Updated a chest (" + ci.InstanceID + ") with " +
                                   _chestInfo[ci.InstanceID].Contents.Count +
                                   " items.");
                    // debug effect
                    _indicatedList.Add(ci);
                    Logger.LogInfo("Indicated list has " + _indicatedList._chestList.Count + " chests");
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

            ChestIndexEffect = new CustomStatusEffect(effect, false);
            ItemManager.Instance.AddStatusEffect(ChestIndexEffect);
        }
    }

    [HarmonyPatch]
    public class ContainerPatch
    {
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(Container), "CheckAccess")]
        public static bool RunCheckAccess(Container container, long playerID)
        {
            throw new NotImplementedException("This is a reverse patch, please use the original method instead.");
        }
    }

    /**
     * This is going to be incredibly inefficient, but it will work for now.
     */
    public class IndicatedChestList
    {
        public readonly List<ChestInfo> _chestList;
        private readonly HashSet<int> _chestSet; // Added for uniqueness
        private readonly ActionableEffect _effect = new ActionableEffect("vfx_WishbonePing");

        public IndicatedChestList()
        {
            _chestList = new List<ChestInfo>();
            _chestSet = new HashSet<int>();
        }

        public IndicatedChestList(List<ChestInfo> chestList, ActionableEffect effect)
        {
            _chestList = chestList;
            _chestSet = new HashSet<int>(chestList.Select(c => c.InstanceID));
            _effect = effect;
        }

        public IndicatedChestList(List<ChestInfo> chestList)
        {
            _chestList = chestList;
            _chestSet = new HashSet<int>(chestList.Select(c => c.InstanceID));
        }

        public void Add(ChestInfo chest, bool unique = true)
        {
            if (unique)
            {
                if (_chestSet.Add(chest.InstanceID)) // Only add if not present
                {
                    _chestList.Add(chest);
                }
            }
            else
            {
                _chestList.Add(chest);
                _chestSet.Add(chest.InstanceID);
            }
        }

        public void clear()
        {
            _chestList.Clear();
            _chestSet.Clear();
        }

        public void RunEffects()
        {
            foreach (var chest in _chestList) _effect.RunEffect(chest.Position, chest.Rotation, chest.InstanceID);
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
                throw new ArgumentException("Prefab must be a GameObject, it is " + prefab.GetType());
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

            Main.logger.LogInfo("Running effect on chest " + chestInstanceID);
            Object.Instantiate(_prefab, position, rotation);
            _lastRunPerChest[chestInstanceID] = DateTime.Now;
        }
    }


    // ReSharper disable once InconsistentNaming
    public class SE_ChestIndex : StatusEffect
    {
        public override string GetIconText()
        {
            if (m_tooltip.Length <= 0) return "";

            var tt = Convert.ToInt32(m_tooltip);
            if (tt > 0 && tt < 2) return "" + tt + " chest";

            // chest count
            return "" + tt + " chests";
        }
    }


    // Struct holding the chest position, an identifier and the contents
    public struct ChestInfo
    {
        public Vector3 Position;
        public readonly int InstanceID;
        public Quaternion Rotation;
        public List<ItemDrop.ItemData> Contents;
        public DateTime LastUpdated;

        public ChestInfo(Container container)
        {
            if (!container.enabled)
            {
                throw new Exception("Container is not enabled");
            }
                
            Position = container.transform.position;
            Rotation = container.transform.rotation;
            InstanceID = container.GetInstanceID();
            
            Contents = container.GetInventory().GetAllItemsInGridOrder();
            LastUpdated = DateTime.Now;
        }
    }
}
