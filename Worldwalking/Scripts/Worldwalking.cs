using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Banking;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.Weather;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Utility.AssetInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.PostProcessing;
using static DaggerfallWorkshop.PlayerGPS;

namespace WorldwalkingMod
{
    public class Worldwalking : MonoBehaviour
    {
        private static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<Worldwalking>();

            mod.IsReady = true;
        }

        public static Worldwalking Instance;

        bool initialized = false;

        DaggerfallUnity dfUnity;
        PlayerEntity playerEntity;

        public Mod BasicRoads;
        public Mod WorldOfDaggerfallTerrain;
        public Mod WarmAshesWilderness;
        public GameObject DynamicMusicObject;
        public GameObject DynamicAmbienceObject;

        public Texture2D WODTerrainHeightMap;

        //settings
        float billboardScaleMod = 1;
        float multiplierTravelSpeed = 1;
        float multiplierFatigueDrain = 1;
        float multiplierSightRadius = 1;
        public int defaultTimeScaleTravel = 1;
        int defaultTimeScaleCamp = 1;
        float fatigueWarningThreshold = 0.2f;
        bool pauseAtDawn = true;
        bool pauseAtDusk = true;
        bool pauseWhenRested = true;
        bool pauseWhenEnemySighted = true;

        public bool showNavigationPanel = false;
        public float navigationPanelScale = 1f;
        public float navigationMoveSensitivity = 1f;
        public float navigationOrbitSensitivity = 1f;
        public float navigationZoomSensitivity = 1f;

        int encounterChanceWilderness = 2;
        int encounterChanceLocation = 2;

        public float ambientLightMod = 1;
        public float fogDensityMod = 1;
        public int scaleToScreen = 1;   //0 = Do not scale, 1 = Scale to screen height, 2 = Scale to screen dimensions
        public float interfaceScale = 1;
        public float textScaleDefault = 3;
        public float textScaleSDF = 4;

        KeyCode keyboardMapKeyCode = KeyCode.KeypadEnter;

        public int lastInputDeviceIsGamepad; //0 = mouse, 1 = navigation, 2 = gamepad;

        KeyCode gamepadMapKeyCode = KeyCode.JoystickButton8;
        string gamepadDPadAxisX = "Axis7";
        string gamepadDPadAxisY = "Axis8";
        KeyCode gamepadLeftBumperKeyCode = KeyCode.JoystickButton6;
        KeyCode gamepadRightBumperKeyCode = KeyCode.JoystickButton7;
        KeyCode gamepadLeftTriggerKeyCode = KeyCode.JoystickButton6;
        string gamepadLeftTriggerAxis = "Axis9";
        KeyCode gamepadRightTriggerKeyCode = KeyCode.JoystickButton7;
        string gamepadRightTriggerAxis = "Axis10";

        GameObject ParentObject;
        GameObject ChunkParentObject;
        List<GameObject> ChunkObjects;

        public List<GameObject> LocationObjects;
        public List<GameObject> LocationDungeonObjects;
        public List<GameObject> LocationDungeonObjectsDiscovered;
        public List<GameObject> LocationPortObjects;

        GameObject[,] locationObjectsMap = new GameObject[1001, 501];
        //string[,] locationNamesMap = new string[1001, 501];

        public GameObject CameraBaseObject;
        GameObject CameraBaseObjectMesh;
        GameObject CameraPivotObject;
        GameObject CameraObject;
        public Camera Camera;
        PostProcessLayer mapPostprocessingLayer;

        public GameObject PlayerObject;
        GameObject PlayerBillboardObject;
        BillboardPlayer PlayerBillboard;
        GameObject PlayerObjectMeshCart;
        GameObject PlayerObjectMeshBoat;
        GameObject PlayerObjectMeshCamp;
        GameObject PlayerLightObject;
        Light PlayerLight;

        GameObject PlayerFogProjectorObject;
        Projector PlayerFogProjector;

        GameObject RainObject;
        GameObject SnowObject;
        GameObject FogObject;

        GameObject FrameObject;

        public GameObject TargetObject;

        public GameObject PinObject;

        GameObject SunObject;
        Light Sun;
        Gradient DayGradient;
        GradientColorKey[] DayGradientColorKeys = new GradientColorKey[7];
        Gradient NightGradient;
        GradientColorKey[] NightGradientColorKeys = new GradientColorKey[4];
        GradientAlphaKey[] GradientAlphaKeys = new GradientAlphaKey[2];

        Shader splatShader;

        bool wasNight = false;
        bool wasWinter = false;
        bool wasMidday = false;
        Texture2D waterTexture;     //black
        Texture2D grassTexture;     //red
        Texture2D sandTexture;      //green
        Texture2D mountainTexture;  //blue
        Texture2D swampTexture;     //alpha

        //roads
        Texture2D roadTemperate;
        Texture2D trackTemperate;
        Texture2D roadDesert;
        Texture2D trackDesert;

        //triplanar
        Texture2D dirtTexture;
        Texture2D stoneTexture;

        //mapgen
        int worldSmooth = 0;
        int worldSmoothRange = 1;
        bool coast = false;
        float heightScaleMod = 1;
        float WaterLevel
        {
            get
            {
                if (WorldOfDaggerfallTerrain != null)
                    return 3;
                else
                    return 3;
            }
        }

        float[,] heightMap = new float[1001, 501];
        float[,] heightMapSmooth = new float[1001, 501];

        float ceilingLevel
        {
            get
            {
                if (WorldOfDaggerfallTerrain != null)
                    return 252;
                else
                    return 109;
            }
        }

        //functionality
        bool mapActive;
        public bool locked;
        public bool automatic;
        IEnumerator showingMap;
        Vector3 lastMousePosition;

        public Vector2Int lastPlayerMapPixel;
        public Vector3 lastPlayerPosition;
        public Vector3 lastMoveDirection;

        //pin mechanics
        public Vector3 PinPosition = Vector3.zero;

        //lodging mechanics
        Vector2Int lastLodgingPosition;
        float lodgingTimer = 0;

        float shipTimer = 0;

        //camera
        Vector3 basePosition;
        float turnAngle = 0;
        float pitchAngle = 0;
        float zoomLevel = 0;

        //time
        int minutesStart;
        int minutesSince;

        float minuteTime = 1;
        float minuteTimer = 0;
        int hourTimer = 0;

        public int timeScaleIndex = 0;
        float[] timeScales = new float[6] { 0, 30, 60, 90, 180, 360 };
        public float TimeScale
        {
            get
            {
                return timeScales[timeScaleIndex];
            }
        }

        public int travelSpeed = 1;

        bool onShip = false;

        public float sightRadius = 4;
        public float playerStealth = 0;

        //travel variables
        public bool travelling;
        public bool onWater = false;
        public int onRoad = 0;
        public int currentClimate;

        public float inputDelayTimer = 10;
        bool DPadHorizontalPressed;
        bool DPadVerticalPressed;

        Texture2D textureCrosshair;
        public Vector2 crosshairSize;

        //audio
        GameObject InterfaceAudioObject;
        public DaggerfallAudioSource InterfaceAudioSource;
        GameObject PlayerAudioObject;
        DaggerfallAudioSource PlayerAudioSource;
        GameObject AmbientAudioObject;
        DaggerfallAudioSource AmbientAudioSource;

        DaggerfallSongPlayer SongPlayer;
        SongManager SongManager;

        bool fade;
        Texture2D fadeTexture;
        Color fadeColor = Color.black;

        //GUI Rects
        MapWindowPanel MapWindowPanel;

        //GUI tooltip
        public string tooltipString = string.Empty;
        Vector2Int lastCursorPixel = Vector2Int.zero;

        //GUI navigation
        float GUIMoveVertical = 0;
        public float GUIMoveVerticalTarget = 0;
        float GUIMoveHorizontal = 0;
        public float GUIMoveHorizontalTarget = 0;
        float GUIOrbit = 0;
        public float GUIOrbitTarget = 0;
        float GUIZoom = 0;
        public float GUIZoomTarget = 0;

        //fast blur variables
        private float avgR = 0;
        private float avgG = 0;
        private float avgB = 0;
        private float avgA = 0;
        private float blurPixelCount = 0;

        Vector2 mapPixelSpawned;
        List<GameObject> objectsSpawned = new List<GameObject>();

        //Warm Ashes Encounter
        bool runEncounter = false;
        EncounterTemplate currentEncounterTemplate = null;
        /*string currentEncounterQuestName = string.Empty;
        string currentEncounterModMessage = string.Empty;
        int currentEncounterIterations = 1;*/

        //store WA quest names here
        //events that can trigger when entering certain location types
        //city events
        string[] QuestsCityDay = new string[]
        {
            "WAQ_CITIES_QUEST01",   //someone approaches you about your foreign-ness
            "WAQ_CITIES_QUEST02",   //hand-to-hand competition
            "WAQ_CITIES_QUEST03",   //wood-chopping competition
            "WAQ_CITIES_QUEST04",   //leaflet for a bounty (nightblade)
            "WAQ_CITIES_QUEST05",   //found noble's money bag
            //"WAQ_CITIES_QUEST06",   //travel tax
            "WAQ_CITIES_QUEST07",   //letter from an old friend (to a trap)
            "WAQ_CITIES_QUEST08",   //archery competition
            "WAQ_CITIES_QUEST09",   //wounded peasant requiring field medicine
            "WAQ_CITIES_QUEST10",   //a game of baseball(cricket?)
            "WAQ_CITIES_QUEST11",   //lost child
            "WAQ_CITIES_QUEST12",   //someone asks you about your god
            "WAQ_CITIES_QUEST14",   //argonian in trouble
            "WAQ_CITIES_QUEST15",   //daedric translation
            "WAQ_CITIES_QUEST16",   //request for restoration
            "WAQ_CITIES_QUEST17",   //diseased beggar
            "WAQ_CITIES_QUEST18",   //pick a lock
        };
        string[] QuestsCityNight = new string[]
        {
            "WAQ_CITIES_NIGHT_QUEST01", //someone asks you about your god
            "WAQ_CITIES_NIGHT_QUEST02", //a request for an escort to a cemetery... or is it?
            "WAQ_CITIES_NIGHT_QUEST04", //assaulted by a necromancer
        };

        //farm events   -
        string[] QuestsWODFarm = new string[]
        {
            "WAQ_FARM_WOD"  //deliver a letter, defend a farm from orcs, defend a farm from bears
        };

        //farm events   -
        string[] QuestsWODGuard = new string[]
        {
            "WAQ_GUARDTOWER_WOD"    //deliver a letter, search for a bandit hideout, escort a child to safety
        };

        //graveyard events
        string[] QuestsGraveyard = new string[]
        {
            "WAQ_GRAVE_GHOSTS",
            "WAQ_GRAVE_LICHS",
            "WAQ_GRAVE_MUMMYS",
            "WAQ_GRAVE_NECRO",
            "WAQ_GRAVE_PRIESTS",
            "WAQ_GRAVE_SKELETONS",
            "WAQ_GRAVE_VAMPIRES",
            "WAQ_GRAVE_WEREWOLVES",
            "WAQ_GRAVE_ZOMBIES",
        };

        //keep events
        string[] QuestsKeep = new string[]
        {
            "WAQ_SIEGE_ALLY",
            "WAQ_SIEGE_RANDOM",
        };

        //wilderness events - events that show up as markers, player must aproach them to trigger
        string[] QuestsWildernessDay = new string[]
        {
            "WAQ_WILD_DAY_EVENT01",     //bandits
            "WAQ_WILD_DAY_EVENT02",     //mugger
            "WAQ_WILD_DAY_EVENT03",     //suspicious details
            "WAQ_WILD_DAY_EVENT04",     //travelling priest
            "WAQ_WILD_DAY_EVENT05",     //highwayman
            "WAQ_WILD_DAY_EVENT06",     //travelling friendly soldiers
            "WAQ_WILD_DAY_EVENT07",     //hunting ranger
        };

        string[] QuestsWildernessNight = new string[]
        {
            "WAQ_WILD_NIGHT_EVENT01",   //necromancer attack
            "WAQ_WILD_NIGHT_EVENT02",   //wilderness zombie ambush
            "WAQ_WILD_NIGHT_EVENT03",   //statue turned gargoyle ambush
        };

        //encounter events  - mobile events that the player must avoid (except Knights)
        string[] QuestsMobiles = new string[4]
        {
            "WAQ_ENC_BANDITS",
            "WAQ_ENC_DAEDRA",
            "WAQ_ENC_GARGOYLES",
            "WAQ_ENC_VAMPIRES",
        };

        public class EncounterTemplate
        {
            //what sort of encounter is this
            //the quest to be started when the encounter is triggered
            public string questName = string.Empty;

            //if questname is empty figure out a way to store mod message stuff to call them that way
            public string modMessageName = string.Empty;
            public int iterations = 1;

            //the string to display when this encounter is triggered
            public string message = "Your journey is interrupted...";

            //what texture to load for the billboard
            public int[] billboardArchives;

            //used for static animated flats
            public int[] billboardRecords;
            public int[] billboardFrameCounts;

            //marker behavior
            public bool isMobile = false;   //will move marker around the origin point
            public bool isEnemy = false;    //will chase the player if they're close enough
            public bool isFlying = false;    //will chase the player over land and sea
            public int maxLevel = -1;

            //water
            public int shipModel = 0;  //0 = shipwreck, 1 = small ship, 2 = large galley

            public EncounterTemplate(string newQuestName, string newModMessage, int[] archives, int[] records, int[] frameCounts, bool mobile = false, bool enemy = false, int iteration = 1, bool flying = false, int ship = -1, int ceiling = -1)
            {
                questName = newQuestName;
                modMessageName = newModMessage;
                iterations = iteration;
                billboardArchives = archives;
                billboardRecords = records;
                billboardFrameCounts = frameCounts;


                isMobile = mobile;
                isEnemy = enemy;
                isFlying = flying;
                shipModel = ship;
                maxLevel = ceiling;
            }
        }

