using System;
using System.IO;
using UnityEngine;
using System.Collections;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallConnect;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;

public class ShieldWidget : MonoBehaviour
{
    static Mod mod;

    [Invoke(StateManager.StateTypes.Start, 0)]

    public static void Init(InitParams initParams)
    {
        mod = initParams.Mod;
        var go = new GameObject(mod.Title);
        go.AddComponent<ShieldWidget>();
    }

    public static ShieldWidget Instance;

    DaggerfallAudioSource audioSource;

    const int nativeScreenWidth = 320;
    const int nativeScreenHeight = 200;

    public Texture2D shieldTexture;
    public Texture2D[] shieldTextures;
    Rect shieldPositionCurrent;
    Rect shieldPositionTarget;
    Rect curAnimRect;

    Rect screenRect;
    Rect screenRectLast;

    float weaponScaleX;
    float weaponScaleY;
    float weaponOffsetHeight;
    float weaponOffsetHeightLast;

    //FPSWeaponMovement.GetSettings
    bool lockAspectRatio;

    bool stepTransforms;
    int stepLength = 8;
    int stepCondition = 0;

    float offsetX = 0.8f;           //distance from the texture's left edge the center of the screen
    float offsetY = 0.6f;           //distance from the texture's bottom edge to the bottom of the screen
    float scale = 1f;
    float scaleTextureFactor = 1f;
    float offsetSpeed = 100f;
    float offsetSpeedLive
    {
        get
        {
            return ((float)GameManager.Instance.PlayerEntity.Stats.LiveSpeed / 100) * offsetSpeed;
        }
    }
    int whenSheathed;
    int whenAttacking;
    int whenCasting;

    bool bob;
    float bobLength = 1.0f;
    float bobOffset = 0f;
    float bobSizeXMod = 0.5f;      //RECOMMEND RANGE OF 0.5-1.5
    float bobSizeYMod = 1.5f;      //RECOMMEND RANGE OF 0.5-1.5
    float moveSmoothSpeed = 1;        //RECOMMEND RANGE OF 1-3
    float bobSmoothSpeed = 1;         //RECOMMEND RANGE OF UP TO 1-3
    float bobShape = 0;
    bool bobWhileIdle;

    bool inertia;
    float inertiaScale = 500;
    float inertiaSpeed = 50;
    float inertiaSpeedMod = 1;
    float inertiaForwardSpeed = 1;
    float inertiaForwardScale = 1;

    bool recoil;
    float recoilScale = 1;
    float recoilSpeed = 1;
    public int recoilCondition = 0;        //SHIELD HIT, SHIELD MISS, SHIELD ATTACK, ANY HIT, ANY MISS, ANY ATTACK
    bool recoilOffset = true;
    IEnumerator recoiling;

    bool animated;
    float animationTime;
    float animationTimeLive
    {
        get
        {
            return animationTime/((float)GameManager.Instance.PlayerEntity.Stats.LiveSpeed / 50);
        }
    }
    int animationDirection;
    IEnumerator animating;

    //PERSISTENT VARIABLES FOR THE SMOOTHING
    float moveSmooth = 0;
    Vector2 bobSmooth = Vector2.zero;

    Vector2 inertiaCurrent = Vector2.zero;
    Vector2 inertiaTarget;

    Vector2 inertiaForwardCurrent = Vector2.zero;
    Vector2 inertiaForwardTarget;

    Vector2 recoilCurrent = Vector2.zero;

    public Color Tint { get; set; } = Color.white;
    public Vector2 Position { get; set; } = Vector2.zero;
    public Vector2 Offset { get; set; } = Vector2.zero;
    public Vector2 Scale { get; set; } = Vector2.one;

    bool attacked;
    bool sheathed;
    bool spelled;
    int lastTemplate = -1;

    bool flipped;

    int conditionPrevious;
    int conditionThresholdUpper = 60;
    int conditionThresholdLower = 30;

    int indexCurrent;
    int frameCurrent;

    //attack reset delay time
    float attackDelayTime = 0.1f;
    float attackDelayTimer = 0;

    //EOTB compatibility
    bool isInThirdPerson;

    Mod fpsModels;
    Animator fpsModelsAnimator;

    void Awake()
    {

        mod.LoadSettingsCallback = LoadSettings;
        mod.LoadSettings();

        if (Instance == null)
            Instance = this;

        if (audioSource == null)
            audioSource = gameObject.AddComponent<DaggerfallAudioSource>();

        if (DaggerfallUI.Instance.CustomScreenRect != null)
            screenRect = DaggerfallUI.Instance.CustomScreenRect.Value;
        else
            screenRect = new Rect(0, 0, Screen.width, Screen.height);
        screenRectLast = screenRect;

        InitializeShieldTextures();

        RefreshShield();
        shieldPositionCurrent = shieldPositionTarget;
        attacked = false;
        sheathed = false;
        spelled = false;

        if (DaggerfallUnity.Settings.Handedness == 1)
            flipped = true;
        if (flipped)
            curAnimRect = new Rect(1, 0, -1, 1);
        else
            curAnimRect = new Rect(0, 0, 1, 1);

        mod.IsReady = true;
    }

    private void Start()
    {
        ModCompatibilityChecking();
    }

