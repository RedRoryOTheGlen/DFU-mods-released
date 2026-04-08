using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Banking;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallConnect.Arena2;
using Wenzil.Console;

namespace ComeSailAwayMod
{
    //[Serializable]
    public class Boat
    {
        public int hull;
        public int variant;
        public ulong uid;

        public bool packable = false;
        public bool crewed = false;

        public float modifierMoveSpeedOar;
        public float modifierMoveAccelerationOar;
        public float modifierMoveSpeedSail;
        public float modifierMoveAccelerationSail;

        public float modifierTurnSpeedOar;
        public float modifierTurnAccelerationOar;
        public float modifierTurnSpeedSail;
        public float modifierTurnAccelerationSail;

        public float modifierAnimation;
        public float modifierRudder;

        public float modifierAudioVolume;
        public float modifierAudioRange;
        public float modifierAudioSpatialBlend;

        public float modifierCargoThreshold;

        public GameObject GameObject;
        public GameObject MeshObject;
        public Vector3 MeshObjectOffset;
        public MeshCollider MeshCollider;
        public GameObject FireObject;
        public GameObject BedObject;
        public List<GameObject> BoardTriggers = new List<GameObject>();
        public List<GameObject> DoorTriggers = new List<GameObject>();
        public GameObject DriveTrigger;
        public GameObject DrivePosition;
        public GameObject CargoTrigger;

        public GameObject StatusTrigger;
        public GameObject VariantTrigger;
        public GameObject VariantObject;
        public int GetVariantCount
        {
            get
            {
                return VariantObject.transform.childCount;
            }
        }

        public GameObject WakeObject;
        public ParticleSystem WakeEmitter;
        public ParticleSystem.MainModule WakeEmitterMain;

        public GameObject FlagObject;
        public ParticleSystem FlagEmitter;
        public ParticleSystem.MainModule FlagEmitterMain;

        public GameObject RudderObject;
        public Animator RudderAnimator;
        public List<ParticleSystem> RudderEmitters = new List<ParticleSystem>();

        public GameObject ActiveObject;
        public GameObject IdleObject;

        public GameObject[] Nodes = new GameObject[5];
        public int[] NodeTileMapIndices = new int[5];

        public List<Transform> Booms = new List<Transform>();
        public List<Transform> Sails = new List<Transform>();
        public List<Transform> SailsSquare = new List<Transform>();
        public List<Transform> SailsLateen = new List<Transform>();
        public List<Transform> SailsGaff = new List<Transform>();
        public List<Transform> SailsStay = new List<Transform>();
        public List<Transform> SailsSmall = new List<Transform>();
        public List<Transform> SailsLarge = new List<Transform>();

        public bool LightOn = false;
        public List<Light> Lights = new List<Light>();

        public DFPosition MapPixel;
        public Vector3 Position;
        public Vector3 Direction;

        public DaggerfallLoot Cargo;

        public DaggerfallAudioSource DFAudioSource;
        public AudioSource AudioSourceSlow;
        public AudioSource AudioSourceFast;

        public List<ParticleSystem> OarParticles = new List<ParticleSystem>();

        public Boat (int hullTarget = 0, int variantTarget = 0)
        {
            hull = hullTarget;
            variant = variantTarget;
        }
    }

    public class ComeSailAway : MonoBehaviour
    {
        private static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            mod.SaveDataInterface = new ComeSailAwaySaveData();

            var go = new GameObject(mod.Title);
            go.AddComponent<ComeSailAway>();

            mod.IsReady = true;
        }

        public const float terrainEdge = 819.2f;

        public static ComeSailAway Instance;

        GameObject playerObject;
        UserInterfaceManager uiManager;
        Boat variantBoatTarget;

        LayerMask layerMask;

        public bool TemporaryShip = false;

        //[SerializeField]
        public Boat CurrentBoat;
        public List<Boat> AllBoats = new List<Boat>();

        float wakeThreshold
        {
            get
            {
                return moveSpeedSail * 0.25f;
            }
        }

        public const uint firstHullModelID = 112410;
        public float sailAnimationSpeed = 2;

        bool wave = false;
        public int waveDistance = 2;
        float waveLength = 1;
        float waveFade = 0.5f;
        GameObject waveObject;
        MeshRenderer waveMeshRenderer;
        MeshFilter waveMeshFilter;
        Mesh waveMesh;

        bool[,] currentNeighbors;
        Vector3 currentVector;
        Vector3 currentVectorPrevious;

        public float waveFrameTime = 0.1f;
        float waveFrameTimer = 0;
        List<Texture2D> waveFrames;
        int waveFrameIndex = 0;

        ParticleSystem rainEmitter;
        ParticleSystem snowEmitter;
        ParticleSystem.ForceOverLifetimeModule rainEmitterForceOverLifetime;
        ParticleSystem.ForceOverLifetimeModule snowEmitterForceOverLifetime;

        public bool placing;
        public float placeTime = 0;
        DaggerfallUnityItem placeItem;
        ItemCollection placeItemCollection;
        public int portSearchRange = 3;
        
        float WaterLevel
        {
            get
            {
                if (WODTerrain != null)
                    return 101;

                return 35;
            }
        }

        float moveSpeedOar = 2f;
        float moveSpeedSail = 2f;
        float moveAccelOar = 1f;
        float moveAccelSail = 0.2f;

        float turnSpeedOar = 20f;
        float turnSpeedSail = 10f;
        float turnAccelOar = 10f;
        float turnAccelSail = 5f;
        float moveSpeed
        {
            get
            {
                if (sailPosition == 0)
                    return moveSpeedOar * moveSpeedOarMod * boatCargoMod * CurrentBoat.modifierMoveSpeedOar;
                return moveSpeedSail * moveSpeedSailMod * boatCargoMod * CurrentBoat.modifierMoveSpeedSail;
            }
        }
        float moveAccel
        {
            get
            {
                if (sailPosition == 0 && (
                    InputManager.Instance.HasAction(InputManager.Actions.MoveForwards) ||
                    InputManager.Instance.HasAction(InputManager.Actions.MoveBackwards) ||
                    (InputManager.Instance.HasAction(InputManager.Actions.Run) && (InputManager.Instance.HasAction(InputManager.Actions.MoveRight) || InputManager.Instance.HasAction(InputManager.Actions.MoveLeft)))))
                    return moveAccelOar * moveAccelOarMod * boatCargoMod * CurrentBoat.modifierMoveAccelerationOar;
                else if (sailPosition == 1)
                    return moveAccelSail * moveAccelSailMod * windVectorCurrent.magnitude * boatCargoMod * CurrentBoat.modifierMoveAccelerationSail;
                else
                    return moveAccelSail * moveAccelSailMod * boatCargoMod * CurrentBoat.modifierMoveAccelerationSail;
            }
        }
        float turnSpeed
        {
            get
            {
                if (sailPosition == 0)
                    return turnSpeedOar * turnSpeedOarMod * boatCargoMod * CurrentBoat.modifierTurnSpeedOar;
                return turnSpeedSail * turnSpeedSailMod * boatCargoMod * CurrentBoat.modifierTurnSpeedSail;
            }
        }
        float turnAccel
        {
            get
            {
                if (sailPosition == 0 && (
                    InputManager.Instance.HasAction(InputManager.Actions.MoveRight) ||
                    InputManager.Instance.HasAction(InputManager.Actions.MoveLeft)))
                    return turnAccelOar * turnAccelOarMod * boatCargoMod * CurrentBoat.modifierTurnAccelerationOar;
                return turnAccelSail * turnAccelSailMod * boatCargoMod * CurrentBoat.modifierTurnAccelerationSail;
            }
        }

        public Vector3 windVectorTarget;
        public Vector3 windVectorCurrent;

        //0 = oars, 1 = quarter-sail, 2 = half-sail, 3 = full-sail
        public int sailPosition;

        public Vector3 lastBoatPosition = Vector3.zero;
        public Vector3 lastBoatDirection = Vector3.zero;

        public Vector3 MoveVectorTarget = Vector3.zero;
        public Vector3 MoveVectorCurrent = Vector3.zero;
        public float TurnTarget = 0;
        public float TurnCurrent = 0;

        public Vector3 velocityTarget;
        public Vector3 velocityCurrent;

        //sail control
        public float trimAngle;
        public float trimAngleSquare;
        bool trimAuto = true;    //0 = manual, 1 = automatic
        bool trimAutoSquareUpwind = true;
        //bool trimAutoGaffLargeSquare = true;

        public int nativeScreenWidth = 320;
        public int nativeScreenHeight = 200;
        public Rect screenRect;

        public int scaleToScreen;   //0 = Do not scale, 1 = Scale to screen height, 2 = Scale to screen dimensions
        public float screenScaleX;
        public float screenScaleY;

        //wind direction widget
        public bool windDirectionWidget = true;
        public Vector2 windDirectionWidgetOffset = Vector2.one * 0.5f;
        public float windDirectionWidgetScale = 1;
        public Color windDirectionWidgetColor = Color.white;
        public int windDirectionWidgetIntervalIndex = 7;
        Texture2D[] windDirectionWidgetTextures;
        Texture2D windDirectionWidgetTextureCurrent;
        Rect windDirectionWidgetRect;
        int[] windDirectionWidgetIntervals = new int[] { 1, 2, 3, 5, 6, 9, 10, 15, 18, 30, 45, 90 };    //factors of 90
        int windDirectionWidgetInterval
        {
            get { return windDirectionWidgetIntervals[windDirectionWidgetIntervalIndex]; }
        }
        int windDirectionWidgetFrameCount
        {
            get
            {
                return (int)(360 / windDirectionWidgetInterval);
            }
        }

        //cargo
        float boatCargoMod = 1;
        float boatCargoThreshold = 500;
        float lastWeight;
        bool cargoCountPlayerWeight;
        bool cargoCountPlayerCarriedWeight;
        bool cargoCountCartItemWeight;
        bool cargoCountCartCarriedWeight;
        bool cargoCountHorseItemWeight;

        //oar mode tracking
        public float oarModeTime = 1;
        float oarModeTimer = 0;

        //time acceleration
        int timeScaleIndex = 0;
        int[] currentTimeScale = new int[5] { 1, 5, 10, 15, 30 };

        //audio
        AudioClip[] audioClips = new AudioClip[2] {null,null};
        IEnumerator fading;

        //input smoothing
        Vector2 inputCurrent;
        bool HasInput
        {
            get
            {
                if (InputManager.Instance.Vertical != 0 || InputManager.Instance.Horizontal != 0 || InputManager.Instance.ToggleAutorun)
                    return true;

                return false;
            }
        }
        Vector2 inputTarget
        {
            get
            {
                Vector2 result = Vector2.zero;


                if (HasInput)
                {
                    if (InputManager.Instance.ToggleAutorun)
                        result = new Vector2(InputManager.Instance.Horizontal, 1);
                    else
                        result = new Vector2(InputManager.Instance.Horizontal, InputManager.Instance.Vertical);
                }

                return result;
            }
        }

        //settings
        KeyCode keyCodeDisembark = KeyCode.None;
        KeyCode keyCodeToggleSail = KeyCode.None;
        KeyCode keyCodeToggleLight = KeyCode.None;
        KeyCode keyCodeTimeScaleUp = KeyCode.None;
        KeyCode keyCodeTimeScaleDown = KeyCode.None;
        KeyCode keyCodeTimeScaleReset = KeyCode.None;
        KeyCode keyCodeTrimLeft = KeyCode.None;
        KeyCode keyCodeTrimRight = KeyCode.None;
        KeyCode keyCodeTrimMod = KeyCode.None;
        float sfxVolume = 1f;
        float moveSpeedOarMod = 1f;
        float moveAccelOarMod = 1f;
        float moveSpeedSailMod = 1f;
        float moveAccelSailMod = 1f;
        float turnSpeedOarMod = 1f;
        float turnAccelOarMod = 1f;
        float turnSpeedSailMod = 1f;
        float turnAccelSailMod = 1f;
        bool debugShow = false;

        //Mod Messages
        public event Action<Vector3> OnUpdateWind;
        public event Action<Vector3> OnUpdateCurrent;
        public event Action<bool> OnUpdateSailing;

        //mod compatibility
        Mod WODTerrain;

        Mod AnimatedWater;
        bool AWVertexWaves = false;
        bool DisableParticles
        {
            get
            {
                if (AnimatedWater != null && AWVertexWaves)
                    return true;

                return false;
            }
        }

        public bool IsSailing
        {
            get
            {
                if (CurrentBoat != null && disembarking == null)
                    return true;

                return false;
            }
        }

        IEnumerator disembarking;

        List<GameObject> parentedObjects = new List<GameObject>();

        //track cargo in packed boats
        public Dictionary<ulong, ItemCollection> PackedCargoes = new Dictionary<ulong, ItemCollection>();

        //set timescale properlty
        bool wasPaused = false;

        //collision tracking
        List<Vector3> collisionDirections = new List<Vector3>();
        Vector3 combinedCollisionDirection
        {
            get
            {
                if (collisionDirections.Count > 0)
                {
                    Vector3 collisionDirection = Vector3.zero;
                    foreach(Vector3 direction in collisionDirections)
                    {
                        collisionDirection += direction;
                    }
                    return collisionDirection;
                }

                return Vector3.zero;
            }
        }

        bool CanTurnLeft(Boat boat)
        {
            if (collisionDirections.Count > 0)
            {
                foreach (Vector3 direction in collisionDirections)
                {
                    float dotForward = Vector3.Dot(boat.GameObject.transform.forward, direction);
                    float dotRight = Vector3.Dot(boat.GameObject.transform.right, direction);
                    if ((dotForward >= 0 && dotRight < 0) || (dotForward < 0 && dotRight >= 0))
                        return false;
                }
            }

            return true;
        }


        bool CanTurnRight(Boat boat)
        {
            if (collisionDirections.Count > 0)
            {
                foreach (Vector3 direction in collisionDirections)
                {
                    float dotForward = Vector3.Dot(boat.GameObject.transform.forward, direction);
                    float dotRight = Vector3.Dot(boat.GameObject.transform.right, direction);
                    if ((dotForward >= 0 && dotRight >= 0) || (dotForward < 0 && dotRight < 0))
                        return false;
                }
            }

            return true;
        }

        public Boat GetPlacedBoatWithUID(ulong UID)
        {
            if (AllBoats.Count < 1)
                return null;

            Boat boat = null;

            foreach (Boat placedBoat in AllBoats)
            {
                if (placedBoat.uid == UID)
                {
                    boat = placedBoat;
                    break;
                }
            }

            return boat;
        }

