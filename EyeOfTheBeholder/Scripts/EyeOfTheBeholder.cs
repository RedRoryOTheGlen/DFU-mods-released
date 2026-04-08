using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;

public class EyeOfTheBeholder : MonoBehaviour
{
    public const string IS_IN_THIRD_PERSON = "isInThirdPerson";
    public const string ON_TOGGLE_OFFSET = "onToggleOffset";

    //other mods
    public const string HAS_FREE_REIN = "hasFreeRein";

    public static EyeOfTheBeholder Instance;

    Camera eye;
    Transform body;
    FPSSpellCasting spellCasting;

    Transform torch = null;
    Vector3 torchPosLocalDefault;

    float speed = 10f;
    float dampen = 1;

    KeyCode offsetKeyCode = KeyCode.KeypadEnter;
    bool offset = true;
    bool offsetDefault = true;

    KeyCode mirrorKeyCode = KeyCode.Tab;
    bool mirror;
    bool mirrorOriginal;
    bool mirrorAuto;
    float mirrorTime = 3;
    float mirrorTimer = 0;

    bool hideWeapon;
    bool hideHorse;
    bool offsetAttacks;

    Vector3 posLocalDefault;
    Vector3 pivotLocal
    {
        get
        {
            return posLocalDefault + Vector3.forward;
        }
    }

    public float offsetX;
    public float offsetY;
    public float offsetZ;
    float offsetScroll;
    Vector3 posOffset {
        get
        {
            if (mirror)
            {
                if (overrideBoat && isSailing)
                    return new Vector3(-overrideBoatOffsetX, overrideBoatOffsetY, overrideBoatOffsetZ - offsetScroll);
                else if (overrideMount && GameManager.Instance.PlayerMotor.IsRiding)
                    return new Vector3(-overrideMountOffsetX, overrideMountOffsetY, overrideMountOffsetZ - offsetScroll);
                else if (overrideWeapon && (!GameManager.Instance.WeaponManager.Sheathed || GameManager.Instance.PlayerEffectManager.HasReadySpell))
                    return new Vector3(-overrideWeaponOffsetX, overrideWeaponOffsetY, overrideWeaponOffsetZ - offsetScroll);
                else
                    return new Vector3(-offsetX, offsetY, offsetZ + (offsetZ * offsetRidingMod) - offsetScroll);
            }
            else
            {
                if (overrideBoat && isSailing)
                    return new Vector3(overrideBoatOffsetX, overrideBoatOffsetY, overrideBoatOffsetZ - offsetScroll);
                else if (overrideMount && GameManager.Instance.PlayerMotor.IsRiding)
                    return new Vector3(overrideMountOffsetX, overrideMountOffsetY, overrideMountOffsetZ - offsetScroll);
                else if (overrideWeapon && (!GameManager.Instance.WeaponManager.Sheathed || GameManager.Instance.PlayerEffectManager.HasReadySpell))
                    return new Vector3(overrideWeaponOffsetX, overrideWeaponOffsetY, overrideWeaponOffsetZ - offsetScroll);
                else
                    return new Vector3(offsetX, offsetY + (offsetY * offsetRidingMod), offsetZ + (offsetZ * offsetRidingMod) - offsetScroll);
            }
        }
    }

    public bool overrideWeapon;
    public float overrideWeaponOffsetX;
    public float overrideWeaponOffsetY;
    public float overrideWeaponOffsetZ;

    public bool overrideMount;
    public float overrideMountOffsetX;
    public float overrideMountOffsetY;
    public float overrideMountOffsetZ;

    Vector3 posCurrent;
    Vector3 posTarget;
    float offsetMinZ;
    float offsetRiding;
    float offsetRidingMod
    {
        get
        {
            if (GameManager.Instance.PlayerMotor.IsRiding)
                return offsetRiding;
            else
                return 0;
        }
    }

    bool billboard;
    int firstpersonBillboard;
    PlayerBillboard billboardPlayer;
    GameObject billboardObject;

    LayerMask layerMask;

    float eyeRadius = 0.25f;
    float boundsX = 2;
    float boundsY = 2;
    float boundsZ = 2;

    //billboard settings
    int indexFoot;
    int indexHorse;
    float size;
    float sizeOffset;
    int readyStance;
    int turnToView;
    int combo;
    float comboTime;
    int comboOffset;
    int torchFollow;

    int playerLayerMask = 0;
    float missileTimer;
    public bool HasSwungWeapon;
    public bool HasFiredMissile;

    bool scrollableOffset;
    bool scrollableOffsetTogglePOV;
    string scrollableOffsetAxis;
    float scrollableIncrement;

    public float walkAnimSpeed;
    public bool footstepSync;

    public bool wagon;
    GameObject wagonObject;
    Vector3 wagonOffsetLast;
    public LayerMask wagonLayerMask;

    //bool autoPOV;
    KeyCode autoPOVSwitchKeyCode = KeyCode.KeypadPlus;
    bool autoPOVSwitch;
    int autoPOVOnFoot;
    int autoPOVOnFootMelee;
    int autoPOVOnFootRanged;
    int autoPOVOnFootSpell;
    int autoPOVOnHorse;
    int autoPOVOnHorseReady;
    int autoPOVOnLycan;
    int autoPOVOnTransitionInterior;
    int autoPOVOnTransitionExterior;

    bool isTransformedPrevious;
    bool isRidingPrevious;
    bool isSpellcastingPrevious;
    bool isSheathedPrevious;
    bool isRangedPrevious;

    bool debugMessages;

    public event Action<bool> OnToggleOffset;

    public bool hasFreeRein;
    Mod FreeRein;

    Mod ComeSailAway;
    public bool isSailing;
    public GameObject boatMeshObject;
    public GameObject boatDriveObject;
    public GameObject boatFlagObject;
    Vector3 boatMeshObjectCenter;
    float boatMeshObjectExtent;

