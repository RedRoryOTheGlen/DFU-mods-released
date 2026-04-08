using System;
using System.Collections;
using System.Collections.Generic;
using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using UnityEngine;

public class HorseRidingOverhaul : MonoBehaviour
{
    public static HorseRidingOverhaul Instance;

    bool wasRiding;
    bool wasGrounded;

    Camera playerCamera;
    GameObject playerObject;
    PlayerMotor playerMotor;
    PlayerMouseLook playerMouseLook;
    TransportManager transportManager;

    HorseRidingWidget widget;

    TransportModes lastTransportMode;

    public float targetYaw;
    public float currentYaw;
    public float Yaw;
    public float targetThrottle;
    public float currentThrottle;
    public float lastThrottle;
    public Vector3 moveVector;
    public Vector3 lastMoveVector;
    public Vector3 strafeVector;

    public float angleYaw;
    public float anglePitch;

    bool limitYaw;
    float limitYawAngle = 165f;

    public int speedIndex = 1;
    float[] speeds = new float[] { -0.25f, 0, 0.5f, 1.0f };

    //horse characteristics
    bool acceleration;
    float moveAcceleration = 1;
    float moveBraking = 1;
    float turnSpeed = 90;
    float turnAcceleration = 3;
    float turnBraking = 3;
    bool colliding;

    //horse sprite
    public float ScaleFactorX = 0.8f;
    public int nativeScreenWidth = 320;
    public int nativeScreenHeight = 200;
    public Rect screenRect;
    Rect horseRect;

    Texture2D ridingTexture;
    Texture2D[] ridingTextures = new Texture2D[4];
    float lastFrameTime = 0;
    int frameIndex = 0;
    public Color Tint { get; set; } = Color.white;

    const string horseTextureName = "MRED00I0.CFA";
    const string cartTextureName = "MRED01I0.CFA";
    const float animFrameTimeBase = 0.125f;  // Time between animation frames in seconds.
    float animFrameTime;
    string lastTextureName;
    bool mirror;

    //horse sounds
    GameObject audioObject;
    const SoundClips horseSound = SoundClips.AnimalHorse;
    const SoundClips horseRidingSound1 = SoundClips.HorseClop;
    const SoundClips horseRidingSound2 = SoundClips.HorseClop2;
    const SoundClips cartRidingSound = SoundClips.HorseAndCart;
    DaggerfallAudioSource dfAudioSource;
    AudioSource ridingAudioSource;
    AudioClip neighClip;
    float neighTime = 0;

    public bool customAudio;
    float customAudioVolume;
    IEnumerator audioRiding;
    AudioClip[] audioHorseStepWalk;
    AudioClip[] audioHorseStepWalkPath;
    AudioClip[] audioHorseStepWalkSnow;
    AudioClip[] audioHorseStepSprint;
    AudioClip[] audioHorseStepSprintPath;
    AudioClip[] audioHorseStepSprintSnow;
    AudioClip[] audioHorseMount;
    AudioClip[] audioHorseDismount;
    AudioClip[] audioHorseNoise;
    AudioClip[] audioHorseCollision;
    AudioClip[] audioHorseWinded;
    public bool[] gait1 = new bool[] { false, false, false, false, true };
    public bool[] gait2 = new bool[] { false, false, false, true };
    public bool[] gait3 = new bool[] { false, false, true };
    public bool[] gait4 = new bool[] { false, false, false, true, true, true };
    public bool[] gait5 = new bool[] { false, false, true, true, true, true };
    public bool[] currentGait;
    public bool[] gaitLand = new bool[] { true, true };
    bool sprint;

    AudioClip[] audioCamelNoise;
    AudioClip[] audioCamelCollision;
    AudioClip[] audioCamelWinded;

    bool playerVocalization;

    float lastCollision;

    public int steering;   //0 = default, 1 = mouse steering
    public int throttle;    //0 = default, 1 = hold
    public int view;       //0 = free, 1 = locked
    KeyCode centerViewKeyCode = KeyCode.X;
    KeyCode centerHorseKeyCode = KeyCode.C;
    KeyCode steeringKeyCode = KeyCode.Tab;
    KeyCode throttleKeyCode = KeyCode.KeypadEnter;
    KeyCode viewKeyCode = KeyCode.KeypadPlus;

    public int galloping;  //0 = none, 1 = vanilla, 2 = stamina, 3 = token

    public int tokenCurrent = 3;
    public int tokenMax = 3;
    float tokenDuration = 1;
    float tokenTime = 6;
    float tokenTimer;
    IEnumerator charging;

    public float staminaCurrent = 100;
    public float staminaMax = 100;
    float staminaDrainBase = 0.25f;
    float staminaDrainThrottle = 0.75f;
    float staminaDrain;
    public bool isStaminaDrained;

    //testing handling mods
    //horse
    public float modMoveSpeed = 1;
    public float modMoveAccel = 1;
    public float modMoveBrake = 1;
    public float modTurnSpeed = 1;
    public float modTurnAccel = 1;
    public float modTurnBrake = 1;

    //cart
    public float modCartMoveSpeed = 1;
    public float modCartMoveAccel = 1;
    public float modCartMoveBrake = 1;
    public float modCartTurnSpeed = 1;
    public float modCartTurnAccel = 1;
    public float modCartTurnBrake = 1;

    //gallop
    public bool modCanRun = true;   //can prevent gallop
    public float modRunSpeed = 1;  //multiplier for gallop speed
    string modRunSpeedUID;

    //trample
    public float modTrampleAccuracy = 1;    //base accuracy is 25%
    public float modTrampleDamage = 1;

    //gallop token system
    public int modTokenMax = 3;
    public float modTokenDuration = 1;
    public float modTokenTime = 6;

    //gallop stamina system
    public float modStaminaMax = 100;
    public float modStaminaDrain = 0.25f;

    bool showCharge;
    float showChargeTime = 2;
    float showChargeTimer;
    public bool ShowCharges
    {
        get
        {
            if (!showCharge)
                return true;
            else if (galloping == 3 && (tokenCurrent < tokenMax || showChargeTimer < showChargeTime))
                return true;
            else if (galloping == 2 && (staminaCurrent < staminaMax || showChargeTimer < showChargeTime))
                return true;
            else
                return false;
        }
    }

    bool slopeFollow;
    float targetSlopeAngle;
    float currentSlopeAngle;
    float slopeFollowSpeed;
    float slopeFollowStrength;

    bool inertia;
    float targetInertia;
    float currentInertia;
    float currentGravityOffset;
    float targetGravityOffset;
    float inertiaSpeed = 1;
    float inertiaStrength = 1;

    float TextureScaleFactor = 1;

    //visual
    int rideIndex = 0;
    int variantIndex = 0;
    int camelArchive = 10002;

    //override
    int rideIndexMod = -1;
    int variantIndexMod = -1;

    //trampling
    public int trample;    //0 = none, 1 = enemies only, 2 = all entities
    public int trampleSkill = 20;
    public DaggerfallUnityItem trampleWeapon;

    //VCEH mirror
    public event Action<DaggerfallEntity, DaggerfallEntity, DaggerfallUnityItem, int, int> OnAttackDamageCalculated;

    //EOTB compatibility
    bool isInThirdPerson;

    //Travel Options compatibility
    bool hasTravelOptions;
    bool fullSpeedOnTravel;
    public bool isTravelling;
    public bool wasTravelling;

    public bool IsMovingLessThanHalfSpeed
    {
        get
        {
            if (GameManager.Instance.PlayerMotor.IsStandingStill)
                return true;

            //add a buffer to prevent ping-ponging from decimals
            float threshold;
            if (GameManager.Instance.PlayerMotor.IsCrouching)
                threshold = 1 + GameManager.Instance.SpeedChanger.GetWalkSpeed(GameManager.Instance.PlayerEntity) / 2;
            else
                threshold = 1 + GameManager.Instance.SpeedChanger.GetBaseSpeed() / 2;

            //Get actual movement speed without gravity
            float speed = new Vector3(GameManager.Instance.PlayerMotor.MoveDirection.x,0, GameManager.Instance.PlayerMotor.MoveDirection.z).magnitude;

            //Debug.Log(threshold.ToString() + " > " + speed.ToString());

            return threshold > speed;
        }
    }

    static Mod mod;
    [Invoke(StateManager.StateTypes.Start, 0)]

    public static void Init(InitParams initParams)
    {
        mod = initParams.Mod;
        var go = new GameObject(mod.Title);
        go.AddComponent<HorseRidingOverhaul>();
    }
    public bool CanRunUnlessRidingCart()
    {
        //prevent run if mod horse can't gallop
        if (!modCanRun && playerMotor.IsRiding)
            return false;

        //prevent carts from galloping
        if (galloping == 3)
            return !(playerMotor.IsRiding && charging == null);
        else if (galloping == 2)
            return !(playerMotor.IsRiding && (staminaCurrent <= 0 || isStaminaDrained || GameManager.Instance.TransportManager.TransportMode == TransportModes.Cart));
        else
            return !((GameManager.Instance.TransportManager.TransportMode == TransportModes.Cart || galloping != 1) && playerMotor.IsRiding);

        //allow carts to gallop
        //return !(galloping == 0 && playerMotor.IsRiding);
    }

