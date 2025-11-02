using System;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Utility.AssetInjection;
using Wenzil.Console;

namespace AlternativeLargeHUDMod
{
    public class AlternativeLargeHUD : MonoBehaviour
    {
        private static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<AlternativeLargeHUD>();

            mod.IsReady = true;
        }

        public static AlternativeLargeHUD Instance;

        ConsoleController consoleController;

        const int nativeScreenWidth = 320;
        const int nativeScreenHeight = 200;

        Rect screenRect;
        float ScaleX;
        float ScaleY;

        int depth = -1;

        Texture2D BackgroundTexture
        {
            get
            {
                if (messageBox == 1)
                    return BackgroundTextureCenter;
                else
                    return BackgroundTextureSide;
            }
        }
        Texture2D BackgroundTextureSide;
        Texture2D BackgroundTextureCenter;
        Rect BackgroundRect;

        Texture2D MessageBackgroundTexture;
        float lastBackgroundHeight;

        Texture2D BarTexture
        {
            get
            {
                if (messageBox == 1)
                    return BarTextureCenter;
                else
                    return BarTextureSide;
            }
        }
        Texture2D BarTextureSide;
        Texture2D BarTextureCenter;
        Rect BarRect;
        Texture2D BarBaseTexture
        {
            get
            {
                if (messageBox == 1)
                    return BarBaseTextureCenter;
                else
                    return BarBaseTextureSide;
            }
        }
        Texture2D BarBaseTextureSide;
        Texture2D BarBaseTextureCenter;
        Rect BarBaseRect;

        public List<string> messages = new List<string>();
        int messagesMaxCount = 10;
        int messagesMaxLength = 12;
        Vector2 messagePos = new Vector2();

        public string info;
        Vector2 infoPos = new Vector2();

        GameObject lastHit;
        LayerMask layerMask;
        float hitDelayTime = 2;
        float hitDelayTimer = 0;

        EnemyEntity lastHitEnemy;
        string lastHitName;
        string lastHitChallenge;
        float lastHitHealth;
        string lastHitCondition;

        Vector2 barPos = new Vector2();
        float lastBarWidthHealth;
        Color lastBarWidthColor = new Color(1,1,1,0.5f);

        //setting
        int messageBox = 0; //0 = 2 windows, 1 = centered log
        bool messageGradient = false;
        int enemyCondition = 0; //0 = description, 1 = percent, 2 = numbers, 3 = bar
        float textScaleMod = 1;

        //event
        public bool lastLargeHUD;