    public bool overrideBoat;
    public float overrideBoatOffsetX;
    public float overrideBoatOffsetY;
    public float overrideBoatOffsetZ;
    public int boatTarget;

    public bool hasTravelOptions;
    public KeyCode tomeOfBattleSwingKeyCode = KeyCode.None;

    static Mod mod;
    [Invoke(StateManager.StateTypes.Start, 0)]

    public static void Init(InitParams initParams)
    {
        mod = initParams.Mod;
        var go = new GameObject(mod.Title);
        go.AddComponent<EyeOfTheBeholder>();
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;

        mod.MessageReceiver = MessageReceiver;
        mod.IsReady = true;
    }

    void Start()
    {
        ModCompatibilityChecking();

        eye = GameManager.Instance.MainCamera;
        posLocalDefault = eye.transform.localPosition;

        body = GameManager.Instance.MainCamera.transform.parent;

        spellCasting = GameManager.Instance.PlayerSpellCasting;

        torch = GameManager.Instance.PlayerObject.GetComponent<EnablePlayerTorch>().PlayerTorch.transform;
        torchPosLocalDefault = torch.localPosition;

        posTarget = eye.transform.position;
        posCurrent = posTarget;

        layerMask |= (1 << LayerMask.NameToLayer("Default"));
        playerLayerMask = ~(1 << LayerMask.NameToLayer("Player"));

        if (billboardObject == null)
            SpawnBillboard();

        if (wagonObject == null)
            SpawnWagon();

        mod.LoadSettingsCallback = LoadSettings;
        mod.LoadSettings();

        offset = offsetDefault;
        ToggleOffset(offset);

        SaveLoadManager.OnLoad += OnLoad;
        StartGameBehaviour.OnNewGame += OnNewGame;

        PlayerEnterExit.OnTransitionInterior += OnTransitionInterior;
        PlayerEnterExit.OnTransitionDungeonInterior += OnTransitionInterior;
        PlayerEnterExit.OnTransitionExterior += OnTransitionExterior;
        PlayerEnterExit.OnTransitionDungeonExterior += OnTransitionExterior;
        FloatingOrigin.OnPositionUpdate += OnPositionUpdate;

        /*GameManager.Instance.WeatherManager.PlayerWeather.RainParticles.transform.SetParent(GameManager.Instance.MainCameraObject.transform);
        GameManager.Instance.WeatherManager.PlayerWeather.SnowParticles.transform.SetParent(GameManager.Instance.MainCameraObject.transform);*/
    }
    private void ModCompatibilityChecking()
    {
        /*//Check for Realistic Wagon mod
        Mod realisticWagon = ModManager.Instance.GetModFromGUID("a30d1974-4d5d-477b-91c0-83f7f4bbd64b");
        hasRealisticWagon = realisticWagon != null ? true : false;*/

        Mod travelOptions = ModManager.Instance.GetModFromGUID("93f3ad1c-83cc-40ac-b762-96d2f47f2e05");
        hasTravelOptions = travelOptions != null ? true : false;

        //check if Tome of Battle is installed
        Mod tob = ModManager.Instance.GetModFromGUID("a166c215-0a5a-4582-8bf3-8be8df80d5e5");
        if (tob != null)
        {
            ModManager.Instance.SendModMessage(tob.Title, "getSwingKeyCode", null, (string message, object data) =>
            {
                tomeOfBattleSwingKeyCode = (KeyCode)data;
            });
        }

        ComeSailAway = ModManager.Instance.GetModFromGUID("dbe3e8ff-9059-45f1-a8be-732bb6000df7");
        if (ComeSailAway != null)
        {
            ModManager.Instance.SendModMessage(ComeSailAway.Title, "OnUpdateSailing", (Action<bool>)OnUpdateSailing);
        }
    }

    public void OnUpdateSailing(bool value)
    {
        isSailing = value;

        if (isSailing)
        {
            Debug.Log("EYE OF THE BEHOLDER - WE ARE NOW SAILING!");
            boatFlagObject = null;
            boatDriveObject = null;

            ModManager.Instance.SendModMessage(ComeSailAway.Title, "GetBoatMeshObject", null, (string message, object data) =>
            {
                boatMeshObject = (GameObject)data;
            });

            Bounds boatMeshObjectBounds = boatMeshObject.GetComponentInChildren<MeshCollider>().bounds;
            boatMeshObjectCenter = boatMeshObject.transform.InverseTransformVector(boatMeshObjectBounds.center - boatMeshObject.transform.position);
            Vector3 e = boatMeshObjectBounds.extents;
            boatMeshObjectExtent = Mathf.Max(Mathf.Max(Mathf.Abs(e.x), Mathf.Abs(e.y)), Mathf.Abs(e.z));

            for (int i = 0; i < boatMeshObject.transform.childCount; i++)
            {
                if (boatFlagObject != null && boatDriveObject != null)
                    break;

                Transform child = boatMeshObject.transform.GetChild(i);
                if (child.name == "FlagObject")
                {
                    boatFlagObject = child.gameObject;
                }

                if (child.name == "DrivePosition")
                {
                    boatDriveObject = child.gameObject;
                }
            }

            if (boatFlagObject == null)
                boatFlagObject = boatMeshObject;
        }
        else
        {
            Debug.Log("EYE OF THE BEHOLDER - WE HAVE STOPPED SAILING!");
        }
    }