        public EncounterTemplate[] encounterTemplates = new EncounterTemplate[]
        {
            new EncounterTemplate(string.Empty,"summonBirds",new int[] { 10010 }, new int[] { 42, 43,44,45,46,47,48,49,50 }, new int[] { 10, 1,1,1,8,11,9,15,15 }),      //bird
            new EncounterTemplate(string.Empty,"summonDeer",new int[] { 10009 },new int[] { 7,8,19,20,21,22,23 },new int[] { 1,1,1,1,1,1,1 }),      //deer
            new EncounterTemplate(string.Empty,"summonMapSeller",new int[] { 10015 },new int[] { 0 },new int[] { 1 }),      //cartographer
            new EncounterTemplate(string.Empty,"summonChurch",new int[] { 181 }, new int[] { 0,1,2,3,4 }, new int[] { 4,8,4,2,7 }),      //pilgrims
            new EncounterTemplate(string.Empty,"summonGuards",new int[] { 399 },new int[] { 0 },new int[] { 0 }, true, false),      //guards
            new EncounterTemplate(string.Empty,"summonMerchant",new int[] { 182 }, new int[] { 0, 17 }, new int[] { 12, 1 }),      //merchant
            new EncounterTemplate(string.Empty,"summonBandits",new int[] { 479, 480 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -1, 5),      //bandits
            new EncounterTemplate(string.Empty,"summonBandits",new int[] { 483, 484 },new int[] { 0 },new int[] { 0 }, true, true, 2, false, -1, 10),      //bandits
            new EncounterTemplate(string.Empty,"summonBandits",new int[] { 489, 490 },new int[] { 0 },new int[] { 0 }, true, true, 4, false, -1, 20),      //bandits
            new EncounterTemplate(string.Empty,"summonMountedBandits",new int[] { 1530, 1531 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -1, 7),      //mounted bandits
            new EncounterTemplate(string.Empty,"summonMountedBandits",new int[] { 1532, 1533 },new int[] { 0 },new int[] { 0 }, true, true, 2, false, -1, 14),      //mounted bandits
            //new EncounterTemplate(string.Empty,"summonMountedBandits",new int[] { 1532, 1533 },new int[] { 0 },new int[] { 0 }, true, true, 4, false, -1, 21),      //mounted bandits
            new EncounterTemplate(string.Empty,"summonKnights",new int[] { 481, 482 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -1, 7),      //knights
            new EncounterTemplate(string.Empty,"summonKnights",new int[] { 475, 476 },new int[] { 0 },new int[] { 0 }, true, true, 2, false, -1, 14),      //knights
            new EncounterTemplate(string.Empty,"summonKnights",new int[] { 477, 478 },new int[] { 0 },new int[] { 0 }, true, true, 4, false, -1, 21),      //knights
            new EncounterTemplate(string.Empty,"summonMountedKnights",new int[] { 1500, 1506 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -1, 8),      //mounted knights
            new EncounterTemplate(string.Empty,"summonMountedKnights",new int[] { 1502, 1501 },new int[] { 0 },new int[] { 0 }, true, true, 2, false, -1, 16),      //mounted knights
            //new EncounterTemplate(string.Empty,"summonMountedKnights",new int[] { 1502, 1501 },new int[] { 0 },new int[] { 0 }, true, true, 4, false, -1, 24),      //mounted knights
            new EncounterTemplate(string.Empty,"summonOrcs", new int[] { 262 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -1, 6),      //orcs, small
            new EncounterTemplate(string.Empty,"summonOrcs", new int[] { 267 },new int[] { 0 },new int[] { 0 }, true, true, 2, false, -1, 12),      //orcs, medium
            new EncounterTemplate(string.Empty,"summonOrcs", new int[] { 276 },new int[] { 0 },new int[] { 0 }, true, true, 3, false, -1, 18),      //orcs, large
            new EncounterTemplate(string.Empty,"summonOrcs", new int[] { 279 },new int[] { 0 },new int[] { 0 }, true, true, 4, false, -1, 24),      //orcs, huge
            new EncounterTemplate(string.Empty,"summonBattle",new int[] { 206 }, new int[] { 2,7,9,11,12,13,14 }, new int[] { 1,1,1,1,1,1,1 }),      //battle
            new EncounterTemplate(string.Empty,"summonBears",new int[] { 259 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -1, 5),      //bears
            new EncounterTemplate(string.Empty,"summonTigers",new int[] { 260 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -1, 5),      //
            new EncounterTemplate(string.Empty,"summonHarpies",new int[] { 268 },new int[] { 0 },new int[] { 0 }, true, true, 1, true, -1, 10),      //
            new EncounterTemplate(string.Empty,"summonHarpies",new int[] { 268 },new int[] { 0 },new int[] { 0 }, true, true, 2, true, -1, 20),      //
            new EncounterTemplate(string.Empty,"summonCentaurs",new int[] { 263 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -1, 10),      //
            new EncounterTemplate(string.Empty,"summonSpriggans",new int[] { 257, 265 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -1, 6),      //
            new EncounterTemplate(string.Empty,"summonWerewolf",new int[] { 264 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -1, 6),      //
            new EncounterTemplate(string.Empty,"summonWereboar",new int[] { 269 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -1, 8),      //
            new EncounterTemplate(string.Empty,"summonWerewolf",new int[] { 264 },new int[] { 0 },new int[] { 0 }, true, true, 2, false, -1, 12),      //
            new EncounterTemplate(string.Empty,"summonWereboar",new int[] { 269 },new int[] { 0 },new int[] { 0 }, true, true, 2, false, -1, 16),      //
            new EncounterTemplate(string.Empty,"summonGiants",new int[] { 271 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -1, 6),      //
            new EncounterTemplate(string.Empty,"summonGiants",new int[] { 271 },new int[] { 0 },new int[] { 0 }, true, true, 2, false, -1, 12),      //
            new EncounterTemplate(string.Empty,"summonWolves",new int[] { 1612 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -1, 5),      //
            new EncounterTemplate(string.Empty,"summonWolves",new int[] { 1612 },new int[] { 0 },new int[] { 0 }, true, true, 2, false, -1, 10),      //
            new EncounterTemplate(string.Empty,"summonLions",new int[] { 1617 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -1, 8),      //
            new EncounterTemplate(string.Empty,"summonLions",new int[] { 1617 },new int[] { 0 },new int[] { 0 }, true, true, 2, false, -1, 16),      //
            new EncounterTemplate(string.Empty,"summonBoars",new int[] { 1616 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -1, 6),      //
            new EncounterTemplate(string.Empty,"summonBoars",new int[] { 1616 },new int[] { 0 },new int[] { 0 }, true, true, 2, false, -1, 12),      //
            new EncounterTemplate(string.Empty,"summonGoblin",new int[] { 1600 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -1, 4),      //
            new EncounterTemplate(string.Empty,"summonGoblin",new int[] { 1600 },new int[] { 0 },new int[] { 0 }, true, true, 2, false, -1, 8),      //
            new EncounterTemplate(string.Empty,"summonGoblin",new int[] { 1600 },new int[] { 0 },new int[] { 0 }, true, true, 3, false, -1, 12),      //
            new EncounterTemplate(string.Empty,"summonGoblin",new int[] { 1600 },new int[] { 0 },new int[] { 0 }, true, true, 4, false, -1, 16),      //
            new EncounterTemplate(string.Empty,"summonLizardmen",new int[] { 1601 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -1, 6),      //
            new EncounterTemplate(string.Empty,"summonLizardmen",new int[] { 1607 },new int[] { 0 },new int[] { 0 }, true, true, 2, false, -1, 12),      //
            new EncounterTemplate(string.Empty,"summonTrolls",new int[] { 1618 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -1, 8),      //
            new EncounterTemplate(string.Empty,"summonTrolls",new int[] { 1618 },new int[] { 0 },new int[] { 0 }, true, true, 2, false, -1, 16),      //
            new EncounterTemplate(string.Empty,"summonOgres",new int[] { 1619 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -1, 10),      //
            new EncounterTemplate(string.Empty,"summonOgres",new int[] { 1619 },new int[] { 0 },new int[] { 0 }, true, true, 2, false, -1, 20),      //
            new EncounterTemplate(string.Empty,"summonGhouls",new int[] { 1626 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -1, 6),      //
            new EncounterTemplate(string.Empty,"summonGhouls",new int[] { 1630 },new int[] { 0 },new int[] { 0 }, true, true, 2, false, -1, 12),      //
            new EncounterTemplate(string.Empty,"spyingLanguage",new int[] { 208 },new int[] { 4 },new int[] { 1 }),      //
            new EncounterTemplate("WAQ_WILD_DAY_EVENT01",string.Empty,new int[] { 479, 480 },new int[] { 0 },new int[] { 0 },true,true, 1, false, -1, 9),       //bandit group
            new EncounterTemplate("WAQ_WILD_DAY_EVENT02",string.Empty,new int[] { 483, 484 },new int[] { 0 },new int[] { 0 },true,true, 1, false, -1, 3),       //single mugger
            new EncounterTemplate("WAQ_WILD_DAY_EVENT03",string.Empty,new int[] { 210 },new int[] { 1 },new int[] { 4 }),                 //suspicious location
            new EncounterTemplate("WAQ_WILD_DAY_EVENT04",string.Empty,new int[] { 485, 486 },new int[] { 0 },new int[] { 0 },true,false),      //lone pilgrim
            new EncounterTemplate("WAQ_WILD_DAY_EVENT05",string.Empty,new int[] { 483, 484 },new int[] { 0 },new int[] { 0 },true,true, 1, false, -1, 6),       //single highwayman
            new EncounterTemplate("WAQ_WILD_DAY_EVENT06",string.Empty,new int[] { 477, 478 },new int[] { 0 },new int[] { 0 },true,false),      //travelling friendly soldiers
            new EncounterTemplate("WAQ_WILD_DAY_EVENT07",string.Empty,new int[] { 481, 482 },new int[] { 0 },new int[] { 0 },true,false),      //hunting ranger
            new EncounterTemplate("WAQ_WILD_NIGHT_EVENT01",string.Empty,new int[] { 1515 },new int[] { 0 },new int[] { 0 },true,true, 1, false, -1, 12),    //necromancer assassins
            new EncounterTemplate("WAQ_CITIES_QUEST01",string.Empty,new int[] { 381, 382, 383, 384, 385, 386, 387, 388, 389, 390, 398, 451, 452, 453, 454, 455, 456 },new int[] { 0 },new int[] { 0 },true,true),    //inquiry on citizenship
            new EncounterTemplate("WAQ_CITIES_QUEST02",string.Empty,new int[] { 205 },new int[] { 27 },new int[] { 1 }),                 //hand-to-hand competition
            new EncounterTemplate("WAQ_CITIES_QUEST03",string.Empty,new int[] { 212 },new int[] { 11 },new int[] { 1 }),                 //wood-chopping competition
            new EncounterTemplate("WAQ_CITIES_QUEST04",string.Empty,new int[] { 209 },new int[] { 7 },new int[] { 1 }),                 //leaflet for a bounty (nightblade)
            new EncounterTemplate("WAQ_CITIES_QUEST05",string.Empty,new int[] { 205 },new int[] { 36 },new int[] { 1 }),                 //found noble's money bag
            new EncounterTemplate("WAQ_CITIES_QUEST07",string.Empty,new int[] { 381, 382, 383, 384, 385, 386, 387, 388, 389, 390, 398, 451, 452, 453, 454, 455, 456 },new int[] { 0 },new int[] { 0 },true,true),    //letter from an old friend (to a trap)
            new EncounterTemplate("WAQ_CITIES_QUEST08",string.Empty,new int[] { 211 },new int[] { 20 },new int[] { 1 }),                 //archery competition
            new EncounterTemplate("WAQ_CITIES_QUEST09",string.Empty,new int[] { 96 },new int[] { 4 },new int[] { 1 }),                 //wounded peasant requiring field medicine
            new EncounterTemplate("WAQ_CITIES_QUEST10",string.Empty,new int[] { 207 },new int[] { 6 },new int[] { 1 }),                 //a game of baseball(cricket?)
            new EncounterTemplate("WAQ_CITIES_QUEST11",string.Empty,new int[] { 182 },new int[] { 4, 38, 42, 43,  },new int[] { 1, 1, 1, 1 }),                 //lost child
            new EncounterTemplate("WAQ_CITIES_QUEST12",string.Empty,new int[] { 381, 382, 383, 384, 385, 386, 387, 388, 389, 390, 398, 451, 452, 453, 454, 455, 456 },new int[] { 0 },new int[] { 0 },true,true),    //someone asks you about your god
            new EncounterTemplate("WAQ_CITIES_QUEST14",string.Empty,new int[] { 381, 382, 383, 384, 385, 386, 387, 388, 389, 390, 398, 451, 452, 453, 454, 455, 456 },new int[] { 0 },new int[] { 0 },true,false),    //argonian in trouble
            new EncounterTemplate("WAQ_CITIES_QUEST15",string.Empty,new int[] { 485, 486 },new int[] { 0 },new int[] { 0 },true,false),    //daedric translation
            new EncounterTemplate("WAQ_CITIES_QUEST16",string.Empty,new int[] { 209 },new int[] { 7 },new int[] { 1 }),                 //request for restoration
            new EncounterTemplate("WAQ_CITIES_QUEST17",string.Empty,new int[] { 182 },new int[] { 21, 29, 30, 31 },new int[] { 1, 1, 1, 1 }),                 //diseased beggar
            //new EncounterTemplate("WAQ_CITIES_QUEST18",string.Empty,new int[] { 381, 382, 383, 384, 385, 386, 387, 388, 389, 390, 398, 451, 452, 453, 454, 455, 456 },new int[] { 0 },new int[] { 0 },true,false),    //pick a lock
            new EncounterTemplate("WAQ_CITIES_NIGHT_QUEST01",string.Empty,new int[] { 381, 382, 383, 384, 385, 386, 387, 388, 389, 390, 398, 451, 452, 453, 454, 455, 456 },new int[] { 0 },new int[] { 0 },true,true),    //someone asks you about your god
            new EncounterTemplate("WAQ_CITIES_NIGHT_QUEST02",string.Empty,new int[] { 381, 382, 383, 384, 385, 386, 387, 388, 389, 390, 398, 451, 452, 453, 454, 455, 456 },new int[] { 0 },new int[] { 0 },true,true),    //a request for an escort to a cemetery... or is it?
            new EncounterTemplate("WAQ_CITIES_NIGHT_QUEST03",string.Empty,new int[] { 485, 486 },new int[] { 0 },new int[] { 0 },true,true),    //assaulted by a necromancer
        };

        public EncounterTemplate[] encounterTemplatesWater = new EncounterTemplate[]
        {
            new EncounterTemplate(string.Empty,"summonBandits",new int[] { 479, 480 },new int[] { 0 },new int[] { 0 }, true, true, 2, false, 2, 5),      //bandits
            new EncounterTemplate(string.Empty,"summonBandits",new int[] { 483, 484 },new int[] { 0 },new int[] { 0 }, true, true, 3, false, 3, 10),      //bandits
            new EncounterTemplate(string.Empty,"summonOrcs", new int[] { 276 },new int[] { 0 },new int[] { 0 }, true, true, 3, false, 2, 10),      //orcs, large
            new EncounterTemplate(string.Empty,"summonOrcs", new int[] { 279 },new int[] { 0 },new int[] { 0 }, true, true, 4, false, 3, 20),      //orcs, huge
            new EncounterTemplate(string.Empty,"summonKnights",new int[] { 475, 476 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, 2, 6),      //knights
            new EncounterTemplate(string.Empty,"summonKnights",new int[] { 477, 478 },new int[] { 0 },new int[] { 0 }, true, true, 2, false, 3, 12),      //knights
            new EncounterTemplate(string.Empty,"summonGoblin", new int[] { 276 },new int[] { 0 },new int[] { 0 }, true, true, 3, false, 2, 8),      //goblin, large
            new EncounterTemplate(string.Empty,"summonGoblin", new int[] { 279 },new int[] { 0 },new int[] { 0 }, true, true, 4, false, 3, 16),      //goblin, huge
            new EncounterTemplate(string.Empty,"summonLizardmen", new int[] { 276 },new int[] { 0 },new int[] { 0 }, true, true, 3, false, 2, 6),      //lizardmen, large
            new EncounterTemplate(string.Empty,"summonLizardmen", new int[] { 279 },new int[] { 0 },new int[] { 0 }, true, true, 4, false, 3, 12),      //lizardmen, huge
            new EncounterTemplate(string.Empty,"summonMerchantShip",new int[] { },new int[] { },new int[] { }, true, false, 1, false, 2),      //small merchant ship
            new EncounterTemplate(string.Empty,"summonMerchantWarShip",new int[] { },new int[] { },new int[] { }, true, false, 1, false, 3),      //large merchant ship
            new EncounterTemplate(string.Empty,"summonPirateShip",new int[] { },new int[] { },new int[] { }, true, false, 1, false, 2),      //small pirate ship
            new EncounterTemplate(string.Empty,"summonPirateWarShip",new int[] { },new int[] { },new int[] { }, true, false, 1, false, 3),      //large pirate ship
            new EncounterTemplate(string.Empty,"summonOrcShip",new int[] { },new int[] { },new int[] { }, true, false, 1, false, 2),      //small pirate ship
            new EncounterTemplate(string.Empty,"summonOrcWarShip",new int[] { },new int[] { },new int[] { }, true, false, 1, false, 3),      //large pirate ship
            new EncounterTemplate(string.Empty,"summonHauntedShip",new int[] { },new int[] { },new int[] { }, true, false, 1, false, 2),      //small haunted ship
            new EncounterTemplate(string.Empty,"summonShipWreck",new int[] { },new int[] { },new int[] { }, false, false, 1, false, 0),      //small shipwreck
            new EncounterTemplate(string.Empty,"summonWarShipWreck",new int[] { },new int[] { },new int[] { }, false, false, 1, false, 1),      //large shipwreck
            new EncounterTemplate(string.Empty,"summonHarpies",new int[] { 268 },new int[] { 0 },new int[] { 0 }, true, true, 1, true, -1, 10),      //harpies
            new EncounterTemplate(string.Empty,"summonHarpies",new int[] { 268 },new int[] { 0 },new int[] { 0 }, true, true, 2, true, -1, 20),      //harpies
            new EncounterTemplate(string.Empty,"summonFish",new int[] { 266 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -2, 6),      //slaughterfish
            new EncounterTemplate(string.Empty,"summonFish",new int[] { 266 },new int[] { 0 },new int[] { 0 }, true, true, 2, false, -2, 9),      //slaughterfish
            new EncounterTemplate(string.Empty,"summonLamias",new int[] { 297 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -2, 14),      //lamias
            new EncounterTemplate(string.Empty,"summonDreughs",new int[] { 296 },new int[] { 0 },new int[] { 0 }, true, true, 1, false, -2, 16),      //dreughs
            new EncounterTemplate(string.Empty,"summonMudcrabs",new int[] { 1620 },new int[] { 0 },new int[] { 0 }, true, true, 1, true, -1, 6),      //mudcrabs

        };

        public GameObject EncounterParent;
        List<Encounter> currentEncounters = new List<Encounter>();
        int maxActiveTriggers = 4;

        float triggerChance = 0.25f;
        float triggerChanceBonus = 0.1f;
        int triggerChanceBonusCount;

        float triggerSpawnDistanceMin = 2;
        float triggerSpawnDistanceMax = 6;

        float triggerActiveDistanceThreshold = 12;

        int triggerDurationMin = 1;
        int triggerDurationMax = 3;

        public float mobileSpeedMod = 1.0f;
        public float mobileSightMod = 1.0f;

        public class Encounter
        {
            public GameObject EncounterObject;
            public Vector2Int position;
            public DaggerfallDateTime expiration;

            public EncounterTemplate template;

            public Encounter(Vector2Int newPos, int newLife)
            {
                //position = new Vector2Int(newPos.x + Mathf.RoundToInt(Instance.PlayerObject.transform.localPosition.x), newPos.y + Mathf.RoundToInt(-Instance.PlayerObject.transform.localPosition.z));
                position = newPos;
                expiration = DaggerfallUnity.Instance.WorldTime.Now.Clone();
                expiration.RaiseTime(86400 * newLife);


                //check if position is in water
                bool isWater = Instance.IsOpenWater(position.x, position.y);

                //spawn marker object and place it in world position
                EncounterObject = new GameObject();

                EncounterObject.transform.SetParent(Instance.EncounterParent.transform);
                EncounterObject.transform.localPosition = new Vector3(position.x, 0, -position.y);
                EncounterObject.layer = 2;

                if (isWater)
                {
                    template = Instance.encounterTemplatesWater[UnityEngine.Random.Range(0, Instance.encounterTemplatesWater.Length)];
                    if (template.shipModel == 3)
                    {
                        //large galley
                        GameObject boatObject = GameObjectHelper.CreateDaggerfallMeshGameObject(907, EncounterObject.transform, false, null, true);
                        boatObject.transform.localPosition = Vector3.zero;
                        boatObject.transform.localScale = Vector3.one * 0.02f;
                        boatObject.transform.localEulerAngles = new Vector3(0, -90, 0);
                    }
                    else if (template.shipModel == 2)
                    {
                        //small ship
                        GameObject boatObject = GameObjectHelper.CreateDaggerfallMeshGameObject(906, EncounterObject.transform, false, null, true);
                        boatObject.transform.localPosition = Vector3.zero;
                        boatObject.transform.localScale = Vector3.one * 0.02f;
                        boatObject.transform.localEulerAngles = new Vector3(0, -90, 0);
                    }
                    else if (template.shipModel == 1)
                    {
                        //large shipwreck
                        GameObject boatObject = GameObjectHelper.CreateDaggerfallMeshGameObject(41512, EncounterObject.transform, false, null, true);
                        boatObject.transform.localPosition = Vector3.zero;
                        boatObject.transform.localScale = Vector3.one * 0.02f;
                        boatObject.transform.localEulerAngles = new Vector3(7.5f * (UnityEngine.Random.Range(0, 2) * 2 - 1), -90, 0);
                    }
                    else if (template.shipModel == 0)
                    {
                        //small shipwreck
                        GameObject boatObject = GameObjectHelper.CreateDaggerfallMeshGameObject(41509, EncounterObject.transform, false, null, true);
                        boatObject.transform.localPosition = Vector3.zero;
                        boatObject.transform.localScale = Vector3.one * 0.02f;
                        boatObject.transform.localEulerAngles = new Vector3(7.5f * (UnityEngine.Random.Range(0, 2) * 2 - 1), -90, 0);
                    }
                    else
                    {
                        //use billboard
                        GameObject billboardObject = new GameObject();
                        billboardObject.transform.SetParent(EncounterObject.transform);
                        if (template.isMobile)
                        {
                            EncounterObject.transform.Rotate(Vector3.up, UnityEngine.Random.Range(0, 8) * 45);
                            BillboardMobile billboard = billboardObject.AddComponent<BillboardMobile>();
                            billboard.Initialize(template.billboardArchives[UnityEngine.Random.Range(0, template.billboardArchives.Length)], 0.1f, Instance.billboardScaleMod);
                        }
                        else
                        {
                            Billboard billboard = billboardObject.AddComponent<Billboard>();
                            int record = UnityEngine.Random.Range(0, template.billboardRecords.Length);
                            billboard.Initialize(template.billboardArchives[UnityEngine.Random.Range(0, template.billboardArchives.Length)], template.billboardRecords[record], template.billboardFrameCounts[record], 0.1f, Instance.billboardScaleMod);
                        }
                    }

                    if (template.isMobile)
                    {
                        //add mobile logic to EncounterObject here
                        WorldwalkingMobile mobileLogic = EncounterObject.AddComponent<WorldwalkingMobile>();
                        mobileLogic.Initialize(EncounterObject.transform.position, template.isEnemy, template.isFlying, template.shipModel != -1, template.maxLevel);
                    }

                }
                else
                {
                    template = Instance.encounterTemplates[UnityEngine.Random.Range(0, Instance.encounterTemplates.Length)];

                    GameObject billboardObject = new GameObject();
                    billboardObject.transform.SetParent(EncounterObject.transform);
                    if (template.isMobile)
                    {
                        EncounterObject.transform.Rotate(Vector3.up, UnityEngine.Random.Range(0, 8) * 45);
                        BillboardMobile billboard = billboardObject.AddComponent<BillboardMobile>();
                        billboard.Initialize(template.billboardArchives[UnityEngine.Random.Range(0, template.billboardArchives.Length)], 0.1f, Instance.billboardScaleMod);

                        //add mobile logic to EncounterObject here
                        WorldwalkingMobile mobileLogic = EncounterObject.AddComponent<WorldwalkingMobile>();
                        mobileLogic.Initialize(EncounterObject.transform.position, template.isEnemy, template.isFlying, template.shipModel != -1, template.maxLevel);

                    }
                    else
                    {
                        Billboard billboard = billboardObject.AddComponent<Billboard>();
                        int record = UnityEngine.Random.Range(0, template.billboardRecords.Length);
                        billboard.Initialize(template.billboardArchives[UnityEngine.Random.Range(0, template.billboardArchives.Length)], template.billboardRecords[record], template.billboardFrameCounts[record], 0.1f, Instance.billboardScaleMod);
                    }
                }

                //raycast from above the marker base position and check height of terrain
                Ray ray = new Ray(new Vector3(EncounterObject.transform.position.x, EncounterObject.transform.position.y + 100, EncounterObject.transform.position.z), Vector3.down);
                RaycastHit hit = new RaycastHit();

                float baseHeight = 0;

                if (Physics.Raycast(ray, out hit, 200, Physics.DefaultRaycastLayers))
                {
                    baseHeight = hit.point.y;
                }

                ray = new Ray(new Vector3(EncounterObject.transform.position.x, EncounterObject.transform.position.y + 100, EncounterObject.transform.position.z), Vector3.down);
                if (Physics.Raycast(ray, out hit, 200, Physics.DefaultRaycastLayers))
                {
                    if (hit.point.y > baseHeight)
                        baseHeight = hit.point.y;
                }

                if (baseHeight != 0)
                    EncounterObject.transform.position = new Vector3(EncounterObject.transform.position.x, baseHeight, EncounterObject.transform.position.z);
            }

            public void Destruct()
            {
                Destroy(EncounterObject);
            }
        }

        public void TriggerEncounterObject(GameObject encounterObject)
        {
            for (int i = 0; i < currentEncounters.Count; i++)
            {
                if (currentEncounters[i].EncounterObject == encounterObject)
                {
                    /*currentEncounterQuestName = currentEncounters[i].template.questName;
                    currentEncounterModMessage = currentEncounters[i].template.modMessage;
                    currentEncounterIterations = currentEncounters[i].template.iterations;*/
                    currentEncounterTemplate = currentEncounters[i].template;
                    currentEncounters[i].Destruct();
                    currentEncounters.RemoveAt(i);
                    runEncounter = true;
                    break;
                }
            }
        }

        void ClearEncounters()
        {
            //update existing encounters
            if (currentEncounters.Count > 0)
            {
                //iterate backwards
                for (int i = currentEncounters.Count - 1; i > -1; i--)
                {
                    //remove encounters
                    currentEncounters[i].Destruct();
                    currentEncounters.RemoveAt(i);
                }
            }
        }

        void UpdateEncounters()
        {
            //remove expired encounters
            if (currentEncounters.Count > 0)
            {
                //iterate backwards
                for (int i = currentEncounters.Count-1; i > -1 ; i--)
                {
                    //check expiration date and anchor distance
                    if (currentEncounters[i].expiration.LessThan(dfUnity.WorldTime.Now) || Vector2.Distance(lastPlayerMapPixel, currentEncounters[i].position) > triggerActiveDistanceThreshold)
                    {
                        currentEncounters[i].Destruct();
                        currentEncounters.RemoveAt(i);
                    }
                }
            }

            if (triggerChance > 0 && currentEncounters.Count < maxActiveTriggers)
            {
                //roll
                if (UnityEngine.Random.value < triggerChance + (triggerChanceBonus * triggerChanceBonusCount))
                {
                    //success
                    triggerChanceBonusCount = 0;

                    bool bigEnough = false;
                    Vector2 pos = UnityEngine.Random.insideUnitCircle * triggerSpawnDistanceMax;
                    //make sure the encounter isn't too close to the player
                    while (!bigEnough)
                    {
                        if (pos.magnitude >= triggerSpawnDistanceMin)
                            bigEnough = true;
                        else
                            pos = UnityEngine.Random.insideUnitCircle * triggerSpawnDistanceMax;
                    }
                    Vector2Int position = new Vector2Int(Mathf.RoundToInt(pos.x + Instance.PlayerObject.transform.localPosition.x), Mathf.RoundToInt(pos.y - Instance.PlayerObject.transform.localPosition.z));

                    bool free = false;
                    int breakout = 10;
                    while (!free && breakout > 0)
                    {
                        bool broke = false;

                        if (locationObjectsMap[position.x, position.y] != null)
                            broke = true;

                        if (!broke)
                        {
                            foreach (Encounter encounter in currentEncounters)
                            {
                                if (encounter.position == position)
                                {
                                    broke = true;
                                    break;
                                }
                            }
                        }

                        if (broke)
                        {
                            breakout--;
                            pos = UnityEngine.Random.insideUnitCircle * triggerSpawnDistanceMax;
                            position = new Vector2Int(Mathf.RoundToInt(pos.x + Instance.PlayerObject.transform.localPosition.x), Mathf.RoundToInt(pos.y - Instance.PlayerObject.transform.localPosition.z));
                        }
                        else
                        {
                            free = true;
                        }
                    }

                    currentEncounters.Add(new Encounter(position, UnityEngine.Random.Range(triggerDurationMin, triggerDurationMax)));
                }
                else
                {
                    //failure
                    triggerChanceBonusCount++;
                }
            }
        }

        private void Start()
        {
            if (Instance == null)
                Instance = this;

            ModCompatibilityChecking();

            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();

            dfUnity = DaggerfallUnity.Instance;
            playerEntity = GameManager.Instance.PlayerEntity;

            splatShader = mod.GetAsset<Shader>("Shaders/DaggerfallDefaultSplat.shader");

            textureCrosshair = DaggerfallUI.GetTextureFromResources("Crosshair", out crosshairSize);

            //spawn the parent object
            ParentObject = new GameObject("ParentObject");
            ParentObject.transform.SetParent(transform);
            ParentObject.transform.position = Vector3.down * 1000;

            //spawn the camera
            CameraBaseObject = new GameObject("CameraBase");
            CameraBaseObject.transform.SetParent(ParentObject.transform);
            CameraBaseObjectMesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            CameraBaseObjectMesh.transform.SetParent(CameraBaseObject.transform);
            CameraBaseObjectMesh.transform.localScale = new Vector3(0.1f,100f,0.1f);
            CameraBaseObjectMesh.GetComponent<Collider>().enabled = false;
            CameraBaseObjectMesh.layer = 2;
            MeshRenderer renderer = CameraBaseObjectMesh.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetColor("_Color", Color.green);
            propertyBlock.SetColor("_EmissionColor", Color.green);
            renderer.SetPropertyBlock(propertyBlock);

            CameraPivotObject = new GameObject("CameraPivot");
            CameraPivotObject.transform.SetParent(CameraBaseObject.transform);
            CameraObject = new GameObject("Camera");
            CameraObject.transform.SetParent(CameraPivotObject.transform);
            CameraObject.AddComponent<AudioListener>();

            Camera = CameraObject.AddComponent<Camera>();

            /*CameraObject.SetActive(false);
            PostProcessLayer mainPostprocessingLayer = GameManager.Instance.MainCameraObject.GetComponent<PostProcessLayer>();
            Camera.CopyFrom(GameManager.Instance.MainCamera);
            mapPostprocessingLayer = CopyComponent<PostProcessLayer>(mainPostprocessingLayer, CameraObject);
            mapPostprocessingLayer.volumeTrigger = CameraObject.transform;
            CameraObject.SetActive(true);*/

            Camera.depth = 10;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.farClipPlane = 200;
            Camera.fieldOfView = 30;
            Camera.renderingPath = RenderingPath.Forward;

            CameraPivotObject.transform.localEulerAngles = Vector3.right * 45f;
            CameraObject.transform.localPosition = Vector3.forward * -100f;

            //spawn the player
            PlayerObject = new GameObject("Player");
            PlayerObject.transform.SetParent(ParentObject.transform);
            PlayerBillboardObject = new GameObject("Billboard");
            PlayerBillboardObject.transform.SetParent(PlayerObject.transform);
            PlayerBillboard = PlayerBillboardObject.AddComponent<BillboardPlayer>();
            PlayerBillboard.Initialize(0,0.1f, billboardScaleMod);

            PlayerObjectMeshCart = GameObjectHelper.CreateDaggerfallMeshGameObject(41239, PlayerObject.transform, false, null, true);
            PlayerObjectMeshCart.transform.localPosition = new Vector3(0, 0.1f, -0.2f) * billboardScaleMod;
            PlayerObjectMeshCart.transform.localScale = Vector3.one * 0.1f * billboardScaleMod;

            PlayerObjectMeshBoat = GameObjectHelper.CreateDaggerfallMeshGameObject(906, PlayerObject.transform, false, null, true);
            PlayerObjectMeshBoat.transform.localScale = Vector3.one * 0.02f;
            PlayerObjectMeshBoat.transform.localEulerAngles = new Vector3(0,-90,0);

            PlayerObjectMeshCamp = MeshReplacement.ImportCustomGameobject(45088, PlayerObject.transform, GameManager.Instance.PlayerObject.transform.localToWorldMatrix);
            PlayerObjectMeshCamp.transform.localPosition = Vector3.zero;
            PlayerObjectMeshCamp.transform.localScale = Vector3.one * 0.1f * billboardScaleMod;
            PlayerObjectMeshCamp.GetComponent<MeshCollider>().enabled = false;
            /*GameObject campfire = GameObjectHelper.CreateDaggerfallBillboardGameObject(210, 1, PlayerObjectMeshCamp.transform);
            campfire.transform.localPosition = Vector3.forward * 0.25f;*/

            PlayerLightObject = new GameObject("Light");
            PlayerLightObject.transform.SetParent(PlayerObject.transform);
            PlayerLightObject.transform.localPosition = (Vector3.up * 0.25f) + (Vector3.forward * 0.125f);
            PlayerLight = PlayerLightObject.AddComponent<Light>();
            PlayerLight.type = LightType.Point;
            PlayerLight.range = 1;

            PlayerFogProjectorObject = new GameObject("Fog");
            PlayerFogProjectorObject.transform.SetParent(ParentObject.transform);
            PlayerFogProjectorObject.transform.localEulerAngles = Vector3.right * 90;
            PlayerFogProjector = PlayerFogProjectorObject.AddComponent<Projector>();
            PlayerFogProjector.orthographic = true;
            PlayerFogProjector.nearClipPlane = -50;
            PlayerFogProjector.farClipPlane = 50;
            PlayerFogProjector.material = mod.GetAsset<Material>("Materials/projectorMaterial.mat");
            PlayerFogProjector.ignoreLayers = (1 << LayerMask.NameToLayer("Ignore Raycast"));

            //add child neighbor projectors
            for (int x = -3; x < 4; x++)
            {
                for (int y = -1; y < 2; y++)
                {
                    if (x == 0 && y == 0)
                        continue;

                    GameObject PlayerChildProjector = new GameObject("FogChild");
                    PlayerChildProjector.transform.SetParent(PlayerFogProjectorObject.transform);
                    PlayerChildProjector.transform.localPosition = new Vector3(x * 250, y* 250, 0);
                    PlayerChildProjector.transform.localRotation = Quaternion.identity;
                    Projector childProjector = PlayerChildProjector.AddComponent<Projector>();
                    childProjector.orthographic = true;
                    childProjector.orthographicSize = 1;
                    childProjector.nearClipPlane = -50;
                    childProjector.farClipPlane = 50;
                    childProjector.material = PlayerFogProjector.material;
                    childProjector.ignoreLayers = (1 << LayerMask.NameToLayer("Ignore Raycast"));
                }
            }

            //spawn additional meshes
            TargetObject = new GameObject("Target");
            TargetObject.transform.SetParent(ParentObject.transform);
            GameObject TargetObjectMesh = GameObjectHelper.CreateDaggerfallMeshGameObject(99900, TargetObject.transform, false, null, true);
            TargetObjectMesh.transform.localPosition = Vector3.up * 0.6f;
            TargetObjectMesh.transform.localScale = new Vector3(0.2f, 0.2f, 0.4f);
            TargetObjectMesh.transform.localEulerAngles = new Vector3(90, 0, 0);
            TargetObjectMesh.layer = 2;
            MeshRenderer TargetObjectMeshRenderer = TargetObjectMesh.GetComponent<MeshRenderer>();
            TargetObjectMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            TargetObjectMeshRenderer.receiveShadows = false;
            TargetObjectMeshRenderer.material.EnableKeyword(KeyWords.Emission);
            TargetObjectMeshRenderer.material.SetTexture("_EmissionMap", TargetObjectMeshRenderer.material.GetTexture("_MainTex"));
            TargetObjectMeshRenderer.material.SetColor("_EmissionColor", Color.white);
            TargetObject.SetActive(false);

            PinObject = new GameObject("Pin");
            PinObject.transform.SetParent(ParentObject.transform);
            GameObject PinObjectMesh = GameObjectHelper.CreateDaggerfallMeshGameObject(99900, PinObject.transform, false, null, true);
            PinObjectMesh.transform.localPosition = Vector3.up * 0.6f;
            PinObjectMesh.transform.localScale = new Vector3(0.2f, 0.2f, 0.4f);
            PinObjectMesh.transform.localEulerAngles = new Vector3(90, 0, 0);
            PinObjectMesh.layer = 2;
            MeshRenderer PinObjectMeshRenderer = PinObjectMesh.GetComponent<MeshRenderer>();
            PinObjectMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            PinObjectMeshRenderer.receiveShadows = false;
            PinObjectMeshRenderer.material.EnableKeyword(KeyWords.Emission);
            PinObjectMeshRenderer.material.SetTexture("_MainTex", dfUnity.MaterialReader.TextureReader.GetTexture2D(0, 100));
            PinObjectMeshRenderer.material.SetTexture("_EmissionMap", PinObjectMeshRenderer.material.GetTexture("_MainTex"));
            PinObjectMeshRenderer.material.SetColor("_EmissionColor", Color.white);
            PinObject.SetActive(false);

            //spawn light
            SunObject = new GameObject("Sun");
            SunObject.transform.SetParent(ParentObject.transform);
            Sun = SunObject.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.shadows = LightShadows.Soft;
            GradientAlphaKeys[0] = new GradientAlphaKey(1f, 0f);
            GradientAlphaKeys[1] = new GradientAlphaKey(1f, 1f);
            DayGradientColorKeys[0] = new GradientColorKey(Color.black, 0);
            DayGradientColorKeys[1] = new GradientColorKey(Color.Lerp(Color.red, Color.yellow, 0.5f), 0.1f);
            DayGradientColorKeys[2] = new GradientColorKey(Color.Lerp(Color.white, Color.blue, 0.2f), 0.2f);
            DayGradientColorKeys[3] = new GradientColorKey(Color.white, 0.5f);
            DayGradientColorKeys[4] = new GradientColorKey(Color.Lerp(Color.white, Color.blue, 0.2f), 0.8f);
            DayGradientColorKeys[5] = new GradientColorKey(Color.Lerp(Color.red, Color.yellow, 0.5f), 0.9f);
            DayGradientColorKeys[6] = new GradientColorKey(Color.black, 1.0f);
            NightGradientColorKeys[0] = new GradientColorKey(Color.black, 0);
            NightGradientColorKeys[1] = new GradientColorKey(Color.Lerp(Color.blue, Color.black, 0.5f), 0.33f);
            NightGradientColorKeys[2] = new GradientColorKey(Color.Lerp(Color.blue, Color.black, 0.5f), 0.67f);
            NightGradientColorKeys[3] = new GradientColorKey(Color.black, 1.0f);
            DayGradient = new Gradient();
            DayGradient.SetKeys(DayGradientColorKeys, GradientAlphaKeys);
            NightGradient = new Gradient();
            NightGradient.SetKeys(NightGradientColorKeys, GradientAlphaKeys);

            RainObject = Instantiate(mod.GetAsset<GameObject>("Prefabs/Rain.prefab"), PlayerObject.transform, false);
            SnowObject = Instantiate(mod.GetAsset<GameObject>("Prefabs/Snow.prefab"), PlayerObject.transform, false);

            FrameObject = Instantiate(mod.GetAsset<GameObject>("Prefabs/MapFrame.prefab"), ParentObject.transform, false);
            FrameObject.transform.localPosition = new Vector3(500, 0, -250);
            FrameObject.transform.localEulerAngles = new Vector3(-90, 0, 0);
            FrameObject.transform.localScale = new Vector3(200, 167, 100);
            FrameObject.layer = 2;

            //custom fader
            //setup fake screen blocker
            fadeTexture = new Texture2D(2, 2);
            fadeTexture.filterMode = FilterMode.Point;
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    fadeTexture.SetPixel(x, y, Color.black);
                }
            }
            fadeTexture.Apply();

            //audio
            InterfaceAudioObject = new GameObject("Audio Interface");
            InterfaceAudioObject.transform.SetParent(CameraObject.transform);
            InterfaceAudioSource = InterfaceAudioObject.AddComponent<DaggerfallAudioSource>();
            InterfaceAudioSource.AudioSource.loop = true;

            PlayerAudioObject = new GameObject("Audio Player");
            PlayerAudioObject.transform.SetParent(PlayerObject.transform);
            PlayerAudioSource = PlayerAudioObject.AddComponent<DaggerfallAudioSource>();
            PlayerAudioSource.AudioSource.loop = true;
            PlayerAudioSource.AudioSource.minDistance = 1;
            PlayerAudioSource.AudioSource.maxDistance = 50;

            AmbientAudioObject = new GameObject("Audio Ambient");
            AmbientAudioObject.transform.SetParent(CameraBaseObject.transform);
            AmbientAudioSource = AmbientAudioObject.AddComponent<DaggerfallAudioSource>();
            AmbientAudioSource.AudioSource.loop = true;
            AmbientAudioSource.AudioSource.minDistance = 1;
            AmbientAudioSource.AudioSource.maxDistance = 100;

            //bgm
            GameObject SongPlayerObject = new GameObject("BGM");
            SongPlayerObject.transform.SetParent(CameraObject.transform);
            SongPlayerObject.AddComponent<AudioSource>();
            SongPlayer = SongPlayerObject.AddComponent<DaggerfallSongPlayer>();
            SongPlayer.Gain = 2f;
            SongManager = SongPlayerObject.AddComponent<SongManager>();

            //encounter parent
            EncounterParent = new GameObject("Encounters");
            EncounterParent.transform.SetParent(ParentObject.transform);
            EncounterParent.transform.localPosition = Vector3.zero;

            //spawn the chunks
            ChunkParentObject = new GameObject("ChunkParentObject");
            ChunkParentObject.transform.SetParent(ParentObject.transform);
            ChunkObjects = new List<GameObject>();
            LocationObjects = new List<GameObject>();
            LocationDungeonObjects = new List<GameObject>();
            LocationDungeonObjectsDiscovered = new List<GameObject>();
            LocationPortObjects = new List<GameObject>();
            SetTextures(true);
            CreateChunk(0);
            CreateChunk(1);
            CreateChunk(2);
            CreateChunk(3);
            CreateChunk(4);
            CreateChunk(5);
            CreateChunk(6);
            CreateChunk(7);
            ChunkParentObject.transform.localPosition = new Vector3(-0.5f, 0, 0.5f);

            playerEntity.OnExhausted += OnExhausted;

            SaveLoadManager.OnLoad += OnLoad;
            StartGameBehaviour.OnStartGame += OnStartGame;
            PlayerEnterExit.OnPreTransition += OnPreTransition;
            //GameManager.OnEnemySpawn += OnEnemySpawn;

            AdjustLocations(true);
            DisableMap(false, true);

            MapWindowPanel = new MapWindowPanel();
            MapWindowPanel.Setup();

            initialized = true;
        }

        void UpdatePlayerObject()
        {
            PlayerBillboard.Initialize(0, 0.1f, billboardScaleMod);

            PlayerObjectMeshCart.transform.localPosition = new Vector3(0, 0.1f, -0.2f) * billboardScaleMod;
            PlayerObjectMeshCart.transform.localScale = Vector3.one * 0.1f * billboardScaleMod;

            //PlayerObjectMeshBoat.transform.localScale = Vector3.one * 0.02f * billboardScaleMod;

            PlayerObjectMeshCamp.transform.localScale = Vector3.one * 0.1f * billboardScaleMod;
        }

        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("Generation"))
            {
                worldSmooth = settings.GetInt("Generation", "HeightReading");
                worldSmoothRange = settings.GetInt("Generation", "SmoothingRadius");
                heightScaleMod = settings.GetInt("Generation", "MapHeightScale")/100f;
                coast = settings.GetBool("Generation", "DrawCoasts");
            }
            if (change.HasChanged("Main"))
            {
                float billboardScale = settings.GetInt("Main", "BillboardScale") / 100f;
                if (billboardScaleMod != billboardScale)
                {
                    billboardScaleMod = billboardScale;
                    if (initialized)
                        UpdatePlayerObject();
                }
                multiplierTravelSpeed = settings.GetInt("Main", "TravelSpeedMultiplier")/100f;
                multiplierFatigueDrain = settings.GetInt("Main", "FatigueDrainMultiplier")/100f;
                multiplierSightRadius = settings.GetInt("Main", "SightRadiusMultiplier")/100f;
                defaultTimeScaleTravel = settings.GetInt("Main", "DefaultTravelingTimeScale");
                defaultTimeScaleCamp = settings.GetInt("Main", "DefaultCampingTimeScale");
                fatigueWarningThreshold = settings.GetInt("Main", "LowFatigueThreshold")/100f;
                pauseAtDawn = settings.GetBool("Main", "PauseAtDawn");
                pauseAtDusk = settings.GetBool("Main", "PauseAtDusk");
                pauseWhenRested = settings.GetBool("Main", "PauseWhenFullyRested");
                pauseWhenEnemySighted = settings.GetBool("Main", "PauseWhenEnemySighted");
                encounterChanceWilderness = settings.GetInt("Main", "WildernessEncounterChance");
                encounterChanceLocation = settings.GetInt("Main", "LocationEncounterChance");
                triggerChance = settings.GetInt("Main", "TriggerEncounterChance") / 100f;
                triggerChanceBonus = settings.GetInt("Main", "TriggerEncounterChanceBonus") / 100f;
                maxActiveTriggers = settings.GetInt("Main", "MaxActiveTriggers");
                triggerActiveDistanceThreshold = settings.GetInt("Main", "TriggerActiveDistanceThreshold");
                triggerSpawnDistanceMin = settings.GetInt("Main", "TriggerSpawnDistanceMin");
                triggerSpawnDistanceMax = settings.GetInt("Main", "TriggerSpawnDistanceMax");
                triggerDurationMin = settings.GetInt("Main", "TriggerDurationMin");
                triggerDurationMax = settings.GetInt("Main", "TriggerDurationMax");
                mobileSpeedMod = settings.GetInt("Main", "MobileTriggerSpeed") / 100f;
                mobileSightMod = settings.GetInt("Main", "MobileTriggerSight") / 100f;
            }
            if (change.HasChanged("Interface"))
            {
                markerStyle = settings.GetInt("Interface", "PlayerMarkerStyle");
                markerScale = settings.GetInt("Interface", "PlayerMarkerScale")/100f;
                markerDistance = settings.GetInt("Interface", "PlayerMarkerDistance");
                ambientLightMod = settings.GetInt("Interface", "MapAmbientLight") / 100f;
                fogDensityMod = settings.GetInt("Interface", "MapFogDensity") / 100f;
                scaleToScreen = settings.GetInt("Interface", "ScalingMode");
                interfaceScale = settings.GetFloat("Interface", "InterfaceScale");
                textScaleDefault = settings.GetFloat("Interface", "DefaultTextScale");
                textScaleSDF = settings.GetFloat("Interface", "SDFTextScale");
                showNavigationPanel = settings.GetBool("Interface", "ShowNavigationPanel");
                navigationPanelScale = settings.GetInt("Interface", "NavigationPanelScale")/100f;
                navigationMoveSensitivity = settings.GetInt("Interface", "NavigationMoveSensitivity")/100f;
                navigationOrbitSensitivity = settings.GetInt("Interface", "NavigationMoveSensitivity")/100f;
                navigationZoomSensitivity = settings.GetInt("Interface", "NavigationZoomSensitivity")/100f;
                if (MapWindowPanel != null)
                    MapWindowPanel.UpdateInterface();
            }
            if (change.HasChanged("Keyboard"))
            {
                keyboardMapKeyCode = SetKeyFromText(settings.GetString("Keyboard", "ToggleMap"));
            }
            if (change.HasChanged("Gamepad"))
            {
                gamepadMapKeyCode = SetKeyFromText(settings.GetString("Gamepad", "ToggleMap"));
                gamepadDPadAxisX = settings.GetValue<string>("Gamepad", "DPadXAxis");
                gamepadDPadAxisY = settings.GetValue<string>("Gamepad", "DPadYAxis");
                gamepadLeftBumperKeyCode = SetKeyFromText(settings.GetString("Gamepad", "LeftBumperKeyCode"));
                gamepadRightBumperKeyCode = SetKeyFromText(settings.GetString("Gamepad", "RightBumperKeyCode"));
                gamepadLeftTriggerKeyCode = SetKeyFromText(settings.GetString("Gamepad", "LeftTriggerKeyCode"));
                gamepadLeftTriggerAxis = settings.GetValue<string>("Gamepad", "LeftTriggerAxis");
                gamepadRightTriggerKeyCode = SetKeyFromText(settings.GetString("Gamepad", "RightTriggerKeyCode"));
                gamepadRightTriggerAxis = settings.GetValue<string>("Gamepad", "RightTriggerAxis");
            }
        }
        private KeyCode SetKeyFromText(string text)
        {
            Debug.Log("Setting Key");
            if (Enum.TryParse(text, true, out KeyCode result))
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

        bool IsLeftTriggerPressed()
        {
            if (!DaggerfallUnity.Settings.EnableController)
                return false;

            if (InputManager.Instance.GetKey(gamepadLeftTriggerKeyCode) || (gamepadLeftTriggerAxis != "None" && Input.GetAxis(gamepadLeftTriggerAxis) != 0))
                return true;

            return false;
        }

        bool IsRightTriggerPressed()
        {
            if (!DaggerfallUnity.Settings.EnableController)
                return false;

            if (InputManager.Instance.GetKey(gamepadRightTriggerKeyCode) || (gamepadRightTriggerAxis != "None" && Input.GetAxis(gamepadRightTriggerAxis) != 0))
                return true;

            return false;
        }

        private void OnGUI()
        {
            if (mapActive)
            {
                if (MapWindowPanel != null)
                    MapWindowPanel.Draw();
            }
            /*
            if (mapActive)
            {
                GUI.depth = -1;

                rectPanelVitals.x = screenRect.x;
                rectPanelVitals.y = screenRect.height - texturePanelVitals.height * screenScaleY;
                rectPanelVitals.width = texturePanelVitals.width * screenScaleX;
                rectPanelVitals.height = texturePanelVitals.height * screenScaleY;

                rectPanelSpeed.x = screenRect.x + (screenRect.width * 0.5f) - (texturePanelSpeed.width * 0.5f) * screenScaleX;
                rectPanelSpeed.y = screenRect.height - texturePanelSpeed.height * screenScaleY;
                rectPanelSpeed.width = texturePanelSpeed.width * screenScaleX;
                rectPanelSpeed.height = texturePanelSpeed.height * screenScaleY;

                rectButtonSpeedCamp.x = rectPanelSpeed.x;
                rectButtonSpeedCamp.y = screenRect.height - textureButtonSpeedCamp.height * screenScaleY;
                rectButtonSpeedCamp.width = textureButtonSpeedCamp.width * screenScaleX;
                rectButtonSpeedCamp.height = textureButtonSpeedCamp.height * screenScaleY;

                rectButtonSpeedCautious.x = rectPanelSpeed.x;
                rectButtonSpeedCautious.y = rectButtonSpeedCamp.y - (textureButtonSpeedCautious.height * screenScaleY);
                rectButtonSpeedCautious.width = textureButtonSpeedCautious.width * screenScaleX;
                rectButtonSpeedCautious.height = textureButtonSpeedCautious.height * screenScaleY;

                rectButtonSpeedReckless.x = rectPanelSpeed.x;
                rectButtonSpeedReckless.y = rectButtonSpeedCautious.y - (textureButtonSpeedReckless.height * screenScaleY);
                rectButtonSpeedReckless.width = textureButtonSpeedReckless.width * screenScaleX;
                rectButtonSpeedReckless.height = textureButtonSpeedReckless.height * screenScaleY;

                rectPanelTime.x = screenRect.x;
                rectPanelTime.y = 0;
                rectPanelTime.width = texturePanelTime.width * screenScaleX;
                rectPanelTime.height = texturePanelTime.height * screenScaleY;

                rectButtonTimeScale0.x = rectPanelTime.x;
                rectButtonTimeScale0.y = rectPanelTime.height - textureButtonTimeScale0.height * screenScaleY;
                rectButtonTimeScale0.width = textureButtonTimeScale0.width * screenScaleX;
                rectButtonTimeScale0.height = textureButtonTimeScale0.height * screenScaleY;

                rectButtonTimeScale1.x = rectButtonTimeScale0.x + (textureButtonTimeScale0.width * screenScaleX);
                rectButtonTimeScale1.y = rectPanelTime.height - textureButtonTimeScale1.height * screenScaleY;
                rectButtonTimeScale1.width = textureButtonTimeScale1.width * screenScaleX;
                rectButtonTimeScale1.height = textureButtonTimeScale1.height * screenScaleY;

                rectButtonTimeScale2.x = rectButtonTimeScale1.x + (textureButtonTimeScale1.width * screenScaleX);
                rectButtonTimeScale2.y = rectPanelTime.height - textureButtonTimeScale2.height * screenScaleY;
                rectButtonTimeScale2.width = textureButtonTimeScale2.width * screenScaleX;
                rectButtonTimeScale2.height = textureButtonTimeScale2.height * screenScaleY;

                rectButtonTimeScale3.x = rectButtonTimeScale2.x + (textureButtonTimeScale2.width * screenScaleX);
                rectButtonTimeScale3.y = rectPanelTime.height - textureButtonTimeScale3.height * screenScaleY;
                rectButtonTimeScale3.width = textureButtonTimeScale3.width * screenScaleX;
                rectButtonTimeScale3.height = textureButtonTimeScale3.height * screenScaleY;

                rectButtonTimeScale4.x = rectButtonTimeScale3.x + (textureButtonTimeScale3.width * screenScaleX);
                rectButtonTimeScale4.y = rectPanelTime.height - textureButtonTimeScale4.height * screenScaleY;
                rectButtonTimeScale4.width = textureButtonTimeScale4.width * screenScaleX;
                rectButtonTimeScale4.height = textureButtonTimeScale4.height * screenScaleY;

                rectButtonTimeScale5.x = rectButtonTimeScale4.x + (textureButtonTimeScale4.width * screenScaleX);
                rectButtonTimeScale5.y = rectPanelTime.height - textureButtonTimeScale5.height * screenScaleY;
                rectButtonTimeScale5.width = textureButtonTimeScale5.width * screenScaleX;
                rectButtonTimeScale5.height = textureButtonTimeScale5.height * screenScaleY;

                rectPanelButtons.x = screenRect.x + screenRect.width - texturePanelButtons.width * screenScaleX;
                rectPanelButtons.y = 0 * screenScaleY;
                rectPanelButtons.width = texturePanelButtons.width * screenScaleX;
                rectPanelButtons.height = texturePanelButtons.height * screenScaleY;

                rectButtonsFindLocation.x = rectPanelButtons.x;
                rectButtonsFindLocation.y = rectPanelButtons.y;
                rectButtonsFindLocation.width = texturePanelButtons.width * screenScaleX;
                rectButtonsFindLocation.height = (texturePanelButtons.height / 3) * screenScaleY;

                rectButtonsCenterPlayer.x = rectPanelButtons.x;
                rectButtonsCenterPlayer.y = rectPanelButtons.y + rectButtonsFindLocation.height;
                rectButtonsCenterPlayer.width = rectButtonsFindLocation.width;
                rectButtonsCenterPlayer.height = rectButtonsFindLocation.height;

                rectButtonShip.x = rectPanelButtons.x;
                rectButtonShip.y = rectButtonsCenterPlayer.y + rectButtonsFindLocation.height;
                rectButtonShip.width = rectButtonsFindLocation.width;
                rectButtonShip.height = rectButtonsFindLocation.height;

                rectPanelPins.x = screenRect.x + screenRect.width - texturePanelButtons.width * screenScaleX;
                rectPanelPins.y = screenRect.y + (screenRect.height/2) - (texturePanelPins.height/2) * screenScaleY;
                rectPanelPins.width = texturePanelPins.width * screenScaleX;
                rectPanelPins.height = texturePanelPins.height * screenScaleY;

                rectButtonSetPin.x = rectPanelPins.x;
                rectButtonSetPin.y = rectPanelPins.y;
                rectButtonSetPin.width = texturePanelPins.width * screenScaleX;
                rectButtonSetPin.height = (texturePanelPins.height / 4) * screenScaleY;

                rectButtonGoToPin.x = rectPanelPins.x;
                rectButtonGoToPin.y = rectPanelPins.y + rectButtonSetPin.height;
                rectButtonGoToPin.width = rectButtonSetPin.width;
                rectButtonGoToPin.height = rectButtonSetPin.height;

                rectButtonTravelToPin.x = rectPanelPins.x;
                rectButtonTravelToPin.y = rectButtonGoToPin.y + rectButtonSetPin.height;
                rectButtonTravelToPin.width = rectButtonSetPin.width;
                rectButtonTravelToPin.height = rectButtonSetPin.height;

                rectButtonClearPin.x = rectPanelPins.x;
                rectButtonClearPin.y = rectButtonTravelToPin.y + rectButtonSetPin.height;
                rectButtonClearPin.width = rectButtonSetPin.width;
                rectButtonClearPin.height = rectButtonSetPin.height;

                rectButtonExit.x = screenRect.x + screenRect.width - textureButtonExit.width * screenScaleX;
                rectButtonExit.y = screenRect.height - textureButtonExit.height * screenScaleY;
                rectButtonExit.width = textureButtonExit.width * screenScaleX;
                rectButtonExit.height = textureButtonExit.height * screenScaleY;

                DaggerfallUI.DrawTexture(rectPanelVitals, texturePanelVitals);
                DaggerfallUI.DrawTexture(rectPanelSpeed, texturePanelSpeed);
                DaggerfallUI.DrawTexture(rectPanelTime, texturePanelTime);
                DaggerfallUI.DrawTexture(rectPanelButtons, texturePanelButtons);
                DaggerfallUI.DrawTexture(rectPanelPins, texturePanelPins);
                DaggerfallUI.DrawTexture(rectButtonExit, textureButtonExit);

                float health = (float)playerEntity.CurrentHealth / (float)playerEntity.MaxHealth;
                float fatigue = (float)playerEntity.CurrentFatigue / (float)playerEntity.MaxFatigue;
                float magicka = (float)playerEntity.CurrentMagicka / (float)playerEntity.MaxMagicka;

                rectBarVitalsHealth.x = rectPanelVitals.x + (7 * screenScaleX);
                rectBarVitalsHealth.y = rectPanelVitals.y + (7 * screenScaleY) + ((textureBarVitalsHealth.height * screenScaleY) * (1 - health));
                rectBarVitalsHealth.width = (textureBarVitalsHealth.width) * screenScaleX;
                rectBarVitalsHealth.height = textureBarVitalsHealth.height * screenScaleY * health;

                rectBarVitalsFatigue.x = rectBarVitalsHealth.x + (textureBarVitalsHealth.width + 7 * screenScaleX);
                rectBarVitalsFatigue.y = rectPanelVitals.y + (7 * screenScaleY) + ((textureBarVitalsFatigue.height * screenScaleY) * (1 - fatigue));
                rectBarVitalsFatigue.width = (textureBarVitalsFatigue.width) * screenScaleX;
                rectBarVitalsFatigue.height = textureBarVitalsFatigue.height * screenScaleY * fatigue;

                rectBarVitalsMagicka.x = rectBarVitalsFatigue.x + (textureBarVitalsFatigue.width + 7 * screenScaleX);
                rectBarVitalsMagicka.y = rectPanelVitals.y + (7 * screenScaleY) + ((textureBarVitalsMagicka.height * screenScaleY) * (1 - magicka));
                rectBarVitalsMagicka.width = (textureBarVitalsMagicka.width) * screenScaleX;
                rectBarVitalsMagicka.height = textureBarVitalsMagicka.height * screenScaleY * magicka;

                Rect rectNewHealth = new Rect(0, 0, 1, (textureBarVitalsHealth.height * screenScaleY * health) / (textureBarVitalsHealth.height * screenScaleY));
                Rect rectNewFatigue = new Rect(0, 0, 1, (textureBarVitalsFatigue.height * screenScaleY * fatigue) / (textureBarVitalsFatigue.height * screenScaleY));
                Rect rectNewMagicka = new Rect(0, 0, 1, (textureButtonExit.height * screenScaleY * magicka) / (textureBarVitalsMagicka.height * screenScaleY));

                DaggerfallUI.DrawTextureWithTexCoords(rectBarVitalsHealth, textureBarVitalsHealth, rectNewHealth, true, Color.white);
                DaggerfallUI.DrawTextureWithTexCoords(rectBarVitalsFatigue, textureBarVitalsFatigue, rectNewFatigue, true, Color.white);
                DaggerfallUI.DrawTextureWithTexCoords(rectBarVitalsMagicka, textureBarVitalsMagicka, rectNewMagicka, true, Color.white);

                if (travelSpeed == 2)
                    DaggerfallUI.DrawTexture(rectButtonSpeedReckless, textureButtonSpeedReckless);
                else if (travelSpeed == 1)
                    DaggerfallUI.DrawTexture(rectButtonSpeedCautious, textureButtonSpeedCautious);
                else if (travelSpeed == 0)
                    DaggerfallUI.DrawTexture(rectButtonSpeedCamp, textureButtonSpeedCamp);

                if (timeScaleIndex == 5)
                    DaggerfallUI.DrawTexture(rectButtonTimeScale5, textureButtonTimeScale5);
                else if (timeScaleIndex == 4)
                    DaggerfallUI.DrawTexture(rectButtonTimeScale4, textureButtonTimeScale4);
                else if (timeScaleIndex == 3)
                    DaggerfallUI.DrawTexture(rectButtonTimeScale3, textureButtonTimeScale3);
                else if (timeScaleIndex == 2)
                    DaggerfallUI.DrawTexture(rectButtonTimeScale2, textureButtonTimeScale2);
                else if (timeScaleIndex == 1)
                    DaggerfallUI.DrawTexture(rectButtonTimeScale1, textureButtonTimeScale1);
                else if (timeScaleIndex == 0)
                    DaggerfallUI.DrawTexture(rectButtonTimeScale0, textureButtonTimeScale0);

                //DaggerfallUI.DefaultFont.DrawText(currentTime.DateTimeString(), Vector2.zero + (Vector2.right * (rectPanelTime.x + (rectPanelTime.width * 0.05f))) + (Vector2.up * (rectPanelTime.height * 0.2f)), Vector2.one * 3, Color.yellow, Color.black, DaggerfallUI.DaggerfallDefaultShadowPos * 2);

                if (DaggerfallUnity.Settings.SDFFontRendering)
                {
                    DaggerfallUI.DefaultFont.DrawText(dfUnity.WorldTime.Now.DateTimeString(), Vector2.zero + (Vector2.right * (rectPanelTime.x + (rectPanelTime.width * 0.05f))) + (Vector2.up * (rectPanelTime.height * 0.2f)), Vector2.one * screenScaleY, Color.yellow, Color.black, DaggerfallUI.DaggerfallDefaultShadowPos * 2);
                    if (logStrings.Count > 0)
                    {
                        for (int i = 0; i < logStrings.Count; i++)
                        {
                            DaggerfallUI.DefaultFont.DrawText(logStrings[i], screenRect.position + (Vector2.right * (5 * screenScaleX)) + (Vector2.up * (rectPanelTime.height + (5 * screenScaleY))) + (Vector2.up * ((6 * screenScaleY) * (1 + i))), Vector2.one * screenScaleY, Color.yellow, Color.black, DaggerfallUI.DaggerfallDefaultShadowPos * 2);
                        }
                    }

                    if (tooltipString != string.Empty)
                    {
                        if (lastInputDeviceIsGamepad)
                            DaggerfallUI.DefaultFont.DrawText(tooltipString, new Vector3(Screen.width / 2, Screen.height / 2, 0) + ((Vector3.right + Vector3.down) * 10 * screenScaleY), Vector2.one * screenScaleY, Color.yellow, Color.black, DaggerfallUI.DaggerfallDefaultShadowPos * 2);
                        else
                            DaggerfallUI.DefaultFont.DrawText(tooltipString, new Vector3(Input.mousePosition.x, Screen.height - Input.mousePosition.y, 0) + ((Vector3.right + Vector3.down) * 10 * screenScaleY), Vector2.one * screenScaleY, Color.yellow, Color.black, DaggerfallUI.DaggerfallDefaultShadowPos * 2);

                    }
                }
                else
                {
                    DaggerfallUI.DefaultFont.DrawText(dfUnity.WorldTime.Now.DateTimeString(), Vector2.zero + (Vector2.right * (rectPanelTime.x + (rectPanelTime.width * 0.05f))) + (Vector2.up * (rectPanelTime.height * 0.2f)), Vector2.one * 0.8f * screenScaleY, Color.yellow, Color.black, DaggerfallUI.DaggerfallDefaultShadowPos * 2);
                    if (logStrings.Count > 0)
                    {
                        for (int i = 0; i < logStrings.Count; i++)
                        {
                            DaggerfallUI.DefaultFont.DrawText(logStrings[i], screenRect.position + (Vector2.right * (5 * screenScaleX)) + (Vector2.up * (rectPanelTime.height + (5 * screenScaleY))) + (Vector2.up * ((6 * screenScaleY) * (1 + i))), Vector2.one * 0.8f * screenScaleY, Color.yellow, Color.black, DaggerfallUI.DaggerfallDefaultShadowPos * 2);
                        }
                    }

                    if (tooltipString != string.Empty)
                    {
                        if (lastInputDeviceIsGamepad)
                            DaggerfallUI.DefaultFont.DrawText(tooltipString, new Vector3(Screen.width / 2, Screen.height / 2, 0) + ((Vector3.right + Vector3.down) * 10 * screenScaleY), Vector2.one * 0.8f * screenScaleY, Color.yellow, Color.black, DaggerfallUI.DaggerfallDefaultShadowPos * 2);
                        else
                            DaggerfallUI.DefaultFont.DrawText(tooltipString, new Vector3(Input.mousePosition.x, Screen.height - Input.mousePosition.y, 0) + ((Vector3.right + Vector3.down) * 10 * screenScaleY), Vector2.one * 0.8f * screenScaleY, Color.yellow, Color.black, DaggerfallUI.DaggerfallDefaultShadowPos * 2);

                    }
                }
            }
*/
            GUI.depth = -20;
            DaggerfallUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), fadeTexture, ScaleMode.StretchToFill, false, fadeColor);
        }

        public static void OnStartGame(object sender, EventArgs e)
        {
            Instance.shipTimer = 0;
            Instance.LocationDungeonObjectsDiscovered.Clear();
            Instance.AdjustLocations();
            Instance.PinPosition = Vector3.zero;
            Instance.DisableMap(false, true);
            Instance.ClearSpawnedObjects();
            Instance.ClearEncounters();
        }

        public static void OnLoad(SaveData_v1 saveData)
        {
            Instance.shipTimer = 0;
            Instance.LocationDungeonObjectsDiscovered.Clear();
            Instance.AdjustLocations();
            Instance.PinPosition = Vector3.zero;
            Instance.DisableMap(false, true);
            Instance.ClearSpawnedObjects();
            Instance.ClearEncounters();
        }

        public int markerStyle = 0; //0 = arrow, 1 = portrait, 2 = paperdoll
        public float markerDistance = 30f;
        public float markerScale = 1f;
        Texture2D textureMarker;

        void UpdatePlayerHeadTexture()
        {
            if (markerStyle == 2)
            {
                DaggerfallUI.Instance.PaperDollRenderer.Refresh();
                MapWindowPanel.UpdatePlayerHeadTexture(DaggerfallUI.Instance.PaperDollRenderer.PaperDollTexture, markerScale * 0.02f);
            }
            else if (markerStyle == 1)
            {
                ImageData head;

                // Check for racial override head
                RacialOverrideEffect racialOverride = GameManager.Instance.PlayerEffectManager.GetRacialOverrideEffect();
                if (racialOverride != null && racialOverride.GetCustomHeadImageData(playerEntity, out head))
                {
                    MapWindowPanel.UpdatePlayerHeadTexture(head.texture, markerScale * 0.5f);
                    return;
                }

                // Otherwise just get standard head based on gender and race
                switch (playerEntity.Gender)
                {
                    default:
                    case Genders.Male:
                        head = ImageReader.GetImageData(playerEntity.RaceTemplate.PaperDollHeadsMale, playerEntity.FaceIndex, 0, true);
                        break;
                    case Genders.Female:
                        head = ImageReader.GetImageData(playerEntity.RaceTemplate.PaperDollHeadsFemale, playerEntity.FaceIndex, 0, true);
                        break;
                }
                MapWindowPanel.UpdatePlayerHeadTexture(head.texture, markerScale * 0.5f);
            }
            else
            {
                if (textureMarker == null)
                    TextureReplacement.TryImportImage("marker", true, out textureMarker);
                MapWindowPanel.UpdatePlayerHeadTexture(textureMarker, markerScale);
            }

        }

        public static void OnExhausted(DaggerfallEntity entity)
        {
            if (Instance.onShip)
                return;

            if (Instance.showingMap != null)
            {
                Instance.StopShowingMap();

                //Instance.SetTimeScale(0);
                //Instance.SetTravelSpeed(0);
            }

            //chance to roll an exhaustion encounter?
            //"You wake up as an animal is about to devour you"
            //"You wake up to see someone rifling through your things"
        }

        public static void OnPreTransition(PlayerEnterExit.TransitionEventArgs args)
        {
            Instance.ClearSpawnedObjects();
        }

        void OnEnemySpawn(GameObject enemy)
        {
            //check if player is in exterior
            if (!GameManager.Instance.PlayerEnterExit.IsPlayerInside)
            {
                //check if enemy spawned in exterior swims (eg, Aquatic)
                MobileUnit mobile = enemy.GetComponentInChildren<MobileUnit>();
                if (mobile.Enemy.Behaviour == MobileBehaviour.Aquatic)
                {
                    StartCoroutine(MakeFishWalk(enemy));
                }
            }
        }

        IEnumerator MakeFishWalk(GameObject enemy)
        {
            yield return new WaitForSeconds(1);

            EnemyMotor motor = enemy.GetComponent<EnemyMotor>();
            FieldInfo swims = motor.GetType().GetField("swims", BindingFlags.NonPublic | BindingFlags.Instance);
            if (swims != null)
            {
                int count = 0;
                //wait until the enemy object is enabled (break if more than 600 frames has passed or gameobject is destroyed)
                while (enemy != null && !enemy.activeSelf && count < 600)
                {
                    count++;
                    yield return new WaitForEndOfFrame();
                }

                //enemy object was enabled before breakout and wasn't destroyed
                if (enemy != null && count < 600)
                {
                    //wait one frame to allow enemy scripts to initialize
                    yield return new WaitForEndOfFrame();

                    //set the bool
                    swims.SetValue(motor, false);
                }
            }
        }

        private void Update()
        {
            if (fade)
                fadeColor.a = Mathf.MoveTowards(fadeColor.a, 1, 2 * Time.unscaledDeltaTime);
            else
                fadeColor.a = Mathf.MoveTowards(fadeColor.a, 0, 2 * Time.unscaledDeltaTime);

            if (GameManager.IsGamePaused || InputManager.Instance.IsPaused || SaveLoadManager.Instance.LoadInProgress)
                return;

            if ((InputManager.Instance.GetKeyUp(keyboardMapKeyCode) || InputManager.Instance.GetKeyUp(gamepadMapKeyCode)) && showingMap == null)
            {
                if (GameManager.Instance.PlayerEnterExit.IsPlayerInside)
                    DaggerfallUI.MessageBox(TextManager.Instance.GetLocalizedText("cannotTravelIndoors"));
                else if (GameManager.Instance.AreEnemiesNearby())
                    DaggerfallUI.MessageBox(TextManager.Instance.GetLocalizedText("cannotTravelWithEnemiesNearby"));
                else
                    StartShowingMap();
            }

            if (objectsSpawned.Count > 0)
            {
                if (Vector2.Distance(mapPixelSpawned, new Vector2(GameManager.Instance.PlayerGPS.CurrentMapPixel.X, GameManager.Instance.PlayerGPS.CurrentMapPixel.Y)) > 1)
                    ClearSpawnedObjects();
            }

        }

        private void LateUpdate()
        {
            if (mapActive)
            {
                AmbientAudioObject.transform.position = new Vector3(CameraObject.transform.position.x, CameraBaseObject.transform.position.y, CameraObject.transform.position.z);

                /*if (!AmbientAudioSource.IsPlaying())
                {
                    //set a new clip and play
                    PlayAmbientSoundClip();
                }*/
        }
        }

        bool IsPlayerFullyHealed()
        {
            // Check if player fully healed
            // Will eventually need to tailor check for character
            // For example, sorcerers cannot recover magicka from resting
            if (playerEntity.CurrentHealth == playerEntity.MaxHealth &&
                playerEntity.CurrentFatigue == playerEntity.MaxFatigue &&
                (playerEntity.CurrentMagicka == playerEntity.MaxMagicka || playerEntity.Career.NoRegenSpellPoints))
            {
                return true;
            }

            return false;
        }

        public bool IsPlayerInLocation()
        {
            if (locationObjectsMap[lastPlayerMapPixel.x, lastPlayerMapPixel.y] != null)
                return true;

            return false;
        }

        public bool TryCamp()
        {
            if (onWater)
            {
                if (!onShip)
                {
                    MapWindowPanel.Instance.logLabel.AddText("You cannot camp while swimming!");
                    return false;
                }
                else
                {
                    //stuff
                    return true;
                }
            }
            else
            {
                //-1 = cannot camp or lodge, 0 = camp freely, 1+ = lodge fees
                int index = GetCampIndex();

                if (index > 0)
                {
                    if (GameManager.Instance.GuildManager.GetGuild(FactionFile.GuildGroups.KnightlyOrder).FreeTavernRooms())
                    {
                        MapWindowPanel.Instance.logLabel.AddText("Free lodging as befits a knight such as you");
                        return true;
                    }
                    else if (playerEntity.GoldPieces < 5 * index)
                    {
                        MapWindowPanel.Instance.logLabel.AddText("Not enough gold to pay for lodgings");
                        return false;
                    }
                }
                else if (index < 0)
                {
                    MapWindowPanel.Instance.logLabel.AddText("No lodgings available at this location");
                    return false;
                }
            }

            return true;
        }

        int GetCampIndex()
        {
            int index = 0;

            Vector2Int currentLodgingPosition = new Vector2Int(Mathf.RoundToInt(PlayerObject.transform.localPosition.x), -Mathf.RoundToInt(PlayerObject.transform.localPosition.z));

            if (currentLodgingPosition != lastLodgingPosition)
            {
                //lodged in a new place
                lodgingTimer = 25;
                lastLodgingPosition = currentLodgingPosition;
            }

            //check location
            ContentReader.MapSummary summary;
            if (dfUnity.ContentReader.HasLocation(currentLodgingPosition.x, currentLodgingPosition.y, out summary))
            {
                switch (summary.LocationType)
                {
                    case DFRegion.LocationTypes.HomeWealthy: //30GP, Tourist trap type shit
                        index = 6;     
                        break;
                    case DFRegion.LocationTypes.Tavern:      //15GP
                        index = 3;     
                        break;
                    case DFRegion.LocationTypes.TownCity:    //30GP
                        index = 6;    
                        break;
                    case DFRegion.LocationTypes.TownHamlet:  //20GP
                        index = 4;     
                        break;
                    case DFRegion.LocationTypes.TownVillage: //10GP
                        index = 2;     
                        break;
                    case DFRegion.LocationTypes.Coven:
                    case DFRegion.LocationTypes.HomeFarms:
                    case DFRegion.LocationTypes.HomePoor:
                    case DFRegion.LocationTypes.ReligionTemple:
                    case DFRegion.LocationTypes.ReligionCult:
                        index = -1;     //cannot camp
                        break;
                }
            }

            return index;
        }

        public bool IsWater(int X, int Y)
        {
            if (initialized)
            {
                return heightMap[X, Y] < WaterLevel;
                /*if (worldSmooth == 1)
                    return heightMapSmooth[X, Y] < WaterLevel;
                else
                    return heightMap[X, Y] < WaterLevel;*/
            }
            else
            {
                return GetWorldHeight(X, Y) < WaterLevel;
                /*if (worldSmooth == 1)
                    return GetAverageWorldHeight(X, Y) < WaterLevel;
                else
                    return GetWorldHeight(X, Y) < WaterLevel;*/
            }
        }

        public bool IsOpenWater(int X, int Y)
        {
            return IsWater(X, Y) && !IsCoast(X, Y);
        }

        bool IsBeach(int X, int Y)
        {
            if (coast)
                return false;

            if (IsWater(X, Y))
                return false;

            bool value = false;
            int cellX = 0;
            int cellY = 0;

            for (int x = -1; x < 2; x++)
            {
                cellX = X + x;

                if (cellX < 0 || cellX > 1000)
                    continue;

                for (int y = -1; y < 2; y++)
                {
                    cellY = Y + y;

                    if (cellY < 0 || cellY > 500)
                        continue;

                    if (IsWater(cellX, cellY))
                    {
                        value = true;
                        break;
                    }
                }
            }

            return value;
        }

        bool IsCoast(int X, int Y)
        {
            if (!coast)
                return false;

            if (!IsWater(X, Y))
                return false;

            bool value = false;
            int cellX = 0;
            int cellY = 0;

            //int count = 0;
            for (int x = -1; x < 2; x++)
            {
                cellX = X + x;

                if (cellX < 0 || cellX > 1000)
                    continue;

                for (int y = -1; y < 2; y++)
                {
                    cellY = Y + y;

                    if (cellY < 0 || cellY > 500)
                        continue;

                    if (!IsWater(cellX, cellY))
                    {
                        value = true;
                        break;
                        //count++;
                    }
                }
            }

            /*if (count > 0 && count < 5)
                value = true;*/

            return value;
        }

        bool CheckWater(int range = 5)
        {
            for (int x = -range; x <= range; x++)
            {
                for (int y = -range; y <= range; y++)
                {
                    if (IsOpenWater(lastPlayerMapPixel.x + x, lastPlayerMapPixel.y + y))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        Vector2 GetClosestWater(Vector2 startPos, int range = 5)
        {
            Vector2 currentPosition = startPos;
            Vector2 closestPosition = Vector2.zero;
            float currentDistance = 0;
            float closestDistance = Mathf.Infinity;
            for (int x = -range; x <= range; x++)
            {
                for (int y = -range; y <= range; y++)
                {
                    currentPosition = new Vector2(startPos.x + x, startPos.y + y);
                    if (IsOpenWater(Mathf.RoundToInt(currentPosition.x), Mathf.RoundToInt(currentPosition.y)))
                    {
                        currentDistance = Vector2.Distance(startPos, currentPosition);
                        if (currentDistance < closestDistance)
                        {
                            closestPosition = currentPosition;
                            closestDistance = currentDistance;
                        }

                    }
                }
            }

            return closestPosition;
        }

        bool CheckGround(int range = 5)
        {
            for (int x = -range; x <= range; x++)
            {
                for (int y = -range; y <= range; y++)
                {
                    if (!IsOpenWater(lastPlayerMapPixel.x + x, lastPlayerMapPixel.y + y))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        Vector2 GetClosestGround(Vector2 startPos, int range = 5)
        {
            Vector2 currentPosition = startPos;
            Vector2 closestPosition = Vector2.zero;
            float currentDistance = 0;
            float closestDistance = Mathf.Infinity;
            for (int x = -range; x <= range; x++)
            {
                for (int y = -range; y <= range; y++)
                {
                    currentPosition = new Vector2(startPos.x + x, startPos.y + y);
                    if (!IsOpenWater(Mathf.RoundToInt(currentPosition.x), Mathf.RoundToInt(currentPosition.y)))
                        {
                        currentDistance = Vector2.Distance(startPos, currentPosition);
                        if (currentDistance < closestDistance)
                        {
                            closestPosition = currentPosition;
                            closestDistance = currentDistance;
                        }

                    }
                }
            }

            return closestPosition;
        }

        bool CheckPort(int range = 5)
        {
            ContentReader.MapSummary summary;
            for (int x = -range; x <= range; x++)
            {
                for (int y = -range; y <= range; y++)
                {
                    if (dfUnity.ContentReader.HasLocation(lastPlayerMapPixel.x + x, lastPlayerMapPixel.y + y, out summary))
                    {
                        if (HasPort(summary.MapID))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        GameObject GetClosestPort(Vector3 startPos)
        {
            GameObject closestObject = null;
            float currentDistance = 0;
            float closestDistance = Mathf.Infinity;
            foreach (GameObject PortLocationObject in LocationPortObjects)
            {
                currentDistance = (startPos - PortLocationObject.transform.position).sqrMagnitude;
                if (currentDistance < closestDistance)
                {
                    closestObject = PortLocationObject;
                    closestDistance = currentDistance;
                }
            }
            return closestObject;
        }


        public bool TryShip()
        {
            if (!onShip)
            {
                //check for nearby water
                bool hasWater = CheckWater(5);

                if (!hasWater)
                {
                    MapWindowPanel.Instance.logLabel.AddText("No water nearby");
                    return false;
                }

                //check for a nearby port
                bool hasPort = CheckPort(5);

                if (!hasPort)
                {
                    MapWindowPanel.Instance.logLabel.AddText("No port nearby");
                    return false;
                }

                //if player does not own a ship, they must pay
                if (!DaggerfallBankManager.OwnsShip && playerEntity.GoldPieces < 25)
                {
                    MapWindowPanel.Instance.logLabel.AddText("Not enough gold to pay for passage");
                    return false;
                }

                //board a ship
                BoardShip();
                return true;
            }
            else
            {
                //check for nearby ground
                bool hasGround = CheckGround(5);

                if (!hasGround)
                {
                    MapWindowPanel.Instance.logLabel.AddText("No ground nearby");
                    return false;
                }

                //disembark ship
                DisembarkShip();
                return false;
            }

            return false;
        }

        void CancelShip()
        {
            onShip = false;

            //teleport player to nearest port city
            //pass random amount of time

            GameObject closestObject = GetClosestPort(PlayerObject.transform.position);

            if (closestObject != null)
            {
                SetTimeScale(0);
                SetPlayerPosition((int)closestObject.transform.localPosition.x, -(int)closestObject.transform.localPosition.z);
                SetCameraTargetPlayer();
                ResetTargetPosition();
            }
        }

        void BoardShip(bool stealth = false)
        {
            onShip = true;

            MapWindowPanel.Instance.logLabel.AddText("Boarded a ship");

            //teleport to nearest water tile
            Vector2 closestPosition = GetClosestWater(new Vector2(PlayerObject.transform.position.x, -PlayerObject.transform.position.z));

            if (closestPosition != Vector2.zero)
            {
                shipTimer = 1441;
                SetPlayerPosition((int)closestPosition.x, (int)closestPosition.y);
                if (!stealth)
                {
                    SetTimeScale(0);
                    SetCameraTargetPlayer();
                    ResetTargetPosition();
                }
            }

            //update travel variables
            lastPlayerMapPixel = new Vector2Int(Mathf.RoundToInt(PlayerObject.transform.localPosition.x), Mathf.RoundToInt(-PlayerObject.transform.localPosition.z));
            playerStealth = CalculateStealth();
            sightRadius = CalculateSightRange();

            UpdateTravelVariables();
            UpdateLocations();
        }

        void DisembarkShip(bool stealth = false)
        {
            onShip = false;

            MapWindowPanel.Instance.logLabel.AddText("Disembarked from a ship");

            //teleport to nearest ground tile
            //Vector2 closestPosition = GetClosestGround(lastPlayerMapPixel);
            Vector2 closestPosition = GetClosestGround(new Vector2(PlayerObject.transform.position.x, -PlayerObject.transform.position.z));

            if (closestPosition != Vector2.zero)
            {
                SetPlayerPosition((int)closestPosition.x, (int)closestPosition.y);
                if (!stealth)
                {
                    SetTimeScale(0);
                    SetCameraTargetPlayer();
                    ResetTargetPosition();
                }
            }

            //update travel variables
            lastPlayerMapPixel = new Vector2Int(Mathf.RoundToInt(PlayerObject.transform.localPosition.x), Mathf.RoundToInt(-PlayerObject.transform.localPosition.z));
            playerStealth = CalculateStealth();
            sightRadius = CalculateSightRange();

            UpdateTravelVariables();
            UpdateLocations();
        }

        public void StartShowingMap()
        {
            if (showingMap != null)
                return;

            showingMap = ShowMapCoroutine();
            StartCoroutine(showingMap);
        }

        public void StopShowingMap()
        {
            mapActive = false;
        }

        IEnumerator StopShowingMapCoroutine()
        {
            yield return new WaitForEndOfFrame();

            StopShowingMap();
        }

        void UpdateTravelVariables()
        {
            lastMoveDirection = PlayerObject.transform.localPosition - lastPlayerPosition;
            lastMoveDirection.y = 0;
            lastPlayerPosition = PlayerObject.transform.localPosition;

            if (BasicRoads != null)
                onRoad = Compatibility.BasicRoadsUtils.GetRoadType(lastPlayerMapPixel.x, lastPlayerMapPixel.y);
            else
                onRoad = 0;

            if (!onShip)
                onWater = onRoad > 0 ? false : IsOpenWater(lastPlayerMapPixel.x, lastPlayerMapPixel.y);
            else
                onWater = IsOpenWater(lastPlayerMapPixel.x, lastPlayerMapPixel.y);

            currentClimate = dfUnity.ContentReader.MapFileReader.GetClimateIndex(lastPlayerMapPixel.x, lastPlayerMapPixel.y);

            UpdatePlayerSoundClip();
            UpdateAmbientSoundClip();
            UpdatePlayerBillboard();
        }

        void UpdatePlayerModel()
        {
            //adjust player model
            if (onShip)
            {
                //on ship
                if (PlayerBillboardObject.activeSelf)
                    PlayerBillboardObject.SetActive(false);

                if (PlayerObjectMeshCart.activeSelf)
                    PlayerObjectMeshCart.SetActive(false);

                if (!PlayerObjectMeshBoat.activeSelf)
                    PlayerObjectMeshBoat.SetActive(true);

                if (PlayerObjectMeshCamp.activeSelf)
                    PlayerObjectMeshCamp.SetActive(false);
            }
            else if (travelSpeed == 0)
            {
                //camping
                if (PlayerBillboardObject.activeSelf)
                    PlayerBillboardObject.SetActive(false);

                if (PlayerObjectMeshCart.activeSelf)
                    PlayerObjectMeshCart.SetActive(false);

                if (!PlayerObjectMeshCamp.activeSelf)
                    PlayerObjectMeshCamp.SetActive(true);

                if (PlayerObjectMeshBoat.activeSelf)
                    PlayerObjectMeshBoat.SetActive(false);
            }
            else
            {
                //normal
                if (!PlayerBillboardObject.activeSelf)
                    PlayerBillboardObject.SetActive(true);

                if (GameManager.Instance.TransportManager.HasCart())
                {
                    if (!PlayerObjectMeshCart.activeSelf)
                        PlayerObjectMeshCart.SetActive(true);
                }
                else
                {
                    if (PlayerObjectMeshCart.activeSelf)
                        PlayerObjectMeshCart.SetActive(false);
                }

                if (PlayerObjectMeshBoat.activeSelf)
                    PlayerObjectMeshBoat.SetActive(false);

                if (PlayerObjectMeshCamp.activeSelf)
                    PlayerObjectMeshCamp.SetActive(false);
            }

            //adjust player light
            if (dfUnity.WorldTime.Now.Hour <= DaggerfallDateTime.DawnHour + 1 || dfUnity.WorldTime.Now.Hour >= DaggerfallDateTime.DuskHour - 1)
            {
                if (!PlayerLightObject.activeSelf)
                    PlayerLightObject.SetActive(true);
            }
            else
            {
                if (PlayerLightObject.activeSelf)
                    PlayerLightObject.SetActive(false);
            }
        }

        bool HoverCursorHandler()
        {
            if (MapWindowPanel.MouseOverGUI(0))
                return false;

            Ray ray = Camera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit = new RaycastHit();
            if (Physics.Raycast(ray, out hit, 1000, Physics.DefaultRaycastLayers))
            {
                Vector2Int currentCursorPixel = new Vector2Int(Mathf.RoundToInt(hit.point.x), Mathf.RoundToInt(-hit.point.z));
                if (currentCursorPixel == lastCursorPixel)
                    return false;

                lastCursorPixel = currentCursorPixel;

                /*if (IsWater(lastCursorPixel.x,lastCursorPixel.y))
                {
                    if (IsCoast(lastCursorPixel.x, lastCursorPixel.y))
                        tooltipString = "Coast";
                    else
                        tooltipString = "Ocean";
                }
                else
                {
                    if (IsBeach(lastCursorPixel.x, lastCursorPixel.y))
                        tooltipString = "Beach";
                    else
                        tooltipString = "Land";
                }
                return true;*/

                ContentReader.MapSummary summary;
                if (dfUnity.ContentReader.HasLocation(currentCursorPixel.x, currentCursorPixel.y, out summary))
                {
                    if (summary.DungeonType == DFRegion.DungeonTypes.NoDungeon || GameManager.Instance.PlayerGPS.HasDiscoveredLocation(summary.ID))
                    {
                        DFLocation location;
                        if (DaggerfallUnity.Instance.ContentReader.GetLocation(summary.RegionIndex, summary.MapIndex, out location))
                        {
                            tooltipString = location.Name;
                            return true;
                        }
                    }
                }

                if (currentEncounters.Count > 0)
                {
                    foreach (Encounter encounter in currentEncounters)
                    {
                        if (encounter.template.isMobile)
                            continue;

                        if (encounter.position == lastCursorPixel)
                        {
                            tooltipString = "???";
                            return true;
                        }
                    }
                }

                tooltipString = string.Empty;
            }

            return false;
        }

        bool HoverCrosshairHandler()
        {
            Vector2Int currentCursorPixel = new Vector2Int(Mathf.RoundToInt(CameraBaseObject.transform.localPosition.x), Mathf.RoundToInt(-CameraBaseObject.transform.localPosition.z));

            if (locationObjectsMap[currentCursorPixel.x, currentCursorPixel.y] != null)
            {
                if (currentCursorPixel == lastCursorPixel)
                    return false;

                lastCursorPixel = currentCursorPixel;

                ContentReader.MapSummary summary;
                if (dfUnity.ContentReader.HasLocation(currentCursorPixel.x, currentCursorPixel.y, out summary))
                {
                    if (summary.DungeonType == DFRegion.DungeonTypes.NoDungeon || GameManager.Instance.PlayerGPS.HasDiscoveredLocation(summary.ID))
                    {
                        DFLocation location;
                        if (DaggerfallUnity.Instance.ContentReader.GetLocation(summary.RegionIndex, summary.MapIndex, out location))
                        {
                            tooltipString = location.Name;
                            return true;
                        }
                    }
                }
            }

            lastCursorPixel = Vector2Int.zero;
            tooltipString = string.Empty;

            return false;
        }
        public void SetTravelSpeed(int index = 0)
        {
            travelSpeed = index;
            if (timeScaleIndex == 0)
                timeScaleIndex = defaultTimeScaleCamp;
        }

        public void SetTimeScale(int index = 0)
        {
            if (timeScaleIndex == 0)
                automatic = false;
            timeScaleIndex = index;
        }

        public void EnemySighted()
        {
            MapWindowPanel.Instance.logLabel.AddText("Enemy sighted");
            if (pauseWhenEnemySighted)
            {
                timeScaleIndex = 0;
            }
        }

        void UpdatePlayerBillboard()
        {
            PlayerBillboard.isRiding = GameManager.Instance.TransportManager.HasHorse() || GameManager.Instance.TransportManager.HasCart() ? true : false;
            PlayerBillboard.isSwimming = onWater;
        }

        IEnumerator ShowMapCoroutine()
        {
            DaggerfallUI.Instance.DaggerfallHUD.Enabled = false;
            GameManager.Instance.PauseGame(true,true);
            playerEntity.IsResting = true;

            minutesStart = dfUnity.WorldTime.Now.Minute;
            minutesSince = 0;
            minuteTimer = 0;
            hourTimer = 0;
            timeScaleIndex = 0;
            automatic = false;
            locked = false;
            /*currentEncounterQuestName = string.Empty;
            currentEncounterModMessage = string.Empty;
            currentEncounterIterations = 1;*/
            currentEncounterTemplate = null;

            fade = true;
            //DaggerfallUI.Instance.FadeBehaviour.FadeHUDToBlack();

            yield return new WaitForSecondsRealtime(0.5f);

            mapActive = true;

            EnableMap();

            SetPlayerPosition(GameManager.Instance.PlayerGPS.CurrentMapPixel.X, GameManager.Instance.PlayerGPS.CurrentMapPixel.Y);

            basePosition = CameraBaseObject.transform.localPosition;
            turnAngle = CameraBaseObject.transform.localEulerAngles.y;
            pitchAngle = CameraPivotObject.transform.localEulerAngles.x;
            PlayerFogProjectorObject.transform.position = PlayerObject.transform.position;
            zoomLevel = -10;

            lastPlayerMapPixel = new Vector2Int(Mathf.RoundToInt(PlayerObject.transform.localPosition.x), Mathf.RoundToInt(-PlayerObject.transform.localPosition.z));

            float restTimer = 0;
            bool fatigueWarning = false;

            SetLightAngle(dfUnity.WorldTime.Now.MinuteOfDay);

            //update player billboard
            UpdatePlayerBillboard();

            sightRadius = CalculateSightRange();
            playerStealth = CalculateStealth();
            PlayerFogProjector.orthographicSize = sightRadius * 1.5f;
            UpdateTravelVariables();
            UpdateLocations();
            UpdatePlayerModel();
            UpdatePlayerHeadTexture();

            Rect modRect = GameManager.Instance.MainCamera.rect;
            if (DaggerfallUnity.Settings.RetroRenderingMode > 0)
                modRect = GameManager.Instance.RetroPresenter.GetComponent<Camera>().rect;

            //in case large hud is enabled
            modRect.y = 0;
            modRect.height = 1;

            Camera.rect = modRect;

            RenderSettings.ambientLight = Color.Lerp(Color.black, Color.white, ambientLightMod/1);

            if (WarmAshesWilderness != null)
            {
                ModManager.Instance.SendModMessage(WarmAshesWilderness.Title, "suppressDungeonExteriorEncounter");
                ModManager.Instance.SendModMessage(WarmAshesWilderness.Title, "suppressWildernessEncounter");
                ModManager.Instance.SendModMessage(WarmAshesWilderness.Title, "suppressOceanShips");
            }

            yield return new WaitForSecondsRealtime(0.5f);

            fade = false;
            //DaggerfallUI.Instance.FadeBehaviour.FadeHUDFromBlack();

            //setup player movement sounds

            MapWindowPanel.Enabled = true;


            UpdatePlayerSoundClip();
            UpdateAmbientSoundClip();

            while (mapActive)
                {
                if (!GameManager.IsGamePaused)
                    GameManager.Instance.PauseGame(true, true);

                playerEntity.PreventEnemySpawns = true;

                MapWindowPanel.Update();

                UpdatePlayerModel();

                if (Vector3.Distance(CameraObject.transform.position, PlayerObject.transform.position) > markerDistance)
                    MapWindowPanel.showPlayerMarker = true;
                else
                    MapWindowPanel.showPlayerMarker = false;

                //weather
                WeatherType weather = GameManager.Instance.WeatherManager.PlayerWeather.WeatherType;
                if (weather == WeatherType.Rain || weather == WeatherType.Thunder)
                {
                    if (!RainObject.activeSelf)
                        RainObject.SetActive(true);
                    if (SnowObject.activeSelf)
                        SnowObject.SetActive(false);
                }
                else if (weather == WeatherType.Snow)
                {
                    if (RainObject.activeSelf)
                        RainObject.SetActive(false);
                    if (!SnowObject.activeSelf)
                        SnowObject.SetActive(true);
                }
                else
                {
                    if (RainObject.activeSelf)
                        RainObject.SetActive(false);
                    if (SnowObject.activeSelf)
                        SnowObject.SetActive(false);
                }

                if (locked)
                {
                    CameraBaseObject.transform.position = Vector3.MoveTowards(CameraBaseObject.transform.position, PlayerObject.transform.position, 2 * Time.unscaledDeltaTime * TimeScale);
                    basePosition = CameraBaseObject.transform.localPosition;
                }
                else
                {
                    CameraBaseObject.transform.localPosition = Vector3.Lerp(CameraBaseObject.transform.localPosition, basePosition, 10 * Time.unscaledDeltaTime);
                    SetCameraHeight();
                }

                if (PinPosition.sqrMagnitude > 0)
                {
                    //pin is active
                    //show pin
                    if (!PinObject.activeSelf)
                        PinObject.SetActive(true);

                    //place pin at pinned position
                    PinObject.transform.localPosition = PinPosition;
                }
                else
                {
                    //pin is inactove
                    if (PinObject.activeSelf)
                        PinObject.SetActive(false);
                }

                if (!AmbientAudioSource.IsPlaying())
                    AmbientAudioSource.AudioSource.Play();

                //clock is ticking
                if (timeScaleIndex > 0)
                {
                    //update sun angle
                    //SetLightAngle(currentTime.MinuteOfDay);
                    SetLightAngle(dfUnity.WorldTime.Now.MinuteOfDay);

                    //check if someone triggered an encounter
                    if (runEncounter)
                    {
                        timeScaleIndex = 0;

                        DaggerfallUI.MessageBox(currentEncounterTemplate.message);

                        while (DaggerfallUI.UIManager.TopWindow is DaggerfallMessageBox)
                        {
                            //wait  until the message is dismissed
                            yield return new WaitForEndOfFrame();
                        }

                        StopShowingMap();
                        break;
                    }

                    float currentSightRadius = CalculateSightRange();
                    Vector2Int currentPlayerMapPixel = new Vector2Int(Mathf.RoundToInt(PlayerObject.transform.localPosition.x), Mathf.RoundToInt(-PlayerObject.transform.localPosition.z));
                    if (lastPlayerMapPixel != currentPlayerMapPixel || sightRadius != currentSightRadius)
                    {
                        playerStealth = CalculateStealth();
                        if (lastPlayerMapPixel != currentPlayerMapPixel)
                        {
                            //player has changed map pixel
                            lastPlayerMapPixel = currentPlayerMapPixel;
                        }

                        if (sightRadius != currentSightRadius)
                        {
                            //sight radius has changed
                            sightRadius = currentSightRadius;
                            PlayerFogProjector.orthographicSize = sightRadius;
                        }

                        UpdateTravelVariables();
                        UpdateLocations();
                    }

                    bool stopped = false;
                    if (travelSpeed > 0)
                    {
                        if (Vector3.Distance(PlayerObject.transform.position, TargetObject.transform.position) > 0.01f)
                        {
                            if (!travelling)
                            {
                                //started travelling
                                travelling = true;
                            }

                            float speed = CalculateTravelSpeed();
                            PlayerObject.transform.position = Vector3.MoveTowards(PlayerObject.transform.position, TargetObject.transform.position, speed * Time.unscaledDeltaTime * TimeScale);
                            Vector3 look = new Vector3(TargetObject.transform.position.x - PlayerObject.transform.position.x, 0, TargetObject.transform.position.z - PlayerObject.transform.position.z).normalized;
                            PlayerObject.transform.forward = Vector3.Lerp(PlayerObject.transform.forward, look, 1 * Time.unscaledDeltaTime * TimeScale);
                            PlayerFogProjectorObject.transform.position = PlayerObject.transform.position;
                            SetPlayerHeight();
                            if (!PlayerAudioSource.IsPlaying())
                                PlayerAudioSource.AudioSource.Play();
                        }
                        else
                        {
                            if (travelling)
                            {
                                //stopped travelling by reaching/canceling destination
                                travelling = false;
                                stopped = true;
                            }

                            if (automatic)
                                timeScaleIndex = 0;

                            if (travelSpeed == 2)
                                travelSpeed = 1;

                            if (PlayerAudioSource.IsPlaying())
                                PlayerAudioSource.AudioSource.Stop();
                        }
                    }
                    else
                    {
                        if (travelling)
                        {
                            //stopped travelling by choosing to camp
                            travelling = false;
                            stopped = true;
                        }

                        if (restTimer == 0)
                            UpdatePlayerSoundClip();

                        if (!PlayerAudioSource.IsPlaying())
                                PlayerAudioSource.AudioSource.Play();
                    }

                    if (stopped)
                    {
                        //player ended travelling somehow
                        //update travel variables
                        playerStealth = CalculateStealth();
                        if (lastPlayerMapPixel != currentPlayerMapPixel)
                        {
                            //player has changed map pixel
                            lastPlayerMapPixel = currentPlayerMapPixel;
                        }

                        if (sightRadius != currentSightRadius)
                        {
                            //sight radius has changed
                            sightRadius = currentSightRadius;
                            PlayerFogProjector.orthographicSize = sightRadius;
                        }

                        UpdateTravelVariables();
                        UpdateLocations();

                        //check destination for encounter
                        if (locationObjectsMap[currentPlayerMapPixel.x, currentPlayerMapPixel.y] == null && currentEncounters.Count > 0)
                        {
                            for (int i = 0; i < currentEncounters.Count; i++)
                            {
                                if (Vector3.Distance(currentEncounters[i].EncounterObject.transform.position, PlayerObject.transform.position) < 1f)
                                {
                                    currentEncounterTemplate = currentEncounters[i].template;
                                    currentEncounters[i].Destruct();
                                    currentEncounters.RemoveAt(i);
                                    runEncounter = true;
                                    break;
                                }
                            }

                            if (runEncounter)
                            {
                                timeScaleIndex = 0;

                                DaggerfallUI.MessageBox(currentEncounterTemplate.message);

                                while (DaggerfallUI.UIManager.TopWindow is DaggerfallMessageBox)
                                {
                                    //wait  until the message is dismissed
                                    yield return new WaitForEndOfFrame();
                                }

                                StopShowingMap();
                                break;
                            }
                        }

                        //chance to run encounter when entering a location
                        if (encounterChanceLocation > 0 && locationObjectsMap[currentPlayerMapPixel.x, currentPlayerMapPixel.y] != null && !onWater && Dice100.SuccessRoll(encounterChanceLocation))
                        {
                            runEncounter = true;
                            timeScaleIndex = 0;

                            DaggerfallUI.MessageBox("As you enter, something happens...");

                            while (DaggerfallUI.UIManager.TopWindow is DaggerfallMessageBox)
                            {
                                //wait  until the message is dismissed
                                yield return new WaitForEndOfFrame();
                            }

                            StopShowingMap();
                            break;
                        }
                    }

                    if (travelling || Vector3.Distance(PlayerObject.transform.position, TargetObject.transform.position) > 0.01f)
                    {
                        if (!TargetObject.activeSelf)
                            TargetObject.SetActive(true);
                    }
                    else
                    {
                        if (TargetObject.activeSelf)
                            TargetObject.SetActive(false);
                    }

                    PlayerBillboard.isMoving = travelling;

                    //increment time here in minutes?
                    int minutes = Mathf.FloorToInt(minuteTimer / minuteTime);
                    if (minutes > 0)
                    {
                        //A minute(s) has passed, do a thing
                        //currentTime.RaiseTime(60);
                        dfUnity.WorldTime.Now.RaiseTime(60 * minutes);

                        if (dfUnity.WorldTime.Now.IsNight != wasNight)
                        {
                            if (minutesSince > 0)
                            {
                                //specific transition event
                                if (dfUnity.WorldTime.Now.IsNight)
                                {
                                    //dusk
                                    MapWindowPanel.Instance.logLabel.AddText("Dusk");
                                    if (pauseAtDusk)
                                    {
                                        timeScaleIndex = 0;
                                    }

                                }
                                else
                                {
                                    //dawn
                                    MapWindowPanel.Instance.logLabel.AddText("Dawn");
                                    if (pauseAtDawn)
                                    {
                                        timeScaleIndex = 0;
                                    }
                                }

                                //general transition event
                                UpdateEncounters();
                            }
                            wasNight = dfUnity.WorldTime.Now.IsNight;
                        }

                        bool isMidday = dfUnity.WorldTime.Now.Hour >= DaggerfallDateTime.MiddayHour;
                        if (isMidday != wasMidday)
                        {
                            if (minutesSince > 0)
                            {
                                //specific transition event
                                if (isMidday)
                                {
                                    //midday
                                    MapWindowPanel.Instance.logLabel.AddText("Midday");

                                }
                                else
                                {
                                    //midnight
                                    MapWindowPanel.Instance.logLabel.AddText("Midnight");
                                }

                                //general transition event
                                UpdateEncounters();
                            }
                            wasMidday = isMidday;
                        }

                        minutesSince += (int)minutes;

                        int hours = Mathf.FloorToInt(hourTimer / 60);
                        if (hours > 0)
                        {
                            //an hour(s) has passed do a thing

                            //check whether or not to change map textures
                            if (wasWinter != (dfUnity.WorldTime.Now.SeasonValue == DaggerfallDateTime.Seasons.Winter))
                                SetTextures();

                            //run encounter if not in location nor on water?
                            if (encounterChanceWilderness > 0 && WarmAshesWilderness != null && locationObjectsMap[Mathf.RoundToInt(PlayerObject.transform.localPosition.x), Mathf.RoundToInt(-PlayerObject.transform.localPosition.z)] == null && !onWater && Dice100.SuccessRoll(encounterChanceWilderness * hours))
                            {
                                runEncounter = true;
                                timeScaleIndex = 0;

                                DaggerfallUI.MessageBox("Your journey is interrupted...");

                                while (DaggerfallUI.UIManager.TopWindow is DaggerfallMessageBox)
                                {
                                    //wait  until the message is dismissed
                                    yield return new WaitForEndOfFrame();
                                }

                                StopShowingMap();
                            }

                            hourTimer -= hours * 60;
                        }
                        else
                            hourTimer += (int)minutes;

                        //ship payment if not owning a ship
                        if (onShip)
                        {
                            if (!DaggerfallBankManager.OwnsShip || !GameManager.Instance.GuildManager.GetGuild(FactionFile.GuildGroups.KnightlyOrder).FreeShipTravel())
                            {
                                if (shipTimer >= 1440)
                                {
                                    //pay for passage
                                    if (playerEntity.GoldPieces >= 100)
                                    {
                                        playerEntity.DeductGoldAmount(100);
                                        shipTimer = 0;
                                        MapWindowPanel.Instance.logLabel.AddText("Paid 100 gold for passage");
                                    }
                                    else
                                    {
                                        //disembark
                                        MapWindowPanel.Instance.logLabel.AddText("Not enough gold to pay for passage");
                                        //get dumped into a random port after a random amount of time
                                        CancelShip();
                                    }
                                }
                                else
                                    shipTimer += minutes;
                            }

                            if (!onWater)
                            {
                                DisembarkShip(true);
                            }
                        }

                        if (travelling)
                        {
                            //player moving, therefore decrease fatigue
                            float fatigueMultiplier = CalculateFatigueDrain();
                            if (travelSpeed == 2 && !onShip)
                                playerEntity.DecreaseFatigue(Mathf.RoundToInt(PlayerEntity.RunningFatigueLoss * fatigueMultiplier * minutes));
                            else
                                playerEntity.DecreaseFatigue(Mathf.RoundToInt(PlayerEntity.DefaultFatigueLoss * fatigueMultiplier * minutes));

                            restTimer = 0;
                        }
                        else
                        {
                            //player not moving
                            if (travelSpeed == 0)
                            {
                                //player is choosing to rest
                                int restHours = Mathf.FloorToInt(restTimer / 60f);
                                if (restHours > 0)
                                {
                                    //do lodging logic here
                                    int campIndex = GetCampIndex();
                                    if (campIndex > 0)
                                    {
                                        //paid lodging
                                        if (!GameManager.Instance.GuildManager.GetGuild(FactionFile.GuildGroups.KnightlyOrder).FreeTavernRooms())
                                        {
                                            int roomCost = campIndex * 5;
                                            if (lodgingTimer >= 24)
                                            {
                                                //pay the price
                                                if (playerEntity.GoldPieces >= roomCost)
                                                {
                                                    playerEntity.DeductGoldAmount(roomCost);
                                                    lodgingTimer = 0;
                                                    MapWindowPanel.Instance.logLabel.AddText("Paid " + roomCost.ToString() + " gold for lodging");
                                                }
                                                else
                                                {
                                                    //stop camping
                                                    travelSpeed = 1;
                                                    timeScaleIndex = 0;
                                                    MapWindowPanel.Instance.logLabel.AddText("Not enough gold to pay for lodging");
                                                }
                                            }
                                            else
                                            {
                                                lodgingTimer += restHours;
                                            }
                                        }
                                    }

                                    //every 60 minutes, tick a heal
                                    if (!IsPlayerFullyHealed())
                                    {
                                        //regen magnitude is halved if resting outside of settlements
                                        float locationMod = campIndex == 0 ? 0.5f : 1;

                                        int healthRecoveryRate = Mathf.RoundToInt(FormulaHelper.CalculateHealthRecoveryRate(playerEntity) * locationMod);
                                        int fatigueRecoveryRate = Mathf.RoundToInt(FormulaHelper.CalculateFatigueRecoveryRate(playerEntity.MaxFatigue) * locationMod);
                                        int spellPointRecoveryRate = Mathf.RoundToInt(FormulaHelper.CalculateSpellPointRecoveryRate(playerEntity) * locationMod);

                                        playerEntity.CurrentHealth += healthRecoveryRate * restHours;
                                        playerEntity.CurrentFatigue += fatigueRecoveryRate * restHours;
                                        playerEntity.CurrentMagicka += spellPointRecoveryRate * restHours;

                                        playerEntity.TallySkill((short)Skills.Medical, (short)restHours);

                                        if (IsPlayerFullyHealed() && automatic && pauseWhenRested)
                                        {
                                            MapWindowPanel.Instance.logLabel.AddText("Fully rested!");
                                            timeScaleIndex = 0;
                                        }
                                    }
                                    restTimer = (restTimer / 60f) - (float)restHours;
                                }
                                else
                                    restTimer += minutes;
                            }
                            else
                            {
                                float fatigueMultiplier = CalculateFatigueDrain();
                                playerEntity.DecreaseFatigue(Mathf.RoundToInt(PlayerEntity.DefaultFatigueLoss * fatigueMultiplier));
                            }
                        }

                        //pause when at low Fatigue?
                        if ((float)playerEntity.CurrentFatigue / (float)playerEntity.MaxFatigue < fatigueWarningThreshold)
                        {
                            //post warning once
                            if (!fatigueWarning)
                            {
                                MapWindowPanel.Instance.logLabel.AddText("Fatigue low!");
                                fatigueWarning = true;
                                timeScaleIndex = 0;
                            }
                        }
                        else
                        {
                            //reset warning if Fatigue goes above threshold
                            fatigueWarning = false;
                        }

                        //set it to the remainder
                        minuteTimer = (minuteTimer / minuteTime) - minutes;
                    }
                    else
                        minuteTimer += Time.unscaledDeltaTime * (int)TimeScale;
                }
                else
                {
                    if (PlayerAudioSource.IsPlaying())
                        PlayerAudioSource.AudioSource.Stop();
                }

                CameraBaseObject.transform.rotation = Quaternion.Lerp(CameraBaseObject.transform.rotation, Quaternion.AngleAxis(turnAngle, CameraBaseObject.transform.up), 10 * Time.unscaledDeltaTime);
                CameraPivotObject.transform.localRotation = Quaternion.Lerp(CameraPivotObject.transform.localRotation, Quaternion.AngleAxis(pitchAngle, Vector3.right), 10 * Time.unscaledDeltaTime);
                CameraObject.transform.localPosition = Vector3.Lerp(CameraObject.transform.localPosition, new Vector3(0, 0, zoomLevel), 10 * Time.unscaledDeltaTime);

                if (DaggerfallUI.UIManager.TopWindow is DaggerfallInputMessageBox || DaggerfallUI.UIManager.TopWindow is DaggerfallMessageBox || DaggerfallUI.UIManager.TopWindow is DaggerfallListPickerWindow || DaggerfallUI.UIManager.TopWindow is MapWindowPanel.DaggerfallLocationsListPickerWindow || inputDelayTimer < 1)
                {
                    if (inputDelayTimer < 1)
                        inputDelayTimer += Time.unscaledDeltaTime;

                    yield return new WaitForEndOfFrame();
                    continue;
                }

                //keyboard controls
                if (InputManager.Instance.GetKeyDown(KeyCode.Mouse1) || InputManager.Instance.GetKeyDown(KeyCode.Mouse2))
                    lastMousePosition = Input.mousePosition;

                //get input delta
                Vector2 stickLeftDelta = new Vector2(Input.GetAxis(InputManager.Instance.GetAxisBinding(InputManager.AxisActions.MovementHorizontal)), Input.GetAxis(InputManager.Instance.GetAxisBinding(InputManager.AxisActions.MovementVertical)));
                Vector2 stickRightDelta = new Vector2(Input.GetAxis(InputManager.Instance.GetAxisBinding(InputManager.AxisActions.CameraHorizontal)), Input.GetAxis(InputManager.Instance.GetAxisBinding(InputManager.AxisActions.CameraVertical)));
                Vector3 mouseDelta = Input.mousePosition - lastMousePosition;

                if ((DaggerfallUnity.Settings.EnableController && (stickLeftDelta.sqrMagnitude > 0 || stickRightDelta.sqrMagnitude > 0)))
                    lastInputDeviceIsGamepad = 2;
                else if (showNavigationPanel && (GUIMoveHorizontal != 0 || GUIMoveVertical != 0 || GUIOrbit != 0 || GUIZoom != 0))
                    lastInputDeviceIsGamepad = 1;
                else if (mouseDelta.sqrMagnitude > 0)
                    lastInputDeviceIsGamepad = 0;

                if (lastInputDeviceIsGamepad != 0)
                {

                    if (!locked)
                    {
                        if (!CameraBaseObjectMesh.activeSelf)
                            CameraBaseObjectMesh.SetActive(true);
                    }
                    else
                    {
                        if (CameraBaseObjectMesh.activeSelf)
                            CameraBaseObjectMesh.SetActive(false);
                    }

                    HoverCrosshairHandler();

                    if (lastInputDeviceIsGamepad == 2)
                    {
                        GameManager.Instance.PlayerMouseLook.ForceHideCursor(true);
                        mouseDelta = Vector3.zero;
                    }
                }
                else
                {

                    if (CameraBaseObjectMesh.activeSelf)
                        CameraBaseObjectMesh.SetActive(false);

                    HoverCursorHandler();

                    GameManager.Instance.PlayerMouseLook.ForceHideCursor(false);
                    stickLeftDelta = Vector3.zero;
                    stickRightDelta = Vector3.zero;
                }

                /*Debug.Log("Stick deltas are " + stickRightDelta.ToString() + " and " + stickLeftDelta.ToString());
                Debug.Log("Mouse delta is " + mouseDelta.ToString());*/

                if (InputManager.Instance.GetKeyUp(KeyCode.Mouse0))
                {
                    if (!MapWindowPanel.MouseOverGUI(2))
                    {
                        if (InputManager.Instance.GetKey(KeyCode.LeftControl))
                        {
                            //cancel journey
                            ResetTargetPosition();
                        }
                        else if (InputManager.Instance.GetKey(KeyCode.LeftAlt))
                        {
                            //set pin at point
                            Ray ray = Camera.ScreenPointToRay(Input.mousePosition);
                            RaycastHit hit = new RaycastHit();
                            if (Physics.Raycast(ray, out hit, 1000, Physics.DefaultRaycastLayers))
                            {
                                SetPinPosition(Mathf.RoundToInt(hit.point.x), Mathf.RoundToInt(-hit.point.z));
                            }
                        }
                        else
                        {
                            Ray ray = Camera.ScreenPointToRay(Input.mousePosition);
                            RaycastHit hit = new RaycastHit();
                            if (Physics.Raycast(ray, out hit, 1000, Physics.DefaultRaycastLayers))
                            {
                                SetTargetPosition(Mathf.RoundToInt(hit.point.x), Mathf.RoundToInt(-hit.point.z));
                                //always reset to default timescale
                                timeScaleIndex = defaultTimeScaleTravel;
                                if (travelSpeed == 0)
                                    travelSpeed = 1;
                                automatic = true;
                                //Left Shift to go lock on to player
                                if (InputManager.Instance.GetKey(KeyCode.LeftShift))
                                {
                                    //MapWindowPanel.Instance.logLabel.AddText("Left Shift held. Tracking player.");
                                    locked = true;
                                }
                            }
                        }
                    }
                }
                else if (InputManager.Instance.GetKeyDown(KeyCode.Mouse0))
                {
                    if (!MapWindowPanel.MouseOverGUI(1))
                    {
                        //do thing on down
                    }
                }

                if (lastInputDeviceIsGamepad == 2)
                {
                    if (InputManager.Instance.GetKeyDown(InputManager.Instance.GetJoystickUIBinding(InputManager.JoystickUIActions.LeftClick)))
                    {
                        if (IsLeftTriggerPressed())
                        {
                            //Hold Left Trigger to cancel journey
                            ResetTargetPosition();
                        }
                        else
                        {
                            SetTargetPosition(Mathf.RoundToInt(CameraBaseObject.transform.localPosition.x), Mathf.RoundToInt(-CameraBaseObject.transform.localPosition.z));
                            //always reset to default timescale
                            timeScaleIndex = defaultTimeScaleTravel;
                            if (travelSpeed == 0)
                            {
                                travelSpeed = 1;
                            }
                            automatic = true;
                            //Hold Right Trigger to lock on to player after setting destination
                            if (IsRightTriggerPressed())
                            {
                                //MapWindowPanel.Instance.logLabel.AddText("Right Trigger held. Tracking player.");
                                locked = true;
                            }
                        }
                        PlayButtonClick();
                    }


                    //set pins
                    if (InputManager.Instance.GetKeyDown(InputManager.Instance.GetJoystickUIBinding(InputManager.JoystickUIActions.RightClick)))
                    {
                        if (IsLeftTriggerPressed())
                        {
                            //clear pin
                            ClearPinPosition();
                        }
                        else if (IsRightTriggerPressed())
                        {
                            //Travel to pin (if any)
                            if (PinObject.activeSelf)
                            {
                                MapWindowPanel.Instance.logLabel.AddText("Travelling to pinned position");
                                SetTargetPosition((int)PinPosition.x, (int)-PinPosition.z);
                                if (timeScaleIndex == 0)
                                    SetTimeScale(defaultTimeScaleTravel);
                                if (travelSpeed == 0)
                                    SetTravelSpeed(1);
                                automatic = true;
                                locked = true;
                            }
                            else
                                MapWindowPanel.Instance.logLabel.AddText("No pinned position");
                        }
                        else
                        {
                            //set pin at point
                            SetPinPosition(Mathf.RoundToInt(CameraBaseObject.transform.localPosition.x), Mathf.RoundToInt(-CameraBaseObject.transform.localPosition.z));
                        }
                        PlayButtonClick();
                    }

                    //set view
                    if (InputManager.Instance.GetKeyDown(InputManager.Instance.GetJoystickUIBinding(InputManager.JoystickUIActions.MiddleClick)))
                    {
                        if (IsLeftTriggerPressed())
                        {
                            //look at pin (if any)
                            if (PinObject.activeSelf)
                                SetCameraTargetPosition(Mathf.RoundToInt(PinObject.transform.localPosition.x), Mathf.RoundToInt(-PinObject.transform.localPosition.z));
                            else
                                MapWindowPanel.Instance.logLabel.AddText("No pinned position");
                        }
                        else if (IsRightTriggerPressed())
                        {
                            //look at destination (if any)
                            if (TargetObject.activeSelf)
                                SetCameraTargetPosition(Mathf.RoundToInt(TargetObject.transform.localPosition.x), Mathf.RoundToInt(-TargetObject.transform.localPosition.z));
                            else
                                MapWindowPanel.Instance.logLabel.AddText("No target destination");
                        }
                        else
                        {
                            //look at player
                            SetCameraTargetPlayer();
                        }
                        PlayButtonClick();
                    }

                    //set time and speed via dpad
                    float DPadHorizontal = Input.GetAxis(gamepadDPadAxisX);
                    float DPadVertical = Input.GetAxis(gamepadDPadAxisY);

                    if (DPadHorizontal > 0.5f)
                    {
                        if (!DPadHorizontalPressed)
                        {
                            if (IsRightTriggerPressed())
                            {
                                //pause
                                SetTimeScale(5);
                                PlayButtonClick();
                            }
                            else
                            {
                                //increment time up
                                int index = timeScaleIndex + 1;
                                if (index < 6)
                                {
                                    SetTimeScale(index);
                                    PlayButtonClick();
                                }
                            }
                            DPadHorizontalPressed = true;
                        }
                    }
                    else if (DPadHorizontal < -0.5f)
                    {
                        if (!DPadHorizontalPressed)
                        {
                            if (IsRightTriggerPressed())
                            {
                                //pause
                                SetTimeScale(0);
                                PlayButtonClick();
                            }
                            else
                            {
                                //increment time down
                                int index = timeScaleIndex - 1;
                                if (index > -1)
                                {
                                    SetTimeScale(index);
                                    PlayButtonClick();
                                }
                            }
                            DPadHorizontalPressed = true;
                        }
                    }
                    else
                        DPadHorizontalPressed = false;

                    if (DPadVertical > 0.5f)
                    {
                        if (!DPadVerticalPressed)
                        {
                            if (IsRightTriggerPressed())
                            {
                                //set to max speed
                                if (travelSpeed == 0)
                                    automatic = true;
                                SetTravelSpeed(2);
                                PlayButtonClick();
                            }
                            else
                            {
                                //increment speed up
                                int index = travelSpeed + 1;
                                if (index < 3)
                                {
                                    if (travelSpeed == 0)
                                        automatic = true;
                                    SetTravelSpeed(index);
                                    PlayButtonClick();
                                }
                            }
                            DPadVerticalPressed = true;
                        }
                    }
                    else if (DPadVertical < -0.5f)
                    {
                        if (!DPadVerticalPressed)
                        {
                            if (IsRightTriggerPressed())
                            {
                                //set to camp
                                if (TryCamp())
                                {
                                    SetTravelSpeed(0);
                                    automatic = true;
                                }
                                PlayButtonClick();
                            }
                            else
                            {
                                //increment speed down
                                int index = travelSpeed - 1;
                                if (index > 0)
                                {
                                    SetTravelSpeed(index);
                                    PlayButtonClick();
                                }
                                else if (index == 0 || travelSpeed == 0)
                                {
                                    //increment speed down
                                    if (TryCamp())
                                    {
                                        SetTravelSpeed(0);
                                        automatic = true;
                                    }
                                    PlayButtonClick();
                                }
                            }
                            DPadVerticalPressed = true;
                        }
                    }
                    else
                        DPadVerticalPressed = false;

                    //find location
                    if (InputManager.Instance.GetKeyDown(gamepadLeftBumperKeyCode))
                    {
                        //find location
                        //ClickButtonFindLocation();
                    }

                    //toggle ship
                    if (InputManager.Instance.GetKeyDown(gamepadRightBumperKeyCode))
                    {
                        if (TryShip())
                        {

                        }
                    }
                }

                //camera inputs
                bool moving = false;
                bool rotating = false;
                bool zooming = false;

                if (showNavigationPanel)
                {
                    GUIMoveVertical = Mathf.MoveTowards(GUIMoveVertical, GUIMoveVerticalTarget, Time.unscaledDeltaTime * 10f);
                    GUIMoveHorizontal = Mathf.MoveTowards(GUIMoveHorizontal, GUIMoveHorizontalTarget, Time.unscaledDeltaTime * 10f);
                    GUIOrbit = Mathf.MoveTowards(GUIOrbit, -GUIOrbitTarget, Time.unscaledDeltaTime * 10f);
                    GUIZoom = Mathf.MoveTowards(GUIZoom, -GUIZoomTarget, Time.unscaledDeltaTime * 10f);
                }
                else
                {

                    GUIMoveVertical = 0;
                    GUIMoveHorizontal = 0;
                    GUIOrbit = 0;
                    GUIZoom = 0;
                }

                if (stickLeftDelta.sqrMagnitude > 0 ||
                    InputManager.Instance.GetKey(KeyCode.W) ||
                    InputManager.Instance.GetKey(KeyCode.A) ||
                    InputManager.Instance.GetKey(KeyCode.S) ||
                    InputManager.Instance.GetKey(KeyCode.D) ||
                    InputManager.Instance.GetKey(KeyCode.Mouse2) ||
                    GUIMoveVertical != 0 ||
                    GUIMoveHorizontal != 0
                    )
                {
                    moving = true;
                    locked = false;
                }

                if (stickRightDelta.y != 0 || Input.GetAxis("Mouse ScrollWheel") != 0 || GUIZoom != 0)
                    zooming = true;

                if (stickRightDelta.sqrMagnitude > 0 || InputManager.Instance.GetKey(KeyCode.Mouse1) || GUIOrbit != 0 || GUIZoom != 0)
                    rotating = true;

                if (moving || rotating || zooming)
                {
                    float zoomFactor = CameraObject.transform.localPosition.z / -100;

                    if (moving)
                    {
                        float horizontal = stickLeftDelta.x;
                        float vertical = stickLeftDelta.y;

                        if (InputManager.Instance.GetKey(KeyCode.W))
                            vertical = 1;
                        if (InputManager.Instance.GetKey(KeyCode.S))
                            vertical = -1;
                        if (InputManager.Instance.GetKey(KeyCode.D))
                            horizontal = 1;
                        if (InputManager.Instance.GetKey(KeyCode.A))
                            horizontal = -1;
                        if (InputManager.Instance.GetKey(KeyCode.Mouse2))
                        {
                            horizontal -= mouseDelta.x * 0.05f;
                            vertical -= mouseDelta.y * 0.05f;
                        }

                        if (GUIMoveVertical != 0)
                            vertical += GUIMoveVertical * 0.1f * navigationMoveSensitivity;

                        if (GUIMoveHorizontal != 0)
                            horizontal += GUIMoveHorizontal * 0.1f * navigationMoveSensitivity;

                        basePosition += ((CameraBaseObject.transform.forward * vertical) + (CameraBaseObject.transform.right * horizontal)).normalized * 100 * Mathf.Clamp01(zoomFactor) * Time.unscaledDeltaTime;

                        //clamp
                        if (basePosition.x < 0)
                            basePosition = new Vector3(0, basePosition.y, basePosition.z);
                        if (basePosition.x > 1000)
                            basePosition = new Vector3(1000, basePosition.y, basePosition.z);
                        if (basePosition.z > 0)
                            basePosition = new Vector3(basePosition.x, basePosition.y, 0);
                        if (basePosition.z < -500)
                            basePosition = new Vector3(basePosition.x, basePosition.y, -500);
                    }

                    if (rotating)
                    {
                        float turn = (stickRightDelta.x * 1f) + (mouseDelta.x * 10f);

                        if (GUIOrbit != 0 || GUIZoom != 0)
                            turn = GUIOrbit * 100f * navigationOrbitSensitivity;

                        turnAngle += turn * Mathf.Clamp01(zoomFactor * 100) * Time.unscaledDeltaTime;

                        if (stickRightDelta.y != 0 || GUIZoom != 0)
                            pitchAngle = Mathf.Lerp(15, 60, Mathf.Clamp01(zoomLevel / -25));
                        else if (GUIOrbit == 0)
                        {
                            pitchAngle -= mouseDelta.y * 10f * Mathf.Clamp01(zoomFactor * 100) * Time.unscaledDeltaTime;

                            if (pitchAngle > 60)
                                pitchAngle = 60;
                            if (pitchAngle < 15)
                                pitchAngle = 15;
                        }
                    }

                    if (zooming)
                    {
                        float zoom = (stickRightDelta.y * 0.1f) - (Input.GetAxis("Mouse ScrollWheel") * 2000f);

                        if (GUIZoom != 0)
                            zoom = GUIZoom * 100f * navigationZoomSensitivity;

                        zoomLevel -= zoom * Mathf.Clamp01(0.5f + zoomFactor) * Time.unscaledDeltaTime;

                        if (zoomLevel > -1)
                            zoomLevel = -1;
                        if (zoomLevel < -100)
                            zoomLevel = -100;
                    }

                    //set last mouse position
                    lastMousePosition = Input.mousePosition;
                }

                //simulation inputs
                //toggle simulation
                if (InputManager.Instance.GetKeyUp(KeyCode.Space))
                {
                    automatic = false;
                    if (timeScaleIndex > 0)
                        timeScaleIndex = 0;
                    else
                        timeScaleIndex = 1;
                    timeScaleIndex = 0;
                }

                //set travel speed
                if (InputManager.Instance.GetKeyUp(KeyCode.F1))
                {
                    if (TryCamp())
                    {
                        SetTravelSpeed(0);
                        automatic = true;
                    }
                }

                if (InputManager.Instance.GetKeyUp(KeyCode.F2))
                {
                    if (travelSpeed == 0)
                        automatic = true;
                    SetTravelSpeed(1);
                }

                if (InputManager.Instance.GetKeyUp(KeyCode.F3))
                {
                    if (travelSpeed == 0)
                        automatic = true;
                    SetTravelSpeed(2);
                }

                //set time scale
                if (InputManager.Instance.GetKeyUp(KeyCode.Alpha1))
                {
                    SetTimeScale(1);
                }
                if (InputManager.Instance.GetKeyUp(KeyCode.Alpha2))
                {
                    SetTimeScale(2);
                }
                if (InputManager.Instance.GetKeyUp(KeyCode.Alpha3))
                {
                    SetTimeScale(3);
                }
                if (InputManager.Instance.GetKeyUp(KeyCode.Alpha4))
                {
                    SetTimeScale(4);
                }
                if (InputManager.Instance.GetKeyUp(KeyCode.Alpha5))
                {
                    SetTimeScale(5);
                }

                //exit with ESCAPE or same KEY
                if (InputManager.Instance.GetBackButtonUp() || InputManager.Instance.GetKeyUp(keyboardMapKeyCode) || InputManager.Instance.GetKeyUp(gamepadMapKeyCode))
                {
                    StopShowingMap();
                }

                yield return new WaitForEndOfFrame();
            }



            MapWindowPanel.Enabled = false;

            fade = true;
            //DaggerfallUI.Instance.FadeBehaviour.FadeHUDToBlack();

            yield return new WaitForSecondsRealtime(0.5f);

            DisableMap();

            yield return new WaitForSecondsRealtime(1f);

            //if not location spawn things
            if (!GameManager.Instance.PlayerGPS.CurrentLocation.Loaded)
            {
                if (onWater)
                {
                    if (onShip)
                        SpawnShip();
                }
                else
                {
                    if (travelSpeed == 0)
                        SpawnCamp();
                }
            }

            yield return new WaitForSecondsRealtime(1f);

            fade = false;

            yield return new WaitForSecondsRealtime(0.5f);

            GameManager.Instance.PauseGame(false);
            playerEntity.IsResting = false;

            GameManager.Instance.PlayerMouseLook.ForceHideCursor(false);
            DaggerfallUI.Instance.DaggerfallHUD.Enabled = true;

            yield return new WaitForSecondsRealtime(0.5f);

            if (runEncounter)
            {
                WarmAshesEncounter();
            }

            yield return new WaitForSecondsRealtime(0.5f);

            if (WarmAshesWilderness != null)
            {
                ModManager.Instance.SendModMessage(WarmAshesWilderness.Title, "allowDungeonExteriorEncounter");
                ModManager.Instance.SendModMessage(WarmAshesWilderness.Title, "allowWildernessEncounter");
                ModManager.Instance.SendModMessage(WarmAshesWilderness.Title, "allowOceanShips");
            }

            playerEntity.PreventEnemySpawns = false;

            showingMap = null;
        }


        public void SetPlayerPosition(int X, int Y)
        {
            PlayerObject.transform.localPosition = new Vector3(X, 0, -Y);

            SetPlayerHeight();
        }

        void SetPlayerHeight()
        {
            Ray ray = new Ray(new Vector3(PlayerObject.transform.position.x, ParentObject.transform.position.y + 100, PlayerObject.transform.position.z), Vector3.down);
            RaycastHit hit = new RaycastHit();
            if (Physics.Raycast(ray, out hit, 200, Physics.DefaultRaycastLayers))
            {
                PlayerObject.transform.position = new Vector3(PlayerObject.transform.position.x, hit.point.y, PlayerObject.transform.position.z);
            }
        }

        public void SetTargetPosition(int X, int Y)
        {
            TargetObject.transform.localPosition = new Vector3(X, 0, -Y);

            Ray ray = new Ray(new Vector3(TargetObject.transform.position.x, ParentObject.transform.position.y + 100, TargetObject.transform.position.z), Vector3.down);
            RaycastHit hit = new RaycastHit();
            if (Physics.Raycast(ray, out hit, 200, Physics.DefaultRaycastLayers))
            {
                TargetObject.transform.position = new Vector3(TargetObject.transform.position.x, hit.point.y, TargetObject.transform.position.z);
            }
        }

        public void ResetTargetPosition()
        {
            TargetObject.transform.position = PlayerObject.transform.position;
            TargetObject.SetActive(false);
        }

        public void SetPinPosition(int X, int Y)
        {
            PinObject.transform.localPosition = new Vector3(X, 0, -Y);

            Ray ray = new Ray(new Vector3(PinObject.transform.position.x, ParentObject.transform.position.y + 100, PinObject.transform.position.z), Vector3.down);
            RaycastHit hit = new RaycastHit();
            if (Physics.Raycast(ray, out hit, 200, Physics.DefaultRaycastLayers))
            {
                PinObject.transform.position = new Vector3(PinObject.transform.position.x, hit.point.y, PinObject.transform.position.z);
            }

            if (PinPosition != PinObject.transform.localPosition)
            {
                MapWindowPanel.Instance.logLabel.AddText("Pin set");
                PinPosition = PinObject.transform.localPosition;
            }
            else
                ClearPinPosition();
        }

        public void ClearPinPosition()
        {
            MapWindowPanel.Instance.logLabel.AddText("Pin cleared");
            PinPosition = Vector3.zero;
        }

        void TeleportPlayerToPosition(int X, int Y)
        {
            GameManager.Instance.StreamingWorld.RestoreWorldCompensationHeight(0);
            if (locationObjectsMap[X, Y] != null)
                GameManager.Instance.StreamingWorld.TeleportToCoordinates(X, Y, StreamingWorld.RepositionMethods.DirectionFromStartMarker);
            else
            {
                GameManager.Instance.StreamingWorld.TeleportToCoordinates(X, Y, StreamingWorld.RepositionMethods.Offset);
                Vector3 offset = new Vector3(410, 0, 410);
                if (onWater && onShip)
                {
                    //offset *= 0.5f;
                }
                else if (BasicRoads != null && travelSpeed == 0)
                {
                    int road = Compatibility.BasicRoadsUtils.GetRoadType(X, Y);
                    if (road != 0)
                    {
                        List<Vector3> directions = Compatibility.BasicRoadsUtils.GetRoadDirections(Compatibility.BasicRoadsUtils.GetRoadData(X, Y));
                        List<Vector3> free = new List<Vector3>();

                        if (!directions.Contains(Vector3.up))
                            free.Add(Vector3.forward);
                        if (!directions.Contains(Vector3.down))
                            free.Add(Vector3.back);
                        if (!directions.Contains(Vector3.right))
                            free.Add(Vector3.right);
                        if (!directions.Contains(Vector3.left))
                            free.Add(Vector3.left);
                        if (!directions.Contains(Vector3.up + Vector3.right))
                            free.Add(Vector3.forward + Vector3.right);
                        if (!directions.Contains(Vector3.down + Vector3.right))
                            free.Add(Vector3.back + Vector3.right);
                        if (!directions.Contains(Vector3.up + Vector3.left))
                            free.Add(Vector3.forward + Vector3.left);
                        if (!directions.Contains(Vector3.down + Vector3.left))
                            free.Add(Vector3.back + Vector3.left);

                        if (free.Count > 0)
                            offset += free[UnityEngine.Random.Range(0,free.Count)] * 41f;
                    }
                }
                GameManager.Instance.StreamingWorld.SetAutoReposition(StreamingWorld.RepositionMethods.Offset, offset);
                //set player to face last movement direction
                GameManager.Instance.PlayerMouseLook.SetHorizontalFacing(lastMoveDirection);
            }

        }

        public void SetCameraPosition(int X, int Y)
        {
            CameraBaseObject.transform.localPosition = new Vector3(X, 0, -Y);
            basePosition = CameraBaseObject.transform.localPosition;
            locked = false;
            SetCameraHeight();
        }

        public void SetCameraTargetPosition(int X, int Y)
        {
            basePosition = new Vector3(X, 0, -Y);
            locked = false;
            SetCameraHeight();
        }

        public void SetCameraTargetPlayer()
        {
            if (timeScaleIndex > 0)
                locked = true;
            else
            {
                locked = false;
                basePosition = new Vector3(Mathf.RoundToInt(PlayerObject.transform.localPosition.x), 0, Mathf.RoundToInt(PlayerObject.transform.localPosition.z));
            }
            //locked = true;
        }

        void SetCameraHeight()
        {
            //raycast from above the camera base position and check height of terrain
            Ray ray = new Ray(new Vector3(CameraBaseObject.transform.position.x, ParentObject.transform.position.y + 100, CameraBaseObject.transform.position.z), Vector3.down);
            RaycastHit hit = new RaycastHit();

            float baseHeight = 0;

            if (Physics.Raycast(ray, out hit, 200, Physics.DefaultRaycastLayers))
            {
                baseHeight = hit.point.y;
            }

            ray = new Ray(new Vector3(CameraObject.transform.position.x, ParentObject.transform.position.y + 100, CameraObject.transform.position.z), Vector3.down);
            if (Physics.Raycast(ray, out hit, 200, Physics.DefaultRaycastLayers))
            {
                if (hit.point.y > baseHeight)
                    baseHeight = hit.point.y;
            }

            if (baseHeight != 0)
                CameraBaseObject.transform.position = new Vector3(CameraBaseObject.transform.position.x, baseHeight, CameraBaseObject.transform.position.z);
        }

        void SetLightAngle(int minutes)
        {
            // Get value 0-1 for dawn through dusk
            float dawn = DaggerfallDateTime.DawnHour * DaggerfallDateTime.MinutesPerHour;
            float dayRange = DaggerfallDateTime.DuskHour * DaggerfallDateTime.MinutesPerHour - dawn;
            float time = (minutes - dawn) / dayRange;

            // Set angle of rotation based on time of day and user value
            float xrot = 180f * time;

            float colorTime = 0;
            if (dfUnity.WorldTime.Now.Hour >= DaggerfallDateTime.DuskHour || dfUnity.WorldTime.Now.Hour < DaggerfallDateTime.DawnHour)
            {
                if (dfUnity.WorldTime.Now.Hour >= 0 && dfUnity.WorldTime.Now.Hour < DaggerfallDateTime.DawnHour)
                {
                    //time is -0.5 to 0.0
                    //needs to be 0.5 to 1.0
                    colorTime = time + 1;
                }
                else
                {   //time is 1.0 to 1.5
                    //needs to be 0.0 to 0.5
                    colorTime = time-1;
                }

                Sun.color = NightGradient.Evaluate(colorTime);

                xrot += 180;
            }
            else
            {
                colorTime = time;

                Sun.color = DayGradient.Evaluate(colorTime);
            }

            //Debug.Log(colorTime.ToString());

            Camera.backgroundColor = Sun.color;
            RenderSettings.fogColor = Sun.color;
            RenderSettings.fogDensity = 0.01f * fogDensityMod;

            SunObject.transform.rotation = Quaternion.Euler(xrot, -60, 0);
        }

        void SetTextures(bool force = false)
        {
            bool isWinter = (dfUnity.WorldTime.Now.SeasonValue == DaggerfallDateTime.Seasons.Winter);

            //do not update textures if not required and not forced
            if (wasWinter == isWinter && !force)
            {
                //Debug.Log("WORLDWALKING - NO TEXTURE UPDATE REQUIRED");
                return;
            }

            //Debug.Log("WORLDWALKING - UPDATING TEXTURES. WINTER IS " + isWinter.ToString());

            waterTexture = DaggerfallUnity.Instance.MaterialReader.TextureReader.GetTexture2D(302, 0);

            if (isWinter)
            {
                grassTexture = DaggerfallUnity.Instance.MaterialReader.TextureReader.GetTexture2D(303, 2);
            }
            else
            {
                grassTexture = DaggerfallUnity.Instance.MaterialReader.TextureReader.GetTexture2D(302, 2);
            }

            mountainTexture = DaggerfallUnity.Instance.MaterialReader.TextureReader.GetTexture2D(102, 2);
            swampTexture = DaggerfallUnity.Instance.MaterialReader.TextureReader.GetTexture2D(402, 2);
            sandTexture = DaggerfallUnity.Instance.MaterialReader.TextureReader.GetTexture2D(004, 1);

            stoneTexture = DaggerfallUnity.Instance.MaterialReader.TextureReader.GetTexture2D(302, 3);
            dirtTexture = DaggerfallUnity.Instance.MaterialReader.TextureReader.GetTexture2D(302, 1);

            roadTemperate = DaggerfallUnity.Instance.MaterialReader.TextureReader.GetTexture2D(302, 46);
            trackTemperate = DaggerfallUnity.Instance.MaterialReader.TextureReader.GetTexture2D(302, 54);
            roadDesert = DaggerfallUnity.Instance.MaterialReader.TextureReader.GetTexture2D(004, 46);
            trackDesert = DaggerfallUnity.Instance.MaterialReader.TextureReader.GetTexture2D(004, 54);

            //season changed, update chunks, if any
            if (ChunkObjects.Count > 0)
            {
                foreach (GameObject Chunk in ChunkObjects)
                {
                    MeshRenderer meshRenderer = Chunk.GetComponent<MeshRenderer>();
                    if (meshRenderer != null)
                    {
                        meshRenderer.material.SetTexture("_MainTex1", waterTexture);
                        meshRenderer.material.SetTexture("_MainTex2", grassTexture);
                        meshRenderer.material.SetTextureScale("_MainTex2", Vector2.one * 2f);
                        meshRenderer.material.SetTexture("_MainTex3", sandTexture);
                        meshRenderer.material.SetTextureScale("_MainTex3", Vector2.one * 3f);
                        meshRenderer.material.SetTexture("_MainTex4", mountainTexture);
                        meshRenderer.material.SetTexture("_MainTex5", swampTexture);
                    }
                }
            }

            wasWinter = isWinter;
        }

        public void PlayButtonClick()
        {
            InterfaceAudioSource.PlayOneShot((int)SoundClips.ButtonClick, 0, 1);
        }

        private void ModCompatibilityChecking()
        {
            BasicRoads = ModManager.Instance.GetModFromGUID("566ab21a-22d8-4eea-8ccd-6cb8f7a7ed25");
            if (BasicRoads != null)
                Compatibility.BasicRoadsUtils.Init();

            WorldOfDaggerfallTerrain = ModManager.Instance.GetModFromGUID("a9091dd7-e07a-4171-b16d-d13d67a5f221");
            WODTerrainHeightMap = mod.GetAsset<Texture2D>("Textures/WODTerrain_height.png");

            WarmAshesWilderness = ModManager.Instance.GetModFromGUID("2858f27c-7b60-4aa4-a22f-c29d2dc9e6fe");

            DynamicAmbienceObject = GameObject.Find("Dynamic Ambience");
            DynamicMusicObject = GameObject.Find("Dynamic Music");
        }

        void EnableMap()
        {
            /*//setup camera
            Camera.enabled = false;
            PostProcessLayer mainPostprocessingLayer = GameManager.Instance.MainCameraObject.GetComponent<PostProcessLayer>();
            mapPostprocessingLayer.antialiasingMode = mainPostprocessingLayer.antialiasingMode;
            mapPostprocessingLayer.fastApproximateAntialiasing = mainPostprocessingLayer.fastApproximateAntialiasing;
            mapPostprocessingLayer.subpixelMorphologicalAntialiasing = mainPostprocessingLayer.subpixelMorphologicalAntialiasing;
            mapPostprocessingLayer.temporalAntialiasing = mainPostprocessingLayer.temporalAntialiasing;
            Camera.enabled = true;*/

            ParentObject.SetActive(true);
            SetPlayerPosition(GameManager.Instance.PlayerGPS.CurrentMapPixel.X, GameManager.Instance.PlayerGPS.CurrentMapPixel.Y);
            SetCameraPosition(GameManager.Instance.PlayerGPS.CurrentMapPixel.X, GameManager.Instance.PlayerGPS.CurrentMapPixel.Y);
            SetLightAngle(dfUnity.WorldTime.Now.MinuteOfDay);
            SetTextures();

            //place target over player
            ResetTargetPosition();

            if (SongManager.SongPlayer != null)
                SongManager.StartPlaying();

            //disable other things here
            GameManager.Instance.ExteriorParent.SetActive(false);
            GameManager.Instance.MainCameraObject.GetComponent<AudioListener>().enabled = false;

            if (DynamicAmbienceObject != null)
                DynamicAmbienceObject.SetActive(false);

            if (DynamicMusicObject != null)
                DynamicMusicObject.SetActive(false);
        }

        void DisableMap(bool teleport = true, bool start = false)
        {
            if (SongManager.SongPlayer != null)
                SongManager.StopPlaying();

            ParentObject.SetActive(false);

            if (!start)
            {
                //re-enable other things here
                GameManager.Instance.ExteriorParent.SetActive(true);
                GameManager.Instance.MainCameraObject.GetComponent<AudioListener>().enabled = true;
            }

            if (DynamicAmbienceObject != null)
                DynamicAmbienceObject.SetActive(true);

            if (DynamicMusicObject != null)
                DynamicMusicObject.SetActive(true);

            if (teleport)
            {
                bool journeyed = false;
                //sets the world time if it is different
                if (minutesSince > 0)
                {
                    journeyed = true;
                }

                //set the player position if it was different
                int X = Mathf.RoundToInt(PlayerObject.transform.localPosition.x);
                int Y = Mathf.RoundToInt(-PlayerObject.transform.localPosition.z);
                if (GameManager.Instance.PlayerGPS.CurrentMapPixel.X != X ||
                    GameManager.Instance.PlayerGPS.CurrentMapPixel.Y != Y)
                {
                    journeyed = true;
                }

                if (journeyed)
                {
                    ClearSpawnedObjects();
                    TeleportPlayerToPosition(X, Y);
                    //level up player skills when ending a journey
                    playerEntity.RaiseSkills();
                }
            }
        }

        float CalculateTravelSpeed()
        {
            float speed = 1;
            float speedFactor = playerEntity.Stats.LiveSpeed / 100f;
            float runningFactor = playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Running) / 100f;

            //horse and cart increases travel speed
            if (GameManager.Instance.TransportManager.HasCart())
            {
                if (travelSpeed == 2)
                    speed = 0.016f + (0.016f * Mathf.Lerp(-0.16f,0.16f,speedFactor)) * Mathf.Lerp(1.35f, 1.85f, runningFactor);
                else
                    speed = 0.016f + (0.016f * Mathf.Lerp(-0.16f, 0.16f, speedFactor));
            }
            else if (GameManager.Instance.TransportManager.HasHorse())
            {
                if (travelSpeed == 2)
                    speed = 0.025f + (0.025f * Mathf.Lerp(-0.12f, 0.12f, speedFactor)) * Mathf.Lerp(1.35f, 1.85f, runningFactor);
                else
                    speed = 0.025f + (0.025f * Mathf.Lerp(-0.12f, 0.12f, speedFactor));
            }
            else
            {
                if (travelSpeed == 2)
                    speed = 0.01f + (0.01f * Mathf.Lerp(-0.33f, 0.33f, speedFactor)) * Mathf.Lerp(1.35f, 1.85f, runningFactor);
                else
                    speed = 0.01f + (0.01f * Mathf.Lerp(-0.33f, 0.33f, speedFactor));
            }

            //water
            if (onWater)
            {
                if (!onShip)
                {
                    float swimFactor = playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Swimming) / 100f;
                    speed *= Mathf.Lerp(0.25f, 0.75f, swimFactor);
                }
                else
                {
                    //do wind stuff here
                    speed = 0.05f * (float)travelSpeed;
                }
            }

            //night penalty
            if (dfUnity.WorldTime.Now.IsNight)
            {
                if (PlayerLightObject.activeSelf)
                    speed *= 0.8f;
                else
                    speed *= 0.6f;
            }

            //roads don't affect speed, just fatigue

            //climate
            if (currentClimate == (int)MapsFile.Climates.Swamp ||
            currentClimate == (int)MapsFile.Climates.Rainforest ||
            currentClimate == (int)MapsFile.Climates.MountainWoods)
                speed *= 0.9f;
            else if (currentClimate == (int)MapsFile.Climates.Mountain ||
            currentClimate == (int)MapsFile.Climates.HauntedWoodlands)
                speed *= 0.8f;

            //weather
            WeatherType weather = GameManager.Instance.WeatherManager.PlayerWeather.WeatherType;
            if (!onShip)
            {
                if (weather == WeatherType.Thunder || weather == WeatherType.Snow) //20% speed penalty in snow and thunder
                {
                    speed *= 0.8f;
                }
                else if (weather == WeatherType.Rain)   //10% speed penalty in rain
                    speed *= 0.9f;
            }
            else
            {
                if (weather == WeatherType.Thunder)
                    speed *= 1.2f;
                else if (weather == WeatherType.Fog)  
                    speed *= 0.2f;
            }

            return speed * multiplierTravelSpeed;
        }

        float CalculateFatigueDrain()
        {
            float drain = 1;

            //horse and cart lowers fatigue drain
            if (GameManager.Instance.TransportManager.HasCart())
                drain = 0.2f;
            else if (GameManager.Instance.TransportManager.HasHorse())
                drain = 0.6f;

            //water
            if (onWater)
            {
                if (!onShip)
                    drain *= 2f;
                else
                {
                    if (GameManager.Instance.WeatherManager.PlayerWeather.WeatherType == WeatherType.Thunder)
                        drain = 1;
                    else
                        drain = -1f;
                }
            }

            //climate
            if (currentClimate == (int)MapsFile.Climates.Swamp ||
            currentClimate == (int)MapsFile.Climates.Rainforest ||
            currentClimate == (int)MapsFile.Climates.MountainWoods)
                drain *= 1.5f;
            else if (currentClimate == (int)MapsFile.Climates.Mountain ||
            currentClimate == (int)MapsFile.Climates.HauntedWoodlands)
                drain *= 2f;

            //weather
            WeatherType weather = GameManager.Instance.WeatherManager.PlayerWeather.WeatherType;
            if (weather == WeatherType.Thunder || weather == WeatherType.Snow) //+40% fatigue drain in snow or thunderstorms
            {
                drain *= 1.4f;
            }
            else if (weather == WeatherType.Rain)   //20% more fatigue drain in rain
                drain *= 1.2f;

            //ground slope

            //roads
            if (onRoad == 2)    //track
                drain *= 0.8f;
            else if (onRoad == 1)   //paved
                drain *= 0.6f;

            if (playerEntity.Career.Athleticism)
            {
                if (playerEntity.ImprovedAthleticism)
                    drain *= 0.8f;
                else
                    drain *= 0.9f;
            }

            return drain * multiplierFatigueDrain;
        }

        float CalculateSightRange()
        {
            float range = 8;

            if (travelSpeed == 2)
                range *= 0.6f;
            else if (travelSpeed == 1)
                range *= 0.8f;

            //night penalty
            if (dfUnity.WorldTime.Now.IsNight)
                range *= 0.6f;

            //climate
            if (currentClimate == (int)MapsFile.Climates.Swamp ||
            currentClimate == (int)MapsFile.Climates.Rainforest ||
            currentClimate == (int)MapsFile.Climates.MountainWoods)
                range *= 0.8f;
            else if (currentClimate == (int)MapsFile.Climates.Mountain ||
            currentClimate == (int)MapsFile.Climates.HauntedWoodlands)
                range *= 0.6f;

            //weather
            WeatherType weather = GameManager.Instance.WeatherManager.PlayerWeather.WeatherType;
            if (weather == WeatherType.Thunder || weather == WeatherType.Fog) //40% less sight range in fog or thunderstorms
            {
                range *= 0.6f;
            }
            else if (weather == WeatherType.Rain || weather == WeatherType.Snow)   //20% less sight range in rain and snow
                range *= 0.8f;

            return range * multiplierSightRadius;
        }
        float CalculateStealth()
        {
            float value = 3 * Mathf.Lerp(0.5f, 1.5f, playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth) / 100f);

            if (travelSpeed > 0)
            {
                //travelling
                if (travelSpeed == 2)
                    value *= 0.6f;
                else if (travelSpeed == 1)
                    value *= 0.8f;

                if (GameManager.Instance.TransportManager.HasCart() || GameManager.Instance.TransportManager.HasHorse())
                {
                    //while travelling having a horse or cart makes it easier to be spotted
                    float horseMod = 1;
                    if (GameManager.Instance.TransportManager.HasCart())
                        horseMod *= 0.4f;
                    else if (GameManager.Instance.TransportManager.HasHorse())
                        horseMod *= 0.6f;

                    value *= horseMod;
                }
            }
            else
            {
                //camped
            }

            //night bonus
            if (dfUnity.WorldTime.Now.IsNight)
                value *= 1.2f;

            //climate
            if (currentClimate == (int)MapsFile.Climates.Mountain ||
            currentClimate == (int)MapsFile.Climates.Rainforest ||
            currentClimate == (int)MapsFile.Climates.MountainWoods ||
            currentClimate == (int)MapsFile.Climates.HauntedWoodlands)
                value *= 1.2f;

            return value;
        }

        void UpdatePlayerSoundClip()
        {
            if (travelSpeed == 0)
            {
                PlayerAudioSource.SetSound((int)SoundClips.SnoringByFire, AudioPresets.LoopOnDemand, 1);
            }
            else if (onWater)
            {
                PlayerAudioSource.SetSound((int)SoundClips.EnemyEelMove, AudioPresets.LoopOnDemand, 1);
            }
            else if (GameManager.Instance.TransportManager.HasCart())
            {
                PlayerAudioSource.SetSound((int)SoundClips.HorseAndCart, AudioPresets.LoopOnDemand, 1);
            }
            else if (GameManager.Instance.TransportManager.HasHorse())
            {
                PlayerAudioSource.SetSound((int)SoundClips.EnemyCentaurMove, AudioPresets.LoopOnDemand, 1);
            }
            else
            {
                PlayerAudioSource.SetSound((int)SoundClips.EnemyHumanMove, AudioPresets.LoopOnDemand, 1);
            }
        }

        void UpdateAmbientSoundClip()
        {
            if (GameManager.Instance.WeatherManager.PlayerWeather.WeatherType == WeatherType.Rain || GameManager.Instance.WeatherManager.PlayerWeather.WeatherType == WeatherType.Thunder)
            {
                //weather is raining
                AmbientAudioSource.SetSound((int)SoundClips.AmbientRaining, AudioPresets.LoopOnDemand, 1);
            }
            else if (dfUnity.WorldTime.Now.IsNight)
            {
                AmbientAudioSource.SetSound((int)SoundClips.AmbientCrickets, AudioPresets.LoopOnDemand, 1);
            }
            else
            {
                ContentReader.MapSummary summary;
                if (dfUnity.ContentReader.HasLocation(Mathf.RoundToInt(PlayerObject.transform.localPosition.x), Mathf.RoundToInt(-PlayerObject.transform.localPosition.z), out summary))
                {
                    int soundClip = (int)SoundClips.None;
                    switch (summary.LocationType)
                    {
                        case DFRegion.LocationTypes.DungeonRuin:
                        case DFRegion.LocationTypes.DungeonKeep:
                        case DFRegion.LocationTypes.DungeonLabyrinth:
                        case DFRegion.LocationTypes.Graveyard:
                        case DFRegion.LocationTypes.Coven:
                            soundClip = (int)SoundClips.AmbientWindMoanDeep;
                            break;
                        case DFRegion.LocationTypes.Tavern:
                        case DFRegion.LocationTypes.TownCity:
                        case DFRegion.LocationTypes.TownHamlet:
                        case DFRegion.LocationTypes.TownVillage:
                        case DFRegion.LocationTypes.HomeFarms:
                        case DFRegion.LocationTypes.HomeWealthy:
                        case DFRegion.LocationTypes.HomePoor:
                        case DFRegion.LocationTypes.ReligionTemple:
                        case DFRegion.LocationTypes.ReligionCult:
                            soundClip = (int)SoundClips.AmbientPeople1;
                            break;
                        default:
                            break;
                    }
                    AmbientAudioSource.SetSound(soundClip, AudioPresets.LoopOnDemand, 1);
                }
                else
                    AmbientAudioSource.SetSound((int)SoundClips.BirdCall1, AudioPresets.LoopOnDemand, 1);
            }
        }

        void PlayAmbientSoundClip()
        {
            if (GameManager.Instance.WeatherManager.PlayerWeather.WeatherType == WeatherType.Rain || GameManager.Instance.WeatherManager.PlayerWeather.WeatherType == WeatherType.Thunder)
            {
                //weather is raining
                AmbientAudioSource.SetSound((int)SoundClips.AmbientRaining, AudioPresets.LoopOnDemand, 1);
            }
            else if (dfUnity.WorldTime.Now.IsNight)
            {
                AmbientAudioSource.SetSound((int)SoundClips.AmbientCrickets, AudioPresets.LoopOnDemand, 1);
            }
            else
            {
                ContentReader.MapSummary summary;
                if (dfUnity.ContentReader.HasLocation(Mathf.RoundToInt(PlayerObject.transform.localPosition.x), Mathf.RoundToInt(-PlayerObject.transform.localPosition.z), out summary))
                {
                    int soundClip = (int)SoundClips.None;
                    switch (summary.LocationType)
                    {
                        case DFRegion.LocationTypes.DungeonRuin:
                        case DFRegion.LocationTypes.DungeonKeep:
                        case DFRegion.LocationTypes.DungeonLabyrinth:
                        case DFRegion.LocationTypes.Graveyard:
                        case DFRegion.LocationTypes.Coven:
                            soundClip = (int)SoundClips.AmbientWindMoan;
                            break;
                        case DFRegion.LocationTypes.Tavern:
                        case DFRegion.LocationTypes.TownCity:
                        case DFRegion.LocationTypes.TownHamlet:
                        case DFRegion.LocationTypes.TownVillage:
                        case DFRegion.LocationTypes.HomeFarms:
                        case DFRegion.LocationTypes.HomeWealthy:
                        case DFRegion.LocationTypes.HomePoor:
                        case DFRegion.LocationTypes.ReligionTemple:
                        case DFRegion.LocationTypes.ReligionCult:
                            soundClip = (int)SoundClips.AmbientPeople1 + UnityEngine.Random.Range(0, 10);
                            break;
                        default:
                            break;
                    }
                    AmbientAudioSource.SetSound(soundClip, AudioPresets.OnDemand, 1);
                }
                else
                    AmbientAudioSource.SetSound((int)SoundClips.BirdCall1, AudioPresets.OnDemand, 1);
            }
            AmbientAudioSource.AudioSource.Play();
        }

        float GetWorldHeight(int X, int Y)
        {
            float worldHeight = 0;
            if (WorldOfDaggerfallTerrain != null)
            {
                worldHeight = WODTerrainHeightMap.GetPixel(X, WODTerrainHeightMap.height - Y).grayscale * 255;
            }
            else
                worldHeight = dfUnity.ContentReader.WoodsFileReader.GetHeightMapValue(X, Y);

            if (worldHeight < WaterLevel)
                worldHeight = 0;

            return worldHeight;
        }

        float GetAverageWorldHeight(int X, int Y, int range = 1)
        {
            float worldHeight = 0;
            if (WorldOfDaggerfallTerrain != null)
            {
                worldHeight = WODTerrainHeightMap.GetPixel(X, WODTerrainHeightMap.height - Y).grayscale * 255;
            }
            else
                worldHeight = dfUnity.ContentReader.WoodsFileReader.GetHeightMapValue(X, Y);

            //if (worldSmooth == 0 && worldHeight < WaterLevel)
            if (worldHeight < WaterLevel)
                    return 0;

            if (range == 0)
                return worldHeight;

            float height = 0;
            int count = 0;

            for (int x = -range; x < range + 1; x++)
            {
                int cellX = X + x;

                if (cellX < 0 || cellX > 1000)
                    continue;

                for (int y = -range; y < range + 1; y++)
                {
                    int cellY = Y + y;

                    if (cellY < 0 || cellY > 500)
                        continue;

                    float tempHeight = 0;
                    if (WorldOfDaggerfallTerrain != null)
                    {
                        tempHeight = WODTerrainHeightMap.GetPixel(cellX, WODTerrainHeightMap.height - cellY).grayscale * 255;
                    }
                    else
                        tempHeight = dfUnity.ContentReader.WoodsFileReader.GetHeightMapValue(cellX, cellY);

                    //if (worldSmooth == 0 && tempHeight < WaterLevel)
                    if (tempHeight < WaterLevel)
                        tempHeight = 0;

                    height += tempHeight;
                    count++;
                }
            }

            worldHeight = height / count;

            //if (worldSmooth == 0 && worldHeight < WaterLevel)
            if (worldHeight < WaterLevel)
                return 0;

            return worldHeight;
        }

        public int GetPixelColorIndex(DFRegion.LocationTypes locationType)
        {
            int index = -1;
            switch (locationType)
            {
                case DFRegion.LocationTypes.DungeonLabyrinth:
                    index = 0;
                    break;
                case DFRegion.LocationTypes.DungeonKeep:
                    index = 1;
                    break;
                case DFRegion.LocationTypes.DungeonRuin:
                    index = 2;
                    break;
                case DFRegion.LocationTypes.Graveyard:
                    index = 3;
                    break;
                case DFRegion.LocationTypes.Coven:
                    index = 4;
                    break;
                case DFRegion.LocationTypes.HomeFarms:
                    index = 5;
                    break;
                case DFRegion.LocationTypes.HomeWealthy:
                    index = 6;
                    break;
                case DFRegion.LocationTypes.HomePoor:
                    index = 7;
                    break;
                case DFRegion.LocationTypes.HomeYourShips:
                    break;
                case DFRegion.LocationTypes.ReligionTemple:
                    index = 8;
                    break;
                case DFRegion.LocationTypes.ReligionCult:
                    index = 9;
                    break;
                case DFRegion.LocationTypes.Tavern:
                    index = 10;
                    break;
                case DFRegion.LocationTypes.TownCity:
                    index = 11;
                    break;
                case DFRegion.LocationTypes.TownHamlet:
                    index = 12;
                    break;
                case DFRegion.LocationTypes.TownVillage:
                    index = 13;
                    break;
                default:
                    break;
            }

            /*if (index < 0)
                return index;
            else if (index < 5 && filterDungeons)
                index = -1;
            else if (index > 4 && index < 8 && filterHomes)
                index = -1;
            else if (index > 7 && index < 10 && filterTemples)
                index = -1;
            else if (index > 9 && index < 14 && filterTowns)
                index = -1;*/

            return index;
        }

        void CreateChunk(int index)
        {
            int chunkWidth = 251;
            int chunkHeight = 251;
            float edgeLength = 1f;

            int offsetX = 0;
            int offsetY = 0;

            if (index == 7)
            {
                offsetX = 750;
                offsetY = 250;
            }
            else if (index == 6)
            {
                offsetX = 500;
                offsetY = 250;
            }
            else if (index == 5)
            {
                offsetX = 250;
                offsetY = 250;
            }
            else if (index == 4)
            {
                offsetY = 250;
            }
            else if (index == 3)
            {
                offsetX = 750;
            }
            else if (index == 2)
            {
                offsetX = 500;
            }
            else if (index == 1)
            {
                offsetX = 250;
            }

            //grab heightmap and generate mesh
            List<Vector3> verts = new List<Vector3>();
            //List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector2> uvs_mask = new List<Vector2>();
            List<int> tris = new List<int>();

            Texture2D textureMain = new Texture2D(chunkWidth, chunkHeight);
            Texture2D textureSub = new Texture2D(chunkWidth*3, chunkHeight*3);
            Texture2D textureHeight = new Texture2D(chunkWidth, chunkHeight);

            //float heightScale = (WorldOfDaggerfallTerrain != null ? 0.01f : 0.05f) * heightScaleMod;
            float heightScale = 0.01f * heightScaleMod;

            //Bottom left section of the map, other sections are similar
            for (int x = 0; x < chunkWidth; x++)
            {
                for (int y = 0; y < chunkHeight; y++)
                {
                    int X = x + offsetX;
                    int Y = y + offsetY;

                    //Debug.Log("current cell is " + X.ToString() + ", " + Y.ToString());

                    int start = verts.Count;

                    heightMap[X, Y] = GetWorldHeight(X, Y);
                    heightMapSmooth[X, Y] = GetAverageWorldHeight(X, Y, worldSmoothRange);

                    //Add each new vertex in the plane
                    //verts.Add(new Vector3((float)X * edgeLength, worldHeight * heightScale, (float)Y * -edgeLength));
                    //verts.Add(new Vector3((float)X * edgeLength, worldHeightSmooth * heightScale, (float)Y * -edgeLength));
                    if (IsWater(X, Y) || worldSmooth == 0)
                        verts.Add(new Vector3((float)X * edgeLength, heightMap[X, Y] * heightScale, (float)Y * -edgeLength));
                    else
                        verts.Add(new Vector3((float)X * edgeLength, heightMapSmooth[X, Y] * heightScale, (float)Y * -edgeLength));

                    uvs.Add(new Vector2(x, y));
                    uvs_mask.Add(new Vector2((float)x / chunkWidth, (float)y / chunkHeight));

                    //coloring
                    Color colorMain = Color.green;
                    colorMain.a = 0;

                    int worldClimate = dfUnity.ContentReader.MapFileReader.GetClimateIndex(X, Y);

                    //main splat
                    if (IsWater(X, Y))
                    {
                        /*colorMain = Color.black;
                        colorMain.a = 0;*/
                        if (!IsCoast(X, Y))
                        {
                            colorMain = Color.black;
                            colorMain.a = 0;
                        }
                    }
                    else if (!IsBeach(X, Y))
                    {
                        if (worldClimate == (int)MapsFile.Climates.Woodlands)
                        {
                            if (UnityEngine.Random.value < 0.1f)
                                colorMain = Color.blue;
                            else
                                colorMain = Color.red;
                        }
                        else if (worldClimate == (int)MapsFile.Climates.Desert || worldClimate == (int)MapsFile.Climates.Desert2)
                            colorMain = Color.green;
                        else if (worldClimate == (int)MapsFile.Climates.Mountain)
                        {
                            //half grass, half mountain
                            //if ((x % 2 == 0 && y % 2 != 0) || (x % 2 != 0 && y % 2 == 0))
                            if (UnityEngine.Random.value < 0.2f)
                                colorMain = Color.red;
                            else
                                colorMain = Color.blue;
                            //all mountain
                            //color = Color.blue;
                        }
                        else if (worldClimate == (int)MapsFile.Climates.MountainWoods)
                        {
                            //half grass, half mountain
                            //if ((x % 2 == 0 && y % 2 != 0) || (x % 2 != 0 && y % 2 == 0))
                            if (UnityEngine.Random.value < 0.4f)
                                colorMain = Color.red;
                            else
                                colorMain = Color.blue;
                        }

                        colorMain.a = 0;

                        if (worldClimate == (int)MapsFile.Climates.Rainforest)
                        {
                            //half mountain, half swamp
                            if (UnityEngine.Random.value < 0.5f)
                            {
                                colorMain = Color.blue;
                                colorMain.a = 0;

                            }
                            else
                            {
                                colorMain = Color.blue;
                                colorMain.a = 1;
                            }
                        }
                        else if (worldClimate == (int)MapsFile.Climates.Swamp)
                        {
                            //all swamp
                            colorMain = Color.blue;
                            colorMain.a = 1;
                        }
                        else if (worldClimate == (int)MapsFile.Climates.HauntedWoodlands)
                        {
                            //half grass, half swamp
                            if (UnityEngine.Random.value < 0.2f)
                            {
                                colorMain = Color.red;
                                colorMain.a = 0;

                            }
                            else
                            {
                                colorMain = Color.red;
                                colorMain.a = 1;
                            }
                        }
                        else if (worldClimate == (int)MapsFile.Climates.Subtropical)
                        {
                            //half grass, half sand
                            //if ((x % 2 == 0 && y % 2 != 0) || (x % 2 != 0 && y % 2 == 0))

                            //some swamp, mostly sand
                            if (UnityEngine.Random.value < 0.1f)
                            {
                                colorMain = Color.green;
                                colorMain.a = 1;
                            }
                            else
                            {
                                colorMain = Color.green;
                                colorMain.a = 0;
                            }
                        }

                        //color = Color.Lerp(ground, Color.blue, (float)worldHeightSmooth / 60);
                    }

                    //if basic roads are supported do them here
                    int road = Compatibility.BasicRoadsUtils.GetRoadType(X, Y);
                    //Redo this in a second splat
                    Color[,] colorSub = new Color[3, 3];

                    if (BasicRoads != null && road != 0)
                    {
                        List<Vector3> directions = Compatibility.BasicRoadsUtils.GetRoadDirections(Compatibility.BasicRoadsUtils.GetRoadData(X,Y));
                        if (road == 2)
                        {
                            //track
                            if (worldClimate == (int)MapsFile.Climates.Desert || worldClimate == (int)MapsFile.Climates.Desert2 || worldClimate == (int)MapsFile.Climates.Subtropical)
                            {
                                //desert
                                //center
                                colorSub[1, 1] = Color.blue;
                                colorSub[1, 1].a = 1;

                                //cardinals
                                if (directions.Contains(Vector3.up))
                                {
                                    colorSub[1, 0] = Color.blue;
                                    colorSub[1, 0].a = 1;
                                }
                                if (directions.Contains(Vector3.down))
                                {
                                    colorSub[1, 2] = Color.blue;
                                    colorSub[1, 2].a = 1;
                                }
                                if (directions.Contains(Vector3.right))
                                {
                                    colorSub[2, 1] = Color.blue;
                                    colorSub[2, 1].a = 1;
                                }
                                if (directions.Contains(Vector3.left))
                                {
                                    colorSub[0, 1] = Color.blue;
                                    colorSub[0, 1].a = 1;
                                }

                                //diagonals
                                if (directions.Contains(Vector3.up + Vector3.right))
                                {
                                    colorSub[2, 0] = Color.blue;
                                    colorSub[2, 0].a = 1;
                                }
                                if (directions.Contains(Vector3.up + Vector3.left))
                                {
                                    colorSub[0, 0] = Color.blue;
                                    colorSub[0, 0].a = 1;
                                }
                                if (directions.Contains(Vector3.down + Vector3.right))
                                {
                                    colorSub[2, 2] = Color.blue;
                                    colorSub[2, 2].a = 1;
                                }
                                if (directions.Contains(Vector3.down + Vector3.left))
                                {
                                    colorSub[0, 2] = Color.blue;
                                    colorSub[0, 2].a = 1;
                                }
                            }
                            else
                            {
                                //temperate
                                //center
                                colorSub[1, 1] = Color.green;
                                colorSub[1, 1].a = 0;

                                //cardinals
                                if (directions.Contains(Vector3.up))
                                {
                                    colorSub[1, 0] = Color.green;
                                    colorSub[1, 0].a = 0;
                                }
                                if (directions.Contains(Vector3.down))
                                {
                                    colorSub[1, 2] = Color.green;
                                    colorSub[1, 2].a = 0;
                                }
                                if (directions.Contains(Vector3.right))
                                {
                                    colorSub[2, 1] = Color.green;
                                    colorSub[2, 1].a = 0;
                                }
                                if (directions.Contains(Vector3.left))
                                {
                                    colorSub[0, 1] = Color.green;
                                    colorSub[0, 1].a = 0;
                                }

                                //diagonals
                                if (directions.Contains(Vector3.up + Vector3.right))
                                {
                                    colorSub[2, 0] = Color.green;
                                    colorSub[2, 0].a = 0;
                                }
                                if (directions.Contains(Vector3.up + Vector3.left))
                                {
                                    colorSub[0, 0] = Color.green;
                                    colorSub[0, 0].a = 0;
                                }
                                if (directions.Contains(Vector3.down + Vector3.right))
                                {
                                    colorSub[2, 2] = Color.green;
                                    colorSub[2, 2].a = 0;
                                }
                                if (directions.Contains(Vector3.down + Vector3.left))
                                {
                                    colorSub[0, 2] = Color.green;
                                    colorSub[0, 2].a = 0;
                                }

                            }
                        }
                        else
                        {
                            //road
                            if (worldClimate == (int)MapsFile.Climates.Desert || worldClimate == (int)MapsFile.Climates.Desert2 || worldClimate == (int)MapsFile.Climates.Subtropical)
                            {
                                //desert
                                //center
                                colorSub[1, 1] = Color.blue;
                                colorSub[1, 1].a = 0;

                                //cardinals
                                if (directions.Contains(Vector3.up))
                                {
                                    colorSub[1, 0] = Color.blue;
                                    colorSub[1, 0].a = 0;
                                }
                                if (directions.Contains(Vector3.down))
                                {
                                    colorSub[1, 2] = Color.blue;
                                    colorSub[1, 2].a = 0;
                                }
                                if (directions.Contains(Vector3.right))
                                {
                                    colorSub[2, 1] = Color.blue;
                                    colorSub[2, 1].a = 0;
                                }
                                if (directions.Contains(Vector3.left))
                                {
                                    colorSub[0, 1] = Color.blue;
                                    colorSub[0, 1].a = 0;
                                }

                                //diagonals
                                if (directions.Contains(Vector3.up + Vector3.right))
                                {
                                    colorSub[2, 0] = Color.blue;
                                    colorSub[2, 0].a = 0;
                                }
                                if (directions.Contains(Vector3.up + Vector3.left))
                                {
                                    colorSub[0, 0] = Color.blue;
                                    colorSub[0, 0].a = 0;
                                }
                                if (directions.Contains(Vector3.down + Vector3.right))
                                {
                                    colorSub[2, 2] = Color.blue;
                                    colorSub[2, 2].a = 0;
                                }
                                if (directions.Contains(Vector3.down + Vector3.left))
                                {
                                    colorSub[0, 2] = Color.blue;
                                    colorSub[0, 2].a = 0;
                                }
                            }
                            else
                            {
                                //temperate
                                //center
                                colorSub[1, 1] = Color.red;
                                colorSub[1, 1].a = 0;

                                //cardinals
                                if (directions.Contains(Vector3.up))
                                {
                                    colorSub[1, 0] = Color.red;
                                    colorSub[1, 0].a = 0;
                                }
                                if (directions.Contains(Vector3.down))
                                {
                                    colorSub[1, 2] = Color.red;
                                    colorSub[1, 2].a = 0;
                                }
                                if (directions.Contains(Vector3.right))
                                {
                                    colorSub[2, 1] = Color.red;
                                    colorSub[2, 1].a = 0;
                                }
                                if (directions.Contains(Vector3.left))
                                {
                                    colorSub[0, 1] = Color.red;
                                    colorSub[0, 1].a = 0;
                                }

                                //diagonals
                                if (directions.Contains(Vector3.up + Vector3.right))
                                {
                                    colorSub[2, 0] = Color.red;
                                    colorSub[2, 0].a = 0;
                                }
                                if (directions.Contains(Vector3.up + Vector3.left))
                                {
                                    colorSub[0, 0] = Color.red;
                                    colorSub[0, 0].a = 0;
                                }
                                if (directions.Contains(Vector3.down + Vector3.right))
                                {
                                    colorSub[2, 2] = Color.red;
                                    colorSub[2, 2].a = 0;
                                }
                                if (directions.Contains(Vector3.down + Vector3.left))
                                {
                                    colorSub[0, 2] = Color.red;
                                    colorSub[0, 2].a = 0;
                                }

                            }
                        }
                    }
                    else
                    {
                        colorSub[0, 0] = Color.black;
                        colorSub[0, 0].a = 0;
                        colorSub[0, 1] = Color.black;
                        colorSub[0, 1].a = 0;
                        colorSub[0, 2] = Color.black;
                        colorSub[0, 2].a = 0;
                        colorSub[1, 0] = Color.black;
                        colorSub[1, 0].a = 0;
                        colorSub[1, 1] = Color.black;
                        colorSub[1, 1].a = 0;
                        colorSub[1, 2] = Color.black;
                        colorSub[1, 2].a = 0;
                        colorSub[2, 0] = Color.black;
                        colorSub[2, 0].a = 0;
                        colorSub[2, 1] = Color.black;
                        colorSub[2, 1].a = 0;
                        colorSub[2, 2] = Color.black;
                        colorSub[2, 2].a = 0;
                    }

                    Color colorHeight = Color.Lerp(Color.black, Color.white, heightMapSmooth[X,Y] / ceilingLevel);
                    if (WorldOfDaggerfallTerrain == null && UnityEngine.Random.value > 0.5f)
                        colorHeight *= 0.5f;

                    textureMain.SetPixel(x, y, colorMain);

                    for (int subX = -2; subX < 1; subX++)
                    {
                        for (int subY = -2; subY < 1; subY++)
                        {
                            textureSub.SetPixel((x * 3) + subX + 2, (y * 3) + subY + 2, colorSub[subX+2,subY+2]);
                        }
                    }

                    textureHeight.SetPixel(x, y, colorHeight);

                    if (x == 0 || y == 0)
                    {
                        //y += 4;
                        continue;
                    }

                    tris.Add(start);
                    tris.Add(start - chunkWidth - 1);
                    tris.Add(start - 1);

                    tris.Add(start);
                    tris.Add(start - chunkWidth);
                    tris.Add(start - chunkWidth - 1);

                    //check location
                    ContentReader.MapSummary summary;
                    if (dfUnity.ContentReader.HasLocation(X, Y, out summary) && summary.LocationType != DFRegion.LocationTypes.HomeYourShips)
                    {
                        int modelID = 0;
                        switch (summary.LocationType)
                        {
                            case DFRegion.LocationTypes.DungeonLabyrinth:
                                modelID = 42001;
                                break;
                            case DFRegion.LocationTypes.DungeonKeep:
                                modelID = 860;
                                break;
                            case DFRegion.LocationTypes.DungeonRuin:
                                modelID = 43509;
                                break;
                            case DFRegion.LocationTypes.Graveyard:
                                modelID = 43137;
                                break;
                            case DFRegion.LocationTypes.Coven:
                                modelID = 41216;
                                break;
                            case DFRegion.LocationTypes.HomeFarms:
                                modelID = 323;
                                break;
                            case DFRegion.LocationTypes.HomeWealthy:
                                modelID = 822;
                                break;
                            case DFRegion.LocationTypes.HomePoor:
                                modelID = 329;
                                break;
                            case DFRegion.LocationTypes.ReligionTemple:
                                modelID = 362;
                                break;
                            case DFRegion.LocationTypes.ReligionCult:
                                modelID = 74088;
                                break;
                            case DFRegion.LocationTypes.Tavern:
                                modelID = 249;
                                break;
                            case DFRegion.LocationTypes.TownCity:
                                modelID = 509;
                                break;
                            case DFRegion.LocationTypes.TownHamlet:
                                modelID = 507;
                                break;
                            case DFRegion.LocationTypes.TownVillage:
                                modelID = 324;
                                break;
                            default:
                                break;
                        }

                        //GameObject LocationObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        //LocationObject.transform.SetParent(ParentObject.transform);
                        //LocationObject.GetComponent<Collider>().enabled = false;
                        GameObject LocationObject = GameObjectHelper.CreateDaggerfallMeshGameObject((uint)modelID, ParentObject.transform, true, null, true, false);
                        LocationObject.transform.localScale = Vector3.one * 0.025f;
                        LocationObject.transform.localPosition = new Vector3(X, heightMapSmooth[X,Y] * heightScale, -Y);

                        LocationObjects.Add(LocationObject);
                        locationObjectsMap[X,Y] = LocationObject;

                        /*DFLocation location;
                        if (DaggerfallUnity.Instance.ContentReader.GetLocation(summary.RegionIndex, summary.MapIndex, out location))
                        {
                            locationNamesMap[X, Y] = location.Name;
                        }*/

                        LocationObject.SetActive(false);

                        if (summary.LocationType == DFRegion.LocationTypes.Coven || summary.DungeonType != DFRegion.DungeonTypes.NoDungeon)
                        {
                            LocationDungeonObjects.Add(LocationObject);
                        }

                        if (HasPort(summary.MapID))
                        {
                            LocationPortObjects.Add(LocationObject);

                            //add a boat model to the nearest water tile
                            /*GameObject LocationPortMesh = GameObjectHelper.CreateDaggerfallMeshGameObject(910, ParentObject.transform, false, null, true);
                            Vector2 waterPixel = GetClosestWater(new Vector2(LocationObject.transform.position.x,-LocationObject.transform.position.z));
                            LocationPortMesh.transform.localPosition = new Vector3(waterPixel.x,WaterLevel,waterPixel.y);
                            LocationPortMesh.transform.localScale = Vector3.one * 0.02f;
                            LocationPortMesh.transform.SetParent(LocationObject.transform);*/
                        }

                        /*//set color
                        int colorIndex = GetPixelColorIndex(summary.LocationType);
                        if (colorIndex >= 0)
                        {
                            MeshRenderer renderer = LocationObject.GetComponent<MeshRenderer>();
                            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                            propertyBlock.SetColor("_Color", locationPixelColors[colorIndex]);
                            renderer.SetPropertyBlock(propertyBlock);
                        }*/
                    }
                    //y += 4;
                }
                //x += 4;
            }

            textureMain.Apply();
            textureMain.wrapMode = TextureWrapMode.Clamp;
            textureMain.filterMode = FilterMode.Trilinear;
            //texture = FastBlur(texture, 1, 3);
            //texture = ResizeTexture(texture, texture.width * 2, texture.height * 2);

            textureSub.Apply();
            //textureSub = ResizeTexture(textureSub, textureSub.width * 2, textureSub.height * 2);
            //textureSub = FastBlur(textureSub, 2, 2);
            textureSub.wrapMode = TextureWrapMode.Clamp;
            textureSub.filterMode = FilterMode.Trilinear;

            textureHeight.Apply();
            textureHeight = FastBlur(textureHeight, 2, 2);
            textureHeight.wrapMode = TextureWrapMode.Clamp;
            textureHeight.filterMode = FilterMode.Trilinear;

            GameObject plane = new GameObject("Chunk" + index.ToString()); //Create GO and add necessary components
            ChunkObjects.Add(plane);
            plane.transform.SetParent(ChunkParentObject.transform);
            MeshFilter meshFilter = plane.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = plane.AddComponent<MeshRenderer>();

            Mesh procMesh = new Mesh();
            procMesh.SetVertices(verts); //Assign verts, uvs, and tris to the mesh
            procMesh.SetUVs(0, uvs);
            procMesh.SetUVs(1, uvs_mask);
            procMesh.SetTriangles(tris, 0);
            //procMesh.SetNormals(normals); //Determines which way the triangles are facing
            procMesh.RecalculateNormals(); //Determines which way the triangles are facing
            meshFilter.mesh = procMesh; //Assign Mesh object to MeshFilter
            meshFilter.mesh.Optimize();

            MeshCollider meshCollider = plane.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = procMesh;

            meshRenderer.material = new Material(splatShader);
            meshRenderer.material.SetTexture("_MainTex1", waterTexture);
            meshRenderer.material.SetTexture("_MainTex2", grassTexture);
            meshRenderer.material.SetTextureScale("_MainTex2", Vector2.one * 10f);
            meshRenderer.material.SetTexture("_MainTex3", sandTexture);
            meshRenderer.material.SetTextureScale("_MainTex3", Vector2.one * 20f);
            meshRenderer.material.SetTexture("_MainTex4", mountainTexture);
            meshRenderer.material.SetTexture("_MainTex5", swampTexture);
            meshRenderer.material.SetTexture("_MainMask", textureMain);

            meshRenderer.material.SetTexture("_SubTex1", roadTemperate);
            meshRenderer.material.SetTexture("_SubTex2", trackTemperate);
            meshRenderer.material.SetTexture("_SubTex3", roadDesert);
            meshRenderer.material.SetTexture("_SubTex4", trackDesert);
            meshRenderer.material.SetTexture("_SubMask", textureSub);

            meshRenderer.material.SetTexture("_HeightTex", stoneTexture);
            meshRenderer.material.SetTexture("_HeightMask", textureHeight);
        }

        void AdjustLocations(bool start = false)
        {
            //place object on actual ground
            foreach (GameObject LocationObject in LocationObjects)
            {
                if (start)
                {
                    Ray ray = new Ray(LocationObject.transform.position + (Vector3.up * 5), Vector3.down);
                    RaycastHit hit = new RaycastHit();
                    float yPos = LocationObject.transform.position.y;
                    if (Physics.Raycast(ray, out hit, 100))
                    {
                        yPos = hit.point.y;
                    }
                    LocationObject.transform.position = new Vector3(LocationObject.transform.position.x, yPos, LocationObject.transform.position.z);

                    //shift mesh above pivot
                    Bounds bounds = LocationObject.GetComponent<MeshRenderer>().bounds;
                    Vector3 offset = bounds.center - LocationObject.transform.position;
                    LocationObject.transform.position = LocationObject.transform.position - offset + (Vector3.up * bounds.extents.y * 1);
                    LocationObject.transform.up = hit.normal;

                    //randomly rotate
                    LocationObject.transform.Rotate(Vector3.up, UnityEngine.Random.Range(0, 8) * 45f,Space.Self);

                    //add ship to ports
                    if (LocationPortObjects.Contains(LocationObject))
                    {
                        //GameObject LocationPortMesh = GameObjectHelper.CreateDaggerfallMeshGameObject(910, ParentObject.transform, false, null, true);
                        GameObject LocationPortMesh = GameObjectHelper.CreateDaggerfallMeshGameObject(41507, ParentObject.transform, false, null, true);
                        Vector2 waterPixel = GetClosestWater(new Vector2(LocationObject.transform.position.x, -LocationObject.transform.position.z));
                        LocationPortMesh.transform.localPosition = new Vector3(waterPixel.x, heightMapSmooth[Mathf.RoundToInt(LocationObject.transform.position.x), Mathf.RoundToInt(-LocationObject.transform.position.z)], -waterPixel.y);
                        ray.origin = LocationPortMesh.transform.position+(Vector3.up * 5);
                        if (Physics.Raycast(ray, out hit, 100))
                        {
                            yPos = hit.point.y;
                            LocationPortMesh.transform.position = new Vector3(LocationPortMesh.transform.position.x, hit.point.y+0.1f, LocationPortMesh.transform.position.z);
                        }
                        LocationPortMesh.transform.localScale = Vector3.one * 0.02f;
                        //LocationPortMesh.transform.LookAt(new Vector3(LocationObject.transform.position.x, LocationPortMesh.transform.position.y, LocationObject.transform.position.z));
                        LocationPortMesh.transform.right = (new Vector3(LocationObject.transform.position.x, LocationPortMesh.transform.position.y, LocationObject.transform.position.z)-LocationPortMesh.transform.position);
                        LocationPortMesh.transform.SetParent(LocationObject.transform);
                    }
                }
                LocationObject.SetActive(false);
            }
        }

        void UpdateLocations()
        {
            int X = 0;
            int Y = 0;
            ContentReader.MapSummary summary;
            DFLocation location;
            bool discovered = false;
            bool show = false;
            float distance = 0;
            int range = 10;
            float discoveryRadius = sightRadius / 2f;
            for (int x = -range; x <= range; x++)
            {
                X = (int)PlayerObject.transform.localPosition.x + x;

                if (X < 0 || X > 1000)
                    continue;

                for (int y = -range; y <= range; y++)
                {
                    Y = (int)-PlayerObject.transform.localPosition.z + y;

                    if (Y < 0 || Y > 500)
                        continue;

                    if (locationObjectsMap[X, Y] != null)
                    {
                        show = false;
                        distance = Vector3.Distance(PlayerObject.transform.localPosition, locationObjectsMap[X, Y].transform.localPosition);
                        if (distance <= sightRadius)
                        {
                            if (!LocationDungeonObjects.Contains(locationObjectsMap[X, Y]) || LocationDungeonObjectsDiscovered.Contains(locationObjectsMap[X, Y]))
                                show = true;
                            else
                            {
                                if (dfUnity.ContentReader.HasLocation(X, Y, out summary))
                                {
                                    //check if discovered
                                    discovered = GameManager.Instance.PlayerGPS.HasDiscoveredLocation(summary.ID);
                                    if (!discovered && distance <= discoveryRadius)
                                    {
                                        //if not discovered, discover locations if within discovery radius
                                        if (WorldDataReplacement.GetDFLocationReplacementData(summary.RegionIndex, summary.MapIndex, out location))
                                        {
                                            //MapWindowPanel.Instance.logLabel.AddText("Discovered " + location.Name + " in " + location.RegionName);
                                            MapWindowPanel.Instance.logLabel.AddText("Discovered " + location.Name);
                                            //GameManager.Instance.PlayerGPS.DiscoverLocation(location.RegionName, location.Name);
                                            DiscoverLocation(location);
                                            discovered = true;
                                        }
                                        else if (dfUnity.ContentReader.GetLocation(summary.RegionIndex, summary.MapIndex, out location))
                                        {
                                            //MapWindowPanel.Instance.logLabel.AddText("Discovered " + location.Name + " in " + location.RegionName);
                                            MapWindowPanel.Instance.logLabel.AddText("Discovered " + location.Name);
                                            //GameManager.Instance.PlayerGPS.DiscoverLocation(location.RegionName, location.Name);
                                            DiscoverLocation(location);
                                            discovered = true;
                                        }
                                        /*if (dfUnity.ContentReader.GetLocation(summary.RegionIndex, summary.MapIndex, out location))
                                        {
                                            MapWindowPanel.Instance.logLabel.AddText("Discovered " + location.Name + " in " + location.RegionName);
                                            //GameManager.Instance.PlayerGPS.DiscoverLocation(location.RegionName, location.Name);
                                            DiscoverLocation(location);
                                            discovered = true;
                                        }*/
                                    }

                                    show = discovered;

                                    if (show)
                                        LocationDungeonObjectsDiscovered.Add(locationObjectsMap[X, Y]);
                                }
                            }
                        }

                        if (locationObjectsMap[X, Y].activeSelf != show)
                            locationObjectsMap[X, Y].SetActive(show);
                    }
                }
            }
        }

        FieldInfo discoveredLocationsField;
        Dictionary<int, DiscoveredLocation> discoveredLocations;

        void DiscoverLocation(DFLocation location)
        {
            int mapPixelID = MapsFile.GetMapPixelIDFromLongitudeLatitude((int)location.MapTableData.Longitude, location.MapTableData.Latitude);
            if (GameManager.Instance.PlayerGPS.HasDiscoveredLocation(mapPixelID))
                return;

            // Add to discovered locations dict
            DiscoveredLocation dl = new DiscoveredLocation();
            dl.mapID = location.MapTableData.MapId;
            dl.mapPixelID = mapPixelID;
            dl.regionName = location.RegionName;
            dl.locationName = location.Name;

            if (discoveredLocationsField == null)
            {
                Debug.Log("WORLDWALKING - DICTIONARY FIELD IS NULL! ATTEMPTING REFLECTION!");
                discoveredLocationsField = GameManager.Instance.PlayerGPS.GetType().GetField("discoveredLocations", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            else
                Debug.Log("WORLDWALKING - DICTIONARY FIELD EXISTS!");

            discoveredLocations = (Dictionary<int, DiscoveredLocation>)discoveredLocationsField.GetValue(GameManager.Instance.PlayerGPS);
            discoveredLocations.Add(mapPixelID,dl);
        }

        Texture2D ResizeTexture(Texture2D sourceTexture, int targetWidth, int targetHeight)
        {
            Texture2D newTexture = new Texture2D(targetWidth, targetHeight, sourceTexture.format, false);
            float scaleX = (float)sourceTexture.width / targetWidth;
            float scaleY = (float)sourceTexture.height / targetHeight;

            Color[] resizedColours = new Color[targetWidth * targetHeight];

            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    int index = (y * targetWidth) + x;
                    float sourceX = x * scaleX;
                    float sourceY = y * scaleY;
                    Color newColor = sourceTexture.GetPixelBilinear(sourceX / sourceTexture.width, sourceY / sourceTexture.height);
                    resizedColours[index] = newColor;
                }
            }

            newTexture.SetPixels(resizedColours);
            newTexture.Apply();
            //return FastBlur(newTexture,3,3);
            return newTexture;
        }

        //fast blur by elecman
        Texture2D FastBlur(Texture2D image, int radius, int iterations)
        {

            Texture2D tex = image;

            for (var i = 0; i < iterations; i++)
            {

                tex = BlurImage(tex, radius, true);
                tex = BlurImage(tex, radius, false);
            }

            return tex;
        }

        Texture2D BlurImage(Texture2D image, int blurSize, bool horizontal)
        {

            Texture2D blurred = new Texture2D(image.width, image.height);
            int _W = image.width;
            int _H = image.height;
            int xx, yy, x, y;

            if (horizontal)
            {
                for (yy = 0; yy < _H; yy++)
                {
                    for (xx = 0; xx < _W; xx++)
                    {
                        ResetPixel();

                        //Right side of pixel
                        for (x = xx; (x < xx + blurSize &&  x < _W) ; x++)
                        {
                            AddPixel(image.GetPixel(x, yy));
                        }

                        //Left side of pixel
                        for (x = xx; (x > xx - blurSize && x > 0) ; x--)
                        {
                            AddPixel(image.GetPixel(x, yy));
                        }

                        CalcPixel();

                        for (x = xx; x < xx + blurSize && x < _W; x++)
                        {
                            blurred.SetPixel(x, yy, new Color(avgR, avgG, avgB, avgA));
                        }
                    }
                }
            }
            else {

                for (xx = 0; xx<_W; xx++) {

                    for (yy = 0; yy<_H; yy++) {

                        ResetPixel();

                        //Over pixel
                        for (y = yy; (y<yy + blurSize && y < _H); y++)
                        {
                            AddPixel(image.GetPixel(xx, y));
                        }

                        //Under pixel
                        for (y = yy; (y > yy - blurSize && y > 0) ; y--)
                        {
                            AddPixel(image.GetPixel(xx, y));
                        }

                        CalcPixel();

                        for (y = yy; y < yy + blurSize && y < _H; y++)
                        {
                            blurred.SetPixel(xx, y, new Color(avgR, avgG, avgB, avgA));
                        }
                    }
                }
            }

            blurred.Apply();
            return blurred;
        }

        void AddPixel(Color pixel)
        {

            avgR += pixel.r;
            avgG += pixel.g;
            avgB += pixel.b;
            avgA += pixel.a;
            blurPixelCount++;
        }

        void ResetPixel()
        {
            avgR = 0.0f;
            avgG = 0.0f;
            avgB = 0.0f;
            avgA = 0.0f;
            blurPixelCount = 0;
        }

        void CalcPixel()
        {
            avgR = avgR / blurPixelCount;
            avgG = avgG / blurPixelCount;
            avgB = avgB / blurPixelCount;
            avgA = avgA / blurPixelCount;
        }

        //Clone of Travel Options' port detection code. By Hazelnut.
        public static int MaskMapId(int mapId)
        {
            return mapId & 0x000FFFFF;
        }

        public static bool HasPort(ContentReader.MapSummary mapSummary)
        {
            return HasPort(mapSummary.ID);
        }

        public static bool HasPort(DFRegion.RegionMapTable mapTable)
        {
            return HasPort(mapTable.MapId);
        }

        public static bool HasPort(int mapId)
        {
            return Array.Exists(portLocationIds, n => n == MaskMapId(mapId));
        }

        public static readonly int[] portLocationIds = {
            443401, 280614, 285597, 485856, 86496, 137544, 139547, 143535, 143542, 149513,
            150625, 158629, 162631, 162646, 164648, 166644, 168652, 169640, 170654, 178663,
            182685, 188727, 192653, 195681, 201654, 202646, 203671, 225685, 234712, 22763,
            184263, 189097, 192248, 194242, 194279, 196245, 199102, 199111, 201125, 210132,
            212138, 213207, 226205, 228209, 235146, 236143, 239139, 239144, 239146, 241140,
            91170, 93168, 96150, 96212, 107137, 109167, 325404, 325406, 328409, 341392,
            342399, 343397, 344387, 345378, 345383, 346398, 347375, 348372, 348396, 350370,
            351392, 352369, 353387, 354364, 361382, 364381, 369385, 369388, 370441, 372411,
            372439, 373407, 373415, 373422, 373425, 373427, 373429, 374419, 120375, 121377,
            148460, 148463, 150459, 158499, 168357, 172455, 187406, 192361, 193358, 193366,
            195353, 195361, 197366, 200356, 277751, 278764, 279644, 279697, 279749, 279754,
            279766, 280747, 281656, 281658, 281663, 281699, 281702, 281704, 281741, 281770,
            282712, 282724, 282728, 282731, 282734, 282737, 283687, 283707, 284685, 285682,
            286674, 289737, 292695, 293697, 310763, 311766, 194855, 195860, 223828, 225840,
            229847, 236854, 240841, 242856, 243846, 244859, 247836, 249839, 249861, 249866,
            250875, 255876, 256887, 256900, 257889, 258892, 258907, 261923, 261925, 262907,
            262931, 264900, 264902, 264940, 264942, 265956, 266964, 273975, 5222, 5224,
            11215, 14210, 23240, 35152, 49219, 157795, 181800, 187807, 193793, 210785,
            215821, 216791, 112707, 133701, 133718, 134711, 135713, 135717, 135735, 138745,
            140758, 140760, 148782, 151788, 83668, 125675, 111631, 111645, 112652, 113637,
            113646, 113649, 115622, 118573, 134553, 137558, 137561, 137593, 138583, 139588,
            145609, 146607, 147614, 148589, 151591, 152587, 56637, 35449, 41483, 121473,
            129449, 29347, 40361, 69406, 160305, 451180, 451186, 453173, 455174, 457179,
            458198, 460176, 461173, 463171, 468168, 468188, 473169, 474207, 476162, 476164,
            477177, 478159, 483153, 493144, 495141, 422217, 432218, 433205, 435202, 455199,
            459220, 405246, 405263, 406266, 407241, 408235, 408249, 417227, 393300, 397296,
            403279, 406276, 418291, 364449, 370446, 402451, 276583, 279596, 290582, 294569,
            295564, 296558, 297552, 305534, 308524, 308527, 308530, 309521, 312518, 313516,
            316550, 318514, 334515, 339496, 341496, 346475, 351470, 337704, 263832, 264825,
            269847, 269849, 276835, 277798, 278817, 278843, 279815, 283779, 283782, 287827,
            287829, 289866, 294842, 302839, 306854, 337914, 338912, 341916, 346918, 351919,
            354916, 357915, 357918, 361913, 363915, 364868, 370908, 379876, 379888, 380885,
            381881, 382879, 278962, 281872, 281969, 324981, 469891, 437653, 446471, 472431,
            480415, 217966, 100086, 121067, 123073, 144059, 75104, 77077, 83137, 86218,
            86334, 89333, 343439,
            // Extras allowing travel to:
            205676,     // "Isle of Balfiera", "Blackhead"
            278901,     // "Mournoth", "Zagoparia"
            263119,     // "Betony", "Whitefort"
            148062,     // "Tulune", "The Citadel of Hearthham"
            144059,     // "Tulune", "The Elyzanna Assembly"
            343439,     // "Cybiades", "Ruins of Cosh Hall"
            243846,     // Wayrest	Penwall Derry
            273975,     // Wayrest	Tunmont
            255884,     // Wayrest	Eastwold
            256887,     // Wayrest	Chardale
            262931,     // Wayrest	Longmore Field
            106061,     // Tulune	Midmont
            164072,     // Tulune  Lambrugh
            157092,     // Tulune	Gallocart
            296558,     // Tigonus Antelibuton
            297552,     // Tigonus Wadijerareg
            308527,     // Tigonus Kalureg
            376329,     // Sentinel	Pibuda
            359347,     // Sentinel	Zenuhno
            357347,     // Sentinel	Mji-Ij
            358355,     // Sentinel	Antelajda
            350370,     // Sentinel	Naresa
            347375,     // Sentinel	Jalonia
            343397,     // Sentinel Sentinel
            373407,     // Sentinel	Cudakasa
            373415,     // Sentinel	Bubumbaret
            327404,     // Sentinel	Bubissidata
            283779,     // Satakalaam	Tulajidax
            406266,     // Pothago	Berbajan
            42127,      // Northmoor	Gothcroft
            5229,       // Northmoor	Knightshope
            8219,       // Northmoor	Stokwall
            34162,      // Northmoor	Vanpath
            38152,      // Northmoor	Pencart
            45124,      // Northmoor	Burgcart Heath
            403279,     // Myrkwasa	Elissinia
            285898,     // Mournoth	Wadijilanis
            278962,     // Mournoth	Meseraara
            216791,     // Menevia	Chesterbrugh
            279754,     // Lainlyn	Kalunnunu
            281658,     // Lainlyn	Syrotubu
            286674,     // Lainlyn	Papiladisu
            281699,     // Lainlyn	Syrallao
            281702,     // Lainlyn	Pythohajer
            73079,      // Glenumbra Moors	Tambridge
            77077,      // Glenumbra Moors	Deerpath
            223207,     // Daggerfall	Westhead Moor
            199102,     // Daggerfall	Whitecroft
            201118,     // Daggerfall	Holwych
            213285,     // Daggerfall	Ripmore
            214207,     // Daggerfall	Copperfield Manor
            221160,     // Daggerfall	Fontborne
            225169,     // Daggerfall	Longwich End
            224178,     // Daggerfall	Wilderham
            217198,     // Daggerfall	Midbrugh
            214294,     // Daggerfall	Vanvale
            214297,     // Daggerfall	Blackcart Hollow
            239144,     // Daggerfall	Aldpath Hall
            236143,     // Daggerfall	Grimton
            139588,     // Bhoriane	Wartale
            113637,     // Bhoriane	Fontbridge
            112652,     // Bhoriane	Stokbrone
            255114,     // Betony	Kirkbeth Hamlet
            364449,     // Ayasofya Umbopala
            183401,     // Anticlere	Aldwall Rock
            175401,     // Anticlere	Crossleigh
            187406,     // Anticlere	Cathwold Heath
            195353,     // Anticlere	Vanwood Hollow
            193358,     // Anticlere	Ipspath
            197366,     // Anticlere	Wilderbury Rock
            133718,     // Alcaire	Cathborne
            140760,     // Alcaire	Wargate
            455174,     // Abibon-Gora	Papyrydai
            202333,  // Shalgora Aldbrugh
        };


        void WarmAshesEncounter()
        {
            //trigger random WA quest
            if (currentEncounterTemplate == null)
            {
                string questName = string.Empty;
                if (GameManager.Instance.PlayerGPS.CurrentLocation.Loaded)
                {
                    DFRegion.LocationTypes locationType = GameManager.Instance.PlayerGPS.CurrentLocationType;

                    switch (locationType)
                    {
                        case DFRegion.LocationTypes.DungeonKeep:
                            questName = QuestsKeep[UnityEngine.Random.Range(0, QuestsKeep.Length)];
                            break;
                        case DFRegion.LocationTypes.Graveyard:
                            questName = QuestsGraveyard[UnityEngine.Random.Range(0, QuestsGraveyard.Length)];
                            break;
                        case DFRegion.LocationTypes.TownCity:
                        case DFRegion.LocationTypes.TownHamlet:
                        case DFRegion.LocationTypes.TownVillage:
                        case DFRegion.LocationTypes.Tavern:
                        case DFRegion.LocationTypes.HomeWealthy:
                            questName = GetQuestCity();
                            break;
                        case DFRegion.LocationTypes.HomeFarms:
                        case DFRegion.LocationTypes.HomePoor:
                        case DFRegion.LocationTypes.ReligionTemple:
                        case DFRegion.LocationTypes.ReligionCult:
                            questName = QuestsWODFarm[UnityEngine.Random.Range(0, QuestsWODFarm.Length)];
                            break;
                        default:
                            questName = GetQuestWilderness();
                            break;
                    }
                }
                else
                {
                    //in the wilderness
                    questName = GetQuestWilderness();
                }

                if (questName != string.Empty)
                {
                    string[] args = new string[] { questName };
                    Wenzil.Console.Console.ExecuteCommand("startquest", args);
                }
            }
            else
            {
                if (currentEncounterTemplate.modMessageName != string.Empty)
                {
                    //call mod message times iterations
                    for (int i = 0; i < currentEncounterTemplate.iterations; i++)
                    {
                        ModManager.Instance.SendModMessage(WarmAshesWilderness.Title, currentEncounterTemplate.modMessageName);
                    }
                }
                else {
                    string[] args = new string[] { currentEncounterTemplate.questName };
                    Wenzil.Console.Console.ExecuteCommand("startquest", args);
                }

                if (currentEncounterTemplate.shipModel > -1 && !currentEncounterTemplate.questName.Contains("Ship") && !currentEncounterTemplate.modMessageName.Contains("Ship"))
                {
                    //spawn npc ship here

                    //place boat
                    Transform playerTransform = GameManager.Instance.PlayerObject.transform;
                    Vector3 shipPos = playerTransform.position + (playerTransform.right * -50);

                    //get height
                    uint modelID = 41506;

                    float level = 34;
                    if (WorldOfDaggerfallTerrain != null)
                        level = 100;

                    float height = 5;
                    if (currentEncounterTemplate.shipModel == 3)
                    {
                        modelID = 41511;
                        height = 6;
                    }

                    shipPos = new Vector3(shipPos.x, level + height, shipPos.z);

                    GameObject shipObject = GameObjectHelper.CreateDaggerfallMeshGameObject(modelID, GameObjectHelper.GetBestParent(), true, null, false);
                    shipObject.transform.position = shipPos;
                    shipObject.transform.forward = lastMoveDirection;
                    objectsSpawned.Add(shipObject);
                }
            }

            runEncounter = false;
        }

        string GetQuestCity()
        {
            if (dfUnity.WorldTime.Now.IsNight)
                return QuestsCityNight[UnityEngine.Random.Range(0, QuestsCityNight.Length)];

            return QuestsCityDay[UnityEngine.Random.Range(0, QuestsCityDay.Length)];
        }

        string GetQuestWilderness()
        {
            if (dfUnity.WorldTime.Now.IsNight)
                return QuestsWildernessNight[UnityEngine.Random.Range(0, QuestsWildernessNight.Length)];

            return QuestsWildernessDay[UnityEngine.Random.Range(0, QuestsWildernessDay.Length)];
        }

        void ClearSpawnedObjects()
        {
            if (objectsSpawned.Count > 0)
            {
                for (int i = 0; i < objectsSpawned.Count; i++)
                {
                    Destroy(objectsSpawned[i]);
                }
                objectsSpawned.Clear();
            }
        }


        void SpawnCamp()
        {
            if (GameManager.Instance.PlayerEnterExit.IsPlayerInside)
                return;

            //dismount player if needed
            if (GameManager.Instance.TransportManager.TransportMode != TransportModes.Foot)
                GameManager.Instance.TransportManager.TransportMode = TransportModes.Foot;

            ClearSpawnedObjects();

            Transform playerTransform = GameManager.Instance.PlayerObject.transform;

            //camp fire
            Vector3 firePos = playerTransform.position + (playerTransform.forward * 3f);

            Ray fireRay = new Ray(firePos + (Vector3.up * 50f), Vector3.down);
            RaycastHit fireHit = new RaycastHit();
            if (Physics.Raycast(fireRay, out fireHit, 200f))
                firePos = fireHit.point + (Vector3.up * 0.9f);
            //firePos = fireHit.point + (Vector3.up * 0.9f);
            //firePos = fireHit.point + (Vector3.up * 0.6f);

            //use this when WA updates to DET1.0
            GameObject fireObject = GameObjectHelper.CreateDaggerfallBillboardGameObject(10022, 2, GameObjectHelper.GetBestParent());
            //GameObject fireObject = GameObjectHelper.CreateDaggerfallBillboardGameObject(210, 1, GameObjectHelper.GetBestParent());
            fireObject.transform.position = firePos;
            objectsSpawned.Add(fireObject);

            //add light
            GameObject lightObject = GameObjectHelper.InstantiatePrefab(DaggerfallUnity.Instance.Option_DungeonLightPrefab.gameObject, string.Empty, fireObject.transform, firePos);
            //Light light = lightObject.GetComponent<Light>();

            /*if (ClimatesAndCalories != null)
            {
                //fireObject.GetComponentInChildren<Collider>().enabled = false;

                GameObject fireObjectCC = GameObjectHelper.CreateDaggerfallBillboardGameObject(210, 1, GameObjectHelper.GetBestParent());
                fireObjectCC.transform.position = firePos + ((Vector3.up * -0.3f));
                fireObjectCC.GetComponent<Renderer>().enabled = false;
                objectsSpawned.Add(fireObjectCC);
            }*/

            //add sound
            DaggerfallAudioSource fireAudioSource = fireObject.AddComponent<DaggerfallAudioSource>();
            fireAudioSource.AudioSource.dopplerLevel = 0;
            fireAudioSource.AudioSource.rolloffMode = AudioRolloffMode.Linear;
            fireAudioSource.AudioSource.maxDistance = 5f;
            fireAudioSource.AudioSource.volume = 0.7f;
            fireAudioSource.SetSound(SoundClips.Burning, AudioPresets.LoopIfPlayerNear);

            //Don't spawn tent if pixel has location
            if (GameManager.Instance.PlayerGPS.CurrentLocation.Loaded)
                return;

            //tent
            uint tentModelID = 45088;
            GameObject tentObject = MeshReplacement.ImportCustomGameobject(tentModelID, GameObjectHelper.GetBestParent(), playerTransform.localToWorldMatrix);
            Vector3 tentPos = playerTransform.position - (playerTransform.forward * 3f);

            Ray tentRay = new Ray(tentPos + (Vector3.up * 50f), Vector3.down);
            RaycastHit tentHit = new RaycastHit();
            Vector3 tentNormal = Vector3.up;
            if (Physics.Raycast(tentRay, out tentHit, 200f))
            {
                tentPos = tentHit.point;
                tentNormal = tentHit.normal;
            }

            tentObject.transform.position = tentPos;
            tentObject.transform.rotation = Quaternion.FromToRotation(tentObject.transform.up, tentNormal) * tentObject.transform.rotation;

            //tentObject.transform.rotation = tentObject.transform.rotation * Quaternion.FromToRotation(tentObject.transform.forward, firePos-tentPos);
            //Vector3 pos = new Vector3(playerTransform.position.x, tentPos.y, playerTransform.position.z);
            //tentObject.transform.LookAt(pos, tentNormal);

            objectsSpawned.Add(tentObject);

            //correct tent materials
            tentObject.GetComponent<Renderer>().material.shader = Shader.Find("Daggerfall/Billboard");

            mapPixelSpawned = new Vector2(GameManager.Instance.PlayerGPS.CurrentMapPixel.X, GameManager.Instance.PlayerGPS.CurrentMapPixel.Y);

            if (GameManager.Instance.PlayerEntity.LightSource != null)
                GameManager.Instance.PlayerEntity.LightSource = null;
        }
        void SpawnShip()
        {
            if (GameManager.Instance.PlayerEnterExit.IsPlayerInside)
                return;

            //dismount player if needed
            if (GameManager.Instance.TransportManager.TransportMode != TransportModes.Foot)
                GameManager.Instance.TransportManager.TransportMode = TransportModes.Foot;

            ClearSpawnedObjects();

            Transform playerTransform = GameManager.Instance.PlayerObject.transform;

            //place boat

            float level = 34;
            if (WorldOfDaggerfallTerrain != null)
                level = 100;

            float height = 5;

            Vector3 shipPos = new Vector3(playerTransform.position.x, level + height, playerTransform.position.z);

            GameObject shipObject = GameObjectHelper.CreateDaggerfallMeshGameObject(41506, GameObjectHelper.GetBestParent(), true, null, false);
            shipObject.transform.position = shipPos;
            shipObject.transform.forward = lastMoveDirection;
            objectsSpawned.Add(shipObject);

            //place player on boat
            Ray ray = new Ray(shipPos + (Vector3.up * 6f), Vector3.down);
            RaycastHit hit = new RaycastHit();
            if (Physics.Raycast(ray, out hit, 200f))
            {
                GameManager.Instance.PlayerObject.transform.position = hit.point + (Vector3.up * 0.9f);
            }

            mapPixelSpawned = new Vector2(GameManager.Instance.PlayerGPS.CurrentMapPixel.X, GameManager.Instance.PlayerGPS.CurrentMapPixel.Y);

            if (GameManager.Instance.PlayerEntity.LightSource != null)
                GameManager.Instance.PlayerEntity.LightSource = null;
        }
    }
}