    void Awake()
    {
        Instance = this;

        playerCamera = GameManager.Instance.MainCamera;
        playerObject = GameManager.Instance.PlayerObject;
        playerMouseLook = GameManager.Instance.PlayerMouseLook;
        playerMotor = GameManager.Instance.PlayerMotor;
        transportManager = GameManager.Instance.TransportManager;
        widget = gameObject.AddComponent<HorseRidingWidget>();

        mod.LoadSettingsCallback = LoadSettings;
        mod.LoadSettings();

        ModCompatibilityChecking();

        GameManager.Instance.SpeedChanger.CanRun = CanRunUnlessRidingCart;

        //do the sounds
        audioObject = new GameObject("Free Rein - Audio");
        audioObject.transform.SetParent(playerObject.transform, false);
        audioObject.transform.localPosition = Vector3.zero;
        audioObject.transform.localRotation = Quaternion.identity;

        dfAudioSource = playerObject.GetComponent<DaggerfallAudioSource>();

        // Use custom audio source as we don't want to affect other sounds while riding.
        ridingAudioSource = audioObject.AddComponent<AudioSource>();
        ridingAudioSource.hideFlags = HideFlags.HideInInspector;
        ridingAudioSource.playOnAwake = false;
        ridingAudioSource.loop = false;
        ridingAudioSource.dopplerLevel = 0f;
        ridingAudioSource.spatialBlend = 1.0f;
        ridingAudioSource.volume = 0.5f * DaggerfallUnity.Settings.SoundVolume;
        neighClip = dfAudioSource.GetAudioClip((int)horseSound);

        transportManager.RidingVolumeScale = 0;

        //add collision handler
        GameManager.Instance.PlayerObject.AddComponent<HorseRidingCollision>();

        if (trampleWeapon == null)
        {
            trampleWeapon = ItemBuilder.CreateWeapon(Weapons.Warhammer, WeaponMaterialTypes.Steel);
            trampleWeapon.shortName = "horse";
        }

        StartGameBehaviour.OnNewGame += OnNewGame;
        SaveLoadManager.OnLoad += OnLoad;
        SaveLoadManager.OnStartLoad += OnStartLoad;
        GameManager.Instance.PlayerEntity.OnExhausted += OnExhausted;
        DaggerfallTravelPopUp.OnPostFastTravel += OnPostFastTravel;
        DaggerfallTravelPopUp.OnPreFastTravel += OnPreFastTravel;

        audioHorseStepWalk = new AudioClip[]
        {
            mod.GetAsset<AudioClip>("npc_horse_foot_walk_front_01"),
            mod.GetAsset<AudioClip>("npc_horse_foot_walk_back_01"),
            mod.GetAsset<AudioClip>("npc_horse_foot_walk_front_02"),
            mod.GetAsset<AudioClip>("npc_horse_foot_walk_back_02"),
            mod.GetAsset<AudioClip>("npc_horse_foot_walk_front_03"),
            mod.GetAsset<AudioClip>("npc_horse_foot_walk_back_03"),
            mod.GetAsset<AudioClip>("npc_horse_foot_walk_front_04"),
            mod.GetAsset<AudioClip>("npc_horse_foot_walk_back_04"),
            mod.GetAsset<AudioClip>("npc_horse_foot_walk_front_05"),
            mod.GetAsset<AudioClip>("npc_horse_foot_walk_back_05"),
            mod.GetAsset<AudioClip>("npc_horse_foot_walk_front_06"),
            mod.GetAsset<AudioClip>("npc_horse_foot_walk_back_06"),
        };

        audioHorseStepSprint = new AudioClip[]
        {
            mod.GetAsset<AudioClip>("fst_horse_sprint_front_01"),
            mod.GetAsset<AudioClip>("fst_horse_sprint_back_01"),
            mod.GetAsset<AudioClip>("fst_horse_sprint_front_02"),
            mod.GetAsset<AudioClip>("fst_horse_sprint_back_02"),
            mod.GetAsset<AudioClip>("fst_horse_sprint_front_03"),
            mod.GetAsset<AudioClip>("fst_horse_sprint_back_03"),
            mod.GetAsset<AudioClip>("fst_horse_sprint_front_04"),
            mod.GetAsset<AudioClip>("fst_horse_sprint_back_04"),
            mod.GetAsset<AudioClip>("fst_horse_sprint_front_05"),
            mod.GetAsset<AudioClip>("fst_horse_sprint_back_05"),
            mod.GetAsset<AudioClip>("fst_horse_sprint_front_06"),
            mod.GetAsset<AudioClip>("fst_horse_sprint_back_06"),
        };

        audioHorseNoise = new AudioClip[]
        {
            mod.GetAsset<AudioClip>("Horse Snort 1"),
            mod.GetAsset<AudioClip>("Horse Snort 2"),
            mod.GetAsset<AudioClip>("Horse Snort 3"),
            mod.GetAsset<AudioClip>("Horse Snort 4"),
            mod.GetAsset<AudioClip>("Horse Snort 5"),
            mod.GetAsset<AudioClip>("Horse Snort 6"),
            mod.GetAsset<AudioClip>("Horse Snort 7"),
            mod.GetAsset<AudioClip>("Horse Snort 8"),
            mod.GetAsset<AudioClip>("Horse Snort 9"),
            mod.GetAsset<AudioClip>("Horse Snort 10"),
            mod.GetAsset<AudioClip>("horse_snort_1"),
            mod.GetAsset<AudioClip>("horse_snort_2"),
            mod.GetAsset<AudioClip>("horse_snort_3"),
            mod.GetAsset<AudioClip>("horse_snort_4"),
            mod.GetAsset<AudioClip>("horse_snort_5"),
        };

        audioHorseMount = new AudioClip[]
        {
            mod.GetAsset<AudioClip>("Horse Mount 1"),
            mod.GetAsset<AudioClip>("Horse Mount 2"),
        };

        audioHorseDismount = new AudioClip[]
        {
            mod.GetAsset<AudioClip>("Horse Dismount 1"),
            mod.GetAsset<AudioClip>("Horse Dismount 2"),
        };

        audioHorseCollision = new AudioClip[]
        {
            mod.GetAsset<AudioClip>("Horse Rearup 1"),
            mod.GetAsset<AudioClip>("Horse Rearup 2"),
            mod.GetAsset<AudioClip>("Horse Rearup 3"),
        };

        audioHorseWinded = new AudioClip[]
        {
            mod.GetAsset<AudioClip>("horse_exterior_whinny_01"),
            mod.GetAsset<AudioClip>("horse_exterior_whinny_02"),
            mod.GetAsset<AudioClip>("horse_exterior_whinny_03"),
            mod.GetAsset<AudioClip>("horse_exterior_whinny_04"),
            mod.GetAsset<AudioClip>("horse_exterior_whinny_05"),
            mod.GetAsset<AudioClip>("horse_whinny"),
            mod.GetAsset<AudioClip>("horse_whinny-1"),
            mod.GetAsset<AudioClip>("horse_whinny-2"),
        };

        audioCamelNoise = new AudioClip[]
        {
            mod.GetAsset<AudioClip>("CamelDromedary_Adult_GrowlA"),
            mod.GetAsset<AudioClip>("CamelDromedary_Adult_GruntA"),
            mod.GetAsset<AudioClip>("CamelDromedary_Adult_SnortA"),
            mod.GetAsset<AudioClip>("CamelDromedary_Adult_SpitA"),
            mod.GetAsset<AudioClip>("CamelDromedary_Adult_Yawn"),
        };

        audioCamelCollision = new AudioClip[]
        {
            mod.GetAsset<AudioClip>("CamelDromedary_Adult_BellowA"),
        };

        audioCamelWinded = new AudioClip[]
        {
            mod.GetAsset<AudioClip>("CamelDromedary_Adult_ContactCallA"),
        };

        mod.MessageReceiver = MessageReceiver;
        mod.IsReady = true;
    }

    private void ModCompatibilityChecking()
    {
        //listen to Eye Of The Beholder for changes in POV
        Mod eotb = ModManager.Instance.GetModFromGUID("2942ea8c-dbd4-42af-bdf9-8199d2f4a0aa");
        if (eotb != null)
        {
            //tell EOTB this mod exists
            ModManager.Instance.SendModMessage(eotb.Title, "hasFreeRein");

            //subscribe to EOTB's OnToggleOffset event
            ModManager.Instance.SendModMessage(eotb.Title, "onToggleOffset", (Action<bool>)(toggleState => {
                isInThirdPerson = toggleState;
            }));
        }

        //Check for Travel Options mod
        Mod travelOptions = ModManager.Instance.GetModFromGUID("93f3ad1c-83cc-40ac-b762-96d2f47f2e05");
        hasTravelOptions = travelOptions != null ? true : false;
    }

    void MirroCombatEvents()
    {
        //grab VCEH's events to use it for our own
        Mod ceh = ModManager.Instance.GetModFromGUID("fb086c76-38e7-4d83-91dc-f29e6f1bb17e");
        if (ceh != null)
        {
            ModManager.Instance.SendModMessage(ceh.Title, "getAttackDamageCalculatedEvent", null, (string message, object data) =>
            {
                OnAttackDamageCalculated = (Action<DaggerfallEntity, DaggerfallEntity, DaggerfallUnityItem, int, int>)data;
            });

        }
    }