    public static void OnNewGame()
    {
        if (Instance.autoPOVSwitch)
        {
            if (GameManager.Instance.PlayerEnterExit.IsPlayerInside)
            {
                if (Instance.autoPOVOnTransitionInterior == 2 && !Instance.offset)
                    Instance.ToggleOffset(true);
                else if (Instance.autoPOVOnTransitionInterior == 1 && Instance.offset)
                    Instance.ToggleOffset(false);
            }
            else
            {
                if (Instance.autoPOVOnTransitionExterior == 2 && !Instance.offset)
                    Instance.ToggleOffset(true);
                else if (Instance.autoPOVOnTransitionExterior == 1 && Instance.offset)
                    Instance.ToggleOffset(false);
            }
        }
        else
            Instance.ToggleOffset(Instance.offsetDefault);
    }

    public static void OnLoad(SaveData_v1 saveData)
    {
        if (Instance.autoPOVSwitch)
        {
            if (GameManager.Instance.PlayerEnterExit.IsPlayerInside)
            {
                if (Instance.autoPOVOnTransitionInterior == 2 && !Instance.offset)
                    Instance.ToggleOffset(true);
                else if (Instance.autoPOVOnTransitionInterior == 1 && Instance.offset)
                    Instance.ToggleOffset(false);
            }
            else
            {
                if (Instance.autoPOVOnTransitionExterior == 2 && !Instance.offset)
                    Instance.ToggleOffset(true);
                else if (Instance.autoPOVOnTransitionExterior == 1 && Instance.offset)
                    Instance.ToggleOffset(false);
            }
        }
        else
            Instance.ToggleOffset(Instance.offsetDefault);
    }

    private void LoadSettings(ModSettings settings, ModSettingsChange change)
    {
        if (change.HasChanged("Camera"))
        {
            offsetDefault = settings.GetValue<bool>("Camera", "StartInThirdPerson");
            offsetKeyCode = SetKeyFromText(settings.GetString("Camera", "TogglePerspective"));
            offsetX = settings.GetTupleFloat("Camera", "FrontalPlaneOffset").First;
            offsetY = settings.GetTupleFloat("Camera", "FrontalPlaneOffset").Second;
            offsetZ = settings.GetValue<float>("Camera", "LongitudinalDistance")*-1;
            offsetMinZ = settings.GetValue<float>("Camera", "MinimumDistance");
            offsetRiding = settings.GetValue<float>("Camera", "RidingOffset");
            speed = settings.GetValue<float>("Camera", "Speed");
            dampen = settings.GetValue<float>("Camera", "Dampen");
            mirrorKeyCode = SetKeyFromText(settings.GetString("Camera", "SwitchShoulder"));
            mirrorAuto = settings.GetValue<bool>("Camera", "Auto-Switch");
            mirrorTime = settings.GetValue<float>("Camera", "SwitchResetTime");
        }
        if (change.HasChanged("CameraOverrideWeapon"))
        {
            overrideWeapon = settings.GetValue<bool>("CameraOverrideWeapon", "Enable");
            overrideWeaponOffsetX = settings.GetTupleFloat("CameraOverrideWeapon", "FrontalPlaneOffset").First;
            overrideWeaponOffsetY = settings.GetTupleFloat("CameraOverrideWeapon", "FrontalPlaneOffset").Second;
            overrideWeaponOffsetZ = settings.GetValue<float>("CameraOverrideWeapon", "LongitudinalDistance") * -1;
        }
        if (change.HasChanged("CameraOverrideMount"))
        {
            overrideMount = settings.GetValue<bool>("CameraOverrideMount", "Enable");
            overrideMountOffsetX = settings.GetTupleFloat("CameraOverrideMount", "FrontalPlaneOffset").First;
            overrideMountOffsetY = settings.GetTupleFloat("CameraOverrideMount", "FrontalPlaneOffset").Second;
            overrideMountOffsetZ = settings.GetValue<float>("CameraOverrideMount", "LongitudinalDistance") * -1;
        }
        if (change.HasChanged("CameraOverrideBoat"))
        {
            overrideBoat = settings.GetValue<bool>("CameraOverrideBoat", "Enable");
            overrideBoatOffsetX = settings.GetTupleFloat("CameraOverrideBoat", "FrontalPlaneOffset").First;
            overrideBoatOffsetY = settings.GetTupleFloat("CameraOverrideBoat", "FrontalPlaneOffset").Second;
            overrideBoatOffsetZ = settings.GetValue<float>("CameraOverrideBoat", "LongitudinalDistance") * -1;
            boatTarget = settings.GetValue<int>("CameraOverrideBoat", "Target");
        }
        if (change.HasChanged("CameraScrolling"))
        {
            scrollableOffset = settings.GetValue<bool>("CameraScrolling", "ScrollableZOffset");
            //scrollableOffsetTogglePOV = settings.GetValue<bool>("CameraScrolling", "TogglePerspective");
            scrollableOffsetAxis = settings.GetValue<string>("CameraScrolling", "ScrollableZOffsetAxis");
            scrollableIncrement = settings.GetValue<float>("CameraScrolling", "ScrollIncrement");
        }
        if (change.HasChanged("Graphics"))
        {
            billboard = settings.GetValue<bool>("Graphics", "Enable");
            indexFoot = settings.GetValue<int>("Graphics", "OnFoot");
            indexHorse = settings.GetValue<int>("Graphics", "OnHorse");
            readyStance = settings.GetValue<int>("Graphics", "ReadyStance");
            turnToView = settings.GetValue<int>("Graphics", "TurnToView");
            combo = settings.GetValue<int>("Graphics", "AttackStrings");
            comboTime = settings.GetValue<float>("Graphics", "MirrorTime");
            comboOffset = settings.GetValue<int>("Graphics", "PingPongOffset");
            firstpersonBillboard = settings.GetValue<int>("Graphics", "FirstPersonBillboard");
            wagon = settings.GetValue<bool>("Graphics", "ShowCart");
            torchFollow = settings.GetValue<int>("Graphics", "TorchOffset");
        }
        if (change.HasChanged("Animation"))
        {
            if (billboardPlayer != null)
            {
                billboardPlayer.footsteps = settings.GetValue<bool>("Animation", "SyncFootsteps");
                billboardPlayer.walkAnimSpeedMod = settings.GetValue<float>("Animation", "WalkCycleSpeed");

                billboardPlayer.lengthsChanged = true;
                sizeOffset = settings.GetValue<float>("Animation", "GlobalOffsetScale") + (settings.GetValue<float>("Animation", "FineGlobalOffsetScale") * 0.1f);
                size = settings.GetValue<float>("Animation", "BillboardScale")+(settings.GetValue<float>("Animation", "FineBillboardScale") * 0.1f);
            }
        }
        if (change.HasChanged("Compatibility"))
        {
            hideWeapon = settings.GetValue<bool>("Compatibility", "Don'tHideWeapon");
            hideHorse = settings.GetValue<bool>("Compatibility", "Don'tHideHorse");
            offsetAttacks = settings.GetValue<bool>("Compatibility", "Don'tOffsetAttacks");
        }
        if (change.HasChanged("AutoTogglePerspective"))
        {
            //autoPOV = settings.GetValue<bool>("AutoTogglePerspective", "Enable");
            //autoPOVSwitch = autoPOV;
            autoPOVSwitchKeyCode = SetKeyFromText(settings.GetString("AutoTogglePerspective", "ToggleInput"));
            autoPOVOnFoot = settings.GetValue<int>("AutoTogglePerspective", "OnFoot");
            autoPOVOnFootMelee = settings.GetValue<int>("AutoTogglePerspective", "OnFootMelee");
            autoPOVOnFootRanged = settings.GetValue<int>("AutoTogglePerspective", "OnFootRanged");
            autoPOVOnFootSpell = settings.GetValue<int>("AutoTogglePerspective", "OnFootSpell");
            autoPOVOnHorse = settings.GetValue<int>("AutoTogglePerspective", "OnHorse");
            autoPOVOnHorseReady = settings.GetValue<int>("AutoTogglePerspective", "OnHorseReady");
            autoPOVOnLycan = settings.GetValue<int>("AutoTogglePerspective", "OnLycan");
            autoPOVOnTransitionInterior = settings.GetValue<int>("AutoTogglePerspective", "OnTransitionInterior");
            autoPOVOnTransitionExterior = settings.GetValue<int>("AutoTogglePerspective", "OnTransitionExterior");

            if (autoPOVOnFoot + autoPOVOnFootMelee + autoPOVOnFootRanged + autoPOVOnFootSpell + autoPOVOnHorse + autoPOVOnHorseReady + autoPOVOnLycan + autoPOVOnTransitionExterior + autoPOVOnTransitionInterior > 0)
                autoPOVSwitch = true;
            else
                autoPOVSwitch = false;
        }

        if (change.HasChanged("Debug"))
        {
            debugMessages = settings.GetValue<bool>("Debug", "ShowMessages");
        }

        if (change.HasChanged("Camera") || change.HasChanged("Graphics") || change.HasChanged("Animation") || change.HasChanged("Compatibility"))
        {
            ToggleOffset(offset);
        }
    }