    private void ModCompatibilityChecking()
    {
        //listen to Eye Of The Beholder for changes in POV
        Mod eotb = ModManager.Instance.GetModFromGUID("2942ea8c-dbd4-42af-bdf9-8199d2f4a0aa");
        if (eotb != null)
        {
            ModManager.Instance.SendModMessage(eotb.Title, "onToggleOffset", (Action<bool>)(toggleState => {
                isInThirdPerson = toggleState;
            }));
        }

        //listen to Combat Event Handler for attacks
        Mod ceh = ModManager.Instance.GetModFromGUID("fb086c76-38e7-4d83-91dc-f29e6f1bb17e");
        if (ceh != null)
        {
            ModManager.Instance.SendModMessage(ceh.Title, "onAttackDamageCalculated", (Action<DaggerfallEntity, DaggerfallEntity, DaggerfallUnityItem, int, int>)OnAttackDamageCalculated);
        }

        //Check for FPSModels and grab the Animator
        fpsModels = ModManager.Instance.GetModFromGUID("41284af0-81c7-4630-bbc5-a976efa162a0");
        if (fpsModels != null)
        {
            ModManager.Instance.SendModMessage(fpsModels.Title, "getAnimator", null, (string message, object data) =>
            {
                fpsModelsAnimator = (Animator)data;
            });
        }
    }

    public void OnAttackDamageCalculated(DaggerfallEntity attacker, DaggerfallEntity target, DaggerfallUnityItem weapon, int bodyPart, int damage)
    {
        //if target is not the player, do nothing
        if (target != GameManager.Instance.PlayerEntity || !recoil)
            return;

        //else check if the player has a shield equipped
        //if the player does not have a shield equipped, do nothing
        DaggerfallUnityItem shield = target.ItemEquipTable.GetItem(EquipSlots.LeftHand);
        if (shield != null)
        {
            if (shield.IsShield)
            {
                if (Instance.recoilCondition > 2)                           //recoil shield on unshielded bits
                {
                    Instance.HitShield(damage, shield);
                }
                else                                                        //recoil shield only on shielded bits
                {
                    bool shielded = Instance.IsPartShielded(shield, bodyPart);
                    if (shielded)
                        Instance.HitShield(damage, shield);
                }
            }
        }
        else
            return;
    }

    private void LoadSettings(ModSettings settings, ModSettingsChange change)
    {
        if (change.HasChanged("Shield"))
        {
            offsetX = settings.GetValue<float>("Shield", "OffsetHorizontal");
            offsetY = settings.GetValue<float>("Shield", "OffsetVertical");
            scale = settings.GetValue<float>("Shield", "Scale");
            offsetSpeed = settings.GetValue<float>("Shield", "Speed") * 5000;
            whenSheathed = settings.GetValue<int>("Shield", "WhenSheathed");
            whenAttacking = settings.GetValue<int>("Shield", "WhenAttacking");
            whenCasting = settings.GetValue<int>("Shield", "WhenCasting");
            lockAspectRatio = settings.GetValue<bool>("Shield", "LockAspectRatio");
            conditionThresholdUpper = settings.GetValue<int>("Shield", "ConditionThresholdUpper");
            conditionThresholdLower = settings.GetValue<int>("Shield", "ConditionThresholdLower");
        }
        if (change.HasChanged("Modules"))
        {
            bob = settings.GetValue<bool>("Modules", "Bob");
            inertia = settings.GetValue<bool>("Modules", "Inertia");
            recoil = settings.GetValue<bool>("Modules", "Recoil");
            stepTransforms = settings.GetValue<bool>("Modules", "Step");
            animated = settings.GetValue<bool>("Modules", "Animation");
        }
        if (change.HasChanged("Bob"))
        {
            bobLength = (float)settings.GetValue<int>("Bob", "Length") / 100; ;
            bobOffset = settings.GetValue<float>("Bob", "Offset");
            bobSizeXMod = settings.GetValue<float>("Bob", "SizeX") * 2;
            bobSizeYMod = settings.GetValue<float>("Bob", "SizeY") * 2;
            moveSmoothSpeed = settings.GetValue<float>("Bob", "SpeedMove")*4;
            bobSmoothSpeed = settings.GetValue<float>("Bob", "SpeedState")*500;
            bobShape = settings.GetValue<int>("Bob", "Shape") * 0.5f;
            bobWhileIdle = settings.GetValue<bool>("Bob", "BobWhileIdle");
        }
        if (change.HasChanged("Inertia"))
        {
            inertiaScale = settings.GetValue<float>("Inertia", "Scale") * 500;
            inertiaSpeed = settings.GetValue<float>("Inertia", "Speed") * 500;
            inertiaForwardScale = settings.GetValue<float>("Inertia", "ForwardDepth") * 0.2f;
            inertiaForwardSpeed = settings.GetValue<float>("Inertia", "ForwardSpeed") * 0.2f;
        }
        if (change.HasChanged("Recoil"))
        {
            recoilScale = settings.GetValue<float>("Recoil", "Scale")*2f;
            recoilOffset = settings.GetValue<bool>("Recoil", "Offset");
            recoilSpeed = settings.GetValue<float>("Recoil", "Speed")*0.5f;
            recoilCondition = settings.GetValue<int>("Recoil", "Condition");
        }
        if (change.HasChanged("Step"))
        {
            stepLength = settings.GetValue<int>("Step", "Length");
            stepCondition = settings.GetValue<int>("Step", "Condition");
        }
        if (change.HasChanged("Animation"))
        {
            animationTime = 1-(settings.GetValue<float>("Animation", "Speed")*0.5f);
            animationDirection = settings.GetValue<int>("Animation", "Direction");
        }
        if (change.HasChanged("Compatibility"))
        {
            scaleTextureFactor = (float)settings.GetValue<int>("Compatibility", "TextureScaleFactor");
        }

        //if off-hand is shield
        DaggerfallUnityItem itemLeftHand = GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(EquipSlots.LeftHand);
        if (itemLeftHand != null)
            RefreshShield();
    }

