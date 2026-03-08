using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Entity;
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

        public static void OnStartLoad(SaveData_v1 saveData)
        {
            //initialize save data?;
        }

        public const float terrainEdge = 819.2f;

        public static ComeSailAway Instance;

        GameObject playerObject;

        public GameObject boatObject;
        GameObject boatMesh;
        GameObject boatMeshOffset;

        public GameObject boardTrigger;
        public GameObject driveTrigger;
        public GameObject boardPosition;
        public GameObject drivePosition;
        public GameObject cargoTrigger;

        public DFPosition boatMapPixel = null;
        Vector3 lastBoatPosition;
        Vector3 lastBoatDirection;

        //0 = Fore, 1 = Aft, 2, Starboard, 3 = Port
        List<GameObject> boatNodes = new List<GameObject>();
        List<int> boatNodeTileMapIndices = new List<int>();

        GameObject wakeObject;
        ParticleSystem wakeEmitter;
        ParticleSystemRenderer wakeRenderer;
        ParticleSystem.MainModule wakeEmitterMain;
        float wakeThreshold
        {
            get
            {
                return moveSpeedSail * 0.25f;
            }
        }

        GameObject flagObject;
        ParticleSystem flagEmitter;
        ParticleSystem.MainModule flagEmitterMain;

        GameObject rudderObject;    //object that reflects rudder activity
        GameObject sailingObject;   //put stuff here that appears when sailing and disappears when not
        GameObject fireObject;
        GameObject bedObject;

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

        public float waveFrameTime = 0.1f;
        float waveFrameTimer = 0;
        List<Texture2D> waveFrames;
        int waveFrameIndex = 0;

        ParticleSystem rainEmitter;
        ParticleSystem snowEmitter;
        ParticleSystem.ForceOverLifetimeModule rainEmitterForceOverLifetime;
        ParticleSystem.ForceOverLifetimeModule snowEmitterForceOverLifetime;

        public bool placing;
        DaggerfallUnityItem placeItem;

        public bool sailing;

        float moveSpeedOar = 2f;
        float moveSpeedSail = 10f;
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
                    return moveSpeedOar * moveSpeedOarMod * boatCargoMod;
                return moveSpeedSail * moveSpeedSailMod * boatCargoMod;
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
                    return moveAccelOar * moveAccelOarMod * boatCargoMod;
                else if (sailPosition == 1)
                    return moveAccelSail * moveAccelSailMod * windVectorCurrent.magnitude * boatCargoMod;
                else
                    return moveAccelSail * moveAccelSailMod * boatCargoMod;
            }
        }
        float turnSpeed
        {
            get
            {
                if (sailPosition == 0)
                    return turnSpeedOar * turnSpeedOarMod * boatCargoMod;
                return turnSpeedSail * turnSpeedSailMod * boatCargoMod;
            }
        }
        float turnAccel
        {
            get
            {
                if (sailPosition == 0 && (
                    InputManager.Instance.HasAction(InputManager.Actions.MoveRight) ||
                    InputManager.Instance.HasAction(InputManager.Actions.MoveLeft)))
                    return turnAccelOar * turnAccelOarMod * boatCargoMod;
                return turnAccelSail * turnAccelSailMod * boatCargoMod;
            }
        }

        public Vector3 windVectorTarget;
        public Vector3 windVectorCurrent;

        //0 = oars, 1 = quarter-sail, 2 = half-sail, 3 = full-sail
        public int sailPosition;

        public Vector3 MoveVectorTarget = Vector3.zero;
        public Vector3 MoveVectorCurrent = Vector3.zero;
        public float TurnTarget = 0;
        public float TurnCurrent = 0;

        //sail control
        public float trimAngle;
        public float trimAngleSquare;
        bool trimAuto = true;    //0 = manual, 1 = automatic
        bool trimAutoSquareUpwind = true;

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
        public DaggerfallLoot boatCargo;
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
        DaggerfallAudioSource dfAudioSource;
        AudioSource audioSourceSlow;
        AudioSource audioSourceFast;
        AudioClip[] audioClips = new AudioClip[2] {null,null};
        IEnumerator fading;

        //input smoothing
        float inputCurrent = 0;
        bool HasInput
        {
            get
            {
                if (InputManager.Instance.Vertical != 0 || InputManager.Instance.Horizontal != 0)
                    return true;

                return false;
            }
        }
        float inputTarget
        {
            get
            {
                float result = 0;


                if (HasInput)
                {
                    if (Mathf.Abs(InputManager.Instance.Vertical) > Mathf.Abs(InputManager.Instance.Horizontal))
                        result = InputManager.Instance.Vertical;
                    else
                        result = InputManager.Instance.Horizontal;
                }

                return result;
            }
        }

        //settings
        KeyCode keyCodeDisembark = KeyCode.None;
        KeyCode keyCodeToggleSail = KeyCode.None;
        KeyCode keyCodeTimeScaleUp = KeyCode.None;
        KeyCode keyCodeTimeScaleDown = KeyCode.None;
        KeyCode keyCodeTimeScaleReset = KeyCode.None;
        KeyCode keyCodeTrimLeft = KeyCode.Minus;
        KeyCode keyCodeTrimRight = KeyCode.Plus;
        KeyCode keyCodeTrimMod = KeyCode.RightShift;
        float sfxVolume = 1f;
        float moveSpeedOarMod = 1f;
        float moveAccelOarMod = 1f;
        float moveSpeedSailMod = 1f;
        float moveAccelSailMod = 1f;
        float turnSpeedOarMod = 1f;
        float turnAccelOarMod = 1f;
        float turnSpeedSailMod = 1f;
        float turnAccelSailMod = 1f;

        //Mod Messages
        public event Action<Vector3> OnUpdateWind;

        //prefabstuff
        public List<Transform> Masts = new List<Transform>();
        public List<Transform> Booms = new List<Transform>();
        public List<Transform> Sails = new List<Transform>();
        public List<Transform> SailsLarge = new List<Transform>();
        public List<Transform> SailsSmall = new List<Transform>();
        public List<Transform> SailsSquare = new List<Transform>();
        public List<Transform> SailsLateen = new List<Transform>();
        public List<Transform> SailsGaff = new List<Transform>();
        public List<Transform> SailsStay = new List<Transform>();

        //mod compatibility
        Mod WODTerrain;


        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("Controls"))
            {
                keyCodeDisembark = SetKeyFromText(settings.GetString("Controls", "Disembark"));
                keyCodeToggleSail = SetKeyFromText(settings.GetString("Controls", "ToggleSail"));
                keyCodeTimeScaleUp = SetKeyFromText(settings.GetString("Controls", "IncreaseTimeScale"));
                keyCodeTimeScaleDown = SetKeyFromText(settings.GetString("Controls", "DecreaseTimeScale"));
                keyCodeTimeScaleReset = SetKeyFromText(settings.GetString("Controls", "ResetTimeScale"));
                keyCodeTrimRight = SetKeyFromText(settings.GetString("Controls", "TrimRight"));
                keyCodeTrimLeft = SetKeyFromText(settings.GetString("Controls", "TrimLeft"));
                keyCodeTrimMod = SetKeyFromText(settings.GetString("Controls", "TrimModifier"));
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

            playerObject = GameManager.Instance.PlayerObject;

            boatObject = new GameObject("Boat");
            boatObject.transform.position = Vector3.zero;
            boatMeshOffset = new GameObject("BoatMeshOffset");
            boatMeshOffset.transform.SetParent(boatObject.transform);

            //PlayerActivate.RegisterCustomActivation(mod, 214, 1, DriveBoat);
            //PlayerActivate.RegisterCustomActivation(mod, 253, 15, BoardBoat);

            PlayerActivate.RegisterCustomActivation(mod, 112400, DriveBoat);
            PlayerActivate.RegisterCustomActivation(mod, 112401, BoardBoat);
            PlayerActivate.RegisterCustomActivation(mod, 112402, OpenBoatCargo);

            SpawnBoat();

            Bounds boatMeshBounds = boatMesh.GetComponentInChildren<MeshCollider>().bounds;
            GameObject boatNodeFore = new GameObject("Fore");
            boatNodes.Add(boatNodeFore);
            boatNodeTileMapIndices.Add(-1);
            boatNodeFore.transform.SetParent(boatObject.transform);
            boatNodeFore.transform.localPosition = new Vector3(
                boatMeshBounds.center.x,
                boatMeshBounds.center.y,
                boatMeshBounds.center.z + boatMeshBounds.extents.z
                );
            GameObject boatNodeAft = new GameObject("Aft");
            boatNodes.Add(boatNodeAft);
            boatNodeTileMapIndices.Add(-1);
            boatNodeAft.transform.SetParent(boatObject.transform);
            boatNodeAft.transform.localPosition = new Vector3(
                boatMeshBounds.center.x,
                boatMeshBounds.center.y,
                boatMeshBounds.center.z - boatMeshBounds.extents.z
                );
            GameObject boatNodeStarboard = new GameObject("Starboard");
            boatNodes.Add(boatNodeStarboard);
            boatNodeTileMapIndices.Add(-1);
            boatNodeStarboard.transform.SetParent(boatObject.transform);
            boatNodeStarboard.transform.localPosition = new Vector3(
                boatMeshBounds.center.x + boatMeshBounds.extents.x,
                boatMeshBounds.center.y,
                boatMeshBounds.center.z
                );
            GameObject boatNodePort = new GameObject("Port");
            boatNodes.Add(boatNodePort);
            boatNodeTileMapIndices.Add(-1);
            boatNodePort.transform.SetParent(boatObject.transform);
            boatNodePort.transform.localPosition = new Vector3(
                boatMeshBounds.center.x - boatMeshBounds.extents.x,
                boatMeshBounds.center.y,
                boatMeshBounds.center.z
                );

            waveObject = new GameObject("WaveObject");
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

            DaggerfallUnity.Instance.ItemHelper.RegisterCustomItem(ItemSkiff.templateIndex, ItemGroups.UselessItems2, typeof(ItemSkiff));

            //audio clips
            audioClips = new AudioClip[2] {
                mod.GetAsset<AudioClip>("SmallShipAmbience.ogg"),
                mod.GetAsset<AudioClip>("ShipExteriorAmbience2.ogg") };

            //audio source
            GameObject audioSourceSlowObject = new GameObject("BoatSFXSlow");
            audioSourceSlowObject.transform.SetParent(boatObject.transform);
            audioSourceSlowObject.transform.localPosition = Vector3.zero;
            audioSourceSlow = audioSourceSlowObject.AddComponent<AudioSource>();
            audioSourceSlow.clip = audioClips[0];
            audioSourceSlow.loop = true;
            audioSourceSlow.spatialBlend = 1;

            GameObject audioSourceFastObject = new GameObject("BoatSFXFast");
            audioSourceFastObject.transform.SetParent(boatObject.transform);
            audioSourceFastObject.transform.localPosition = Vector3.zero;
            audioSourceFast = audioSourceFastObject.AddComponent<AudioSource>();
            audioSourceFast.clip = audioClips[1];
            audioSourceFast.loop = true;
            audioSourceFast.spatialBlend = 1;

            GameObject dfAudioSourceObject = new GameObject("BoatSFXOneShot");
            dfAudioSourceObject.transform.SetParent(boatObject.transform);
            dfAudioSourceObject.transform.localPosition = Vector3.zero;
            AudioSource dfAudioSourceSource = dfAudioSourceObject.AddComponent<AudioSource>();
            dfAudioSource = dfAudioSourceObject.AddComponent<DaggerfallAudioSource>();

            GameObject boatCargoObject = new GameObject("BoatCargo");
            boatCargoObject.transform.SetParent(boatObject.transform);
            boatCargoObject.transform.localPosition = Vector3.zero;
            boatCargo = boatCargoObject.AddComponent<DaggerfallLoot>();
            boatCargo.playerOwned = true;

            //register console commands
            ConsoleCommandsDatabase.RegisterCommand(GiveMeBoat.name, GiveMeBoat.description, GiveMeBoat.usage, GiveMeBoat.Execute);
            ConsoleCommandsDatabase.RegisterCommand(PlaceBoatAtMe.name, PlaceBoatAtMe.description, PlaceBoatAtMe.usage, PlaceBoatAtMe.Execute);

            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();

            mod.MessageReceiver = MessageReceiver;

            //Finish setup
            boatObject.SetActive(false);

            WODTerrain = ModManager.Instance.GetModFromGUID("a9091dd7-e07a-4171-b16d-d13d67a5f221");
        }

        void SpawnBoat()
        {
            boatMesh = MeshReplacement.ImportCustomGameobject(112408, boatMeshOffset.transform, GameManager.Instance.PlayerObject.transform.localToWorldMatrix);
            boatMesh.transform.localPosition = Vector3.zero;

            //search boatMesh hierarchy for transforms
            for (int i = 0; i < boatMesh.transform.childCount - 1; i++)
            {
                Transform child = boatMesh.transform.GetChild(i);
                if (flagObject == null && child.name == "FlagObject")
                {
                    flagObject = child.gameObject;
                    flagEmitter = flagObject.GetComponentInChildren<ParticleSystem>();
                    flagEmitterMain = flagEmitter.main;
                }

                if (wakeObject == null && child.name == "WakeObject")
                {
                    wakeObject = child.gameObject;
                    wakeEmitter = wakeObject.GetComponent<ParticleSystem>();
                    wakeEmitterMain = wakeEmitter.main;
                }

                if (driveTrigger == null && child.name == "DriveTrigger")
                {
                    //setup driving trigger
                    driveTrigger = MeshReplacement.ImportCustomGameobject(112400, child, GameManager.Instance.PlayerObject.transform.localToWorldMatrix);
                    driveTrigger.transform.localPosition = Vector3.zero;
                }

                if (boardTrigger == null && child.name == "BoardTrigger")
                {
                    //setup boarding trigger
                    boardTrigger = MeshReplacement.ImportCustomGameobject(112401, child, GameManager.Instance.PlayerObject.transform.localToWorldMatrix);
                    boardTrigger.transform.localPosition = Vector3.zero;

                    //add the chain billboard from the unused texture archive
                    //TO-DO: Make a custom texture archive for stuff like this?
                    GameObject boardTriggerBillboard = GameObjectHelper.CreateDaggerfallBillboardGameObject(253, 15, boardTrigger.transform);
                    boardTriggerBillboard.transform.localPosition = Vector3.zero - (Vector3.up * (boardTriggerBillboard.GetComponent<DaggerfallBillboard>().Summary.Size.y / 2));
                }

                if (cargoTrigger == null && child.name == "CargoTrigger")
                {
                    //setup driving trigger
                    cargoTrigger = MeshReplacement.ImportCustomGameobject(112402, child, GameManager.Instance.PlayerObject.transform.localToWorldMatrix);
                    cargoTrigger.transform.localPosition = Vector3.zero;
                }

                if (fireObject == null && child.name == "FireObject")
                {
                    //setup campfire for C&C
                    fireObject = GameObjectHelper.CreateDaggerfallBillboardGameObject(210, 1, child);
                    fireObject.transform.localPosition = Vector3.zero + (Vector3.up * (fireObject.GetComponent<DaggerfallBillboard>().Summary.Size.y / 2));
                    fireObject.GetComponent<MeshRenderer>().enabled = false;

                    GameObject fireObjectBillboard = GameObjectHelper.CreateDaggerfallBillboardGameObject(205, 0, child);
                    fireObjectBillboard.transform.localPosition = Vector3.zero + (Vector3.up * (fireObjectBillboard.GetComponent<DaggerfallBillboard>().Summary.Size.y / 2));
                }

                if (drivePosition == null && child.name == "DrivePosition")
                {
                    drivePosition = boatMesh.transform.GetChild(i).gameObject;
                }

                if (boardPosition == null && child.name == "BoardPosition")
                {
                    boardPosition = boatMesh.transform.GetChild(i).gameObject;
                }

                if (rudderObject == null && child.name == "RudderObject")
                    rudderObject = child.gameObject;

                if (sailingObject == null && child.name == "SailingObject")
                    sailingObject = child.gameObject;
            }

            Masts.Clear();
            Sails.Clear();
            SailsLarge.Clear();
            SailsSmall.Clear();
            SailsSquare.Clear();
            SailsLateen.Clear();
            SailsGaff.Clear();
            SailsStay.Clear();
            GetBoatTransforms(boatMesh.transform);

            if (Sails.Count > 0)
            {
                foreach (Transform sail in Sails)
                {
                    Animator animator = sail.GetComponent<Animator>();
                    if (animator != null)
                    {
                        animator.CrossFade("Stowed", 1);
                        animator.SetBool("Stowed", true);
                    }
                }
            }
            sailingObject.gameObject.SetActive(false);
        }

        void GetBoatTransforms(Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);

                //skip inactive children, for testing
                if (!child.gameObject.activeSelf)
                    continue;

                if (child.name.Contains("Mast"))
                    Masts.Add(child);

                if (child.name.Contains("Boom"))
                    Booms.Add(child);

                if (child.name.Contains("Sail") && child.name != "SailingObject" && !child.name.Contains("Skelly"))
                {
                    Sails.Add(child);

                    if (child.name.Contains("Large"))
                        SailsLarge.Add(child);
                    else
                        SailsSmall.Add(child);

                    if (child.name.Contains("Square"))
                        SailsSquare.Add(child);
                    else if (child.name.Contains("Lateen"))
                        SailsLateen.Add(child);
                    else if (child.name.Contains("Gaff"))
                        SailsGaff.Add(child);
                    else if (child.name.Contains("Stay"))
                        SailsStay.Add(child);
                }


                if (child.gameObject.GetComponent<SkinnedMeshRenderer>())
                {
                    child.gameObject.AddComponent<ApplyGameTextures>();
                }

                GetBoatTransforms(child);
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

        void MessageReceiver(string message, object data, DFModMessageCallback callBack)
        {
            switch (message)
            {
                case "GetWind":
                    callBack?.Invoke("GetWind", windVectorCurrent);
                    break;

                case "OnUpdateWind":
                    OnUpdateWind += data as Action<Vector3>;
                    break;

                case "ResetTimeScale":
                    ResetTimeScale();
                    break;

                case "StopSailing":
                    StopSailing();
                    break;

                case "IsPlayerSailing":
                    callBack?.Invoke("IsPlayerSailing", sailing);
                    break;

                case "GetBoatObject":
                    callBack?.Invoke("GetBoatObject", boatObject);
                    break;

                default:
                    Debug.LogErrorFormat("{0}: unknown message received ({1}).", this, message);
                    break;
            }
        }

        private static class GiveMeBoat
        {
            public static readonly string name = "giveboat";
            public static readonly string description = "Add a boat to the player's inventory if they do not already have one";
            public static readonly string usage = "giveboat";

            public static string Execute(params string[] args)
            {
                string result = "";
                if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, ItemSkiff.templateIndex))
                {
                    result = "Player inventory already contains a boat";
                }
                else
                {
                    result = "Boat added to player's inventory";
                    DaggerfallUnityItem newBoatItem = ItemBuilder.CreateItem(ItemGroups.UselessItems2, ItemSkiff.templateIndex);
                    newBoatItem.weightInKg += Instance.boatCargo.Items.GetWeight();
                    GameManager.Instance.PlayerEntity.Items.AddItem(newBoatItem);
                }
                return result;
            }
        }

        private static class PlaceBoatAtMe
        {
            public static readonly string name = "placeboat";
            public static readonly string description = "place a boat where the player is looking";
            public static readonly string usage = "placeboat";

            public static string Execute(params string[] args)
            {
                string result = "Attempting to place boat";
                Instance.PlaceBoat();
                return result;
            }
        }

        void UpdateAudioSource()
        {
            if (audioSourceSlow.volume > 0)
                audioSourceSlow.volume = DaggerfallUnity.Settings.SoundVolume * sfxVolume;

            if (audioSourceFast.volume > 0)
                audioSourceFast.volume = DaggerfallUnity.Settings.SoundVolume * sfxVolume;
        }
        public static void OnLoad(SaveData_v1 saveData)
        {
            if (Instance.waveObject != null)
                Instance.UpdateWaveMesh();
        }

        void OnPreFastTravel(DaggerfallTravelPopUp daggerfallTravelPopUp)
        {
            if (sailing)
            {
                StopSailing();
                PackBoat(true);
            }
        }

        void OnPostFastTravel()
        {

        }

        void OnTransition(PlayerEnterExit.TransitionEventArgs args)
        {
            UpdateBoatVisibility();
        }

        void OnPositionUpdate(Vector3 offset)
        {
            wakeEmitter.Stop();

            //when floating origin resets, update boat position
            boatObject.transform.position += offset;

            if (sailing)
            {
                playerObject.transform.localPosition = drivePosition.transform.localPosition;
                boatMapPixel = GameManager.Instance.PlayerGPS.CurrentMapPixel;
            }

            //fix wake
            if (wakeEmitter.particleCount > 0)
            {
                ParticleSystem.Particle[] particles = new ParticleSystem.Particle[wakeEmitter.particleCount];
                wakeEmitter.GetParticles(particles);

                for (int i = 0; i < wakeEmitter.particleCount; i++)
                    particles[i].position += offset;

                wakeEmitter.SetParticles(particles);
            }

            if (MoveVectorCurrent.magnitude >= wakeThreshold)
                wakeEmitter.Play();

            UpdateWaveMesh();
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
            if (sailing)
            {
                UpdateBoatCargoMod();
            }
        }

        void UpdateWaveMesh()
        {
            //clear mesh
            waveMeshFilter.mesh.Clear();

            if (!wave)
                return;

            //reset origin
            Vector3 worldCompensation = GameManager.Instance.StreamingWorld.WorldCompensation;
            waveObject.transform.localPosition = (Vector3.forward * (terrainEdge / 2)) + (Vector3.right * (terrainEdge / 2)) + (Vector3.up * 34) + (Vector3.up * worldCompensation.y);

            //get map pixel
            DFPosition mapPixel = GameManager.Instance.PlayerGPS.CurrentMapPixel;

            int mapHeight = DaggerfallUnity.Instance.ContentReader.WoodsFileReader.GetHeightMapValue(mapPixel.X, mapPixel.Y);

            //check neighbors for ocean pixels
            List<Vector2> neighbors = new List<Vector2>();
            for (int x = -waveDistance; x < waveDistance+1; x++)
            {
                for (int y = -waveDistance; y < waveDistance+1; y++)
                {
                    int heightMapValue = DaggerfallUnity.Instance.ContentReader.WoodsFileReader.GetHeightMapValue(mapPixel.X + x, mapPixel.Y + y);
                    int waterHeight = 2;

                    /*if (WODTerrain != null)
                        waterHeight = 5;*/

                    if (heightMapValue <= waterHeight)
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
                            if (hit.point.y > worldCompensation.y + 34)
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
                            if (hit.point.y > worldCompensation.y + 34)
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
                            if (hit.point.y > worldCompensation.y + 34)
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
                            if (hit.point.y > worldCompensation.y + 34)
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
                            if (DaggerfallUnity.Instance.ContentReader.WoodsFileReader.GetHeightMapValue((int)neighbor.x + x, (int)neighbor.y + y) > 2)
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
                                    if (hit.point.y > worldCompensation.y + 34)
                                        isNeighborGround[x + 1, y + 1] = true;
                                }

                                if (Physics.Raycast(ray2, out hit, 1000f, layerMask))
                                {
                                    if (hit.point.y > worldCompensation.y + 34)
                                        isNeighborGround[x + 1, y + 1] = true;
                                }

                                if (Physics.Raycast(ray3, out hit, 1000f, layerMask))
                                {
                                    if (hit.point.y > worldCompensation.y + 34)
                                        isNeighborGround[x + 1, y + 1] = true;
                                }

                                if (Physics.Raycast(ray4, out hit, 1000f, layerMask))
                                {
                                    if (hit.point.y > worldCompensation.y + 34)
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

        void UpdateBoatNodes()
        {
            for (int i = 0; i < boatNodes.Count; i++)
            {
                boatNodeTileMapIndices[i] = GetTileMapIndexAtPosition(boatNodes[i].transform.position,GameManager.Instance.StreamingWorld.PlayerTerrainTransform);
            }
        }

        void UpdateBoatVisibility()
        {
            //hide boat if player is inside building or boat is outside the range of rendered map pixels

            if (GameManager.Instance.PlayerEnterExit.IsPlayerInside)
            {
                if (boatObject.activeSelf)
                    boatObject.SetActive(false);
                return;
            }

            int x = GameManager.Instance.PlayerGPS.CurrentMapPixel.X;
            int y = GameManager.Instance.PlayerGPS.CurrentMapPixel.Y;

            if (boatMapPixel != null && Mathf.Max(Mathf.Abs(x - boatMapPixel.X), Mathf.Abs(y - boatMapPixel.Y)) > DaggerfallUnity.Settings.TerrainDistance)
            {
                if (boatObject.activeSelf)
                    boatObject.SetActive(false);
                return;
            }

            if (!boatObject.activeSelf)
                boatObject.SetActive(true);
        }

        void UpdateBoatCargoMod()
        {
            float weight = boatCargo.Items.GetWeight();

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

            boatCargoMod = Mathf.Clamp(2 - (weight / boatCargoThreshold),0,1);

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
        }

        IEnumerator LerpWind(float time = 3)
        {
            float currentTime = 0;
            Vector3 startVector = windVectorCurrent;

            while (currentTime < time)
            {
                windVectorCurrent = Vector3.Lerp(startVector, windVectorTarget, currentTime / time);
                rainEmitterForceOverLifetime.x = new ParticleSystem.MinMaxCurve(windVectorCurrent.x * 10f, windVectorCurrent.x * 50f);
                rainEmitterForceOverLifetime.z = new ParticleSystem.MinMaxCurve(windVectorCurrent.z * 10f, windVectorCurrent.z * 50f);
                snowEmitterForceOverLifetime.x = new ParticleSystem.MinMaxCurve(windVectorCurrent.x * 5f, windVectorCurrent.x * 25f);
                snowEmitterForceOverLifetime.z = new ParticleSystem.MinMaxCurve(windVectorCurrent.z * 5f, windVectorCurrent.z * 25f);

                currentTime += Time.deltaTime;

                yield return new WaitForEndOfFrame();
            }
        }

        private void OnGUI()
        {
            if (!sailing || GameManager.IsGamePaused || InputManager.Instance.IsPaused || SaveLoadManager.Instance.LoadInProgress)
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

        }

        private void Update()
        {
            if (GameManager.IsGamePaused || InputManager.Instance.IsPaused || SaveLoadManager.Instance.LoadInProgress)
                return;

            if (sailing)
            {
                if (lastBoatPosition != boatObject.transform.position || lastBoatDirection != boatObject.transform.forward)
                {
                    //position/direction changed
                    lastBoatPosition = boatObject.transform.position;
                    lastBoatDirection = boatObject.transform.forward;
                    UpdateBoatNodes();
                }

                if (GameManager.Instance.TransportManager.TransportMode != TransportModes.Foot)
                    GameManager.Instance.TransportManager.TransportMode = TransportModes.Foot;

                if (IsBeached() && MoveVectorCurrent.sqrMagnitude > 0)
                {
                    MoveVectorCurrent = Vector3.zero;
                    MoveVectorTarget = Vector3.zero;
                    ResetTimeScale();
                }

                if (GameManager.Instance.AreEnemiesNearby())
                {
                    DaggerfallUI.SetMidScreenText("There are enemies nearby...");
                    ResetTimeScale();
                }

                //update input
                inputCurrent = Mathf.MoveTowards(inputCurrent, inputTarget, 2*Time.deltaTime);

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
                if (playerObject.transform.localPosition != drivePosition.transform.localPosition)
                    playerObject.transform.localPosition = drivePosition.transform.localPosition;

                //keep player sat and froze
                GameManager.Instance.PlayerMotor.FreezeMotor = 1;

                if (InputManager.Instance.GetKeyDown(keyCodeToggleSail))
                {
                    if (sailPosition > 0 && SailsSquare.Count > 0 && !trimAutoSquareUpwind && InputManager.Instance.GetKey(keyCodeTrimMod) && (SailsLateen.Count > 0 || SailsGaff.Count > 0))
                    {
                        ToggleSquareSails();
                    }
                    else
                        ToggleSails();
                }

                if (InputManager.Instance.GetKeyDown(keyCodeDisembark))
                    StopSailing();

                //trim inputs
                if (!trimAuto)
                {
                    if (InputManager.Instance.GetKey(keyCodeTrimRight))
                    {
                        if (InputManager.Instance.GetKey(keyCodeTrimMod) || (SailsLateen.Count < 1 && SailsGaff.Count < 1))
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
                        if (InputManager.Instance.GetKey(keyCodeTrimMod) || (SailsLateen.Count < 1 && SailsGaff.Count < 1))
                        {
                            if (trimAngleSquare > -45)
                                trimAngleSquare -= 15 * Time.deltaTime;
                        }
                        else if (trimAngle > -90)
                        {
                            trimAngle -= 15 * Time.deltaTime;
                        }
                    }

                    foreach (Transform boom in Booms)
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

                    float forward = 0;
                    float right = 0;
                    float turn = 0;

                    if ((InputManager.Instance.HasAction(InputManager.Actions.MoveForwards) || InputManager.Instance.ToggleAutorun) && IsNodeOnWater(0))
                        forward = 1f;
                    else if (InputManager.Instance.HasAction(InputManager.Actions.MoveBackwards) && IsNodeOnWater(1))
                        forward = -1f;

                    if (InputManager.Instance.HasAction(InputManager.Actions.Run))
                    {
                        //if holding RUN, boat will strafe
                        if (InputManager.Instance.HasAction(InputManager.Actions.MoveRight) && IsNodeOnWater(2))
                            right = 0.5f;
                        else if (InputManager.Instance.HasAction(InputManager.Actions.MoveLeft) && IsNodeOnWater(3))
                            right = -0.5f;
                    }
                    else
                    {
                        if (InputManager.Instance.HasAction(InputManager.Actions.MoveRight))
                            turn = 1f;
                        else if (InputManager.Instance.HasAction(InputManager.Actions.MoveLeft))
                            turn = -1f;
                    }

                    TurnTarget = turn;
                    MoveVectorTarget = (Vector3.forward * forward) + (Vector3.right * right);

                    //update rudder position
                    rudderObject.transform.localRotation = Quaternion.AngleAxis((Mathf.Sin(Time.time * 2) * 15f)* inputCurrent, Vector3.up);
                }
                else
                {
                    if (!CanSail())
                    {
                        LowerSails();
                        MoveVectorCurrent = Vector3.zero;
                        MoveVectorTarget = Vector3.zero;
                        ResetTimeScale();
                    }

                    //turning sails logic
                    if (Sails.Count > 0)
                    {
                        float hullWindAngle = Vector3.SignedAngle(Vector3.ProjectOnPlane(boatObject.transform.forward, Vector3.up), Vector3.ProjectOnPlane(windVectorCurrent, Vector3.up), Vector3.up);
                        bool upwind = Mathf.Abs(hullWindAngle) > 90;
                        float maxAngle = 60;

                        foreach (Transform sail in Sails)
                        {
                            float windFactor = 0;
                            float sailWindAngle = Vector3.SignedAngle(Vector3.ProjectOnPlane(sail.forward, Vector3.up), Vector3.ProjectOnPlane(windVectorCurrent, Vector3.up), Vector3.up);
                            if (SailsSquare.Contains(sail))
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
                            else if (SailsLateen.Contains(sail))
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
                            else if (SailsGaff.Contains(sail))
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
                            else if (SailsStay.Contains(sail))
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
                                if (SailsSquare.Contains(sail))
                                {
                                    if (animator != null)
                                    {
                                        bool stowed = animator.GetBool("Stowed");
                                        if (upwind && !stowed)
                                        {
                                            animator.CrossFade("Stowed", 1);
                                            animator.SetBool("Stowed", true);
                                        }
                                        else if (!upwind && stowed)
                                        {
                                            animator.CrossFade("Unstowed", 1);
                                            animator.SetBool("Stowed", false);
                                        }
                                    }
                                }
                            }
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
                                lateenAngle = -gaffAngle;
                            }
                            else if (hullWindAngle > 90 && hullWindAngle <= 150)
                            {
                                gaffAngle = Mathf.Lerp(-45, 0, (hullWindAngle - 90) / 60);
                                lateenAngle = -Mathf.Lerp(-45, 0, (hullWindAngle - 90) / 75);
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

                            foreach (Transform boom in Booms)
                            {
                                if (boom.name.Contains("Square"))
                                    boom.localRotation = Quaternion.RotateTowards(boom.localRotation, Quaternion.AngleAxis(squareAngle, Vector3.up), 100 * Time.deltaTime);
                                else if (boom.name.Contains("Lateen"))
                                    boom.localRotation = Quaternion.RotateTowards(boom.localRotation, Quaternion.AngleAxis(lateenAngle, Vector3.up), 100 * Time.deltaTime);
                                else if (boom.name.Contains("Gaff"))
                                {
                                    //if heading towards 0, slow
                                    //if heading away from 0, fast
                                    float gaffSpeed = 100;
                                    float currentAngle = Vector3.SignedAngle(boom.transform.forward, boatObject.transform.forward,boatObject.transform.up);
                                    if ((currentAngle < 0 && currentAngle < gaffAngle) || (currentAngle > 0 && currentAngle > gaffAngle))
                                        gaffSpeed = 300;
                                    boom.localRotation = Quaternion.RotateTowards(boom.localRotation, Quaternion.AngleAxis(gaffAngle, Vector3.up), gaffSpeed * Time.deltaTime);
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

                    TurnTarget = turn * (MoveVectorCurrent.magnitude/1);
                    MoveVectorTarget = Vector3.forward * power;


                    //update rudder position
                    rudderObject.transform.localRotation = Quaternion.AngleAxis(-inputCurrent * 30f, Vector3.up);
                }

                TurnCurrent = Mathf.MoveTowards(TurnCurrent, TurnTarget * turnSpeed, turnAccel * Time.deltaTime);
                MoveVectorCurrent = Vector3.MoveTowards(MoveVectorCurrent, MoveVectorTarget * moveSpeed, moveAccel * Time.deltaTime);

                //adjust wake
                if (MoveVectorCurrent.magnitude >= wakeThreshold && !wakeEmitter.isEmitting)
                {
                    wakeEmitter.Play();
                    PlayFast(true);
                }
                else if (MoveVectorCurrent.magnitude < wakeThreshold && wakeEmitter.isEmitting)
                {
                    wakeEmitter.Stop();
                    PlaySlow(true);
                }

                wakeEmitterMain.startLifetimeMultiplier = 8 * MoveVectorCurrent.magnitude / moveSpeedSail;
                wakeEmitterMain.startSize = 20 * MoveVectorCurrent.magnitude / moveSpeedSail;

                ParticleSystem.ForceOverLifetimeModule wakeEmitterForceOverLifetime = wakeEmitter.forceOverLifetime;
                wakeEmitterForceOverLifetime.space = ParticleSystemSimulationSpace.World;
                wakeEmitterForceOverLifetime.x = new ParticleSystem.MinMaxCurve(currentVector.x * 2);
                wakeEmitterForceOverLifetime.z = new ParticleSystem.MinMaxCurve(currentVector.z * 2);

                //time acceleration controls
                if (InputManager.Instance.GetKeyDown(keyCodeTimeScaleUp))
                    IncreaseTimeScale();

                if (InputManager.Instance.GetKeyDown(keyCodeTimeScaleDown))
                    DecreaseTimeScale();

                if (InputManager.Instance.GetKeyDown(keyCodeTimeScaleReset))
                    ResetTimeScale();
            }

            //animate the boat
            if (!IsBeached())
            {
                float windStrength = windVectorCurrent.magnitude;
                float turning = MoveVectorCurrent.magnitude < wakeThreshold ? 0 : (TurnCurrent / turnSpeedSail) * (MoveVectorCurrent.magnitude / moveSpeedSail);
                boatMeshOffset.transform.localEulerAngles =
                    (Vector3.forward * (Mathf.Clamp(-turning * 5,-30,30) + (Mathf.Sin(Time.time * 0.5f * windStrength) * windStrength))) +
                    (Vector3.right * (Mathf.Sin(Time.time * windStrength) * windStrength));
            }

            //animate the flag
            if (flagObject != null)
            {
                Vector3 apparentWind = windVectorCurrent + (boatObject.transform.TransformDirection(-MoveVectorCurrent) * 0.1f);
                flagObject.transform.forward = apparentWind.normalized;
                flagEmitterMain.startSpeed = apparentWind.magnitude * 0.5f;
            }

            if (wave && waveObject != null)
            {
                int hour = DaggerfallUnity.Instance.WorldTime.Now.Hour;

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

                    if (currentNeighbors[1,1])
                        currentVector *= -1;

                    if (hour > 18 && hour < 6)
                        currentVector *= -1;
                }
                else
                    currentVector = windVectorCurrent * 0.5f;

                //animate waves
                if (waveFrameTimer < waveFrameTime)
                {
                    waveFrameTimer += Time.deltaTime;
                }
                else
                {
                    waveFrameTimer = 0;

                    if (hour > 6 && hour < 18)
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

            if (sailing && !IsBeached())
            {
                boatObject.transform.Translate((MoveVectorCurrent+ boatObject.transform.InverseTransformDirection(currentVector)) * Time.smoothDeltaTime);
                boatObject.transform.Rotate(Vector3.up * TurnCurrent * Time.smoothDeltaTime);
            }
            else
            {
                if (placing && InputManager.Instance.ActionComplete(InputManager.Actions.ActivateCenterObject) && !GameManager.Instance.PlayerEnterExit.IsPlayerInside)
                {
                    //PlaceBoat();
                    StartCoroutine(PlaceBoatDelayed());
                }
            }
        }

        float GetSailPower()
        {
            float power = 0;

            if (Sails.Count < 1)
                return power;

            foreach (Transform sail in Sails)
            {
                if (sail.gameObject.GetComponent<Animator>().GetBool("Stowed"))
                    continue;

                float sailPower = 0;
                float angleHull = Vector3.Angle(Vector3.ProjectOnPlane(boatObject.transform.forward, Vector3.up), Vector3.ProjectOnPlane(sail.forward, Vector3.up));
                float angleWind = Vector3.Angle(Vector3.ProjectOnPlane(windVectorCurrent, Vector3.up), Vector3.ProjectOnPlane(sail.forward, Vector3.up));

                if (SailsLateen.Contains(sail))
                {
                    //is a lateen sail
                    if (angleWind <= 135)
                        sailPower = Mathf.LerpUnclamped(50, 100, angleWind / 135) / 100;
                    else
                        sailPower = Mathf.LerpUnclamped(100, 0, (angleWind - 135) / 30) / 100;

                    //if lateen sail, apply bad tack modifier
                    if (Vector3.SignedAngle(sail.forward, windVectorCurrent, Vector3.up) > 0)
                        sailPower *= 0.5f;

                    if (sailPower < 0)
                        sailPower *= 0.5f;
                }
                else if (SailsGaff.Contains(sail) || SailsStay.Contains(sail))
                {
                    //is a gaff sail or staysail
                    if (angleWind <= 135)
                        sailPower = Mathf.LerpUnclamped(0, 100, angleWind / 135) / 100;
                    else
                        sailPower = Mathf.LerpUnclamped(100, 0, (angleWind - 135) / 15) / 100;

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

                if (SailsSmall.Contains(sail))
                    sailPower *= 0.5f;

                //Debug.Log("COME SAIL AWAY - SAIL POWER IS " + sailPower.ToString());
                power += sailPower;
            }

            //Debug.Log("COME SAIL AWAY - TOTAL POWER IS " + power.ToString());
            return power;
        }

        void ToggleSails()
        {
            if (Sails.Count < 1)
            {
                DaggerfallUI.AddHUDText("Boat does not have any sail.");
                return;
            }

            if (sailPosition == 0)
                RaiseSails();
            else
                LowerSails();
        }

        void RaiseSails()
        {
            if (!CanSail())
            {
                DaggerfallUI.AddHUDText("Unable to raise sail. Boat is obstructed.");
                return;
            }

            DaggerfallUI.AddHUDText("Sail raised!");

            //GameObjectHelper.ChangeDaggerfallMeshGameObject(boatMesh.GetComponent<DaggerfallMesh>(), 41501);

            if (Sails.Count > 0)
            {
                foreach (Transform sail in Sails)
                {
                    if (SailsSquare.Contains(sail) && trimAutoSquareUpwind)
                    {
                        //skip raising square sail if heading upwind
                        if (Mathf.Abs(Vector3.SignedAngle(Vector3.ProjectOnPlane(boatObject.transform.forward, Vector3.up), Vector3.ProjectOnPlane(windVectorCurrent, Vector3.up), Vector3.up)) > 90)
                            continue;
                    }

                    Animator animator = sail.GetComponent<Animator>();
                    if (animator != null)
                    {
                        animator.CrossFade("Unstowed", 1);
                        animator.SetBool("Stowed", false);
                    }
                }
            }

            sailPosition = 1;

            dfAudioSource.PlayOneShot(SoundClips.EquipStaff,1,DaggerfallUnity.Settings.SoundVolume*sfxVolume);
        }

        void LowerSails()
        {
            DaggerfallUI.AddHUDText("Sail lowered!");

            //GameObjectHelper.ChangeDaggerfallMeshGameObject(boatMesh.GetComponent<DaggerfallMesh>(), 41502);

            if (Sails.Count > 0)
            {
                foreach (Transform sail in Sails)
                {
                    Animator animator = sail.GetComponent<Animator>();
                    if (animator != null)
                    {
                        animator.CrossFade("Stowed", 1);
                        animator.SetBool("Stowed", true);
                    }
                }
            }

            sailPosition = 0;

            dfAudioSource.PlayOneShot(SoundClips.EquipClothing, 1, DaggerfallUnity.Settings.SoundVolume * sfxVolume);
        }

        void ToggleSquareSails()
        {
            if (SailsSquare.Count > 0)
            {
                bool value = SailsSquare[0].gameObject.GetComponent<Animator>().GetBool("Stowed");

                if (value)
                {
                    DaggerfallUI.AddHUDText("Square sails raised!");

                    foreach (Transform sail in SailsSquare)
                    {
                        Animator animator = sail.GetComponent<Animator>();
                        if (animator != null)
                        {
                            animator.CrossFade("Unstowed", 1);
                            animator.SetBool("Stowed", false);
                        }
                    }
                }
                else
                {
                    DaggerfallUI.AddHUDText("Square sails lowered!");

                    foreach (Transform sail in SailsSquare)
                    {
                        Animator animator = sail.GetComponent<Animator>();
                        if (animator != null)
                        {
                            animator.CrossFade("Stowed", 1);
                            animator.SetBool("Stowed", true);
                        }
                    }
                }
            }
        }

        private static void BoardBoat(RaycastHit hit)
        {
            if (hit.collider.gameObject.GetInstanceID() != Instance.boardTrigger.GetInstanceID())
                return;

            if (GameManager.Instance.PlayerActivate.CurrentMode == PlayerActivateModes.Steal)
            {
                //store the boat
                Instance.PackBoat(true);
            }
            else
            {
                GameManager.Instance.PlayerObject.transform.position = Instance.boardPosition.transform.position;
                GameObjectHelper.AlignControllerToGround(GameManager.Instance.PlayerObject.GetComponent<CharacterController>());
            }
        }

        private static void DriveBoat(RaycastHit hit)
        {
            if (hit.collider.gameObject.GetInstanceID() != Instance.driveTrigger.GetInstanceID())
                return;

            if (Instance.sailing)
                Instance.StopSailing();
            else
                Instance.StartSailing();
        }

        private static void OpenBoatCargo(RaycastHit hit)
        {
            if (hit.collider.gameObject.GetInstanceID() != Instance.cargoTrigger.GetInstanceID())
                return;

            Instance.OpenCargo();
        }

        public void StartSailing()
        {
            DaggerfallUI.AddHUDText("You control the boat!");

            sailing = true;

            if (GameManager.Instance.TransportManager.TransportMode != TransportModes.Foot)
                GameManager.Instance.TransportManager.TransportMode = TransportModes.Foot;

            wakeEmitter.Stop();

            Vector3 oldForward = playerObject.transform.forward;

            playerObject.transform.SetParent(boatObject.transform);
            GameManager.Instance.PlayerMouseLook.SetFacing(0,0);
            playerObject.transform.localPosition = drivePosition.transform.localPosition;
            GameManager.Instance.PlayerMotor.smoothFollowerLerpSpeed = 250f;

            boatMapPixel = GameManager.Instance.PlayerGPS.CurrentMapPixel;

            UpdateBoatCargoMod();

            //boatObject.GetComponent<Collider>().enabled = false;
            boardTrigger.SetActive(false);

            //show sailing objects
            sailingObject.gameObject.SetActive(true);

        }

        public void StopSailing()
        {
            DaggerfallUI.AddHUDText("You stop controlling the boat!");

            sailing = false;

            if (sailPosition > 0)
                LowerSails();

            wakeEmitter.Stop();

            Vector3 oldForward = playerObject.transform.forward;

            playerObject.transform.SetParent(null);
            GameManager.Instance.PlayerMouseLook.SetHorizontalFacing(oldForward);

            GameManager.Instance.PlayerMotor.smoothFollowerLerpSpeed = 25f;

            //GameObjectHelper.ChangeDaggerfallMeshGameObject(boatMesh.GetComponent<DaggerfallMesh>(), 41502);
            sailPosition = 0;

            MoveVectorTarget = Vector3.zero;
            MoveVectorCurrent = Vector3.zero;
            TurnCurrent = 0;
            TurnTarget = 0;

            lastWeight = 0;

            boatMapPixel = GameManager.Instance.PlayerGPS.CurrentMapPixel;

            ResetTimeScale();

            //boatObject.GetComponent<Collider>().enabled = true;
            boardTrigger.SetActive(true);

            //hide sailing objects
            sailingObject.gameObject.SetActive(false);
        }

        bool CanSail()
        {
            foreach (int index in boatNodeTileMapIndices)
            {
                if (index != 0)
                    return false;
            }

            return true;
        }
        bool IsBeached()
        {
            int count = 0;
            foreach (int index in boatNodeTileMapIndices)
            {
                if (index != 0)
                    count++;

                if (count > 3)
                    return true;
            }

            return false;
        }

        bool IsNodeOnWater(int index)
        {
            if (boatNodeTileMapIndices[index] != 0)
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
            if (timeScaleIndex > 0)
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

        public void StartPlacing(DaggerfallUnityItem item)
        {
            if (placing)
                return;

            placing = true;
            placeItem = item;

            DaggerfallUI.SetMidScreenText("Place the boat in water", 3);
        }

        public void StopPlacing()
        {
            placing = false;
            placeItem = null;
        }

        public void PackBoat(bool item = false)
        {
            boatMapPixel = null;
            boatObject.SetActive(false);

            if (item)
            {
                if (!GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, ItemSkiff.templateIndex))
                {
                    DaggerfallUI.AddHUDText("The boat collapses for storage.");
                    DaggerfallUnityItem newBoatItem = ItemBuilder.CreateItem(ItemGroups.UselessItems2, ItemSkiff.templateIndex);
                    newBoatItem.weightInKg += boatCargo.Items.GetWeight();
                    GameManager.Instance.PlayerEntity.Items.AddItem(newBoatItem);
                }
                else
                {
                    DaggerfallUI.AddHUDText("You already have a boat.");
                }
            }
        }

        void PlaceBoat()
        {
            Ray ray = new Ray(GameManager.Instance.MainCameraObject.transform.position, GameManager.Instance.MainCameraObject.transform.forward);
            RaycastHit hit = new RaycastHit();
            LayerMask layerMask = ~(1 << LayerMask.NameToLayer("Player"));
            layerMask = layerMask & ~(1 << LayerMask.NameToLayer("Ignore Raycast"));
            if (Physics.Raycast(ray, out hit, 100f, layerMask))
            {
                if (GetTileMapIndexAtPosition(hit.point, hit.collider.transform) == 0)
                {
                    DaggerfallUI.AddHUDText("Boat placed!");
                    SetBoatPositionAndDirection(new Vector3(hit.point.x, hit.point.y + 1, hit.point.z), GameManager.Instance.PlayerObject.transform.right);
                    if (placeItem != null)
                        GameManager.Instance.PlayerEntity.Items.RemoveItem(placeItem);
                    PlaySlow();
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

        IEnumerator PlaceBoatDelayed()
        {
            placing = false;
            Ray ray = new Ray(GameManager.Instance.MainCameraObject.transform.position, GameManager.Instance.MainCameraObject.transform.forward);
            RaycastHit hit = new RaycastHit();
            LayerMask layerMask = ~(1 << LayerMask.NameToLayer("Player"));
            layerMask = layerMask & ~(1 << LayerMask.NameToLayer("Ignore Raycast"));
            if (Physics.Raycast(ray, out hit, 100f, layerMask))
            {
                yield return new WaitForSeconds(0.2f);
                if (GetTileMapIndexAtPosition(hit.point, hit.collider.transform) == 0)
                {
                    DaggerfallUI.AddHUDText("Boat placed!");
                    SetBoatPositionAndDirection(new Vector3(hit.point.x, hit.point.y + 1, hit.point.z), GameManager.Instance.PlayerObject.transform.right);
                    GameManager.Instance.PlayerEntity.Items.RemoveItem(placeItem);
                    PlaySlow();
                }
                else
                {
                    DaggerfallUI.SetMidScreenText("Boat can only be placed on water!", 3);
                }
            }
            else
            {
                DaggerfallUI.AddHUDText("Placement aborted!", 3);
            }
            StopPlacing();
        }

        public void SetBoatPositionAndDirection(Vector3 position, Vector3 direction)
        {
            wakeEmitter.Stop();
            boatObject.SetActive(true);
            boatObject.transform.position = position;
            boatObject.transform.forward = direction;
            boatMapPixel = GameManager.Instance.PlayerGPS.CurrentMapPixel;
            UpdateBoatNodes();
        }

        public void OpenCargo()
        {
            DaggerfallUI.Instance.InventoryWindow.LootTarget = boatCargo;
            DaggerfallUI.PostMessage(DaggerfallUIMessages.dfuiOpenInventoryWindow);
        }

        void PlaySlow(bool crossfade = false)
        {
            if (crossfade)
                CrossfadeAudioSource(audioSourceFast, audioSourceSlow);
            else
            {
                audioSourceFast.Stop();
                FadeAudioSource(audioSourceSlow, 0, DaggerfallUnity.Settings.SoundVolume * sfxVolume);
            }
        }
        void PlayFast(bool crossfade = false)
        {
            if (crossfade)
                CrossfadeAudioSource(audioSourceSlow, audioSourceFast);
            else
            {
                audioSourceSlow.Stop();
                FadeAudioSource(audioSourceFast, 0, DaggerfallUnity.Settings.SoundVolume * sfxVolume);
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

            /*if (fading != null)
                StopCoroutine(fading);*/

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
    }

    public class ComeSailAwaySaveData : IHasModSaveData
    {
        public DFPosition boatMapPixel;
        public Vector3 boatPosition;
        public Vector3 boatDirection;
        public ItemData_v1[] boatCargoItems;

        public Vector3 windVector;

        public bool sailing;

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
            emptyData.boatMapPixel = null;
            emptyData.boatPosition = Vector3.zero;
            emptyData.boatDirection = Vector3.forward;
            emptyData.windVector = Vector3.forward;
            emptyData.boatCargoItems = null;
            emptyData.sailing = false;
            return emptyData;
        }
        public object GetSaveData()
        {
            ComeSailAwaySaveData data = new ComeSailAwaySaveData();
            data.boatMapPixel = ComeSailAway.Instance.boatMapPixel;
            data.boatPosition = ComeSailAway.Instance.boatObject.transform.position;
            data.boatDirection = ComeSailAway.Instance.boatObject.transform.forward;
            data.windVector = ComeSailAway.Instance.windVectorCurrent;
            data.boatCargoItems = ComeSailAway.Instance.boatCargo.Items.SerializeItems();
            data.sailing = ComeSailAway.Instance.sailing;
            return data;
        }

        public void RestoreSaveData(object dataIn)
        {
            if (ComeSailAway.Instance.sailing)
                ComeSailAway.Instance.StopSailing();

            ComeSailAwaySaveData data = (ComeSailAwaySaveData)dataIn;
            ComeSailAway.Instance.boatMapPixel = data.boatMapPixel;
            ComeSailAway.Instance.windVectorCurrent = data.windVector;

            ComeSailAway.Instance.boatCargo.Items.DeserializeItems(data.boatCargoItems);

            ComeSailAway.Instance.sailing = data.sailing;

            if (ComeSailAway.Instance.boatMapPixel != null)
                ComeSailAway.Instance.SetBoatPositionAndDirection(data.boatPosition, data.boatDirection);
            else
                ComeSailAway.Instance.PackBoat();

            if (ComeSailAway.Instance.sailing)
                ComeSailAway.Instance.StartSailing();
        }
    }
}