    void MessageReceiver(string message, object data, DFModMessageCallback callBack)
    {
        switch (message)
        {
            case IS_IN_THIRD_PERSON:
                callBack?.Invoke(IS_IN_THIRD_PERSON, offset);
                break;

            case HAS_FREE_REIN:
                hasFreeRein = true;
                break;

            case ON_TOGGLE_OFFSET:
                OnToggleOffset += data as Action<bool>;
                break;

            default:
                Debug.LogErrorFormat("{0}: unknown message received ({1}).", this, message);
                break;
        }
    }

    private void Update()
    {
        if (GameManager.IsGamePaused)
            return;

        if (InputManager.Instance.GetKeyUp(offsetKeyCode))
            ToggleOffset(!offset);

        float currentScroll = posOffset.z;
        float minimumScroll = -offsetMinZ;

        if (!offset)
        {
            //if (scrollableOffset && scrollableOffsetTogglePOV)
            if (scrollableOffset)
            {
                if (Input.GetAxis(scrollableOffsetAxis) < 0)
                {
                    ToggleOffset(true);
                    offsetScroll = 0;
                }
            }

            return;
        }

        /*if (Vector3.Distance(body.transform.position, posCurrent) > 205)
            posCurrent = eye.transform.position;*/

        if (scrollableOffset)
        {

            if (Input.GetAxis(scrollableOffsetAxis) > 0)
                offsetScroll -= scrollableIncrement;
            else if (Input.GetAxis(scrollableOffsetAxis) < 0)
                offsetScroll += scrollableIncrement;

            if (currentScroll < -10)
                offsetScroll = 10 + (posOffset.z+offsetScroll);
            else if (currentScroll > minimumScroll)
                ToggleOffset(false);

            //Debug.Log("CURRENT SCROLL VALUE IS " + currentScroll.ToString() + " AND MINIMUM SCROLL VALUE IS " + minimumScroll.ToString());
        }

        if (!offsetAttacks)
        {
            if (HasSwungWeapon && GameManager.Instance.WeaponManager.ScreenWeapon.GetCurrentFrame() == GameManager.Instance.WeaponManager.ScreenWeapon.GetHitFrame())
            {
                bool hitEnemy = false;
                MeleeDamage(GameManager.Instance.WeaponManager.ScreenWeapon, out hitEnemy);
                HasSwungWeapon = false;
            }

            if (HasFiredMissile)
            {
                if (missileTimer > 3)
                {
                    //Debug.Log("MISSILE - DURATION EXCEEDED - RESETTING");
                    HasFiredMissile = false;
                    missileTimer = 0;
                }
                else
                {
                    Vector3 origin = body.TransformPoint(posLocalDefault);
                    //Debug.Log("MISSILE - SEARCHING FOR A MISSILE");
                    DaggerfallMissile myMissile = null;
                    DaggerfallMissile[] missiles = FindObjectsOfType<DaggerfallMissile>();
                    float closestDistance = 5;
                    foreach (DaggerfallMissile missile in missiles)
                    {
                        if (missile.Caster == GameManager.Instance.PlayerEntityBehaviour)
                        {
                            float distance = (origin - missile.transform.position).sqrMagnitude;
                            if (distance < closestDistance)
                            {
                                myMissile = missile;
                                closestDistance = distance;
                            }
                        }
                    }

                    if (myMissile != null)
                    {
                        //Debug.Log("MISSILE - MISSILE FOUND! RELOCATING!");
                        HasFiredMissile = false;
                        myMissile.CustomAimPosition = origin;
                        myMissile.transform.position = origin;
                        missileTimer = 0;
                    }
                    else
                    {
                        //Debug.Log("MISSILE - NO MISSILE YET!");
                        missileTimer += Time.deltaTime;
                    }
                }
            }
            else
                missileTimer = 0;
        }

        //if offsetX is not zero, allow mirroring
        if (posOffset.x != 0)
        {
            if (InputManager.Instance.GetKeyUp(mirrorKeyCode))
            {
                mirror = !mirror;
                mirrorOriginal = mirror;
            }

            if (mirror != mirrorOriginal && mirrorTime > 0)
            {
                if (mirrorTimer > mirrorTime)
                {
                    Vector3 posLocal = body.InverseTransformPoint(posTarget);
                    Vector3 originX = posLocal;
                    originX.x = posLocalDefault.x;
                    originX = body.TransformPoint(originX);

                    Vector3 dir = -body.transform.right;
                    if (posOffset.x < 0)
                        dir = body.transform.right;

                    Ray ray = new Ray(originX, dir);
                    RaycastHit hit = new RaycastHit();
                    if (Physics.Raycast(ray, out hit, Mathf.Abs(posOffset.x * 2f) + eyeRadius, layerMask, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.distance < (Mathf.Abs(posOffset.x) / 2) + eyeRadius)
                            mirrorTimer = 0;
                        else
                            mirror = mirrorOriginal;
                    }
                    else
                        mirror = mirrorOriginal;
                }
                else
                {
                    mirrorTimer += Time.deltaTime;
                }
            }
        }

        CheckBounds();

        if (isSailing && overrideBoat)
        {
            if (boatTarget == 1)
            {
                posTarget = boatFlagObject.transform.position + eye.transform.TransformVector(SetVectorBounds(posOffset) * boatMeshObjectExtent);
            }
            else
            {
                posTarget = boatMeshObject.transform.position + boatMeshObjectCenter + eye.transform.TransformVector(SetVectorBounds(posOffset) * boatMeshObjectExtent);
            }
        }
        else
            posTarget = body.transform.position + body.TransformVector(posLocalDefault) + eye.transform.TransformVector(SetVectorBounds(posOffset));

        /*float pitch = GameManager.Instance.PlayerMouseLook.Pitch / 90;
        Vector3 posOffsetPitched = posOffset;
        posOffsetPitched.y = posOffset.y - (posOffset.z * pitch);
        posOffsetPitched.z = posOffset.z - (posOffset.z * pitch);
        posTarget = body.transform.position + body.transform.TransformVector(posLocalDefault + posOffsetPitched);*/

        float speedFinal = speed;
        if (dampen != 0)
            speedFinal = speed * (Vector3.Distance(posCurrent, posTarget) / dampen);

        posCurrent = Vector3.MoveTowards(posCurrent, posTarget, Time.deltaTime * speedFinal);

        //clamp z offset but respect terrain collision
        if (offsetMinZ != 0)
        {
            float distance = posOffset.z * offsetMinZ;
            if (boundsZ > Mathf.Abs(distance))
            {
                Vector3 posCurrentLocal = body.transform.InverseTransformPoint(posCurrent);
                if (posCurrentLocal.z > distance)
                    posCurrentLocal.z = distance;
                posCurrent = body.transform.TransformPoint(posCurrentLocal);
            }
        }

    }

