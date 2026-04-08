using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

public class HandheldTorches : MonoBehaviour
{
    public static HandheldTorches Instance;

    public Light playerLightSource;
    DaggerfallUnityItem lastLightSource;

    public DaggerfallAudioSource audioSourceOneShot;
    DaggerfallAudioSource audioSourceLoop;

    LayerMask layerMaskPlayer;

    public List<GameObject> billboardsObject = new List<GameObject>();
    public List<float> billboardsTime = new List<float>();
    public List<int> billboardsItemTemplateIndex = new List<int>();

    bool showSprite = true;
    bool mirrorSprite = false;
    bool tintSprite = true;
    bool playAudio = true;
    float sfxVolume = 0.5f;
    KeyCode toggleLightKeyCode = KeyCode.F;
    bool lastLight;
    int lastLightTemplateIndex = 0;
    KeyCode dropKeyCode = KeyCode.Tab;
    KeyCode throwKeyCode = KeyCode.X;
    int onStow = 0; //0 = unequip, 1 = drop
    int onPick = 0; //0 = inventory, 1 = equip, 2 = force equip
    bool stowOnSpellcasting;
    bool stowOnClimbing;
    bool stowOnSwimming;
    bool twoHandedRelaxed;
    bool lanternRelaxed;

    public float throwStrength = 1f;
    public float throwAngle = 15f;
    public float throwSpread = 2;
    public float throwGravity = 1f;
    public float throwBounce = 0.5f;
    public float throwTime = 1;
    float throwScale = 1;
    float throwTimer = 0;
    bool throwDrawTrajectory;
    LineRenderer lineRenderer;
    List<Vector3> positions;

    public bool fire;
    public int fireAccuracy;
    public int fireDuration;
    public int fireChance;
    public Vector2Int fireDamageRange;

    public bool fireLight;
    public bool fireLightShadows;

    string messageDrop = "You drop the ";
    string messageDropTorchless = "You don't have a light source to drop";
    string messagePickupEquip = "You pick up the ";
    string messagePickupStore = "You stow the ";
    string messageNoFreeHand = "You can't hold a light source right now";
    string messageExamine = "You see a ";
    string messageThrow = "You throw the ";
    string messageThrowTorchless = "You don't have a torch to throw";
    string messageIgnite = "You ignite the ";
    string messageIgniteTorchless = "You don't have a light source";
    string messageDouse = "You douse the ";

    bool usingRightHand;
    bool sheathed;
    bool attacked;

    bool attacking;
    bool spellcasting;
    bool climbing;
    bool swimming;

    public bool handLeft;
    public bool handRight;

    public bool HasFreeHand
    {
        get
        {
            if (handLeft || handRight)
                return true;
            else
                return false;
        }
    }

    public int GetFreeHand
    {
        get
        {
            if (HasFreeHand)
            {
                if (handLeft)
                    return 1;
                else
                    return 2;
            }
            else
                return 0;
        }
    }

    bool hasLightSource;
    bool hasDroppedOrThrownLight;
    int lightSourceTemplateIndexLast;

    const int nativeScreenWidth = 320;
    const int nativeScreenHeight = 200;

    bool flipped;
    bool flippedCurrent;
    bool flippedLast;

    public Texture2D currentTexture;
    public List<Texture2D> textures;

    int offsetFrame = 0;
    int currentFrame;
    float animationTime = 0.0625f;
    float animationTimer;

    int animTorchLength;
    int animLanternLength;

    Rect positionCurrent;
    Rect positionTarget;
    Rect curAnimRect;

    Rect screenRect;
    Rect screenRectLast;

    float weaponScaleX;
    float weaponScaleY;
    float weaponOffsetHeight;
    float weaponOffsetHeightLast;

    bool lockAspectRatio;

    bool stepTransforms;
    int stepLength = 16;
    int stepCondition = 1;

    float offsetX = 2.0f;           //distance from the texture's left edge the center of the screen
    float offsetY = 0.75f;           //distance from the texture's bottom edge to the bottom of the screen
    float scale = 1f;
    public float scaleTextureFactor = 1f;
    float offsetSpeed = 1000f;
    float offsetSpeedLive
    {
        get
        {
            return ((float)GameManager.Instance.PlayerEntity.Stats.LiveSpeed / 100) * offsetSpeed;
        }
    }
    int whenSheathed;
    int whenCasting;

    bool bob;
    float bobLength = 1.0f;
    float bobOffset = 0.5f;
    float bobSizeXMod = 1f;      //RECOMMEND RANGE OF 0.5-1.5
    float bobSizeYMod = 4f;      //RECOMMEND RANGE OF 0.5-1.5
    float moveSmoothSpeed = 4;        //RECOMMEND RANGE OF 1-3
    float bobSmoothSpeed = 500;         //RECOMMEND RANGE OF UP TO 1-3
    float bobShape = 1;
    bool bobWhileIdle;

    bool inertia;
    float inertiaScale = 500;
    float inertiaSpeed = 500;
    float inertiaSpeedMod = 1;
    float inertiaForwardSpeed = 0.2f;
    float inertiaForwardScale = 0.2f;

    //PERSISTENT VARIABLES FOR THE SMOOTHING
    float moveSmooth = 0;
    Vector2 bobSmooth = Vector2.zero;

    Vector2 inertiaCurrent = Vector2.zero;
    Vector2 inertiaTarget;

    Vector2 inertiaForwardCurrent = Vector2.zero;
    Vector2 inertiaForwardTarget;

    public Vector2 Position { get; set; } = Vector2.zero;
    public Vector2 Offset { get; set; } = Vector2.zero;
    public Vector2 Scale { get; set; } = Vector2.one;

    public ulong skipTimeStart;
    IUserInterfaceWindow LastUIWindow;

    //Mod compatibility
    public bool isInThirdPerson;
    public bool hasBloodfall;

    static Mod mod;
    [Invoke(StateManager.StateTypes.Start, 0)]

    public static void Init(InitParams initParams)
    {
        mod = initParams.Mod;
        mod.SaveDataInterface = new HandheldTorchesSaveData();
        var go = new GameObject(mod.Title);
        go.AddComponent<HandheldTorches>();
    }

    public static void DestroyLightSources_OnStartLoad(SaveData_v1 saveData)
    {
        DestroyLightSources();
    }

    public static void OnRestWindowOpen(object sender, EventArgs e)
    {
        if (!GameManager.Instance.StateManager.GameInProgress)
            return;

        if (GameManager.Instance.StateManager.CurrentState == StateManager.StateTypes.Game)
        {
            if (DaggerfallUI.Instance.UserInterfaceManager.TopWindow is DaggerfallRestWindow)
            {
                Debug.Log("ON REST WINDOW OPENED! RECORDING START TIME!");
                Instance.skipTimeStart = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToSeconds();
            }
        }
    }
    public static void OnRestWindowClose(object sender, EventArgs e)
    {
        if (!GameManager.Instance.StateManager.GameInProgress)
            return;

        if (GameManager.Instance.StateManager.LastState == StateManager.StateTypes.Game || GameManager.Instance.StateManager.LastState == StateManager.StateTypes.UI)
        {
            if (DaggerfallUI.UIManager.WindowCount > 0)
                Instance.LastUIWindow = DaggerfallUI.Instance.UserInterfaceManager.TopWindow;

            if (DaggerfallUI.UIManager.WindowCount == 0 && Instance.LastUIWindow is DaggerfallRestWindow)
            {
                Debug.Log("ON REST WINDOW CLOSED! CALCULATING DURATION!");
                ulong skipGameTimeDuration = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToSeconds()-Instance.skipTimeStart;

                if (skipGameTimeDuration > 0 && Instance.billboardsTime.Count > 0)
                {
                    Debug.Log("TIME SKIP DETECTED, UPDATING DROPPED TORCHES");
                    float skipRealTimeDuration = skipGameTimeDuration / 12;
                    for (int i  = 0; i < Instance.billboardsTime.Count; i++)
                    {
                        Instance.billboardsTime[i] -= skipRealTimeDuration;

                        if (Instance.billboardsTime[i] < 0)
                        {
                            Instance.billboardsItemTemplateIndex[i] = 0;
                            Destroy(Instance.billboardsObject[i]);
                        }
                    }
                }
                Instance.LastUIWindow = null;
            }
        }
    }

