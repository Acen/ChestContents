using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
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
        const string pluginGUID = "sticky.chestcontents";
        const string pluginName = "ChestContents";
        const string pluginVersion = "1.0.0";
        private readonly Harmony HarmonyInstance = new Harmony(pluginGUID);
        public static ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(pluginName);

        private static List<Container> _chests = new List<Container>();
        private static Dictionary<int, IndicatableChest> _chestInfo = new Dictionary<int, IndicatableChest>();
        private CustomStatusEffect ChestIndexEffect;

        public void Awake()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            HarmonyInstance.PatchAll(assembly);

            AddStatusEffect();
        }

        private void Update()
        {
            // Check player is in game before running
            if (Player.m_localPlayer != null)
            {
                PopulateContainers();
                // Flesh out container info
                foreach(var chest in _chests.Where(chest => chest.transform != null && chest.transform.position != null))
                {
                    ChestInfo ci = new ChestInfo(chest);
                    IndicatableChest ic = new IndicatableChest(ci);
                    if (!_chestInfo.ContainsKey(ci.InstanceID))
                    {
                        Main.logger.LogInfo("Added a chest (" + ci.InstanceID + ") with " + ci.Contents.Count +
                                            " items.");
                        _chestInfo.Add(ci.InstanceID, ic);
                    }
                    else if (_chestInfo[ci.InstanceID].Chest.LastUpdated <
                             (DateTime.Now.Subtract(TimeSpan.FromSeconds(1))))
                    {
                        // Update the chest info if it has changed
                        _chestInfo[ci.InstanceID] = ic;
                        Main.logger.LogInfo("Updated a chest (" + ci.InstanceID + ") with " +
                                            _chestInfo[ci.InstanceID].Chest.Contents.Count +
                                            " items.");

                        GameObject ping = ZNetScene.instance.GetPrefab("vfx_WishbonePing");
                        ParticleSystem[] particleSystems = ping.GetComponentsInChildren<ParticleSystem>();
                        foreach (var particleSystem in particleSystems)
                        {
                            ParticleSystem.MainModule main = particleSystem.main;
                            main.startDelayMultiplier = 0f;
                        }

                        if (ping != null)
                        {
                            Object.Instantiate(ping, ic.Chest.Position, ic.Chest.Rotation);
                        }
                    }
                };
                var chestIndex = ChestIndexEffect.StatusEffect;
                if (chestIndex is SE_ChestIndex)
                {
                    chestIndex.m_tooltip = _chestInfo.Count.ToString();
                }

                Player.m_localPlayer.GetSEMan().AddStatusEffect(ChestIndexEffect.StatusEffect, true);
            }
        }

        private static Collider[] _colliders = new Collider[10240];

        public void PopulateContainers()
        {
            // Get all chests in the world
            int size = Physics.OverlapSphereNonAlloc(Player.m_localPlayer.transform.position, 30f, _colliders);
            for (int i = 0; i < size; i++)
            {
                Collider collider = _colliders[i];

                // get collider parent, then check if it has a container component
                if (collider.transform.parent == null)
                {
                    continue;
                }

                Container container = collider.transform.gameObject.GetComponentInParent<Container>();
                if (container)
                {
                    int instanceID = container.GetInstanceID();
                    Container result = _chests.Find(x => x.GetInstanceID() == instanceID);
                    if (CheckContainerAccess(container, Player.m_localPlayer))
                    {
                        if (result == null)
                        {
                            // Add the container to the list if instance isn't already there
                            _chests.Add(container);
                        }
                    }
                }
            }
        }

        private static bool CheckContainerAccess(Container container, Player player)
        {
            return ContainerPatch.RunCheckAccess(container, player.GetPlayerID());
        }

        private void AddStatusEffect()
        {
            SE_ChestIndex effect = ScriptableObject.CreateInstance<SE_ChestIndex>();
            effect.GetIconText();
            effect.name = "ChestIndexEffect";
            effect.m_name = "Chest Contents";
            effect.m_icon = AssetUtils.LoadSpriteFromFile("ChestContents/Assets/chest.png");

            ChestIndexEffect = new CustomStatusEffect(effect, fixReference: false);
            ItemManager.Instance.AddStatusEffect(ChestIndexEffect);
        }
    }

    [HarmonyPatch]
    public class ContainerPatch
    {
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(Container), "CheckAccess")]
        public static bool RunCheckAccess(Container container, long playerID) =>
            throw new NotImplementedException("This is a reverse patch, please use the original method instead.");
    }

    public class IndicatableChest
    {
        public ChestInfo Chest;
        public EffectList OnIndicated = new IndicatedChestEffectList();

        public IndicatableChest(ChestInfo chest)
        {
            Chest = chest;
        }
    }

    public class IndicatedChestEffectList : EffectList
    {
        public IndicatedChestEffectList()
        {
            m_effectPrefabs = new EffectData[1];
            m_effectPrefabs[0] = new IndicatedChestEffectData();
        }
    }

    public class IndicatedChestEffectData : EffectList.EffectData
    {
        // ReSharper disable once InconsistentNaming
        public new GameObject m_prefab = ZNetScene.instance.GetPrefab("vfx_WishbonePing");
    }

    // ReSharper disable once InconsistentNaming
    public class SE_ChestIndex : StatusEffect
    {
        public override string GetIconText()
        {
            if (m_tooltip.Length <= 0)
            {
                return "";
            }

            var tt = Convert.ToInt32(m_tooltip);
            if (tt > 0 && tt < 2)
            {
                return "" + tt + " chest";
            }

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

        public ChestInfo(Vector3 position, Quaternion rotation, int instanceID, List<ItemDrop.ItemData> contents)
        {
            Position = position;
            InstanceID = instanceID;
            Rotation = rotation;
            Contents = contents;
            LastUpdated = DateTime.Now;
        }

        public ChestInfo(Container container)
        {
            Position = container.transform.position;
            Rotation = container.transform.rotation;
            InstanceID = container.GetInstanceID();
            Contents = container.GetInventory().GetAllItemsInGridOrder();
            LastUpdated = DateTime.Now;
        }
    }
}