    private void LateUpdate()
    {
        //if (autoPOV)
        //{

        bool forcePOV = false;
        if (InputManager.Instance.GetKeyUp(autoPOVSwitchKeyCode))
        {
            autoPOVSwitch = !autoPOVSwitch;

            if (autoPOVSwitch)
            {
                forcePOV = true;
                if (debugMessages)
                    DaggerfallUI.Instance.PopupMessage("Auto-toggle POV enabled!");
            }
            else
            {
                if (debugMessages)
                    DaggerfallUI.Instance.PopupMessage("Auto-toggle POV disabled!");
            }
        }

        if (autoPOVSwitch)
        {
            bool isTransformedCurrent = GameManager.Instance.PlayerEffectManager.IsTransformedLycanthrope();
            bool isRidingCurrent = GameManager.Instance.PlayerMotor.IsRiding;
            bool isSpellcastingCurrent = GameManager.Instance.PlayerEffectManager.HasReadySpell;
            bool isSheathedCurrent = GameManager.Instance.WeaponManager.Sheathed;
            bool isRangedCurrent = GameManager.Instance.WeaponManager.ScreenWeapon.WeaponType == WeaponTypes.Bow ? true : false;
            if (isTransformedCurrent)
            {
                //transformation started
                if (isTransformedPrevious != isTransformedCurrent)
                {
                    if (autoPOVOnLycan == 2 && !offset)
                        ToggleOffset(true);
                    else if (autoPOVOnLycan == 1 && offset)
                        ToggleOffset(false);
                }
            }
            else
            {
                //transformation ended
                if (isTransformedPrevious != isTransformedCurrent)
                    forcePOV = true;
            }

            if (isRidingCurrent)
            {
                //riding started
                if (isRidingPrevious != isRidingCurrent)
                {
                    if (autoPOVOnHorse == 2 && !offset)
                        ToggleOffset(true);
                    else if (autoPOVOnHorse == 1 && offset)
                        ToggleOffset(false);
                }
            }
            else
            {
                //riding ended
                if (isRidingPrevious != isRidingCurrent)
                    forcePOV = true;
            }

            if (isSpellcastingCurrent)
            {
                //spellcasting started
                if (isSpellcastingPrevious != isSpellcastingCurrent)
                {
                    if (autoPOVOnFootSpell == 2 && !offset)
                        ToggleOffset(true);
                    else if (autoPOVOnFootSpell == 1 && offset)
                        ToggleOffset(false);
                }
            }
            else
            {
                //spellcasting ended
                if (isSpellcastingPrevious != isSpellcastingCurrent)
                    forcePOV = true;
            }

            //a state ended, force POV to check
            if (forcePOV)
            {
                //weapon is unsheathed
                if (!isSheathedCurrent)
                {
                    //player is riding horse or cart
                    if (isRidingCurrent)
                    {
                        if (autoPOVOnHorseReady == 2 && !offset)
                            ToggleOffset(true);
                        else if (autoPOVOnHorseReady == 1 && offset)
                            ToggleOffset(false);
                    }
                    else
                    {
                        //weapon is ranged
                        if (GameManager.Instance.WeaponManager.ScreenWeapon.WeaponType == WeaponTypes.Bow)
                        {
                            if (autoPOVOnFootRanged == 2 && !offset)
                                ToggleOffset(true);
                            else if (autoPOVOnFootRanged == 1 && offset)
                                ToggleOffset(false);
                        }
                        else
                        {
                            if (autoPOVOnFootMelee == 2 && !offset)
                                ToggleOffset(true);
                            else if (autoPOVOnFootMelee == 1 && offset)
                                ToggleOffset(false);
                        }
                    }
                }
                else
                {
                    if (isRidingCurrent)
                    {
                        if (autoPOVOnHorse == 2 && !offset)
                            ToggleOffset(true);
                        else if (autoPOVOnHorse == 1 && offset)
                            ToggleOffset(false);
                    }
                    else
                    {
                        if (autoPOVOnFoot == 2 && !offset)
                            ToggleOffset(true);
                        else if (autoPOVOnFoot == 1 && offset)
                            ToggleOffset(false);
                    }
                }
            }
            else
            {
                if (!isSheathedCurrent)
                {
                    //has unsheathed or changed weapon tyoe
                    if (isSheathedPrevious != isSheathedCurrent || isRangedPrevious != isRangedCurrent)
                    {
                        if (isRidingCurrent)
                        {
                            if (autoPOVOnHorseReady == 2 && !offset)
                                ToggleOffset(true);
                            else if (autoPOVOnHorseReady == 1 && offset)
                                ToggleOffset(false);
                        }
                        else
                        {
                            if (isRangedCurrent)
                            {
                                if (autoPOVOnFootRanged == 2 && !offset)
                                    ToggleOffset(true);
                                else if (autoPOVOnFootRanged == 1 && offset)
                                    ToggleOffset(false);
                            }
                            else
                            {
                                if (autoPOVOnFootMelee == 2 && !offset)
                                    ToggleOffset(true);
                                else if (autoPOVOnFootMelee == 1 && offset)
                                    ToggleOffset(false);
                            }
                        }
                    }
                }
                else
                {
                    //has sheathed weapon
                    if (isSheathedPrevious != isSheathedCurrent)
                    {
                        if (isRidingCurrent)
                        {
                            if (autoPOVOnHorse == 2 && !offset)
                                ToggleOffset(true);
                            else if (autoPOVOnHorse == 1 && offset)
                                ToggleOffset(false);
                        }
                        else
                        {
                            if (autoPOVOnFoot == 2 && !offset)
                                ToggleOffset(true);
                            else if (autoPOVOnFoot == 1 && offset)
                                ToggleOffset(false);
                        }
                    }
                }
            }
            isTransformedPrevious = isTransformedCurrent;
            isRidingPrevious = isRidingCurrent;
            isSpellcastingPrevious = isSpellcastingCurrent;
            isSheathedPrevious = isSheathedCurrent;
            isRangedPrevious = isRangedCurrent;
        }

        UpdateWagon();

        if (!offset)
            return;

        eye.transform.position = posCurrent;

        if (!hideWeapon)
            GameManager.Instance.WeaponManager.ScreenWeapon.ShowWeapon = false;
    }