    //Fill in the shieldTextures array with all the textures
    void InitializeShieldTextures()
    {
        shieldTextures = new Texture2D[600];
        int archive = 112360;
        int record = 0;
        int frame = 0;
        for (int i = 0; i < 600; i++)
        {
            Texture2D texture;
            DaggerfallWorkshop.Utility.AssetInjection.TextureReplacement.TryImportTexture(archive, record, frame, out texture);
            texture.filterMode = DaggerfallUnity.Instance.MaterialReader.MainFilterMode;
            shieldTextures[i] = texture;

            if (frame == 4)
            {
                frame = 0;
                if (record == 29)
                {
                    record = 0;
                    archive++;
                }
                else
                    record++;
            }
            else
                frame++;
        }
        shieldTexture = shieldTextures[0];
    }

    //Updates the sprite to match the currently equipped shield
    //Only run when the shield has changed
    void UpdateShieldTextures(DaggerfallUnityItem shield)
    {
        Debug.Log("Updating shield textures");
        int shieldType = 0;
        int shieldMaterial = 0;
        switch (shield.TemplateIndex)
        {
            case (int)Armor.Buckler:
                shieldType = 0;
                break;
            case (int)Armor.Round_Shield:
                shieldType = 150;
                break;
            case (int)Armor.Kite_Shield:
                shieldType = 300;
                break;
            case (int)Armor.Tower_Shield:
                shieldType = 450;
                break;
        }
        switch (shield.NativeMaterialValue)
        {
            case (int)ArmorMaterialTypes.Leather:
                shieldMaterial = 0;
                break;
            case (int)ArmorMaterialTypes.Chain:
                shieldMaterial = 0;
                break;
            case (int)ArmorMaterialTypes.Iron:
                shieldMaterial = 5;
                break;
            case (int)ArmorMaterialTypes.Steel:
                shieldMaterial = 10;
                break;
            case (int)ArmorMaterialTypes.Silver:
                shieldMaterial = 0;
                break;
            case (int)ArmorMaterialTypes.Elven:
                shieldMaterial = 15;
                break;
            case (int)ArmorMaterialTypes.Dwarven:
                shieldMaterial = 20;
                break;
            case (int)ArmorMaterialTypes.Mithril:
                shieldMaterial = 25;
                break;
            case (int)ArmorMaterialTypes.Adamantium:
                shieldMaterial = 30;
                break;
            case (int)ArmorMaterialTypes.Ebony:
                shieldMaterial = 35;
                break;
            case (int)ArmorMaterialTypes.Orcish:
                shieldMaterial = 40;
                break;
            case (int)ArmorMaterialTypes.Daedric:
                shieldMaterial = 45;
                break;
        }

        int conditionCurrent = shield.ConditionPercentage;

        if (conditionCurrent <= conditionThresholdLower)
            shieldMaterial += 100;
        else if (conditionCurrent <= conditionThresholdUpper)
            shieldMaterial += 50;

        conditionPrevious = conditionCurrent;

        indexCurrent = shieldType + shieldMaterial;

        shieldTexture = shieldTextures[indexCurrent+frameCurrent];
    }

    private void OnGUI()
    {
        if (shieldTexture == null || GameManager.Instance.PlayerEntity == null || isInThirdPerson)
            return;

        //if off-hand is shield
        DaggerfallUnityItem itemLeftHand = GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(EquipSlots.LeftHand);
        if (itemLeftHand != null)
        {
            if (!itemLeftHand.IsShield ||
                GameManager.Instance.WeaponManager.EquipCountdownLeftHand > 0 ||
                GameManager.Instance.ClimbingMotor.IsClimbing ||
                GameManager.IsGamePaused ||
                SaveLoadManager.Instance.LoadInProgress ||
                (!animated && whenSheathed == 0 && sheathed && !attacked && !spelled) ||
                (animated && (whenSheathed == 0 || whenSheathed == 1) && sheathed && animating == null && !attacked && !spelled) ||
                (!animated && whenAttacking == 0 && attacked) ||
                (animated && (whenAttacking == 0 || whenAttacking == 1) && attacked && animating == null) ||
                (!animated && whenCasting == 0 && spelled) ||
                (animated && (whenCasting == 0 || whenCasting == 1) && spelled && animating == null)
                )
                return;

            if (fpsModels == null && Event.current.type == EventType.Repaint) {
                GUI.depth = 0;
                DaggerfallUI.DrawTextureWithTexCoords(GetShieldRect(), shieldTexture, curAnimRect, true, GameManager.Instance.WeaponManager.ScreenWeapon.Tint);
            }
        }
    }