        //reference to StreamingWorld's terrain array
        FieldInfo terrainArray;

        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("Controls"))
            {
                keyCodeDisembark = SetKeyFromText(settings.GetString("Controls", "Disembark"));
                keyCodeToggleSail = SetKeyFromText(settings.GetString("Controls", "ToggleSail"));
                keyCodeToggleLight = SetKeyFromText(settings.GetString("Controls", "ToggleLight"));
                keyCodeTimeScaleUp = SetKeyFromText(settings.GetString("Controls", "IncreaseTimeScale"));
                keyCodeTimeScaleDown = SetKeyFromText(settings.GetString("Controls", "DecreaseTimeScale"));
                keyCodeTimeScaleReset = SetKeyFromText(settings.GetString("Controls", "ResetTimeScale"));
                keyCodeTrimRight = SetKeyFromText(settings.GetString("Controls", "TrimRight"));
                keyCodeTrimLeft = SetKeyFromText(settings.GetString("Controls", "TrimLeft"));
                keyCodeTrimMod = SetKeyFromText(settings.GetString("Controls", "TrimModifier"));
                portSearchRange = settings.GetInt("Controls", "PortLocationSearchRange");
            }
            if (change.HasChanged("WindDirectionWidget"))
            {
                windDirectionWidget = settings.GetBool("WindDirectionWidget", "Enable");
                windDirectionWidgetOffset = new Vector2(settings.GetTupleFloat("WindDirectionWidget", "Position").First, settings.GetTupleFloat("WindDirectionWidget", "Position").Second);
                windDirectionWidgetScale = settings.GetFloat("WindDirectionWidget", "Scale");
                scaleToScreen = settings.GetInt("WindDirectionWidget", "ScalingMode");
                windDirectionWidgetColor = settings.GetColor("WindDirectionWidget", "Color");
            }
            if (change.HasChanged("Waves"))
            {
                wave = settings.GetBool("Waves", "Enable");
                waveDistance = settings.GetInt("Waves", "Distance");
                waveLength = settings.GetFloat("Waves", "Length") * 0.5f;
                waveFade = settings.GetFloat("Waves", "Fade") * waveLength;
                waveFrameTime = (2f-(settings.GetInt("Waves", "Speed")/100)) * 0.125f;

                if (waveMeshRenderer != null)
                {
                    waveMeshRenderer.material.SetFloat("_DitherStart", waveFade);
                    waveMeshRenderer.material.SetFloat("_DitherEnd", waveLength);
                }
            }
            if (change.HasChanged("Audio"))
            {
                sfxVolume = settings.GetFloat("Audio", "SoundVolume");
                UpdateAudioSource();
            }
            if (change.HasChanged("Handling"))
            {
                moveSpeedOarMod = settings.GetFloat("Handling", "OarMoveSpeed");
                moveAccelOarMod = settings.GetFloat("Handling", "OarMoveAcceleration");
                turnSpeedOarMod = settings.GetFloat("Handling", "OarTurnSpeed");
                turnAccelOarMod = settings.GetFloat("Handling", "OarTurnAcceleration");
                moveSpeedSailMod = settings.GetFloat("Handling", "SailMoveSpeed");
                moveAccelSailMod = settings.GetFloat("Handling", "SailMoveAcceleration");
                turnSpeedSailMod = settings.GetFloat("Handling", "SailTurnSpeed");
                turnAccelSailMod = settings.GetFloat("Handling", "SailTurnAcceleration");
            }
            if (change.HasChanged("Cargo"))
            {
                boatCargoThreshold = (float)settings.GetInt("Cargo", "CargoThreshold");
                cargoCountPlayerCarriedWeight = settings.GetBool("Cargo", "PlayerCarriedWeight");
                cargoCountPlayerWeight = settings.GetBool("Cargo", "PlayerWeight");
                cargoCountCartCarriedWeight = settings.GetBool("Cargo", "CartCarriedWeight");
                cargoCountCartItemWeight = settings.GetBool("Cargo", "CartItem");
                cargoCountHorseItemWeight = settings.GetBool("Cargo", "HorseItem");
            }
            if (change.HasChanged("SailingAssist"))
            {
                trimAuto = settings.GetBool("SailingAssist", "AutoTrimming");
                trimAutoSquareUpwind = settings.GetBool("SailingAssist", "AutoStowSquareSails");
                //trimAutoGaffLargeSquare = settings.GetBool("SailingAssist", "AutoStowGaffSails");
            }
            if (change.HasChanged("Compatibility"))
            {
                AWVertexWaves = settings.GetBool("Compatibility", "AnimatedWaterVertexWaves");
            }
            if (change.HasChanged("Debug"))
            {
                debugShow = settings.GetBool("Debug", "ShowValues");
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

        private void Awake()
        {
            if (Instance == null)
                Instance = this;

            mod.MessageReceiver = MessageReceiver;
        }

        private void Start()
        {

            WODTerrain = ModManager.Instance.GetModFromGUID("a9091dd7-e07a-4171-b16d-d13d67a5f221");
            AnimatedWater = ModManager.Instance.GetMod("Animated Water");

            playerObject = GameManager.Instance.PlayerObject;

            uiManager = DaggerfallUI.Instance.UserInterfaceManager;

            layerMask = ~(1 << LayerMask.NameToLayer("Player"));
            layerMask = layerMask & ~(1 << LayerMask.NameToLayer("Ignore Raycast"));

            /*boatMeshOffset = new GameObject("BoatMeshOffset");
            boatMeshOffset.transform.SetParent(boatObject.transform);*/

            PlayerActivate.RegisterCustomActivation(mod, 112400, ActivateRudder);
            PlayerActivate.RegisterCustomActivation(mod, 112401, BoardBoat);
            PlayerActivate.RegisterCustomActivation(mod, 112402, OpenBoatCargo);
            PlayerActivate.RegisterCustomActivation(mod, 112403, TriggerDoor);
            PlayerActivate.RegisterCustomActivation(mod, 112404, PickVariant);
            PlayerActivate.RegisterCustomActivation(mod, 112405, CheckBoatStatus);

            //audio clips
            audioClips = new AudioClip[5]
            {
                mod.GetAsset<AudioClip>("SmallShipAmbience.ogg"),
                mod.GetAsset<AudioClip>("ShipExteriorAmbience2.ogg"),
                mod.GetAsset<AudioClip>("Oars_In.wav"),
                mod.GetAsset<AudioClip>("Oars_Sweep.wav"),
                mod.GetAsset<AudioClip>("Oars_Out.wav"),
            };

            waveObject = new GameObject("WaveObject");
            if (WODTerrain != null)
                waveObject.transform.localPosition = (Vector3.forward * (terrainEdge / 2)) + (Vector3.right * (terrainEdge / 2)) + (Vector3.up * 100);
            else
                waveObject.transform.localPosition = (Vector3.forward * (terrainEdge / 2)) + (Vector3.right * (terrainEdge / 2)) + (Vector3.up * 34);
            waveObject.transform.localScale = Vector3.one * terrainEdge;
            waveMeshRenderer = waveObject.AddComponent<MeshRenderer>();
            waveMeshFilter = waveObject.AddComponent<MeshFilter>();
            waveMeshRenderer.material = mod.GetAsset<Material>("CurrentMaterial.mat");
            waveMesh = new Mesh();
            waveMeshFilter.mesh = waveMesh;
            InitializeWaveTextures();

            rainEmitter = GameManager.Instance.WeatherManager.PlayerWeather.RainParticles.GetComponent<ParticleSystem>();
            rainEmitterForceOverLifetime = rainEmitter.forceOverLifetime;
            rainEmitterForceOverLifetime.space = ParticleSystemSimulationSpace.World;

            snowEmitter = GameManager.Instance.WeatherManager.PlayerWeather.SnowParticles.GetComponent<ParticleSystem>();
            snowEmitterForceOverLifetime = snowEmitter.forceOverLifetime;
            snowEmitterForceOverLifetime.space = ParticleSystemSimulationSpace.World;

            PlayerActivate.OnLootSpawned += OnLootSpawned;
            FloatingOrigin.OnPositionUpdate += OnPositionUpdate;
            WorldTime.OnNewHour += OnNewHour;
            WeatherManager.OnWeatherChange += OnWeatherChange;
            PlayerEnterExit.OnTransitionInterior += OnTransition;
            PlayerEnterExit.OnTransitionExterior += OnTransition;
            PlayerEnterExit.OnTransitionDungeonInterior += OnTransition;
            PlayerEnterExit.OnTransitionDungeonExterior += OnTransition;
            DaggerfallTravelPopUp.OnPostFastTravel += OnPostFastTravel;
            DaggerfallTravelPopUp.OnPreFastTravel += OnPreFastTravel;
            EntityEffectBroker.OnNewMagicRound += OnNewMagicRound;
            SaveLoadManager.OnLoad += OnLoad;

            SaveLoadManager.OnStartLoad += OnStartLoad;

            GameManager.Instance.PlayerEntity.OnDeath += OnPlayerDeath;
            GameManager.Instance.PlayerEntity.OnExhausted += OnPlayerDeath;

            //pick an entirely random direction
            float angle = 15 * UnityEngine.Random.Range(-12, 12);
            windVectorTarget = (Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward).normalized;
            windVectorCurrent = windVectorTarget;

            //initialize windDirectionWidget textures
            windDirectionWidgetTextures = new Texture2D[windDirectionWidgetFrameCount];
            int archive = 112395;
            int record = 1;
            int frame = 0;
            for (int i = 0; i < windDirectionWidgetFrameCount; i++)
            {
                Texture2D texture;
                DaggerfallWorkshop.Utility.AssetInjection.TextureReplacement.TryImportTexture(archive, record, frame, out texture);
                windDirectionWidgetTextures[i] = texture;
                frame++;
            }
            windDirectionWidgetTextureCurrent = windDirectionWidgetTextures[0];

            DaggerfallUnity.Instance.ItemHelper.RegisterCustomItem(ItemBoatParts.templateIndex, ItemGroups.UselessItems2, typeof(ItemBoatParts));
            DaggerfallUnity.Instance.ItemHelper.RegisterCustomItem(ItemBoatDeed.templateIndex, ItemGroups.UselessItems2, typeof(ItemBoatDeed));

            //register console commands
            ConsoleCommandsDatabase.RegisterCommand(GiveMeBoat.name, GiveMeBoat.description, GiveMeBoat.usage, GiveMeBoat.Execute);
            ConsoleCommandsDatabase.RegisterCommand(PlaceBoatAtMe.name, PlaceBoatAtMe.description, PlaceBoatAtMe.usage, PlaceBoatAtMe.Execute);

            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();

            //get reference to terrain array
            terrainArray = GameManager.Instance.StreamingWorld.GetType().GetField("terrainArray", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.Instance);
            if (terrainArray != null)
            {
                Debug.Log("COME SAIL AWAY - FOUND TERRAIN ARRAY FIELD");
                //terrainArray.SetValue(GameManager.Instance.StreamingWorld, new DaggerfallWorkshop.StreamingWorld.TerrainDesc[1089]);
            }

            //Finish setup
        }
        public bool IsNearPort(int range = 3)
        {
            bool result = false;

            DFPosition position = GameManager.Instance.PlayerGPS.CurrentMapPixel;

            for (int x = position.X - range; x < position.X + range - 1; x++)
            {
                for (int y = position.Y - range; y < position.Y + range - 1; y++)
                {
                    ContentReader.MapSummary mapSummary;
                    bool hasLocation = DaggerfallUnity.Instance.ContentReader.HasLocation(x, y, out mapSummary);
                    if (hasLocation)
                    {
                        DaggerfallConnect.DFLocation location;
                        if (DaggerfallUnity.Instance.ContentReader.GetLocation(mapSummary.RegionIndex, mapSummary.MapIndex, out location))
                        {
                            if (location.Exterior.ExteriorData.PortTownAndUnknown != 0)
                            {
                                result = true;
                                break;
                            }
                        }
                    }

                }
            }

            return result;
        }

        public void SpawnBoat(Boat newBoat)
        {
            newBoat.GameObject = new GameObject("Boat");
            newBoat.GameObject.transform.position = Vector3.zero;

            GameObject boatGameObject = MeshReplacement.ImportCustomGameobject(firstHullModelID+(uint)(newBoat.hull), newBoat.GameObject.transform, GameManager.Instance.PlayerObject.transform.localToWorldMatrix);
            boatGameObject.transform.localPosition = Vector3.zero;
            boatGameObject.transform.localRotation = Quaternion.identity;

            //set variant here

            //0 = Fore, 1 = Aft, 2, Starboard, 3 = Port
            newBoat.MeshCollider = boatGameObject.GetComponentInChildren<MeshCollider>();
            newBoat.MeshObject = newBoat.MeshCollider.gameObject;
            newBoat.MeshObjectOffset = newBoat.MeshObject.transform.localPosition;
            Bounds meshBounds = newBoat.MeshCollider.bounds;
            GameObject boatNodeCenter = new GameObject("Center");
            newBoat.Nodes[0] = boatNodeCenter;
            newBoat.NodeTileMapIndices[0] = -1;
            boatNodeCenter.transform.SetParent(newBoat.GameObject.transform);
            boatNodeCenter.transform.localPosition = new Vector3(
                meshBounds.center.x,
                meshBounds.center.y,
                meshBounds.center.z
                );
            GameObject boatNodeFore = new GameObject("Fore");
            newBoat.Nodes[1] = boatNodeFore;
            newBoat.NodeTileMapIndices[1] = -1;
            boatNodeFore.transform.SetParent(newBoat.GameObject.transform);
            boatNodeFore.transform.localPosition = new Vector3(
                meshBounds.center.x,
                meshBounds.center.y,
                meshBounds.center.z + meshBounds.extents.z
                );
            GameObject boatNodeAft = new GameObject("Aft");
            newBoat.Nodes[2] = boatNodeAft;
            newBoat.NodeTileMapIndices[2] = -1;
            boatNodeAft.transform.SetParent(newBoat.GameObject.transform);
            boatNodeAft.transform.localPosition = new Vector3(
                meshBounds.center.x,
                meshBounds.center.y,
                meshBounds.center.z - meshBounds.extents.z
                );
            GameObject boatNodeStarboard = new GameObject("Starboard");
            newBoat.Nodes[3] = boatNodeStarboard;
            newBoat.NodeTileMapIndices[3] = -1;
            boatNodeStarboard.transform.SetParent(newBoat.GameObject.transform);
            boatNodeStarboard.transform.localPosition = new Vector3(
                meshBounds.center.x + meshBounds.extents.x,
                meshBounds.center.y,
                meshBounds.center.z
                );
            GameObject boatNodePort = new GameObject("Port");
            newBoat.Nodes[4] = boatNodePort;
            newBoat.NodeTileMapIndices[4] = -1;
            boatNodePort.transform.SetParent(newBoat.GameObject.transform);
            boatNodePort.transform.localPosition = new Vector3(
                meshBounds.center.x - meshBounds.extents.x,
                meshBounds.center.y,
                meshBounds.center.z
                );

            //search boatMesh hierarchy for transforms
            GetBoatTransforms(newBoat, newBoat.GameObject.transform);

            if (newBoat.Sails.Count > 0)
            {
                foreach (Transform sail in newBoat.Sails)
                {
                    Animator animator = sail.GetComponent<Animator>();
                    if (animator != null)
                    {
                        animator.CrossFade("Stowed", sailAnimationSpeed);
                        animator.SetBool("Stowed", true);
                    }
                }
            }

            newBoat.IdleObject.gameObject.SetActive(true);
            newBoat.ActiveObject.gameObject.SetActive(false);

            SetLights(newBoat, newBoat.LightOn);

            //audio source
            GameObject audioSourceSlowObject = new GameObject("BoatSFXSlow");
            audioSourceSlowObject.transform.SetParent(newBoat.GameObject.transform);
            audioSourceSlowObject.transform.localPosition = Vector3.zero;
            newBoat.AudioSourceSlow = audioSourceSlowObject.AddComponent<AudioSource>();
            newBoat.AudioSourceSlow.clip = audioClips[0];
            newBoat.AudioSourceSlow.loop = true;
            //newBoat.AudioSourceSlow.volume = newBoat.modifierAudioVolume * DaggerfallUnity.Settings.SoundVolume;
            newBoat.AudioSourceSlow.spatialBlend = 1;
            newBoat.AudioSourceSlow.minDistance = newBoat.MeshCollider.sharedMesh.bounds.extents.z * 0.5f;
            newBoat.AudioSourceSlow.maxDistance = newBoat.AudioSourceSlow.minDistance * 2;

            GameObject audioSourceFastObject = new GameObject("BoatSFXFast");
            audioSourceFastObject.transform.SetParent(newBoat.GameObject.transform);
            audioSourceFastObject.transform.localPosition = Vector3.zero;
            newBoat.AudioSourceFast = audioSourceFastObject.AddComponent<AudioSource>();
            newBoat.AudioSourceFast.clip = audioClips[1];
            newBoat.AudioSourceFast.loop = true;
            //newBoat.AudioSourceFast.volume = newBoat.modifierAudioVolume * DaggerfallUnity.Settings.SoundVolume;
            newBoat.AudioSourceFast.spatialBlend = 1;
            newBoat.AudioSourceFast.minDistance = newBoat.MeshCollider.sharedMesh.bounds.extents.z * 0.5f;
            newBoat.AudioSourceFast.maxDistance = newBoat.AudioSourceFast.minDistance * 2;

            GameObject dfAudioSourceObject = new GameObject("BoatSFXOneShot");
            dfAudioSourceObject.transform.SetParent(newBoat.GameObject.transform);
            dfAudioSourceObject.transform.localPosition = Vector3.zero;
            AudioSource dfAudioSourceSource = dfAudioSourceObject.AddComponent<AudioSource>();
            newBoat.DFAudioSource = dfAudioSourceObject.AddComponent<DaggerfallAudioSource>();
            //dfAudioSourceSource.volume = newBoat.modifierAudioVolume * DaggerfallUnity.Settings.SoundVolume;

            GameObject boatCargoObject = new GameObject("BoatCargo");
            boatCargoObject.transform.SetParent(newBoat.GameObject.transform);
            boatCargoObject.transform.localPosition = Vector3.zero;
            newBoat.Cargo = boatCargoObject.AddComponent<DaggerfallLoot>();
            newBoat.Cargo.playerOwned = true;
        }

        void ApplyBoatVariant(Boat boat)
        {
            if (boat.VariantObject == null)
                return;

            for (int i = 0; i < boat.GetVariantCount; i++)
            {
                if (i == boat.variant)
                    boat.VariantObject.transform.GetChild(i).gameObject.SetActive(true);
                else
                    boat.VariantObject.transform.GetChild(i).gameObject.SetActive(false);
            }

            //TO-DO: reset transforms
            ReinitializeBoat(boat);
        }

        void ReinitializeBoat(Boat boat)
        {
            boat.Sails.Clear();
            boat.SailsGaff.Clear();
            boat.SailsLarge.Clear();
            boat.SailsLateen.Clear();
            boat.SailsSmall.Clear();
            boat.SailsSquare.Clear();
            boat.SailsStay.Clear();
            boat.Booms.Clear();
            boat.FlagObject = null;
            GetBoatTransforms(boat, boat.GameObject.transform, true);
        }

        void SetBoatVariant(Boat boat, int variant)
        {
            boat.variant = variant;

            ApplyBoatVariant(boat);
        }

        void OpenBoatVariantPicker(Boat boat)
        {
            if (boat.VariantObject == null || boat.GetVariantCount < 1)
            {
                DaggerfallUI.SetMidScreenText("This boat has no variants");
                return;
            }

            if (!IsNearPort(ComeSailAway.Instance.portSearchRange))
            {
                DaggerfallUI.SetMidScreenText("There is no port nearby");
                return;
            }

            variantBoatTarget = boat;

            DaggerfallListPickerWindow variantPicker = new DaggerfallListPickerWindow(uiManager, uiManager.TopWindow);
            variantPicker.OnItemPicked += OpenBoatVariantPicker_OnItemPicked;

            for (int i = 0; i < boat.GetVariantCount; i++)
            {
                variantPicker.ListBox.AddItem(i.ToString());
            }
            uiManager.PushWindow(variantPicker);
        }


        public void OpenBoatVariantPicker_OnItemPicked(int index, string itemName)
        {
            DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
            DaggerfallUI.UIManager.PopWindow();

            //somehow get the index here
            SetBoatVariant(variantBoatTarget, index);

            variantBoatTarget = null;
        }

        public const char archiveStart = '-';
        public const char recordStart = '_';
        public const char alignmentStart = ':';

        void SetupBillboardHelper(Boat boat, GameObject parent)
        {
            //get archive and record from name here
            string name = parent.name;

            int archive = Convert.ToInt32(name.Substring(name.IndexOf(archiveStart) + 1, 3));
            int record = Convert.ToInt32(name.Substring(name.IndexOf(recordStart) + 1, 3));
            int alignment = Convert.ToInt32(name.Substring(name.IndexOf(alignmentStart) + 1, 1));

            //Debug.Log("COME SAIL AWAY - BILLBOARD HELPER ARCHIVE AND RECORD IS " + archive.ToString() + "_" + record.ToString());

            GameObject billboardObject = GameObjectHelper.CreateDaggerfallBillboardGameObject(archive, record, parent.transform);
            billboardObject.transform.localPosition = Vector3.zero;
            billboardObject.transform.localScale = Vector3.one;

            DaggerfallBillboard billboard = billboardObject.GetComponent<DaggerfallBillboard>();
            switch (alignment)
            {
                case 1: //bottom
                    billboardObject.transform.localPosition += (Vector3.up * (billboard.Summary.Size.y/2));
                    break;

                case 2: //top
                    billboardObject.transform.localPosition -= (Vector3.up * (billboard.Summary.Size.y / 2));
                    break;
            }

            //is a light
            if (archive == 210)
            {
                boat.Lights.Add(AddBillboardLight(billboard));
                //add light to boat list?
            }
        }

        Light AddBillboardLight(DaggerfallBillboard billboard, int alignment = 0)
        {
            GameObject go = GameObjectHelper.InstantiatePrefab(DaggerfallUnity.Instance.Option_DungeonLightPrefab.gameObject, string.Empty, billboard.transform, billboard.transform.position);
            Light light = go.GetComponent<Light>();
            Color32 fireColor = new Color32(255, 147, 41, 255);
            if (light != null)
            {
                light.color = fireColor;
                light.intensity = 1;
                light.range = 20;
                light.type = LightType.Point;
                light.shadows = LightShadows.Hard;
                light.shadowStrength = 1f;
                light.spotAngle = 140;
            }

            switch (alignment)
            {
                case 1: //bottom
                    go.transform.localPosition += (Vector3.up * (billboard.Summary.Size.y / 2));
                    break;

                case 2: //top
                    go.transform.localPosition -= (Vector3.up * (billboard.Summary.Size.y / 2));
                    break;
            }

            return light;
        }

        void SetupModelHelper(Boat boat, GameObject parent)
        {
            //get archive and record from name here
            string name = parent.name;

            uint modelID = (uint)Convert.ToInt32(name.Substring(name.IndexOf(archiveStart) + 1, 5));
            int alignment = Convert.ToInt32(name.Substring(name.IndexOf(alignmentStart) + 1, 1));

            //Debug.Log("COME SAIL AWAY - BILLBOARD HELPER ARCHIVE AND RECORD IS " + archive.ToString() + "_" + record.ToString());

            GameObject meshObject = GameObjectHelper.CreateDaggerfallMeshGameObject(modelID,parent.transform);
            meshObject.transform.localPosition = Vector3.zero;
            meshObject.transform.localScale = Vector3.one;

            MeshRenderer meshRenderer = meshObject.GetComponent<MeshRenderer>();
            Vector3 offset = meshObject.transform.position-meshRenderer.bounds.center;
            meshObject.transform.localPosition += offset;
            switch (alignment)
            {
                case 1: //bottom
                    meshObject.transform.localPosition += (Vector3.up * (meshRenderer.bounds.extents.y));
                    break;

                case 2: //top
                    meshObject.transform.localPosition -= (Vector3.up * (meshRenderer.bounds.extents.y));
                    break;
            }
        }

        public void SetLights(Boat boat, bool value)
        {
            if (boat.Lights.Count < 1)
                return;

            boat.LightOn = value;

            foreach(Light light in boat.Lights)
            {
                light.enabled = value;
                light.GetComponent<DaggerfallLight>().enabled = value;
                light.GetComponent<DungeonLightHandler>().enabled = value;

                //billboard stuff
                //disable emission?
                Renderer renderer = light.transform.parent.GetComponent<Renderer>();
                if (value)
                {
                    renderer.material.SetColor("_EmissionColor", Color.white);
                }
                else
                {
                    renderer.material.SetColor("_EmissionColor", Color.black);
                }
            }
        }

        void GetBoatTransforms(Boat boat, Transform parent, bool reinitialize = false)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);

                //skip inactive children, for testing
                if (!child.gameObject.activeSelf)
                    continue;

                if (boat.VariantObject == null && child.name == "Variants")
                {
                    boat.VariantObject = child.gameObject;
                    boat.VariantObject.transform.GetChild(boat.variant).gameObject.SetActive(true);
                }

                if (child.name.Contains("BillboardHelper") && !reinitialize)
                {
                    SetupBillboardHelper(boat, child.gameObject);
                }

                if (child.name.Contains("ModelHelper") && !reinitialize)
                {
                    SetupModelHelper(boat, child.gameObject);
                }

                if (child.name == "Crewed")
                {
                    boat.crewed = true;
                }

                if (child.name == "Packable")
                {
                    boat.packable = true;
                }

                if (child.name.Contains("Handling"))
                {
                    if (child.name.Contains("Oar"))
                    {
                        boat.modifierMoveSpeedOar = child.localPosition.x;
                        boat.modifierMoveAccelerationOar = child.localPosition.y;
                        boat.modifierTurnSpeedOar = child.localScale.x;
                        boat.modifierTurnAccelerationOar = child.localScale.y;
                    }
                    if (child.name.Contains("Sail"))
                    {
                        boat.modifierMoveSpeedSail = child.localPosition.x;
                        boat.modifierMoveAccelerationSail = child.localPosition.y;
                        boat.modifierTurnSpeedSail = child.localScale.x;
                        boat.modifierTurnAccelerationSail = child.localScale.y;
                    }
                    if (child.name.Contains("Rudder"))
                    {
                        boat.modifierRudder = child.localPosition.x;
                    }
                    if (child.name.Contains("Animation"))
                    {
                        boat.modifierAnimation = child.localPosition.x;
                    }
                }

                if (child.name == ("Cargo"))
                {
                    boat.modifierCargoThreshold = child.localPosition.x;
                }

                if (child.name.Contains("Audio"))
                {
                    boat.modifierAudioVolume = 1;
                    //boat.modifierAudioVolume = child.localPosition.x;
                    boat.modifierAudioRange = child.localPosition.y;
                    boat.modifierAudioSpatialBlend = child.localPosition.z;
                }

                if (boat.FlagObject == null && child.name == "FlagObject")
                {
                    boat.FlagObject = child.gameObject;
                    boat.FlagEmitter = boat.FlagObject.GetComponentInChildren<ParticleSystem>();
                    boat.FlagEmitterMain = boat.FlagEmitter.main;
                }

                if (boat.WakeObject == null && child.name == "WakeObject")
                {
                    boat.WakeObject = child.gameObject;
                    boat.WakeEmitter = boat.WakeObject.GetComponent<ParticleSystem>();
                    boat.WakeEmitterMain = boat.WakeEmitter.main;
                }

                if (boat.DriveTrigger == null && child.name == "DriveTrigger")
                {
                    //setup driving trigger
                    boat.DriveTrigger = MeshReplacement.ImportCustomGameobject(112400, child, GameManager.Instance.PlayerObject.transform.localToWorldMatrix);
                    boat.DriveTrigger.transform.localPosition = Vector3.zero;
                    boat.DriveTrigger.transform.localRotation = Quaternion.identity;
                }

                if (boat.CargoTrigger == null && child.name == "CargoTrigger")
                {
                    //setup cargo trigger
                    boat.CargoTrigger = MeshReplacement.ImportCustomGameobject(112402, child, GameManager.Instance.PlayerObject.transform.localToWorldMatrix);
                    boat.CargoTrigger.transform.localPosition = Vector3.zero;
                    boat.CargoTrigger.transform.localRotation = Quaternion.identity;
                }

                if (child.name == "BoardTrigger" && !reinitialize)
                {
                    //setup boarding trigger
                    GameObject trigger = MeshReplacement.ImportCustomGameobject(112401, child, GameManager.Instance.PlayerObject.transform.localToWorldMatrix);
                    trigger.transform.localPosition = Vector3.zero;
                    trigger.transform.localRotation = Quaternion.identity;
                    trigger.transform.localScale = Vector3.one;
                    boat.BoardTriggers.Add(trigger);
                }

                if (child.name == "DoorTrigger" && !reinitialize)
                {
                    //setup door trigger
                    GameObject trigger = MeshReplacement.ImportCustomGameobject(112403, child.parent, GameManager.Instance.PlayerObject.transform.localToWorldMatrix);
                    trigger.transform.localPosition = Vector3.zero;
                    trigger.transform.localRotation = Quaternion.identity;
                    MeshCollider colliderMesh = child.parent.GetComponent<MeshCollider>();
                    BoxCollider colliderBox = trigger.GetComponent<BoxCollider>();
                    colliderBox.center = colliderBox.transform.InverseTransformPoint(colliderMesh.bounds.center);
                    colliderBox.size = colliderMesh.bounds.size + (Vector3.one * 0.01f);
                    boat.BoardTriggers.Add(trigger);
                }

                if (boat.BedObject == null && child.name.Contains("BedObject"))
                {
                    //setup bed
                    boat.BedObject = GameObjectHelper.CreateDaggerfallMeshGameObject(41000, child);
                    boat.BedObject.transform.localPosition = Vector3.zero;
                    boat.BedObject.transform.localRotation = Quaternion.identity;

                    if (child.name.Contains("0"))
                    {
                        //hide bed model
                        boat.BedObject.GetComponent<MeshRenderer>().enabled = false;

                        //make bed non-collideable
                        MeshCollider collider = boat.BedObject.GetComponent<MeshCollider>();
                        collider.convex = true;
                        collider.isTrigger = true;
                    }
                }

                if (boat.FireObject == null && child.name == "FireObject")
                {
                    //setup campfire for C&C
                    boat.FireObject = GameObjectHelper.CreateDaggerfallBillboardGameObject(210, 1, child);
                    boat.FireObject.transform.localScale = Vector3.one * 0.5f;
                    boat.FireObject.transform.localPosition = Vector3.zero + (Vector3.up * (boat.FireObject.GetComponent<DaggerfallBillboard>().Summary.Size.y / 4));

                    boat.FireObject.GetComponent<MeshRenderer>().enabled = false;
                }

                if (boat.VariantTrigger == null && child.name == "VariantTrigger")
                {
                    //setup hull config trigger
                    boat.VariantTrigger = MeshReplacement.ImportCustomGameobject(112404, child, GameManager.Instance.PlayerObject.transform.localToWorldMatrix);
                    boat.VariantTrigger.transform.localPosition = Vector3.zero;
                    boat.VariantTrigger.transform.localScale = Vector3.one;
                }

                if (boat.StatusTrigger == null && child.name == "StatusTrigger")
                {
                    //setup status trigger
                    boat.StatusTrigger = MeshReplacement.ImportCustomGameobject(112405, child, GameManager.Instance.PlayerObject.transform.localToWorldMatrix);
                    boat.StatusTrigger.transform.localPosition = Vector3.zero;
                    boat.StatusTrigger.transform.localScale = Vector3.one;
                }

                if (boat.DrivePosition == null && child.name == "DrivePosition")
                {
                    boat.DrivePosition = child.gameObject;
                }

                if (boat.RudderObject == null && child.name == "RudderObject")
                {
                    boat.RudderObject = child.gameObject;
                    boat.RudderAnimator = child.gameObject.GetComponent<Animator>();
                    boat.RudderObject.AddComponent<RudderAnimationEventListener>();
                }

                if (child.name == "RudderEffect" && !reinitialize)
                {
                    boat.RudderEmitters.Add(child.GetComponent<ParticleSystem>());
                }

                if (child.name == "OarEffect" && !reinitialize)
                {
                    boat.OarParticles.Add(child.GetComponent<ParticleSystem>());
                }

                if (boat.IdleObject == null && child.name == "IdleObject")
                    boat.IdleObject = child.gameObject;

                if (boat.ActiveObject == null && child.name == "ActiveObject")
                    boat.ActiveObject = child.gameObject;

                if (child.name.Contains("Boom"))
                    boat.Booms.Add(child);

                if (child.name.Contains("Sail") && !child.name.Contains("Skelly") && !child.name.Contains("Mesh") && !child.name.Contains("Bones") && !child.name.Contains("Handling"))
                {
                    boat.Sails.Add(child);

                    if (child.name.Contains("Small"))
                        boat.SailsSmall.Add(child);
                    else if (child.name.Contains("Large"))
                        boat.SailsLarge.Add(child);

                    if (child.name.Contains("Square"))
                        boat.SailsSquare.Add(child);
                    else if (child.name.Contains("Lateen"))
                        boat.SailsLateen.Add(child);
                    else if (child.name.Contains("Gaff"))
                        boat.SailsGaff.Add(child);
                    else if (child.name.Contains("Stay"))
                        boat.SailsStay.Add(child);
                }


                if (child.gameObject.GetComponent<SkinnedMeshRenderer>())
                {
                    if (child.gameObject.GetComponent<ApplyGameTextures>() == null)
                    {
                        child.gameObject.AddComponent<ApplyGameTextures>();
                        GameObject fixer = new GameObject();
                        fixer.transform.parent = child;
                        fixer.transform.localPosition = Vector3.zero;
                        fixer.transform.localRotation = Quaternion.identity;
                        fixer.AddComponent<FixDeformations>();
                    }
                }

                GetBoatTransforms(boat, child, reinitialize);
            }
        }

        void InitializeWaveTextures()
        {
            //Debug.Log("COME SAIL AWAY - INITIALIZING TEXTURES!");

            waveFrames = new List<Texture2D>();
            for (int i = 0; i < 99; i++)
            {
                Texture2D texture;
                bool valid = DaggerfallWorkshop.Utility.AssetInjection.TextureReplacement.TryImportTexture(112395, 2, i, out texture);
                if (valid)
                    waveFrames.Add(texture);
                else
                    break;
            }

            //Debug.Log("COME SAIL AWAY - FOUND " + waveFrames.Count.ToString() + " TEXTURES!");
        }

        public void RunOnUpdateEvents()
        {
            if (OnUpdateWind != null)
            {
                OnUpdateWind(windVectorCurrent);
            }

            currentVectorPrevious = Vector3.zero;
        }

        void MessageReceiver(string message, object data, DFModMessageCallback callBack)
        {
            switch (message)
            {
                case "GetWind":
                    callBack?.Invoke("GetWind", windVectorCurrent);
                    break;

                case "GetCurrent":
                    callBack?.Invoke("GetCurrent", currentVector);
                    break;

                case "OnUpdateWind":
                    OnUpdateWind += data as Action<Vector3>;
                    break;

                case "OnUpdateCurrent":
                    OnUpdateCurrent += data as Action<Vector3>;
                    break;

                case "OnUpdateSailing":
                    OnUpdateSailing += data as Action<bool>;
                    break;

                case "ResetTimeScale":
                    ResetTimeScale();
                    break;

                case "StopSailing":
                    StopSailing();
                    break;

                case "IsPlayerSailing":
                    callBack?.Invoke("IsPlayerSailing", IsSailing);
                    break;

                case "GetBoatGameObject":
                    callBack?.Invoke("GetBoatGameObject", CurrentBoat.GameObject);
                    break;

                case "GetBoatMeshObject":
                    callBack?.Invoke("GetBoatMeshObject", CurrentBoat.MeshObject);
                    break;

                default:
                    Debug.LogErrorFormat("{0}: unknown message received ({1}).", this, message);
                    break;
            }
        }

        private static class GiveMeBoat
        {
            public static readonly string name = "giveboat";
            public static readonly string description = "Add a boat to the player's inventory";
            public static readonly string usage = "giveboat [hull] [variant]; No argument will result in random hull and variant.";

            public static string Execute(params string[] args)
            {
                string result = "";
                result = "Boat added to player's inventory";
                //add variant stuff here
                int hull = 0;
                int variant = 0;
                if (args.Length == 0)
                {
                    hull = UnityEngine.Random.Range(0, 4);
                    if (hull == 1)
                        variant = UnityEngine.Random.Range(0, 7);
                }
                else if (args.Length == 1)
                {
                    hull = Convert.ToInt32(args[0]);
                    if (hull == 1)
                        variant = UnityEngine.Random.Range(0, 7);
                }
                else if (args.Length == 2)
                {
                    hull = Convert.ToInt32(args[0]);
                    variant = Convert.ToInt32(args[1]);
                }

                int templateIndex = ItemBoatDeed.templateIndex;

                DaggerfallUnityItem newBoatItem = ItemBuilder.CreateItem(ItemGroups.UselessItems2,templateIndex);
                newBoatItem.message = (hull * 10) + variant;
                newBoatItem.shortName += " " + Instance.hullNames[hull] + " '" + Instance.variantNames[variant] + "'";
                GameManager.Instance.PlayerEntity.Items.AddItem(newBoatItem);
                return result;
            }
        }

        private static class PlaceBoatAtMe
        {
            public static readonly string name = "placeboat";
            public static readonly string description = "place a boat where the player is looking";
            public static readonly string usage = "placeboat [hull] [variant]; No argument will result in random hull and variant. WARNING: only hull 0 is available now and variants only go from 0-6";

            public static string Execute(params string[] args)
            {
                if (args.Length > 2)
                    return "Error - Too many arguments, check the usage notes.";

                string result = "Attempting to place boat";
                Instance.PlaceBoatAtRayHit(args);
                return result;
            }
        }

        void UpdateAudioSource()
        {
            if (AllBoats.Count < 1)
                return;

            foreach (Boat boat in AllBoats)
            {
                if (boat.AudioSourceSlow.volume > 0)
                    boat.AudioSourceSlow.volume = DaggerfallUnity.Settings.SoundVolume * sfxVolume;

                if (boat.AudioSourceFast.volume > 0)
                    boat.AudioSourceFast.volume = DaggerfallUnity.Settings.SoundVolume * sfxVolume;
            }
        }

        public static void OnStartLoad(SaveData_v1 saveData)
        {
            if (Instance.parentedObjects.Count > 0)
            {
                for (int i = Instance.parentedObjects.Count-1; i >= 0; i--)
                {
                    Destroy(Instance.parentedObjects[i]);
                }
                Instance.parentedObjects.Clear();
            }

            if (Instance.IsSailing)
            {
                Instance.StopSailing();
            }
            else
            {
                Instance.ResetTimeScale();
            }
        }

        public static void OnLoad(SaveData_v1 saveData)
        {
            Instance.UpdateBoatVisibility();

            if (Instance.waveObject != null)
                Instance.UpdateWaveMesh();
        }

        void OnPreFastTravel(DaggerfallTravelPopUp daggerfallTravelPopUp)
        {
            if (placing)
                StopPlacing();

            if (IsSailing)
            {
                Boat boat = CurrentBoat;
                StopSailing();
                if (boat.packable)
                    PackBoat(boat, true);
            }
            else
            {
                Instance.ResetTimeScale();
            }
        }

        void OnPostFastTravel()
        {
            Instance.ResetTimeScale();
        }

        void OnTransition(PlayerEnterExit.TransitionEventArgs args)
        {
            Instance.ResetTimeScale();

            UpdateBoatVisibility();
        }

        void OnPositionUpdate(Vector3 offset)
        {
            UpdateWaveMesh();

            if (AllBoats.Count < 1)
                return;

            UpdateBoatVisibility();

            foreach (Boat boat in AllBoats)
            {
                if (!boat.GameObject.activeSelf)
                    continue;

                boat.WakeEmitter.Stop();

                //when floating origin resets, update boat position
                Vector3 pos = boat.GameObject.transform.position + offset;
                //boat.GameObject.transform.position = new Vector3(pos.x,WaterLevel,pos.z);
                boat.GameObject.transform.position = pos;

                //fix wake
                if (boat.WakeEmitter.particleCount > 0)
                {
                    ParticleSystem.Particle[] particles = new ParticleSystem.Particle[boat.WakeEmitter.particleCount];
                    boat.WakeEmitter.GetParticles(particles);

                    for (int i = 0; i < boat.WakeEmitter.particleCount; i++)
                        particles[i].position += offset;

                    boat.WakeEmitter.SetParticles(particles);
                }

                if (boat.OarParticles.Count > 0)
                {
                    foreach (ParticleSystem particleSystem in boat.OarParticles)
                    {
                        ParticleSystem.SubEmittersModule subEmitters = particleSystem.subEmitters;
                        ParticleSystem subEmitter = subEmitters.GetSubEmitterSystem(0);
                        if (subEmitter.particleCount > 0)
                        {
                            ParticleSystem.Particle[] particles = new ParticleSystem.Particle[subEmitter.particleCount];
                            subEmitter.GetParticles(particles);

                            for (int i = 0; i < subEmitter.particleCount; i++)
                                particles[i].position += offset;

                            subEmitter.SetParticles(particles);
                        }
                    }
                }

                if (boat == CurrentBoat)
                {
                    playerObject.transform.position = boat.DrivePosition.transform.position;
                    boat.MapPixel = GameManager.Instance.PlayerGPS.CurrentMapPixel;

                    if (MoveVectorCurrent.magnitude >= wakeThreshold && !DisableParticles)
                        boat.WakeEmitter.Play();
                }
            }
        }

        void OnNewHour()
        {
            //every hour, update wind
            UpdateWind(GameManager.Instance.WeatherManager.PlayerWeather.WeatherType);
        }

        void OnWeatherChange(DaggerfallWorkshop.Game.Weather.WeatherType next)
        {
            //when weather changes, update wind
            UpdateWind(next);
        }

        void OnPlayerDeath(DaggerfallEntity entity)
        {
            if (IsSailing)
            {
                StopSailing();
            }
            else
                ResetTimeScale();
        }

        int GetTileMapIndexAtPosition(Vector3 position, Transform terrainTransform)
        {
            int tileMapIndex = -1;

            // Player must be above a known terrain object
            DaggerfallTerrain terrain = terrainTransform.GetComponent<DaggerfallTerrain>();
            if (!terrain)
                return tileMapIndex;

            // The terrain must have a valid tilemap array
            if (terrain.TileMap == null || terrain.TileMap.Length == 0)
                return tileMapIndex;

            // Get player relative position from terrain origin
            Vector3 relativePos = position - terrain.transform.position;

            // Convert X, Z position into 0-1 domain
            float dim = DaggerfallConnect.Arena2.MapsFile.WorldMapTerrainDim * MeshReader.GlobalScale;
            float u = relativePos.x / dim;
            float v = relativePos.z / dim;

            // Get clamped offset into tilemap array
            int x = Mathf.Clamp((int)(DaggerfallConnect.Arena2.MapsFile.WorldMapTileDim * u), 0, DaggerfallConnect.Arena2.MapsFile.WorldMapTileDim - 1);
            int y = Mathf.Clamp((int)(DaggerfallConnect.Arena2.MapsFile.WorldMapTileDim * v), 0, DaggerfallConnect.Arena2.MapsFile.WorldMapTileDim - 1);

            // Update index - divide by 4 to find actual tile base as each tile has 4x variants (flip, rotate, etc.)
            tileMapIndex = terrain.TileMap[y * DaggerfallConnect.Arena2.MapsFile.WorldMapTileDim + x].r / 4;

            return tileMapIndex;
        }

        void OnNewMagicRound()
        {
            if (IsSailing)
            {
                UpdateBoatCargoMod(CurrentBoat);
            }
        }

        void UpdateWaveMesh()
        {
            //clear mesh
            waveMeshFilter.mesh.Clear();

            if (!wave || (AnimatedWater != null && AWVertexWaves))
                return;

            //reset origin
            Vector3 worldCompensation = GameManager.Instance.StreamingWorld.WorldCompensation;
            if (WODTerrain != null)
                waveObject.transform.localPosition = (Vector3.forward * (terrainEdge / 2)) + (Vector3.right * (terrainEdge / 2)) + (Vector3.up * 100) + (Vector3.up * worldCompensation.y);
            else
                waveObject.transform.localPosition = (Vector3.forward * (terrainEdge / 2)) + (Vector3.right * (terrainEdge / 2)) + (Vector3.up * 34) + (Vector3.up * worldCompensation.y);

            //get map pixel
            DFPosition mapPixel = GameManager.Instance.PlayerGPS.CurrentMapPixel;

            int waterMapHeight = 2;
            int waterWorldHeight = (int)WaterLevel;

            if (WODTerrain != null)
                waterMapHeight = 6;

            //check neighbors for ocean pixels
            List<Vector2> neighbors = new List<Vector2>();
            for (int x = -waveDistance; x < waveDistance+1; x++)
            {
                for (int y = -waveDistance; y < waveDistance+1; y++)
                {
                    int heightMapValue = DaggerfallUnity.Instance.ContentReader.WoodsFileReader.GetHeightMapValue(mapPixel.X + x, mapPixel.Y + y);

                    if (heightMapValue <= waterMapHeight)
                    {
                        Vector3 offset = new Vector3(terrainEdge * x,0,terrainEdge * -y);
                        //Ray ray = new Ray(new Vector3(terrainEdge / 2, worldCompensation.y+500, terrainEdge / 2) + offset, Vector3.down);
                        Ray ray1 = new Ray(new Vector3(terrainEdge * 0.25f, worldCompensation.y+500, terrainEdge * 0.25f) + offset, Vector3.down);
                        Ray ray2 = new Ray(new Vector3(terrainEdge * 0.75f, worldCompensation.y+500, terrainEdge * 0.25f) + offset, Vector3.down);
                        Ray ray3 = new Ray(new Vector3(terrainEdge * 0.25f, worldCompensation.y+500, terrainEdge * 0.75f) + offset, Vector3.down);
                        Ray ray4 = new Ray(new Vector3(terrainEdge * 0.75f, worldCompensation.y+500, terrainEdge * 0.75f) + offset, Vector3.down);
                        RaycastHit hit = new RaycastHit();
                        LayerMask layerMask = new LayerMask();
                        layerMask = ~(1 << LayerMask.NameToLayer("Player"));
                        layerMask = layerMask & ~(1 << LayerMask.NameToLayer("Ignore Raycast"));


                        /*if (Physics.Raycast(ray, out hit, 1000f, layerMask))
                        {
                            if (hit.point.y < worldCompensation.y+34)
                            {
                                neighbors.Add(new Vector2(mapPixel.X + x, mapPixel.Y + y));
                                Debug.DrawRay(ray.origin, ray.direction * 1000f, Color.green, 5);
                            }
                            else
                                Debug.DrawRay(ray.origin, ray.direction * 1000f, Color.red, 5);
                        }*/


                        bool ocean = true;
                        if (Physics.Raycast(ray1, out hit, 1000f, layerMask))
                        {
                            if (hit.point.y > worldCompensation.y + waterWorldHeight)
                            {
                                ocean = false;
                                Debug.DrawRay(ray1.origin, ray1.direction * 1000f, Color.green, 5);
                            }
                            else
                                Debug.DrawRay(ray1.origin, ray1.direction * 1000f, Color.red, 5);
                        }
                        else
                            Debug.DrawRay(ray1.origin, ray1.direction * 1000f, Color.yellow, 5);

                        if (Physics.Raycast(ray2, out hit, 1000f, layerMask))
                        {
                            if (hit.point.y > worldCompensation.y + waterWorldHeight)
                            {
                                ocean = false;
                                Debug.DrawRay(ray2.origin, ray2.direction * 1000f, Color.green, 5);
                            }
                            else
                                Debug.DrawRay(ray2.origin, ray2.direction * 1000f, Color.red, 5);
                        }
                        else
                            Debug.DrawRay(ray2.origin, ray2.direction * 1000f, Color.yellow, 5);

                        if (Physics.Raycast(ray3, out hit, 1000f, layerMask))
                        {
                            if (hit.point.y > worldCompensation.y + waterWorldHeight)
                            {
                                ocean = false;
                                Debug.DrawRay(ray3.origin, ray3.direction * 1000f, Color.green, 5);
                            }
                            else
                                Debug.DrawRay(ray3.origin, ray3.direction * 1000f, Color.red, 5);
                        }
                        else
                            Debug.DrawRay(ray3.origin, ray3.direction * 1000f, Color.yellow, 5);

                        if (Physics.Raycast(ray4, out hit, 1000f, layerMask))
                        {
                            if (hit.point.y > worldCompensation.y + waterWorldHeight)
                            {
                                ocean = false;
                                Debug.DrawRay(ray4.origin, ray4.direction * 1000f, Color.green, 5);
                            }
                            else
                                Debug.DrawRay(ray4.origin, ray4.direction * 1000f, Color.red, 5);
                        }
                        else
                            Debug.DrawRay(ray4.origin, ray4.direction * 1000f, Color.yellow, 5);

                        if (ocean)
                            neighbors.Add(new Vector2(mapPixel.X + x, mapPixel.Y + y));
                    }
                }
            }

            //if any neighbors are ocean pixels
            if (neighbors.Count > 0)
            {
                //generate mesh
                List<Vector3> vertices = new List<Vector3>();
                int triangleIndex = 0;
                List<int> triangles = new List<int>();
                List<Vector2> uvs = new List<Vector2>();

                //check each ocean pixels' neighbors for ground
                foreach (Vector2 neighbor in neighbors)
                {
                    Vector3 offset = new Vector3(neighbor.x-mapPixel.X, 0, mapPixel.Y-neighbor.y);
                    bool[,] isNeighborGround = new bool[3, 3];
                    for (int x = -1; x < 2; x++)
                    {
                        for (int y = -1; y < 2; y++)
                        {
                            if (DaggerfallUnity.Instance.ContentReader.WoodsFileReader.GetHeightMapValue((int)neighbor.x + x, (int)neighbor.y + y) > waterMapHeight)
                            {
                                isNeighborGround[x + 1, y + 1] = true;
                            }
                            else
                            {
                                Vector3 offsetNeighbor = ((Vector3.right + Vector3.forward) * (terrainEdge / 2)) + (offset * terrainEdge) + (new Vector3(x,0,-y) * terrainEdge);

                                Ray ray1 = new Ray(offsetNeighbor + (Vector3.up * 500f) + ((Vector3.back + Vector3.left) * (terrainEdge / 4)), Vector3.down);
                                Ray ray2 = new Ray(offsetNeighbor + (Vector3.up * 500f) + ((Vector3.back + Vector3.right) * (terrainEdge / 4)), Vector3.down);
                                Ray ray3 = new Ray(offsetNeighbor + (Vector3.up * 500f) + ((Vector3.forward + Vector3.left) * (terrainEdge / 4)), Vector3.down);
                                Ray ray4 = new Ray(offsetNeighbor + (Vector3.up * 500f) + ((Vector3.forward + Vector3.right) * (terrainEdge / 4)), Vector3.down);
                                RaycastHit hit = new RaycastHit();
                                LayerMask layerMask = new LayerMask();
                                layerMask = ~(1 << LayerMask.NameToLayer("Player"));
                                layerMask = layerMask & ~(1 << LayerMask.NameToLayer("Ignore Raycast"));

                                if (Physics.Raycast(ray1, out hit, 1000f, layerMask))
                                {
                                    if (hit.point.y > worldCompensation.y + waterWorldHeight)
                                        isNeighborGround[x + 1, y + 1] = true;
                                }

                                if (Physics.Raycast(ray2, out hit, 1000f, layerMask))
                                {
                                    if (hit.point.y > worldCompensation.y + waterWorldHeight)
                                        isNeighborGround[x + 1, y + 1] = true;
                                }

                                if (Physics.Raycast(ray3, out hit, 1000f, layerMask))
                                {
                                    if (hit.point.y > worldCompensation.y + waterWorldHeight)
                                        isNeighborGround[x + 1, y + 1] = true;
                                }

                                if (Physics.Raycast(ray4, out hit, 1000f, layerMask))
                                {
                                    if (hit.point.y > worldCompensation.y + waterWorldHeight)
                                        isNeighborGround[x + 1, y + 1] = true;
                                }
                            }

                            if (x == 0 && y == 0)
                            {
                                //this is the current pixel
                                currentNeighbors = isNeighborGround;
                            }
                        }
                    }

                    //north
                    if (isNeighborGround[1, 0])
                    {
                        //Debug.Log("COME SAIL AWAY - NORTH NEIGHOR IS GROUND!");

                        int start = vertices.Count;

                        vertices.Add(offset + new Vector3(0.25f, 0, 0.25f));      //0
                        vertices.Add(offset + new Vector3(-0.25f, 0, 0.25f));       //1
                        vertices.Add(offset + new Vector3(-0.5f, 0, 0.5f));         //2
                        vertices.Add(offset + new Vector3(0, 0, 1f));              //3
                        vertices.Add(offset + new Vector3(0.5f, 0, 0.5f));        //4

                        uvs.Add(new Vector2(0.25f, 1f));
                        uvs.Add(new Vector2(0.75f, 1f));
                        uvs.Add(new Vector2(1f, 0.75f));
                        uvs.Add(new Vector2(0.5f, 0.25f));
                        uvs.Add(new Vector2(0, 0.75f));

                        triangles.Add(start + 0);
                        triangles.Add(start + 1);
                        triangles.Add(start + 4);

                        triangles.Add(start + 1);
                        triangles.Add(start + 2);
                        triangles.Add(start + 4);

                        triangles.Add(start + 2);
                        triangles.Add(start + 3);
                        triangles.Add(start + 4);

                        //check diagonals
                        if (isNeighborGround[0, 0])
                        {
                            //Debug.Log("COME SAIL AWAY - NORTHWEST NEIGHOR IS GROUND!");

                            if (isNeighborGround[0, 1])
                            {
                                //west neighbor is ground
                                //add the large triangle to the tile overlapping north neighbor
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(-0.5f, 0, 0.5f));     //0
                                vertices.Add(offset + new Vector3(-1f, 0, 1f));       //1
                                vertices.Add(offset + new Vector3(0, 0, 1f));          //2
                                uvs.Add(new Vector2(1f, 0.75f));
                                uvs.Add(new Vector2(1.5f, 0.25f));
                                uvs.Add(new Vector2(0.5f, 0.25f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                            else
                            {
                                //west neighbor is water
                                //add the large triangle to the tile overlapping north neighbor
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(-0.5f, 0, 0.5f));     //0
                                vertices.Add(offset + new Vector3(-0.5f, 0, 1f));       //1
                                vertices.Add(offset + new Vector3(0, 0, 1f));          //2
                                uvs.Add(new Vector2(1f, 0.75f));
                                uvs.Add(new Vector2(1f, 0.25f));
                                uvs.Add(new Vector2(0.5f, 0.25f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);

                                //add the small triangle to the current tile
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(-0.25f, 0, 0.25f));   //0
                                vertices.Add(offset + new Vector3(-0.5f, 0, 0.25f));    //1
                                vertices.Add(offset + new Vector3(-0.5f, 0, 0.5f));     //2
                                uvs.Add(new Vector2(0.75f, 1f));
                                uvs.Add(new Vector2(1f, 1f));
                                uvs.Add(new Vector2(1f, 0.75f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                        }
                        else
                        {
                            if (!isNeighborGround[0, 1])
                            {
                                //west neighbor is water
                                //add the small triangle to the current tile
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(-0.25f, 0, 0.25f));   //0
                                vertices.Add(offset + new Vector3(-0.75f, 0, 0.25f));    //1
                                vertices.Add(offset + new Vector3(-0.5f, 0, 0.5f));     //2
                                uvs.Add(new Vector2(0.75f, 1f));
                                uvs.Add(new Vector2(1.25f, 1f));
                                uvs.Add(new Vector2(1f, 0.75f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                        }
                        if (isNeighborGround[2, 0])
                        {
                            //Debug.Log("COME SAIL AWAY - NORTHEAST NEIGHOR IS GROUND!");

                            if (isNeighborGround[2, 1])
                            {
                                //east neighbor is ground
                                //add the large triangle to the tile overlapping south neighbor
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(0.5f, 0, 0.5f));     //0
                                vertices.Add(offset + new Vector3(0, 0, 1f));              //1
                                vertices.Add(offset + new Vector3(1f, 0, 1f));          //2
                                uvs.Add(new Vector2(0, 0.75f));
                                uvs.Add(new Vector2(0.5f, 0.25f));
                                uvs.Add(new Vector2(-0.5f, 0.25f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                            else
                            {
                                //east neighbor is water
                                //add the large triangle to the tile overlapping south neighbor
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(0.5f, 0, 0.5f));     //0
                                vertices.Add(offset + new Vector3(0, 0, 1f));              //1
                                vertices.Add(offset + new Vector3(0.5f, 0, 1f));          //2
                                uvs.Add(new Vector2(0, 0.75f));
                                uvs.Add(new Vector2(0.5f, 0.25f));
                                uvs.Add(new Vector2(0, 0.25f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);

                                //add the small triangle to the current tile
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(0.5f, 0, 0.25f));   //0
                                vertices.Add(offset + new Vector3(0.25f, 0, 0.25f));   //1
                                vertices.Add(offset + new Vector3(0.5f, 0, 0.5f));    //2
                                uvs.Add(new Vector2(0f, 1f));
                                uvs.Add(new Vector2(0.25f, 1f));
                                uvs.Add(new Vector2(0f, 0.75f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                        }
                        else
                        {
                            if (!isNeighborGround[2, 1])
                            {
                                //east neighbor is ground
                                //add the small triangle to the current tile
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(0.75f, 0, 0.25f));   //0
                                vertices.Add(offset + new Vector3(0.25f, 0, 0.25f));   //1
                                vertices.Add(offset + new Vector3(0.5f, 0, 0.5f));    //2
                                uvs.Add(new Vector2(-0.25f, 1f));
                                uvs.Add(new Vector2(0.25f, 1f));
                                uvs.Add(new Vector2(0f, 0.75f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                        }
                    }

                    //west
                    if (isNeighborGround[0, 1])
                    {
                        //Debug.Log("COME SAIL AWAY - WEST NEIGHOR IS GROUND!");

                        int start = vertices.Count;

                        vertices.Add(offset + new Vector3(-0.25f, 0, 0.25f));      //0
                        vertices.Add(offset + new Vector3(-0.25f, 0, -0.25f));       //1
                        vertices.Add(offset + new Vector3(-0.5f, 0, -0.5f));         //2
                        vertices.Add(offset + new Vector3(-1, 0, 0));              //3
                        vertices.Add(offset + new Vector3(-0.5f, 0, 0.5f));        //4

                        uvs.Add(new Vector2(0.25f, 1f));
                        uvs.Add(new Vector2(0.75f, 1f));
                        uvs.Add(new Vector2(1f, 0.75f));
                        uvs.Add(new Vector2(0.5f, 0.25f));
                        uvs.Add(new Vector2(0, 0.75f));

                        triangles.Add(start + 0);
                        triangles.Add(start + 1);
                        triangles.Add(start + 4);

                        triangles.Add(start + 1);
                        triangles.Add(start + 2);
                        triangles.Add(start + 4);

                        triangles.Add(start + 2);
                        triangles.Add(start + 3);
                        triangles.Add(start + 4);

                        //check diagonals
                        if (isNeighborGround[0, 2])
                        {
                            //Debug.Log("COME SAIL AWAY - SOUTHWEST NEIGHOR IS GROUND!");

                            if (isNeighborGround[1, 2])
                            {
                                //south neighbor is ground
                                //add the large triangle to the tile overlapping east neighbor
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(-0.5f, 0, -0.5f));     //0
                                vertices.Add(offset + new Vector3(-1f, 0, -1f));       //1
                                vertices.Add(offset + new Vector3(-1f, 0, 0f));          //2
                                uvs.Add(new Vector2(1f, 0.75f));
                                uvs.Add(new Vector2(1.5f, 0.25f));
                                uvs.Add(new Vector2(0.5f, 0.25f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                            else
                            {
                                //south neighbor is water
                                //add the large triangle to the tile overlapping east neighbor
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(-0.5f, 0, -0.5f));     //0
                                vertices.Add(offset + new Vector3(-1f, 0, -0.5f));       //1
                                vertices.Add(offset + new Vector3(-1f, 0, 0f));          //2
                                uvs.Add(new Vector2(1f, 0.75f));
                                uvs.Add(new Vector2(1f, 0.25f));
                                uvs.Add(new Vector2(0.5f, 0.25f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);

                                //add the small triangle to the current tile
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(-0.25f, 0, -0.25f));   //0
                                vertices.Add(offset + new Vector3(-0.25f, 0, -0.5f));    //1
                                vertices.Add(offset + new Vector3(-0.5f, 0, -0.5f));     //2
                                uvs.Add(new Vector2(0.75f, 1f));
                                uvs.Add(new Vector2(1f, 1f));
                                uvs.Add(new Vector2(1f, 0.75f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                        }
                        else
                        {
                            if (!isNeighborGround[1, 2])
                            {
                                //south neighbor is water
                                //add the small triangle to the current tile
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(-0.25f, 0, -0.25f));   //0
                                vertices.Add(offset + new Vector3(-0.25f, 0, -0.75f));    //1
                                vertices.Add(offset + new Vector3(-0.5f, 0, -0.5f));     //2
                                uvs.Add(new Vector2(0.75f, 1f));
                                uvs.Add(new Vector2(1.25f, 1f));
                                uvs.Add(new Vector2(1f, 0.75f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                        }
                        if (isNeighborGround[0, 0])
                        {
                            //Debug.Log("COME SAIL AWAY - NORTHWEST NEIGHOR IS GROUND!");

                            if (isNeighborGround[1, 0])
                            {
                                //north neighbor is ground
                                //add the large triangle to the tile overlapping east neighbor
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(-0.5f, 0, 0.5f));     //0
                                vertices.Add(offset + new Vector3(-1, 0, 0));              //1
                                vertices.Add(offset + new Vector3(-1, 0, 1f));          //2
                                uvs.Add(new Vector2(0, 0.75f));
                                uvs.Add(new Vector2(0.5f, 0.25f));
                                uvs.Add(new Vector2(-0.5f, 0.25f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                            else
                            {
                                //north neighbor is water
                                //add the large triangle to the tile overlapping east neighbor
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(-0.5f, 0, 0.5f));     //0
                                vertices.Add(offset + new Vector3(-1, 0, 0));              //1
                                vertices.Add(offset + new Vector3(-1, 0, 0.5f));          //2
                                uvs.Add(new Vector2(0, 0.75f));
                                uvs.Add(new Vector2(0.5f, 0.25f));
                                uvs.Add(new Vector2(0, 0.25f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);

                                //add the small triangle to the current tile
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(-0.25f, 0, 0.5f));   //0
                                vertices.Add(offset + new Vector3(-0.25f, 0, 0.25f));   //1
                                vertices.Add(offset + new Vector3(-0.5f, 0, 0.5f));    //2
                                uvs.Add(new Vector2(0f, 1f));
                                uvs.Add(new Vector2(0.25f, 1f));
                                uvs.Add(new Vector2(0f, 0.75f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                        }
                        else
                        {
                            if (!isNeighborGround[1, 0])
                            {
                                //north neighbor is water
                                //add the small triangle to the current tile
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(-0.25f, 0, 0.75f));   //0
                                vertices.Add(offset + new Vector3(-0.25f, 0, 0.25f));   //1
                                vertices.Add(offset + new Vector3(-0.5f, 0, 0.5f));    //2
                                uvs.Add(new Vector2(-0.25f, 1f));
                                uvs.Add(new Vector2(0.25f, 1f));
                                uvs.Add(new Vector2(0f, 0.75f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                        }
                    }

                    //south
                    if (isNeighborGround[1, 2])
                    {
                        //Debug.Log("COME SAIL AWAY - SOUTH NEIGHOR IS GROUND!");

                        int start = vertices.Count;

                        vertices.Add(offset + new Vector3(-0.25f, 0, -0.25f));      //0
                        vertices.Add(offset + new Vector3(0.25f, 0, -0.25f));       //1
                        vertices.Add(offset + new Vector3(0.5f, 0, -0.5f));         //2
                        vertices.Add(offset + new Vector3(0, 0, -1f));              //3
                        vertices.Add(offset + new Vector3(-0.5f, 0, -0.5f));        //4

                        uvs.Add(new Vector2(0.25f, 1f));
                        uvs.Add(new Vector2(0.75f, 1f));
                        uvs.Add(new Vector2(1f, 0.75f));
                        uvs.Add(new Vector2(0.5f, 0.25f));
                        uvs.Add(new Vector2(0, 0.75f));

                        triangles.Add(start + 0);
                        triangles.Add(start + 1);
                        triangles.Add(start + 4);

                        triangles.Add(start + 1);
                        triangles.Add(start + 2);
                        triangles.Add(start + 4);

                        triangles.Add(start + 2);
                        triangles.Add(start + 3);
                        triangles.Add(start + 4);

                        //check diagonals
                        if (isNeighborGround[2, 2])
                        {
                            //Debug.Log("COME SAIL AWAY - SOUTHEAST NEIGHOR IS GROUND!");

                            if (isNeighborGround[2, 1])
                            {
                                //east neighbor is ground
                                //add the large triangle to the tile overlapping south and southeast neighbor
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(0.5f, 0, -0.5f));     //0
                                vertices.Add(offset + new Vector3(1f, 0, -1f));       //1
                                vertices.Add(offset + new Vector3(0, 0, -1f));          //2
                                uvs.Add(new Vector2(1f, 0.75f));
                                uvs.Add(new Vector2(1.5f, 0.25f));
                                uvs.Add(new Vector2(0.5f, 0.25f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                            else
                            {
                                //east neighbor is water
                                //add the large triangle to the tile overlapping south neighbor
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(0.5f, 0, -0.5f));     //0
                                vertices.Add(offset + new Vector3(0.5f, 0, -1f));       //1
                                vertices.Add(offset + new Vector3(0, 0, -1f));          //2
                                uvs.Add(new Vector2(1f, 0.75f));
                                uvs.Add(new Vector2(1f, 0.25f));
                                uvs.Add(new Vector2(0.5f, 0.25f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);

                                //add the small triangle to the current tile
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(0.25f, 0, -0.25f));   //0
                                vertices.Add(offset + new Vector3(0.5f, 0, -0.25f));    //1
                                vertices.Add(offset + new Vector3(0.5f, 0, -0.5f));     //2
                                uvs.Add(new Vector2(0.75f, 1f));
                                uvs.Add(new Vector2(1f, 1f));
                                uvs.Add(new Vector2(1f, 0.75f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                        }
                        else
                        {
                            if (!isNeighborGround[2, 1])
                            {
                                //east neighbor is water
                                //add the small triangle that overlaps the current tile and western neighbor
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(0.25f, 0, -0.25f));   //0
                                vertices.Add(offset + new Vector3(0.75f, 0, -0.25f));    //1
                                vertices.Add(offset + new Vector3(0.5f, 0, -0.5f));     //2
                                uvs.Add(new Vector2(0.75f, 1f));
                                uvs.Add(new Vector2(1.25f, 1f));
                                uvs.Add(new Vector2(1f, 0.75f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                        }
                        if (isNeighborGround[0, 2])
                        {
                            //Debug.Log("COME SAIL AWAY - SOUTHWEST NEIGHOR IS GROUND!");

                            if (isNeighborGround[0, 1])
                            {
                                //west neighbor is ground
                                //add the large triangle to the tile overlapping south neighbor
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(-0.5f, 0, -0.5f));     //0
                                vertices.Add(offset + new Vector3(0, 0, -1f));              //1
                                vertices.Add(offset + new Vector3(-1f, 0, -1f));          //2
                                uvs.Add(new Vector2(0, 0.75f));
                                uvs.Add(new Vector2(0.5f, 0.25f));
                                uvs.Add(new Vector2(-0.5f, 0.25f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                            else
                            {
                                //west neighbor is water
                                //add the large triangle to the tile overlapping south neighbor
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(-0.5f, 0, -0.5f));     //0
                                vertices.Add(offset + new Vector3(0, 0, -1f));              //1
                                vertices.Add(offset + new Vector3(-0.5f, 0, -1f));          //2
                                uvs.Add(new Vector2(0, 0.75f));
                                uvs.Add(new Vector2(0.5f, 0.25f));
                                uvs.Add(new Vector2(0, 0.25f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);

                                //add the small triangle to the current tile
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(-0.5f, 0, -0.25f));   //0
                                vertices.Add(offset + new Vector3(0.25f, 0, -0.25f));   //1
                                vertices.Add(offset + new Vector3(-0.5f, 0, -0.5f));    //2
                                uvs.Add(new Vector2(0f, 1f));
                                uvs.Add(new Vector2(0.25f, 1f));
                                uvs.Add(new Vector2(0f, 0.75f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                        }
                        else
                        {
                            if (!isNeighborGround[0, 1])
                            {
                                //west neighbor is water
                                //add the small triangle that overlaps the current tile and western neighbor
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(-0.75f, 0, -0.25f));   //0
                                vertices.Add(offset + new Vector3(0.25f, 0, -0.25f));   //1
                                vertices.Add(offset + new Vector3(-0.5f, 0, -0.5f));    //2
                                uvs.Add(new Vector2(-0.25f, 1f));
                                uvs.Add(new Vector2(0.25f, 1f));
                                uvs.Add(new Vector2(0f, 0.75f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                        }
                    }

                    //east
                    if (isNeighborGround[2, 1])
                    {
                        //Debug.Log("COME SAIL AWAY - EAST NEIGHOR IS GROUND!");

                        int start = vertices.Count;

                        vertices.Add(offset + new Vector3(0.25f, 0, -0.25f));      //0
                        vertices.Add(offset + new Vector3(0.25f, 0, 0.25f));       //1
                        vertices.Add(offset + new Vector3(0.5f, 0, 0.5f));         //2
                        vertices.Add(offset + new Vector3(1, 0, 0));              //3
                        vertices.Add(offset + new Vector3(0.5f, 0, -0.5f));        //4

                        uvs.Add(new Vector2(0.25f, 1f));
                        uvs.Add(new Vector2(0.75f, 1f));
                        uvs.Add(new Vector2(1f, 0.75f));
                        uvs.Add(new Vector2(0.5f, 0.25f));
                        uvs.Add(new Vector2(0, 0.75f));

                        triangles.Add(start + 0);
                        triangles.Add(start + 1);
                        triangles.Add(start + 4);

                        triangles.Add(start + 1);
                        triangles.Add(start + 2);
                        triangles.Add(start + 4);

                        triangles.Add(start + 2);
                        triangles.Add(start + 3);
                        triangles.Add(start + 4);

                        //check diagonals
                        if (isNeighborGround[2, 0])
                        {
                            //Debug.Log("COME SAIL AWAY - NORTHEAST NEIGHOR IS GROUND!");

                            if (isNeighborGround[1, 0])
                            {
                                //north neighbor is ground
                                //add the large triangle to the tile overlapping east and northeast neighbor
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(0.5f, 0, 0.5f));     //0
                                vertices.Add(offset + new Vector3(1f, 0, 1f));       //1
                                vertices.Add(offset + new Vector3(1f, 0, 0f));          //2
                                uvs.Add(new Vector2(1f, 0.75f));
                                uvs.Add(new Vector2(1.5f, 0.25f));
                                uvs.Add(new Vector2(0.5f, 0.25f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                            else
                            {
                                //north neighbor is water
                                //add the large triangle to the tile overlapping east neighbor
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(0.5f, 0, 0.5f));     //0
                                vertices.Add(offset + new Vector3(1f, 0, 0.5f));       //1
                                vertices.Add(offset + new Vector3(1f, 0, 0f));          //2
                                uvs.Add(new Vector2(1f, 0.75f));
                                uvs.Add(new Vector2(1f, 0.25f));
                                uvs.Add(new Vector2(0.5f, 0.25f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);

                                //add the small triangle to the current tile
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(0.25f, 0, 0.25f));   //0
                                vertices.Add(offset + new Vector3(0.25f, 0, 0.5f));    //1
                                vertices.Add(offset + new Vector3(0.5f, 0, 0.5f));     //2
                                uvs.Add(new Vector2(0.75f, 1f));
                                uvs.Add(new Vector2(1f, 1f));
                                uvs.Add(new Vector2(1f, 0.75f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                        }
                        else
                        {
                            if (!isNeighborGround[1, 0])
                            {
                                //north neighbor is water
                                //add the small triangle to the tile overlapping north neighbor
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(0.25f, 0, 0.25f));   //0
                                vertices.Add(offset + new Vector3(0.25f, 0, 0.75f));    //1
                                vertices.Add(offset + new Vector3(0.5f, 0, 0.5f));     //2
                                uvs.Add(new Vector2(0.75f, 1f));
                                uvs.Add(new Vector2(1.25f, 1f));
                                uvs.Add(new Vector2(1f, 0.75f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                        }
                        if (isNeighborGround[2, 2])
                        {
                            //Debug.Log("COME SAIL AWAY - SOUTHEAST NEIGHOR IS GROUND!");

                            if (isNeighborGround[1, 2])
                            {
                                //south neighbor is ground
                                //add the large triangle to the tile overlapping east and southeast neighbor
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(0.5f, 0, -0.5f));     //0
                                vertices.Add(offset + new Vector3(1, 0, 0));              //1
                                vertices.Add(offset + new Vector3(1f, 0, -1f));          //2
                                uvs.Add(new Vector2(0, 0.75f));
                                uvs.Add(new Vector2(0.5f, 0.25f));
                                uvs.Add(new Vector2(-0.5f, 0.25f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                            else
                            {
                                //south neighbor is water
                                //add the large triangle to the tile overlapping east neighbor
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(0.5f, 0, -0.5f));     //0
                                vertices.Add(offset + new Vector3(1, 0, 0));              //1
                                vertices.Add(offset + new Vector3(1f, 0, -0.5f));          //2
                                uvs.Add(new Vector2(0, 0.75f));
                                uvs.Add(new Vector2(0.5f, 0.25f));
                                uvs.Add(new Vector2(0, 0.25f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);

                                //add the small triangle to the current tile
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(0.25f, 0, -0.5f));   //0
                                vertices.Add(offset + new Vector3(0.25f, 0, -0.25f));   //1
                                vertices.Add(offset + new Vector3(0.5f, 0, -0.5f));    //2
                                uvs.Add(new Vector2(0f, 1f));
                                uvs.Add(new Vector2(0.25f, 1f));
                                uvs.Add(new Vector2(0f, 0.75f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                        }
                        else
                        {
                            if (!isNeighborGround[1, 2])
                            {
                                //south neighbor is water
                                //add the small triangle to the tile overlapping south neighbor
                                start = vertices.Count;
                                vertices.Add(offset + new Vector3(0.25f, 0, -0.75f));   //0
                                vertices.Add(offset + new Vector3(0.25f, 0, -0.25f));   //1
                                vertices.Add(offset + new Vector3(0.5f, 0, -0.5f));    //2
                                uvs.Add(new Vector2(-0.25f, 1f));
                                uvs.Add(new Vector2(0.25f, 1f));
                                uvs.Add(new Vector2(0f, 0.75f));
                                triangles.Add(start + 0);
                                triangles.Add(start + 1);
                                triangles.Add(start + 2);
                            }
                        }
                    }

                }

                //update mesh
                if (vertices.Count > 0)
                {
                    waveMesh.SetVertices(vertices);
                    waveMesh.SetTriangles(triangles.ToArray(), 0);
                    waveMesh.SetUVs(0, uvs.ToArray());
                    waveMesh.RecalculateNormals();
                }
            }
        }

        void UpdateCurrentBoatNodes()
        {
            if (!IsSailing)
                return;

            UpdateBoatNodes(CurrentBoat);
            CheckCollision(CurrentBoat);
        }

        void CheckCollision(Boat boat)
        {
            bool z = false;
            bool x = false;

            Bounds bounds = boat.MeshCollider.sharedMesh.bounds;

            //construct the capsule
            Vector3 start = boat.MeshCollider.bounds.center;
            float radius = bounds.extents.x;
            Vector3 length = boat.MeshObject.transform.forward * (bounds.extents.z - radius);
            Vector3 p1 = start + length;
            Vector3 p2 = start - length;

            /*Debug.DrawLine(p1, p2, Color.green, 1, false);
            Debug.DrawRay(p1, boat.MeshObject.transform.forward * radius, Color.yellow, 1, false);
            Debug.DrawRay(p1, boat.MeshObject.transform.right * radius, Color.yellow, 1, false);
            Debug.DrawRay(p1, -boat.MeshObject.transform.right * radius, Color.yellow, 1, false);
            Debug.DrawRay(p1, boat.MeshObject.transform.up * radius, Color.yellow, 1, false);
            Debug.DrawRay(p1, -boat.MeshObject.transform.up * radius, Color.yellow, 1, false);
            Debug.DrawRay(p2, -boat.MeshObject.transform.forward * radius, Color.blue, 1, false);
            Debug.DrawRay(p2, boat.MeshObject.transform.right * radius, Color.blue, 1, false);
            Debug.DrawRay(p2, -boat.MeshObject.transform.right * radius, Color.blue, 1, false);
            Debug.DrawRay(p2, boat.MeshObject.transform.up * radius, Color.blue, 1, false);
            Debug.DrawRay(p2, -boat.MeshObject.transform.up * radius, Color.blue, 1, false);*/


            //capsulecast in movement direction
            /*
            RaycastHit hit = new RaycastHit();
            if (Physics.CapsuleCast(p1,p2,radius,MoveVectorCurrent.normalized, out hit, 1))
            {
                Debug.Log("COME SAIL AWAY - BOAT COLLIDED WITH SOMETHING");
                MoveVectorTarget = -MoveVectorTarget;
                MoveVectorCurrent = MoveVectorTarget;
            }*/
            Vector3 distance = p1 - p2;

            //spherecastAll from stern to bow
            RaycastHit[] hits = Physics.SphereCastAll(p2, radius + 1, distance.normalized, distance.magnitude);
            collisionDirections.Clear();
            if (hits.Length > 0)
            {
                foreach (RaycastHit hit in hits)
                {
                    Terrain terrain = hit.collider.GetComponent<Terrain>();
                    DaggerfallEntityBehaviour entity = hit.collider.GetComponent<DaggerfallEntityBehaviour>();
                    if (hit.collider.transform.root != boat.GameObject.transform && terrain == null && entity == null)
                    {
                        Debug.Log("COME SAIL AWAY - BOAT COLLIDED WITH " + hit.collider.name);
                        collisionDirections.Add(Vector3.ProjectOnPlane(boat.GameObject.transform.position - hit.collider.ClosestPointOnBounds(boat.GameObject.transform.position), Vector3.up).normalized);
                    }
                }
            }

            if (collisionDirections.Count > 0)
            {
                if (timeScaleIndex > 0)
                    ResetTimeScale();
                MoveVectorTarget += boat.GameObject.transform.InverseTransformDirection(combinedCollisionDirection).normalized * Time.fixedDeltaTime * 100;
                MoveVectorCurrent = MoveVectorTarget;
            }
        }

        void UpdateAllBoatsNodes()
        {
            if (AllBoats.Count < 1)
                return;

            foreach (Boat boat in AllBoats)
            {
                //only update boat nodes if in the same map pixel as player
                if (boat.MapPixel == GameManager.Instance.PlayerGPS.CurrentMapPixel)
                {
                    UpdateBoatNodes(boat);
                }
            }
        }
        void UpdateBoatNodes(Boat boat, Terrain terrain = null)
        {
            Transform terrainTransform = GameManager.Instance.StreamingWorld.PlayerTerrainTransform;
            if (terrain != null)
                terrainTransform = terrain.transform;
            for (int i = 0; i < boat.NodeTileMapIndices.Length; i++)
            {
                boat.NodeTileMapIndices[i] = GetTileMapIndexAtPosition(boat.Nodes[i].transform.position, terrainTransform);
            }
        }
        void UpdateBoatNodes(Boat boat, DFPosition mapPixel)
        {
            /*DFPosition playerMapPixel = GameManager.Instance.PlayerGPS.CurrentMapPixel;
            if (Mathf.Max(Mathf.Abs(playerMapPixel.X - mapPixel.X), Mathf.Abs(playerMapPixel.Y - mapPixel.Y)) > 1)
            {
                return;
            }*/

            Transform terrainTransform = GameManager.Instance.StreamingWorld.GetTerrainTransform(mapPixel.X, mapPixel.Y);

            if (terrainTransform == null)
                return;

            for (int i = 0; i < boat.NodeTileMapIndices.Length; i++)
            {
                boat.NodeTileMapIndices[i] = GetTileMapIndexAtPosition(boat.Nodes[i].transform.position, terrainTransform);
            }
        }

        public void UpdateBoatVisibility()
        {
            if (AllBoats.Count < 1)
                return;

            //hide boats if player is inside building or boat is outside the range of rendered map pixels
            if (GameManager.Instance.PlayerEnterExit.IsPlayerInside)
            {
                foreach (Boat boat in AllBoats)
                {
                    if (boat.GameObject.activeSelf)
                        boat.GameObject.SetActive(false);
                }
                return;
            }

            int x = GameManager.Instance.PlayerGPS.CurrentMapPixel.X;
            int y = GameManager.Instance.PlayerGPS.CurrentMapPixel.Y;

            //hide boat if it is in a map pixel not adjacent to the player's
            foreach (Boat boat in AllBoats)
            {
                if (boat.MapPixel != null && Mathf.Max(Mathf.Abs(x - boat.MapPixel.X), Mathf.Abs(y - boat.MapPixel.Y)) > 1)
                {
                    if (boat.GameObject.activeSelf)
                        boat.GameObject.SetActive(false);
                    continue;
                }

                if (!boat.GameObject.activeSelf)
                    boat.GameObject.SetActive(true);

                UpdateBoatNodes(boat, boat.MapPixel);
            }
        }

        public void UpdateBoatVisibility(Boat boat)
        {
            if (AllBoats.Count < 1)
                return;

            //hide boats if player is inside building or boat is outside the range of rendered map pixels
            if (GameManager.Instance.PlayerEnterExit.IsPlayerInside)
            {
                if (boat.GameObject.activeSelf)
                    boat.GameObject.SetActive(false);

                return;
            }

            int x = GameManager.Instance.PlayerGPS.CurrentMapPixel.X;
            int y = GameManager.Instance.PlayerGPS.CurrentMapPixel.Y;

            //hide boat if it is in a map pixel not adjacent to the player's
            if (boat.MapPixel != null && Mathf.Max(Mathf.Abs(x - boat.MapPixel.X), Mathf.Abs(y - boat.MapPixel.Y)) > 1)
            {
                if (boat.GameObject.activeSelf)
                    boat.GameObject.SetActive(false);
            }
            else
            {
                if (!boat.GameObject.activeSelf)
                    boat.GameObject.SetActive(true);
            }

            UpdateBoatNodes(boat, boat.MapPixel);
        }

        void UpdateBoatCargoMod(Boat boat)
        {
            float weight = boat.Cargo.Items.GetWeight();

            if (cargoCountPlayerWeight)
                weight += GameManager.Instance.PlayerEntity.Gender == Genders.Female ? 120 : 175;   //half of humanoid weight in classic units

            if (cargoCountPlayerCarriedWeight)
                weight += GameManager.Instance.PlayerEntity.CarriedWeight;

            if (cargoCountCartCarriedWeight)
                weight += GameManager.Instance.PlayerEntity.WagonWeight;

            if (cargoCountHorseItemWeight && GameManager.Instance.TransportManager.HasHorse())
                weight += 800;

            if (cargoCountCartItemWeight && GameManager.Instance.TransportManager.HasCart())
                weight += 400;

            boatCargoMod = Mathf.Clamp(2 - (weight / (boatCargoThreshold * boat.modifierCargoThreshold)),0,1);

            if (lastWeight != weight)
            {
                lastWeight = weight;

                //if boat is encumbered and weight has changed
                //send encumberance message
                if (boatCargoMod < 0.5f)
                    DaggerfallUI.SetMidScreenText("You're going to need a bigger boat", 3);
                else if (boatCargoMod < 1f)
                    DaggerfallUI.SetMidScreenText("The boat draws a little lower than usual", 3);
            }

        }

        void UpdateWind(DaggerfallWorkshop.Game.Weather.WeatherType weather)
        {
            //strength
            float windStrength = UnityEngine.Random.Range(1f, 2f);
            if (weather == DaggerfallWorkshop.Game.Weather.WeatherType.Fog)
                windStrength *= 0.1f;
            else if (weather == DaggerfallWorkshop.Game.Weather.WeatherType.Rain)
                windStrength *= 1.5f;
            else if (weather == DaggerfallWorkshop.Game.Weather.WeatherType.Thunder)
                windStrength *= 2f;

            //determine prevailing wind direction based on map position and time
            Vector3 lastDirection = Vector3.right;
            if (DaggerfallUnity.Instance.WorldTime.Now.Hour > 6 && DaggerfallUnity.Instance.WorldTime.Now.Hour < 18)
                lastDirection += Vector3.back;
            else
                lastDirection += Vector3.forward;

            //current map pixel is on lower half of map, reverse the direction
            if (GameManager.Instance.PlayerGPS.CurrentMapPixel.Y > 250)
                lastDirection *= -1;

            //pick a direction up to 60 degrees off from prevailing wind direction
            float angle = 15 * UnityEngine.Random.Range(-4, 4);
            windVectorTarget = (Quaternion.AngleAxis(angle, Vector3.up) * lastDirection).normalized * windStrength;

            StartCoroutine(RotateWind());
        }

        IEnumerator RotateWind()
        {
            while (windVectorCurrent != windVectorTarget)
            {
                windVectorCurrent = Vector3.RotateTowards(windVectorCurrent, windVectorTarget, 0.1f * Time.deltaTime, 1);

                rainEmitterForceOverLifetime.x = new ParticleSystem.MinMaxCurve(windVectorCurrent.x * 10f, windVectorCurrent.x * 50f);
                rainEmitterForceOverLifetime.z = new ParticleSystem.MinMaxCurve(windVectorCurrent.z * 10f, windVectorCurrent.z * 50f);
                snowEmitterForceOverLifetime.x = new ParticleSystem.MinMaxCurve(windVectorCurrent.x * 5f, windVectorCurrent.x * 25f);
                snowEmitterForceOverLifetime.z = new ParticleSystem.MinMaxCurve(windVectorCurrent.z * 5f, windVectorCurrent.z * 25f);

                yield return new WaitForEndOfFrame();
            }

            if (OnUpdateWind != null)
                OnUpdateWind(windVectorCurrent);
        }

        private void OnGUI()
        {
            if (!IsSailing || GameManager.IsGamePaused || InputManager.Instance.IsPaused || SaveLoadManager.Instance.LoadInProgress)
                return;

            GUI.depth = -1;

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

            // Allow texture to be offset when large HUD enabled
            // This is enabled by default to match classic but can be toggled for either docked/undocked large HUD
            float LargeHudOffset = 0;
            if (DaggerfallUI.Instance.DaggerfallHUD != null && DaggerfallUnity.Settings.LargeHUD && DaggerfallUnity.Settings.LargeHUDOffsetHorse)
                LargeHudOffset = (int)DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.ScreenHeight;

            //draw wind direction indicator
            //animate the windDirectionWidget indicator to show the angle
            if (windDirectionWidget)
            {
                Vector2 windDirectionWidgetPos = new Vector2(screenRect.x + (screenRect.width * windDirectionWidgetOffset.x), screenRect.y + (screenRect.height * windDirectionWidgetOffset.y) - LargeHudOffset);
                Vector2 windDirectionWidgetTextureScale = new Vector2(windDirectionWidgetTextureCurrent.width * screenScaleX, windDirectionWidgetTextureCurrent.height * screenScaleY) * windDirectionWidgetScale;
                Vector2 windDirectionWidgetTextureOffset = new Vector2(windDirectionWidgetTextureScale.x, windDirectionWidgetTextureScale.y) * 0.5f;
                windDirectionWidgetRect = new Rect(windDirectionWidgetPos - windDirectionWidgetTextureOffset, windDirectionWidgetTextureScale);
                DaggerfallUI.DrawTexture(windDirectionWidgetRect, windDirectionWidgetTextureCurrent, ScaleMode.StretchToFill, false, windDirectionWidgetColor);
            }

            if (debugShow)
            {
                DaggerfallUI.DefaultFont.DrawText(velocityCurrent.magnitude.ToString(), Vector2.zero, Vector2.one * 5, Color.red, Color.black, DaggerfallUI.DaggerfallDefaultShadowPos * 2);
                DaggerfallUI.DefaultFont.DrawText(velocityTarget.magnitude.ToString(), Vector2.zero + (Vector2.right * 500), Vector2.one * 5, Color.green, Color.black, DaggerfallUI.DaggerfallDefaultShadowPos * 2);
                DaggerfallUI.DefaultFont.DrawText(windVectorCurrent.magnitude.ToString(), Vector2.zero + (Vector2.up * 50), Vector2.one * 5, Color.blue, Color.black, DaggerfallUI.DaggerfallDefaultShadowPos * 2);
            }

        }

        private void Update()
        {
            if (GameManager.IsGamePaused || InputManager.Instance.IsPaused || SaveLoadManager.Instance.LoadInProgress)
            {
                wasPaused = true;
                return;
            }

            if (wasPaused && Time.timeScale != 1)
            {
                timeScaleIndex = 0;
                SetTimeScale(currentTimeScale[timeScaleIndex], false);
            }

            wasPaused = false;

            if (IsSailing)
            {
                if (GameManager.Instance.PlayerMotor.OnExteriorWater == PlayerMotor.OnExteriorWaterMethod.Swimming)
                {
                }

                if (GameManager.Instance.SpeedChanger.isRunning)
                    GameManager.Instance.SpeedChanger.isRunning = false;

                if (GameManager.Instance.TransportManager.TransportMode != TransportModes.Foot)
                    GameManager.Instance.TransportManager.TransportMode = TransportModes.Foot;

                if (IsBeached(CurrentBoat) && MoveVectorCurrent.sqrMagnitude > 0)
                {
                    MoveVectorCurrent = Vector3.zero;
                    MoveVectorTarget = Vector3.zero;
                    ResetTimeScale();
                }

                if (timeScaleIndex != 0 && GameManager.Instance.AreEnemiesNearby())
                {
                    DaggerfallUI.SetMidScreenText("There are enemies nearby...");
                    ResetTimeScale();
                }

                //update input
                if (CurrentBoat != null)
                    inputCurrent = Vector2.MoveTowards(inputCurrent, inputTarget, CurrentBoat.modifierAnimation * (1 * Time.deltaTime));
                else
                    inputCurrent = Vector2.MoveTowards(inputCurrent, inputTarget, 1 * Time.deltaTime);

                if (windDirectionWidget)
                {
                    //update windDirectionWidget texture
                    int windDirectionWidgetTextureIndex = 0;
                    float angle = Vector3.SignedAngle(playerObject.transform.forward, windVectorCurrent,Vector3.up);
                    windDirectionWidgetTextureIndex = (int)((360 - ((angle / windDirectionWidgetInterval) * windDirectionWidgetInterval)) / windDirectionWidgetInterval);
                    if (angle < 0)
                        windDirectionWidgetTextureIndex = (int)(((-angle / windDirectionWidgetInterval) * windDirectionWidgetInterval) / windDirectionWidgetInterval);
                    windDirectionWidgetTextureIndex = Mathf.Clamp(windDirectionWidgetTextureIndex, 0, windDirectionWidgetFrameCount);
                    if (windDirectionWidgetTextureIndex == 0)
                        windDirectionWidgetTextureIndex = windDirectionWidgetFrameCount;
                    windDirectionWidgetTextureCurrent = windDirectionWidgetTextures[windDirectionWidgetFrameCount - windDirectionWidgetTextureIndex];
                }

                //re-seat player if their localposition is not the same as the seat
                if (playerObject.transform.position != CurrentBoat.DrivePosition.transform.position)
                    playerObject.transform.position = CurrentBoat.DrivePosition.transform.position;

                //keep player sat and froze
                GameManager.Instance.PlayerMotor.FreezeMotor = 1;

                if (InputManager.Instance.GetKeyDown(keyCodeToggleSail))
                {
                    if (sailPosition > 0 && CurrentBoat.SailsSquare.Count > 0 && !trimAutoSquareUpwind && InputManager.Instance.GetKey(keyCodeTrimMod) && (CurrentBoat.SailsLateen.Count > 0 || CurrentBoat.SailsGaff.Count > 0))
                    {
                        ToggleSquareSails();
                    }
                    else
                        ToggleSails();
                }

                if (InputManager.Instance.GetKeyDown(keyCodeDisembark) || InputManager.Instance.ActionStarted(InputManager.Actions.Transport))
                    StopSailingDelayed();

                if (InputManager.Instance.GetKeyDown(keyCodeToggleLight))
                    SetLights(CurrentBoat,!CurrentBoat.LightOn);

                //trim inputs
                if (!trimAuto)
                {
                    if (InputManager.Instance.GetKey(keyCodeTrimRight))
                    {
                        if (InputManager.Instance.GetKey(keyCodeTrimMod) || (CurrentBoat.SailsLateen.Count < 1 && CurrentBoat.SailsGaff.Count < 1))
                        {
                            if (trimAngleSquare < 45)
                                trimAngleSquare += 15 * Time.deltaTime;
                        }
                        else if (trimAngle < 90)
                        {
                            trimAngle += 15 * Time.deltaTime;
                        }
                    }

                    if (InputManager.Instance.GetKey(keyCodeTrimLeft))
                    {
                        if (InputManager.Instance.GetKey(keyCodeTrimMod) || (CurrentBoat.SailsLateen.Count < 1 && CurrentBoat.SailsGaff.Count < 1))
                        {
                            if (trimAngleSquare > -45)
                                trimAngleSquare -= 15 * Time.deltaTime;
                        }
                        else if (trimAngle > -90)
                        {
                            trimAngle -= 15 * Time.deltaTime;
                        }
                    }

                    foreach (Transform boom in CurrentBoat.Booms)
                    {
                        if (boom.name.Contains("Square"))
                            boom.localRotation = Quaternion.AngleAxis(trimAngleSquare, Vector3.up);
                        else
                            boom.localRotation = Quaternion.AngleAxis(trimAngle, Vector3.up);
                    }
                }

                //create force based on wind direction, boat direction and sail position
                if (sailPosition == 0)
                {
                    //using oars to move and turn
                    //ignore wind direction

                    if (!CurrentBoat.crewed)
                    {
                        //only drain fatigue while rowing if there is no crew to row for the player
                        if (MoveVectorTarget.sqrMagnitude > 0 || TurnTarget > 0)
                        {
                            //if target was non-zero last update, increment fatigue timer
                            if (oarModeTimer >= oarModeTime)
                            {
                                oarModeTimer = 0;
                                GameManager.Instance.PlayerEntity.DecreaseFatigue(PlayerEntity.DefaultFatigueLoss);
                            }
                            else
                                oarModeTimer += Time.deltaTime;
                        }
                    }

                    float forward = 0;
                    float right = 0;
                    float turn = 0;

                    if ((InputManager.Instance.HasAction(InputManager.Actions.MoveForwards) || InputManager.Instance.ToggleAutorun) && IsNodeOnWater(CurrentBoat, 0))
                        forward = 1f;
                    else if (InputManager.Instance.HasAction(InputManager.Actions.MoveBackwards) && IsNodeOnWater(CurrentBoat, 1))
                        forward = -1f;

                    if (InputManager.Instance.HasAction(InputManager.Actions.Run))
                    {
                        //if holding RUN, boat will strafe
                        if (InputManager.Instance.HasAction(InputManager.Actions.MoveRight) && IsNodeOnWater(CurrentBoat,2))
                            right = 0.5f;
                        else if (InputManager.Instance.HasAction(InputManager.Actions.MoveLeft) && IsNodeOnWater(CurrentBoat, 3))
                            right = -0.5f;
                    }
                    else
                    {
                        if (InputManager.Instance.HasAction(InputManager.Actions.MoveBackwards))
                        {
                            if (InputManager.Instance.HasAction(InputManager.Actions.MoveRight))
                                turn = -1f;
                            else if (InputManager.Instance.HasAction(InputManager.Actions.MoveLeft))
                                turn = 1f;
                        }
                        else
                        {
                            if (InputManager.Instance.HasAction(InputManager.Actions.MoveRight))
                                turn = 1f;
                            else if (InputManager.Instance.HasAction(InputManager.Actions.MoveLeft))
                                turn = -1f;
                        }
                    }

                    TurnTarget = turn;
                    MoveVectorTarget = (Vector3.forward * forward) + (Vector3.right * right);

                    if ((TurnTarget > 0 && !CanTurnRight(CurrentBoat)) || TurnTarget < 0 && !CanTurnLeft(CurrentBoat))
                    {
                        TurnTarget = -turn;
                        TurnCurrent = TurnTarget;
                    }

                    //update rudder position
                    /*if (CurrentBoat.RudderObject != null)
                        CurrentBoat.RudderObject.transform.localRotation = Quaternion.AngleAxis((Mathf.Sin(Time.time * 2) * 15f)* inputCurrent, Vector3.up);*/

                    if (CurrentBoat.RudderAnimator != null)
                    {
                        CurrentBoat.RudderAnimator.SetFloat("RowZ",inputCurrent.y);
                        CurrentBoat.RudderAnimator.SetFloat("RowX", inputCurrent.x);
                        CurrentBoat.RudderAnimator.SetFloat("RowSpeed", Mathf.Clamp(MoveVectorCurrent.magnitude/20,0.2f,2));
                    }
                }
                else
                {
                    if (!CanSail(CurrentBoat))
                    {
                        LowerSails();
                        MoveVectorCurrent = Vector3.zero;
                        MoveVectorTarget = Vector3.zero;
                        ResetTimeScale();
                    }

                    //turning sails logic
                    if (CurrentBoat.Sails.Count > 0)
                    {
                        float hullWindAngle = Vector3.SignedAngle(Vector3.ProjectOnPlane(CurrentBoat.GameObject.transform.forward, Vector3.up), Vector3.ProjectOnPlane(windVectorCurrent, Vector3.up), Vector3.up);
                        bool upwind = Mathf.Abs(hullWindAngle) > 90;
                        float maxAngle = 60;

                        foreach (Transform sail in CurrentBoat.Sails)
                        {
                            float windFactor = 0;
                            float sailWindAngle = Vector3.SignedAngle(Vector3.ProjectOnPlane(sail.forward, Vector3.up), Vector3.ProjectOnPlane(windVectorCurrent, Vector3.up), Vector3.up);
                            if (CurrentBoat.SailsSquare.Contains(sail))
                            {
                                if (sailWindAngle > 0 && sailWindAngle <= 90)
                                    windFactor = Mathf.Lerp(1, 0, sailWindAngle / 90);
                                else if (sailWindAngle > 90 && sailWindAngle <= 180)
                                    windFactor = Mathf.Lerp(0, -1, (sailWindAngle - 90) / 90);
                                else if (sailWindAngle <= 0 && sailWindAngle > -90)
                                    windFactor = Mathf.Lerp(1, 0, sailWindAngle / -90);
                                else if (sailWindAngle <= -90 && sailWindAngle > -180)
                                    windFactor = Mathf.Lerp(0, -1, (sailWindAngle + 90) / -90);
                            }
                            else if (CurrentBoat.SailsLateen.Contains(sail))
                            {
                                if (sailWindAngle > 0 && sailWindAngle <= 90)
                                    windFactor = Mathf.Lerp(0, -1, sailWindAngle / 90);
                                else if (sailWindAngle > 90 && sailWindAngle <= 180)
                                    windFactor = Mathf.Lerp(-1, 0, (sailWindAngle - 90) / 90);
                                else if (sailWindAngle <= 0 && sailWindAngle > -90)
                                    windFactor = Mathf.Lerp(0, 1, sailWindAngle / -90);
                                else if (sailWindAngle <= -90 && sailWindAngle > -180)
                                    windFactor = Mathf.Lerp(1, 0, (sailWindAngle + 90) / -90);
                            }
                            else if (CurrentBoat.SailsGaff.Contains(sail))
                            {
                                if (sailWindAngle > 0 && sailWindAngle <= 90)
                                    windFactor = Mathf.Lerp(0,1,sailWindAngle/90);
                                else if (sailWindAngle > 90 && sailWindAngle <= 180)
                                    windFactor = Mathf.Lerp(1, 0, (sailWindAngle-90) / 90);
                                else if (sailWindAngle <= 0 && sailWindAngle > -90)
                                    windFactor = Mathf.Lerp(0, -1, sailWindAngle / -90);
                                else if (sailWindAngle <= -90 && sailWindAngle > -180)
                                    windFactor = Mathf.Lerp(-1, 0, (sailWindAngle + 90) / -90);
                            }
                            else if (CurrentBoat.SailsStay.Contains(sail))
                            {
                                if (sailWindAngle > 0 && sailWindAngle <= 90)
                                    windFactor = Mathf.Lerp(0, 0.5f, sailWindAngle / 90);
                                else if (sailWindAngle > 90 && sailWindAngle <= 180)
                                    windFactor = Mathf.Lerp(0.5f, 1, (sailWindAngle - 90) / 90);
                                else if (sailWindAngle <= 0 && sailWindAngle > -90)
                                    windFactor = Mathf.Lerp(0, -0.5f, sailWindAngle / -90);
                                else if (sailWindAngle <= -90 && sailWindAngle > -180)
                                    windFactor = Mathf.Lerp(-0.5f, -1, (sailWindAngle + 90) / -90);
                            }

                            Animator animator = sail.GetComponent<Animator>();
                            if (animator != null)
                                animator.SetFloat("Wind", windFactor);

                            if (trimAutoSquareUpwind)
                            {
                                //stow square sails if travelling upwind
                                if (CurrentBoat.SailsSquare.Contains(sail))
                                {
                                    if (animator != null)
                                    {
                                        bool stowed = animator.GetBool("Stowed");
                                        if (upwind && !stowed)
                                        {
                                            animator.CrossFade("Stowed", sailAnimationSpeed);
                                            animator.SetBool("Stowed", true);
                                        }
                                        else if (!upwind && stowed)
                                        {
                                            animator.CrossFade("Unstowed", sailAnimationSpeed);
                                            animator.SetBool("Stowed", false);
                                        }
                                    }
                                }
                            }

                            /*if (trimAutoGaffLargeSquare)
                            {
                                //stow Gaff sails if has large square sail and travelling downwind
                                if (CurrentBoat.SailsGaff.Contains(sail))
                                {
                                    if (HasLargeSquareSail(CurrentBoat))
                                    {
                                        if (animator != null)
                                        {
                                            bool stowed = animator.GetBool("Stowed");
                                            if (upwind && stowed)
                                            {
                                                animator.CrossFade("Unstowed", sailAnimationSpeed);
                                                animator.SetBool("Stowed", false);
                                            }
                                            else if (!upwind && !stowed)
                                            {
                                                animator.CrossFade("Stowed", sailAnimationSpeed);
                                                animator.SetBool("Stowed", true);
                                            }
                                        }
                                    }
                                }
                            }*/
                        }

                        if (trimAuto)
                        {
                            //automatic sail control
                            float squareAngle = Mathf.Clamp(hullWindAngle, -45, 45); //point in wind direction
                            float lateenAngle = 0;
                            float gaffAngle = 0;
                            if (hullWindAngle > 0 && hullWindAngle <= 90)
                            {
                                gaffAngle = Mathf.Lerp(-90, -45, hullWindAngle / 90);
                                lateenAngle = gaffAngle;
                            }
                            else if (hullWindAngle > 90 && hullWindAngle <= 180)
                            {
                                gaffAngle = Mathf.Lerp(-45, 0, (hullWindAngle - 90) / 60);
                                lateenAngle = Mathf.Lerp(-45, 0, (hullWindAngle - 90) / 75);
                            }
                            else if (hullWindAngle <= 0 && hullWindAngle > -90)
                            {
                                gaffAngle = Mathf.Lerp(90, 45, hullWindAngle / -90);
                                lateenAngle = gaffAngle;
                            }
                            else if (hullWindAngle <= -90 && hullWindAngle > -180)
                            {
                                gaffAngle = Mathf.Lerp(45, 0, (hullWindAngle + 90) / -60);
                                lateenAngle = Mathf.Lerp(45, 0, (hullWindAngle + 90) / -75);
                            }

                            //limit gaff/square boom angle if both are used together
                            if (HasLargeSquareSailWithGaff(CurrentBoat))
                            {
                                if (gaffAngle > 30)
                                    gaffAngle = 30;
                                if (gaffAngle < -30)
                                    gaffAngle = -30;
                                if (squareAngle > 30)
                                    squareAngle = 30;
                                if (squareAngle < -30)
                                    squareAngle = -30;
                            }

                            foreach (Transform boom in CurrentBoat.Booms)
                            {
                                bool stowed = true;
                                Animator animator = null;
                                for (int i = 0; i < boom.childCount; i++)
                                {
                                    Transform child = boom.GetChild(i);
                                    if (!child.gameObject.activeSelf)
                                        continue;

                                    animator = child.GetComponent<Animator>();
                                    break;
                                }

                                if (animator != null)
                                    stowed = animator.GetBool("Stowed");

                                if (boom.name.Contains("Square"))
                                {
                                    if (stowed)
                                    {
                                        boom.localRotation = Quaternion.RotateTowards(boom.localRotation, Quaternion.AngleAxis(0, Vector3.up), 100 * Time.deltaTime);
                                    }
                                    else
                                    {
                                        boom.localRotation = Quaternion.RotateTowards(boom.localRotation, Quaternion.AngleAxis(squareAngle, Vector3.up), 100 * Time.deltaTime);
                                    }
                                }
                                else if (boom.name.Contains("Lateen"))
                                {
                                    if (stowed)
                                    {
                                        boom.localRotation = Quaternion.RotateTowards(boom.localRotation, Quaternion.AngleAxis(0, Vector3.up), 100 * Time.deltaTime);
                                    }
                                    else
                                    {
                                        boom.localRotation = Quaternion.RotateTowards(boom.localRotation, Quaternion.AngleAxis(lateenAngle, Vector3.up), 100 * Time.deltaTime);
                                    }
                                }
                                else if (boom.name.Contains("Gaff"))
                                {
                                    if (stowed)
                                    {
                                        boom.localRotation = Quaternion.RotateTowards(boom.localRotation, Quaternion.AngleAxis(0, Vector3.up), 100 * Time.deltaTime);
                                    }
                                    else
                                    {
                                        //if heading towards 0, slow
                                        //if heading away from 0, fast
                                        float gaffSpeed = 100;
                                        float currentAngle = Vector3.SignedAngle(boom.transform.forward, CurrentBoat.GameObject.transform.forward, CurrentBoat.GameObject.transform.up);
                                        if ((currentAngle < 0 && currentAngle < gaffAngle) || (currentAngle > 0 && currentAngle > gaffAngle))
                                            gaffSpeed = 300;
                                        boom.localRotation = Quaternion.RotateTowards(boom.localRotation, Quaternion.AngleAxis(gaffAngle, Vector3.up), gaffSpeed * Time.deltaTime);
                                    }
                                }
                            }
                        }
                    }

                    //using wind and sail to move and rudder to turn
                    float power = GetSailPower() * windVectorCurrent.magnitude;
                    float turn = 0;

                    if (InputManager.Instance.HasAction(InputManager.Actions.MoveRight))
                        turn = 1;
                    else if (InputManager.Instance.HasAction(InputManager.Actions.MoveLeft))
                        turn = -1;

                    TurnTarget = turn * ((MoveVectorCurrent.magnitude * CurrentBoat.modifierRudder)/10);
                    MoveVectorTarget = Vector3.forward * power;

                    if ((TurnTarget > 0 && !CanTurnRight(CurrentBoat)) || TurnTarget < 0 && !CanTurnLeft(CurrentBoat))
                    {
                        TurnTarget = -turn;
                        TurnCurrent = TurnTarget;
                    }


                    //update rudder position
                    /*if (CurrentBoat.RudderObject != null)
                        CurrentBoat.RudderObject.transform.localRotation = Quaternion.AngleAxis(-inputCurrent * 30f, Vector3.up);*/

                    if (CurrentBoat.RudderAnimator != null)
                    {
                        CurrentBoat.RudderAnimator.SetFloat("TurnAngle", inputCurrent.x);
                    }
                }

                TurnCurrent = Mathf.MoveTowards(
                    TurnCurrent,
                    TurnTarget * turnSpeed,
                    turnAccel * Time.deltaTime);
                velocityTarget = MoveVectorTarget * moveSpeed;
                MoveVectorCurrent = Vector3.MoveTowards(MoveVectorCurrent,
                    velocityTarget,
                    moveAccel * Time.deltaTime);

                //adjust wake
                if (MoveVectorCurrent.magnitude >= wakeThreshold && !CurrentBoat.AudioSourceFast.isPlaying)
                {
                    if (!DisableParticles)
                        CurrentBoat.WakeEmitter.Play();
                    PlayFast(CurrentBoat, true);
                }
                else if (MoveVectorCurrent.magnitude < wakeThreshold && !CurrentBoat.AudioSourceSlow.isPlaying)
                {
                    CurrentBoat.WakeEmitter.Stop();
                    PlaySlow(CurrentBoat, true);
                }

                CurrentBoat.WakeEmitterMain.startLifetimeMultiplier = Mathf.Clamp(((MoveVectorCurrent.magnitude*0.2f) / moveSpeedSail) * CurrentBoat.WakeObject.transform.localScale.x,1f,10f);
                CurrentBoat.WakeEmitterMain.startSize = (MoveVectorCurrent.magnitude * 0.2f) / moveSpeedSail;

                ParticleSystem.ForceOverLifetimeModule wakeEmitterForceOverLifetime = CurrentBoat.WakeEmitter.forceOverLifetime;
                wakeEmitterForceOverLifetime.space = ParticleSystemSimulationSpace.World;
                wakeEmitterForceOverLifetime.x = new ParticleSystem.MinMaxCurve((currentVector.x * 0.1f) / (CurrentBoat.WakeObject.transform.localScale.x));
                wakeEmitterForceOverLifetime.z = new ParticleSystem.MinMaxCurve((currentVector.z * 0.1f) / (CurrentBoat.WakeObject.transform.localScale.x));

                //time acceleration controls
                if (InputManager.Instance.GetKeyDown(keyCodeTimeScaleUp))
                    IncreaseTimeScale();

                if (InputManager.Instance.GetKeyDown(keyCodeTimeScaleDown))
                    DecreaseTimeScale();

                if (InputManager.Instance.GetKeyDown(keyCodeTimeScaleReset))
                    ResetTimeScale();
            }

            if (wave && waveObject != null && !(AnimatedWater != null && AWVertexWaves))
            {
                DaggerfallDateTime Now = DaggerfallUnity.Instance.WorldTime.Now;

                //animate waves
                if (waveFrameTimer < waveFrameTime)
                {
                    waveFrameTimer += Time.deltaTime;
                }
                else
                {
                    waveFrameTimer = 0;

                    if (Now.IsDay)
                    {
                        if (waveFrameIndex < waveFrames.Count - 1)
                            waveFrameIndex++;
                        else
                            waveFrameIndex = 0;
                    }
                    else
                    {
                        if (waveFrameIndex > 0)
                            waveFrameIndex--;
                        else
                            waveFrameIndex = waveFrames.Count - 1;
                    }

                    waveMeshRenderer.material.SetTexture("_MainTex", waveFrames[waveFrameIndex]);
                }

                /*currentObjectMeshRenderer.material.mainTextureScale = Vector2.one * 3;
                currentObjectMeshRenderer.material.mainTextureOffset =
                    (Vector2.up * (Mathf.Round(Time.time*10)*0.01f)) +
                    (Vector2.right * (Mathf.Round(Time.time * 10) * 0.1f));*/

                /*//get direction
                currentObject.transform.rotation = Quaternion.identity;
                currentObjectRenderer.sharedMaterial.mainTextureScale = Vector2.one * 10f;
                //get speed
                Vector3 currentSpeed = boatObject.transform.TransformDirection(-MoveVectorCurrent) * 0.01f;
                currentMove += new Vector2(currentSpeed.x, currentSpeed.z) * Time.deltaTime;
                currentObjectRenderer.sharedMaterial.mainTextureOffset = currentMove + (Vector2.down*Time.time*0.02f) + (Vector2.right*(Mathf.Round(Time.time * 5)*0.2f));*/
            }
        }

        private void LateUpdate()
        {
            if (GameManager.IsGamePaused || InputManager.Instance.IsPaused || SaveLoadManager.Instance.LoadInProgress)
                return;

            if (IsSailing)
            {

                if (lastBoatPosition != CurrentBoat.GameObject.transform.position || lastBoatDirection != CurrentBoat.GameObject.transform.forward)
                {
                    //position/direction changed
                    lastBoatPosition = CurrentBoat.GameObject.transform.position;
                    lastBoatDirection = CurrentBoat.GameObject.transform.forward;
                    UpdateCurrentBoatNodes();
                }

                if (!IsBeached(CurrentBoat))
                {
                    velocityCurrent = MoveVectorCurrent + CurrentBoat.GameObject.transform.InverseTransformDirection(currentVector);
                    CurrentBoat.GameObject.transform.Translate(velocityCurrent * Time.deltaTime);
                    CurrentBoat.GameObject.transform.Rotate(Vector3.up * TurnCurrent * Time.deltaTime);
                }
            }
            else
            {
                if (placing && InputManager.Instance.ActionComplete(InputManager.Actions.ActivateCenterObject) && !GameManager.Instance.PlayerEnterExit.IsPlayerInside && Time.time - placeTime > 0.2f)
                {
                    int hull = GetHullFromMessage(placeItem.message);
                    int variant = GetVariantFromMessage(placeItem.message);
                    PlaceBoatAtRayHit(hull, variant);
                }
            }

            bool IsOnABoat = false;

            if (AllBoats.Count > 0)
            {
                for (int i = 0; i < AllBoats.Count; i++)
                {
                    Boat boat = AllBoats[i];
                    if (!boat.GameObject.activeSelf)
                        continue;

                    //check if player is inside meshcollider bounds
                    Bounds boatBounds = boat.MeshCollider.sharedMesh.bounds;
                    Vector3 playerPos = boat.MeshObject.transform.InverseTransformPoint(GameManager.Instance.PlayerObject.transform.position);
                    playerPos.y = boatBounds.center.y;

#if (UNITY_EDITOR)
                    //DrawBox(boat.MeshObject.transform.position+ boat.MeshObject.transform.TransformVector(boatBounds.center), boat.MeshObject.transform.rotation, boatBounds.extents*2, Color.red, 1);
#endif

                    if (boatBounds.Contains(playerPos))
                        IsOnABoat = true;



                    //animate the boats
                    if (!IsBeached(boat))
                    {
                        float windStrength = windVectorCurrent.magnitude;
                        float time = Time.time + i;
                        float turning = 0;
                        if (boat == CurrentBoat)
                            turning = MoveVectorCurrent.magnitude < wakeThreshold ? 0 : (TurnCurrent / turnSpeedSail) * ((MoveVectorCurrent.magnitude * 0.1f) / moveSpeedSail);
                        if (AnimatedWater != null && AWVertexWaves)
                        {
                            bool crest = false;
                            Vector3 heightCenter = boat.Nodes[0].transform.position;
                            Vector3 heightFore = boat.Nodes[1].transform.position;
                            Vector3 heightAft = boat.Nodes[2].transform.position;
                            Vector3 heightStarboard = boat.Nodes[3].transform.position;
                            Vector3 heightPort = boat.Nodes[4].transform.position;
                            ModManager.Instance.SendModMessage(AnimatedWater.Title,
                                "getWaveHeights", new[] { heightCenter, heightFore, heightAft, heightStarboard, heightPort },
                                (msg, result) =>
                                {
                                    float[] heights = (float[])result;
                                    heightCenter = new Vector3(heightCenter.x, heightCenter.y + heights[0], heightCenter.z);
                                    heightFore = new Vector3(heightFore.x, heightFore.y + heights[1], heightFore.z);
                                    heightAft = new Vector3(heightAft.x, heightAft.y + heights[2], heightAft.z);
                                    heightStarboard = new Vector3(heightStarboard.x, heightStarboard.y + heights[3], heightStarboard.z);
                                    heightPort = new Vector3(heightPort.x, heightPort.y + heights[4], heightPort.z);

                                    if (heights[0] > heights[1] && heights[0] > heights[2])
                                        crest = true;
                                });

                            Vector3 targetPosition = boat.MeshObjectOffset + (Vector3.up * (heightCenter.y- boat.Nodes[0].transform.position.y));
                            boat.MeshObject.transform.localPosition = Vector3.Lerp(boat.MeshObject.transform.localPosition, targetPosition, Time.deltaTime * (5 * boat.modifierAnimation));
                            Vector3 angleVector = crest ? heightFore - heightCenter : heightFore - heightAft;
                            Quaternion targetRotation = Quaternion.Euler((Vector3.forward * (Mathf.Clamp(-turning * 5, -30, 30) + (Vector3.SignedAngle(boat.GameObject.transform.right, heightStarboard - heightPort, boat.GameObject.transform.forward) * 1)) +
                                (Vector3.right * (Vector3.SignedAngle(boat.GameObject.transform.forward, angleVector, boat.GameObject.transform.right) * 1))) *
                                boat.modifierAnimation);
                            boat.MeshObject.transform.localRotation = Quaternion.Lerp(boat.MeshObject.transform.localRotation,targetRotation,Time.deltaTime * (5 * boat.modifierAnimation));
                        }
                        else
                        {
                            boat.MeshObject.transform.localEulerAngles =
                                (Vector3.forward * (Mathf.Clamp(-turning * 5, -30, 30) + (Mathf.Sin(time * 0.5f * windStrength) * windStrength))) +
                                (Vector3.right * (Mathf.Sin(time * windStrength) * windStrength))
                                * boat.modifierAnimation;
                        }
                    }

                    //animate the flag
                    if (boat.FlagObject != null)
                    {
                        Vector3 apparentWind = windVectorCurrent + (boat.GameObject.transform.TransformDirection(-MoveVectorCurrent) * 0.1f);
                        boat.FlagObject.transform.forward = apparentWind.normalized;
                        boat.FlagEmitterMain.startSpeed = apparentWind.magnitude * 0.5f;
                    }
                }
            }

            if (IsOnABoat && !GameManager.Instance.PlayerEntity.IsWaterWalking)
                StartWaterwalking();
            else if (!IsOnABoat && GameManager.Instance.PlayerEntity.IsWaterWalking)
                EndWaterwalking();
        }


        private void FixedUpdate()
        {
            if (GameManager.IsGamePaused || InputManager.Instance.IsPaused || SaveLoadManager.Instance.LoadInProgress)
                return;

            if (!GameManager.Instance.PlayerEnterExit.IsPlayerInside && AllBoats.Count > 0)
            {
                Transform bestParent = GameObjectHelper.GetBestParent();
                //parent stuff
                foreach (GameObject enemy in ActiveGameObjectDatabase.GetActiveEnemyObjects())
                {
                    CharacterController controller = enemy.GetComponent<CharacterController>();
                    if (controller == null)
                        continue;

                    //raycast downwards
                    //if hit is player's currentboat and parent is not player's boatMeshObject, parent to boatMeshObject
                    Ray ray = new Ray(enemy.transform.position, Vector3.down);
                    RaycastHit hit = new RaycastHit();
                    if (Physics.Raycast(ray, out hit, controller.height))
                    {
                        Boat hitBoat = null;
                        foreach (Boat boat in AllBoats)
                        {
                            if (hit.collider.transform == boat.MeshCollider.transform)
                            {
                                hitBoat = boat;
                                break;
                            }
                        }

                        if (hitBoat != null && controller.isGrounded)
                        {
                            if (enemy.transform.parent != hitBoat.MeshObject.transform)
                            {
                                enemy.transform.SetParent(hitBoat.MeshObject.transform);
                                if (!parentedObjects.Contains(enemy))
                                    parentedObjects.Add(enemy);
                            }
                        }
                        else
                        {
                            if (enemy.transform.parent != bestParent)
                            {
                                enemy.transform.SetParent(bestParent);
                                if (parentedObjects.Contains(enemy))
                                    parentedObjects.Remove(enemy);
                            }
                        }
                    }
                    else
                    {
                        if (enemy.transform.parent != bestParent)
                        {
                            enemy.transform.SetParent(bestParent);
                            if (parentedObjects.Contains(enemy))
                                parentedObjects.Remove(enemy);
                        }
                    }
                }
            }

            if (wave && waveObject != null && !(AnimatedWater != null && AWVertexWaves))
            {
                DaggerfallDateTime Now = DaggerfallUnity.Instance.WorldTime.Now;

                //update current
                if (currentNeighbors != null)
                {
                    Vector3 boatPos = playerObject.transform.position;
                    float x = windVectorCurrent.x;
                    if (boatPos.x > terrainEdge * 0.75f && currentNeighbors[2, 1])
                        x = 1;
                    else if (boatPos.x < terrainEdge * 0.25f && currentNeighbors[0, 1])
                        x = -1;

                    float z = windVectorCurrent.z;
                    if (boatPos.z > terrainEdge * 0.75f && currentNeighbors[1, 0])
                        z = 1;
                    else if (boatPos.z < terrainEdge * 0.25f && currentNeighbors[1, 2])
                        z = -1;

                    currentVector = (new Vector3(x, 0, z)).normalized * windVectorCurrent.magnitude * 0.5f;

                    if (currentNeighbors[1, 1])
                        currentVector *= -1;

                    if (Now.IsNight)
                        currentVector *= -1;
                }
                else
                    currentVector = windVectorCurrent * 0.5f;

                if (currentVector != currentVectorPrevious)
                {
                    if (OnUpdateCurrent != null)
                        OnUpdateCurrent(currentVector);

                    currentVectorPrevious = currentVector;
                }
            }
        }

        int GetHullFromMessage(int message)
        {
            int hull = (message/10) % 10;
            Debug.Log("COME SAIL AWAY - ITEM HULL IS " + hull.ToString());
            return hull;
        }

        int GetVariantFromMessage(int message)
        {
            int variant = message % 10;
            Debug.Log("COME SAIL AWAY - ITEM VARIANT IS " + variant.ToString());
            return variant;
        }

        float GetSailPower()
        {
            float power = 0;

            if (CurrentBoat.Sails.Count < 1)
                return power;

            foreach (Transform sail in CurrentBoat.Sails)
            {
                if (sail.gameObject.GetComponent<Animator>().GetBool("Stowed"))
                    continue;

                float sailPower = 0;
                float angleHull = Vector3.Angle(Vector3.ProjectOnPlane(CurrentBoat.GameObject.transform.forward, Vector3.up), Vector3.ProjectOnPlane(sail.forward, Vector3.up));
                float angleWind = Vector3.Angle(Vector3.ProjectOnPlane(windVectorCurrent, Vector3.up), Vector3.ProjectOnPlane(sail.forward, Vector3.up));

                if (CurrentBoat.SailsLateen.Contains(sail))
                {
                    //is a lateen sail
                    if (angleWind <= 135)
                        sailPower = Mathf.Lerp(50, 100, angleWind / 135) / 100;
                    else if (angleWind <= 165)
                        sailPower = Mathf.Lerp(100, 50, (angleWind - 135) / 30) / 100;
                    else
                        sailPower = 0;

                    //if lateen sail, apply bad tack modifier
                    if (Vector3.SignedAngle(sail.forward, windVectorCurrent, Vector3.up) > 0)
                        sailPower *= 0.85f;

                    if (sailPower < 0)
                        sailPower *= 0.5f;
                }
                else if (CurrentBoat.SailsGaff.Contains(sail))
                {
                    //is a gaff sail
                    if (angleWind <= 135)
                        sailPower = Mathf.LerpUnclamped(50, 100, angleWind / 135) / 100;
                    else
                        sailPower = Mathf.LerpUnclamped(100, 0, (angleWind - 135) / 15) / 100;

                    if (sailPower < 0)
                        sailPower *= 0.25f;
                }
                else if (CurrentBoat.SailsStay.Contains(sail))
                {
                    //is a staysail
                    if (angleWind <= 135)
                        sailPower = Mathf.LerpUnclamped(20, 80, angleWind / 135) / 100;
                    else
                        sailPower = Mathf.LerpUnclamped(80, 0, (angleWind - 135) / 15) / 100;

                    if (sailPower < 0)
                        sailPower *= 0.25f;
                }
                else
                {
                    //is a square sail
                    //best power when pointing in the same direction as the wind
                    //no power when perpendicular to the wind
                    sailPower = Mathf.LerpUnclamped(100, 0, angleWind / 90) / 100;

                    if (sailPower < 0)
                        sailPower *= 2f;
                }

                //further modify with angle from hull
                //sailPower *= Mathf.Clamp(100, 0, angleHull / 90);

                if (CurrentBoat.SailsSmall.Contains(sail))
                {
                    if (CurrentBoat.SailsGaff.Contains(sail))
                    {
                        //is a gaff sail
                        sailPower *= 0.5f;
                    }
                    else if (CurrentBoat.SailsStay.Contains(sail))
                    {
                        //is a staysail
                        sailPower *= 0.3f;
                    }
                    else
                        sailPower *= 0.4f;
                }
                else if (CurrentBoat.SailsLarge.Contains(sail))
                {
                    sailPower *= 1.5f;
                }

                //Debug.Log("COME SAIL AWAY - SAIL POWER IS " + sailPower.ToString());
                power += sailPower;
            }

            //Debug.Log("COME SAIL AWAY - TOTAL POWER IS " + power.ToString());
            return power;
        }

        void ToggleSails()
        {
            if (CurrentBoat.Sails.Count < 1)
            {
                DaggerfallUI.AddHUDText("Boat does not have any sail.");
                return;
            }

            if (sailPosition == 0)
                RaiseSails();
            else
                LowerSails();
        }

        public void RaiseSails()
        {
            if (!CanSail(CurrentBoat))
            {
                DaggerfallUI.AddHUDText("Unable to raise sail. Boat is obstructed.");
                return;
            }

            DaggerfallUI.AddHUDText("Sail raised!");

            //GameObjectHelper.ChangeDaggerfallMeshGameObject(boatMesh.GetComponent<DaggerfallMesh>(), 41501);

            bool upwind = Mathf.Abs(Vector3.SignedAngle(Vector3.ProjectOnPlane(CurrentBoat.GameObject.transform.forward, Vector3.up), Vector3.ProjectOnPlane(windVectorCurrent, Vector3.up), Vector3.up)) > 90;

            if (CurrentBoat.Sails.Count > 0)
            {
                foreach (Transform sail in CurrentBoat.Sails)
                {
                    if (CurrentBoat.SailsSquare.Contains(sail) && trimAutoSquareUpwind)
                    {
                        //skip raising square sail if heading upwind
                        if (upwind)
                            continue;
                    }
                    /*if (CurrentBoat.SailsGaff.Contains(sail) && trimAutoGaffLargeSquare)
                    {
                        //skip raising gaff sail if heading downwind and has large square sails
                        if (!upwind && HasLargeSquareSail(CurrentBoat))
                            continue;
                    }*/

                    Animator animator = sail.GetComponent<Animator>();
                    if (animator != null)
                    {
                        animator.CrossFade("Unstowed", sailAnimationSpeed);
                        animator.SetBool("Stowed", false);
                    }
                }
            }

            sailPosition = 1;

            CurrentBoat.DFAudioSource.PlayOneShot(SoundClips.EquipStaff,1,DaggerfallUnity.Settings.SoundVolume*sfxVolume);


            if (CurrentBoat.RudderAnimator != null)
            {
                CurrentBoat.RudderAnimator.SetBool("Sailing", true);
            }
        }

        public void LowerSails()
        {
            DaggerfallUI.AddHUDText("Sail lowered!");

            //GameObjectHelper.ChangeDaggerfallMeshGameObject(boatMesh.GetComponent<DaggerfallMesh>(), 41502);

            if (CurrentBoat.Sails.Count > 0)
            {
                foreach (Transform sail in CurrentBoat.Sails)
                {
                    Animator animator = sail.GetComponent<Animator>();
                    if (animator != null)
                    {
                        animator.CrossFade("Stowed", sailAnimationSpeed);
                        animator.SetBool("Stowed", true);
                    }
                }
            }

            sailPosition = 0;

            CurrentBoat.DFAudioSource.PlayOneShot(SoundClips.EquipClothing, 1, DaggerfallUnity.Settings.SoundVolume * sfxVolume);

            if (CurrentBoat.RudderAnimator != null)
            {
                CurrentBoat.RudderAnimator.SetBool("Sailing", false);
            }
        }

        bool HasLargeSquareSailWithGaff(Boat boat)
        {
            bool hasLargeSquare = false;
            if (boat.SailsSquare.Count > 0)
            {
                foreach (Transform square in boat.SailsSquare)
                {
                    if (!boat.SailsSmall.Contains(square))
                    {
                        hasLargeSquare = true;
                        break;
                    }
                }
            }
            return hasLargeSquare && boat.SailsGaff.Count > 0;
        }
        void ToggleSquareSails()
        {
            if (CurrentBoat.SailsSquare.Count > 0)
            {
                bool value = CurrentBoat.SailsSquare[0].gameObject.GetComponent<Animator>().GetBool("Stowed");

                if (value)
                {
                    DaggerfallUI.AddHUDText("Square sails raised!");

                    foreach (Transform sail in CurrentBoat.SailsSquare)
                    {
                        Animator animator = sail.GetComponent<Animator>();
                        if (animator != null)
                        {
                            animator.CrossFade("Unstowed", sailAnimationSpeed);
                            animator.SetBool("Stowed", false);
                        }
                    }
                }
                else
                {
                    DaggerfallUI.AddHUDText("Square sails lowered!");

                    foreach (Transform sail in CurrentBoat.SailsSquare)
                    {
                        Animator animator = sail.GetComponent<Animator>();
                        if (animator != null)
                        {
                            animator.CrossFade("Stowed", sailAnimationSpeed);
                            animator.SetBool("Stowed", true);
                        }
                    }
                }
            }
        }

        private static void BoardBoat(RaycastHit hit)
        {
            /*if (hit.collider.gameObject.GetInstanceID() != Instance.CurrentBoat.BoardTrigger.GetInstanceID())
                return;*/

            Boat hitBoat = null;
            foreach (Boat boat in Instance.AllBoats)
            {
                if (hit.transform.root.gameObject.GetInstanceID() == boat.GameObject.GetInstanceID())
                {
                    hitBoat = boat;
                    break;
                }
            }

            if (hitBoat == null)
                return;

            //move player to boarding position
            Transform BoardPositionTransform = hit.transform.parent.GetChild(hit.transform.GetSiblingIndex() - 1);
            GameManager.Instance.PlayerObject.transform.position = BoardPositionTransform.position;
            GameManager.Instance.PlayerMouseLook.SetHorizontalFacing(BoardPositionTransform.forward);
            GameObjectHelper.AlignControllerToGround(GameManager.Instance.PlayerObject.GetComponent<CharacterController>());
        }

        private static void ActivateRudder(RaycastHit hit)
        {
            Boat hitBoat = null;
            foreach (Boat boat in Instance.AllBoats)
            {
                if (hit.transform.root.gameObject.GetInstanceID() == boat.GameObject.GetInstanceID())
                {
                    hitBoat = boat;
                    break;
                }
            }

            if (hitBoat == null)
                return;

            if (GameManager.Instance.PlayerActivate.CurrentMode == PlayerActivateModes.Steal)
            {
                //pack the boat if able
                if (hitBoat.packable)
                    Instance.PackBoat(hitBoat, true);
            }
            else
            {
                //sail the boat
                if (Instance.IsSailing && hitBoat == Instance.CurrentBoat)
                    Instance.StopSailingDelayed();
                else
                    Instance.StartSailing(hitBoat);
            }
        }

        private static void PickVariant(RaycastHit hit)
        {
            Boat hitBoat = null;
            foreach (Boat boat in Instance.AllBoats)
            {
                if (hit.transform.root.gameObject.GetInstanceID() == boat.GameObject.GetInstanceID())
                {
                    hitBoat = boat;
                    break;
                }
            }

            if (hitBoat == null)
                return;

            //open the menu
            if (!Instance.IsSailing)
                Instance.OpenBoatVariantPicker(hitBoat);
        }

        private static void CheckBoatStatus(RaycastHit hit)
        {
            Boat hitBoat = null;
            foreach (Boat boat in Instance.AllBoats)
            {
                if (hit.transform.root.gameObject.GetInstanceID() == boat.GameObject.GetInstanceID())
                {
                    hitBoat = boat;
                    break;
                }
            }

            if (hitBoat == null)
                return;

            //check boat status
            DaggerfallUI.MessageBox("Nice Boat!");
        }

        private static void TriggerDoor(RaycastHit hit)
        {
            Boat hitBoat = null;
            foreach (Boat boat in Instance.AllBoats)
            {
                if (hit.transform.root.gameObject.GetInstanceID() == boat.GameObject.GetInstanceID())
                {
                    hitBoat = boat;
                    break;
                }
            }

            if (hitBoat == null)
                return;

            Animator animator = hit.transform.parent.GetComponent<Animator>();
            if (animator == null)
                return;

            bool state = animator.GetBool("Opened");

            animator.SetBool("Opened", !state);

            SoundClips clip = SoundClips.NormalDoorOpen;
            if (state)
                clip = SoundClips.NormalDoorClose;

            hitBoat.DFAudioSource.PlayClipAtPoint(clip, hit.transform.position);
        }

        private static void OpenBoatCargo(RaycastHit hit)
        {
            Boat hitBoat = null;
            foreach (Boat boat in Instance.AllBoats)
            {
                if (hit.transform.root.gameObject.GetInstanceID() == boat.GameObject.GetInstanceID())
                {
                    hitBoat = boat;
                    break;
                }
            }

            Instance.OpenCargo(hitBoat);
        }

        public void StartSailing(Boat boat)
        {
            DaggerfallUI.AddHUDText("You control the boat!");

            CurrentBoat = boat;

            if (GameManager.Instance.TransportManager.TransportMode != TransportModes.Foot)
                GameManager.Instance.TransportManager.TransportMode = TransportModes.Foot;

            //give player a temporary ship while sailing a crewed if they already do not own a vanilla ship
            if (boat.crewed && !DaggerfallBankManager.OwnsShip)
            {
                TemporaryShip = true;
                DaggerfallBankManager.AssignShipToPlayer(ShipType.Small);
            }

            boat.WakeEmitter.Stop();

            Vector3 oldForward = playerObject.transform.forward;

            playerObject.transform.SetParent(boat.GameObject.transform);
            GameManager.Instance.PlayerMouseLook.SetFacing(0,0);
            playerObject.transform.position = boat.DrivePosition.transform.position;
            GameManager.Instance.PlayerMotor.smoothFollowerLerpSpeed = 250f;

            boat.MapPixel = GameManager.Instance.PlayerGPS.CurrentMapPixel;

            UpdateBoatCargoMod(boat);
            UpdateCurrentBoatNodes();

            boat.IdleObject.SetActive(false);
            boat.ActiveObject.SetActive(true);

            GameManager.Instance.PlayerObject.GetComponent<PlayerFootsteps>().enabled = false;

            //GameManager.Instance.PlayerEntity.IsWaterWalking = true;
            if (CurrentBoat.RudderEmitters.Count > 0 && !DisableParticles)
            {
                foreach (ParticleSystem rudderEmitter in CurrentBoat.RudderEmitters)
                {
                    rudderEmitter.Play();
                }
            }

            //add toggle to enable stencil mask?
            GameManager.Instance.MainCamera.renderingPath = RenderingPath.Forward;
            GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>().renderingPath = RenderingPath.Forward;

            /*if (AnimatedWater != null && AWVertexWaves)
            {
                GameManager.Instance.MainCamera.renderingPath = RenderingPath.Forward;
                GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>().renderingPath = RenderingPath.Forward;
            }*/

            if (CurrentBoat.RudderAnimator != null)
                CurrentBoat.RudderAnimator.CrossFade("Rowing",1);

            if (OnUpdateSailing != null)
                OnUpdateSailing(true);

        }

        public void StopSailing()
        {
            //disable autorun
            if (InputManager.Instance.ToggleAutorun)
                InputManager.Instance.ToggleAutorun = false;

            if (TemporaryShip)
            {
                //remove temporary ship
                SaveLoadManager.StateManager.RemovePermanentScene(StreamingWorld.GetSceneName(5, 5));
                SaveLoadManager.StateManager.RemovePermanentScene(DaggerfallInterior.GetSceneName(2102157, BuildingDirectory.buildingKey0));
                DaggerfallBankManager.AssignShipToPlayer(ShipType.None);
                TemporaryShip = false;
            }

            Boat boatlast = CurrentBoat;
            DaggerfallUI.AddHUDText("You stop controlling the boat!");

            if (sailPosition > 0)
                LowerSails();
            
            CurrentBoat.WakeEmitter.Stop();

            sailPosition = 0;
            MoveVectorTarget = Vector3.zero;
            MoveVectorCurrent = Vector3.zero;
            TurnCurrent = 0;
            TurnTarget = 0;

            lastWeight = 0;

            CurrentBoat.MapPixel = GameManager.Instance.PlayerGPS.CurrentMapPixel;

            ResetTimeScale();

            //hide sailing objects
            CurrentBoat.ActiveObject.SetActive(false);
            CurrentBoat.IdleObject.SetActive(true);

            GameManager.Instance.PlayerObject.GetComponent<PlayerFootsteps>().enabled = true;

            //GameManager.Instance.PlayerEntity.IsWaterWalking = false;

            if (CurrentBoat.RudderAnimator != null)
                CurrentBoat.RudderAnimator.CrossFade("Disembarked", 1);

            if (CurrentBoat.RudderEmitters.Count > 0)
            {
                foreach (ParticleSystem rudderEmitter in CurrentBoat.RudderEmitters)
                {
                    rudderEmitter.Stop();
                }
            }

            CurrentBoat = null;

            Vector3 oldForward = playerObject.transform.forward;
            playerObject.transform.SetParent(null, true);
            playerObject.transform.position = boatlast.DrivePosition.transform.position;
            GameManager.Instance.PlayerMouseLook.SetHorizontalFacing(oldForward);
            GameManager.Instance.PlayerMotor.smoothFollowerLerpSpeed = 25f;
            GameManager.Instance.PlayerMotor.FreezeMotor = 0;

            GameManager.Instance.MainCamera.renderingPath = RenderingPath.DeferredShading;
            GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>().renderingPath = RenderingPath.DeferredShading;

            /*if (AnimatedWater != null && AWVertexWaves)
            {
                GameManager.Instance.MainCamera.renderingPath = RenderingPath.DeferredShading;
                GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>().renderingPath = RenderingPath.DeferredShading;
            }*/

            if (OnUpdateSailing != null)
                OnUpdateSailing(false);
        }

        public void StopSailingDelayed()
        {
            if (disembarking != null)
                return;

            disembarking = StopSailingCoroutine();
            StartCoroutine(disembarking);
        }

        IEnumerator StopSailingCoroutine()
        {
            //disable autorun
            if (InputManager.Instance.ToggleAutorun)
                InputManager.Instance.ToggleAutorun = false;

            //remove temporary ship
            if (TemporaryShip)
            {
                SaveLoadManager.StateManager.RemovePermanentScene(StreamingWorld.GetSceneName(5, 5));
                SaveLoadManager.StateManager.RemovePermanentScene(DaggerfallInterior.GetSceneName(2102157, BuildingDirectory.buildingKey0));
                DaggerfallBankManager.AssignShipToPlayer(ShipType.None);
                TemporaryShip = false;
            }

            Boat boatlast = CurrentBoat;
            DaggerfallUI.AddHUDText("You stop controlling the boat!");

            if (sailPosition > 0)
                LowerSails();

            CurrentBoat.WakeEmitter.Stop();

            sailPosition = 0;
            MoveVectorTarget = Vector3.zero;
            MoveVectorCurrent = Vector3.zero;
            TurnCurrent = 0;
            TurnTarget = 0;

            lastWeight = 0;

            CurrentBoat.MapPixel = GameManager.Instance.PlayerGPS.CurrentMapPixel;

            ResetTimeScale();

            //boatObject.GetComponent<Collider>().enabled = true;

            //hide sailing objects
            CurrentBoat.ActiveObject.SetActive(false);
            CurrentBoat.IdleObject.SetActive(true);

            GameManager.Instance.PlayerObject.GetComponent<PlayerFootsteps>().enabled = true;

            if (CurrentBoat.RudderAnimator != null)
                CurrentBoat.RudderAnimator.CrossFade("Disembarked", 1);

            if (CurrentBoat.RudderEmitters.Count > 0)
            {
                foreach (ParticleSystem rudderEmitter in CurrentBoat.RudderEmitters)
                {
                    rudderEmitter.Stop();
                }
            }

            yield return new WaitForEndOfFrame();

            CurrentBoat = null;

            Vector3 oldForward = playerObject.transform.forward;
            playerObject.transform.SetParent(null, true);
            GameManager.Instance.PlayerMouseLook.SetHorizontalFacing(oldForward);
            GameManager.Instance.PlayerMotor.smoothFollowerLerpSpeed = 25f;
            //GameManager.Instance.PlayerMotor.FreezeMotor = 0;

            GameManager.Instance.MainCamera.renderingPath = RenderingPath.DeferredShading;
            GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>().renderingPath = RenderingPath.DeferredShading;
            /*if (AnimatedWater != null && AWVertexWaves)
            {
                GameManager.Instance.MainCamera.renderingPath = RenderingPath.DeferredShading;
                GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>().renderingPath = RenderingPath.DeferredShading;
            }*/

            while (GameManager.Instance.PlayerMotor.FreezeMotor > 0)
            {
                playerObject.transform.position = boatlast.DrivePosition.transform.position;
                yield return new WaitForEndOfFrame();
            }

            if (OnUpdateSailing != null)
                OnUpdateSailing(false);

            disembarking = null;
        }

        public void StartWaterwalking()
        {
            //Thanks Exco!
            EntityEffectManager manager = GameManager.Instance.PlayerEffectManager as EntityEffectManager;
            if (manager)
            {
                bool isWaterwalking = false;
                foreach (LiveEffectBundle bundle in manager.EffectBundles)
                {
                    if (bundle.name == "I'm On A Boat")
                    {
                        isWaterwalking = true;
                        break;
                    }
                }

                if (!isWaterwalking)
                {
                    EffectBundleSettings settings = new EffectBundleSettings
                    {
                        Name = "I'm On A Boat",
                        BundleType = BundleTypes.Spell,
                        TargetType = TargetTypes.CasterOnly,
                        Effects = new EffectEntry[]
                        {
                        // Super long duration
                        new EffectEntry(WaterWalkingSilent.EffectKey, new EffectSettings { DurationBase = 90000, DurationPlus = 0, DurationPerLevel = 1 })
                        }
                    };
                    EntityEffectBundle newBundle = new EntityEffectBundle(settings, GameManager.Instance.PlayerEntityBehaviour);
                    manager.AssignBundle(newBundle, AssignBundleFlags.BypassSavingThrows);
                }
            }
        }

        public void EndWaterwalking()
        {
            EntityEffectManager manager = GameManager.Instance.PlayerEffectManager as EntityEffectManager;
            if (manager)
            {
                foreach (LiveEffectBundle bundle in manager.EffectBundles)
                {
                    if (bundle.name == "I'm On A Boat" || bundle.name == "Jesus Mode")
                    {
                        manager.RemoveBundle(bundle);
                        break;
                    }
                }
            }
        }

        bool CanSail(Boat boat)
        {
            foreach (int index in boat.NodeTileMapIndices)
            {
                if (index != 0)
                    return false;
            }

            return true;
        }
        bool IsBeached(Boat boat)
        {
            int count = 0;
            foreach (int index in boat.NodeTileMapIndices)
            {
                if (index != 0)
                    count++;

                if (count > 3)
                    return true;
            }

            return false;
        }

        bool IsNodeOnWater(Boat boat, int index)
        {
            if (boat.NodeTileMapIndices[index] != 0)
                return false;

            return true;
        }

        void IncreaseTimeScale()
        {
            if (GameManager.Instance.AreEnemiesNearby())
            {
                DaggerfallUI.SetMidScreenText("There are enemies nearby...");
                return;
            }

            if (timeScaleIndex < currentTimeScale.Length-1)
            {
                timeScaleIndex++;
                SetTimeScale(currentTimeScale[timeScaleIndex]);
            }
        }

        void DecreaseTimeScale()
        {
            if (timeScaleIndex > 0 )
            {
                timeScaleIndex--;
                SetTimeScale(currentTimeScale[timeScaleIndex]);
            }
        }

        void ResetTimeScale(bool message = true)
        {
            if (timeScaleIndex != 0)
            {
                timeScaleIndex = 0;
                SetTimeScale(currentTimeScale[timeScaleIndex], message);
            }
        }

        void SetTimeScale(float scale, bool message = true)
        {
            Time.timeScale = scale;
            Time.fixedDeltaTime = 0.02f * scale;

            if (message)
                DaggerfallUI.SetMidScreenText("Time scale set to " + scale.ToString() + ".", 3 * scale);
        }

        public void StartPlacing(DaggerfallUnityItem item, ItemCollection itemCollection)
        {
            if (placing)
                return;

            placing = true;
            placeTime = Time.time;
            placeItem = item;
            placeItemCollection = itemCollection;

            DaggerfallUI.SetMidScreenText("Place the boat in water", 3);
        }

        public void StopPlacing()
        {
            placing = false;
            placeItem = null;
        }

        public string[] hullNames = new string[5]
        {
            "Rowboat",
            "Large Boat",
            "Small Ship",
            "Large Galley",
            "Carrack",
        };

        public int[] hullPrices = new int[5]
        {
            4000,
            8000,
            100000,
            200000,
            150000,
        };

        public float[] hullWeights = new float[5]
        {
            30,
            120,
            2400,
            48000,
            240000,
        };

        public string[] variantNames = new string[10]
        {
            "I",
            "II",
            "III",
            "IV",
            "V",
            "VI",
            "VII",
            "VIII",
            "IX",
            "X",
        };

        public void PackBoat(Boat boat, bool item = false)
        {

            if (item)
            {
                ItemGroups itemGroup = ItemGroups.UselessItems2;
                int templateIndex = ItemBoatParts.templateIndex;

                /*if (boat.crewed)
                {
                    //check if player has a deed with a matching UID
                    bool hasDeed = false;
                    for (int i = 0; i < GameManager.Instance.PlayerEntity.Items.Count; i++)
                    {
                        DaggerfallUnityItem inventoryItem = GameManager.Instance.PlayerEntity.Items.GetItem(i);
                        if (inventoryItem.TemplateIndex != ItemBoatDeed.templateIndex)
                            continue;

                        if (inventoryItem.UID == boat.uid)
                        {
                            hasDeed = true;
                            break;
                        }
                    }

                    if (!hasDeed)
                    {
                        templateIndex = ItemBoatDeed.templateIndex;
                    }
                }*/

                DaggerfallUI.AddHUDText("You store the boat in your inventory");

                DaggerfallUnityItem newBoatItem = ItemBuilder.CreateItem(itemGroup, templateIndex);

                //TO-DO: When variants are implemented, save them here (probably in message)
                newBoatItem.message = (boat.hull * 10) + boat.variant;
                //adjust price based on thing?

                newBoatItem.value = Instance.hullPrices[boat.hull];
                newBoatItem.weightInKg = Instance.hullWeights[boat.hull];
                newBoatItem.shortName += " " + Instance.hullNames[boat.hull] + " '" + Instance.variantNames[boat.variant] + "'";

                if (boat.Cargo.Items.Count > 0)
                {
                    ItemCollection PackedCargo = new ItemCollection();
                    float cargoWeight = boat.Cargo.Items.GetWeight();
                    PackedCargo.TransferAll(boat.Cargo.Items);
                    newBoatItem.weightInKg += cargoWeight;
                    PackedCargoes.Add(newBoatItem.UID, PackedCargo);
                }

                GameManager.Instance.PlayerEntity.Items.AddItem(newBoatItem);
            }

            boat.MapPixel = null;

            Destroy(boat.GameObject);
            AllBoats.Remove(boat);
        }

        public Boat PlaceBoat(Vector3 position, Vector3 direction, int hull = 0, int variant = 0, Terrain terrain = null)
        {
            Boat newBoat = new Boat(hull,variant);
            SpawnBoat(newBoat);
            AllBoats.Add(newBoat);

            SetBoatPositionAndDirection(newBoat, position, direction, terrain);

            PlaySlow(newBoat);

            return newBoat;
        }

        public void PlaceBoat(Boat newBoat, Vector3 position, Vector3 direction, Terrain terrain = null)
        {
            SpawnBoat(newBoat);
            SetBoatPositionAndDirection(newBoat, position, direction, terrain);
            PlaySlow(newBoat);
        }

        public void PlaceBoat(Boat newBoat, Vector3 position, Vector3 direction, DFPosition mapPixel = null)
        {
            SpawnBoat(newBoat);
            SetBoatPositionAndDirection(newBoat, position, direction, mapPixel);
            PlaySlow(newBoat);
        }

        public void RepositionBoat(Boat boat, Vector3 position, Vector3 direction, Terrain terrain = null)
        {
            SetBoatPositionAndDirection(boat, position, direction, terrain);

            UpdateBoatVisibility(boat);

            PlaySlow(boat);
        }

        public void RepositionBoat(Boat boat, Vector3 position, Vector3 direction, DFPosition mapPixel = null)
        {
            SetBoatPositionAndDirection(boat, position, direction, mapPixel);

            UpdateBoatVisibility(boat);

            PlaySlow(boat);
        }

        void PlaceBoatAtRayHit(string[] args)
        {
            int hull = 0;
            int variant = 0;
            if (args.Length == 0)
            {
                variant = UnityEngine.Random.Range(0,7);
            }
            else if (args.Length == 1)
            {
                hull = Convert.ToInt32(args[0]);
                if (hull == 0)
                    variant = UnityEngine.Random.Range(0, 7);
            }
            else if (args.Length == 2)
            {
                hull = Convert.ToInt32(args[0]);
                variant = Convert.ToInt32(args[1]);
            }

            PlaceBoatAtRayHit(hull, variant);
        }

        void PlaceBoatAtRayHit(int hull = 0, int variant = 0)
        {
            Ray ray = new Ray(GameManager.Instance.MainCameraObject.transform.position, GameManager.Instance.MainCameraObject.transform.forward);
            RaycastHit hit = new RaycastHit();
            LayerMask layerMask = ~(1 << LayerMask.NameToLayer("Player"));
            layerMask = layerMask & ~(1 << LayerMask.NameToLayer("Ignore Raycast"));
            if (Physics.Raycast(ray, out hit, 100f, layerMask))
            {
                if (hit.collider.GetComponent<Terrain>() != null && GetTileMapIndexAtPosition(hit.point, hit.collider.transform) == 0)
                {
                    //must have hit terrain and water tile
                    DaggerfallUI.AddHUDText("Boat placed!");

                    Boat boat = null;

                    if (placeItem != null && placeItem.TemplateIndex == ItemBoatDeed.templateIndex)
                    {
                        //if spawned using a deed, check if boat already exists
                        boat = GetPlacedBoatWithUID(placeItem.UID);
                    }

                    if (boat == null)
                        boat = PlaceBoat(new Vector3(hit.point.x, hit.point.y, hit.point.z), GameManager.Instance.PlayerObject.transform.right, hull, variant, hit.collider.GetComponent<Terrain>());
                    else
                        RepositionBoat(boat, new Vector3(hit.point.x, hit.point.y, hit.point.z), GameManager.Instance.PlayerObject.transform.right, hit.collider.GetComponent<Terrain>());

                    //if spawned from item, check if it has packed cargo
                    if (placeItem != null)
                    {
                        boat.uid = placeItem.UID;

                        if (PackedCargoes.ContainsKey(placeItem.UID))
                        {
                            ItemCollection packedCargo = new ItemCollection();
                            PackedCargoes.TryGetValue(placeItem.UID, out packedCargo);
                            boat.Cargo.Items.TransferAll(packedCargo);
                        }

                        if (!boat.crewed)
                        {
                            placeItemCollection.RemoveItem(placeItem);
                        }
                    }

                    StopPlacing();
                    return;
                }
                else
                {
                    DaggerfallUI.SetMidScreenText("Boat can only be placed on water!", 3);
                    StopPlacing();
                    return;
                }
            }

            DaggerfallUI.AddHUDText("Placement aborted!", 3);
            StopPlacing();
        }

        public void SetBoatPositionAndDirection(Boat boat, Vector3 position, Vector3 direction, Terrain terrain = null)
        {
            boat.WakeEmitter.Stop();
            boat.GameObject.transform.position = position;
            boat.GameObject.transform.forward = direction;
            boat.MapPixel = GameManager.Instance.PlayerGPS.CurrentMapPixel;
            if (terrain != null && terrain.transform != GameManager.Instance.StreamingWorld.PlayerTerrainTransform)
                boat.MapPixel = GetMapPixelFromTerrain(terrain);
            UpdateBoatNodes(boat, terrain);
        }

        DFPosition GetMapPixelFromTerrain(Terrain terrain)
        {

            StreamingWorld.TerrainDesc[] terrainDescs = (StreamingWorld.TerrainDesc[])terrainArray.GetValue(GameManager.Instance.StreamingWorld);
            foreach (StreamingWorld.TerrainDesc terrainDesc in terrainDescs)
            {
                if (terrainDesc.terrainObject == terrain.gameObject)
                {
                    DFPosition mapPixel = new DFPosition(terrainDesc.mapPixelX,terrainDesc.mapPixelY);
                    return mapPixel;
                }
            }

            return null;
        }

        public void SetBoatPositionAndDirection(Boat boat, Vector3 position, Vector3 direction, DFPosition mapPixel = null)
        {
            boat.WakeEmitter.Stop();
            boat.GameObject.transform.position = position;
            boat.GameObject.transform.forward = direction;
            boat.MapPixel = mapPixel;
            UpdateBoatNodes(boat, mapPixel);
        }

        public void OpenCargo(Boat boat)
        {
            DaggerfallUI.Instance.InventoryWindow.LootTarget = boat.Cargo;
            DaggerfallUI.PostMessage(DaggerfallUIMessages.dfuiOpenInventoryWindow);
        }

        void PlaySlow(Boat boat, bool crossfade = false)
        {
            if (crossfade)
                CrossfadeAudioSource(boat.AudioSourceFast, boat.AudioSourceSlow);
            else
            {
                boat.AudioSourceFast.Stop();
                FadeAudioSource(boat.AudioSourceSlow, 0, DaggerfallUnity.Settings.SoundVolume * sfxVolume);
            }
        }
        void PlayFast(Boat boat, bool crossfade = false)
        {
            if (crossfade)
                CrossfadeAudioSource(boat.AudioSourceSlow, boat.AudioSourceFast);
            else
            {
                boat.AudioSourceSlow.Stop();
                FadeAudioSource(boat.AudioSourceFast, 0, DaggerfallUnity.Settings.SoundVolume * sfxVolume);
            }
        }

        void FadeAudioSource(AudioSource target, float from, float to, float duration = 1)
        {
            if (fading != null)
                StopCoroutine(fading);

            fading = FadeAudioSourceCoroutine(target, from, to, duration);
            StartCoroutine(fading);
        }

        void CrossfadeAudioSource(AudioSource from, AudioSource to, float duration = 2)
        {
            if (fading != null)
                return;

            fading = CrossfadeAudioSourceCoroutine(from, to, duration);
            StartCoroutine(fading);
        }

        IEnumerator FadeAudioSourceCoroutine(AudioSource target, float from, float to, float duration = 1)
        {
            float time = 0;

            target.Play();

            while (time < duration)
            {
                float factor = time / duration;
                target.volume = Mathf.Lerp(from, to, factor);

                time += Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }

            if (to == 0)
                target.Stop();

            fading = null;
        }

        IEnumerator CrossfadeAudioSourceCoroutine(AudioSource from, AudioSource to, float duration = 2)
        {
            float time = 0;

            from.Play();
            to.Play();

            while (time < duration)
            {
                float factor = time / duration;
                from.volume = Mathf.Lerp(DaggerfallUnity.Settings.SoundVolume * sfxVolume, 0, factor);
                to.volume = Mathf.Lerp(0, DaggerfallUnity.Settings.SoundVolume * sfxVolume, factor);

                time += Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }

            from.Stop();

            fading = null;
        }

        public void OarEvent_In()
        {
            if (!IsSailing)
                return;

            if (CurrentBoat.OarParticles.Count > 0 && !DisableParticles)
            {
                foreach (ParticleSystem particleSystem in CurrentBoat.OarParticles)
                {
                    ParticleSystem.MainModule main = particleSystem.main;
                    main.startDelay = 0.4f;
                    particleSystem.Play();
                }
            }

            if (timeScaleIndex == 0)
            {
                AudioSource ruddeAudioSource = CurrentBoat.RudderObject.GetComponent<AudioSource>();
                if (ruddeAudioSource != null)
                {
                    ruddeAudioSource.PlayOneShot(audioClips[2]);
                }
            }
        }

        public void OarEvent_Sweep()
        {
            if (!IsSailing)
                return;

            if (CurrentBoat.OarParticles.Count > 0 && !DisableParticles)
            {
                foreach (ParticleSystem particleSystem in CurrentBoat.OarParticles)
                {
                    ParticleSystem.MainModule main = particleSystem.main;
                    main.startDelay = 0f;
                    particleSystem.Play();
                }
            }


            if (timeScaleIndex == 0)
            {
                AudioSource ruddeAudioSource = CurrentBoat.RudderObject.GetComponent<AudioSource>();
                if (ruddeAudioSource != null)
                {
                    ruddeAudioSource.PlayOneShot(audioClips[3]);
                }
            }
        }

        public void OarEvent_Out()
        {
            if (!IsSailing)
                return;

            if (CurrentBoat.OarParticles.Count > 0 && !DisableParticles)
            {
                foreach (ParticleSystem particleSystem in CurrentBoat.OarParticles)
                {
                    ParticleSystem.MainModule main = particleSystem.main;
                    main.startDelay = 0.1f;
                    particleSystem.Play();
                }
            }


            if (timeScaleIndex == 0)
            {
                AudioSource ruddeAudioSource = CurrentBoat.RudderObject.GetComponent<AudioSource>();
                if (ruddeAudioSource != null)
                {
                    ruddeAudioSource.PlayOneShot(audioClips[4]);
                }
            }
        }

        public static void OnLootSpawned(object sender, ContainerLootSpawnedEventArgs e)
        {
            Instance.AssignVariantsToShopItems(e.Loot);
        }

        public void AssignVariantsToShopItems(ItemCollection items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                DaggerfallUnityItem item = items.GetItem(i);
                if (item.TemplateIndex == ItemBoatDeed.templateIndex)
                {
                    int hull = UnityEngine.Random.Range(1, 4);
                    int variant = 0;
                    /*if (hull == 1)
                        variant = UnityEngine.Random.Range(0, 7);*/
                    item.message = (hull * 10) + variant;

                    //adjust price based on thing?
                    item.value = Instance.hullPrices[hull];
                    item.weightInKg = Instance.hullWeights[hull];
                    item.shortName += " " + Instance.hullNames[hull] + " '" + Instance.variantNames[variant] + "'";
                }
                else if (item.TemplateIndex == ItemBoatParts.templateIndex)
                {
                    int hull = 0;
                    //int variant = UnityEngine.Random.Range(0, 7);
                    int variant = 0;
                    item.message = (hull * 10) + variant;

                    //adjust price based on thing?
                    item.value = Instance.hullPrices[hull];
                    item.weightInKg = Instance.hullWeights[hull];
                    item.shortName += " " + Instance.hullNames[hull] + " '" + Instance.variantNames[variant] + "'";
                }
            }
        }
        public void DrawBox(Vector3 pos, Quaternion rot, Vector3 scale, Color c, float duration)
        {
            // create matrix
            Matrix4x4 m = new Matrix4x4();
            m.SetTRS(pos, rot, scale);

            var point1 = m.MultiplyPoint(new Vector3(-0.5f, -0.5f, 0.5f));
            var point2 = m.MultiplyPoint(new Vector3(0.5f, -0.5f, 0.5f));
            var point3 = m.MultiplyPoint(new Vector3(0.5f, -0.5f, -0.5f));
            var point4 = m.MultiplyPoint(new Vector3(-0.5f, -0.5f, -0.5f));

            var point5 = m.MultiplyPoint(new Vector3(-0.5f, 0.5f, 0.5f));
            var point6 = m.MultiplyPoint(new Vector3(0.5f, 0.5f, 0.5f));
            var point7 = m.MultiplyPoint(new Vector3(0.5f, 0.5f, -0.5f));
            var point8 = m.MultiplyPoint(new Vector3(-0.5f, 0.5f, -0.5f));

            Debug.DrawLine(point1, point2, c, duration);
            Debug.DrawLine(point2, point3, c, duration);
            Debug.DrawLine(point3, point4, c, duration);
            Debug.DrawLine(point4, point1, c, duration);

            Debug.DrawLine(point5, point6, c, duration);
            Debug.DrawLine(point6, point7, c, duration);
            Debug.DrawLine(point7, point8, c, duration);
            Debug.DrawLine(point8, point5, c, duration);

            Debug.DrawLine(point1, point5, c, duration);
            Debug.DrawLine(point2, point6, c, duration);
            Debug.DrawLine(point3, point7, c, duration);
            Debug.DrawLine(point4, point8, c, duration);

            /*// optional axis display
            Debug.DrawRay(m.GetPosition(), m.GetForward(), Color.magenta);
            Debug.DrawRay(m.GetPosition(), m.GetUp(), Color.yellow);
            Debug.DrawRay(m.GetPosition(), m.GetRight(), Color.red);*/
        }
    }
    public class PlacedBoatData
    {
        public ulong UID;
        public int Hull;
        public int Variant;
        public DFPosition MapPixel;
        public Vector3 Position;
        public Vector3 Direction;
        public ItemData_v1[] Items;
        public bool lights;
    }

    public class ComeSailAwaySaveData : IHasModSaveData
    {
        public Vector3 worldCompensation = Vector3.zero;

        public List<PlacedBoatData> placedBoats = new List<PlacedBoatData>();

        //if the player was sailing
        public int currentBoat = -1;

        public bool TemporaryShip = false;

        //if sailing, record the current boat state
        public int sailPosition = 0;
        public Vector3 moveVectorCurrent = Vector3.zero;
        public Vector3 moveVectorTarget = Vector3.zero;

        public Vector3 windVector = Vector3.forward;

        //record packed cargo
        public Dictionary<ulong, ItemData_v1[]> packedCargoes = new Dictionary<ulong, ItemData_v1[]>();

        public Type SaveDataType
        {
            get
            {
                return typeof(ComeSailAwaySaveData);
            }
        }

        public object NewSaveData()
        {
            ComeSailAwaySaveData emptyData = new ComeSailAwaySaveData();
            emptyData.worldCompensation = Vector3.zero;

            emptyData.placedBoats = new List<PlacedBoatData>();

            emptyData.currentBoat = -1;
            emptyData.TemporaryShip = false;
            emptyData.sailPosition = 0;
            emptyData.moveVectorCurrent = Vector3.zero;
            emptyData.moveVectorTarget = Vector3.zero;

            emptyData.windVector = Vector3.forward;

            emptyData.packedCargoes = new Dictionary<ulong, ItemData_v1[]>();

            return emptyData;
        }
        public object GetSaveData()
        {
            ComeSailAwaySaveData data = new ComeSailAwaySaveData();

            data.worldCompensation = GameManager.Instance.StreamingWorld.WorldCompensation;

            int currentBoatIndex = -1;
            if (ComeSailAway.Instance.AllBoats.Count > 0)
            {
                for (int i = 0; i < ComeSailAway.Instance.AllBoats.Count; i++)
                {
                    PlacedBoatData boatData = new PlacedBoatData();
                    boatData.UID = ComeSailAway.Instance.AllBoats[i].uid;
                    boatData.Hull = ComeSailAway.Instance.AllBoats[i].hull;
                    boatData.Variant = ComeSailAway.Instance.AllBoats[i].variant;
                    boatData.MapPixel = ComeSailAway.Instance.AllBoats[i].MapPixel;
                    boatData.Position = ComeSailAway.Instance.AllBoats[i].GameObject.transform.position ;
                    boatData.Direction = ComeSailAway.Instance.AllBoats[i].GameObject.transform.forward;
                    boatData.Items = ComeSailAway.Instance.AllBoats[i].Cargo.Items.SerializeItems();
                    boatData.lights = ComeSailAway.Instance.AllBoats[i].LightOn;

                    data.placedBoats.Add(boatData);

                    if (ComeSailAway.Instance.CurrentBoat == ComeSailAway.Instance.AllBoats[i])
                        currentBoatIndex = i;
                }
            }

            data.currentBoat = currentBoatIndex;
            data.TemporaryShip = ComeSailAway.Instance.TemporaryShip;
            data.sailPosition = ComeSailAway.Instance.sailPosition;
            data.moveVectorCurrent = ComeSailAway.Instance.MoveVectorCurrent;
            data.moveVectorTarget = ComeSailAway.Instance.MoveVectorTarget;

            data.windVector = ComeSailAway.Instance.windVectorCurrent;

            //record packed cargo
            if (ComeSailAway.Instance.PackedCargoes.Count > 0)
            {
                data.packedCargoes = new Dictionary<ulong, ItemData_v1[]>();
                foreach (KeyValuePair<ulong,ItemCollection> packedCargo in ComeSailAway.Instance.PackedCargoes)
                {
                    data.packedCargoes.Add(packedCargo.Key, packedCargo.Value.SerializeItems());
                }
            }

            return data;
        }

        public void RestoreSaveData(object dataIn)
        {
            if (ComeSailAway.Instance.IsSailing)
                ComeSailAway.Instance.StopSailing();

            if (ComeSailAway.Instance.AllBoats.Count > 0)
            {
                foreach (Boat boat in ComeSailAway.Instance.AllBoats)
                    GameObject.Destroy(boat.GameObject);

                ComeSailAway.Instance.AllBoats.Clear();
            }

            ComeSailAwaySaveData data = (ComeSailAwaySaveData)dataIn;

            float OffsetY = GameManager.Instance.StreamingWorld.WorldCompensation.y - data.worldCompensation.y;
            //float OffsetY = 0;

            if (data.placedBoats != null && data.placedBoats.Count > 0)
            {
                foreach (PlacedBoatData placedBoat in data.placedBoats)
                {
                    Boat newBoat = new Boat(placedBoat.Hull,placedBoat.Variant);
                    newBoat.uid = placedBoat.UID;
                    newBoat.Position = placedBoat.Position + (Vector3.up * OffsetY);
                    newBoat.Direction = placedBoat.Direction;
                    newBoat.MapPixel = placedBoat.MapPixel;

                    ComeSailAway.Instance.AllBoats.Add(newBoat);

                    ComeSailAway.Instance.PlaceBoat(newBoat, newBoat.Position, newBoat.Direction, newBoat.MapPixel);

                    //add the cargo in there
                    newBoat.Cargo.Items.DeserializeItems(placedBoat.Items);

                    ComeSailAway.Instance.SetLights(newBoat, placedBoat.lights);
                }
            }

            if (ComeSailAway.Instance.AllBoats.Count > 0)
            {
                if (data.currentBoat != -1)
                {
                    ComeSailAway.Instance.TemporaryShip = data.TemporaryShip;
                    ComeSailAway.Instance.StartSailing(ComeSailAway.Instance.AllBoats[data.currentBoat]);
                    if (data.sailPosition > 0)
                    {
                        ComeSailAway.Instance.RaiseSails();
                    }
                    ComeSailAway.Instance.MoveVectorCurrent = data.moveVectorCurrent;
                    ComeSailAway.Instance.MoveVectorTarget = data.moveVectorTarget;
                }
            }

            ComeSailAway.Instance.windVectorCurrent = data.windVector;

            //retrieve packed cargo
            ComeSailAway.Instance.PackedCargoes = new Dictionary<ulong, ItemCollection>();
            if (data.packedCargoes.Count > 0)
            {
                foreach (KeyValuePair<ulong, ItemData_v1[]> packedCargo in data.packedCargoes)
                {
                    ItemCollection items = new ItemCollection();
                    items.DeserializeItems(packedCargo.Value);
                    ComeSailAway.Instance.PackedCargoes.Add(packedCargo.Key, items);
                }
            }

            ComeSailAway.Instance.RunOnUpdateEvents();
        }
    }
}
