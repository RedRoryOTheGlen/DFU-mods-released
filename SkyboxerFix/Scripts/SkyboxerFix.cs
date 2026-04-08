using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;

namespace SkyboxerFixMod
{
    public class SkyboxerFix : MonoBehaviour
    {
        private static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<SkyboxerFix>();

            mod.IsReady = true;
        }

        Camera playerCamera;
        Camera skyCamera;

        int updateMode;

        private void Awake()
        {
            playerCamera = GameManager.Instance.MainCamera;
            skyCamera = GameManager.Instance.SkyRig.SkyCamera;
            skyCamera.fieldOfView = DaggerfallUnity.Settings.FieldOfView;

            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();
        }
        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("Main"))
            {
                updateMode = settings.GetValue<int>("Main", "UpdateMode");
            }
        }

        private void LateUpdate()
        {
            if (GameManager.IsGamePaused)
                return;

            //continuous update mode
            if (updateMode == 1)
            {
                if (playerCamera.fieldOfView != skyCamera.fieldOfView)
                    skyCamera.fieldOfView = playerCamera.fieldOfView;

                if (playerCamera.transform.eulerAngles != skyCamera.transform.eulerAngles)
                    skyCamera.transform.eulerAngles = playerCamera.transform.eulerAngles;
            }
        }
    }
}