    private void LateUpdate()
    {
        Position = Vector2.zero;
        Offset = Vector2.zero;
        Scale = Vector2.zero;

        if (shieldTexture == null || GameManager.Instance.PlayerEntity == null)
            return;

        bool attacking = GameManager.Instance.WeaponManager.ScreenWeapon.IsAttacking();

        //if off-hand is shield
        DaggerfallUnityItem itemLeftHand = GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(EquipSlots.LeftHand);
        if (itemLeftHand != null)
        {
            if (!itemLeftHand.IsShield)
            {
                lastTemplate = -1;
                return;
            }

            if (GameManager.Instance.WeaponManager.EquipCountdownLeftHand > 0 ||
                GameManager.Instance.ClimbingMotor.IsClimbing ||
                GameManager.IsGamePaused ||
                SaveLoadManager.Instance.LoadInProgress
                )
                return;

            if (DaggerfallUI.Instance.CustomScreenRect != null)
                screenRect = DaggerfallUI.Instance.CustomScreenRect.Value;
            else
                screenRect = new Rect(0, 0, Screen.width, Screen.height);

            weaponOffsetHeight = 0;
            if (DaggerfallUI.Instance.DaggerfallHUD != null &&
                DaggerfallUnity.Settings.LargeHUD &&
                (DaggerfallUnity.Settings.LargeHUDUndockedOffsetWeapon || DaggerfallUnity.Settings.LargeHUDDocked))
            {
                weaponOffsetHeight = (int)DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.ScreenHeight;
            }

            if (screenRect != screenRectLast || weaponOffsetHeight != weaponOffsetHeightLast)
                RefreshShield();

            screenRectLast = screenRect;
            weaponOffsetHeightLast = weaponOffsetHeight;

            //change shield texture when a new shield type or material is equipped
            int index = itemLeftHand.TemplateIndex + itemLeftHand.NativeMaterialValue;
            if (lastTemplate != index)
            {
                UpdateShieldTextures(itemLeftHand);

                RefreshShield();
                lastTemplate = index;

                attacked = false;
                sheathed = false;
                spelled = false;
            }

            //adjust shield position when attacking
            if (attacking)
            {
                if (!attacked)
                {
                    attacked = true;
                    if (whenAttacking == 1 || whenAttacking == 2)
                        SetAttack();
                    else
                        SetGuard();
                    attackDelayTimer = 0;
                }
            }
            else
            {
                if (attacked)
                {
                    //add delay here?
                    if (attackDelayTimer > attackDelayTime)
                    {
                        attacked = false;
                        RefreshShield();
                    }
                    else
                        attackDelayTimer += Time.deltaTime;
                }
            }

            //adjust shield position when spellcasting
            if ((GameManager.Instance.PlayerSpellCasting.IsPlayingAnim || GameManager.Instance.PlayerEffectManager.HasReadySpell) && !attacking)
            {
                if (!spelled)
                {
                    spelled = true;
                    if (whenCasting == 1 || whenCasting == 2)
                        SetAttack();
                    else
                        SetGuard();
                }
            }
            else if (!attacking)
            {
                if (spelled)
                {
                    spelled = false;
                    RefreshShield();
                }
            }

            //adjust shield position when sheathed
            if (GameManager.Instance.WeaponManager.Sheathed && !(GameManager.Instance.PlayerSpellCasting.IsPlayingAnim || GameManager.Instance.PlayerEffectManager.HasReadySpell))
            {
                if (!sheathed)
                {
                    sheathed = true;
                    if (whenSheathed == 1 || whenSheathed == 2)
                        SetAttack();
                    else
                        SetGuard();
                }
            }
            else if (!(GameManager.Instance.PlayerSpellCasting.IsPlayingAnim || GameManager.Instance.PlayerEffectManager.HasReadySpell))
            {
                if (sheathed)
                {
                    sheathed = false;
                    RefreshShield();
                }
            }

            Vector2 current = new Vector2(shieldPositionCurrent.x, shieldPositionCurrent.y);
            Vector2 target = new Vector2(shieldPositionTarget.x, shieldPositionTarget.y);
            //current = Vector3.RotateTowards(current, target, Time.deltaTime,0);
            current = Vector2.MoveTowards(current, target, Time.deltaTime * offsetSpeedLive);
            shieldPositionCurrent = new Rect(current.x, current.y, shieldPositionTarget.width, shieldPositionTarget.height);

            //SCALE  TO SPEED AND MOVEMENT
            float currentSpeed = GameManager.Instance.PlayerMotor.Speed;
            float baseSpeed = GameManager.Instance.SpeedChanger.GetBaseSpeed();
            float speed = currentSpeed / baseSpeed;

            //start of weapon bob code
            if (bob)
            {
                Vector2 bobRaw = Vector2.zero;
                if (!attacking && shieldPositionCurrent == shieldPositionTarget && animating == null) {
                    //SHAPE OF MOVE BOB
                    float bobYOffset = bobShape;

                    //BOB ONLY WHEN GROUNDED
                    float move = 1;
                    if (!GameManager.Instance.PlayerMotor.IsGrounded)
                        move = 0;
                    moveSmooth = Mathf.MoveTowards(moveSmooth, move, Time.deltaTime * moveSmoothSpeed);

                    //SCALE BOB TO SPEED AND MOVEMENT
                    float bob = speed;

                    //DAMPEN BOB WHEN CROUCHED
                    if (GameManager.Instance.PlayerMotor.IsCrouching)
                        bob *= 0.5f;

                    //DAMPEN BOB WHEN RIDING A HORSE OR CART
                    if (GameManager.Instance.PlayerMotor.IsRiding)
                        bob *= 0.5f;

                    //DAMPEN BOB WHEN NOT MOVING
                    if (GameManager.Instance.PlayerMotor.IsStandingStill)
                    {
                        if (bobWhileIdle)
                            bob = 0.1f;
                        else
                            bob = 0;
                    }

                    //HORIZONTAL BOB
                    float bobXSpeed = baseSpeed * 1.25f * bob * bobLength; //SYNC IT WITH THE FOOTSTEP SOUNDS
                    float bobYSpeed = bobXSpeed * 2f; //MAKE IT A MULTIPLE OF THE HORIZONTAL BOB SPEED
                    float factor = 0.01f;
                    Vector2 bobSize = new Vector2(screenRect.width * factor * bob * bobSizeXMod, screenRect.height * factor * bob * bobSizeYMod);

                    //ADD OFFSET SO SPRITE DOESN'T EXPOSE EDGES WHEN BOBBING
                    float screenOffsetX = 1f;
                    float screenOffsetY = 1f;
                    //REVERSE OFFSET IF LEFT-HANDED
                    if (flipped)
                    {
                        screenOffsetX *= -1;
                        //screenOffsetY *= -1;
                    }

                    if (inertia && !animated)
                        screenOffsetX = 0;

                    //GET CURRENT BOB VALUES
                    bobRaw = new Vector2((screenOffsetX + Mathf.Sin(bobOffset + Time.time * bobXSpeed)) * -bobSize.x, (screenOffsetY - Mathf.Sin(bobOffset + bobYOffset + Time.time * bobYSpeed)) * bobSize.y);
                }

                //SMOOTH TRANSITIONS BETWEEN WALKING, RUNNING, CROUCHING, ETC
                bobSmooth = Vector2.MoveTowards(bobSmooth, bobRaw, Time.deltaTime * bobSmoothSpeed) * moveSmooth;

                Position += bobSmooth;
            }

            //inertia
            if (inertia)
            {
                if (attacking || shieldPositionCurrent != shieldPositionTarget || animating != null || frameCurrent != 0)
                {
                    inertiaCurrent = Vector2.zero;
                    inertiaTarget = Vector2.zero;
                    inertiaForwardCurrent = Vector2.zero;
                    inertiaForwardTarget = Vector2.zero;
                } else
                {
                    float mod = 1;

                    Vector3 MoveDirectionLocal = GameManager.Instance.PlayerObject.transform.InverseTransformVector(GameManager.Instance.PlayerMotor.MoveDirection);

                    float speedX = Mathf.Clamp(MoveDirectionLocal.x / 10, -1, 1);
                    float speedY = 0;
                    if (!GameManager.Instance.PlayerMotor.IsGrounded)
                        speedY = Mathf.Clamp(MoveDirectionLocal.y / 10, -1, 1);
                    float speedZ = Mathf.Clamp(MoveDirectionLocal.z / 10, -1, 1);

                    if (GameManager.Instance.PlayerMouseLook.cursorActive || InputManager.Instance.HasAction(InputManager.Actions.SwingWeapon))
                        inertiaTarget = new Vector2(-speedX * 0.5f * inertiaScale, 0);
                    else
                        inertiaTarget = new Vector2(-(InputManager.Instance.LookX + speedX) * 0.5f * inertiaScale, (InputManager.Instance.LookY + speedY) * 0.5f * inertiaScale);

                    inertiaSpeedMod = Vector2.Distance(inertiaCurrent, inertiaTarget) / inertiaScale;

                    if (inertiaTarget != Vector2.zero)
                        mod = 3;

                    inertiaCurrent = Vector2.MoveTowards(inertiaCurrent, inertiaTarget, Time.deltaTime * inertiaSpeed * inertiaSpeedMod * mod);

                    Position += inertiaCurrent;

                    mod = 1;

                    if (inertiaForwardTarget != Vector2.zero)
                        mod = 3;

                    inertiaForwardTarget = Vector2.one * speedZ * inertiaForwardScale;
                    inertiaForwardCurrent = Vector2.MoveTowards(inertiaForwardCurrent, inertiaForwardTarget, Time.deltaTime * inertiaForwardSpeed * mod);

                    Scale += inertiaForwardCurrent;
                    //Offset += inertiaForwardCurrent * 0.5f;
                }
            }

            if (recoil)
            {
                if (attacking || shieldPositionCurrent != shieldPositionTarget || animating != null || frameCurrent != 0)
                {
                    recoilCurrent = Vector2.zero;
                }
                else
                {
                    //If recoil is not zero, move it towards zero
                    if (recoilCurrent != Vector2.zero)
                    {
                        float mod = recoilCurrent.magnitude / 0.5f;
                        recoilCurrent = Vector2.MoveTowards(recoilCurrent, Vector2.zero, Time.deltaTime * recoilSpeed * mod);
                    }

                    Vector2 recoilFinal = recoilCurrent * recoilScale;

                    recoilFinal = new Vector2(Mathf.Clamp(recoilFinal.x, 0, 0.5f), Mathf.Clamp(recoilFinal.y, 0, 0.5f));

                    Scale += recoilFinal;
                    //Offset -= recoilFinal * 0.5f;

                }
            }
        }
        else
            lastTemplate = -1;
    }

