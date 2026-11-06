using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Utility.AssetInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FootWidgetMod
{
    public class FPSWeaponStandalone : MonoBehaviour
    {
        DaggerfallAudioSource dfAudioSource;

        public KeyCode keyCode = KeyCode.None;
        public SoundClips DrawWeaponSound = SoundClips.DrawWeapon;
        public SoundClips SwingWeaponSound = SoundClips.SwingMediumPitch;

        public bool FlipHorizontal;

        public bool drawIdle = true;

        int GetHitFrame()
        {
            if (currentWeaponType == WeaponTypes.Bow)
                return 5;
            else
                return 2;
        }

        float moveTimer = 10;
        class WeaponAtlas
        {
            public string FileName { get; set; }
            public MetalTypes MetalType { get; set; }
            public Texture2D AtlasTexture { get; set; }
            public Rect[] WeaponRects { get; set; }
            public RecordIndex[] WeaponIndices { get; set; }
        }
        class CustomWeaponAnimation
        {
            public string FileName { get; set; }
            public MetalTypes MetalType { get; set; }
            public Dictionary<int, Texture2D> Textures { get; set; }
        }

        CifRciFile cifFile;

        public DaggerfallUnityItem SpecificWeapon;
        bool loadAtlas = false;

        public bool ShowWeapon = true;

        WeaponAtlas weaponAtlas;
        readonly WeaponAtlas[] weaponAtlasCache = new WeaponAtlas[2];
        readonly CustomWeaponAnimation[] customWeaponAnimationCache = new CustomWeaponAnimation[2];

        WeaponAnimation[] weaponAnims;
        public WeaponStates currentWeaponState = WeaponStates.Idle;
        int currentFrame = 0;
        public WeaponTypes currentWeaponType;
        public MetalTypes currentMetalType;
        public int currentTemplateIndex;
        public float currentWeaponReach;

        Rect weaponPosition;
        float weaponOffsetHeight;
        Rect screenRect;
        float weaponScaleX;
        float weaponScaleY;

        const int nativeScreenWidth = 320;
        const int nativeScreenHeight = 200;

        float animTickTime;
        Rect curAnimRect;

        float lastScreenWidth, lastScreenHeight;
        bool lastLargeHUDSetting, lastLargeHUDDockSetting;
        bool lastSheathed;
        float lastWeaponOffsetHeight;
        bool lastDoubleScale;
        bool lastEnchanted;

        Dictionary<int, Texture2D> customTextures = new Dictionary<int, Texture2D>();
        Texture2D curCustomTexture;
        public Vector2 Position { get; set; } = Vector2.zero;   //Moves the sprite in screen distance
        public Vector2 Scale { get; set; } = Vector2.one;
        public Vector2 Offset { get; set; } = Vector2.zero;     //Moves the sprite relative to its own dimensions ("Offset.x = 0.5f" will move "sprite.width * 0.5f"). Applied after Scale.

        IEnumerator animating;
        bool animatingCancel;

        public bool IsAttacking
        {
            get
            {
                return animating != null;
            }
        }

        public bool swing = true;
        public int swingWindup = 0; //0 = Hide, 1 = Idle, 2 = First Frame
        public int swingRecovery = 0; //0 = Hide, 1 = LastFrame
        public float swingSpeed = 1;
        public bool swingAlignmentOverride = true;
        public bool swingRecoveryOverride = true;
        public bool swingNoDaggerRight = true;

        public bool stepTransforms;
        public int stepLength = 8;

        public bool doubleScale;
        public bool trueSize;
        public int textureScaleFactor = 1;

        public bool recoil;
        public float recoilChance = 0.5f;
        public int recoilCondition = 0;    //0 = hits only, 1 = hits and parries, 2 = all attacks
        public bool hasCurrentAttackHit;
        public bool recoilEnvironment;
        public bool playMissVFXEntity;
        public bool playMissVFXEnvironment;
        public int playMissVFXPos; //0 = body, 1 = crosshair

        public bool mirrorBows;
        public bool mirrorTwoHandedSwords;
        public bool mirrorTwoHandedAxes;
        public bool mirrorTwoHandedBlunts;

        //Mod compatibility
        public bool isInThirdPerson;
        public float FOV;

        //custom weapon override
        public struct WeaponOverride
        {
            public string Filename;
            public bool HasMagicTextures;
            public bool OverrideRecovery;
            public bool OverrideAlignment;
            public bool OverrideMirror;
            public bool OverrideOffset;

            public WeaponOverride(string filename, bool hasMagicTextures, bool overrideRecovery, bool overrideAlignment, bool overrideMirror, bool overrideOffset)
            {
                Filename = filename;
                HasMagicTextures = hasMagicTextures;
                OverrideRecovery = overrideRecovery;
                OverrideAlignment = overrideAlignment;
                OverrideMirror = overrideMirror;
                OverrideOffset = overrideOffset;
            }
        }
        //itemTemplateIndex and Filename
        Dictionary<int, WeaponOverride> customWeaponOverrides = new Dictionary<int, WeaponOverride>();

        public bool offset = true;
        public float offsetSpeed = 10;
        Vector2 offsetCurrent;
        Vector2 offsetTarget;

        // OnReady
        public event Action OnHitFrame;
        void RaiseOnHitFrameEvent()
        {
            if (OnHitFrame != null)
                OnHitFrame();
        }

        public void OnAttackDamageCalculated(DaggerfallEntity attacker, DaggerfallEntity target, DaggerfallUnityItem weapon, int bodyPart, int damage)
        {
            //if attacker is not the player or there is no target or recoil is disabled, do nothing
            if (attacker != GameManager.Instance.PlayerEntity || target == null)
                return;

            float value = UnityEngine.Random.value;

            if (!recoil)
                return;

            if (damage > 0)
            {
                if (recoil && value < recoilChance && (recoilCondition == 0 || recoilCondition == 1 || recoilCondition == 5))
                    hasCurrentAttackHit = true;
            }
            else
            {
                EnemyEntity targetEnemy = target as EnemyEntity;
                if (targetEnemy != null)
                {

                    if (targetEnemy.MobileEnemy.ParrySounds)
                    {
                        if (recoil && value < recoilChance && (recoilCondition == 1 || recoilCondition == 2 || recoilCondition == 3 || recoilCondition == 5))
                            hasCurrentAttackHit = true;
                    }
                    else
                    {
                        if (recoil && value < recoilChance && (recoilCondition == 3 || recoilCondition == 4 || recoilCondition == 5))
                            hasCurrentAttackHit = true;
                    }
                }
            }
        }

        void Start()
        {
            // Ensure cif reader is ready
            if (cifFile == null)
            {
                cifFile = new CifRciFile();
                cifFile.Palette.Load(Path.Combine(DaggerfallUnity.Instance.Arena2Path, cifFile.PaletteName));
            }

            dfAudioSource = gameObject.AddComponent<DaggerfallAudioSource>();

            FOV = GameManager.Instance.MainCamera.fieldOfView;
        }

        void RegisterCustomWeaponFilename(object[] data)
        {
            if (customWeaponOverrides.ContainsKey((int)data[0]))
            {
                customWeaponOverrides[(int)data[0]] = new WeaponOverride((string)data[1], (bool)data[2], (bool)data[3], (bool)data[4], (bool)data[5], (bool)data[6]);
            }
            else
                customWeaponOverrides.Add((int)data[0], new WeaponOverride((string)data[1], (bool)data[2], (bool)data[3], (bool)data[4], (bool)data[5], (bool)data[6]));
        }

        public void OnAttackDirection(WeaponManager.MouseDirections direction, bool delay = false, int delayTicks = 1)
        {
            // Get state based on attack direction
            WeaponStates state;

            switch (direction)
            {
                case WeaponManager.MouseDirections.Down:
                    state = WeaponStates.StrikeDown;
                    break;
                case WeaponManager.MouseDirections.DownLeft:
                    state = WeaponStates.StrikeDownLeft;
                    break;
                case WeaponManager.MouseDirections.Left:
                    state = WeaponStates.StrikeLeft;
                    break;
                case WeaponManager.MouseDirections.Right:
                    state = WeaponStates.StrikeRight;
                    break;
                case WeaponManager.MouseDirections.DownRight:
                    state = WeaponStates.StrikeDownRight;
                    break;
                case WeaponManager.MouseDirections.Up:
                case WeaponManager.MouseDirections.UpLeft:
                case WeaponManager.MouseDirections.UpRight:
                    state = WeaponStates.StrikeUp;
                    break;
                default:
                    return;
            }

            // Do not change if already playing attack animation, unless releasing an arrow (bow & state=up->down)
            PlayAttackAnimation(state, delay, delayTicks);
        }

        public void PlayAttackAnimation(WeaponStates state, bool delay = false, int delayTicks = 1)
        {
            if (animating != null)
            {
                if (!animatingCancel && !delay)
                    return;
                else
                {
                    StopCoroutine(animating);
                    animating = null;

                    if (currentWeaponState != WeaponStates.Idle)
                    {
                        currentFrame = -1;
                        UpdateWeapon();
                    }
                }
            }

            if (currentWeaponType == WeaponTypes.Bow)
                animating = PlayBowAnimation();
            else
            {
                if (swing)
                {
                    if (swingNoDaggerRight && (currentWeaponType == WeaponTypes.Dagger || currentWeaponType == WeaponTypes.Dagger_Magic))
                    {
                        WeaponStates newState = state;
                        if (FlipHorizontal)
                        {
                            if (state == WeaponStates.StrikeLeft)
                                newState = WeaponStates.StrikeRight;
                            else if (state == WeaponStates.StrikeDownLeft)
                                newState = WeaponStates.StrikeDownRight;
                        }
                        else
                        {
                            if (state == WeaponStates.StrikeRight)
                                newState = WeaponStates.StrikeLeft;
                            else if (state == WeaponStates.StrikeDownRight)
                                newState = WeaponStates.StrikeDownLeft;
                        }
                        animating = PlayWeaponAnimation(newState, delay, delayTicks);
                    }
                    else
                        animating = PlayWeaponAnimation(state, delay, delayTicks);
                }
                else
                    animating = PlayVanillaWeaponAnimation(state, delay, delayTicks);
            }

            hasCurrentAttackHit = false;
            StartCoroutine(animating);
        }
        IEnumerator PlayWeaponAnimation(WeaponStates state, bool delay = false, int delayTicks = 1)
        {

            hasCurrentAttackHit = false;
            animatingCancel = false;

            int hitFrame = GetHitFrame();

            int currentTick = 0;
            float tickLength = GetAnimTickTime();

            float tickTimer = 0;
            float tickTime = (tickLength / 5) / swingSpeed;

            float swingTimer = 0;
            float swingTime = tickLength * 6;

            if (delay)
                yield return new WaitForSeconds(tickLength * delayTicks);

            if (swingWindup == 2)
                ChangeWeaponState(state);
            /*else
                ChangeWeaponState(WeaponStates.Idle);*/

            if (swingRecoveryOverride && CheckForRecoveryOverride())
                tickTime *= 0.5f;

            //total swing time
            bool recoveryOverride = false;

            bool specialUnarmed = (currentWeaponType == WeaponTypes.Melee || currentWeaponType == WeaponTypes.Werecreature) && state == WeaponStates.StrikeLeft;

            int swingStage = 0;
            while (swingTimer < swingTime)
            {
                currentTick = Mathf.FloorToInt(swingTimer / tickLength);

                if (swingStage == 0)
                {
                    //startup

                    if (currentTick < hitFrame)
                    {
                        if (swingWindup == 2)
                            currentFrame = 0;
                        else if (swingWindup == 1)
                        {
                            if (CheckForOffsetOverride())
                            {
                                if (FlipHorizontal)
                                {
                                    if (state == WeaponStates.StrikeUp)
                                        offsetTarget = new Vector2(-1, 1);
                                    else if (state == WeaponStates.StrikeDownLeft || state == WeaponStates.StrikeLeft)
                                        offsetTarget = new Vector2(-1, 1);
                                    else if (state == WeaponStates.StrikeDown || state == WeaponStates.StrikeDownRight)
                                        offsetTarget = new Vector2(1, -1);
                                    else if (state == WeaponStates.StrikeRight)
                                        offsetTarget = new Vector2(1, 0);
                                }
                                else
                                {
                                    if (state == WeaponStates.StrikeUp)
                                        offsetTarget = new Vector2(-1, 1);
                                    else if (state == WeaponStates.StrikeRight || state == WeaponStates.StrikeDownRight)
                                        offsetTarget = new Vector2(-1, 1);
                                    else if (state == WeaponStates.StrikeDown || state == WeaponStates.StrikeDownLeft)
                                        offsetTarget = new Vector2(1, -1);
                                    else if (state == WeaponStates.StrikeLeft)
                                        offsetTarget = new Vector2(1, 0);
                                }
                            }
                            else
                            {
                                offsetTarget = new Vector2(0, 1);
                            }
                        }
                        else
                            currentFrame = -1;
                    }
                    else
                    {
                        ChangeWeaponState(state);
                        PlaySwingSound();

                        //do hit frame here

                        //DO ONSWING EVENT HERE
                        RaiseOnHitFrameEvent();

                        //check for static object
                        if (recoil && recoilEnvironment)
                        {
                            if (CheckForEnvDamage())
                                hasCurrentAttackHit = true;
                        }

                        swingStage++;
                    }
                }
                else if (swingStage == 1)
                {
                    //play swing
                    if (hasCurrentAttackHit)
                    {
                        if (currentFrame < hitFrame)
                        {
                            if (tickTimer > tickTime)
                            {
                                offsetTarget = Vector2.zero;
                                offsetCurrent = Vector2.zero;
                                currentFrame++;
                                UpdateWeapon();
                                tickTimer = 0;
                            }
                            else
                                tickTimer += Time.deltaTime;
                        }
                        else
                        {
                            //animatingCancel = true;
                            if (swingRecoveryOverride)
                                recoveryOverride = CheckForRecoveryOverride();
                            swingStage++;
                        }
                    }
                    else
                    {
                        int lastframe = specialUnarmed ? weaponAnims[(int)currentWeaponState].NumFrames - 2 : weaponAnims[(int)currentWeaponState].NumFrames - 1;
                        if (currentFrame < lastframe)
                        {
                            if (tickTimer > tickTime)
                            {
                                offsetTarget = Vector2.zero;
                                offsetCurrent = Vector2.zero;
                                currentFrame++;
                                UpdateWeapon();
                                tickTimer = 0;
                            }
                            else
                                tickTimer += Time.deltaTime;
                        }
                        else
                        {
                            //animatingCancel = true;
                            if (swingRecoveryOverride)
                                recoveryOverride = CheckForRecoveryOverride();
                            swingStage++;
                        }
                    }
                }
                else
                {
                    //recovery
                    if (recoveryOverride || hasCurrentAttackHit || specialUnarmed)
                    {
                        if (currentFrame > 0)
                        {
                            if (tickTimer > tickTime)
                            {
                                offsetCurrent = Vector2.zero;
                                offsetTarget = Vector2.zero;
                                currentFrame--;
                                UpdateWeapon();
                                tickTimer = 0;
                            }
                            else
                                tickTimer += Time.deltaTime;
                        }
                        else
                        {
                            if (swingRecovery == 1)
                                currentFrame = weaponAnims[(int)currentWeaponState].NumFrames - 1;
                            else
                                currentFrame = -1;
                        }
                    }
                    else
                    {
                        if (swingRecovery == 1)
                            currentFrame = weaponAnims[(int)currentWeaponState].NumFrames - 1;
                        else
                            currentFrame = -1;
                    }
                }

                swingTimer += Time.deltaTime;
                yield return null;
            }

            //yield return new WaitForSeconds(tickTime);

            //reset to idle
            ChangeWeaponState(WeaponStates.Idle);

            //set current offset
            float offsetX = 1f;
            if (!CheckForOffsetOverride())
            {
                offsetX = 0f;
            }
            else
            {
                if (state == WeaponStates.StrikeDown || state == WeaponStates.StrikeUp)
                    offsetX = 0f;
                else if (state == WeaponStates.StrikeLeft || state == WeaponStates.StrikeDownLeft)
                    offsetX = -1f;
            }
            offsetCurrent = new Vector2(offsetX, 1f);

            //clear coroutine and reset variables
            hasCurrentAttackHit = false;
            animatingCancel = true;
            animating = null;
        }
        IEnumerator PlayVanillaWeaponAnimation(WeaponStates state, bool delay = false, int delayTicks = 1)
        {
            hasCurrentAttackHit = false;
            animatingCancel = false;

            float tickTime = GetAnimTickTime();

            bool specialUnarmed = (currentWeaponType == WeaponTypes.Melee || currentWeaponType == WeaponTypes.Werecreature) && state == WeaponStates.StrikeLeft;

            if (delay)
                yield return new WaitForSeconds(tickTime * delayTicks);

            ChangeWeaponState(state);
            //yield return new WaitForSeconds(tickTime);

            int lastframe = specialUnarmed ? weaponAnims[(int)currentWeaponState].NumFrames - 2 : weaponAnims[(int)currentWeaponState].NumFrames - 1;
            while (currentFrame < lastframe && !hasCurrentAttackHit)
            {
                offsetTarget = Vector2.zero;
                offsetCurrent = Vector2.zero;

                currentFrame++;
                UpdateWeapon();

                if (currentFrame == GetHitFrame())
                {
                    PlaySwingSound();

                    //DO ONSWING EVENT HERE
                    RaiseOnHitFrameEvent();

                    //check for static object
                    if (recoil && recoilEnvironment)
                    {
                        if (CheckForEnvDamage())
                            hasCurrentAttackHit = true;
                    }
                }

                yield return new WaitForSeconds(tickTime);
            }

            while (currentFrame > 0 && (hasCurrentAttackHit || specialUnarmed))
            {
                offsetTarget = Vector2.zero;
                offsetCurrent = Vector2.zero;

                currentFrame--;
                UpdateWeapon();

                /*if (currentFrame == GetHitFrame())
                    animatingCancel = true;*/

                yield return new WaitForSeconds(tickTime);
            }

            if (swingRecovery == 1)
                currentFrame = weaponAnims[(int)currentWeaponState].NumFrames - 1;
            else
                currentFrame = -1;

            //yield return new WaitForSeconds(tickTime);

            //reset to idle
            ChangeWeaponState(WeaponStates.Idle);

            //set current offset
            float offsetX = 1f;
            if (!CheckForOffsetOverride())
            {
                offsetX = 0f;
            }
            else
            {
                if (state == WeaponStates.StrikeDown || state == WeaponStates.StrikeUp)
                    offsetX = 0f;
                else if (state == WeaponStates.StrikeLeft || state == WeaponStates.StrikeDownLeft)
                    offsetX = -1f;
            }
            offsetCurrent = new Vector2(offsetX, 1f);

            //clear coroutine and reset variables
            animatingCancel = true;
            hasCurrentAttackHit = false;
            animating = null;
        }
        IEnumerator PlayBowAnimation()
        {
            if (DaggerfallUnity.Settings.BowDrawback)
            {
                currentFrame = 0;
                ChangeWeaponState(WeaponStates.StrikeUp);
                UpdateWeapon();


                float drawTime = 12;
                float drawTimer = 0;
                bool drawOver = false;
                //play the draw animation
                while (InputManager.Instance.GetKey(keyCode))
                {
                    if (drawTimer > drawTime || InputManager.Instance.ActionStarted(InputManager.Actions.ActivateCenterObject))
                    {
                        drawOver = true;
                        break;
                    }

                    offsetTarget = Vector2.zero;
                    offsetCurrent = Vector2.zero;

                    if (currentFrame < 3)
                    {
                        currentFrame++;
                        UpdateWeapon();
                        drawTimer += GameManager.classicUpdateInterval;
                        yield return new WaitForSeconds(GameManager.classicUpdateInterval);
                    }
                    else
                    {
                        drawTimer += Time.deltaTime;
                        yield return null;
                    }
                }

                if (!drawOver)
                {
                    ChangeWeaponState(WeaponStates.StrikeDown);
                    UpdateWeapon();
                    yield return new WaitForSeconds(GameManager.classicUpdateInterval);

                    PlaySwingSound();

                    //play the rest of the animation
                    while (currentFrame < weaponAnims[(int)currentWeaponState].NumFrames - 1)
                    {
                        offsetTarget = Vector2.zero;
                        offsetCurrent = Vector2.zero;

                        currentFrame++;
                        UpdateWeapon();
                        yield return new WaitForSeconds(GameManager.classicUpdateInterval);
                    }

                    float timeCooldown = FormulaHelper.GetBowCooldownTime(GameManager.Instance.PlayerEntity);

                    float timerCooldown = 0;
                    //wait for end of cooldown
                    while (timerCooldown < timeCooldown)
                    {
                        offsetTarget = Vector2.up;

                        timerCooldown += Time.deltaTime;

                        yield return null;
                    }
                }
                else
                {
                    //reverse the animation
                    while (currentFrame > 0)
                    {
                        offsetTarget = Vector2.zero;
                        offsetCurrent = Vector2.zero;

                        currentFrame--;
                        UpdateWeapon();
                        yield return new WaitForSeconds(GameManager.classicUpdateInterval);
                    }
                }
                //reset to idle
                currentFrame = 0;
                ChangeWeaponState(WeaponStates.Idle);
                UpdateWeapon();
            }
            else
            {
                currentFrame = 3;
                ChangeWeaponState(WeaponStates.StrikeDown);
                UpdateWeapon();

                yield return new WaitForSeconds(GameManager.classicUpdateInterval);

                PlaySwingSound();

                //play the shoot animation
                while (currentFrame < weaponAnims[(int)currentWeaponState].NumFrames - 1)
                {
                    offsetTarget = Vector2.zero;
                    offsetCurrent = Vector2.zero;

                    currentFrame++;
                    UpdateWeapon();

                    yield return new WaitForSeconds(GameManager.classicUpdateInterval);
                }

                //wait for end of attack
                /*while (ScreenWeapon.IsAttacking())
                {
                    offsetTarget = Vector2.up;
                    yield return null;
                }*/

                //reset to idle
                currentFrame = 3;
                ChangeWeaponState(WeaponStates.StrikeDown);
                UpdateWeapon();
            }

            //set current offset
            offsetCurrent = new Vector2(0, 1);


            //clear coroutine
            animating = null;
        }
        public void ChangeWeaponState(WeaponStates state)
        {
            currentWeaponState = state;

            // Only reset frame to 0 for bows if idle state
            if (currentWeaponType != WeaponTypes.Bow || state == WeaponStates.Idle)
                currentFrame = 0;

            if (state != WeaponStates.Idle)
            {
                offsetCurrent = Vector2.zero;
                offsetTarget = Vector2.zero;
            }

            UpdateWeapon();
        }

        private void OnGUI()
        {
            if (isInThirdPerson)
                return;

            bool updateWeapon = false;

            if (DaggerfallUI.Instance.CustomScreenRect != null)
                screenRect = DaggerfallUI.Instance.CustomScreenRect.Value;
            else
                screenRect = new Rect(0, 0, Screen.width, Screen.height);

            // Must be ready and not loading the game
            if (currentWeaponType == WeaponTypes.None || GameManager.IsGamePaused || SaveLoadManager.Instance.LoadInProgress)
                return;

            // Must have current weapon texture atlas
            if (weaponAtlas == null ||
                (SpecificWeapon != null && (SpecificWeapon.TemplateIndex != currentTemplateIndex || SpecificWeapon.IsEnchanted != lastEnchanted)) ||
                doubleScale != lastDoubleScale) //weapon widget specific code
            {
                LoadWeaponAtlas();
                if (weaponAtlas == null)
                    return;
                updateWeapon = true;
            }

            // Offset weapon by large HUD height when both large HUD and undocked weapon offset enabled
            // Weapon is forced to offset when using docked HUD else it would appear underneath HUD
            // This helps user avoid such misconfiguration or it might be interpreted as a bug
            weaponOffsetHeight = 0;
            if (DaggerfallUI.Instance.DaggerfallHUD != null &&
                DaggerfallUnity.Settings.LargeHUD &&
                (DaggerfallUnity.Settings.LargeHUDUndockedOffsetWeapon || DaggerfallUnity.Settings.LargeHUDDocked))
            {
                weaponOffsetHeight = (int)DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.ScreenHeight;
            }

            // Update weapon when resolution or large HUD state changes
            if (screenRect.width != lastScreenWidth ||
                screenRect.height != lastScreenHeight ||
                DaggerfallUnity.Settings.LargeHUD != lastLargeHUDSetting ||
                DaggerfallUnity.Settings.LargeHUDDocked != lastLargeHUDDockSetting ||
                GameManager.Instance.WeaponManager.Sheathed != lastSheathed ||
                weaponOffsetHeight != lastWeaponOffsetHeight)
            {
                lastScreenWidth = screenRect.width;
                lastScreenHeight = screenRect.height;
                lastLargeHUDSetting = DaggerfallUnity.Settings.LargeHUD;
                lastLargeHUDDockSetting = DaggerfallUnity.Settings.LargeHUDDocked;

                if (GameManager.Instance.WeaponManager.Sheathed != lastSheathed)
                {
                    //sheathed the weapon
                    if (lastSheathed == false)
                    {

                    }
                    else
                    {

                    }
                    lastSheathed = GameManager.Instance.WeaponManager.Sheathed;
                }
                lastWeaponOffsetHeight = weaponOffsetHeight;
                updateWeapon = true;
            }

            // Update weapon state only as needed
            if (updateWeapon)
                UpdateWeapon();

            lastDoubleScale = doubleScale;
            lastEnchanted = SpecificWeapon != null ? SpecificWeapon.IsEnchanted : false;

            if (!drawIdle && currentWeaponState == WeaponStates.Idle)
                return;

            if (Event.current.type.Equals(EventType.Repaint) && currentFrame != -1 && ShowWeapon && !isInThirdPerson && !(!offset && (GameManager.Instance.WeaponManager.Sheathed || GameManager.Instance.PlayerSpellCasting.IsPlayingAnim || GameManager.Instance.PlayerEffectManager.HasReadySpell)))
            {
                // Draw weapon texture behind other HUD elements
                GUI.depth = 1;

                Texture2D tex = curCustomTexture ? curCustomTexture : weaponAtlas.AtlasTexture;
                DaggerfallUI.DrawTextureWithTexCoords(GetWeaponRect(), tex, curAnimRect, true, GameManager.Instance.RightHandWeapon.Tint);
            }
        }
        //Combines the base WeaponPosition Rect with Position, Scale and Offset
        public Rect GetWeaponRect()
        {
            Rect weaponPositionOffset = weaponPosition;

            //scale based on current FOV?
            float zoom = 2 - (GameManager.Instance.MainCamera.fieldOfView / FOV);
            weaponPositionOffset.x *= zoom;
            weaponPositionOffset.y *= zoom;
            weaponPositionOffset.width *= zoom;
            weaponPositionOffset.height *= zoom;

            //Adds the Position to the Rect's position)
            if (FlipHorizontal)
                weaponPositionOffset.x -= Position.x;
            else
                weaponPositionOffset.x += Position.x;
            weaponPositionOffset.y += Position.y;

            if (currentWeaponType != WeaponTypes.Werecreature)
            {
                weaponPositionOffset.width *= Scale.x;
                weaponPositionOffset.height *= Scale.y;
            }

            /*//Adds the Offset to the Rect's position
            weaponPositionOffset.x -= weaponPositionOffset.width * 0.5f;
            weaponPositionOffset.y -= weaponPositionOffset.height * 0.5f;*/

            //Adds the Offset to the Rect's position
            if (FlipHorizontal)
                weaponPositionOffset.x -= weaponPositionOffset.width * Offset.x;
            else
                weaponPositionOffset.x += weaponPositionOffset.width * Offset.x;
            weaponPositionOffset.y += weaponPositionOffset.height * Offset.y;

            if (stepTransforms)
            {
                float length = stepLength * (screenRect.height / 64);
                weaponPositionOffset.x = Snapping.Snap(weaponPositionOffset.x, length);
                weaponPositionOffset.y = Snapping.Snap(weaponPositionOffset.y, length);
            }

            //stop the texture from going higher than its bottom or side edge when idle
            if (currentWeaponState == WeaponStates.Idle)
            {
                weaponPositionOffset.y = Mathf.Clamp(weaponPositionOffset.y, screenRect.height - weaponPositionOffset.height - weaponOffsetHeight, screenRect.height);
                if (FlipHorizontal)
                    weaponPositionOffset.x = Mathf.Clamp(weaponPositionOffset.x, screenRect.x - weaponPositionOffset.width, screenRect.x);
                else
                    weaponPositionOffset.x = Mathf.Clamp(weaponPositionOffset.x, screenRect.x + screenRect.width - weaponPositionOffset.width, screenRect.x + screenRect.width);
            }

            return weaponPositionOffset;
        }

        public void SetWeapon(DaggerfallUnityItem newWeapon)
        {
            if (newWeapon != null)
            {
                //set variables to new weapon

                SpecificWeapon = newWeapon;
                DrawWeaponSound = newWeapon.GetEquipSound();
                SwingWeaponSound = newWeapon.GetSwingSound();

                // Store current weapon
                currentWeaponType = GameManager.Instance.ItemHelper.ConvertItemToAPIWeaponType(SpecificWeapon);
                currentMetalType = GameManager.Instance.ItemHelper.ConvertItemMaterialToAPIMetalType(SpecificWeapon);
                currentTemplateIndex = newWeapon.TemplateIndex;

                LoadWeaponAtlas();

                if (!DaggerfallUnity.Settings.BowDrawback && DaggerfallUnity.Instance.ItemHelper.ConvertItemToAPIWeaponType(SpecificWeapon) == WeaponTypes.Bow)
                {
                    currentFrame = 3;
                    ChangeWeaponState(WeaponStates.StrikeDown);
                }
                else
                    ChangeWeaponState(WeaponStates.Idle);
            }
            else
            {
                //set variables to unarmed

                SpecificWeapon = null;
                DrawWeaponSound = SoundClips.None;
                SwingWeaponSound = SoundClips.SwingHighPitch;

                currentWeaponType = WeaponTypes.Melee;
                currentMetalType = MetalTypes.None;
                currentTemplateIndex = -1;

                LoadWeaponAtlas();

                ChangeWeaponState(WeaponStates.Idle);
            }
        }

        private void LateUpdate()
        {

            Offset = Vector2.zero;

            if (offset)
            {
                bool attacking = animating != null ? true : false;
                if (attacking)
                {
                    Position = Vector2.zero;
                    Scale = Vector2.one;
                }
                else if (GameManager.Instance.RightHandWeapon.IsAttacking())
                {
                    offsetTarget = Vector2.one;
                    Position = Vector2.zero;
                    Scale = Vector2.one;
                }
                else
                {
                    if (GameManager.Instance.WeaponManager.Sheathed)
                        offsetTarget = Vector2.one * 2;
                    else
                        offsetTarget = Vector2.zero;

                    if (GameManager.Instance.WeaponManager.UsingRightHand)
                    {
                        if (GameManager.Instance.WeaponManager.EquipCountdownLeftHand > 0)
                        {
                            offsetTarget = Vector2.one * 2;
                            offsetCurrent = offsetTarget;
                        }
                    }
                    else
                    {
                        if (GameManager.Instance.WeaponManager.EquipCountdownRightHand > 0)
                        {
                            offsetTarget = Vector2.one * 2;
                            offsetCurrent = offsetTarget;
                        }
                    }
                }

                offsetCurrent = Vector2.MoveTowards(offsetCurrent, offsetTarget, Time.deltaTime * (((float)GameManager.Instance.PlayerEntity.Stats.LiveSpeed / 100) * offsetSpeed));
                Offset += offsetCurrent;

                if (doubleScale)
                {
                    //set center of sprite to corner
                    if (currentWeaponState == WeaponStates.Idle || (currentWeaponType == WeaponTypes.Bow && currentFrame == 0))
                    {
                        if (currentWeaponType == WeaponTypes.Werecreature)
                            Offset += new Vector2(0, 0.5f);
                        else
                            Offset += new Vector2(0.5f, 0.5f);
                    }
                }
            }
        }

        private void UpdateWeapon()
        {
            // Do nothing if weapon not ready
            if (weaponAtlas == null || weaponAnims == null ||
                weaponAtlas.WeaponRects == null || weaponAtlas.WeaponIndices == null)
            {
                return;
            }

            // Reset state if weapon not visible
            //if (!ShowWeapon || WeaponType == WeaponTypes.None)
            if (currentWeaponType == WeaponTypes.None)
            {
                currentWeaponState = WeaponStates.Idle;
                currentFrame = 0;
            }

            // Handle bow with no arrows
            if (!GameManager.Instance.WeaponManager.Sheathed && currentWeaponType == WeaponTypes.Bow && GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.Weapons, (int)Weapons.Arrow, allowQuestItem: false) == null)
            {
                GameManager.Instance.WeaponManager.SheathWeapons();
                DaggerfallUI.SetMidScreenText(TextManager.Instance.GetLocalizedText("youHaveNoArrows"));
            }

            // Store rect and anim
            int weaponAnimRecordIndex;
            if (currentWeaponType == WeaponTypes.Bow)
                weaponAnimRecordIndex = 0; // Bow has only 1 animation
            else
                weaponAnimRecordIndex = weaponAnims[(int)currentWeaponState].Record;

            try
            {
                bool isImported = customTextures.TryGetValue(MaterialReader.MakeTextureKey(0, (byte)weaponAnimRecordIndex, (byte)currentFrame), out curCustomTexture);
                if (FlipHorizontal && ((currentWeaponState == WeaponStates.Idle || currentWeaponState == WeaponStates.StrikeDown || currentWeaponState == WeaponStates.StrikeUp) || currentWeaponType == WeaponTypes.Melee))
                {
                    // Mirror weapon rect
                    if (isImported)
                    {
                        curAnimRect = new Rect(1, 0, -1, 1);
                    }
                    else
                    {
                        Rect rect = weaponAtlas.WeaponRects[weaponAtlas.WeaponIndices[weaponAnimRecordIndex].startIndex + currentFrame];
                        curAnimRect = new Rect(rect.xMax, rect.yMin, -rect.width, rect.height);
                    }
                }
                else
                {
                    curAnimRect = isImported ? new Rect(0, 0, 1, 1) : weaponAtlas.WeaponRects[weaponAtlas.WeaponIndices[weaponAnimRecordIndex].startIndex + currentFrame];
                }

                WeaponAnimation anim = weaponAnims[(int)currentWeaponState];

                //if (weaponState == WeaponStates.StrikeDown && (currentWeaponType == WeaponTypes.Mace || currentWeaponType != WeaponTypes.Mace_Magic))
                if (OverrideAlignment())
                {
                    anim.Alignment = WeaponAlignment.Center;
                }

                // Get weapon dimensions
                int width = weaponAtlas.WeaponIndices[weaponAnimRecordIndex].width;
                int height = weaponAtlas.WeaponIndices[weaponAnimRecordIndex].height;

                if (isImported)
                {
                    if (trueSize || (SpecificWeapon != null && customWeaponOverrides.ContainsKey(SpecificWeapon.TemplateIndex)))
                    {
                        width = curCustomTexture.width / textureScaleFactor;
                        height = curCustomTexture.height / textureScaleFactor;
                    }
                    else
                    {
                        if (doubleScale)
                        {
                            if (currentWeaponType != WeaponTypes.Bow)
                            {
                                if (currentWeaponState == WeaponStates.Idle)
                                {
                                    width *= 2;
                                    height *= 2;
                                }
                            }
                            else
                            {
                                if (currentFrame == 0)
                                {
                                    width *= 2;
                                    height *= 2;
                                }
                            }
                        }
                    }
                }

                // Get weapon scale
                weaponScaleX = (float)screenRect.width / (float)nativeScreenWidth;
                weaponScaleY = (float)screenRect.height / (float)nativeScreenHeight;

                // Adjust scale to be slightly larger when not using point filtering
                // This reduces the effect of filter shrink at edge of display
                if (DaggerfallUnity.Instance.MaterialReader.MainFilterMode != FilterMode.Point)
                {
                    weaponScaleX *= 1.01f;
                    weaponScaleY *= 1.01f;
                }

                // Source weapon images are designed to overlay a fixed 320x200 display.
                // Some weapons need to align with both top, bottom, and right of display.
                // This means they might be a little stretched on widescreen displays.
                switch (anim.Alignment)
                {
                    case WeaponAlignment.Left:
                        AlignLeft(anim, width, height);
                        break;

                    case WeaponAlignment.Center:
                        AlignCenter(anim, width, height);
                        break;

                    case WeaponAlignment.Right:
                        AlignRight(anim, width, height);
                        break;
                }
                // Set the frame time (attack speed)
                animTickTime = GetAnimTickTime();
            }
            catch (IndexOutOfRangeException)
            {
                DaggerfallUnity.LogMessage("Index out of range exception for weapon animation. Probably due to weapon breaking + being unequipped during animation.");
            }
        }

        private void AlignLeft(WeaponAnimation anim, int width, int height)
        {
            weaponPosition = new Rect(
                screenRect.x + screenRect.width * anim.Offset,
                screenRect.y + screenRect.height - height * weaponScaleY - weaponOffsetHeight,
                width * weaponScaleX,
                height * weaponScaleY);
        }

        private void AlignCenter(WeaponAnimation anim, int width, int height)
        {
            if (OverrideAlignment())
            {
                if (FlipHorizontal)
                {
                    weaponPosition = new Rect(
                        screenRect.x + screenRect.width / 2f - (width * weaponScaleX),
                        screenRect.y + screenRect.height - height * weaponScaleY - weaponOffsetHeight,
                        width * weaponScaleX,
                        height * weaponScaleY);
                }
                else
                {
                    weaponPosition = new Rect(
                        screenRect.x + screenRect.width / 2f,
                        screenRect.y + screenRect.height - height * weaponScaleY - weaponOffsetHeight,
                        width * weaponScaleX,
                        height * weaponScaleY);
                }
            }
            else
            {
                weaponPosition = new Rect(
                    screenRect.x + screenRect.width / 2f - (width * weaponScaleX) / 2f,
                    screenRect.y + screenRect.height - height * weaponScaleY - weaponOffsetHeight,
                    width * weaponScaleX,
                    height * weaponScaleY);
            }
        }

        private void AlignRight(WeaponAnimation anim, int width, int height)
        {
            if (FlipHorizontal && ((currentWeaponState == WeaponStates.Idle || currentWeaponState == WeaponStates.StrikeDown || currentWeaponState == WeaponStates.StrikeUp) || currentWeaponType == WeaponTypes.Melee))
            {
                // Flip alignment
                AlignLeft(anim, width, height);
                return;
            }

            weaponPosition = new Rect(
                screenRect.x + screenRect.width * (1f - anim.Offset) - width * weaponScaleX,
                screenRect.y + screenRect.height - height * weaponScaleY - weaponOffsetHeight,
                width * weaponScaleX,
                height * weaponScaleY);
        }

        private void LoadWeaponAtlas()
        {
            // Get weapon filename
            string filename = WeaponBasics.GetWeaponFilename(currentWeaponType);

            // Load the weapon texture atlas
            // Texture is dilated into a transparent coloured border to remove dark edges when filtered
            // Important to use returned UV rects when drawing to get right dimensions
            weaponAtlas = GetWeaponTextureAtlas(filename, currentMetalType, 2, 2, out var customAnimation, true);
            if (customAnimation != null)
                customTextures = customAnimation.Textures;
            else
                customTextures = new Dictionary<int, Texture2D>();
            weaponAtlas.AtlasTexture.filterMode = DaggerfallUnity.Instance.MaterialReader.MainFilterMode;

            // Get weapon anims
            weaponAnims = (WeaponAnimation[])WeaponBasics.GetWeaponAnims(currentWeaponType).Clone();

            if (SpecificWeapon != null)
                currentTemplateIndex = SpecificWeapon.TemplateIndex;
            else
                currentTemplateIndex = -1;

            animTickTime = GetAnimTickTime();
        }

        const float max = 0.045918f;
        const float mid = 0.198979f;
        const float min = 0.352041f;

        private float GetAnimTickTime()
        {
            float tickTime = GameManager.classicUpdateInterval;
            PlayerEntity player = GameManager.Instance.PlayerEntity;

            if (currentWeaponType != WeaponTypes.Bow && player != null)
                tickTime = FormulaHelper.GetMeleeWeaponAnimTime(player, currentWeaponType, ItemHands.Either);

            /*if (!swing)
                return tickTime;*/

            /*//dampen the value somehow
            float diff = max - mid;

            //at max, it should equal to "mid + (diff/2)"
            //at mid, it should equal to "mid"
            //at min, it should equal to "mid - (diff/2)"

            float t = Mathf.InverseLerp(0, 2, tickTime / mid);
            tickTime = Mathf.Lerp(max, min, t);*/

            return tickTime;
        }

        string GetMeleeWeaponAnimationGroup(DaggerfallUnityItem weapon)
        {
            if (weapon != null)
            {
                switch (weapon.TemplateIndex)
                {
                    case (int)Weapons.Battle_Axe:
                    case (int)Weapons.Broadsword:
                    case (int)Weapons.Katana:
                    case (int)Weapons.Longsword:
                    case (int)Weapons.Mace:
                    case (int)Weapons.Saber:
                    case (int)Weapons.Shortsword:
                    case (int)Weapons.Wakazashi:
                    case 513:   //Archer's Axe from Roleplay & Realism - Items
                    case 514:   //Light Flail from Roleplay & Realism - Items
                        return "OneHanded_";
                    case (int)Weapons.Dagger:
                    case (int)Weapons.Tanto:
                        return "OneHandedShort_";
                    case (int)Weapons.Dai_Katana:
                    case (int)Weapons.Claymore:
                        return "TwoHandedSword_";
                    case (int)Weapons.War_Axe:
                    case (int)Weapons.Flail:
                    case (int)Weapons.Staff:
                    case (int)Weapons.Warhammer:
                        return "TwoHandedPole_";
                }
            }

            return "Unarmed_";
        }

        private WeaponAtlas GetWeaponTextureAtlas(
                string filename,
                MetalTypes metalType,
                int padding,
                int border,
                out CustomWeaponAnimation customWeaponAnimation,
                bool dilate = false)
        {
            // Check caches
            var cachedAtlas = GetCachedWeaponAtlas(filename, metalType);
            var cachedAnimation = GetCachedCustomWeaponAnimation(filename, metalType);
            customWeaponAnimation = null;
            var modTextures = new Dictionary<int, Texture2D>();
            if (cachedAnimation != null)
                modTextures = cachedAnimation.Textures;
            // Nothing to load if both vanilla atlas and custom texture are currently cached.
            if (cachedAtlas != null && cachedAnimation != null)
            {
                customWeaponAnimation = new CustomWeaponAnimation() { FileName = filename, MetalType = metalType, Textures = modTextures };
                return cachedAtlas;
            }

            //Debug.Log("FPSWEAPON STANDALONE - WEAPON FILE NAME IS " + filename);

            // Load texture file
            if (cifFile == null)
            {
                cifFile = new CifRciFile();
                cifFile.Palette.Load(Path.Combine(DaggerfallUnity.Instance.Arena2Path, cifFile.PaletteName));
            }

            cifFile.Load(Path.Combine(DaggerfallUnity.Instance.Arena2Path, filename), FileUsage.UseMemory, true);

            // Read every image in archive
            Rect rect;
            List<Texture2D> textures = new List<Texture2D>();
            List<RecordIndex> indices = new List<RecordIndex>();
            for (int record = 0; record < cifFile.RecordCount; record++)
            {
                int frames = cifFile.GetFrameCount(record);
                DFSize size = cifFile.GetSize(record);
                RecordIndex ri = new RecordIndex()
                {
                    startIndex = textures.Count,
                    frameCount = frames,
                    width = size.Width,
                    height = size.Height,
                };
                if (cachedAtlas == null) // Load atlas if not already cached.
                {
                    for (int frame = 0; frame < frames; frame++)
                        textures.Add(GetWeaponTexture2D(filename, record, frame, metalType, out rect, border, dilate));
                }

                if (cachedAnimation != null) // No need to load frames if cached.
                    continue;

                for (int frame = 0; frame < frames; frame++)
                {
                    string moddedFileName = filename;

                    if (SpecificWeapon != null)
                    {
                        //check for custom weapon
                        string customWeaponFilename = GetCustomWeaponFilename(SpecificWeapon);

                        if (!string.IsNullOrEmpty(customWeaponFilename))
                            moddedFileName = customWeaponFilename;
                        else if (FPSWeapon.moddedWeaponHUDAnimsEnabled)
                        {
                            moddedFileName = WeaponBasics.GetModdedWeaponFilename(SpecificWeapon);

                            if (string.IsNullOrEmpty(moddedFileName))
                                moddedFileName = WeaponBasics.GetWeaponFilename(currentWeaponType); // Possibly make support for custom weapon types for the HUD in the future.
                        }
                    }

                    Texture2D tex;

                    string doubleScaleFileName = "w_" + moddedFileName;

                    Debug.Log("WEAPON WIDGET - LOADING " + doubleScaleFileName);

                    if (doubleScale && TextureReplacement.TryImportCifRci(doubleScaleFileName, record, frame, metalType, true, out tex))
                    {
                        tex.filterMode = DaggerfallUnity.Instance.MaterialReader.MainFilterMode;
                        tex.wrapMode = TextureWrapMode.Mirror;
                        modTextures.Add(MaterialReader.MakeTextureKey(0, (byte)record, (byte)frame), tex);
                    }
                    else if (TextureReplacement.TryImportCifRci(moddedFileName, record, frame, metalType, true, out tex))
                    {
                        tex.filterMode = DaggerfallUnity.Instance.MaterialReader.MainFilterMode;
                        tex.wrapMode = TextureWrapMode.Mirror;
                        modTextures.Add(MaterialReader.MakeTextureKey(0, (byte)record, (byte)frame), tex);
                    }
                }

                indices.Add(ri);
            }

            if (cachedAnimation == null && modTextures.Count > 0)
                customWeaponAnimation = new CustomWeaponAnimation() { FileName = filename, MetalType = metalType, Textures = modTextures };
            if (cachedAtlas != null)
                return cachedAtlas;

            // Pack textures into atlas
            Texture2D atlasTexture = new Texture2D(2048, 2048, TextureFormat.ARGB32, false);
            Rect[] rectsOut = atlasTexture.PackTextures(textures.ToArray(), padding, 2048);
            RecordIndex[] indicesOut = indices.ToArray();

            // Shrink UV rect to compensate for internal border
            float ru = 1f / atlasTexture.width;
            float rv = 1f / atlasTexture.height;
            for (int i = 0; i < rectsOut.Length; i++)
            {
                Rect rct = rectsOut[i];
                rct.xMin += border * ru;
                rct.xMax -= border * ru;
                rct.yMin += border * rv;
                rct.yMax -= border * rv;
                rectsOut[i] = rct;
            }

            return new WeaponAtlas()
            {
                FileName = filename,
                MetalType = metalType,
                AtlasTexture = atlasTexture,
                WeaponRects = rectsOut,
                WeaponIndices = indicesOut
            };
        }

        private WeaponAtlas GetCachedWeaponAtlas(string fileName, MetalTypes metalType)
        {
            foreach (var atlas in weaponAtlasCache)
            {
                if (atlas != null && atlas.FileName == fileName && atlas.MetalType == metalType)
                    return atlas;
            }

            return null;
        }
        private CustomWeaponAnimation GetCachedCustomWeaponAnimation(string fileName, MetalTypes metalType)
        {
            foreach (var animation in customWeaponAnimationCache)
            {
                if (animation != null && animation.FileName == fileName && animation.MetalType == metalType)
                    return animation;
            }

            return null;
        }
        private Texture2D GetWeaponTexture2D(
            string filename,
            int record,
            int frame,
            MetalTypes metalType,
            out Rect rectOut,
            int border = 0,
            bool dilate = false)
        {
            // Get source bitmap
            DFBitmap dfBitmap = cifFile.GetDFBitmap(record, frame);

            // Tint based on metal type
            // But not for steel as that is default colour in files
            if (metalType != MetalTypes.Steel && metalType != MetalTypes.None)
                dfBitmap = ImageProcessing.ChangeDye(dfBitmap, ImageProcessing.GetMetalDyeColor(metalType), DyeTargets.WeaponsAndArmor);

            // Get Color32 array
            DFSize sz;
            Color32[] colors = cifFile.GetColor32(dfBitmap, 0, border, out sz);

            // Dilate edges
            if (border > 0 && dilate)
                ImageProcessing.DilateColors(ref colors, sz);

            // Create Texture2D
            Texture2D texture = new Texture2D(sz.Width, sz.Height, TextureFormat.ARGB32, false);
            texture.SetPixels32(colors);
            texture.Apply(true);

            // Shrink UV rect to compensate for internal border
            float ru = 1f / sz.Width;
            float rv = 1f / sz.Height;
            rectOut = new Rect(border * ru, border * rv, (sz.Width - border * 2) * ru, (sz.Height - border * 2) * rv);

            return texture;
        }

        public bool CheckForEnvDamage()
        {
            Transform body = GameManager.Instance.PlayerObject.transform;
            Transform eye = GameManager.Instance.MainCameraObject.transform;
            float reach = GameManager.Instance.WeaponManager.ScreenWeapon.Reach;

            reach = currentWeaponReach;
            if (currentWeaponState == WeaponStates.StrikeUp)
                reach *= 1.25f;

            LayerMask layerMask = ~(1 << LayerMask.NameToLayer("Player"));
            layerMask = layerMask & ~(1 << LayerMask.NameToLayer("Ignore Raycast"));

            // Fire ray along player facing using weapon range
            RaycastHit hit;
            Ray ray = new Ray(body.position + (Vector3.up * (GameManager.Instance.PlayerController.height * 0.5f)), eye.forward);
            //Debug.DrawLine(ray.origin, ray.origin + (ray.direction * reach), Color.green, 1f, false);
            if (Physics.SphereCast(ray, 0.25f, out hit, reach, layerMask))
            {
                DaggerfallUnityItem strikingWeapon = GameManager.Instance.WeaponManager.ScreenWeapon.SpecificWeapon;
                return WeaponEnvDamage(strikingWeapon, hit);
            }

            return false;
        }

        // Returns true if hit the environment
        public bool WeaponEnvDamage(DaggerfallUnityItem strikingWeapon, RaycastHit hit)
        {
            // Check if hit has an DaggerfallActionDoor component
            DaggerfallActionDoor actionDoor = hit.transform.gameObject.GetComponent<DaggerfallActionDoor>();
            if (actionDoor)
            {
                if (playMissVFXEnvironment)
                    DoThud(hit.point);
                return true;
            }

            /*// Check if player hit a static door
            if (GameManager.Instance.PlayerActivate.AttemptExteriorDoorBash(hit))
                return true;*/

            // Make hitting walls do a thud or clinging sound (not in classic)
            if (GameObjectHelper.IsStaticGeometry(hit.transform.gameObject))
            {
                if (playMissVFXEnvironment)
                    DoThud(hit.point);
                return true;
            }

            return false;
        }
        public void DoClang(Transform target)
        {
            DoClang(target.position);
        }
        public void DoClang(Vector3 point)
        {
            //Debug.Log("WEAPON WIDGET - Did CLANG");
            //Create oneshot animated billboard for clang effect
            Camera eye = GameManager.Instance.MainCamera;
            Vector3 pos = eye.transform.position + ((point - eye.transform.position) * 0.75f);
            if (playMissVFXPos == 1)
                pos = eye.transform.position + (eye.transform.forward * (Vector3.Distance(eye.transform.position, point) * 0.75f));
            GameObject go = GameObjectHelper.CreateDaggerfallBillboardGameObject(380, 2, null);
            go.name = "CLANG";
            Billboard c = go.GetComponent<Billboard>();
            go.transform.position = pos;
            go.transform.localScale *= 2f;
            c.FaceY = true;
            c.OneShot = true;
            c.FramesPerSecond = 20;
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.material.SetColor("_Color", Color.yellow);
            renderer.material.SetColor("_EmissionColor", Color.yellow);
            renderer.material.SetTexture("_EmissionMap", renderer.material.mainTexture);
            renderer.material.EnableKeyword(KeyWords.Emission);
        }
        public void DoThud(Transform target)
        {
            DoThud(target.position);
        }
        public void DoThud(Vector3 point)
        {
            //Debug.Log("WEAPON WIDGET - Did THUD");
            //Create oneshot animated billboard for thud effect
            Camera eye = GameManager.Instance.MainCamera;
            Vector3 pos = eye.transform.position + ((point - eye.transform.position) * 0.75f);
            if (playMissVFXPos == 1)
                pos = eye.transform.position + (eye.transform.forward * (Vector3.Distance(eye.transform.position, point) * 0.75f));
            GameObject go = GameObjectHelper.CreateDaggerfallBillboardGameObject(380, 2, null);
            go.name = "THUD";
            Billboard c = go.GetComponent<Billboard>();
            go.transform.position = pos;
            go.transform.localScale *= 2f;
            c.FaceY = true;
            c.OneShot = true;
            c.FramesPerSecond = 20;
        }

        bool CheckForRecoveryOverride()
        {
            if (currentWeaponState != WeaponStates.StrikeUp)
                return false;

            if (SpecificWeapon == null)
                return false;

            if (customWeaponOverrides.ContainsKey(SpecificWeapon.TemplateIndex))
                return customWeaponOverrides[SpecificWeapon.TemplateIndex].OverrideRecovery;

            WeaponTypes weaponType = GameManager.Instance.ItemHelper.ConvertItemToAPIWeaponType(SpecificWeapon);

            //if (weaponType != WeaponTypes.Dagger && weaponType != WeaponTypes.Dagger_Magic && weaponType != WeaponTypes.Flail && weaponType != WeaponTypes.Flail_Magic)
            if (weaponType != WeaponTypes.Dagger && weaponType != WeaponTypes.Dagger_Magic)
            {
                return true;
            }

            return false;
        }

        public void PlayUnsheatheSound()
        {
            if (dfAudioSource)
            {
                dfAudioSource.AudioSource.pitch = 1f;// *AttackSpeedScale;
                dfAudioSource.PlayOneShot(DrawWeaponSound, 0);
            }
        }

        void PlaySheatheSound()
        {
            if (dfAudioSource)
            {
                dfAudioSource.AudioSource.pitch = 1f;// *AttackSpeedScale;
                dfAudioSource.PlayOneShot(SoundClips.EquipLeather, 0);
            }
        }

        public void PlaySwingSound()
        {
            if (dfAudioSource)
            {
                dfAudioSource.AudioSource.pitch = 1f * GameManager.Instance.RightHandWeapon.AttackSpeedScale;
                dfAudioSource.PlayOneShot(SwingWeaponSound, 0, 1.1f);
            }
        }

        public void PlayAttackSound(SoundClips customSound = SoundClips.None)
        {
            if (dfAudioSource)
            {
                if (customSound == SoundClips.None)
                {
                    if (GameManager.Instance.PlayerEffectManager.IsTransformedLycanthrope())
                    {
                        LycanthropyTypes lycanthropyType = GameManager.Instance.PlayerEffectManager.LycanthropyType();
                        if (lycanthropyType == LycanthropyTypes.Werewolf)
                        {
                            if (UnityEngine.Random.value > 0.5f)
                                customSound = SoundClips.EnemyWerewolfAttack;
                            else
                                customSound = SoundClips.EnemyWerewolfBark;
                        }
                        else if (lycanthropyType == LycanthropyTypes.Wereboar)
                        {
                            if (UnityEngine.Random.value > 0.5f)
                                customSound = SoundClips.EnemyWereboarAttack;
                            else
                                customSound = SoundClips.EnemyWereboarBark;
                        }
                    }
                    else
                    {
                        PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
                        customSound = DaggerfallEntity.GetRaceGenderAttackSound(playerEntity.Race, playerEntity.Gender, true);
                    }

                    float pitch = dfAudioSource.AudioSource.pitch;
                    dfAudioSource.AudioSource.pitch = pitch + UnityEngine.Random.Range(0, 0.3f);
                    dfAudioSource.AudioSource.pitch = pitch;
                    dfAudioSource.PlayOneShot(customSound, 0, 1f);
                }
                else
                {
                    dfAudioSource.PlayOneShot(customSound, 0, 1f);
                }
            }
        }

        string GetCustomWeaponFilename(DaggerfallUnityItem weapon)
        {
            if (weapon == null)
                return string.Empty;

            if (customWeaponOverrides.ContainsKey(weapon.TemplateIndex))
            {
                if (weapon.IsEnchanted && customWeaponOverrides[weapon.TemplateIndex].HasMagicTextures)
                    return customWeaponOverrides[weapon.TemplateIndex].Filename + "_MAGIC";
                else
                    return customWeaponOverrides[weapon.TemplateIndex].Filename;
            }
            else
                return string.Empty;
        }

        bool OverrideAlignment()
        {
            if (!swingAlignmentOverride)
                return false;

            if (SpecificWeapon == null)
                return false;

            if (customWeaponOverrides.ContainsKey(SpecificWeapon.TemplateIndex))
                return customWeaponOverrides[SpecificWeapon.TemplateIndex].OverrideAlignment;

            if (currentWeaponType == WeaponTypes.Melee)
                return true;

            if ((currentWeaponState == WeaponStates.StrikeDown || currentWeaponState == WeaponStates.StrikeUp) && !(currentWeaponType == WeaponTypes.Bow || currentWeaponType == WeaponTypes.Melee || currentWeaponType == WeaponTypes.Dagger || currentWeaponType == WeaponTypes.Dagger_Magic || currentWeaponType == WeaponTypes.Warhammer || currentWeaponType == WeaponTypes.Warhammer_Magic))
                return true;

            return false;
        }

        bool CheckForMirrorOverride()
        {
            if (SpecificWeapon == null)
                return false;

            if (customWeaponOverrides.ContainsKey(SpecificWeapon.TemplateIndex))
                return customWeaponOverrides[SpecificWeapon.TemplateIndex].OverrideMirror;

            if (SpecificWeapon.GetItemHands() == ItemHands.Both)
            {
                if (mirrorTwoHandedSwords && (currentWeaponType == WeaponTypes.LongBlade || currentWeaponType == WeaponTypes.LongBlade_Magic))
                    return true;

                if (mirrorTwoHandedAxes && (currentWeaponType == WeaponTypes.Battleaxe || currentWeaponType == WeaponTypes.Battleaxe_Magic))
                    return true;

                if (mirrorTwoHandedBlunts && (currentWeaponType == WeaponTypes.Staff || currentWeaponType == WeaponTypes.Staff_Magic || currentWeaponType == WeaponTypes.Warhammer || currentWeaponType == WeaponTypes.Warhammer_Magic || currentWeaponType == WeaponTypes.Flail || currentWeaponType == WeaponTypes.Flail_Magic))
                    return true;
            }

            return false;
        }

        bool CheckForOffsetOverride()
        {
            if (SpecificWeapon != null && customWeaponOverrides.ContainsKey(SpecificWeapon.TemplateIndex))
                return customWeaponOverrides[SpecificWeapon.TemplateIndex].OverrideOffset;

            if (currentWeaponType == WeaponTypes.Melee || currentWeaponType == WeaponTypes.Werecreature)
                return false;

            return true;
        }
    }

}