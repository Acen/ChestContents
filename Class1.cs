using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

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

        public void Awake()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            HarmonyInstance.PatchAll(assembly);
        }

        private void Update()
        {
            // Check player is in game before running
            if (Player.m_localPlayer != null)
            {
                PopulateContainers();
            }
        }

        private static List<Container> _chests = new List<Container>();
        private static Collider[] _colliders = new Collider[10240];

        public void PopulateContainers()
        {
            _chests.Clear();

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
                    if(CheckContainerAccess(container, Player.m_localPlayer))
                    {
                        // Add the container to the list
                        _chests.Add(container);
                    }
                }
            }
            // Check the user has permission to see the contents of the chests
            

            // Log the number of chests found
            Main.logger.LogInfo($"Found {_chests.Count} chests.");
        }

        private static bool CheckContainerAccess(Container container, Player player)
        {
            return ContainerPatch.RunCheckAccess(container, player.GetPlayerID());
        }
    }

    [HarmonyPatch]
    public class ContainerPatch
    {
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(Container), "CheckAccess")]
        public static bool RunCheckAccess(Container container, long playerID) => throw new NotImplementedException("This is a reverse patch, please use the original method instead.");
    }
}