    public Rect GetShieldRect()
    {
        Rect shieldPositionOffset = shieldPositionCurrent;

        shieldPositionOffset.x += Position.x;
        shieldPositionOffset.y += Position.y;

        shieldPositionOffset.width += shieldPositionOffset.width * Scale.x;
        shieldPositionOffset.height += shieldPositionOffset.height * Scale.y;

        shieldPositionOffset.x -= shieldPositionOffset.width * 0.5f;
        shieldPositionOffset.y -= shieldPositionOffset.height * 0.5f;

        shieldPositionOffset.x += shieldPositionOffset.width * Offset.x;
        shieldPositionOffset.y += shieldPositionOffset.height * Offset.y;

        //stop the texture from going higher than its bottom edge
        float largeHUDOffsetHeight = 0;
        if (DaggerfallUI.Instance.DaggerfallHUD != null &&
            DaggerfallUnity.Settings.LargeHUD &&
            (DaggerfallUnity.Settings.LargeHUDUndockedOffsetWeapon || DaggerfallUnity.Settings.LargeHUDDocked))
        {
            largeHUDOffsetHeight = (int)DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.ScreenHeight;
        }
        shieldPositionOffset.y = Mathf.Clamp(shieldPositionOffset.y, screenRect.height - shieldPositionOffset.height - largeHUDOffsetHeight, screenRect.height);

        if (animated)
        {
            if (flipped)
                shieldPositionOffset.x = Mathf.Clamp(shieldPositionOffset.x, screenRect.x + screenRect.width-shieldPositionOffset.width, screenRect.x + screenRect.width);
            else
                shieldPositionOffset.x = Mathf.Clamp(shieldPositionOffset.x, -shieldPositionOffset.width, 0);
        }

        if (stepTransforms)
        {
            float length = stepLength * (screenRect.height / 64);
            shieldPositionOffset.x = Snapping.Snap(shieldPositionOffset.x, length);
            shieldPositionOffset.y = Snapping.Snap(shieldPositionOffset.y, length);
        }

        return shieldPositionOffset;
    }

