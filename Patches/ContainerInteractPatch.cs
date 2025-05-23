using ChestContents.Managers;
using HarmonyLib;

namespace ChestContents.Patches
{
    [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
    public static class ContainerInteractPatch
    {
        public static void Postfix(Container __instance, Humanoid character, bool hold, bool alt, bool __result)
        {
            if (!__result || ChestContentsPlugin.IndicatedList == null ||
                ChestContentsPlugin.IndicatedList.ChestList.Count == 0)
                return;
            var indicated = ChestContentsPlugin.IndicatedList.ChestList[0];
            if (__instance.GetInstanceID() == indicated.InstanceID) ChestContentsPlugin.IndicatedList.Clear();
        }
    }
}