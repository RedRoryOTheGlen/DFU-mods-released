using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.Serialization;
using Wenzil.Console;

namespace PhysicalHUDMod
{
    public class PhysicalHUD : MonoBehaviour
    {
        private static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<PhysicalHUD>();

            mod.IsReady = true;
        }

        public static PhysicalHUD Instance;

        //screen properties
        //public float ScaleFactorX = 0.8f;
        public int nativeScreenWidth = 320;
        public int nativeScreenHeight = 200;
        public Rect screenRect;

        public float screenScaleX;
        public float screenScaleY;

        Texture2D[] headingTextures;
        Texture2D headingTextureCurrent;
        Rect headingRect;

        public int headingIntervalIndex = 7;
        int[] headingIntervals = new int[] { 1, 2, 3, 5, 6, 9, 10, 15, 18, 30, 45, 90 };    //factors of 90
        int headingInterval
        {
            get { return headingIntervals[headingIntervalIndex]; }
        }
        int headingFrameCount
        {
            get
            {
                return (int)(360 / headingInterval);
            }
        }

        bool viewing = false;
        bool unsheathed;

        //texture properties
        public Vector2 headingOffset;
        public float headingScale = 1;
        public Color headingColor = Color.white;

        Vector2 Position;

        //settings
        KeyCode keyCodeCompass;
        int textureScaleFactor = 1;

        public void OnPositionChange(Vector2 value)
        {
            Position = value;
        }