    public void RefreshShield()
    {
        if (GameManager.Instance.WeaponManager.Sheathed || GameManager.Instance.WeaponManager.ScreenWeapon.IsAttacking())
            SetAttack();
        else
            SetGuard();
    }

    public void SetGuard()
    {
        weaponScaleX = (float)screenRect.width / (float)nativeScreenWidth;
        if (lockAspectRatio)
            weaponScaleY = weaponScaleX;
        else
            weaponScaleY = (float)screenRect.height / (float)nativeScreenHeight;

        if (animated)
        {
            if (frameCurrent != 0)
            {
                if (animationDirection == 1)
                {
                    frameCurrent = 0;
                    shieldTexture = shieldTextures[indexCurrent + frameCurrent];
                }
                else
                {
                    if (animating != null)
                        StopCoroutine(animating);
                    animating = AnimateShield(4, 0, animationTimeLive);
                    StartCoroutine(animating);
                }
            }

            if (flipped)
            {
                shieldPositionTarget = new Rect(
                    screenRect.x + screenRect.width - ((screenRect.width * 0.5f) * offsetX),
                    screenRect.y + screenRect.height - weaponOffsetHeight - ((screenRect.height * 0.25f) * offsetY),
                    shieldTexture.width * scale * weaponScaleX / scaleTextureFactor,
                    shieldTexture.height * scale * weaponScaleY / scaleTextureFactor
                    );
            }
            else
            {
                shieldPositionTarget = new Rect(
                    screenRect.x + ((screenRect.width * 0.5f) * offsetX),
                    screenRect.y + screenRect.height - weaponOffsetHeight - ((screenRect.height * 0.25f) * offsetY),
                    shieldTexture.width * scale * weaponScaleX / scaleTextureFactor,
                    shieldTexture.height * scale * weaponScaleY / scaleTextureFactor
                    );
            }

            return;
        }
        else if (frameCurrent != 0)
        {
            frameCurrent = 0;
            shieldTexture = shieldTextures[indexCurrent + frameCurrent];
        }

        if (flipped)
        {
            shieldPositionTarget = new Rect(
                screenRect.x + screenRect.width - ((screenRect.width * 0.5f) * offsetX),
                screenRect.y + screenRect.height - weaponOffsetHeight - ((screenRect.height * 0.25f) * offsetY),
                shieldTexture.width * scale * weaponScaleX / scaleTextureFactor,
                shieldTexture.height * scale * weaponScaleY / scaleTextureFactor
                );
        }
        else
        {
            shieldPositionTarget = new Rect(
                screenRect.x + ((screenRect.width * 0.5f) * offsetX),
                screenRect.y + screenRect.height - weaponOffsetHeight - ((screenRect.height * 0.25f) * offsetY),
                shieldTexture.width * scale * weaponScaleX / scaleTextureFactor,
                shieldTexture.height * scale * weaponScaleY / scaleTextureFactor
                );
        }
    }

