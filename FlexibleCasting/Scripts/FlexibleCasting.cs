using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using System.Reflection;
using UnityEngine;
using WorldwalkingMod;

namespace FlexibleCastingMod
{
    public class FlexibleCasting : MonoBehaviour
    {
        private static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<FlexibleCasting>();

            mod.IsReady = true;
        }

        KeyCode castKeyCode = KeyCode.Q;
        KeyCode abortKeyCode = KeyCode.Tab;
        bool attackToCast = false;
        bool blockInstantCast = false;
        bool blockNoAnim = false;
        int select = 0; //0 = ready, 1 = set last only, 2 = ready and set last
        int recast = 0; //0 = ready, 1 = on press, 2 = on release
        int activate = 0; //0 = none, 1 = 

        EntityEffectManager entityEffectManager;
        DaggerfallEntityBehaviour entityBehaviour;
        FPSSpellCasting fpsSpellcasting;

        FieldInfo EntityEffectManager_CastInProgress;
        FieldInfo EntityEffectManager_ReadySpellDoesNotCostSpellPoints;
        FieldInfo EntityEffectManager_ReadySpellCastingCost;
        FieldInfo EntityEffectManager_LastSpell;
        FieldInfo EntityEffectManager_LastReadySpellCastingCost;
        FieldInfo EntityEffectManager_InstantCast;

        MethodInfo EntityEffectManager_RaiseOnNewReadySpell;

        MethodInfo EntityEffectManager_RemovePendingBundles;
        MethodInfo EntityEffectManager_DoConstantEffects;

        float refreshModsTimer = 0;
        float refreshModsDelay = 0.2f;
        MethodInfo EntityEffectManager_UpdateEntityMods;

        FieldInfo EntityEffectManager_WipeAllBundlesBool;
        MethodInfo EntityEffectManager_WipeAllBundles;

        MethodInfo EntityEffectManager_PassiveSpecialsCheck;
        MethodInfo EntityEffectManager_CastNoAnimSpell;
        MethodInfo EntityEffectManager_CastReadySpell;
        MethodInfo EntityEffectManager_AbortReadySpell;

        Mod tob;
        KeyCode tobAttackKeyCode = KeyCode.None;

        EntityEffectBundle PreviousLastSpellBundle = null;
        EntityEffectBundle PreviousReadySpellBundle = null;

        public bool showLastSpell;
        public int scaleToScreen = 1;   //0 = Do not scale, 1 = Scale to screen height, 2 = Scale to screen dimensions;
        public int nativeScreenWidth = 320;
        public int nativeScreenHeight = 200;
        public Rect screenRect;
        public float screenScaleX;
        public float screenScaleY;
        Rect spellIconRect = new Rect();
        Texture2D spellIconTexture;
        public float spellIconScaleOffset;
        public bool showSpellTargetType = true;
        Rect spellTargetRect = new Rect();
        Texture2D spellTargetTexture;
        public bool showSpellElement = true;
        Rect spellElementRect = new Rect();
        Texture2D spellElementTexture;
        public int iconLayout = 1;   //0 = Horizontal, 1 = Vertical;
        public Vector2 spellIconOffset = Vector2.zero;
        public bool spellLabel;
        public float spellLabelScaleOffset;
        public Vector2 spellLabelOffset = Vector2.zero;
        Vector2 spellLabelPosition = Vector2.zero;
        Vector2 spellLabelScale = Vector2.zero;
        string spellLabelText = string.Empty;