    void Start()
    {
        mod.LoadSettingsCallback = LoadSettings;
        mod.LoadSettings();

        ModCompatibilityChecking();

        Instance = this;

        playerLightSource = GameManager.Instance.PlayerObject.GetComponent<EnablePlayerTorch>().PlayerTorch.GetComponent<Light>();

        audioSourceOneShot = gameObject.AddComponent<DaggerfallAudioSource>();

        GameObject audioSourcePlayerObject = new GameObject(mod.Title + " Player Audio");
        audioSourcePlayerObject.transform.SetParent(GameManager.Instance.PlayerObject.transform,false);
        audioSourcePlayerObject.transform.localPosition = Vector3.zero;
        audioSourcePlayerObject.transform.localRotation = Quaternion.identity;
        audioSourceLoop = audioSourcePlayerObject.AddComponent<DaggerfallAudioSource>();
        audioSourceLoop.AudioSource.dopplerLevel = 0;
        audioSourceLoop.AudioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSourceLoop.AudioSource.maxDistance = 5f;
        audioSourceLoop.AudioSource.volume = sfxVolume;
        audioSourceLoop.SetSound(SoundClips.Burning, AudioPresets.LoopOnDemand);

        layerMaskPlayer = ~(1 << LayerMask.NameToLayer("Player"));

        //setup trajectory line renderer
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.widthMultiplier = 0.1f;
        lineRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
        Color startColor = new Color(1, 1, 1, 0);
        Color endColor = new Color(1, 1, 1, 1);
        lineRenderer.startColor = startColor;
        lineRenderer.endColor = endColor;
        lineRenderer.enabled = false;

        if (DaggerfallUI.Instance.CustomScreenRect != null)
            screenRect = DaggerfallUI.Instance.CustomScreenRect.Value;
        else
            screenRect = new Rect(0, 0, Screen.width, Screen.height);

        screenRectLast = screenRect;

        if (DaggerfallUnity.Settings.Handedness == 1)
            flipped = true;

        if (flipped)
            curAnimRect = new Rect(1, 0, -1, 1);
        else
            curAnimRect = new Rect(0, 0, 1, 1);

        InitializeTextures();

        RefreshSprite();

        PlayerEnterExit.OnTransitionInterior += DestroyLightSources_OnTransition;
        PlayerEnterExit.OnTransitionExterior += DestroyLightSources_OnTransition;
        PlayerEnterExit.OnTransitionDungeonInterior += DestroyLightSources_OnTransition;
        PlayerEnterExit.OnTransitionDungeonExterior += DestroyLightSources_OnTransition;
        PlayerActivate.RegisterCustomActivation(mod, 112358, 0, PickUpLightSource, 3.2f);
        PlayerActivate.RegisterCustomActivation(mod, 112358, 10, PickUpLightSource, 3.2f);
        PlayerActivate.RegisterCustomActivation(mod, 112358, 1, PickUpLightSource, 3.2f);
        PlayerActivate.RegisterCustomActivation(mod, 112358, 11, PickUpLightSource, 3.2f);
        PlayerActivate.RegisterCustomActivation(mod, 112358, 2, PickUpLightSource, 3.2f);
        PlayerActivate.RegisterCustomActivation(mod, 112358, 12, PickUpLightSource, 3.2f);

        DaggerfallUI.UIManager.OnWindowChange += OnRestWindowOpen;
        DaggerfallUI.UIManager.OnWindowChange += OnRestWindowClose;

        SaveLoadManager.OnStartLoad += DestroyLightSources_OnStartLoad;

        //register custom spell effect
        HandheldTorchesEnemyLight enemyLightTemplateEffect = new HandheldTorchesEnemyLight();
        GameManager.Instance.EntityEffectBroker.RegisterEffectTemplate(enemyLightTemplateEffect);

        mod.MessageReceiver = MessageReceiver;
        mod.IsReady = true;
    }

    void MessageReceiver(string message, object data, DFModMessageCallback callBack)
    {
        switch (message)
        {
            case "hasFreeHand":
                callBack?.Invoke("hasFreeHand", HasFreeHand);
                break;

            case "getFreeHand":
                callBack?.Invoke("getFreeHand", GetFreeHand);
                break;

            default:
                Debug.LogErrorFormat("{0}: unknown message received ({1}).", this, message);
                break;
        }
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

        //Check if Bloodfall is installed
        Mod bf = ModManager.Instance.GetModFromGUID("a2b6da68-7b51-4197-b5f1-8aac27194132");
        if (bf != null)
        {
            hasBloodfall = true;
        }

        Mod wt = ModManager.Instance.GetModFromGUID("88e77a95-fca0-4c13-a3b9-55ddf40ee01e");
        if (wt != null)
        {
            ModManager.Instance.SendModMessage(wt.Title, "RegisterCustomTooltip", new System.Tuple<float, Func<RaycastHit, string>>(
                PlayerActivate.DefaultActivationDistance,
                (hit) =>
                {
                    if (hit.transform.name.Length > 16 && hit.transform.name.Contains("TEXTURE.112358"))
                    {
                        if (hit.transform.name.Contains("Index=2"))
                            return "Holy Candle";
                        else if (hit.transform.name.Contains("Index=1"))
                            return "Candle";
                        else
                            return "Torch";
                    }
                    return null;
                }
            ));
        }
    }

