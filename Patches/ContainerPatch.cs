using System;
using HarmonyLib;

namespace ChestContents.Patches
{
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
}