        public static void OnLoad(SaveData_v1 saveData)
        {
            Instance.ClearViewing();
        }

        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("Controls"))
            {
                keyCodeCompass = SetKeyFromText(settings.GetString("Controls", "Compass"));
            }
            if (change.HasChanged("Appearance"))
            {
                textureScaleFactor = settings.GetValue<int>("Appearance", "TextureScaleFactor");
            }
        }

        private KeyCode SetKeyFromText(string text)
        {
            Debug.Log("Setting Key");
            if (System.Enum.TryParse(text, out KeyCode result))
            {
                Debug.Log("Key set to " + result.ToString());
                return result;
            }
            else
            {
                Debug.Log("Detected an invalid key code. Setting to default.");
                return KeyCode.None;
            }
        }

        private void Start()
        {
            if (Instance == null)
                Instance = this;

            //initialize heading textures
            headingTextures = new Texture2D[headingFrameCount];
            int archive = 112396;
            int record = 0;
            int frame = 0;
            for (int i = 0; i < headingFrameCount; i++)
            {
                Texture2D texture;
                DaggerfallWorkshop.Utility.AssetInjection.TextureReplacement.TryImportTexture(archive, record, frame, out texture);
                headingTextures[i] = texture;
                frame++;
            }
            headingTextureCurrent = headingTextures[0];

            //do behaviors
            SaveLoadManager.OnLoad += OnLoad;

            //do mod compatibility stuff here
            Mod ww = ModManager.Instance.GetModFromGUID("9f301f2b-298b-43d8-8f3f-c54deaa841e0");
            if (ww != null)
                ModManager.Instance.SendModMessage(ww.Title, "addPosition", (Action<Vector2>)OnPositionChange);

            //register items
            DaggerfallUnity.Instance.ItemHelper.RegisterCustomItem(ItemCompass.templateIndex, ItemGroups.UselessItems2, typeof(ItemCompass));

            //register console commands
            ConsoleCommandsDatabase.RegisterCommand(GiveMeCompass.name, GiveMeCompass.description, GiveMeCompass.usage, GiveMeCompass.Execute);

            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();
        }

        private static class GiveMeCompass
        {
            public static readonly string name = "givecompass";
            public static readonly string description = "Add a compass to the player's inventory";
            public static readonly string usage = "givecompass";

            public static string Execute(params string[] args)
            {
                string result = "";
                result = "Compass added to player's inventory";
                DaggerfallUnityItem newCompassItem = ItemBuilder.CreateItem(ItemGroups.UselessItems2, ItemCompass.templateIndex);
                GameManager.Instance.PlayerEntity.Items.AddItem(newCompassItem);
                return result;
            }
        }

        private void Update()
        {
            if (!GameManager.Instance.IsPlayingGame() || GameManager.IsGamePaused || InputManager.Instance.IsPaused || SaveLoadManager.Instance.LoadInProgress)
                return;

            Vector2 headingOffsetTarget = Vector2.zero;

            if (InputManager.Instance.GetKeyUp(keyCodeCompass))
            {
                if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, ItemCompass.templateIndex))
                    ToggleViewing();
            }

            if (viewing)
            {
                Vector3 fwd = GameManager.Instance.MainCameraObject.transform.forward;
                fwd.y = 0;
                float yaw = Vector3.SignedAngle(fwd, Vector3.forward, Vector3.up);

                //update heading texture
                int headingTextureIndex = 0;
                headingTextureIndex = (int)((360 - ((yaw / headingInterval) * headingInterval)) / headingInterval);
                if (yaw < 0)
                    headingTextureIndex = (int)(((-yaw / headingInterval) * headingInterval) / headingInterval);
                headingTextureIndex = Mathf.Clamp(headingTextureIndex, 0, headingFrameCount);
                if (headingTextureIndex == 0)
                    headingTextureIndex = headingFrameCount;
                headingTextureCurrent = headingTextures[headingFrameCount - headingTextureIndex];

                //disable when conditions are met
                if (!GameManager.Instance.WeaponManager.Sheathed || GameManager.Instance.PlayerEffectManager.HasReadySpell || GameManager.Instance.PlayerSpellCasting.IsPlayingAnim)
                    StopViewing();
            }
            else
            {
                headingOffsetTarget = new Vector2(-1,-1);
            }

            headingOffset = Vector2.MoveTowards(headingOffset,headingOffsetTarget,10*Time.deltaTime);
        }

        private void OnGUI()
        {
            if (!GameManager.Instance.IsPlayingGame() || GameManager.IsGamePaused || InputManager.Instance.IsPaused || SaveLoadManager.Instance.LoadInProgress)
                return;

            if (DaggerfallUI.Instance.CustomScreenRect != null)
                screenRect = DaggerfallUI.Instance.CustomScreenRect.Value;
            else
                screenRect = new Rect(0, 0, Screen.width, Screen.height);

            screenScaleY = (float)screenRect.height / nativeScreenHeight;
            screenScaleX = (float)screenRect.width / nativeScreenWidth;

            GUI.depth = 1;

            float LargeHudOffset = 0;
            if (DaggerfallUI.Instance.DaggerfallHUD != null && DaggerfallUnity.Settings.LargeHUD && DaggerfallUnity.Settings.LargeHUDOffsetHorse)
                LargeHudOffset = (int)DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.ScreenHeight;

            if (Event.current.type.Equals(EventType.Repaint))
            {
                if (DaggerfallUnity.Settings.Handedness == 1)
                {
                    Vector2 headingPos = new Vector2(screenRect.x, screenRect.y + screenRect.height - LargeHudOffset);
                    if (viewing)
                        headingPos += Position * Vector2.left;
                    Vector2 headingTextureScale = new Vector2(-(headingTextureCurrent.width/textureScaleFactor) * screenScaleX, (headingTextureCurrent.height/textureScaleFactor) * screenScaleY) * headingScale;
                    Vector2 headingTextureOffset = new Vector2(headingTextureScale.x * (0.75f + headingOffset.x), headingTextureScale.y * (0.75f + headingOffset.y));
                    headingRect = new Rect(headingPos - headingTextureOffset, headingTextureScale);
                }
                else
                {
                    Vector2 headingPos = new Vector2(screenRect.x + screenRect.width, screenRect.y + screenRect.height - LargeHudOffset);
                    if (viewing)
                        headingPos += Position;
                    Vector2 headingTextureScale = new Vector2((headingTextureCurrent.width/textureScaleFactor) * screenScaleX, (headingTextureCurrent.height/textureScaleFactor) * screenScaleY) * headingScale;
                    Vector2 headingTextureOffset = new Vector2(headingTextureScale.x * (0.75f + headingOffset.x), headingTextureScale.y * (0.75f + headingOffset.y));
                    headingRect = new Rect(headingPos - headingTextureOffset, headingTextureScale);
                }
                float clamp = screenRect.height - LargeHudOffset - (headingTextureCurrent.height * screenScaleY);
                if (headingRect.y < clamp)
                    headingRect.y = clamp;
                DaggerfallUI.DrawTexture(headingRect, headingTextureCurrent, ScaleMode.StretchToFill, false, GameManager.Instance.RightHandWeapon.Tint);
            }
        }

        public void StartViewing()
        {
            unsheathed = false;
            if (!GameManager.Instance.WeaponManager.Sheathed)
            {
                unsheathed = true;
                GameManager.Instance.WeaponManager.SheathWeapons();
            }

            viewing = true;
        }

        public void StopViewing()
        {
            if (unsheathed && GameManager.Instance.WeaponManager.Sheathed)
                GameManager.Instance.WeaponManager.ToggleSheath();

            viewing = false;
        }

        public void ToggleViewing()
        {
            if (viewing)
                StopViewing();
            else
                StartViewing();
        }

        public void ClearViewing()
        {
            viewing = false;
        }
    }
}