        private void Start()
        {
            entityEffectManager = GameManager.Instance.PlayerEntityBehaviour.GetComponent<EntityEffectManager>();
            entityBehaviour = GameManager.Instance.PlayerEntityBehaviour;
            fpsSpellcasting = GameManager.Instance.PlayerSpellCasting;

            MethodInfo[] methods = entityEffectManager.GetType().GetMethods(BindingFlags.NonPublic |
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.IgnoreCase |
                BindingFlags.Instance);
            if (methods.Length > 0)
            {
                foreach (MethodInfo method in methods)
                {
                    if (method.Name == "RemovePendingBundles")
                        EntityEffectManager_RemovePendingBundles = method;

                    if (method.Name == "DoConstantEffects")
                        EntityEffectManager_DoConstantEffects = method;

                    if (method.Name == "UpdateEntityMods")
                        EntityEffectManager_UpdateEntityMods = method;

                    if (method.Name == "WipeAllBundles")
                        EntityEffectManager_WipeAllBundles = method;

                    if (method.Name == "PassiveSpecialsCheck")
                        EntityEffectManager_PassiveSpecialsCheck = method;

                    if (method.Name == "CastNoAnimSpell")
                        EntityEffectManager_CastNoAnimSpell = method;

                    if (method.Name == "CastReadySpell")
                        EntityEffectManager_CastReadySpell = method;

                    if (method.Name == "AbortReadySpell")
                        EntityEffectManager_AbortReadySpell = method;

                    if (method.Name == "RaiseOnNewReadySpell")
                        EntityEffectManager_RaiseOnNewReadySpell = method;
                }
            }

            FieldInfo[] fields = entityEffectManager.GetType().GetFields(BindingFlags.NonPublic |
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.IgnoreCase |
                BindingFlags.Instance);
            if (fields.Length > 0)
            {
                foreach (FieldInfo field in fields)
                {
                    if (field.Name == "castInProgress")
                        EntityEffectManager_CastInProgress = field;

                    if (field.Name == "readySpellDoesNotCostSpellPoints")
                        EntityEffectManager_ReadySpellDoesNotCostSpellPoints = field;

                    if (field.Name == "readySpellCastingCost")
                        EntityEffectManager_ReadySpellCastingCost = field;

                    if (field.Name == "instantCast")
                        EntityEffectManager_InstantCast = field;

                    if (field.Name == "wipeAllBundles")
                        EntityEffectManager_WipeAllBundlesBool = field;

                    if (field.Name == "lastSpell")
                        EntityEffectManager_LastSpell = field;

                    if (field.Name == "lastReadySpellCastingCost")
                        EntityEffectManager_LastReadySpellCastingCost = field;
                }
            }

            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();

            //check if Tome of Battle is installed
            tob = ModManager.Instance.GetModFromGUID("a166c215-0a5a-4582-8bf3-8be8df80d5e5");
            if (tob != null)
            {
                ModManager.Instance.SendModMessage(tob.Title, "getSwingKeyCode", null, (string message, object data) =>
                {
                    tobAttackKeyCode = (KeyCode)data;
                });
            }
        }

        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("Main"))
            {
                castKeyCode = SetKeyFromText(settings.GetString("Main", "CastSpellInput"));
                abortKeyCode = SetKeyFromText(settings.GetString("Main", "OpenSpellbookInput"));
                attackToCast = settings.GetValue<bool>("Main", "AttackToCast");
                blockInstantCast = settings.GetValue<bool>("Main", "BlockInstantCastSpells");
                blockNoAnim = settings.GetValue<bool>("Main", "BlockNoAnimSpells");
                select = settings.GetInt("Main", "SelectSpellBehaviour");
                recast = settings.GetInt("Main", "RecastReadySpellBehaviour");
                activate = settings.GetInt("Main", "ActivateInputBehaviour");
            }
            if (change.HasChanged("LastSpellIndicator"))
            {
                showLastSpell = settings.GetBool("LastSpellIndicator", "Enable");
                scaleToScreen = settings.GetInt("LastSpellIndicator", "ScalingMode");
                spellIconOffset = new Vector2(settings.GetTupleFloat("LastSpellIndicator", "IconPosition").First, settings.GetTupleFloat("LastSpellIndicator", "IconPosition").Second);
                spellIconScaleOffset = settings.GetInt("LastSpellIndicator", "IconScale") / 100f;
                showSpellTargetType = settings.GetBool("LastSpellIndicator", "ShowSpellTargetType");
                showSpellElement = settings.GetBool("LastSpellIndicator", "ShowSpellElement");
                iconLayout = settings.GetInt("LastSpellIndicator", "IconLayout");
                spellLabel = settings.GetBool("LastSpellIndicator", "ShowSpellNameLabel");
                spellLabelOffset = new Vector2(settings.GetTupleFloat("LastSpellIndicator", "LabelOffset").First, settings.GetTupleFloat("LastSpellIndicator", "LabelOffset").Second);
                spellLabelScaleOffset = settings.GetInt("LastSpellIndicator", "LabelScale")/100f;
                UpdateSpellIcon();
            }
        }

        private KeyCode SetKeyFromText(string text)
        {
            Debug.Log("Setting Key");

            foreach (KeyCode keyCode in InputManager.Instance.KeyCodeList)
            {
                if (keyCode.ToString() == text)
                    return keyCode;
            }

            Debug.Log("Detected an invalid key code. Setting to default.");
            return KeyCode.None;
        }

        bool Attacking
        {
            get
            {
                if (tob != null)
                    return InputManager.Instance.GetKeyDown(tobAttackKeyCode);
                else
                    return InputManager.Instance.ActionStarted(InputManager.Actions.SwingWeapon);
            }
        }

        private void Update()
        {
            // Do nothing if game not in play, or load in progress
            if (!GameManager.Instance.IsPlayingGame() || SaveLoadManager.Instance.LoadInProgress)
                return;

            //disable effect manager to stop Update from running
            if (entityEffectManager.enabled)
                entityEffectManager.enabled = false;

            //custom entity effect manager code
            // Remove any bundles pending deletion
            EntityEffectManager_RemovePendingBundles.Invoke(entityEffectManager, null);

            // Run any per-frame constant effects
            EntityEffectManager_DoConstantEffects.Invoke(entityEffectManager, null);

            // Refresh mods more frequently than magic rounds, but not too frequently
            refreshModsTimer += Time.deltaTime;
            if (refreshModsTimer > refreshModsDelay)
            {
                EntityEffectManager_UpdateEntityMods.Invoke(entityEffectManager, null);
                refreshModsTimer = 0;
            }

            // Wipe all bundles if scheduled - doing here ensures not currently iterating bundles during a magic round
            if ((bool)EntityEffectManager_WipeAllBundlesBool.GetValue(entityEffectManager))
            {
                EntityEffectManager_WipeAllBundles.Invoke(entityEffectManager, null);
                EntityEffectManager_WipeAllBundlesBool.SetValue(entityEffectManager, false);
                return;
            }

            //check for changes in spell
            if (PreviousLastSpellBundle != entityEffectManager.LastSpell || PreviousReadySpellBundle != entityEffectManager.ReadySpell)
                UpdateSpellIcon();

            // Player can cast a spell, recast last spell, or abort current spell
            // Player must always have passive specials effect
            EntityEffectManager_PassiveSpecialsCheck.Invoke(entityEffectManager, null);

            // Fire no anim spells
            if (entityEffectManager.ReadySpell != null && entityEffectManager.ReadySpell.Settings.NoCastingAnims)
            {
                //if (select == 1 && recast != 0 && !InputManager.Instance.GetKey(castKeyCode) && !fpsSpellcasting.IsPlayingAnim)
                if (((select == 1 && recast != 0 && blockNoAnim) || blockNoAnim) && !(bool)EntityEffectManager_ReadySpellDoesNotCostSpellPoints.GetValue(entityEffectManager) && !InputManager.Instance.GetKey(castKeyCode) && !fpsSpellcasting.IsPlayingAnim)
                {
                    //set last spell
                    EntityEffectManager_LastSpell.SetValue(entityEffectManager, entityEffectManager.ReadySpell);
                    EntityEffectManager_LastReadySpellCastingCost.SetValue(entityEffectManager, EntityEffectManager_ReadySpellCastingCost.GetValue(entityEffectManager));

                    //abort spell
                    EntityEffectManager_AbortReadySpell.Invoke(entityEffectManager, null);
                    return;
                }
                else
                {
                    EntityEffectManager_CastNoAnimSpell.Invoke(entityEffectManager, null);
                    return;
                }
            }

            // Fire instant cast spells
            if (entityEffectManager.ReadySpell != null && (bool)EntityEffectManager_InstantCast.GetValue(entityEffectManager))
            {
                //if (select == 1 && recast != 0 && blockInstantCast && !InputManager.Instance.GetKey(castKeyCode) && !fpsSpellcasting.IsPlayingAnim)
                if (((select == 1 && recast != 0 && blockInstantCast) || blockInstantCast) && !(bool)EntityEffectManager_ReadySpellDoesNotCostSpellPoints.GetValue(entityEffectManager) && !InputManager.Instance.GetKey(castKeyCode) && !fpsSpellcasting.IsPlayingAnim)
                {
                    //set last spell
                    EntityEffectManager_LastSpell.SetValue(entityEffectManager, entityEffectManager.ReadySpell);
                    EntityEffectManager_LastReadySpellCastingCost.SetValue(entityEffectManager, EntityEffectManager_ReadySpellCastingCost.GetValue(entityEffectManager));

                    //abort spell
                    EntityEffectManager_AbortReadySpell.Invoke(entityEffectManager, null);
                    return;
                }
                else
                {
                    EntityEffectManager_CastReadySpell.Invoke(entityEffectManager, null);
                    return;
                }
            }

            // Cast spell
            if (InputManager.Instance.GetKeyDown(castKeyCode))
                {
                if (entityEffectManager.ReadySpell != null)
                {
                    EntityEffectManager_CastReadySpell.Invoke(entityEffectManager, null);
                    return;
                }
                else if (entityEffectManager.LastSpell != null && !GameManager.Instance.PlayerSpellCasting.IsPlayingAnim)
                {
                    //input recasts spell if possible
                    if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.MiscItems, (int)MiscItems.Spellbook))
                    {
                        SetReadySpell(entityEffectManager.LastSpell);

                        //instantly cast recasted readied spell
                        if (recast == 1)
                            EntityEffectManager_CastReadySpell.Invoke(entityEffectManager, null);
                    }
                    else
                        DaggerfallUI.AddHUDText(TextManager.Instance.GetLocalizedText("noSpellbook"));
                    return;
                }
                else if (entityEffectManager.ReadySpell == null && entityEffectManager.LastSpell == null && !GameManager.Instance.PlayerSpellCasting.IsPlayingAnim)
                {
                    //input opens spellbook if there is no readied spell nor last cast spell
                    if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.MiscItems, (int)MiscItems.Spellbook))
                    {
                        DaggerfallUI.PostMessage(DaggerfallUIMessages.dfuiOpenSpellBookWindow);
                    }
                    else
                        DaggerfallUI.AddHUDText(TextManager.Instance.GetLocalizedText("noSpellbook"));
                    return;
                }
            }

            //cast recasted readied spell on input release
            if (recast == 2 && InputManager.Instance.GetKeyUp(castKeyCode))
            {
                if (entityEffectManager.ReadySpell != null)
                {
                    EntityEffectManager_CastReadySpell.Invoke(entityEffectManager, null);
                    return;
                }
            }

            //Abort spell
            if (InputManager.Instance.GetKeyDown(abortKeyCode))
            {
                if (entityEffectManager.ReadySpell != null)
                    EntityEffectManager_AbortReadySpell.Invoke(entityEffectManager, null);
                else
                {
                    //Abort Spell opens spellbook if there is no readied spell
                    if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.MiscItems, (int)MiscItems.Spellbook))
                    {
                        DaggerfallUI.PostMessage(DaggerfallUIMessages.dfuiOpenSpellBookWindow);
                    }
                    else
                        DaggerfallUI.AddHUDText(TextManager.Instance.GetLocalizedText("noSpellbook"));
                    return;
                }
            }

            // Activate to cast readied spell
            if (activate < 2 && InputManager.Instance.ActionStarted(InputManager.Actions.ActivateCenterObject) && entityEffectManager.ReadySpell != null)
            {
                if (activate == 1)
                    EntityEffectManager_AbortReadySpell.Invoke(entityEffectManager, null);
                else
                    EntityEffectManager_CastReadySpell.Invoke(entityEffectManager, null);
                return;
            }

            // Attack to cast readied spell
            if (attackToCast && Attacking && entityEffectManager.ReadySpell != null)
            {
                EntityEffectManager_CastReadySpell.Invoke(entityEffectManager, null);
                return;
            }

            //set readied spell as last and abort if necessary
            if ((select == 1 || select == 2) && recast != 0)
            {
                if (entityEffectManager.ReadySpell != null && !(bool)EntityEffectManager_ReadySpellDoesNotCostSpellPoints.GetValue(entityEffectManager) && !InputManager.Instance.GetKey(castKeyCode) && !fpsSpellcasting.IsPlayingAnim)
                {
                    //set last spell
                    EntityEffectManager_LastSpell.SetValue(entityEffectManager, entityEffectManager.ReadySpell);
                    EntityEffectManager_LastReadySpellCastingCost.SetValue(entityEffectManager, EntityEffectManager_ReadySpellCastingCost.GetValue(entityEffectManager));

                    //abort spell
                    if (select == 1)
                        EntityEffectManager_AbortReadySpell.Invoke(entityEffectManager, null);

                    return;
                }
            }
        }

        bool SilenceCheck()
        {
            if (entityBehaviour.Entity.IsSilenced)
            {
                // Output "You are silenced." if the host manager is player
                // Just to let them know why casting isn't working
                if (entityBehaviour == GameManager.Instance.PlayerEntityBehaviour)
                    DaggerfallUI.AddHUDText(TextManager.Instance.GetLocalizedText("youAreSilenced"), 1.5f);

                entityEffectManager.ReadySpell = null;
                return true;
            }

            return false;
        }

        public bool SetReadySpell(EntityEffectBundle spell, bool noSpellPointCost = false)
        {
            // Do nothing if silenced or cast already in progress
            if ((!noSpellPointCost && SilenceCheck()) || (bool)EntityEffectManager_CastInProgress.GetValue(entityEffectManager))
                return false;

            // Spell must appear valid
            if (spell == null || spell.Settings.Version < 1)
                return false;

            //By default, enemy spell costs are calculated using player-character skill levels.
            //Mods can alter this to use enemy skills instead.
            DaggerfallEntity casterEntity = entityBehaviour.Entity;

            // Get spellpoint costs of this spell
            (int _, int spellPointCost) = FormulaHelper.CalculateTotalEffectCosts(spell.Settings.Effects, spell.Settings.TargetType, casterEntity, spell.Settings.MinimumCastingCost);
            EntityEffectManager_ReadySpellCastingCost.SetValue(entityEffectManager, spellPointCost);

            // Allow casting spells of any cost if entity is player and godmode enabled
            bool godModeCast = GameManager.Instance.PlayerEntity.GodMode;

            // Enforce spell point costs - Daggerfall does this when setting ready spell
            // Classic does not enforce this for enemies, they can cast any spell as long as they still have at least 1 spell point.
            // Doing the same here. This also matters for classic AI combat logic, as it uses the existence of any remaining spell points
            // to determine whether or not it can still cast spells.
            if (entityBehaviour.Entity.CurrentMagicka < spellPointCost && !godModeCast && !noSpellPointCost)
            {
                DaggerfallUI.AddHUDText(TextManager.Instance.GetLocalizedText("youDontHaveTheSpellPoints"));

                entityEffectManager.ReadySpell = null;
                EntityEffectManager_ReadySpellCastingCost.SetValue(entityEffectManager, 0);
                return false;
            }

            // Assign spell - caster only spells are cast instantly
            entityEffectManager.ReadySpell = spell;
            object[] readySpellParams = new object[1] { entityEffectManager.ReadySpell };
            EntityEffectManager_RaiseOnNewReadySpell.Invoke(entityEffectManager, readySpellParams);
            EntityEffectManager_ReadySpellDoesNotCostSpellPoints.SetValue(entityEffectManager,noSpellPointCost);
            if (entityEffectManager.ReadySpell.Settings.TargetType == TargetTypes.CasterOnly)
                EntityEffectManager_InstantCast.SetValue(entityEffectManager, true);

            if (!(bool)EntityEffectManager_InstantCast.GetValue(entityEffectManager))
            {
                if (recast == 0)
                    DaggerfallUI.AddHUDText(TextManager.Instance.GetLocalizedText("pressButtonToFireSpell"), 0.4f);

                //Add another message for hold and release?
            }

            return true;
        }

        void UpdateSpellIcon()
        {
            if (DaggerfallUI.Instance.CustomScreenRect != null)
                screenRect = DaggerfallUI.Instance.CustomScreenRect.Value;
            else
                screenRect = new Rect(0, 0, Screen.width, Screen.height);

            if (scaleToScreen == 2) //Scale to whole screen
            {
                screenScaleY = (float)screenRect.height / nativeScreenHeight;
                screenScaleX = (float)screenRect.width / nativeScreenWidth;
            }
            else if (scaleToScreen == 1)    //Scale only to screen height, maintaining aspect ratio
            {
                screenScaleY = (float)screenRect.height / nativeScreenHeight;
                screenScaleX = screenScaleY;
            }
            else //Do not scale to screen
            {
                screenScaleY = 1;
                screenScaleX = 1;
            }

            screenScaleX *= spellIconScaleOffset;
            screenScaleY *= spellIconScaleOffset;

            if (entityEffectManager.ReadySpell != null)
            {
                spellIconTexture = DaggerfallUI.Instance.SpellIconCollection.GetSpellIcon(entityEffectManager.ReadySpell.Settings.Icon);
                spellTargetTexture = DaggerfallUI.Instance.SpellIconCollection.GetSpellTargetIcon(entityEffectManager.ReadySpell.Settings.TargetType);
                spellElementTexture = DaggerfallUI.Instance.SpellIconCollection.GetSpellElementIcon(entityEffectManager.ReadySpell.Settings.ElementType);
                spellLabelText = entityEffectManager.ReadySpell.Settings.Name;
            }
            else if (entityEffectManager.LastSpell != null)
            {
                spellIconTexture = DaggerfallUI.Instance.SpellIconCollection.GetSpellIcon(entityEffectManager.LastSpell.Settings.Icon);
                spellTargetTexture = DaggerfallUI.Instance.SpellIconCollection.GetSpellTargetIcon(entityEffectManager.LastSpell.Settings.TargetType);
                spellElementTexture = DaggerfallUI.Instance.SpellIconCollection.GetSpellElementIcon(entityEffectManager.LastSpell.Settings.ElementType);
                spellLabelText = entityEffectManager.LastSpell.Settings.Name;
            }

            if (spellIconTexture == null)
                return;

            spellIconRect.x = screenRect.x + spellIconOffset.x * screenRect.width;
            spellIconRect.y = screenRect.y + spellIconOffset.y * screenRect.height;
            spellIconRect.width = 16 * screenScaleX;
            spellIconRect.height = 16 * screenScaleY;
            
            spellTargetRect = spellIconRect;
            if (showSpellTargetType)
            {
                if (iconLayout == 1)
                    spellTargetRect.y += spellIconRect.height;
                else
                    spellTargetRect.x += spellIconRect.width;
                spellTargetRect.width = 25 * screenScaleX;
                spellTargetRect.height = 16 * screenScaleY;
            }

            spellElementRect = spellTargetRect;
            if (showSpellElement)
            {
                if (iconLayout == 1)
                    spellElementRect.y += spellTargetRect.height;
                else
                    spellElementRect.x += spellTargetRect.width;
                spellElementRect.width = 16 * screenScaleX;
                spellElementRect.height = 16 * screenScaleY;
            }

            spellLabelPosition.x = spellIconRect.x + (spellLabelOffset.x * screenRect.width);
            spellLabelPosition.y = spellIconRect.y + (spellLabelOffset.y * screenRect.height);
            spellLabelScale.x = spellLabelScaleOffset * screenScaleX;
            spellLabelScale.y = spellLabelScaleOffset * screenScaleY;
        }

        private void OnGUI()
        {
            if (GameManager.IsGamePaused || InputManager.Instance.IsPaused || SaveLoadManager.Instance.LoadInProgress)
                return;

            GUI.depth = -30;

            if (showLastSpell)
            {
                //show spell icon for currently readied spell, or last spell if no spell readied
                if (entityEffectManager.LastSpell != null || entityEffectManager.ReadySpell != null)
                {
                    if (spellIconTexture != null)
                    {
                        DaggerfallUI.DrawTexture(spellIconRect, spellIconTexture);

                        if (showSpellTargetType)
                            DaggerfallUI.DrawTexture(spellTargetRect, spellTargetTexture);

                        if (showSpellElement)
                            DaggerfallUI.DrawTexture(spellElementRect, spellElementTexture);
                    }

                    if (spellLabel)
                    {
                        DaggerfallUI.DefaultFont.DrawText(spellLabelText, spellLabelPosition, spellLabelScale, DaggerfallUI.DaggerfallDefaultTextColor, DaggerfallUI.DaggerfallDefaultShadowColor, Vector2.one * 1 * screenScaleY);
                    }
                }
            }
        }
    }
}