    void OnPositionUpdate(Vector3 offset)
    {
        posCurrent = eye.transform.position;
    }

    public static void OnTransitionInterior(PlayerEnterExit.TransitionEventArgs args)
    {
        if (Instance.autoPOVSwitch)
        {
            if (Instance.autoPOVOnTransitionInterior == 2 && !Instance.offset)
                Instance.ToggleOffset(true);
            else if (Instance.autoPOVOnTransitionInterior == 1 && Instance.offset)
                Instance.ToggleOffset(false);
        }
    }

    public static void OnTransitionExterior(PlayerEnterExit.TransitionEventArgs args)
    {
        if (Instance.autoPOVSwitch)
        {
            if (Instance.autoPOVOnTransitionExterior == 2 && !Instance.offset)
                Instance.ToggleOffset(true);
            else if (Instance.autoPOVOnTransitionExterior == 1 && Instance.offset)
                Instance.ToggleOffset(false);
        }
    }

    void SpawnBillboard()
    {
        billboardObject = Instantiate(new GameObject("PlayerBillboard"));
        billboardObject.SetActive(false);
        billboardObject.AddComponent<MeshRenderer>();
        billboardObject.AddComponent<MeshFilter>();
        billboardObject.transform.SetParent(GameManager.Instance.PlayerObject.transform.GetChild(0));
        billboardObject.transform.localPosition = Vector3.zero;
        billboardObject.transform.localRotation = Quaternion.identity;

        billboardPlayer = billboardObject.AddComponent<PlayerBillboard>();
    }

