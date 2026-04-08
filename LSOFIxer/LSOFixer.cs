using UnityEngine;
using System.Collections;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using Wenzil.Console;

namespace LSOFixerMod
{
    public class LSOFixer : MonoBehaviour
    {
        private static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<LSOFixer>();

            mod.IsReady = true;
        }

        float delay = 1;

        bool transitioned;
        IEnumerator doing;

        private void Start()
        {
            PlayerEnterExit.OnTransitionDungeonExterior += OnTransition;
            PlayerEnterExit.OnTransitionDungeonInterior += OnTransition;
            PlayerEnterExit.OnTransitionExterior += OnTransition;
            PlayerEnterExit.OnTransitionInterior += OnTransition;
            PlayerEnterExit.OnFailedTransition += OnFailedTransition;
            PlayerEnterExit.OnPreTransition += OnPreTransition;

            DaggerfallTravelPopUp.OnPreFastTravel += OnPreFastTravel;
            DaggerfallTravelPopUp.OnPostFastTravel += OnPostFastTravel;

            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();
        }

        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("Main"))
            {
                delay = (float)settings.GetValue<int>("Main", "Delay");
            }
        }

        void OnPreTransition(PlayerEnterExit.TransitionEventArgs args)
        {
            if (doing != null)
                StopCoroutine(doing);

            doing = null;

            //Disable AI only
            GameManager.Instance.DisableAI = true;

            transitioned = true;
        }

        void OnFailedTransition(PlayerEnterExit.TransitionEventArgs args)
        {
            //Enable AI only
            GameManager.Instance.DisableAI = false;

            transitioned = false;

            doing = null;
        }

        void OnTransition(PlayerEnterExit.TransitionEventArgs args)
        {
            if (doing != null)
                StopCoroutine(doing);

            doing = PostTransitionCoroutine();
            StartCoroutine(doing);
        }

        void OnPreFastTravel(DaggerfallTravelPopUp daggerfallTravelPopUp)
        {
            if (doing != null)
                StopCoroutine(doing);

            doing = null;

            //Disable AI only
            GameManager.Instance.DisableAI = true;

            transitioned = true;
        }

        void OnPostFastTravel()
        {
            if (doing != null)
                StopCoroutine(doing);

            doing = PostTransitionCoroutine();
            StartCoroutine(doing);
        }

        IEnumerator PostTransitionCoroutine()
        {
            yield return new WaitForSeconds(delay);

            //Enable AI only
            if (transitioned)
            {
                GameManager.Instance.DisableAI = false;
                transitioned = false;
            }

            doing = null;
        }
    }
}