    public void SetAttack()
    {
        weaponScaleX = (float)screenRect.width / (float)nativeScreenWidth;
        if (lockAspectRatio)
            weaponScaleY = weaponScaleX;
        else
            weaponScaleY = (float)screenRect.height / (float)nativeScreenHeight;

        if (animated)
        {
            if (frameCurrent != 4)
            {
                if (animationDirection == 2)
                {
                    frameCurrent = 4;
                    shieldTexture = shieldTextures[indexCurrent + frameCurrent];
                }
                else
                {
                    if (animating != null)
                        StopCoroutine(animating);
                    animating = AnimateShield(0, 4, animationTimeLive);
                    StartCoroutine(animating);
                }
            }
            if (flipped)
            {
                shieldPositionTarget = new Rect(
                    screenRect.x + screenRect.width - ((screenRect.width * 0.5f) * offsetX),
                    screenRect.y + screenRect.height - weaponOffsetHeight - ((screenRect.height * 0.25f) * offsetY),
                    shieldTexture.width * scale * weaponScaleX / scaleTextureFactor,
                    shieldTexture.height * scale * weaponScaleY / scaleTextureFactor
                    );
            }
            else
            {
                shieldPositionTarget = new Rect(
                    screenRect.x + ((screenRect.width * 0.5f) * offsetX),
                    screenRect.y + screenRect.height - weaponOffsetHeight - ((screenRect.height * 0.25f) * offsetY),
                    shieldTexture.width * scale * weaponScaleX / scaleTextureFactor,
                    shieldTexture.height * scale * weaponScaleY / scaleTextureFactor
                    );
            }
            return;
        }
        else if (frameCurrent != 0)
        {
            frameCurrent = 0;
            shieldTexture = shieldTextures[indexCurrent + frameCurrent];
        }

        if ((!spelled && !attacked && whenSheathed == 2) || (spelled && whenCasting == 2) || (attacked && whenAttacking == 2))
        {
            if (flipped)
            {
                shieldPositionTarget = new Rect(
                    screenRect.x + screenRect.width,
                    screenRect.y + screenRect.height - weaponOffsetHeight,
                    shieldTexture.width * scale * weaponScaleX / scaleTextureFactor,
                    shieldTexture.height * scale * weaponScaleY / scaleTextureFactor
                    );
            }
            else
            {
                shieldPositionTarget = new Rect(
                    screenRect.x,
                    screenRect.y + screenRect.height - weaponOffsetHeight,
                    shieldTexture.width * scale * weaponScaleX / scaleTextureFactor,
                    shieldTexture.height * scale * weaponScaleY / scaleTextureFactor
                    );
            }
        }
        else if ((!spelled && !attacked && whenSheathed == 1) || (spelled && whenCasting == 1) || (attacked && whenAttacking == 1))
        {
            if (flipped)
            {
                shieldPositionTarget = new Rect(
                    screenRect.x + screenRect.width + (shieldTexture.width * scale * weaponScaleX / scaleTextureFactor),
                    screenRect.y + screenRect.height - weaponOffsetHeight + (shieldTexture.height * scale * weaponScaleY / scaleTextureFactor),
                    shieldTexture.width * scale * weaponScaleX / scaleTextureFactor,
                    shieldTexture.height * scale * weaponScaleY / scaleTextureFactor
                    );
            }
            else
            {
                shieldPositionTarget = new Rect(
                    screenRect.x - (shieldTexture.width * scale * weaponScaleX / scaleTextureFactor),
                    screenRect.y + screenRect.height - weaponOffsetHeight + (shieldTexture.height * scale * weaponScaleY / scaleTextureFactor),
                    shieldTexture.width * scale * weaponScaleX / scaleTextureFactor,
                    shieldTexture.height * scale * weaponScaleY / scaleTextureFactor
                    );
            }
        }
    }
    public void SetBlock()
    {
        weaponScaleX = (float)screenRect.width / (float)nativeScreenWidth;
        if (lockAspectRatio)
            weaponScaleY = weaponScaleX;
        else
            weaponScaleY = (float)screenRect.height / (float)nativeScreenHeight;

        if (animated)
        {
            if (frameCurrent != 0)
            {
                if (animationDirection == 1)
                {
                    frameCurrent = 0;
                    shieldTexture = shieldTextures[indexCurrent + frameCurrent];
                }
                else
                {
                    if (animating != null)
                        StopCoroutine(animating);
                    animating = AnimateShield(4, 0, animationTimeLive);
                    StartCoroutine(animating);
                }
            }

            if (flipped)
            {
                shieldPositionCurrent = new Rect(
                    screenRect.x + screenRect.width - ((screenRect.width * 0.5f) * 1),
                    screenRect.y + screenRect.height - weaponOffsetHeight - ((screenRect.height * 0.25f) * 0.8f),
                    shieldTexture.width * scale * weaponScaleX / scaleTextureFactor,
                    shieldTexture.height * scale * weaponScaleY / scaleTextureFactor
                    );
            }
            else
            {
                shieldPositionTarget = new Rect(
                    screenRect.x + ((screenRect.width * 0.5f) * 1),
                    screenRect.y + screenRect.height - weaponOffsetHeight - ((screenRect.height * 0.25f) * 0.8f),
                    shieldTexture.width * scale * weaponScaleX / scaleTextureFactor,
                    shieldTexture.height * scale * weaponScaleY / scaleTextureFactor
                    );
            }

            return;
        }
        else if (frameCurrent != 0)
        {
            frameCurrent = 0;
            shieldTexture = shieldTextures[indexCurrent + frameCurrent];
        }

        if (flipped)
        {
            shieldPositionTarget = new Rect(
                screenRect.x + screenRect.width - ((screenRect.width * 0.5f) * 1),
                screenRect.y + screenRect.height - weaponOffsetHeight - ((screenRect.height * 0.25f) * 0.8f),
                shieldTexture.width * scale * weaponScaleX / scaleTextureFactor,
                shieldTexture.height * scale * weaponScaleY / scaleTextureFactor
                );
        }
        else
        {
            shieldPositionTarget = new Rect(
                screenRect.x + ((screenRect.width * 0.5f) * 1),
                screenRect.y + screenRect.height - weaponOffsetHeight - ((screenRect.height * 0.25f) * 0.8f),
                shieldTexture.width * scale * weaponScaleX / scaleTextureFactor,
                shieldTexture.height * scale * weaponScaleY / scaleTextureFactor
                );
        }
    }