    private void LoadSettings(ModSettings settings, ModSettingsChange change)
    {
        bool showSpriteOld = showSprite;

        if (change.HasChanged("Handling"))
        {
            toggleLightKeyCode = SetKeyFromText(settings.GetString("Handling", "ToggleLightInput"));
            lastLight = settings.GetValue<bool>("Handling", "RememberLastLightSource");
            dropKeyCode = SetKeyFromText(settings.GetString("Handling", "ManualDropInput"));
            onStow = settings.GetValue<int>("Handling", "OnStow");
            onPick = settings.GetValue<int>("Handling", "OnPick");
            stowOnSpellcasting = settings.GetValue<bool>("Handling", "StowWhenSpellcasting");
            stowOnClimbing = settings.GetValue<bool>("Handling", "StowWhenClimbing");
            stowOnSwimming = settings.GetValue<bool>("Handling", "StowWhenSwimming");
            twoHandedRelaxed = settings.GetValue<bool>("Handling", "RelaxedTwoHandedWeapons");
            lanternRelaxed = settings.GetValue<bool>("Handling", "RelaxedLanterns");
        }
        if (change.HasChanged("Throwing"))
        {
            throwKeyCode = SetKeyFromText(settings.GetString("Throwing", "ThrowTorchInput"));
            throwStrength = settings.GetValue<float>("Throwing", "ThrowStrength");
            throwAngle = settings.GetValue<float>("Throwing", "ThrowAngleOffset");
            throwSpread = settings.GetValue<float>("Throwing", "ThrowDispersion");
            throwGravity = settings.GetValue<float>("Throwing", "GravityStrength");
            throwBounce = settings.GetValue<float>("Throwing", "Bounciness");
            throwScale = settings.GetValue<float>("Throwing", "ThrowScaleSpeed");
            throwDrawTrajectory = settings.GetValue<bool>("Throwing", "ShowTrajectory");
            fire = settings.GetValue<bool>("Throwing", "Combustion");
            fireAccuracy = settings.GetValue<int>("Throwing", "Accuracy");
            fireDuration = settings.GetValue<int>("Throwing", "Duration");
            fireChance = settings.GetValue<int>("Throwing", "Chance");
            fireDamageRange = new Vector2Int(settings.GetTupleInt("Throwing", "Magnitude").First, settings.GetTupleInt("Throwing", "Magnitude").Second);
            fireLight = settings.GetValue<bool>("Throwing", "Emission");
            fireLightShadows = settings.GetValue<bool>("Throwing", "EmissionShadows");
        }
        if (change.HasChanged("Modules"))
        {
            showSprite = settings.GetValue<bool>("Modules", "Sprite");
            bob = settings.GetValue<bool>("Modules", "Bob");
            inertia = settings.GetValue<bool>("Modules", "Inertia");
            stepTransforms = settings.GetValue<bool>("Modules", "Step");
        }
        if (change.HasChanged("Presentation"))
        {
            mirrorSprite = settings.GetValue<bool>("Presentation", "Ambidexterity");
            tintSprite = settings.GetValue<bool>("Presentation", "Tint");
            playAudio = settings.GetValue<bool>("Presentation", "PlayerTorchAudio");
            sfxVolume = settings.GetValue<float>("Presentation", "AudioVolume");
            offsetX = settings.GetTupleFloat("Presentation", "Offset").First;
            offsetY = settings.GetTupleFloat("Presentation", "Offset").Second;
            scale = settings.GetValue<float>("Presentation", "Scale");
            offsetSpeed = settings.GetValue<float>("Presentation", "Speed") * 2000;
            lockAspectRatio = settings.GetValue<bool>("Presentation", "LockAspectRatio");
            if (audioSourceLoop != null)
                audioSourceLoop.AudioSource.volume = sfxVolume;
        }
        if (change.HasChanged("Bob"))
        {
            bobLength = (float)settings.GetValue<int>("Bob", "Length") / 100; ;
            bobOffset = settings.GetValue<float>("Bob", "Offset");
            bobSizeXMod = settings.GetValue<float>("Bob", "SizeX") * 2;
            bobSizeYMod = settings.GetValue<float>("Bob", "SizeY") * 2;
            moveSmoothSpeed = settings.GetValue<float>("Bob", "SpeedMove") * 4;
            bobSmoothSpeed = settings.GetValue<float>("Bob", "SpeedState") * 500;
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
        if (change.HasChanged("Step"))
        {
            stepLength = settings.GetValue<int>("Step", "Length");
            stepCondition = settings.GetValue<int>("Step", "Condition");
        }
        if (change.HasChanged("Compatibility"))
        {
            scaleTextureFactor = (float)settings.GetValue<int>("Compatibility", "TextureScaleFactor");
        }

        if (change.HasChanged("Presentation") || showSprite != showSpriteOld && currentTexture != null)
            RefreshSprite();
    }
    void InitializeTextures()
    {
        textures = new List<Texture2D>();
        int archive = 112359;
        int record = 0;
        int frame = 0;
        for (int i = 0; i < 99; i++)
        {
            if (record > 1)
                break;

            Texture2D texture;
            bool valid = DaggerfallWorkshop.Utility.AssetInjection.TextureReplacement.TryImportTexture(archive, record, frame, out texture);
            if (valid)
            {
                Debug.Log("Handheld Torches - Found a texture! " + archive.ToString() + "_" + record.ToString() + "-" + frame.ToString());
                texture.filterMode = DaggerfallUnity.Instance.MaterialReader.MainFilterMode;
                textures.Add(texture);
                frame++;
            } else
            {
                Debug.Log("Handheld Torches - Didn't find a texture! " + archive.ToString() + "_" + record.ToString() + "-" + frame.ToString());

                if (record == 1)
                    animLanternLength = frame;
                else
                    animTorchLength = frame;

                frame = 0;
                record++;
                i--;
            }
        }
        currentTexture = textures[0];
    }

    private void OnGUI()
    {
        if (!showSprite || GameManager.IsGamePaused || SaveLoadManager.Instance.LoadInProgress || isInThirdPerson)
            return;

        /*if (lanternRelaxed && GameManager.Instance.PlayerEntity.LightSource != null && GameManager.Instance.PlayerEntity.LightSource.TemplateIndex == 248 && !HasFreeHand)
            return;*/

        if (Event.current.type == EventType.Repaint)
        {
            GUI.depth = 0;

            if (tintSprite)
                DaggerfallUI.DrawTextureWithTexCoords(GetSpriteRect(), currentTexture, curAnimRect, true, GameManager.Instance.WeaponManager.ScreenWeapon.Tint);
            else
                DaggerfallUI.DrawTextureWithTexCoords(GetSpriteRect(), currentTexture, curAnimRect, true, Color.white);
        }
    }

    public Rect GetSpriteRect()
    {
        Rect positionOffset = positionCurrent;

        positionOffset.x += Position.x;
        positionOffset.y += Position.y;

        positionOffset.width += positionOffset.width * Scale.x;
        positionOffset.height += positionOffset.height * Scale.y;

        positionOffset.x -= positionOffset.width * 0.5f;
        positionOffset.y -= positionOffset.height * 0.5f;

        positionOffset.x += positionOffset.width * Offset.x;
        positionOffset.y += positionOffset.height * Offset.y;

        //stop the texture from going higher than its bottom edge
        float largeHUDOffsetHeight = 0;
        if (DaggerfallUI.Instance.DaggerfallHUD != null &&
            DaggerfallUnity.Settings.LargeHUD &&
            (DaggerfallUnity.Settings.LargeHUDUndockedOffsetWeapon || DaggerfallUnity.Settings.LargeHUDDocked))
        {
            largeHUDOffsetHeight = (int)DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.ScreenHeight;
        }
        positionOffset.y = Mathf.Clamp(positionOffset.y, screenRect.height - positionOffset.height- largeHUDOffsetHeight, screenRect.height);

        if (stepTransforms)
        {
            float length = stepLength * (screenRect.height / 64);
            positionOffset.x = Snapping.Snap(positionOffset.x, length);
            positionOffset.y = Snapping.Snap(positionOffset.y, length);
        }

        return positionOffset;
    }

    // Update is called once per frame
    void Update()
    {
        if (GameManager.IsGamePaused)
            return;

        DaggerfallUnityItem lightSource = GameManager.Instance.PlayerEntity.LightSource;

        UpdateFreeHand();

        if (playAudio)
        {
            if (lightSource != null)
            {

                if (lightSource.TemplateIndex == 247 && !audioSourceLoop.AudioSource.isPlaying)
                    audioSourceLoop.AudioSource.Play();
                else if (lightSource.TemplateIndex != 247 && audioSourceLoop.AudioSource.isPlaying)
                    audioSourceLoop.AudioSource.Stop();
            }
            else
            {

                if (lightSource == null && audioSourceLoop.AudioSource.isPlaying)
                    audioSourceLoop.AudioSource.Stop();
            }
        } else
        {
            if (audioSourceLoop.AudioSource.isPlaying)
                audioSourceLoop.AudioSource.Stop();
        }

        if (!GameManager.Instance.WeaponManager.Sheathed)
        {
            if (sheathed)
            {
                //just unsheathed the weapon
                sheathed = false;
            }
        }
        else if (GameManager.Instance.WeaponManager.Sheathed)
        {
            if (!sheathed)
            {
                //just sheathed the weapon
                sheathed = true;

                if (lanternRelaxed && GameManager.Instance.PlayerEntity.LightSource != null && GameManager.Instance.PlayerEntity.LightSource.TemplateIndex == 248 && HasFreeHand)
                    SetGuard();
            }
        }

        if (GameManager.Instance.WeaponManager.UsingRightHand)
        {
            if (!usingRightHand)
            {
                //just swapped hands to right
                usingRightHand = true;

                if (lanternRelaxed && GameManager.Instance.PlayerEntity.LightSource != null && GameManager.Instance.PlayerEntity.LightSource.TemplateIndex == 248 && HasFreeHand)
                    SetGuard();
            }
        }
        else
        {
            if (usingRightHand)
            {
                //just swapped hands to left
                usingRightHand = false;

                if (lanternRelaxed && GameManager.Instance.PlayerEntity.LightSource != null && GameManager.Instance.PlayerEntity.LightSource.TemplateIndex == 248 && HasFreeHand)
                    SetGuard();
            }
        }

        //check if no hands are free while a light source is equipped
        if (!HasFreeHand && lightSource != null && lightSource.TemplateIndex != 248)
        {
            if (onStow > 0)
                DropLightSource(lightSource);
            else
            {
                lastLightSource = lightSource;
                GameManager.Instance.PlayerEntity.LightSource = null;
            }
        }
        else if (!HasFreeHand && lightSource != null && lightSource.TemplateIndex == 248 && !lanternRelaxed)
        {
            if (!sheathed)
                DaggerfallUI.Instance.PopupMessage(messageNoFreeHand);
            lastLightSource = lightSource;
            GameManager.Instance.PlayerEntity.LightSource = null;
        }
        else if (HasFreeHand && lastLightSource != null)
        {
            //check if last light source is not null and re-equip it when possible
            GameManager.Instance.PlayerEntity.LightSource = lastLightSource;
            lastLightSource = null;
        }

        //animate sprite
        if (showSprite)
        {
            if (lightSource != null)
            {
                if (lightSource.TemplateIndex == 248)   //is a lantern
                    offsetFrame = 4;
                else if (lightSource.TemplateIndex == 247) //is a torch
                    offsetFrame = 0;
                else
                    offsetFrame = -1;
            }

            if (offsetFrame != -1) {
                if (animationTimer > animationTime)
                {
                    animationTimer = 0;

                    int length = animTorchLength;
                    if (offsetFrame == 4)
                        length = animLanternLength;

                    if (currentFrame < length-1)
                        currentFrame++;
                    else
                        currentFrame = 0;

                    currentTexture = textures[offsetFrame + currentFrame];
                }
                else
                {
                    animationTimer += Time.deltaTime;
                }
            }
        }

        bool refreshSprite = false;
        if (lightSource != null)
        {
            if (!hasLightSource)
            {
                //EVENT: has ignited a light source
                hasLightSource = true;

                //Refresh sprite if light source is a torch or lantern
                if (lightSource.TemplateIndex == 247 || lightSource.TemplateIndex == 248)
                    refreshSprite = true;
            }

            if (lightSourceTemplateIndexLast != lightSource.TemplateIndex)
            {
                //EVENT: has switched light sources
                lightSourceTemplateIndexLast = lightSource.TemplateIndex;

                //Refresh sprite if light source is a torch or lantern
                if (lightSource.TemplateIndex == 247 || lightSource.TemplateIndex == 248)
                    refreshSprite = true;
            }
        }
        else
        {
            if (hasLightSource)
            {
                //EVENT: has doused or discarded a light source
                hasLightSource = false;
            }

            lightSourceTemplateIndexLast = -1;
        }

        if (refreshSprite)
            RefreshSprite();

        //Toggle lightsource
        if (InputManager.Instance.GetKeyDown(toggleLightKeyCode))
        {
            if (lanternRelaxed)
            {
                if (HasFreeHand || GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, 248))
                    ToggleLightSourceAction();
                else
                    DaggerfallUI.Instance.PopupMessage(messageNoFreeHand);
            }
            else
            {
                if (HasFreeHand)
                    ToggleLightSourceAction();
                else
                    DaggerfallUI.Instance.PopupMessage(messageNoFreeHand);
            }
        }

        //Manually drop a lightsource
        if (InputManager.Instance.GetKeyDown(dropKeyCode))
        {
            if (HasFreeHand)
                DropLightSourceAction(lightSource);
            else
                DaggerfallUI.Instance.PopupMessage(messageNoFreeHand);
        }

        /*//Throw a lightsource
        if (InputManager.Instance.GetKeyDown(throwKeyCode))
        {
            if (!sheathed)
            {
                if (HasFreeHand)
                    ThrowLightSourceAction(lightSource);
                else
                    DaggerfallUI.Instance.PopupMessage(messageNoFreeHand);
            }
            else
                ThrowLightSourceAction(lightSource);
        }*/

        //Throw a lightsource, scale strength by time
        if (InputManager.Instance.GetKeyDown(throwKeyCode))
        {
            if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, (int)UselessItems2.Torch))
            {
                if (!HasFreeHand)
                    DaggerfallUI.Instance.PopupMessage(messageNoFreeHand);
                else
                {
                    if (lightSource != null && ((lanternRelaxed && lightSource.TemplateIndex != (int)UselessItems2.Lantern) || !lanternRelaxed))
                    {
                        GameManager.Instance.PlayerEntity.LightSource = null;
                    }
                }
            }
            else
                DaggerfallUI.Instance.PopupMessage(Instance.messageThrowTorchless);
        }

        if (InputManager.Instance.GetKey(throwKeyCode))
        {
            if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, (int)UselessItems2.Torch))
            {
                if (HasFreeHand)
                {
                    throwTimer += Time.deltaTime * throwScale;

                    if (throwDrawTrajectory)
                        DrawTrajectory();
                }
            }
        }

        if (InputManager.Instance.GetKeyUp(throwKeyCode))
        {
            if (HasFreeHand)
            {
                float throwScale = Mathf.Clamp(throwTimer / throwTime, 0.25f, 2f);
                ThrowLightSourceAction(lightSource, throwScale);
            }
            else
                DaggerfallUI.Instance.PopupMessage(messageNoFreeHand);

            if (lineRenderer.enabled)
                lineRenderer.enabled = false;

            throwTimer = 0;
        }

            //update light sources
        if (billboardsObject.Count > 0 || billboardsTime.Count > 0)
        {
            CleanUpLightSources();

            for (int i = 0; i < billboardsTime.Count; i++)
            {
                billboardsTime[i] -= (DaggerfallUnity.Instance.WorldTime.TimeScale/12) * Time.deltaTime;
            }

            for (int i = 0; i < billboardsObject.Count; i++)
            {
                if (billboardsObject[i] == null)
                    continue;

                if (billboardsTime.Count <= i)
                    break;

                if (billboardsTime[i] <= 0)
                {
                    billboardsItemTemplateIndex[i] = 0;
                    Destroy(billboardsObject[i]);
                }
                else
                {
                    Light light = billboardsObject[i].GetComponentInChildren<Light>();
                    if (light != null)
                    {
                        float factor = billboardsTime[i] / (GameManager.Instance.ItemHelper.GetItemTemplate(billboardsItemTemplateIndex[i]).hitPoints*20);
                        if (light.color != playerLightSource.color)
                            light.color = playerLightSource.color;
                        light.intensity = (1.25f + (Mathf.Sin(Time.time*2)*0.1f)) * DaggerfallUnity.Settings.PlayerTorchLightScale;
                        light.range = 1 + GameManager.Instance.ItemHelper.GetItemTemplate(billboardsItemTemplateIndex[i]).capacityOrTarget * factor * DaggerfallUnity.Settings.PlayerTorchLightScale;
                        //billboardsObject[i].GetComponent
                    }
                }
            }
        }
    }

    void DrawTrajectory()
    {
        if (!lineRenderer.enabled)
            lineRenderer.enabled = true;

        positions = new List<Vector3>();

        //get start
        Vector3 start = GameManager.Instance.PlayerObject.transform.position;

        int freeHand = GetFreeHand;
        if (freeHand == 2)
            start += GameManager.Instance.MainCameraObject.transform.right * 0.35f;
        else
            start += GameManager.Instance.MainCameraObject.transform.right * -0.35f;

        positions.Add(start);

        Vector3 dir = Quaternion.AngleAxis(-throwAngle, GameManager.Instance.MainCameraObject.transform.right) * GameManager.Instance.MainCameraObject.transform.forward;
        Vector3 startStep = dir * 25 * (GameManager.Instance.PlayerEntity.Stats.LiveStrength / 100f) * throwStrength * Mathf.Clamp(throwTimer / throwTime, 0.25f, 2f);

        //get all positions
        bool stopped = false;
        Vector3 currentPos = start;
        Vector3 currentGravity = Vector3.zero;
        Vector3 currentStep = startStep;
        Ray ray = new Ray(currentPos, currentStep.normalized);
        RaycastHit hit = new RaycastHit();
        while (!stopped && positions.Count < 300)
        {
            startStep = dir * 25 * (GameManager.Instance.PlayerEntity.Stats.LiveStrength / 100f) * throwStrength * Mathf.Clamp(throwTimer / throwTime, 0.25f, 2f);

            currentGravity += Vector3.down * (9.8f * (throwGravity * 0.05f));
            currentStep = (startStep + currentGravity) * Time.fixedDeltaTime;

            ray = new Ray(currentPos, currentStep.normalized);

            if (Physics.Raycast(ray, out hit, currentStep.magnitude, layerMaskPlayer))
            {
                stopped = true;
                positions.Add(hit.point);
            }
            else
            {
                currentPos += currentStep;
                positions.Add(currentPos);
            }
        }
        lineRenderer.positionCount = positions.Count;
        lineRenderer.SetPositions(positions.ToArray());
    }

    private void LateUpdate()
    {
        //if off-hand is shield
        DaggerfallUnityItem itemLightSource = GameManager.Instance.PlayerEntity.LightSource;

        Position = Vector2.zero;
        Offset = Vector2.zero;
        Scale = Vector2.zero;

        attacking = GameManager.Instance.WeaponManager.ScreenWeapon.IsAttacking();

        if (GameManager.Instance.PlayerSpellCasting.IsPlayingAnim || GameManager.Instance.PlayerEffectManager.HasReadySpell)
        {
            //just started spellcasting
            if (!spellcasting)
            {
                spellcasting = true;
            }
        }
        else
        {
            //just stopped spellcasting
            if (spellcasting)
            {
                spellcasting = false;
            }
        }

        climbing = GameManager.Instance.PlayerMotor.IsClimbing;
        swimming = GameManager.Instance.PlayerMotor.IsSwimming;

        //control sprite
        if (itemLightSource != null && offsetFrame >= 0)
        {
            /*if (itemLightSource.TemplateIndex != 247)
                return;*/

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
            weaponOffsetHeight = 0;
            if (DaggerfallUI.Instance.DaggerfallHUD != null &&
                DaggerfallUnity.Settings.LargeHUD &&
                (DaggerfallUnity.Settings.LargeHUDUndockedOffsetWeapon || DaggerfallUnity.Settings.LargeHUDDocked))
            {
                weaponOffsetHeight = (int)DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.ScreenHeight;
            }

            if (screenRect != screenRectLast || weaponOffsetHeight != weaponOffsetHeightLast)
                RefreshSprite();

            screenRectLast = screenRect;
            weaponOffsetHeightLast = weaponOffsetHeight;


            if (lanternRelaxed && GameManager.Instance.PlayerEntity.LightSource != null && GameManager.Instance.PlayerEntity.LightSource.TemplateIndex == 248 && !HasFreeHand)
                SetSheathe();
            else
            {
                if (attacking)
                {
                    //adjust shield position when attacking
                    if (!attacked)
                    {
                        attacked = true;
                        SetAttack();
                    }
                }
                else
                {
                    if (attacked)
                    {
                        attacked = false;
                        RefreshSprite();
                    }
                }
            }

            Vector3 current = new Vector3(positionCurrent.x, positionCurrent.y, 0);
            Vector3 target = new Vector3(positionTarget.x, positionTarget.y, 0);
            current = Vector3.MoveTowards(current, target, Time.deltaTime * offsetSpeedLive);
            positionCurrent = new Rect(current.x, current.y, positionTarget.width, positionTarget.height);

            //SCALE  TO SPEED AND MOVEMENT
            float currentSpeed = GameManager.Instance.PlayerMotor.Speed;
            float baseSpeed = GameManager.Instance.SpeedChanger.GetBaseSpeed();
            float speed = currentSpeed / baseSpeed;

            //start of weapon bob code
            if (bob && !attacking && positionCurrent == positionTarget)
            {
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

                //GET CURRENT BOB VALUES
                Vector2 bobRaw = new Vector2((screenOffsetX + Mathf.Sin(bobOffset + Time.time * bobXSpeed)) * -bobSize.x, (screenOffsetY - Mathf.Sin(bobOffset + bobYOffset + Time.time * bobYSpeed)) * bobSize.y);

                //SMOOTH TRANSITIONS BETWEEN WALKING, RUNNING, CROUCHING, ETC
                bobSmooth = Vector2.MoveTowards(bobSmooth, bobRaw, Time.deltaTime * bobSmoothSpeed) * moveSmooth;

                Position += bobSmooth;
            }

            //inertia
            if (inertia)
            {
                if (attacking || positionCurrent != positionTarget)
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
        }
        else
        {
            SetSheathe();
            if (hasDroppedOrThrownLight)
            {
                positionCurrent = positionTarget;
                hasDroppedOrThrownLight = false;
            }
            else if (lanternRelaxed && !HasFreeHand)
            {
                positionCurrent = positionTarget;
            }
            else
            {
                Vector3 current = new Vector3(positionCurrent.x, positionCurrent.y, 0);
                Vector3 target = new Vector3(positionTarget.x, positionTarget.y, 0);
                current = Vector3.MoveTowards(current, target, Time.deltaTime * offsetSpeedLive * 3);
                positionCurrent = new Rect(current.x, current.y, positionTarget.width, positionTarget.height);
            }
        }
    }

    public void RefreshSprite()
    {
        if (currentTexture == null)
            return;

        if (GameManager.Instance.PlayerEntity.LightSource != null)
        {
            if (GameManager.Instance.PlayerEntity.LightSource.TemplateIndex == 248)
                offsetFrame = 4;
            else
                offsetFrame = 0;

            currentTexture = textures[currentFrame + offsetFrame];
        }

        //if (GameManager.Instance.WeaponManager.ScreenWeapon.IsAttacking() || !HasFreeHand)

        if (lanternRelaxed && GameManager.Instance.PlayerEntity.LightSource != null && GameManager.Instance.PlayerEntity.LightSource.TemplateIndex == 248 && !HasFreeHand)
            SetGuard();
        else
        {
            if (GameManager.Instance.WeaponManager.ScreenWeapon.IsAttacking())
                SetAttack();
            else
                SetGuard();
        }
    }

    public void SetGuard()
    {
        weaponScaleX = (float)screenRect.width / (float)nativeScreenWidth;
        if (lockAspectRatio)
            weaponScaleY = weaponScaleX;
        else
            weaponScaleY = (float)screenRect.height / (float)nativeScreenHeight;

        if (flipped)
        {
            positionTarget = new Rect(
                screenRect.x + screenRect.width - ((screenRect.width * 0.5f) * offsetX),
                screenRect.y + screenRect.height - weaponOffsetHeight - ((screenRect.height * 0.25f) * offsetY),
                currentTexture.width * scale * weaponScaleX / scaleTextureFactor,
                currentTexture.height * scale * weaponScaleY / scaleTextureFactor
                );
        }
        else
        {
            positionTarget = new Rect(
                screenRect.x + ((screenRect.width * 0.5f) * offsetX),
                screenRect.y + screenRect.height - weaponOffsetHeight - ((screenRect.height * 0.25f) * offsetY),
                currentTexture.width * scale * weaponScaleX / scaleTextureFactor,
                currentTexture.height * scale * weaponScaleY / scaleTextureFactor
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

        if (flipped)
        {
            positionTarget = new Rect(
                screenRect.x + screenRect.width,
                screenRect.y + screenRect.height - weaponOffsetHeight,
                currentTexture.width * scale * weaponScaleX / scaleTextureFactor,
                currentTexture.height * scale * weaponScaleY / scaleTextureFactor
                );
        }
        else
        {
            positionTarget = new Rect(
                screenRect.x,
                screenRect.y + screenRect.height - weaponOffsetHeight,
                currentTexture.width * scale * weaponScaleX / scaleTextureFactor,
                currentTexture.height * scale * weaponScaleY / scaleTextureFactor
                );
        }
    }

    public void SetSheathe()
    {
        weaponScaleX = (float)screenRect.width / (float)nativeScreenWidth;
        if (lockAspectRatio)
            weaponScaleY = weaponScaleX;
        else
            weaponScaleY = (float)screenRect.height / (float)nativeScreenHeight;

        if (flipped)
        {
            positionTarget = new Rect(
                screenRect.x + screenRect.width,
                screenRect.y + screenRect.height - weaponOffsetHeight + currentTexture.height * scale * weaponScaleY / scaleTextureFactor,
                currentTexture.width * scale * weaponScaleX / scaleTextureFactor,
                currentTexture.height * scale * weaponScaleY / scaleTextureFactor
                );
        }
        else
        {
            positionTarget = new Rect(
                screenRect.x,
                screenRect.y + screenRect.height - weaponOffsetHeight + currentTexture.height * scale * weaponScaleY / scaleTextureFactor,
                currentTexture.width * scale * weaponScaleX / scaleTextureFactor,
                currentTexture.height * scale * weaponScaleY / scaleTextureFactor
                );
        }
    }


    public void UpdateFreeHand()
    {
        handRight = true;
        handLeft = true;

        DaggerfallUnityItem itemLeftHand = GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(EquipSlots.LeftHand);
        DaggerfallUnityItem itemRightHand = GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(EquipSlots.RightHand);

        if (GameManager.Instance.WeaponManager.Sheathed)
        {
            //check if wielding a shield or bow in left hand
            if (itemLeftHand != null)
            {
                if (itemLeftHand.IsShield || GameManager.Instance.ItemHelper.ConvertItemToAPIWeaponType(itemLeftHand) == WeaponTypes.Bow)
                    handLeft = false;
            }
        }
        else
        {
            //check if wielding anything in left hand
            if (itemLeftHand != null)
            {
                handLeft = false;
                if (ItemEquipTable.GetItemHands(itemLeftHand) == ItemHands.Both || GameManager.Instance.ItemHelper.ConvertItemToAPIWeaponType(itemLeftHand) == WeaponTypes.Bow)
                    handRight = false;
            }
            else if (!GameManager.Instance.WeaponManager.UsingRightHand) //if left hand is free and used for punching, disallow torch
            {
                handLeft = false;
            }

            //check if wielding anything in right hand
            if (itemRightHand != null)
            {
                handRight = false;
                if (ItemEquipTable.GetItemHands(itemRightHand) == ItemHands.Both) //if right hand item is two-handed, occupy the other hand even if free
                {
                    if (twoHandedRelaxed)
                    {
                        if (GameManager.Instance.ItemHelper.ConvertItemToAPIWeaponType(itemRightHand) == WeaponTypes.Bow || attacking)
                            handLeft = false;
                    }
                    else
                        handLeft = false;
                }
            }
            else if (GameManager.Instance.WeaponManager.UsingRightHand) //if right hand is free and used for punching, disallow torch
            {
                handRight = false;
            }
        }

        //occupy both hands if spellcasting
        if (stowOnSpellcasting && spellcasting)
        {
            handRight = false;
            handLeft = false;
        }

        //occupy both hands if climbing
        if (stowOnClimbing && climbing)
        {
            handRight = false;
            handLeft = false;
        }
        //occupy both hands if swimming
        if (stowOnSwimming && swimming)
        {
            handRight = false;
            handLeft = false;
        }

        //prevent transformed lycanthropes from carrying a torch
        if (GameManager.Instance.PlayerEffectManager.IsTransformedLycanthrope())
        {
            handRight = false;
            handLeft = false;
        }

        if (mirrorSprite)
        {
            if (DaggerfallUnity.Settings.Handedness == 1)
                flippedCurrent = true;
            else
                flippedCurrent = false;

            if (!handLeft && handRight)
                flippedCurrent = !flippedCurrent;
            else if (!handLeft && !handRight)
                flippedCurrent = flippedLast;

            if (flippedCurrent != flippedLast)
            {
                flipped = flippedCurrent;
                flippedLast = flippedCurrent;

                if (flipped)
                    curAnimRect = new Rect(1, 0, -1, 1);
                else
                    curAnimRect = new Rect(0, 0, 1, 1);

                RefreshSprite();

                if (GameManager.Instance.PlayerEntity.LightSource != null)
                {
                    float x = 1;
                    if (flipped)
                        x = -1;
                    if (GameManager.Instance.PlayerEntity.LightSource.TemplateIndex == 248)
                        GameManager.Instance.PlayerObject.GetComponent<EnablePlayerTorch>().PlayerTorch.transform.localPosition = new Vector3(-0.26f, 0, 0.25f);
                    else
                        GameManager.Instance.PlayerObject.GetComponent<EnablePlayerTorch>().PlayerTorch.transform.localPosition = new Vector3(-0.34f * x, 0.9f, 0.25f);
                }

                SetSheathe();
                positionCurrent = positionTarget;
            }
        }
        else
        {
            if (DaggerfallUnity.Settings.Handedness == 1)
                flipped = true;
            else
                flipped = false;
        }
    }

    void DropLightSource(DaggerfallUnityItem item)
    {
        Vector3 pos;
        float offset = 0;
        /*if (item.ItemTemplate.index == 247) //is torch
            offset = 0.45f;
        else if (item.ItemTemplate.index == 253) //is candle
            offset = 0.3f;
        else if (item.ItemTemplate.index == 269) //is holy candle
            offset = 0.3f;*/

        //get forward bounds
        Ray ray1 = new Ray(GameManager.Instance.PlayerObject.transform.position, GameManager.Instance.PlayerObject.transform.forward);
        RaycastHit hit = new RaycastHit();
        if (Physics.Raycast(ray1, out hit, 1.45f, layerMaskPlayer))
        {
            pos = hit.point - (GameManager.Instance.PlayerObject.transform.forward * offset);
        }
        else
            pos = ray1.origin + ray1.direction;

        //get lower bounds
        Ray ray2 = new Ray(pos, Vector3.down);
        if (Physics.Raycast(ray2, out hit, 145f, layerMaskPlayer))
        {
            pos = hit.point + (Vector3.up * offset);
        }
        else
            pos = ray2.origin + ray2.direction;

        //Spawn a torch given the player's position and the torch's condition
        SpawnLightSource(item.ItemTemplate.index, pos, item.currentCondition * 20f);

        //remove from inventory
        if (item == GameManager.Instance.PlayerEntity.LightSource)
            GameManager.Instance.PlayerEntity.LightSource = null;
        GameManager.Instance.PlayerEntity.Items.RemoveItem(item);

        DaggerfallUI.Instance.PopupMessage(Instance.messageDrop + item.GetMacroDataSource().Condition().ToLower() + " " + item.LongName.ToLower());

        Instance.audioSourceOneShot.PlayClipAtPoint(380, pos, 1);

        hasDroppedOrThrownLight = true;
    }

    void ThrowLightSource(DaggerfallUnityItem item, float scale = 1)
    {
        Vector3 pos = GameManager.Instance.PlayerObject.transform.position;
        Vector3 dir = GameManager.Instance.MainCameraObject.transform.forward;

        //Spawn a torch given the player's position and the torch's condition
        SpawnLightSourceProjectile(item.ItemTemplate.index, item.currentCondition * 20f, pos, dir, scale);

        //remove from inventory
        if (item == GameManager.Instance.PlayerEntity.LightSource)
            GameManager.Instance.PlayerEntity.LightSource = null;
        GameManager.Instance.PlayerEntity.Items.RemoveItem(item);

        audioSourceOneShot.PlayOneShot((int)SoundClips.SwingHighPitch, 0, 1);
        DaggerfallUI.Instance.PopupMessage(Instance.messageThrow + item.GetMacroDataSource().Condition().ToLower() + " " + item.LongName.ToLower());

        //Instance.audioSourceOneShot.PlayClipAtPoint(380, positionCurrent, 1);

        hasDroppedOrThrownLight = true;
    }

    void ThrowLightSourceAction(DaggerfallUnityItem item, float scale = 1)
    {
        if (item != null && item.TemplateIndex == 247)
            ThrowLightSource(item, scale);
        else
        {
            if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, (int)UselessItems2.Torch))
            {
                //Drop first torch
                ThrowLightSource(GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.UselessItems2, (int)UselessItems2.Torch), scale);
            }
            else
                DaggerfallUI.Instance.PopupMessage(Instance.messageThrowTorchless);
        }
    }

    void DropLightSourceAction(DaggerfallUnityItem item)
    {
        if (item != null && item.TemplateIndex != 248)
            DropLightSource(item);
        else
        {
            if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, (int)UselessItems2.Torch))
            {
                //Drop first torch
                DropLightSource(GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.UselessItems2, (int)UselessItems2.Torch));
            }
            else if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, (int)UselessItems2.Candle))
            {
                //Drop first candle
                DropLightSource(GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.UselessItems2, (int)UselessItems2.Candle));
            }
            else if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.ReligiousItems, (int)ReligiousItems.Holy_candle))
            {
                //Drop first holy candle
                DropLightSource(GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.ReligiousItems, (int)ReligiousItems.Holy_candle));
            }
            else
                DaggerfallUI.Instance.PopupMessage(Instance.messageDropTorchless);
        }
    }

    void ToggleLightSourceAction()
    {
        DaggerfallUnityItem lightSource = GameManager.Instance.PlayerEntity.LightSource;

        //if light source is active disable it
        if (lightSource != null)
        {
            lastLightTemplateIndex = lightSource.TemplateIndex;

            //lastLightSource = lightSource;
            DaggerfallUI.Instance.PopupMessage(Instance.messageDouse + lightSource.GetMacroDataSource().Condition().ToLower() + " " + lightSource.LongName.ToLower());
            GameManager.Instance.PlayerEntity.LightSource = null;
            audioSourceOneShot.PlayOneShot((int)SoundClips.EquipClothing, 0, 1);
        }
        else
        {
            if (lastLightSource != null)
            {
                GameManager.Instance.PlayerEntity.LightSource = lastLightSource;
                lastLightSource = null;
            }
            else
            {
                lastLightSource = null;

                //Ignite last used light source
                if (lastLight && lastLightTemplateIndex != 0)
                {
                    if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, lastLightTemplateIndex))
                        GameManager.Instance.PlayerEntity.LightSource = GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.UselessItems2, lastLightTemplateIndex);
                    else if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.ReligiousItems, lastLightTemplateIndex))
                        GameManager.Instance.PlayerEntity.LightSource = GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.ReligiousItems, lastLightTemplateIndex);
                }

                if (GameManager.Instance.PlayerEntity.LightSource == null)
                {
                    if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, (int)UselessItems2.Lantern))
                    {
                        //Ignite first Lantern
                        GameManager.Instance.PlayerEntity.LightSource = GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.UselessItems2, (int)UselessItems2.Lantern);
                    }
                    else if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, (int)UselessItems2.Torch))
                    {
                        //Ignite first torch
                        GameManager.Instance.PlayerEntity.LightSource = GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.UselessItems2, (int)UselessItems2.Torch);
                    }
                    else if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, (int)UselessItems2.Candle))
                    {
                        //Drop first candle
                        GameManager.Instance.PlayerEntity.LightSource = GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.UselessItems2, (int)UselessItems2.Candle);
                    }
                    else if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.ReligiousItems, (int)ReligiousItems.Holy_candle))
                    {
                        //Drop first holy candle
                        GameManager.Instance.PlayerEntity.LightSource = GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.ReligiousItems, (int)ReligiousItems.Holy_candle);
                    }
                    else
                        DaggerfallUI.Instance.PopupMessage(Instance.messageIgniteTorchless);
                }

                if (GameManager.Instance.PlayerEntity.LightSource != null)
                {
                    //we ignited a light
                    lastLightTemplateIndex = GameManager.Instance.PlayerEntity.LightSource.TemplateIndex;
                    audioSourceOneShot.PlayOneShot((int)SoundClips.Ignite, 0, 0.5f);
                    DaggerfallUI.Instance.PopupMessage(Instance.messageIgnite + GameManager.Instance.PlayerEntity.LightSource.GetMacroDataSource().Condition().ToLower() + " " + GameManager.Instance.PlayerEntity.LightSource.LongName.ToLower());
                    RefreshSprite();
                }
            }
        }
    }

    public void SpawnLightSource(int itemTemplateIndex, Vector3 position, float time)
    {
        //check if spawn point is below water level
        int indexOffset = 0;
        if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeon && GameManager.Instance.PlayerEnterExit.blockWaterLevel != 10000 && position.y + (50 * MeshReader.GlobalScale) < GameManager.Instance.PlayerEnterExit.blockWaterLevel * -1 * MeshReader.GlobalScale)
            indexOffset = 10;

        //spawn billboard object
        GameObject billboardObject = null;
        //DaggerfallUnityItem item = ItemHelper.GetItem;
        if (itemTemplateIndex == 247) //is torch
        {
            billboardObject = GameObjectHelper.CreateDaggerfallBillboardGameObject(112358, 0 + indexOffset, null);
            //billboardObject.name = "HandheldTorches - Torch";
        }
        else if (itemTemplateIndex == 253) //is candle
        {
            billboardObject = GameObjectHelper.CreateDaggerfallBillboardGameObject(112358, 1 + indexOffset, null);
            //billboardObject.name = "HandheldTorches - Candle";
        }
        else if (itemTemplateIndex == 269) //is holy candle
        {
            billboardObject = GameObjectHelper.CreateDaggerfallBillboardGameObject(112358, 2 + indexOffset, null);
            //billboardObject.name = "HandheldTorches - Holy Candle";
        }
        billboardObject.transform.position = position;

        //offset the object billboard upwards
        Renderer renderer = billboardObject.GetComponent<Renderer>();
        billboardObject.transform.position += Vector3.up * (renderer.material.mainTexture.height*0.0125f)/scaleTextureFactor;

        if (UnityEngine.Random.value > 0.5f)
            billboardObject.transform.localScale = new Vector3(-billboardObject.transform.localScale.x, billboardObject.transform.localScale.y, billboardObject.transform.localScale.z);

        //set collider layer to Ignore Raycast for TOB and WW
        billboardObject.layer = LayerMask.NameToLayer("Ignore Raycast");

        billboardsObject.Add(billboardObject);

        //set condition
        billboardsTime.Add(time);

        //set item
        billboardsItemTemplateIndex.Add(itemTemplateIndex);

        //add light and audio if not doused
        if (indexOffset == 0)
        {
            //add light
            GameObject lightSource = GameObjectHelper.InstantiatePrefab(DaggerfallUnity.Instance.Option_InteriorLightPrefab.gameObject, string.Empty, billboardObject.transform, billboardObject.transform.position + (Vector3.up * 0.5f));
            Light light = lightSource.GetComponent<Light>();
            if (light != null)
            {
                light.color = playerLightSource.color;
                light.intensity = 1.25f * DaggerfallUnity.Settings.PlayerTorchLightScale;
                light.range = DaggerfallUnity.Instance.ItemHelper.GetItemTemplate(itemTemplateIndex).capacityOrTarget;
                light.type = playerLightSource.type;
                light.shadows = playerLightSource.shadows;
            }

            //if torch, add audio
            if (itemTemplateIndex == 247)
            {
                DaggerfallAudioSource audioSource = billboardObject.AddComponent<DaggerfallAudioSource>();
                audioSource.AudioSource.dopplerLevel = 0;
                audioSource.AudioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.AudioSource.maxDistance = 5f;
                audioSource.AudioSource.volume = sfxVolume;
                audioSource.SetSound(SoundClips.Burning, AudioPresets.LoopIfPlayerNear);
            }
        }

        //if outside, track the torch as a loose object
        if (!GameManager.Instance.PlayerEnterExit.IsPlayerInside)
            GameManager.Instance.StreamingWorld.TrackLooseObject(billboardObject, false, -1, -1, true);
    }

    public void SpawnLightSourceProjectile(int itemTemplateIndex, float time, Vector3 pos, Vector3 dir, float scale = 1)
    {
        //check if spawn point is below water level
        int indexOffset = 0;
        if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeon && GameManager.Instance.PlayerEnterExit.blockWaterLevel != 10000 && pos.y + (50 * MeshReader.GlobalScale) < GameManager.Instance.PlayerEnterExit.blockWaterLevel * -1 * MeshReader.GlobalScale)
            indexOffset = 10;

        int freeHand = GetFreeHand;
        if (freeHand == 2)
            pos += GameManager.Instance.MainCameraObject.transform.right * 0.35f;
        else
            pos += GameManager.Instance.MainCameraObject.transform.right * -0.35f;

        //spawn billboard object
        GameObject billboardObject = null;
        billboardObject = GameObjectHelper.CreateDaggerfallBillboardGameObject(112358, 0 + indexOffset, null);
        billboardObject.transform.position = pos;
        billboardObject.layer = LayerMask.NameToLayer("Ignore Raycast");

        //add light and audio if not doused
        if (indexOffset == 0)
        {
            //add light
            GameObject lightSource = GameObjectHelper.InstantiatePrefab(DaggerfallUnity.Instance.Option_InteriorLightPrefab.gameObject, string.Empty, billboardObject.transform, billboardObject.transform.position + (Vector3.up * 0.5f));
            Light light = lightSource.GetComponent<Light>();
            if (light != null)
            {
                light.color = playerLightSource.color;
                light.intensity = 1.25f * DaggerfallUnity.Settings.PlayerTorchLightScale;
                light.range = DaggerfallUnity.Instance.ItemHelper.GetItemTemplate(itemTemplateIndex).capacityOrTarget;
                light.type = playerLightSource.type;
                light.shadows = playerLightSource.shadows;
            }

            //if torch, add audio
            if (itemTemplateIndex == 247)
            {
                DaggerfallAudioSource audioSource = billboardObject.AddComponent<DaggerfallAudioSource>();
                audioSource.AudioSource.dopplerLevel = 0;
                audioSource.AudioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.AudioSource.maxDistance = 5f;
                audioSource.AudioSource.volume = sfxVolume;
                audioSource.SetSound(SoundClips.Burning, AudioPresets.LoopIfPlayerNear);
            }
        }

        HandheldTorchesProjectile projectile = billboardObject.AddComponent<HandheldTorchesProjectile>();
        projectile.Initialize(itemTemplateIndex,time,pos,dir,scale);
    }

    public static void PickUpLightSource(RaycastHit hit)
    {
        Debug.Log("Attempting to pick up a light source!");
        if (GameManager.Instance.PlayerActivate.CurrentMode == PlayerActivateModes.Grab || GameManager.Instance.PlayerActivate.CurrentMode == PlayerActivateModes.Steal)
        {
            for (int i = 0; i < Instance.billboardsObject.Count; i++)
            {
                if (hit.transform.gameObject.GetInstanceID() == Instance.billboardsObject[i].GetInstanceID())
                {
                    PickupLightSource(i);
                    break;
                }
            }
        }
        else if (GameManager.Instance.PlayerActivate.CurrentMode == PlayerActivateModes.Info || GameManager.Instance.PlayerActivate.CurrentMode == PlayerActivateModes.Talk)
        {
            for (int i = 0; i < Instance.billboardsObject.Count; i++)
            {
                if (hit.transform.gameObject.GetInstanceID() == Instance.billboardsObject[i].GetInstanceID())
                {
                    DaggerfallUI.Instance.PopupMessage(Instance.messageExamine + DaggerfallUnity.Instance.ItemHelper.GetItemTemplate(Instance.billboardsItemTemplateIndex[i]).name.ToLower());
                    break;
                }
            }
        }
    }

    public static void PickupLightSource(int index)
    {
        //add item to player inventory
        DaggerfallUnityItem item = null;
        if (Instance.billboardsItemTemplateIndex[index] == 247) //is torch
            item = ItemBuilder.CreateItem(ItemGroups.UselessItems2, (int)UselessItems2.Torch);
        else if (Instance.billboardsItemTemplateIndex[index] == 253) //is candle
            item = ItemBuilder.CreateItem(ItemGroups.UselessItems2, (int)UselessItems2.Candle);
        else if (Instance.billboardsItemTemplateIndex[index] == 269) //is holy candle
            item = ItemBuilder.CreateItem(ItemGroups.ReligiousItems, (int)ReligiousItems.Holy_candle);

        GameManager.Instance.PlayerEntity.Items.AddItem(item);

        //set item condition
        item.currentCondition = Mathf.CeilToInt(Instance.billboardsTime[index] / 20);

        //destroy world object
        Instance.billboardsItemTemplateIndex[index] = 0;
        Destroy(Instance.billboardsObject[index]);
        Instance.billboardsTime[index] = -1;

        if (Instance.onPick > 0 && GameManager.Instance.PlayerEntity.LightSource == null)
        {
            if (Instance.HasFreeHand)
            {
                GameManager.Instance.PlayerEntity.LightSource = item;
                DaggerfallUI.Instance.PopupMessage(Instance.messagePickupEquip + item.GetMacroDataSource().Condition().ToLower() + " " + item.LongName.ToLower());
            }
            else if (Instance.onPick == 2 && !GameManager.Instance.WeaponManager.Sheathed)
            {
                GameManager.Instance.WeaponManager.SheathWeapons();
                GameManager.Instance.PlayerEntity.LightSource = item;
                DaggerfallUI.Instance.PopupMessage(Instance.messagePickupEquip + item.GetMacroDataSource().Condition().ToLower() + " " + item.LongName.ToLower());
            }
            else
            {
                DaggerfallUI.Instance.PopupMessage(Instance.messagePickupStore + item.GetMacroDataSource().Condition().ToLower() + " " + item.LongName.ToLower());
                if (Instance.lastLightSource == null)
                    Instance.lastLightSource = item;
            }
        }
        else
        {
            DaggerfallUI.Instance.PopupMessage(Instance.messagePickupStore + item.GetMacroDataSource().Condition().ToLower() + " " + item.LongName.ToLower());
        }

       Instance.audioSourceOneShot.PlayOneShot(417, 0, 1);
    }

    public void CleanUpLightSources()
    {
        billboardsObject.RemoveAll(x => x == null);
        billboardsTime.RemoveAll(y => y < 0);
        billboardsItemTemplateIndex.RemoveAll(z => z == 0);
    }

    public static void DestroyLightSources()
    {
        if (Instance.billboardsObject.Count < 1)
            return;

        foreach (GameObject billboard in Instance.billboardsObject)
        {
            Destroy(billboard);
        }

        Instance.billboardsObject.Clear();
        Instance.billboardsTime.Clear();
        Instance.billboardsItemTemplateIndex.Clear();
    }

    public static void DestroyLightSources_OnTransition(PlayerEnterExit.TransitionEventArgs args)
    {
        DestroyLightSources();
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

    [FullSerializer.fsObject("v1")]
    public class DroppedTorchData
    {
        public Vector3 position;
        public float time;
        public int itemTemplateIndex;
    }

    public class HandheldTorchesSaveData : IHasModSaveData
    {
        public List<DroppedTorchData> DroppedTorches;

        public Type SaveDataType
        {
            get
            {
                return typeof(HandheldTorchesSaveData);
            }
        }

        public object NewSaveData()
        {
            HandheldTorchesSaveData emptyData = new HandheldTorchesSaveData();
            emptyData.DroppedTorches = new List<DroppedTorchData>();
            return emptyData;
        }
        public object GetSaveData()
        {
            List<DroppedTorchData> dropEntries = new List<DroppedTorchData>();

            for (int i = 0; i < Instance.billboardsObject.Count; i++)
            {
                DroppedTorchData newData = new DroppedTorchData();
                newData.position = Instance.billboardsObject[i].transform.position;
                newData.time = Instance.billboardsTime[i];
                newData.itemTemplateIndex = Instance.billboardsItemTemplateIndex[i];
                dropEntries.Add(newData);
            }

            HandheldTorchesSaveData data = new HandheldTorchesSaveData();
            data.DroppedTorches = dropEntries;
            return data;
        }

        public void RestoreSaveData(object dataIn)
        {
            HandheldTorchesSaveData data = (HandheldTorchesSaveData)dataIn;
            List<DroppedTorchData> droppedTorches = data.DroppedTorches;

            foreach (DroppedTorchData torch in droppedTorches)
            {
                Instance.SpawnLightSource(torch.itemTemplateIndex, torch.position, torch.time);
            }
        }
    }
}