    void MessageReceiver(string message, object data, DFModMessageCallback callBack)
    {
        switch (message)
        {
            case "getMoveVector":
                callBack?.Invoke("getMoveVector", moveVector);
                break;
            case "setModHorseMove":
                SetModHorseMove((Vector3)data);
                break;
            case "setModHorseTurn":
                SetModHorseTurn((Vector3)data);
                break;
            case "setModCartMove":
                SetModCartMove((Vector3)data);
                break;
            case "setModCartTurn":
                SetModCartTurn((Vector3)data);
                break;
            case "setModGallop":
                SetModGallop((Vector2)data);
                break;
            case "setModTrample":
                SetModTrample((Vector2)data);
                break;
            case "setRideVariant":
                SetModRideVariant((Vector2Int)data);
                break;
            case "resetHorse":
                ResetVariables();
                break;

            default:
                Debug.LogErrorFormat("{0}: unknown message received ({1}).", this, message);
                break;
        }
    }

    void SetModHorseMove(Vector3 mods)
    {
        modMoveSpeed = mods.x;
        modMoveAccel = mods.y;
        modMoveBrake = mods.z;
    }

    void SetModHorseTurn(Vector3 mods)
    {
        modTurnSpeed = mods.x;
        modTurnAccel = mods.y;
        modTurnBrake = mods.z;
    }

    void SetModCartMove(Vector3 mods)
    {
        modCartMoveSpeed = mods.x;
        modCartMoveAccel = mods.y;
        modCartMoveBrake = mods.z;
    }

    void SetModCartTurn(Vector3 mods)
    {
        modCartTurnSpeed = mods.x;
        modCartTurnAccel = mods.y;
        modCartTurnBrake = mods.z;
    }

    void SetModGallop(Vector2 mods)
    {
        modCanRun = mods.x == 1 ? true : false;
        modRunSpeed = mods.y;

        if (wasRiding)
        {
            GameManager.Instance.SpeedChanger.RemoveSpeedMod(modRunSpeedUID, true, true);
            GameManager.Instance.SpeedChanger.AddRunSpeedMod(out modRunSpeedUID, modRunSpeed);
        }
    }

    void SetModTrample(Vector2 mods)
    {
        modTrampleAccuracy = mods.x;
        modTrampleDamage = mods.y;
    }

    void SetModRideVariant(Vector2Int mods)
    {
        rideIndexMod = mods.x;
        variantIndexMod = mods.y;
    }

    public static void OnNewGame()
    {
        Instance.MirroCombatEvents();
    }

    public static void OnPreFastTravel(DaggerfallTravelPopUp daggerfallTravelPopUp)
    {
        Instance.ResetVariables();
    }

    public static void OnPostFastTravel()
    {
        Instance.ResetVariablesWithDelay();
    }

    public static void OnExhausted(DaggerfallEntity entity)
    {
        Instance.ResetVariables();
    }

    public static void OnStartLoad(SaveData_v1 saveData)
    {
        Instance.ResetVariables();
    }

    public static void OnLoad(SaveData_v1 saveData)
    {
        Instance.ResetVariablesWithDelay();
        Instance.MirroCombatEvents();
    }

    public void ResetVariablesWithDelay(float delay = 1)
    {
        StartCoroutine(ResetVariablesWithDelayCoroutine(delay));
    }

    IEnumerator ResetVariablesWithDelayCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        ResetVariables();
    }

    private void LoadSettings(ModSettings settings, ModSettingsChange change)
    {
        widget.Paused = true;

        int oldGalloping = galloping;
        int oldFrameCount = widget.sprintFrameCount;
        if (change.HasChanged("Controls"))
        {
            steering = settings.GetValue<int>("Controls", "DefaultSteeringType");
            throttle = settings.GetValue<int>("Controls", "DefaultThrottleType");
            view = settings.GetValue<int>("Controls", "DefaultViewType");
            steeringKeyCode = SetKeyFromText(settings.GetString("Controls", "CycleSteeringTypeInput"));
            throttleKeyCode = SetKeyFromText(settings.GetString("Controls", "CycleThrottleTypeInput"));
            viewKeyCode = SetKeyFromText(settings.GetString("Controls", "CycleViewTypeInput"));
            centerHorseKeyCode = SetKeyFromText(settings.GetString("Controls", "AlignHorseToViewInput"));
            centerViewKeyCode = SetKeyFromText(settings.GetString("Controls", "AlignViewToHorseInput"));
        }
        if (change.HasChanged("Handling"))
        {
            galloping = settings.GetValue<int>("Handling", "Galloping");
            staminaDrainBase = 2-settings.GetValue<float>("Handling", "GallopMaxStamina");
            staminaDrainThrottle = settings.GetValue<float>("Handling", "GallopStaminaDrain")+0.5f;
            tokenDuration = settings.GetValue<float>("Handling", "GallopDuration");
            tokenMax = settings.GetValue<int>("Handling", "MaxGallopTokens");
            tokenCurrent = tokenMax;
            if (galloping == 3)
                widget.sprintFrameCount = tokenMax;
            else
                widget.sprintFrameCount = settings.GetValue<int>("Widget", "SprintStaminaFrames");
            tokenTime = (float)settings.GetValue<int>("Handling", "GallopTokenCooldown");
            limitYaw = settings.GetValue<bool>("Handling", "LimitYawAngle");
            limitYawAngle = settings.GetValue<int>("Handling", "MaximumYawAngle");
            acceleration = settings.GetValue<bool>("Handling", "Acceleration");
            moveAcceleration = settings.GetValue<float>("Handling", "MoveAcceleration");
            moveBraking = settings.GetValue<float>("Handling", "MoveBraking");
            turnSpeed = settings.GetValue<float>("Handling", "TurnSpeed");
            turnAcceleration = settings.GetValue<float>("Handling", "TurnAcceleration");
            turnBraking = settings.GetValue<float>("Handling", "TurnBraking");
            trample = settings.GetValue<int>("Handling", "Trampling");
            trampleSkill = settings.GetValue<int>("Handling", "HorseTrampleSkill");
        }
        if (change.HasChanged("CustomAudio"))
        {
            customAudio = settings.GetValue<bool>("CustomAudio", "Enable");
            customAudioVolume = settings.GetValue<float>("CustomAudio", "Volume");
            if (customAudio && ridingAudioSource != null)
                ridingAudioSource.pitch = 1;
            playerVocalization = settings.GetValue<bool>("CustomAudio", "PlayerSprintVocalization");
        }
        if (change.HasChanged("Inertia"))
        {
            inertia = settings.GetValue<bool>("Inertia", "Enable");
            inertiaSpeed = settings.GetValue<float>("Inertia", "Speed");
            inertiaStrength = settings.GetValue<float>("Inertia", "Strength");
        }
        if (change.HasChanged("SlopeFollowing"))
        {
            slopeFollow = settings.GetValue<bool>("SlopeFollowing", "Enable");
            slopeFollowSpeed = settings.GetValue<float>("SlopeFollowing", "Speed");
            slopeFollowStrength = settings.GetValue<float>("SlopeFollowing", "Strength");
        }
        if (change.HasChanged("Widget"))
        {
            widget.scaleToScreen = settings.GetValue<int>("Widget", "Scaling");
            widget.speed = settings.GetValue<bool>("Widget", "Speed");
            widget.speedOffset = new Vector2(settings.GetTupleFloat("Widget", "SpeedPosition").First, settings.GetTupleFloat("Widget", "SpeedPosition").Second);
            widget.speedScale = settings.GetValue<float>("Widget", "SpeedScale");
            widget.speedColor = settings.GetColor("Widget", "SpeedColor");
            widget.heading = settings.GetValue<bool>("Widget", "Heading");
            widget.headingOffset = new Vector2(settings.GetTupleFloat("Widget", "HeadingPosition").First, settings.GetTupleFloat("Widget", "HeadingPosition").Second);
            widget.headingScale = settings.GetValue<float>("Widget", "HeadingScale");
            widget.headingColor = settings.GetColor("Widget", "HeadingColor");
            widget.headingIntervalIndex = settings.GetValue<int>("Widget", "HeadingInterval");
            widget.sprint = settings.GetValue<bool>("Widget", "Sprint");
            widget.sprintOffset = new Vector2(settings.GetTupleFloat("Widget", "SprintPosition").First, settings.GetTupleFloat("Widget", "SprintPosition").Second);
            widget.sprintScale = settings.GetValue<float>("Widget", "SprintScale");
            widget.sprintColor = settings.GetColor("Widget", "SprintColor");
            if (galloping == 3)
                widget.sprintFrameCount = tokenMax;
            else
                widget.sprintFrameCount = settings.GetValue<int>("Widget", "SprintStaminaFrames");
            showCharge = settings.GetValue<bool>("Widget", "HideSprint");
            showChargeTime = settings.GetValue<float>("Widget", "HideSprintTime");
        }
        if (change.HasChanged("Compatibility"))
        {
            fullSpeedOnTravel = settings.GetValue<bool>("Compatibility", "FullSpeedOnTravel");
            TextureScaleFactor = settings.GetValue<float>("Compatibility", "TextureScaleFactor");
        }
        if (change.HasChanged("Graphics"))
        {
            rideIndex = settings.GetValue<int>("Graphics", "Riding");
            variantIndex = settings.GetValue<int>("Graphics", "Variant");
        }

        if (change.HasChanged("Widget") || oldGalloping != galloping || oldFrameCount != widget.sprintFrameCount)
            widget.Initialize();

        widget.Paused = false;
    }

    void ResetVariables()
    {
        speedIndex = 1;

        //Set Yaw to player facing
        Yaw = Vector3.SignedAngle(Vector3.forward,playerObject.transform.forward,Vector3.up);

        targetYaw = 0;
        currentYaw = 0;
        targetThrottle = 0;
        currentThrottle = 0;
        lastThrottle = 0;

        currentInertia = 1;

        moveVector = Quaternion.AngleAxis(Yaw, Vector3.up) * Vector3.forward;
        lastMoveVector = moveVector;
    }

    private void FixedUpdate()
    {
        if (GameManager.IsGamePaused)
            return;

        if (playerMotor.IsRiding)
        {
            if (hasTravelOptions)
            {
                ModManager.Instance.SendModMessage("TravelOptions", "isTravelActive", null, (string message, object data) =>
                {
                    isTravelling = (bool)data;
                });
            }
        }
    }

    private void Update()
    {
        if (!GameManager.IsGamePaused)
        {
            if (playerMotor.IsRiding)
            {
                bool sneaking = GameManager.Instance.SpeedChanger.isSneaking;
                bool running = GameManager.Instance.SpeedChanger.isRunning;

                Tint = transportManager.Tint;

                if (hasTravelOptions)
                {
                    //just started travelling
                    if (isTravelling && !wasTravelling)
                    {
                        //do thing
                        if (fullSpeedOnTravel)
                        {
                            if (throttle == 1)
                                InputManager.Instance.ToggleAutorun = true;
                            else
                                speedIndex = speeds.Length - 1;
                        }

                        wasTravelling = isTravelling;
                    }

                    //just stopped travelling
                    if (!isTravelling && wasTravelling)
                    {
                        ResetVariables();

                        //do thing
                        if (fullSpeedOnTravel)
                        {
                            if (throttle == 1)
                                InputManager.Instance.ToggleAutorun = false;
                            else
                                speedIndex = 1;
                        }

                        wasTravelling = isTravelling;
                    }
                }

                //if transport mode changed or just started riding
                if (lastTransportMode != transportManager.TransportMode || !wasRiding)
                {
                    //Just started riding
                    if (!wasRiding)
                    {
                        wasRiding = true;

                        ResetVariables();
                        playerMotor.limitDiagonalSpeed = false;

                        //GameManager.Instance.SpeedChanger.AddWalkSpeedMod(out speedWalkModUID, speedWalkMod);
                        GameManager.Instance.SpeedChanger.AddRunSpeedMod(out modRunSpeedUID, modRunSpeed);
                    }

                    // Setup appropriate riding textures.

                    string textureName;
                    if (rideIndex > 0)
                    {
                        int modeOffset = (transportManager.TransportMode == TransportModes.Horse) ? 0 : 1;
                        int record = (variantIndex * 2) + modeOffset;
                        textureName = camelArchive.ToString() + record.ToString();
                        Debug.Log("FREE REIN - Riding record is " + record.ToString());
                        if (textureName != lastTextureName)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                Texture2D texture;
                                DaggerfallWorkshop.Utility.AssetInjection.TextureReplacement.TryImportTexture(camelArchive, record, i, out texture);
                                ridingTextures[i] = texture;
                                ridingTextures[i].filterMode = DaggerfallUnity.Instance.MaterialReader.MainFilterMode;
                            }

                            ridingTexture = ridingTextures[0];
                            lastTextureName = textureName;
                        }
                    }
                    else
                    {
                        textureName = (transportManager.TransportMode == TransportModes.Horse) ? horseTextureName : cartTextureName;
                        if (textureName != lastTextureName)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                ridingTextures[i] = ImageReader.GetImageData(textureName, 0, i, true, true).texture;
                                ridingTextures[i].filterMode = DaggerfallUnity.Instance.MaterialReader.MainFilterMode;
                            }

                            ridingTexture = ridingTextures[0];
                            lastTextureName = textureName;
                        }
                    }

                    // Setup appropriate riding sounds.
                    SoundClips sound = (transportManager.TransportMode == TransportModes.Horse) ? horseRidingSound2 : cartRidingSound;
                    ridingAudioSource.clip = dfAudioSource.GetAudioClip((int)sound);
                    lastTransportMode = transportManager.TransportMode;

                    if (customAudio)
                        dfAudioSource.AudioSource.PlayOneShot(audioHorseMount[UnityEngine.Random.Range(0, audioHorseMount.Length)], 0.25f * DaggerfallUnity.Settings.SoundVolume * customAudioVolume);
                }

                //make a noise when RUN is pressed
                if (InputManager.Instance.ActionStarted(InputManager.Actions.Run))
                {
                    if (customAudio)
                    {
                        if (playerVocalization)
                        {
                            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
                            SoundClips sound = DaggerfallEntity.GetRaceGenderAttackSound(playerEntity.Race, playerEntity.Gender, true);
                            float pitch = dfAudioSource.AudioSource.pitch;
                            dfAudioSource.AudioSource.pitch = pitch + UnityEngine.Random.Range(0, 0.3f);
                            dfAudioSource.PlayOneShot(sound, 1, 0.5f * DaggerfallUnity.Settings.SoundVolume * customAudioVolume);
                            dfAudioSource.AudioSource.pitch = pitch;
                        }
                    }
                }

                //slope following
                if (slopeFollow)
                {
                    //check slope
                    if (Physics.Raycast(playerObject.transform.position, Vector3.down, out RaycastHit hit))
                        targetSlopeAngle = Vector3.SignedAngle(Vector3.up, hit.normal, Quaternion.AngleAxis(90, Vector3.up) * lastMoveVector) * slopeFollowStrength;
                    else
                        targetSlopeAngle = 0;

                    //move current slope angle to target slope angle
                    //currentSlopeAngle = Mathf.MoveTowards(currentSlopeAngle, targetSlopeAngle, 45 * slopeFollowSpeed * Time.deltaTime);
                    currentSlopeAngle = Mathf.Lerp(currentSlopeAngle, targetSlopeAngle, 1 * slopeFollowSpeed * Time.deltaTime);
                }
                else
                    currentSlopeAngle = 0;

                GameManager.Instance.PlayerMouseLook.PitchMaxLimit = 45f + currentSlopeAngle + currentGravityOffset;

                bool isRidingCart = transportManager.TransportMode == TransportModes.Cart ? true : false;
                float cartMoveSpeedMod = 1;
                float cartMoveAccelMod = 1;
                float cartMoveBrakeMod = 1;
                float cartTurnSpeedMod = 1;
                float cartTurnAccelMod = 1;
                float cartTurnBrakeMod = 1;
                if (isRidingCart)
                {
                    cartMoveSpeedMod = modCartMoveSpeed;
                    cartMoveAccelMod = modCartMoveAccel * 0.5f;
                    cartMoveBrakeMod = modCartMoveBrake * 0.5f;
                    cartTurnSpeedMod = modCartTurnSpeed * 0.5f;
                    cartTurnAccelMod = modCartTurnAccel * 0.5f;
                    cartTurnBrakeMod = modCartTurnBrake * 0.5f;
                }

                transportManager.DrawHorse = false;

                //cycle modes
                if (InputManager.Instance.GetKeyUp(steeringKeyCode))
                {
                    if (steering == 1)
                        steering = 0;
                    else
                        steering = 1;

                    if (steering == 1)
                        DaggerfallUI.Instance.PopupMessage("You grasp the reins");
                    else
                        DaggerfallUI.Instance.PopupMessage("You let go of the reins");
                }

                if (InputManager.Instance.GetKeyUp(throttleKeyCode))
                {
                    if (throttle == 1)
                        throttle = 0;
                    else
                        throttle = 1;

                    if (throttle == 1)
                    {
                        speedIndex = 1;
                        DaggerfallUI.Instance.PopupMessage("Hold throttle");
                    }
                    else
                        DaggerfallUI.Instance.PopupMessage("Press throttle");
                }

                if (InputManager.Instance.GetKeyUp(viewKeyCode))
                {
                    if (view == 1)
                        view = 0;
                    else
                        view = 1;

                    if (view == 1)
                        DaggerfallUI.Instance.PopupMessage("Locked view");
                    else
                        DaggerfallUI.Instance.PopupMessage("Free view");
                }

                //Autorun input funcitonality
                if (InputManager.Instance.ActionStarted(InputManager.Actions.AutoRun))
                {
                    if (throttle == 1)
                    {
                        //do something?
                    }
                    else
                    {
                        if (speedIndex == 0)
                            speedIndex = 1;    //in reverse, stop
                        else
                            speedIndex = speeds.Length - 1;    //otherwise, go full speed
                    }
                }

                strafeVector = Vector3.zero;

                //change yaw input
                if (steering == 1 || isTravelling)
                {
                    //Horizontal controls
                    if ((InputManager.Instance.HasAction(InputManager.Actions.MoveLeft) || InputManager.Instance.HasAction(InputManager.Actions.MoveRight)))
                    {
                        //strafe if stationary and not riding a cart
                        //if moving, slightly yaw the horse
                        if (((throttle == 0 && speedIndex == 1) || (throttle == 1 && currentThrottle == 0)) && !isRidingCart)
                        {
                            if (Mathf.Abs(angleYaw) > 1)
                                targetYaw = -90 * turnSpeed * modTurnSpeed * cartTurnSpeedMod * Mathf.Clamp(angleYaw / 30, -1, 1) * Time.deltaTime;
                            else
                                targetYaw = 0;

                            if (InputManager.Instance.HasAction(InputManager.Actions.MoveLeft))
                                strafeVector = -playerObject.transform.right * 0.25f;

                            if (InputManager.Instance.HasAction(InputManager.Actions.MoveRight))
                                strafeVector = playerObject.transform.right * 0.25f;
                        }
                        else
                        {
                            if (Mathf.Abs(angleYaw) > 31)
                                targetYaw = -90 * turnSpeed * modTurnSpeed * cartTurnSpeedMod * Mathf.Clamp(angleYaw / 30, -1, 1) * Time.deltaTime;
                            else
                                targetYaw = 0;

                            if (InputManager.Instance.HasAction(InputManager.Actions.MoveLeft) && angleYaw > -30)
                                targetYaw = -45 * turnSpeed * modTurnSpeed * cartTurnSpeedMod * Time.deltaTime;

                            if (InputManager.Instance.HasAction(InputManager.Actions.MoveRight) && angleYaw < 30)
                                targetYaw = 45 * turnSpeed * modTurnSpeed * cartTurnSpeedMod * Time.deltaTime;
                        }
                    }
                    else
                    {
                        if (Mathf.Abs(angleYaw) > 1)
                            targetYaw = -90 * turnSpeed * modTurnSpeed * cartTurnSpeedMod * Mathf.Clamp(angleYaw / 30, -1, 1) * Time.deltaTime;
                        else
                            targetYaw = 0;
                    }

                    Yaw += targetYaw;
                }
                else
                {
                    //center view to horse if input pressed
                    if (InputManager.Instance.GetKeyDown(centerViewKeyCode))
                    {
                        playerMouseLook.SetHorizontalFacing(moveVector);
                    }

                    float turnForce = turnBraking * modTurnBrake * cartTurnBrakeMod;

                    if (InputManager.Instance.HasAction(InputManager.Actions.MoveLeft) || InputManager.Instance.HasAction(InputManager.Actions.MoveRight))
                    {
                        if (InputManager.Instance.HasAction(InputManager.Actions.MoveLeft))
                            targetYaw = -90 * turnSpeed * modTurnSpeed * cartTurnSpeedMod * Time.deltaTime;

                        if (InputManager.Instance.HasAction(InputManager.Actions.MoveRight))
                            targetYaw = 90 * turnSpeed * modTurnSpeed * cartTurnSpeedMod * Time.deltaTime;

                        turnForce = turnAcceleration * modTurnAccel * cartTurnAccelMod;
                    }
                    else
                        targetYaw = 0;

                    //center horse to view while input held
                    if (InputManager.Instance.GetKey(centerHorseKeyCode))
                        targetYaw = -90 * turnSpeed * modTurnSpeed * Mathf.Clamp(angleYaw / 30, -1, 1) * Time.deltaTime;

                    //move currentYaw to targetYaw
                    currentYaw = Mathf.MoveTowards(currentYaw, targetYaw, 3 * turnForce * Time.deltaTime);

                    if (acceleration && !isTravelling)
                        Yaw += currentYaw;
                    else
                        Yaw += targetYaw;

                    //if view is locked, add horse horizontal movement to MouseLook Yaw
                    if (view == 1 && currentYaw != 0)
                    {
                        if (acceleration)
                            playerMouseLook.Yaw += currentYaw + InputManager.Instance.LookX;
                        else
                            playerMouseLook.Yaw += targetYaw + InputManager.Instance.LookX;
                        playerMouseLook.Pitch -= InputManager.Instance.LookY;
                        playerMouseLook.SetFacing(playerMouseLook.Yaw, playerMouseLook.Pitch);
                    }
                }

                //change speed input
                if (throttle == 1)
                {
                    if (InputManager.Instance.ToggleAutorun)
                    {
                        targetThrottle = speeds[speeds.Length - 1];
                    }
                    else
                    {
                        if (InputManager.Instance.HasAction(InputManager.Actions.MoveForwards) || InputManager.Instance.HasAction(InputManager.Actions.MoveBackwards))
                        {
                            if (InputManager.Instance.HasAction(InputManager.Actions.MoveBackwards) && currentThrottle > speeds[0])
                                targetThrottle = speeds[0];
                            else if (InputManager.Instance.HasAction(InputManager.Actions.MoveForwards) && currentThrottle < speeds[speeds.Length - 1])
                                targetThrottle = speeds[speeds.Length - 1];
                        }
                        else
                            targetThrottle = 0;
                    }

                    //prevent cart from reversing
                    if (isRidingCart && targetThrottle < 0)
                        targetThrottle = 0;
                }
                else
                {
                    if (InputManager.Instance.ActionStarted(InputManager.Actions.MoveBackwards) && speedIndex > 0)
                        speedIndex--;

                    if (InputManager.Instance.ActionStarted(InputManager.Actions.MoveForwards) && speedIndex < speeds.Length - 1)
                        speedIndex++;

                    //prevent cart from reversing
                    if (isRidingCart && speedIndex < 1)
                        speedIndex = 1;
                }

                //Sprint interrupt
                //Pressing RUN always goes full speed unless in reverse
                if (InputManager.Instance.HasAction(InputManager.Actions.Run) && !colliding)
                {
                    //if not in reverse, go full speed
                    if (throttle == 1)
                    {
                        if (targetThrottle >= 0)
                            targetThrottle = 1;
                    }
                    else
                    {
                        if (speedIndex > 0)
                            speedIndex = speeds.Length - 1;
                    }
                }

                //SNEAK is now a handbrake
                if (InputManager.Instance.HasAction(InputManager.Actions.Sneak))
                {
                    //go to stop
                    if (throttle == 1)
                        targetThrottle = 0;
                    else
                        speedIndex = 1;

                    //disable sneak
                    GameManager.Instance.SpeedChanger.sneakingMode = false;
                }

                //collision
                colliding = false;
                if ((throttle == 0 && speedIndex != 1) || (throttle == 1 && currentThrottle != 0))
                {
                    Ray ray = new Ray(playerObject.transform.position + (Vector3.down * 0.5f), (moveVector * targetThrottle).normalized);
                    RaycastHit hit = new RaycastHit();
                    Debug.DrawRay(ray.origin, ray.direction * 3, Color.red, 0.1f, false);
                    if (Physics.Raycast(ray, out hit, 3))
                    {
                        //hit something
                        //if hit object is a mesh or non-mobile billboard (eg, building or decoration), stop
                        DaggerfallMesh dfMesh = hit.collider.gameObject.GetComponent<DaggerfallMesh>();
                        DaggerfallBillboard dfBillboard = hit.collider.gameObject.GetComponent<DaggerfallBillboard>();
                        if (dfMesh != null || (dfBillboard != null && !dfBillboard.Summary.IsMobile))
                        {
                            //play rear up noise
                            if (customAudio && Time.time - lastCollision > 2)
                            {
                                if ((rideIndexMod < 0 && rideIndex == 1) || rideIndexMod == 1)    //camel
                                    dfAudioSource.AudioSource.PlayOneShot(audioCamelCollision[UnityEngine.Random.Range(0, audioCamelCollision.Length)], 0.5f * DaggerfallUnity.Settings.SoundVolume * customAudioVolume);
                                else
                                    dfAudioSource.AudioSource.PlayOneShot(audioHorseCollision[UnityEngine.Random.Range(0, audioHorseCollision.Length)], 0.5f * DaggerfallUnity.Settings.SoundVolume * customAudioVolume);
                                lastCollision = Time.time;
                            }

                            if (throttle == 1)
                                targetThrottle = 0;
                            else
                                speedIndex = 1;
                            colliding = true;
                        }
                    }
                }

                if (galloping == 3 && !GameManager.IsGamePaused)
                {
                    //handle charges and cooldowns
                    //do not count cooldown while charging
                    if (tokenCurrent < tokenMax && charging == null)
                    {
                        float chargeTimeFinal = tokenTime + (tokenTime * (-0.5f+currentThrottle));
                        Debug.Log(chargeTimeFinal.ToString());
                        if (tokenTimer > chargeTimeFinal)
                        {
                            tokenCurrent++;
                            tokenTimer = 0;
                        }
                        else
                            tokenTimer += Time.deltaTime;
                    }

                    //handle input
                    if (InputManager.Instance.ActionStarted(InputManager.Actions.Run))
                        TryStartCharge();

                    if (tokenCurrent == tokenMax)
                    {
                        if (showChargeTimer < showChargeTime)
                            showChargeTimer += Time.deltaTime;
                    }
                    else
                        showChargeTimer = 0;
                }

                if (galloping == 2 && !GameManager.IsGamePaused)
                {

                    float runMod = playerMotor.IsRunning ? 2 : 1;
                    float reverseMod = currentThrottle < 0 ? 4 : 1;

                    staminaDrain = (staminaMax*(0.5f*staminaDrainBase)) * (1 - ((0.5f*staminaDrainThrottle) * (Mathf.Abs(currentThrottle) * reverseMod * runMod)));

                    staminaCurrent += staminaDrain * Time.deltaTime;

                    if (staminaCurrent > staminaMax)
                        staminaCurrent = staminaMax;
                    else if (staminaCurrent < 0)
                    {
                        if (!isStaminaDrained)
                        {
                            if ((rideIndexMod < 0 && rideIndex == 1) || rideIndexMod == 1)    //camel
                                dfAudioSource.AudioSource.PlayOneShot(audioCamelWinded[UnityEngine.Random.Range(0, audioCamelWinded.Length)], 0.5f * DaggerfallUnity.Settings.SoundVolume * customAudioVolume);
                            else
                                dfAudioSource.AudioSource.PlayOneShot(audioHorseWinded[UnityEngine.Random.Range(0, audioHorseWinded.Length)], 0.5f * DaggerfallUnity.Settings.SoundVolume * customAudioVolume);
                            isStaminaDrained = true;
                        }
                        if (InputManager.Instance.ToggleAutorun)
                            InputManager.Instance.ToggleAutorun = false;
                        if (GameManager.Instance.SpeedChanger.ToggleRun)
                            GameManager.Instance.SpeedChanger.ToggleRun = false;
                        if (GameManager.Instance.SpeedChanger.runningMode)
                            GameManager.Instance.SpeedChanger.runningMode = false;
                        staminaCurrent = 0;
                    }

                    if (isStaminaDrained)
                    {
                        if (!InputManager.Instance.HasAction(InputManager.Actions.Run) && staminaCurrent > 0)
                            isStaminaDrained = false;
                    }

                    if (staminaCurrent == staminaMax)
                    {
                        if (showChargeTimer < showChargeTime)
                            showChargeTimer += Time.deltaTime;
                    }
                    else
                        showChargeTimer = 0;
                }

                //set variables
                float moveSpeed = modMoveSpeed;
                float moveAccel = modMoveAccel;
                float moveBrake = modMoveBrake;
                if (isRidingCart)
                {
                    moveSpeed = cartMoveSpeedMod;
                    moveAccel = cartMoveAccelMod;
                    moveBrake = cartMoveBrakeMod;
                }

                if (throttle == 1)
                {
                    if (acceleration)
                    {
                        float moveForce = moveBraking * moveBrake;
                        if ((InputManager.Instance.HasAction(InputManager.Actions.MoveForwards) || InputManager.Instance.HasAction(InputManager.Actions.MoveBackwards) || InputManager.Instance.ToggleAutorun) && !colliding)
                            moveForce = moveAcceleration * moveAccel;

                        currentThrottle = Mathf.MoveTowards(currentThrottle, targetThrottle * moveSpeed, moveForce * Time.deltaTime);
                    }
                    else
                        currentThrottle = targetThrottle * moveSpeed;

                    /*if (InputManager.Instance.ToggleAutorun)
                        currentThrottle = Mathf.MoveTowards(currentThrottle, targetThrottle, moveAcceleration * Time.deltaTime);
                    else if (!InputManager.Instance.HasAction(InputManager.Actions.MoveForwards) && !InputManager.Instance.HasAction(InputManager.Actions.MoveBackwards) && !InputManager.Instance.ToggleAutorun)
                        currentThrottle = Mathf.MoveTowards(currentThrottle, 0, moveBraking * Time.deltaTime);*/
                }
                else
                {
                    targetThrottle = speeds[speedIndex];

                    if (acceleration && !isTravelling)
                    {
                        float moveForce = moveBraking * moveBrake;
                        if (speedIndex != 1)
                            moveForce = moveAcceleration * moveAccel;

                        currentThrottle = Mathf.MoveTowards(currentThrottle, targetThrottle * moveSpeed, moveForce * Time.deltaTime);
                    }
                    else
                        currentThrottle = targetThrottle * moveSpeed;
                }

                moveVector = (Quaternion.AngleAxis(Yaw, Vector3.up) * Vector3.forward).normalized;


                angleYaw = Vector3.SignedAngle(playerObject.transform.forward, moveVector, Vector3.up);

                //attempted to make it work with G&G's Free Look
                //angleYaw = Vector3.SignedAngle(playerCamera.transform.forward, moveVector, Vector3.up);

                anglePitch = Vector3.SignedAngle(playerCamera.transform.forward, playerObject.transform.forward, playerObject.transform.right);

                //Vector3 moveVectorLocal = playerObject.transform.InverseTransformDirection(moveVector * currentThrottle + strafeVector) * speedWalkMod;
                Vector3 moveVectorLocal = playerObject.transform.InverseTransformDirection(moveVector * currentThrottle + strafeVector);

                InputManager.Instance.ApplyHorizontalForce(moveVectorLocal.x);
                InputManager.Instance.ApplyVerticalForce(moveVectorLocal.z);

                //get inertia
                if (inertia)
                {
                    float isRunning = GameManager.Instance.PlayerMotor.IsRunning ? 0.2f : 0f;
                    float throttleDiff = currentThrottle - targetThrottle;
                    if (throttleDiff < 0)
                    {
                        targetInertia = 1 - (0.2f - isRunning * inertiaStrength);
                        currentInertia = Mathf.Lerp(currentInertia, targetInertia, 3f * inertiaSpeed * Time.deltaTime);
                    }
                    else if (throttleDiff > 0)
                    {
                        targetInertia = 1 + (0.2f - isRunning * inertiaStrength);
                        currentInertia = Mathf.Lerp(currentInertia, targetInertia, 3f * inertiaSpeed * Time.deltaTime);
                    }
                    else
                    {
                        targetInertia = 1 - (isRunning * inertiaStrength);
                        currentInertia = Mathf.Lerp(currentInertia, targetInertia, 1.5f * inertiaSpeed * Time.deltaTime);
                    }

                    if (GameManager.Instance.AcrobatMotor.Falling)
                    {
                        //move gravity towards value
                        targetGravityOffset = 5f * inertiaStrength * -playerMotor.MoveDirection.y;
                    }
                    else
                    {
                        //move gravity towards 0
                        targetGravityOffset = 0;
                    }
                    currentGravityOffset = Mathf.Lerp(currentGravityOffset, targetGravityOffset, 10 * inertiaSpeed * Time.deltaTime);
                }
                else
                {
                    currentInertia = 1;
                    currentGravityOffset = 0;
                }

                //limit horizontal view
                if (limitYaw)
                {
                    if (angleYaw > limitYawAngle || angleYaw < -limitYawAngle)
                    {
                        //get MouseLook yaw value relative to moveVector
                        float yawAtLimit = Vector3.SignedAngle(Vector3.forward, Quaternion.AngleAxis(-limitYawAngle * Mathf.Sign(angleYaw), Vector3.up) * moveVector, Vector3.up);
                        playerMouseLook.SetFacing(yawAtLimit + currentYaw, playerMouseLook.Pitch);
                    }
                }

                //play landing sound
                if (playerMotor.IsGrounded && !wasGrounded && !(GameManager.Instance.SaveLoadManager.LoadInProgress || GameManager.Instance.StreamingWorld.IsRepositioningPlayer))
                    StartCoroutine(PlayRidingAudioOneShot(gaitLand, 0.1f));
            }
            else
            {
                //Just stopped riding
                if (wasRiding)
                {
                    transportManager.DrawHorse = true;
                    GameManager.Instance.PlayerMouseLook.PitchMaxLimit = 90f;
                    playerMotor.limitDiagonalSpeed = true;

                    //GameManager.Instance.SpeedChanger.RemoveSpeedMod(speedWalkModUID,false,true);
                    GameManager.Instance.SpeedChanger.RemoveSpeedMod(modRunSpeedUID,true,true);

                    if (customAudio)
                    {
                        dfAudioSource.AudioSource.PlayOneShot(audioHorseDismount[UnityEngine.Random.Range(0, audioHorseDismount.Length)], 0.25f * DaggerfallUnity.Settings.SoundVolume * customAudioVolume);

                        if (audioRiding != null)
                            StopRidingAudio();

                        if (ridingAudioSource.isPlaying)
                            StartCoroutine(StopRiding());
                    }
                    else
                    {
                        if (ridingAudioSource.isPlaying)
                            StartCoroutine(StopRiding());
                    }

                    wasRiding = false;
                }
            }
        }

        // Handle horse & cart riding animation & sounds.
        if (transportManager.TransportMode == TransportModes.Horse || transportManager.TransportMode == TransportModes.Cart)
        {
            if ((playerMotor.IsStandingStill && targetYaw == 0) || !playerMotor.IsGrounded || GameManager.IsGamePaused || SaveLoadManager.Instance.LoadInProgress)
            {   // Stop animation frames and sound playing.
                lastFrameTime = 0;
                frameIndex = 0;
                ridingTexture = ridingTextures[0];

                if (customAudio)
                {
                    if (audioRiding != null)
                        StopRidingAudio();
                }

                if (ridingAudioSource.isPlaying)
                    StartCoroutine(StopRiding());
            }
            else
            {
                bool sneaking = GameManager.Instance.SpeedChanger.isSneaking;
                bool running = GameManager.Instance.SpeedChanger.isRunning;

                float sneakMod = sneaking ? 0.8f : 1;
                float runMod = running ? 1.2f : 1;

                if (customAudio && currentGait != null)
                {
                    //if sprinting, stretch animation over two strides
                    float mod = sprint ? 0.025f : 0.05f;
                    animFrameTime = currentGait.Length * mod;
                }
                else
                    animFrameTime = animFrameTimeBase * (2 - ((currentThrottle * runMod * sneakMod) / 1));

                // Update Animation frame?
                if (lastFrameTime == 0)
                {
                    lastFrameTime = Time.unscaledTime;
                }
                else if (Time.unscaledTime > lastFrameTime + animFrameTime)
                {
                    lastFrameTime = Time.unscaledTime;
                    frameIndex = (frameIndex == 3) ? 0 : frameIndex + 1;
                    ridingTexture = ridingTextures[frameIndex];
                }

                if (transportManager.RidingVolumeScale > 0)
                    transportManager.RidingVolumeScale = 0;

                if (customAudio)
                {
                    // refresh audio volume to reflect global changes
                    float volumeScale = 0.5f * sneakMod;
                    ridingAudioSource.volume = volumeScale * DaggerfallUnity.Settings.SoundVolume * customAudioVolume;

                    //play cart sounds
                    if (transportManager.TransportMode == TransportModes.Cart)
                    {
                        if (isTravelling)
                        {
                            if (ridingAudioSource.isPlaying)
                            {
                                ridingAudioSource.Stop();
                            }
                        }
                        else if (!ridingAudioSource.isPlaying)
                        {
                            ridingAudioSource.loop = true;
                            ridingAudioSource.Play();
                        }
                    }

                    //Set gait depending on throttle type and speed
                    bool[] gait = currentGait;
                    if (currentGait == null)
                        gait = gait1;
                    sprint = false;

                    if (transportManager.TransportMode == TransportModes.Cart)
                    {
                        //Carts can't gallop or reverse
                        if (currentThrottle >= 1)
                        {
                            gait = gait3;
                            sprint = false;
                        }
                        else if (currentThrottle >= 0.5f)
                        {
                            gait = gait2;
                            sprint = false;
                        }
                        else if (currentThrottle >= 0.25f)
                        {
                            gait = gait1;
                            sprint = false;
                        }
                        else
                        {
                            gait = gait1;
                            sprint = false;
                        }
                    }
                    else
                    {
                        if (currentThrottle >= 1 && running)
                        {
                            gait = gait5;
                            sprint = true;
                        }
                        else if (currentThrottle >= 1 || (currentThrottle >= 0.5f && running))
                        {
                            gait = gait4;
                            sprint = true;
                        }
                        else if (currentThrottle >= 0.5f || (currentThrottle >= 0.25f && running))
                        {
                            gait = gait3;
                            sprint = false;
                        }
                        else if (currentThrottle >= 0.25f)
                        {
                            gait = gait2;
                            sprint = false;
                        }
                        else
                        {
                            gait = gait1;
                            sprint = false;
                        }
                    }

                    if (audioRiding == null || gait != currentGait)
                        StartRidingAudio(gait);
                }
                else
                {
                    // Get appropriate hoof sound for horse
                    if (transportManager.TransportMode == TransportModes.Horse)
                    {
                        //pendingStopRidingAudio = false;

                        if (currentThrottle * runMod * sneakMod < 0.6f && sprint)
                        {
                            ridingAudioSource.clip = dfAudioSource.GetAudioClip((int)horseRidingSound1);
                            sprint = false;
                        }
                        else if (currentThrottle * runMod * sneakMod >= 0.6f && !sprint)
                        {
                            ridingAudioSource.clip = dfAudioSource.GetAudioClip((int)horseRidingSound2);
                            sprint = true;
                        }

                        if (sprint)
                            ridingAudioSource.pitch = ((Mathf.Abs(currentThrottle) * sneakMod * runMod) / 1f);
                        else
                            ridingAudioSource.pitch = ((Mathf.Abs(currentThrottle) * sneakMod * runMod) / 0.5f);
                    }
                    else if (transportManager.TransportMode == TransportModes.Cart)
                    {
                        sprint = true;
                        ridingAudioSource.pitch = Mathf.Clamp(((Mathf.Abs(currentThrottle) * sneakMod * runMod) / 1f), 0.8f, 1);
                    }

                    if (playerMotor.IsStandingStill || strafeVector.sqrMagnitude > 0)
                        ridingAudioSource.pitch = 0.5f;

                    // refresh audio volume to reflect global changes
                    float volumeScale = 0.5f * sneakMod;

                    ridingAudioSource.volume = volumeScale * DaggerfallUnity.Settings.SoundVolume;

                    if (!ridingAudioSource.isPlaying)
                    {
                        ridingAudioSource.Play();
                    }
                }
            }
            // Time for a whinney?
            if (neighTime < Time.time && dfAudioSource.AudioSource.enabled && !isTravelling)
            {
                if (customAudio)
                {
                    if ((rideIndexMod < 0 && rideIndex == 1) || rideIndexMod == 1)    //camel
                        dfAudioSource.AudioSource.PlayOneShot(audioCamelNoise[UnityEngine.Random.Range(0, audioCamelNoise.Length)], 0.25f * DaggerfallUnity.Settings.SoundVolume * customAudioVolume);
                    else
                        dfAudioSource.AudioSource.PlayOneShot(audioHorseNoise[UnityEngine.Random.Range(0, audioHorseNoise.Length)], 0.25f * DaggerfallUnity.Settings.SoundVolume * customAudioVolume);
                    neighTime = Time.time + UnityEngine.Random.Range(2, 40);
                }
                else
                {
                    dfAudioSource.AudioSource.PlayOneShot(neighClip, 0.5f * DaggerfallUnity.Settings.SoundVolume);
                    neighTime = Time.time + UnityEngine.Random.Range(2, 40);
                }
            }
        }

        wasGrounded = playerMotor.IsGrounded;

        if (currentThrottle != 0)
            lastThrottle = currentThrottle;
        if (moveVector.sqrMagnitude > 0)
            lastMoveVector = moveVector;
    }

    IEnumerator StopRiding()
    {
        //pendingStopRidingAudio = true;
        yield return new WaitForSecondsRealtime(0.2f);
        ridingAudioSource.Stop();
        ridingAudioSource.loop = false;
        //pendingStopRidingAudio = false;
    }

    void OnGUI()
    {
        if (DaggerfallUI.Instance.CustomScreenRect != null)
            screenRect = DaggerfallUI.Instance.CustomScreenRect.Value;
        else
            screenRect = new Rect(0, 0, Screen.width, Screen.height);

        if (Event.current.type.Equals(EventType.Repaint) && !GameManager.IsGamePaused)
        {
            if ((transportManager.TransportMode == TransportModes.Horse || transportManager.TransportMode == TransportModes.Cart) && ridingTexture != null)
            {
                // Draw horse texture behind other HUD elements & weapons.
                GUI.depth = 0;
                // Get horse texture scaling factor. (base on height to avoid aspect ratio issues like fat horses)
                float horseScaleY = (float)screenRect.height / (float)nativeScreenHeight;
                float horseScaleX = horseScaleY * ScaleFactorX;

                // Allow horse to be offset when large HUD enabled
                // This is enabled by default to match classic but can be toggled for either docked/undocked large HUD
                float horseOffsetHeight = 0;
                if (DaggerfallUI.Instance.DaggerfallHUD != null &&
                    DaggerfallUnity.Settings.LargeHUD &&
                    DaggerfallUnity.Settings.LargeHUDOffsetHorse)
                {
                    horseOffsetHeight = (int)DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.ScreenHeight;
                }

                if (!isInThirdPerson)
                {
                    float fov = DaggerfallUnity.Settings.FieldOfView;

                    Vector2 ridingTextureScaled = new Vector2(ridingTexture.width * horseScaleX, ridingTexture.height * horseScaleY) / TextureScaleFactor * currentInertia;

                    float offsetX = ridingTextureScaled.x * (angleYaw / (fov * 0.5f));

                    //flip horse head if using mouse steering
                    if (steering == 1)
                    {
                        if (offsetX > ridingTextureScaled.x * 0.25f)
                            mirror = true;
                        else if (offsetX < ridingTextureScaled.x * -0.25f)
                            mirror = false;
                    }
                    else
                    {
                        if (offsetX > ridingTextureScaled.x * 0.25f)
                            mirror = false;
                        else if (offsetX < ridingTextureScaled.x * -0.25f)
                            mirror = true;
                    }

                    //adjust with mouse look pitch and slope angle as well as gravity
                    //mid is 0.5f
                    float offsetY = ridingTextureScaled.y * (0.5f + ((anglePitch / 90))) + ((currentSlopeAngle+currentGravityOffset) * horseScaleY);

                    //if horse is on the right of the screen, flip the image

                    // Calculate position for horse texture and draw it.
                    if (mirror)
                    {
                        horseRect = new Rect(
                                        (screenRect.x + screenRect.width / 2f + ridingTextureScaled.x / 2f) + offsetX,
                                        (screenRect.y + screenRect.height - ridingTextureScaled.y - horseOffsetHeight) + offsetY,
                                        ridingTextureScaled.x * -1,
                                        ridingTextureScaled.y);
                    }
                    else
                    {
                        horseRect = new Rect(
                                        (screenRect.x + screenRect.width / 2f - ridingTextureScaled.x / 2f) + offsetX,
                                        (screenRect.y + screenRect.height - ridingTextureScaled.y - horseOffsetHeight) + offsetY,
                                        ridingTextureScaled.x,
                                        ridingTextureScaled.y);
                    }
                    DaggerfallUI.DrawTexture(horseRect, ridingTexture, ScaleMode.StretchToFill, true, Tint);
                }
            }
        }
    }
    public Vector2 RotateVector(Vector2 v, float angle)
    {
        float radian = angle * Mathf.Deg2Rad;
        float _x = v.x * Mathf.Cos(radian) - v.y * Mathf.Sin(radian);
        float _y = v.x * Mathf.Sin(radian) + v.y * Mathf.Cos(radian);
        return new Vector2(_x, _y);
    }

    private KeyCode SetKeyFromText(string text)
    {
        Debug.Log("Setting Key");
        if (System.Enum.TryParse(text, false, out KeyCode result))
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

    void StartRidingAudio(bool[] gait, float interval = 0.1f)
    {
        if (audioRiding != null)
            StopCoroutine(audioRiding);

        currentGait = gait;

        audioRiding = PlayRidingAudio(gait, interval);

        StartCoroutine(audioRiding);
    }

    void StopRidingAudio()
    {
        if (audioRiding != null)
            StopCoroutine(audioRiding);

        audioRiding = null;
    }

    void TryStartCharge()
    {
        if (charging != null || tokenCurrent < 1 || transportManager.TransportMode == TransportModes.Cart)
        {
            //play winded noise
            if ((rideIndexMod < 0 && rideIndex == 1) || rideIndexMod == 1)    //camel
                dfAudioSource.AudioSource.PlayOneShot(audioCamelWinded[UnityEngine.Random.Range(0, audioCamelWinded.Length)], 0.5f * DaggerfallUnity.Settings.SoundVolume * customAudioVolume);
            else
                dfAudioSource.AudioSource.PlayOneShot(audioHorseWinded[UnityEngine.Random.Range(0, audioHorseWinded.Length)], 0.5f * DaggerfallUnity.Settings.SoundVolume * customAudioVolume);

            return;
        }

        charging = ChargeCoroutine();

        StartCoroutine(charging);
    }

    IEnumerator ChargeCoroutine()
    {
        tokenCurrent--;
        float currentTime = 0;
        float targetTime = tokenDuration;

        while (currentTime < targetTime)
        {
            if (throttle == 1)
                targetThrottle = 1;
            else
                speedIndex = speeds.Length - 1;

            GameManager.Instance.SpeedChanger.runningMode = true;
            tokenTimer = 0;
            currentTime += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }

        GameManager.Instance.SpeedChanger.runningMode = false;

        charging = null;
    }

    //Based on Hazelnut's Charging code from R&R Enhanced Riding
    public void AttemptTrample(GameObject target, DaggerfallEntityBehaviour targetBehaviour, Vector3 direction)
    {
        Debug.LogFormat("Attempting to trample a {0}!", targetBehaviour.name);

        if (targetBehaviour.Entity is EnemyEntity)
        {
            EnemyEntity hitEnemyEntity = (EnemyEntity)targetBehaviour.Entity;
            EnemyMotor enemyMotor = target.GetComponent<EnemyMotor>();

            //if target is ally or pacified, do not trample
            if (trample == 1 && !enemyMotor.IsHostile)
                return;

            int struckBodyPart = FormulaHelper.CalculateStruckBodyPart();
            int damage = 0;

            //roll for attack based on half of player's Blunt Weapons and half of Horse skill multiplied by Horse Accuracy mod
            if (FormulaHelper.CalculateSuccessfulHit(GameManager.Instance.PlayerEntity, hitEnemyEntity,
                (GameManager.Instance.PlayerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.BluntWeapon)/2+Mathf.RoundToInt(trampleSkill*modTrampleAccuracy)),
                struckBodyPart))
            {
                // Play heavy hit sound.
                EnemySounds enemySounds = target.GetComponent<EnemySounds>();
                MobileUnit entityMobileUnit = target.GetComponentInChildren<MobileUnit>();
                Genders gender;
                if (entityMobileUnit.Summary.Enemy.Gender == MobileGender.Male || hitEnemyEntity.MobileEnemy.ID == (int)MobileTypes.Knight_CityWatch)
                    gender = Genders.Male;
                else
                    gender = Genders.Female;
                enemySounds.PlayCombatVoice(gender, false, true);

                PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;

                //Calculate damage
                //Base damage is calculated from half Blunt Weapons skill plus (horse skill multiplied by horse damage modifier)
                int maxBaseDamage = FormulaHelper.CalculateHandToHandMaxDamage((GameManager.Instance.PlayerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.BluntWeapon)/2)+Mathf.RoundToInt(trampleSkill * modTrampleDamage));
                int minBaseDamage = FormulaHelper.CalculateHandToHandMinDamage((GameManager.Instance.PlayerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.BluntWeapon)/2)+Mathf.RoundToInt(trampleSkill * modTrampleDamage));
                damage = UnityEngine.Random.Range(minBaseDamage, maxBaseDamage + 1);

                //Deal damage to enemy
                DaggerfallEntityBehaviour playerEntityBehaviour = playerEntity.EntityBehaviour;
                targetBehaviour.DamageHealthFromSource(playerEntityBehaviour, damage, true, playerMotor.transform.position + (moveVector * 2) + playerMotor.transform.up);

                //Knockback the enemy, scale magnitude with damage of trample
                enemyMotor.KnockbackSpeed = 500 * damage;
                Vector3 directionRight = Quaternion.Euler(0, 90f, 0) * direction;
                Vector3 dirTarget = target.transform.position - playerObject.transform.position;
                float dot = Vector3.Dot(directionRight.normalized, dirTarget.normalized);
                enemyMotor.KnockbackDirection = Quaternion.Euler(0, Mathf.Sign(dot) * 60f, 0) * direction;

                //Tally Blunt Weapon Skill
                playerEntity.TallySkill(DFCareer.Skills.BluntWeapon, 1);

                Debug.LogFormat("Charged down a {0} for {1} damage! Trample damage is {2} to {3}!", targetBehaviour.name, damage, minBaseDamage, maxBaseDamage);
            }
            else
            {
                //play rear up noise
                if (customAudio && Time.time - lastCollision > 2)
                {
                    if (rideIndex == 1 || rideIndexMod == 1)    //camel
                        dfAudioSource.AudioSource.PlayOneShot(audioCamelCollision[UnityEngine.Random.Range(0, audioCamelCollision.Length)], 0.5f * DaggerfallUnity.Settings.SoundVolume * customAudioVolume);
                    else
                        dfAudioSource.AudioSource.PlayOneShot(audioHorseCollision[UnityEngine.Random.Range(0, audioHorseCollision.Length)], 0.5f * DaggerfallUnity.Settings.SoundVolume * customAudioVolume);
                    lastCollision = Time.time;
                }

                if (hitEnemyEntity.GetWeightInClassicUnits() < 500)
                {
                    //Knockback the enemy away from the path of the horse
                    enemyMotor.KnockbackSpeed = 500;
                    Vector3 directionRight = Quaternion.Euler(0, 90f, 0) * direction;
                    Vector3 dirTarget = target.transform.position - playerObject.transform.position;
                    float dot = Vector3.Dot(directionRight.normalized, dirTarget.normalized);
                    enemyMotor.KnockbackDirection = Quaternion.Euler(0, Mathf.Sign(dot) * 90f, 0) * direction;
                }
            }

            if (Instance.OnAttackDamageCalculated != null)
                Instance.OnAttackDamageCalculated(GameManager.Instance.PlayerEntity, targetBehaviour.Entity, trampleWeapon, struckBodyPart, damage);
        }
    }

    IEnumerator PlayRidingAudio(bool[] gait, float interval)
    {
        int i = 0;
        while (true)
        {
            if (hasTravelOptions && isTravelling)
            {
                //do nothing while in accelerated travel
                yield return new WaitForSeconds(interval);
            }
            else
            {
                foreach (bool step in gait)
                {
                    if (step)
                    {
                        AudioClip[] pace = audioHorseStepWalk;
                        if (sprint)
                            pace = audioHorseStepSprint;

                        ridingAudioSource.PlayOneShot(pace[i], 0.5f * DaggerfallUnity.Settings.SoundVolume * customAudioVolume);

                        if (i < pace.Length - 1)
                            i++;
                        else
                            i = 0;
                    }

                    yield return new WaitForSeconds(interval);
                }
            }
        }
    }

    IEnumerator PlayRidingAudioOneShot(bool[] gait, float interval)
    {

        AudioClip[] pace = audioHorseStepWalk;
        if (sprint)
            pace = audioHorseStepSprint;

        /*DaggerfallDateTime.Seasons playerSeason = DaggerfallUnity.Instance.WorldTime.Now.SeasonValue;
        int playerClimateIndex = GameManager.Instance.PlayerGPS.CurrentClimateIndex;

        if (playerMotor.OnExteriorPath)
        {
            if (sprint)
                pace = audioHorseStepSprintPath;
            else
                pace = audioHorseStepWalkPath;
        }
        else if (playerSeason == DaggerfallDateTime.Seasons.Winter && !WeatherManager.IsSnowFreeClimate(playerClimateIndex) && !playerMotor.OnExteriorStaticGeometry)
        {
            if (sprint)
                pace = audioHorseStepSprintSnow;
            else
                pace = audioHorseStepWalkSnow;
        }*/

        int i = UnityEngine.Random.Range(0,pace.Length);
        foreach (bool step in gait)
        {
            if (step)
            {

                dfAudioSource.AudioSource.PlayOneShot(pace[i], 0.25f * DaggerfallUnity.Settings.SoundVolume * customAudioVolume);

                if (i < pace.Length - 1)
                    i++;
                else
                    i = 0;
            }

            yield return new WaitForSeconds(interval);
        }

        yield return new WaitForSeconds(interval);
    }
}