    IEnumerator BlockCoroutine(float magnitude = 0)
    {
        SetBlock();

        while (shieldPositionCurrent != shieldPositionTarget)
            yield return new WaitForEndOfFrame();

        recoilCurrent += Vector2.one * magnitude;
        PlayImpactSound();

        /*while (recoilCurrent != Vector2.zero)
            yield return new WaitForEndOfFrame();*/

        yield return new WaitForSeconds(0.5f);

        if (attacked || GameManager.Instance.WeaponManager.Sheathed)
            SetAttack();
        else
            SetGuard();
    }

    IEnumerator AnimateShield(int start, int end, float time)
    {
        //Debug.Log("ANIMATING SHIELD!");

        float interval = time / 5;
        float timeCurrent = Time.unscaledTime;
        frameCurrent = start;
        while (frameCurrent != end)
        {
            if (start > end)
                frameCurrent--;
            else
                frameCurrent++;

            shieldTexture = shieldTextures[indexCurrent + frameCurrent];

            //Debug.Log("ADVANCING FRAME!");

            yield return new WaitForSeconds(interval);
        }
        animating = null;
    }

    public void HitShield(int damage, DaggerfallUnityItem shield)
    {
        if (!recoil)
            return;

        if (recoilCondition == 0 || recoilCondition == 3) //HITS ONLY
        {
            if (damage > 0)
            {
                if (recoilOffset && !attacked)
                {
                    if (recoiling != null)
                        StopCoroutine(recoiling);
                    recoiling = BlockCoroutine(0.1f + (damage * 0.01f));
                    StartCoroutine(recoiling);
                }
                else
                {
                    recoilCurrent += Vector2.one * (0.1f + (damage * 0.01f));
                    PlayImpactSound();
                }
                if (fpsModelsAnimator != null)
                    fpsModelsAnimator.Play(GetShieldAnimationGroup(shield) + "Recoil",1);
            }
        }
        else if (recoilCondition == 1 || recoilCondition == 4) //MISSES ONLY
        {
            if (damage < 1)
            {
                if (recoilOffset && !attacked)
                {
                    if (recoiling != null)
                        StopCoroutine(recoiling);
                    recoiling = BlockCoroutine(0.1f + (damage * 0.01f));
                    StartCoroutine(recoiling);
                }
                else
                {
                    recoilCurrent += Vector2.one * (0.1f + (damage * 0.01f));
                    PlayImpactSound();
                }
                if (fpsModelsAnimator != null)
                    fpsModelsAnimator.Play(GetShieldAnimationGroup(shield) + "Recoil", 1);
            }
        }
        else
        {
            if (recoilOffset && !attacked)
            {
                if (recoiling != null)
                    StopCoroutine(recoiling);
                recoiling = BlockCoroutine(0.1f + (damage * 0.01f));
                StartCoroutine(recoiling);
            }
            else
            {
                recoilCurrent += Vector2.one * (0.1f + (damage * 0.01f));
                PlayImpactSound();
            }
            if (fpsModelsAnimator != null)
                fpsModelsAnimator.Play(GetShieldAnimationGroup(shield) + "Recoil", 1);
        }

        //check if condition has changed
        int conditionCurrent = shield.ConditionPercentage;

        if ((conditionCurrent <= conditionThresholdUpper && conditionPrevious > conditionThresholdUpper) || (conditionCurrent <= conditionThresholdLower && conditionPrevious > conditionThresholdLower))
        {
            Debug.Log("Condition threshold passed! Updating shield!");
            UpdateShieldTextures(shield);
        }
        else
            conditionPrevious = conditionCurrent;
    }

    public void PlayImpactSound()
    {
        int sound = (int)SoundClips.Parry1 + UnityEngine.Random.Range(0, 9);
        audioSource.PlayOneShot(sound, 0, 1.1f);
    }

    public bool IsPartShielded(DaggerfallUnityItem shield, int struckBodyPart)
    {
        BodyParts[] parts = shield.GetShieldProtectedBodyParts();
        bool shielded = false;
        foreach (BodyParts part in parts)
        {
            if (part == (BodyParts)struckBodyPart)
            {
                shielded = true;
                break;
            }
        }
        return shielded;
    }

    string GetShieldAnimationGroup(DaggerfallUnityItem shield)
    {
        if (shield != null)
        {
            switch (shield.TemplateIndex)
            {
                case (int)Armor.Buckler:
                    return "ShieldHand_";
                case (int)Armor.Round_Shield:
                case (int)Armor.Kite_Shield:
                case (int)Armor.Tower_Shield:
                    return "ShieldArm_";
            }
        }

        return "Unarmed_";
    }

}