        private void Awake()
        {
            Instance = this;

            consoleController = GameObject.Find("Console").GetComponent<ConsoleController>();

            SaveLoadManager.OnStartLoad += OnStartLoad;
            StartGameBehaviour.OnStartMenu += OnStartMenu;

            layerMask = ~(1 << LayerMask.NameToLayer("Player"));
            layerMask = layerMask & ~(1 << LayerMask.NameToLayer("Ignore Raycast"));

            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();
            mod.IsReady = true;
        }

        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("Main"))
            {
                depth = settings.GetValue<int>("Main", "RenderDepth");
                messageBox = settings.GetValue<int>("Main", "MessageBox");
                messageGradient = settings.GetValue<bool>("Main", "MessageGradient");
                messagesMaxCount = settings.GetValue<int>("Main", "MaxMessageCount");
                messagesMaxLength = settings.GetValue<int>("Main", "MaxMessageLength");

                textScaleMod = (float)settings.GetValue<int>("Main", "TextScale")/100f;

                int lastEnemyCondition = enemyCondition;
                enemyCondition = settings.GetValue<int>("Main", "EnemyHealthDisplay");
                if (enemyCondition != lastEnemyCondition && lastHit != null)
                    UpdateEnemyInfo(true);
            }
        }

            private void Start()
        {
            TextureReplacement.TryImportTexture(112393, 0, 0, out BackgroundTextureSide);
            TextureReplacement.TryImportTexture(112393, 0, 1, out BackgroundTextureCenter);
            TextureReplacement.TryImportTexture(112393, 0, 2, out MessageBackgroundTexture);
            TextureReplacement.TryImportTexture(112393, 1, 0, out BarTextureSide);
            TextureReplacement.TryImportTexture(112393, 1, 1, out BarTextureCenter);
            TextureReplacement.TryImportTexture(112393, 2, 0, out BarBaseTextureSide);
            TextureReplacement.TryImportTexture(112393, 2, 1, out BarBaseTextureCenter);

            DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.Enabled = false;

            if (AlternativeLargeHUDPatches.TryApplyPatch())
                Debug.Log("Harmony: PlayerNotebook patched!");
            else
                Debug.Log("Harmony: PlayerNotebook failed to patch!");

            DaggerfallUI.Instance.DaggerfallHUD.ShowPopupText = false;
            DaggerfallUI.Instance.DaggerfallHUD.ShowMidScreenText = false;
        }

        public static void OnStartMenu(object sender, EventArgs e)
        {
            Instance.ClearMessages();
        }

        public static void OnStartLoad(SaveData_v1 saveData)
        {
            Instance.ClearMessages();
        }

        public void ClearMessages()
        {
            messages.Clear();
        }

        private void LateUpdate()
        {
            if (!GameManager.Instance.IsPlayingGame())
                DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.Enabled = false;

            if (lastLargeHUD != DaggerfallUnity.Settings.LargeHUD)
            {
                if (lastLargeHUD)
                {
                    //large HUD was disabled
                    /*DaggerfallUI.Instance.DaggerfallHUD.ShowPopupText = true;
                    DaggerfallUI.Instance.DaggerfallHUD.ShowMidScreenText = true;*/
                }
                else
                {
                    //large HUD was enabled
                    /*DaggerfallUI.Instance.DaggerfallHUD.ShowPopupText = false;
                    DaggerfallUI.Instance.DaggerfallHUD.ShowMidScreenText = false;*/
                }
                lastLargeHUD = DaggerfallUnity.Settings.LargeHUD;
            }
        }

        private void FixedUpdate()
        {
            if (!DaggerfallUnity.Settings.LargeHUD || !GameManager.Instance.IsPlayingGame())
                return;

            bool newHit = false;
            GameObject previousLastHit = lastHit;

            UpdateHit();

            if (previousLastHit != lastHit)
                newHit = true;

            if (lastHit != null)
            {
                UpdateEnemyInfo(newHit);
            }
        }

        void UpdateHit()
        {
            Ray ray = new Ray(GameManager.Instance.MainCameraObject.transform.position, GameManager.Instance.MainCameraObject.transform.forward);
            RaycastHit hit = new RaycastHit();
            if (Physics.Raycast(ray, out hit, 100, layerMask, QueryTriggerInteraction.UseGlobal))
            {
                if (hit.collider.gameObject == lastHit)
                    return;

                DaggerfallEntityBehaviour behaviour = hit.collider.gameObject.GetComponent<DaggerfallEntityBehaviour>();
                if (behaviour)
                {
                    if (behaviour.Entity is EnemyEntity enemy)
                    {
                        lastHit = hit.collider.gameObject;
                        lastHitEnemy = enemy;
                        lastBarWidthHealth = 0;
                        hitDelayTimer = 0;
                        return;
                    }
                }
            }

            if (hitDelayTimer > hitDelayTime)
            {
                ClearEnemyInfo();
                hitDelayTimer = 0;
            }
            else
                hitDelayTimer += Time.fixedDeltaTime;
        }

        public void SetInfoText(string str)
        {
            if (!String.IsNullOrEmpty(str))
                str = str.Replace("\\r", "\n");

            Instance.info = str;
        }

        public static void SetInfoTextWorld(string str)
        {
            if (Instance.lastHit)
                return;

            Instance.SetInfoText(str);
        }

        void ClearEnemyInfo()
        {
            lastHit = null;
            lastHitEnemy = null;
            lastHitName = string.Empty;
            lastHitChallenge = string.Empty;
            lastHitCondition = string.Empty;
            lastHitHealth = 100;
            lastBarWidthHealth = 0;

        }

        void UpdateEnemyInfo(bool forceUpdateAll = false)
        {
            if (String.IsNullOrEmpty(lastHitName) || forceUpdateAll)
            {
                lastHitName = lastHitEnemy.Name == lastHitEnemy.Career.Name ? TextManager.Instance.GetLocalizedEnemyName(lastHitEnemy.MobileEnemy.ID) : lastHitEnemy.Name;
            }

            if (String.IsNullOrEmpty(lastHitChallenge) || forceUpdateAll)
            {
                lastHitChallenge = "Challenging";
                int level = lastHitEnemy.Level - GameManager.Instance.PlayerEntity.Level;
                if (level > 6)
                    lastHitChallenge = "Impossible";
                else if (level > 4)
                    lastHitChallenge = "Overpowering";
                else if (level > 2)
                    lastHitChallenge = "Difficult";
                else if (level < -2)
                    lastHitChallenge = "Moderate";
                else if (level < -4)
                    lastHitChallenge = "Easy";
                else if (level < -6)
                    lastHitChallenge = "Effortless";
            }

            if (lastHitHealth != lastHitEnemy.CurrentHealthPercent || forceUpdateAll)
            {
                lastHitHealth = lastHitEnemy.CurrentHealthPercent;
                if (enemyCondition == 3)
                {
                    //bar
                    lastHitCondition = string.Empty;
                }
                else if (enemyCondition == 2)
                {
                    //numbers
                    lastHitCondition = lastHitEnemy.CurrentHealth.ToString() + " of " + lastHitEnemy.MaxHealth.ToString();
                }
                else if (enemyCondition == 1)
                {
                    //percent
                    lastHitCondition = (lastHitHealth*100).ToString("0") + "%";
                }
                else
                {
                    lastHitCondition = "Uninjured";
                    if (lastHitHealth <= 0)
                        lastHitCondition = "Dead";
                    else if (lastHitHealth < 0.2f)
                        lastHitCondition = "Near Death";
                    else if (lastHitHealth < 0.4f)
                        lastHitCondition = "Badly Injured";
                    else if (lastHitHealth < 0.6f)
                        lastHitCondition = "Injured";
                    else if (lastHitHealth < 0.8f)
                        lastHitCondition = "Barely Injured";
                }
            }

            string str = lastHitName + "\n" + lastHitChallenge + "\n" + lastHitCondition;

            SetInfoText(str);
        }

        public static void AddMessage(string str)
        {
            if (String.IsNullOrWhiteSpace(str) ||
                String.IsNullOrEmpty(str) ||
                str == TextManager.Instance.GetLocalizedText("pressButtonToFireSpell") ||
                str == TextManager.Instance.GetLocalizedText("saveVersusSpellMade")
                )
                return;

            if (Instance.messages.Count >= Instance.messagesMaxCount)
                Instance.messages.RemoveAt(Instance.messages.Count - 1);

            Instance.messages.Insert(0, str);
        }

        public List<string> WrapTextTooltip(string input, Vector2 scale, int maxLineLength = 35, int alignment = 0)
        {
            if (input.StartsWith("To\n"))
                input = input.Remove(0, 3);

            input = input.Replace(".",string.Empty);

            if (input.Contains("Locked"))
            {
                int index = input.IndexOf("Locked");
                input = input.Insert(index+6,"\n");
            }

            if (input.Contains("%"))
            {
                int index = input.IndexOf("%");
                input = input.Insert(index + 1, "\n");
            }

            if (input.Contains("closed"))
            {
                int index = input.IndexOf("closed");
                input = input.Insert(index + 6, "\n");
            }

            return WrapText(input, scale, maxLineLength, alignment);
        }

        public List<string> WrapText(string input, Vector2 scale, int maxLineLength = 35, int alignment = 0)
        {
            var lines = new List<string>();

            if (string.IsNullOrWhiteSpace(input))
                return lines;

            string[] rawLines = input.Split('\n');

            foreach (string rawLine in rawLines)
            {

                string[] words = rawLine.Trim().Split(' ');
                var currentLine = new StringBuilder();

                foreach (string word in words)
                {
                    if (currentLine.Length + word.Length + 1 > maxLineLength)
                    {
                        lines.Add(currentLine.ToString().TrimEnd());

                        currentLine.Clear();

                        if (alignment == 0)
                            currentLine.Append("  ");
                    }

                    currentLine.Append(word + " ");
                }

                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine.ToString().TrimEnd());
                }
            }

            return lines;
        }

        float GetStringLength(string input, Vector2 scale)
        {
            return DaggerfallUI.DefaultFont.CalculateTextWidth(input,scale);
        }

        private void OnGUI()
        {
            if (consoleController.ui.isConsoleOpen)
                return;

            GUI.depth = GameManager.Instance.IsPlayingGame() ? depth : 0;

            if (DaggerfallUI.Instance.CustomScreenRect != null)
                screenRect = DaggerfallUI.Instance.CustomScreenRect.Value;
            else
                screenRect = new Rect(0, 0, Screen.width, Screen.height);

            ScaleX = (float)screenRect.width / (float)nativeScreenWidth;
            ScaleY = (float)screenRect.height / (float)nativeScreenHeight;

            float backgroundWidth = BackgroundTexture.width * ScaleX;
            float backgroundHeight = BackgroundTexture.height * ScaleY;

            int extraLines = 0;

            float scale = 0.75f * ScaleY * textScaleMod;
            Vector2 textScale = Vector2.one * scale;

            float messageLength = 40 * (screenRect.width/screenRect.height);
            int maxLengthMessages = Mathf.FloorToInt(messageLength / (textScaleMod * 1.5f));
            int maxLengthInfo = Mathf.FloorToInt(52 / (textScaleMod * 1.5f));

            float spaceMessages = 6f * ScaleY * textScaleMod;
            float spaceInfo = 7.5f * ScaleY * textScaleMod;

            if (DaggerfallUnity.Settings.SDFFontRendering)
            {
                textScale *= 1.25f;
                maxLengthMessages = Mathf.FloorToInt(maxLengthMessages * 1.4f);
                spaceMessages *= 1.2f;
                maxLengthInfo = Mathf.FloorToInt(maxLengthInfo * 1.4f);
                spaceInfo *= 1.2f;
            }

            if (DaggerfallUnity.Settings.LargeHUD)
            {
                screenRect.height -= DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.ScreenHeight;
                backgroundHeight = DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.ScreenHeight;
                lastBackgroundHeight = backgroundHeight;

                BackgroundRect = new Rect(screenRect.x, screenRect.y + screenRect.height, backgroundWidth, backgroundHeight);

                if (messageBox == 1)
                {
                    messagePos = new Vector2(screenRect.x + (screenRect.width * 0.25f) + (screenRect.width * 0.02f), screenRect.y + screenRect.height + (backgroundHeight * 0.15f));
                    infoPos = new Vector2(screenRect.x + (screenRect.width * 0.018f), screenRect.y + screenRect.height + (backgroundHeight * 0.1f));
                    maxLengthInfo = Mathf.FloorToInt((float)maxLengthInfo / 2);
                }
                else
                {
                    messagePos = new Vector2(screenRect.x + (screenRect.width * 0.02f), screenRect.y + screenRect.height + (backgroundHeight * 0.15f));
                    infoPos = new Vector2(screenRect.x + (screenRect.width * 0.5f) + (screenRect.width * 0.025f), screenRect.y + screenRect.height + (backgroundHeight * 0.15f));
                }

                if (Event.current.type == EventType.Repaint)
                {
                    DaggerfallUI.DrawTexture(BackgroundRect, BackgroundTexture, ScaleMode.StretchToFill, true, Color.white);

                    if (enemyCondition == 3)
                    {
                        if (lastHit != null)
                        {
                            float barBaseWidth = BarBaseTexture.width * ScaleX;
                            float barBaseHeight = BarBaseTexture.height * ScaleY;

                            float barWidth = BarTexture.width * ScaleX;
                            float barHeight = BarTexture.height * ScaleY;

                            float currentBarWidthHealth = barWidth * lastHitHealth;

                            if (currentBarWidthHealth > lastBarWidthHealth)
                                lastBarWidthHealth = currentBarWidthHealth;
                            else
                                lastBarWidthHealth = Mathf.Lerp(lastBarWidthHealth, currentBarWidthHealth, Time.deltaTime * 2);

                            Rect BarChangeRect = new Rect(screenRect.x + (screenRect.width * 0.75f) - (barWidth * 0.5f), screenRect.y + screenRect.height - (barHeight * 0.5f) + (backgroundHeight * 0.725f), lastBarWidthHealth, barHeight);

                            if (messageBox == 1)
                            {
                                BarBaseRect = new Rect(screenRect.x + (screenRect.width * 0.125f) - (barBaseWidth * 0.5f), screenRect.y + screenRect.height - (barBaseHeight * 0.5f) + (backgroundHeight * 0.8f), barBaseWidth, barBaseHeight);
                                BarChangeRect = new Rect(screenRect.x + (screenRect.width * 0.125f) - (barWidth * 0.5f), screenRect.y + screenRect.height - (barHeight * 0.5f) + (backgroundHeight * 0.8f), lastBarWidthHealth, barHeight);
                                BarRect = new Rect(screenRect.x + (screenRect.width * 0.125f) - (barWidth * 0.5f), screenRect.y + screenRect.height - (barHeight * 0.5f) + (backgroundHeight * 0.8f), currentBarWidthHealth, barHeight);
                            }
                            else
                            {
                                BarBaseRect = new Rect(screenRect.x + (screenRect.width * 0.75f) - (barBaseWidth * 0.5f), screenRect.y + screenRect.height - (barBaseHeight * 0.5f) + (backgroundHeight * 0.725f), barBaseWidth, barBaseHeight);
                                BarChangeRect = new Rect(screenRect.x + (screenRect.width * 0.75f) - (barWidth * 0.5f), screenRect.y + screenRect.height - (barHeight * 0.5f) + (backgroundHeight * 0.725f), lastBarWidthHealth, barHeight);
                                BarRect = new Rect(screenRect.x + (screenRect.width * 0.75f) - (barWidth * 0.5f), screenRect.y + screenRect.height - (barHeight * 0.5f) + (backgroundHeight * 0.725f), currentBarWidthHealth, barHeight);
                            }


                            DaggerfallUI.DrawTexture(BarBaseRect, BarBaseTexture, ScaleMode.StretchToFill, true, Color.white);
                            DaggerfallUI.DrawTexture(BarChangeRect, BarTexture, ScaleMode.StretchToFill, true, lastBarWidthColor);
                            DaggerfallUI.DrawTexture(BarRect, BarTexture, ScaleMode.StretchToFill, true, Color.white);
                        }
                    }

                    //Draw Tooltip
                    if (!String.IsNullOrEmpty(info))
                    {
                        List<string> lines = WrapTextTooltip(info, textScale, maxLengthInfo, 1);
                        for (int i = 0; i < lines.Count; i++)
                        {
                            DaggerfallUI.DefaultFont.DrawText(lines[i], infoPos + (Vector2.up * (i * spaceInfo)), textScale, DaggerfallUI.DaggerfallDefaultTextColor, DaggerfallUI.DaggerfallDefaultShadowColor, Vector2.one);
                        }
                    }
                }
            }
            else
            {
                backgroundWidth = MessageBackgroundTexture.width * ScaleX;

                if (lastBackgroundHeight != 0)
                    backgroundHeight = lastBackgroundHeight;
                else
                    backgroundHeight = MessageBackgroundTexture.height * ScaleY;

                BackgroundRect = new Rect(screenRect.x + (screenRect.width * 0.5f) - (backgroundWidth * 0.5f), screenRect.y + screenRect.height - backgroundHeight, backgroundWidth, backgroundHeight);
                messagePos = new Vector2(screenRect.x + (screenRect.width * 0.25f) + (screenRect.width * 0.02f), screenRect.y + screenRect.height - backgroundHeight + (backgroundHeight * 0.15f));

                if (Event.current.type == EventType.Repaint)
                {
                    DaggerfallUI.DrawTexture(BackgroundRect, MessageBackgroundTexture, ScaleMode.StretchToFill, true, Color.white);
                }
            }

            //Draw Messages
            int currentLines = 0;
            for (int i = 0; i < messages.Count; i++)
            {
                List<string> lines = WrapText(messages[i], textScale, maxLengthMessages);

                Color messageColor = DaggerfallUI.DaggerfallDefaultShadowColor;
                Color messageColorShadow = Color.black;

                if (messageGradient)
                {
                    messageColor = i == 0 ? DaggerfallUI.DaggerfallDefaultTextColor : DaggerfallUI.DaggerfallDefaultShadowColor;
                    messageColorShadow = i == 0 ? DaggerfallUI.DaggerfallDefaultShadowColor : Color.black;

                    if (i != 0)
                    {
                        Color messageColorEnd = messageColor * 2;
                        Color messageColorShadowEnd = messageColor * 2;
                        messageColor = Color.Lerp(messageColor, messageColorEnd, ((float)i - 1) / (float)messagesMaxCount);
                        messageColorShadow = Color.Lerp(messageColorShadow, messageColorShadowEnd, ((float)i - 1) / (float)messagesMaxCount);
                    }
                }

                if (Event.current.type == EventType.Repaint)
                {
                    for (int line = 0; line < lines.Count; line++)
                    {
                        if (currentLines >= messagesMaxCount)
                            break;

                        DaggerfallUI.DefaultFont.DrawText(lines[line], messagePos + (Vector2.up * ((i + line + extraLines) * spaceMessages)), textScale, messageColor, messageColorShadow, Vector2.one);
                        currentLines++;
                    }
                    extraLines += lines.Count - 1;
                }
            }

        }
    }
}
