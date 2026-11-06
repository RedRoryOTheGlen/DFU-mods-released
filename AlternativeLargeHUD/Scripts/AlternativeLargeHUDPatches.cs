using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using HarmonyLib;

namespace AlternativeLargeHUDMod
{
    public static class AlternativeLargeHUDPatches
    {
        public static Harmony harmony;

        #region New Methods

        // Prefix for AddMessage()
        public static bool Prefix_AddMessage(string str)
        {
            Debug.Log("Harmony: Custom Prefix_AddMessage()");

            //Add the string to the HUD's log here
            AlternativeLargeHUD.AddMessage(str);

            return true;
        }

        // Postfix for GetHoverText()
        public static void Postfix_GetHoverText(ref string __result)
        {
            if (AlternativeLargeHUD.Instance.lastLargeHUD)
            {
                //get result for ourselves
                AlternativeLargeHUD.SetInfoTextWorld(__result);

                //stop tooltip from showing
                __result = string.Empty;
            }
        }

        #endregion

        #region Patching

        public static bool TryApplyPatch()
        {
            try
            {
                harmony = new Harmony("AlternativeLargeHUD");

                PatchAddMessage();
                PatchGetHoverText();

                Debug.Log("Harmony: Applied patches successfully.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Harmony: Error while patching => {e}");
                return false;
            }
        }

        private static void PatchAddMessage()
        {
            MethodInfo targetMethod = typeof(DaggerfallWorkshop.Game.Player.PlayerNotebook)
                .GetMethod("AddMessage", BindingFlags.Public | BindingFlags.Instance);

            MethodInfo prefixMethod = typeof(AlternativeLargeHUDPatches)
                .GetMethod("Prefix_AddMessage", BindingFlags.Public | BindingFlags.Static);

            harmony.Patch(original: targetMethod, prefix: new HarmonyMethod(prefixMethod));

            Debug.Log("Harmony: AddMessage() patched successfully.");
        }

        private static void PatchGetHoverText()
        {
            Mod mod = ModManager.Instance.GetModFromGUID("88e77a95-fca0-4c13-a3b9-55ddf40ee01e");
            if (mod == null) return;

            Type targetType = mod.GetCompiledType("Game.Mods.WorldTooltips.Scripts.Modded_HUDTooltipWindow");

            MethodInfo targetMethod = targetType
                .GetMethod("GetHoverText", BindingFlags.NonPublic | BindingFlags.Instance);

            MethodInfo postFixMethod = typeof(AlternativeLargeHUDPatches)
                .GetMethod("Postfix_GetHoverText", BindingFlags.Public | BindingFlags.Static);

            harmony.Patch(original: targetMethod, postfix: new HarmonyMethod(postFixMethod));

            Debug.Log("Harmony: GetHoverText() patched successfully.");
        }

        #endregion
    }
}