    void SpawnWagon()
    {
        wagonObject = GameObjectHelper.CreateDaggerfallMeshGameObject(41239, null);
        wagonOffsetLast = Vector3.forward * -2.5f;
        wagonObject.GetComponent<MeshCollider>().enabled = false;

        MeshRenderer mesh = wagonObject.GetComponent<MeshRenderer>();
        BoxCollider box = wagonObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.bounds.Encapsulate(mesh.bounds);

        /*Material[] materials = mesh.materials;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i].name == "TEXTURE.088 [Index=2] (Instance)")
            {
                Material newMaterial = DaggerfallUnity.Instance.MaterialReader.GetMaterial(090, 5);
                materials[i] = newMaterial;
                mesh.materials = materials;
            }
            if (materials[i].name == "TEXTURE.088 [Index=3] (Instance)")
            {
                Material newMaterial = DaggerfallUnity.Instance.MaterialReader.GetMaterial(090, 7);
                materials[i] = newMaterial;
                mesh.materials = materials;
            }
        }*/

        wagonObject.SetActive(false);

        wagonLayerMask |= (1 << LayerMask.NameToLayer("Default"));

        PlayerActivate.RegisterCustomActivation(mod, 41239, CheckWagon);
    }

    void UpdateWagon()
    {
        if (!wagon)
        {
            if (wagonObject.activeSelf)
                wagonObject.SetActive(false);

            return;
        }

        if (wagonObject == null)
            return;

        if (GameManager.Instance.TransportManager.TransportMode == TransportModes.Cart)
        {
            if (!wagonObject.activeSelf)
            {
                wagonObject.SetActive(true);
                wagonObject.transform.position = body.transform.position - (body.transform.forward * 10);
            }

            bool updatePos = false;
            bool originShifted = false;

            Vector3 wagonOffsetCurrent = body.transform.position - wagonObject.transform.position;

            float distance = wagonOffsetCurrent.magnitude;
            if (distance > 2.5f)
                updatePos = true;
            if (distance > 10f) //floating point origin shifted
                originShifted = true;

            if (updatePos)
            {
                Vector3 offset = wagonOffsetCurrent;
                if (originShifted)
                    offset = wagonOffsetLast;

                //bring wagon closer to player body
                Vector3 newPos = body.transform.position - (offset.normalized * 2.49f);

                //set wagon on ground
                Ray ray = new Ray(wagonObject.transform.position - wagonObject.transform.TransformPoint(Vector3.forward * 0.6f) + newPos + (Vector3.up*2f), Vector3.down);
                RaycastHit hit = new RaycastHit();
                if (Physics.Raycast(ray, out hit, 10f, wagonLayerMask, QueryTriggerInteraction.Ignore))
                {
                    Vector3 groundedPos = new Vector3(newPos.x, hit.point.y+1f, newPos.z);
                    newPos = body.transform.position - ((body.transform.position - groundedPos).normalized * 2.49f);
                }
                //Debug.DrawRay(ray.origin, ray.direction * 10f, Color.red, 1, false);

                wagonObject.transform.position = newPos;
            }

            wagonObject.transform.LookAt(body.transform);

            if (updatePos)
            {
                //tilt the wagon
                float frequency = 10;   //scale with speed
                float amplitude = 2 + ((Mathf.Sin(Time.time) + 1 ));    //scale with surface

                if (GameManager.Instance.PlayerMotor.OnExteriorPath)
                    amplitude = 1 + ((Mathf.Sin(Time.time) + 1) * 0.5f);

                float tiltSmooth = Mathf.Sin(Time.time * frequency) * amplitude;
                //wagonObject.transform.Rotate(Vector3.forward * tiltSmooth, Space.Self);
                Vector3 localEulerAngles = new Vector3(wagonObject.transform.localEulerAngles.x, wagonObject.transform.localEulerAngles.y, tiltSmooth);
                wagonObject.transform.localEulerAngles = localEulerAngles;
            }

            wagonOffsetLast = wagonOffsetCurrent;
            if (originShifted)
                wagonOffsetLast = body.transform.position - wagonObject.transform.position;
        }
        else
        {
            if (wagonObject.activeSelf)
                wagonObject.SetActive(false);
        }
    }

    public static void CheckWagon(RaycastHit hit)
    {
        int hitID = hit.transform.gameObject.GetInstanceID();

        if (hitID == Instance.wagonObject.GetInstanceID())
        {
            //hit the EOTB wagon gameobject while riding
            if (GameManager.Instance.PlayerActivate.CurrentMode == PlayerActivateModes.Info)
            {
                DaggerfallUI.AddHUDText("You see your wagon");
            }
            else
            {
                DaggerfallUI.Instance.InventoryWindow.AllowDungeonWagonAccess();
                DaggerfallUI.PostMessage(DaggerfallUIMessages.dfuiOpenInventoryWindow);
            }
        }
    }

    public void ToggleOffset(bool state)
    {
        if (state)
        {
            offsetScroll = 0;
            posCurrent = eye.transform.position;
            ToggleBillboard(billboard);
            if (firstpersonBillboard > 0)
                billboardPlayer.FP = false;
            spellCasting.enabled = false;
        }
        else
        {
            torch.localPosition = torchPosLocalDefault;
            eye.transform.localPosition = posLocalDefault;
            if (firstpersonBillboard > 0)
            {
                billboardPlayer.FP = true;
                ToggleBillboard(billboard);
            }
            else
                ToggleBillboard(false);
            spellCasting.enabled = true;
        }

        if (!hideHorse)
            GameManager.Instance.TransportManager.DrawHorse = !state;

        offset = state;

        if (OnToggleOffset != null)
            OnToggleOffset(offset);
    }

