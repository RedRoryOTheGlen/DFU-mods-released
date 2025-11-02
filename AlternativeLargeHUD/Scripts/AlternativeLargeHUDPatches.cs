using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace AlternativeLargeHUDMod
{
    public static class AlternativeLargeHUDPatches
    {
        private const string HarmonyAssemblyPath = "Mods/0Harmony.dll";
        private const string HarmonyPatchId = "alternativelargehud.playernotebook.patch";

        private static object harmonyInstance;
        private static MethodInfo patchMethod;
        private static ConstructorInfo harmonyMethodCtor;
        private static Type harmonyMethodType;

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
            string harmonyPath = Path.Combine(Application.streamingAssetsPath, HarmonyAssemblyPath);

            if (!File.Exists(harmonyPath))
            {
                Debug.LogError($"Harmony: {harmonyPath} not found");
                return false;
            }

            try
            {
                Setup(harmonyPath);

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

        private static void Setup(string harmonyPath)
        {
            Assembly harmonyAssembly = GetHarmonyAssembly(harmonyPath);
            Type harmonyType = harmonyAssembly.GetType("HarmonyLib.Harmony");
            harmonyMethodType = harmonyAssembly.GetType("HarmonyLib.HarmonyMethod");

            harmonyInstance = Activator.CreateInstance(harmonyType, new object[] { HarmonyPatchId });

            patchMethod = harmonyType.GetMethod("Patch");
            harmonyMethodCtor = harmonyMethodType.GetConstructor(new Type[] { typeof(MethodInfo) });
        }

        private static Assembly GetHarmonyAssembly(string harmonyPath)
        {
            byte[] dllData = File.ReadAllBytes(harmonyPath);
            return Assembly.Load(dllData);
        }

        private static void PatchAddMessage()
        {
            MethodInfo targetMethod = typeof(DaggerfallWorkshop.Game.Player.PlayerNotebook)
                .GetMethod("AddMessage", BindingFlags.Public | BindingFlags.Instance);

            MethodInfo prefixMethod = typeof(AlternativeLargeHUDPatches)
                .GetMethod("Prefix_AddMessage", BindingFlags.Public | BindingFlags.Static);

            object harmonyPrefix = harmonyMethodCtor.Invoke(new object[] { prefixMethod });

            patchMethod.Invoke(harmonyInstance, new object[]
            {
                targetMethod,
                harmonyPrefix,
                null, null, null
            });

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

            object harmonyPostFix = harmonyMethodCtor.Invoke(new object[] { postFixMethod });

            patchMethod.Invoke(harmonyInstance, new object[]
            {
                targetMethod,
                null,
                harmonyPostFix,
                null,
                null
            });

            Debug.Log("Harmony: GetHoverText() patched successfully.");
        }

        #endregion
    }
}