    void ToggleBillboard(bool state)
    {
        Debug.Log("Toggling billboard to " + state.ToString());

        billboardObject.SetActive(state);

        if (state)
            billboardPlayer.Initialize(indexFoot, indexHorse, size, sizeOffset, readyStance, turnToView, combo, comboTime, comboOffset, firstpersonBillboard, torchFollow);
    }

    void CheckBounds()
    {
        Vector3 posLocal = body.TransformPoint(posLocalDefault);

        if (posOffset.x != 0)    //raycast sideways
        {
            Vector3 dir = eye.transform.right;

            if (posOffset.x < 0)
                dir = -eye.transform.right;

            Ray ray = new Ray(posLocal, dir);
            RaycastHit hit = new RaycastHit();
            float maxDistance = Mathf.Abs(posOffset.x * 2f) + eyeRadius;
            //Debug.DrawRay(ray.origin, ray.direction * maxDistance, Color.red, 1f, false);
            if (Physics.Raycast(ray, out hit, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
                boundsX = hit.distance - (eyeRadius*2);
            else
                boundsX = maxDistance;

            if (mirrorAuto)
            {
                if (Mathf.Abs(boundsX) < (Mathf.Abs(posOffset.x) / 2) + eyeRadius)
                {
                    mirror = !mirror;
                    if (mirrorTime > 0)
                        mirrorTimer = 0;
                }
            }
        }

        if (posOffset.y != 0)    //raycast vertically
        {
            Vector3 dir = eye.transform.up;

            if (posOffset.y < 0)
                dir = -eye.transform.up;

            Ray ray = new Ray(posLocal, dir);
            RaycastHit hit = new RaycastHit();
            float maxDistance = Mathf.Abs(posOffset.y * 2f) + eyeRadius;
            //Debug.DrawRay(ray.origin, ray.direction * maxDistance, Color.green, 1f, false);
            if (Physics.Raycast(ray, out hit, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
                boundsY = hit.distance - (eyeRadius * 2);
            else
                boundsY = maxDistance;
        }

        if (posOffset.z != 0)    //raycast forward/backward
        {
            Vector3 dir = eye.transform.forward;

            if (posOffset.z < 0)
                dir = -eye.transform.forward;

            Ray ray = new Ray(posLocal, dir);
            RaycastHit hit = new RaycastHit();
            float maxDistance = Mathf.Abs(posOffset.z * 2f) + eyeRadius;
            //Debug.DrawRay(ray.origin, ray.direction * maxDistance, Color.blue, 1f, false);
            if (Physics.Raycast(ray, out hit, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
                boundsZ = hit.distance - (eyeRadius * 2);
            else
                boundsZ = maxDistance;
        }
    }

    Vector3 SetVectorBounds(Vector3 vector)
    {
        Vector3 posLocal = vector;
        //Vector3 posLocal = eye.transform.InverseTransformPoint(vector);

        if (posOffset.x != 0)    //constrain sideways
        {
            if (posOffset.x < 0 && posLocal.x < -boundsX)
                posLocal.x = -boundsX;
            else if (posLocal.x > boundsX)
                posLocal.x = boundsX;
        }

        if (posOffset.y != 0)    //constrain vertically
        {
            if (posOffset.y < 0 && posLocal.y < -boundsY)
                posLocal.y = -boundsY;
            else if (posLocal.y > boundsY)
                posLocal.y = boundsY;
        }

        if (posOffset.z != 0)    //constrain sagitally
        {
            if (posOffset.z < 0 && posLocal.z < -boundsZ)
                posLocal.z = -boundsZ;
            else if (posLocal.z > boundsZ)
                posLocal.z = boundsZ;
        }

        //Vector3 newVector = eye.transform.TransformPoint(posLocal);

        return posLocal;
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
            return KeyCode.Tab;
        }
    }

    private void MeleeDamage(FPSWeapon weapon, out bool hitEnemy)
    {
        hitEnemy = false;

        if (!weapon)
            return;

        // Fire ray along player facing using weapon range
        RaycastHit hit;
        Ray ray = new Ray(body.TransformPoint(posLocalDefault), eye.transform.forward);
        //Debug.DrawLine(ray.origin, ray.origin + (ray.direction * weapon.Reach), Color.green, 1f, false);
        if (Physics.SphereCast(ray, 0.25f, out hit, weapon.Reach, playerLayerMask))
        {
            DaggerfallUnityItem strikingWeapon = GameManager.Instance.WeaponManager.ScreenWeapon.SpecificWeapon;
            if (!GameManager.Instance.WeaponManager.WeaponEnvDamage(strikingWeapon, hit)
               // Fall back to simple ray for narrow cages https://forums.dfworkshop.net/viewtopic.php?f=5&t=2195#p39524
               || Physics.Raycast(ray, out hit, weapon.Reach, playerLayerMask))
            {
                hitEnemy = GameManager.Instance.WeaponManager.WeaponDamage(strikingWeapon, false, false, hit.transform, hit.point, ray.direction);
            }
        }
    }

    public Vector3 FreeRein_GetMoveVector()
    {
        if (FreeRein == null)
            FreeRein = ModManager.Instance.GetModFromGUID("c01eeb80-b41f-477b-a5ba-e0bfbc68637f");
        Vector3 moveVector = Vector3.zero;


        if (FreeRein != null)
        {
            ModManager.Instance.SendModMessage(FreeRein.Title, "getMoveVector", null, (string message, object data) =>
            {
                moveVector = (Vector3)data;
            });
        }

        return moveVector;
    }
}
