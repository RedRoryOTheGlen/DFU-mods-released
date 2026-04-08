using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Banking;
using DaggerfallWorkshop.Game.Items;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using Monobelisk.Compatibility;
using Wenzil.Console;

namespace FastTravelEncounters
{
    public class FastTravelEncounters : MonoBehaviour
    {

        static Mod mod;
        [Invoke(StateManager.StateTypes.Start, 0)]

        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            mod.SaveDataInterface = new FastTravelEncountersSaveData();
            go.AddComponent<FastTravelEncounters>();
        }

        public List<GameObject> objectsSpawned = new List<GameObject>();

        //FastTravelEncountersPopUpWindow encountersPopUp;
        DaggerfallTravelPopUp travelPopUp;
        DaggerfallTravelMapWindow travelWindow;

        TravelTimeCalculator travelTimeCalculatorPopUp;
        TravelTimeCalculator travelTimeCalculatorInterrupt = new TravelTimeCalculator();

        int chanceEncounterCautious = 5;
        int chanceEncounterReckless = 15;
        int chanceSurpriseCautious = 10;
        int chanceSurpriseReckless = 20;

        int levelAdjustment = 0;
        int levelWorld = 0;

        int locationSeekRange = 5;
        float locationSeekDot = 0.25f;

        bool shipTemporary;

        bool doEncounter = false;

        public static FastTravelEncounters Instance;

        string lastDestination = null;
        DFPosition lastDestinationPosition = null;
        string lastDestinationRegionName = null;
        int lastDestinationRegionIndex = -1;

        //original travel scheme
        int journeyMinutes;
        int journeyCost;
        int journeyDays;

        Vector2Int[] journeyPath;

        //this marks the day an encounter is rolled
        int encounterDay;

        float encounterDayFraction
        {
            get
            {
                return (float)encounterDay / (float)journeyDays;
            }
        }

        //this is the "new" travel scheme after a location is decided
        int encounterMinutes;
        int encounterCost;
        int encounterDays;

        bool canResume = false;
        bool surprised;

        FieldInfo LoadInProgress;

        PropertyInfo EndPos;
        FieldInfo DoFastTravel;
        FieldInfo TravelTimeTotalMins;
        MethodInfo TravelPopUpUpdateLabels;

        Matrix4x4 tentMatrix;
        int tentModelIndex = 0;
        uint[] tentModelIDs = new uint[12] { 45088, 45089, 45092, 45093, 45094, 45095, 45096, 45097, 45098, 45099, 450100, 450101 };

        //mod compatibility
        public Mod ClimatesAndCalories;
        bool fade;
        Texture2D fadeTexture;

        public Mod WarmAshesWilderness;
        public Mod WarmAshesDungeons;
        public Mod WarmAshesShips;
        public Mod DaggerfallEnemyExpansion;

        //horse and wagon mod handling
        public Mod RealisticWagon;
        GameObject RealisticWagonGameObject;

        public Mod RPGHorses;
        TransportModes lastTransportMode;

        //0 = Vanilla, 1 = Warm Ashes, 2 = Vanilla + Warm Ashes
        int encounterPool = 0;

        //settings
        bool vanillaEncounterGroup;
        bool logJourney;

        MobileTypes[] bosses;
        MobileTypes[] elites;
        MobileTypes[] minions;

        public Mod BasicRoads;
        Vector3[] vectors = new Vector3[8]
        {
            Vector3.up,
            Vector3.right,
            Vector3.down,
            Vector3.left,
            Vector3.up + Vector3.left,
            Vector3.up + Vector3.right,
            Vector3.down + Vector3.left,
            Vector3.down + Vector3.right,
        };

        public static bool hasDEX
        {
            get
            {
                return Instance.DaggerfallEnemyExpansion != null;
            }
        }

        public bool hasResidences;

        void Start()
        {
            Instance = this;

            ModCompatibilityChecking();

            DaggerfallTravelPopUp.OnPreFastTravel += DaggerfallTravelPopUp_OnPreFastTravel;
            DaggerfallTravelPopUp.OnPostFastTravel += DaggerfallTravelPopUp_OnPostFastTravel;

            DaggerfallUI.UIManager.OnWindowChange += OnWindowChange;

            PlayerEnterExit.OnPreTransition += OnTransition_PreTransition;
            PlayerEnterExit.OnTransitionExterior += OnTransition_Exterior;
            PlayerEnterExit.OnTransitionDungeonInterior += OnTransition_DungeonInterior;
            PlayerEnterExit.OnTransitionDungeonExterior += OnTransition_DungeonExterior;

            SaveLoadManager.OnLoad += OnLoad;
            StartGameBehaviour.OnNewGame += OnNewGame;

            LoadInProgress = GameManager.Instance.SaveLoadManager.GetType().GetField("loadInProgress", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.Instance);
            if (LoadInProgress != null)
                Debug.Log("FAST TRAVEL ENCOUNTER - FOUND LOADINPROGRESS FIELD");

            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();

            //Arrays to detect larger enemies
            bosses = new MobileTypes[7]
            {
                MobileTypes.OrcWarlord,
                MobileTypes.Vampire,
                MobileTypes.DaedraSeducer,
                MobileTypes.VampireAncient,
                MobileTypes.DaedraLord,
                MobileTypes.Lich,
                MobileTypes.AncientLich,
            };

            elites = new MobileTypes[11]
            {
                MobileTypes.Spriggan,
                MobileTypes.GrizzlyBear,
                MobileTypes.Werewolf,
                MobileTypes.OrcSergeant,
                MobileTypes.Wereboar,
                MobileTypes.Giant,
                MobileTypes.Mummy,
                MobileTypes.OrcShaman,
                MobileTypes.Gargoyle,
                MobileTypes.Daedroth,
                MobileTypes.Dragonling,
            };

            minions = new MobileTypes[4]
            {
                MobileTypes.Rat,
                MobileTypes.Imp,
                MobileTypes.GiantBat,
                MobileTypes.Spider,
            };

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

            mod.IsReady = true;
        }
        private void ModCompatibilityChecking()
        {
            BasicRoads = ModManager.Instance.GetModFromGUID("566ab21a-22d8-4eea-8ccd-6cb8f7a7ed25");
            if (BasicRoads != null)
                BasicRoadsUtils.Init();

            WarmAshesWilderness = ModManager.Instance.GetModFromGUID("2858f27c-7b60-4aa4-a22f-c29d2dc9e6fe");

            DaggerfallEnemyExpansion = ModManager.Instance.GetModFromGUID("76557441-7025-402e-a145-e3e1a28a093d");

            ClimatesAndCalories = ModManager.Instance.GetModFromGUID("7975b109-1381-485b-bdfd-8d076bb5d0c9");

            RealisticWagon = ModManager.Instance.GetModFromGUID("a30d1974-4d5d-477b-91c0-83f7f4bbd64b");
            if (RealisticWagon != null)
                RealisticWagonGameObject = GameObject.Find("Realistic Wagon");

            RPGHorses = ModManager.Instance.GetModFromGUID("cf01ae31-124f-4b9b-b830-fd43a1847dc0");

            Mod BeautifulCities = ModManager.Instance.GetModFromGUID("9827d938-f296-461f-b9c1-b4963cbb0e6b");
            Mod BeautifulVillages = ModManager.Instance.GetModFromGUID("b2324db6-7557-499a-aa25-d5e99014331d");
            Mod DIEPResidences = ModManager.Instance.GetModFromGUID("7d449ed9-f8f8-4d3b-99f8-01563df7bb5e");

            if (DIEPResidences != null || (BeautifulCities != null && BeautifulVillages != null))
                hasResidences = true;
        }

        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("Encounters"))
            {
                chanceEncounterCautious = settings.GetValue<int>("Encounters", "CautiousEncounterChance");
                chanceEncounterReckless = settings.GetValue<int>("Encounters", "RecklessEncounterChance");
                chanceSurpriseCautious = settings.GetValue<int>("Encounters", "CautiousSurpriseChance");
                chanceSurpriseReckless = settings.GetValue<int>("Encounters", "RecklessSurpriseChance");
                levelAdjustment = settings.GetValue<int>("Encounters", "EncounterLevelAdjustment");
                levelWorld = settings.GetValue<int>("Encounters", "StaticEncounterLevel");
            }
            if (change.HasChanged("LocationSeeking"))
            {
                locationSeekRange = settings.GetValue<int>("LocationSeeking", "Range");
                locationSeekDot = ((float)settings.GetValue<int>("LocationSeeking", "Diversion") / -90f) + 1f;
            }
            if (change.HasChanged("Spawns"))
            {
                vanillaEncounterGroup = settings.GetValue<bool>("Spawns", "VanillaEncounterGroups");
                encounterPool = settings.GetValue<int>("Spawns", "EncounterPool");
            }
            if (change.HasChanged("Feedback"))
            {
                logJourney = settings.GetValue<bool>("Feedback", "NotebookLog");
            }
        }

        public void OnWindowChange(object sender, EventArgs e)
        {
            if (DaggerfallUI.UIManager.TopWindow is DaggerfallTravelMapWindow && canResume)
            {
                Debug.Log("FAST TRAVEL ENCOUNTER - TRAVEL WINDOW OPENED!");
                Debug.Log("FAST TRAVEL ENCOUNTER - ENDPOS IS " + lastDestination);
                if (lastDestination != null && !doEncounter)
                {
                    canResume = false;
                    string resume = "Continue your journey to " + lastDestination + "?";
                    DaggerfallMessageBox resumeMsgBox = new DaggerfallMessageBox(DaggerfallUI.UIManager, DaggerfallMessageBox.CommonMessageBoxButtons.YesNo, resume, DaggerfallUI.UIManager.TopWindow);
                    resumeMsgBox.OnButtonClick += (_sender, button) =>
                    {
                        travelWindow.CloseWindow();
                        if (button == DaggerfallMessageBox.MessageBoxButtons.Yes)
                        {
                            StartCoroutine(ResumeJourney());
                        }
                        else if (button == DaggerfallMessageBox.MessageBoxButtons.No)
                        {
                            lastDestination = null;
                            canResume = false;
                        }
                    };
                    resumeMsgBox.Show();
                }
            }
        }

        private void OnGUI()
        {
            if (fade && !GameManager.IsGamePaused)
            {
                GUI.depth = -20;
                DaggerfallUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), fadeTexture);
            }
        }

        IEnumerator ResumeJourney()
        {
            Debug.Log("FAST TRAVEL ENCOUNTER - RESUMING JOURNEY TO " + lastDestination.ToUpper() + "!");

            bool shipConfiscated = false;
            TransportManager transportManager = GameManager.Instance.TransportManager;
            if (shipTemporary && transportManager.IsOnShip())
            {
                shipConfiscated = true;
                travelWindow.CloseWindow();

                DaggerfallUI.Instance.FadeBehaviour.SmashHUDToBlack();
                DaggerfallUI.Instance.FadeBehaviour.AllowFade = false;

                if (RealisticWagonGameObject != null)
                    RealisticWagonGameObject.SetActive(false);

                TransportModes currentTransportMode = transportManager.TransportMode;
                bool mounted = currentTransportMode == TransportModes.Horse || currentTransportMode == TransportModes.Cart ? true : false;

                if (transportManager.IsOnShip())
                    transportManager.TransportMode = TransportModes.Ship;

                yield return new WaitForSecondsRealtime(0.5f);

                if (mounted && transportManager.TransportMode != currentTransportMode)
                    transportManager.TransportMode = currentTransportMode;

                ConfiscateTemporaryShip();

                yield return new WaitForSecondsRealtime(0.1f);

                /*if (RealisticWagonGameObject != null)
                    RealisticWagonGameObject.SetActive(true);*/
            }

            EndPos.SetValue(travelPopUp, lastDestinationPosition);

            TravelPopUpUpdateLabels.Invoke(travelPopUp, null);

            if (travelTimeCalculatorPopUp.TotalCost <= GameManager.Instance.PlayerEntity.GetGoldAmount())
            {
                yield return new WaitForSecondsRealtime(1);

                DaggerfallUI.UIManager.PushWindow(travelPopUp);

                DoFastTravel.SetValue(travelPopUp, true);

                DaggerfallUI.Instance.FadeBehaviour.AllowFade = true;
            }
            else
            {
                yield return new WaitForSecondsRealtime(1);

                DaggerfallUI.MessageBox("You do not have enough gold!");

                DaggerfallUI.Instance.FadeBehaviour.AllowFade = true;

                if (shipConfiscated)
                {
                    AssignTemporaryShip();

                    if (RealisticWagonGameObject != null)
                        RealisticWagonGameObject.SetActive(false);

                    yield return new WaitForSecondsRealtime(0.2f);

                    if (!transportManager.IsOnShip())
                        transportManager.TransportMode = TransportModes.Ship;
                    else
                        DaggerfallUI.Instance.FadeBehaviour.FadeHUDFromBlack();


                    yield return new WaitForSecondsRealtime(0.5f);

                    /*if (RealisticWagonGameObject != null)
                        RealisticWagonGameObject.SetActive(true);*/
                }
            }

        }

        public void OnLoad(SaveData_v1 saveData)
        {
            Instance.OnGameStart();

            lastDestination = null;
            canResume = false;
        }

        public void OnNewGame()
        {
            Instance.OnGameStart();

            lastDestination = null;
            canResume = false;
        }

        public void OnGameStart()
        {
            if (travelWindow == null)
            {
                travelWindow = DaggerfallUI.Instance.DfTravelMapWindow;

                FieldInfo portLocationIDsField = travelWindow.GetType().GetField("portLocationIds", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.Instance);

                if (portLocationIDsField != null)
                {
                    Debug.Log("FAST TRAVEL ENCOUNTER - FOUND PORTLOCATIONIDS FIELD");

                    List<int> portsList = new List<int>((int[])portLocationIDsField.GetValue(travelWindow));
                    int count = portsList.Count;
                    portsList.Insert(0, 1050578 & 0x000FFFFF);
                    portsList.Insert(1, 2102157 & 0x000FFFFF);
                    int[] portsArray = portsList.ToArray();

                    portLocationIDsField.SetValue(travelWindow, portsArray);

                    //check if it happened
                    portsList = new List<int>((int[])portLocationIDsField.GetValue(travelWindow));
                    int newCount = portsList.Count;

                    if (count < newCount)
                        Debug.Log("FAST TRAVEL ENCOUNTER - SUCCESSFULLY ADDED THE NEW IDS");
                    else
                        Debug.Log("FAST TRAVEL ENCOUNTER - ARRAY WAS NOT CHANGED");
                }
            }
        }

        void RentRoom(PlayerGPS.DiscoveredBuilding buildingData, RoomRental_v1 rentedRoom, ulong seconds)
        {
            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
            PlayerEnterExit playerEnterExit = GameManager.Instance.PlayerEnterExit;

            int mapId = GameManager.Instance.PlayerGPS.CurrentLocation.MapTableData.MapId;
            string sceneName = DaggerfallInterior.GetSceneName(mapId, buildingData.buildingKey);
            if (rentedRoom == null)
            {
                // Get rest markers and select a random marker index for allocated bed
                // We store marker by index as building positions are not stable, they can move from terrain mods or floating Y
                Vector3[] restMarkers = playerEnterExit.Interior.FindMarkers(DaggerfallInterior.InteriorMarkerTypes.Rest);
                int markerIndex = UnityEngine.Random.Range(0, restMarkers.Length);

                // Create room rental and add it to player rooms
                RoomRental_v1 room = new RoomRental_v1()
                {
                    name = buildingData.displayName,
                    mapID = mapId,
                    buildingKey = buildingData.buildingKey,
                    allocatedBedIndex = markerIndex,
                    expiryTime = DaggerfallUnity.Instance.WorldTime.Now.ToSeconds() + seconds
                };
                playerEntity.RentedRooms.Add(room);
                SaveLoadManager.StateManager.AddPermanentScene(sceneName); ;
            }
            else
            {
                rentedRoom.expiryTime += seconds;
                Debug.LogFormat("Rented room for additional {1} seconds. {0}", sceneName, seconds);
            }
        }

        public static void OnTransition_PreTransition(PlayerEnterExit.TransitionEventArgs args)
        {
            Instance.ClearSpawnedObjects();
        }

        public static void OnTransition_Exterior(PlayerEnterExit.TransitionEventArgs args)
        {
            Instance.ClearSpawnedObjects();

            if (Instance.lastTransportMode != TransportModes.Foot && GameManager.Instance.TransportManager.TransportMode == TransportModes.Foot && Instance.lastDestination != null)
                Instance.RescueHorseAndCart();
        }

        public static void OnTransition_DungeonInterior(PlayerEnterExit.TransitionEventArgs args)
        {
            Instance.ClearSpawnedObjects();
        }

        public static void OnTransition_DungeonExterior(PlayerEnterExit.TransitionEventArgs args)
        {
            Instance.ClearSpawnedObjects();
        }

        void ClearSpawnedObjects()
        {
            if (objectsSpawned.Count > 0)
            {
                foreach (GameObject enemy in objectsSpawned)
                    Destroy(enemy);

                objectsSpawned.Clear();
            }
        }

        void DaggerfallTravelPopUp_OnPreFastTravel(DaggerfallTravelPopUp daggerfallTravelPopUp)
        {
            if (GameManager.Instance.TransportManager.TransportMode != TransportModes.Ship)
                lastTransportMode = GameManager.Instance.TransportManager.TransportMode;

            if (travelPopUp == null)
            {
                travelPopUp = daggerfallTravelPopUp;

                EndPos = travelPopUp.GetType().GetProperty("EndPos", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.Instance);
                if (EndPos != null)
                    Debug.Log("FAST TRAVEL ENCOUNTER - FOUND ENDPOS PROPERTY");

                DoFastTravel = travelPopUp.GetType().GetField("doFastTravel", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.Instance);
                if (DoFastTravel != null)
                    Debug.Log("FAST TRAVEL ENCOUNTER - FOUND DOFASTTRAVEL FIELD");

                travelTimeCalculatorPopUp = (TravelTimeCalculator)travelPopUp.GetType().GetField("travelTimeCalculator", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.Instance).GetValue(travelPopUp);
                if (travelTimeCalculatorPopUp != null)
                    Debug.Log("FAST TRAVEL ENCOUNTER - FOUND TRAVELTIMECALCULATOR FIELD");

                TravelPopUpUpdateLabels = travelPopUp.GetType().GetMethod("UpdateLabels", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.Instance);
                if (TravelPopUpUpdateLabels != null)
                    Debug.Log("FAST TRAVEL ENCOUNTER - FOUND UPDATELABELS METHOD");

                TravelTimeTotalMins = travelPopUp.GetType().GetField("travelTimeTotalMins", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.Instance);
                if (TravelTimeTotalMins != null)
                    Debug.Log("FAST TRAVEL ENCOUNTER - FOUND TravelTimeTotalMins FIELD");
            }

            ClearSpawnedObjects();

            doEncounter = CheckEncounter();

            if (doEncounter)
                InterruptTravel();
        }

        void DaggerfallTravelPopUp_OnPostFastTravel()
        {
            if (doEncounter)
            {
                RebalanceGold();
                SpawnEncounter();

                string locationName = "the wilds of " + GameManager.Instance.PlayerGPS.CurrentRegionName;
                if (GameManager.Instance.PlayerGPS.CurrentLocation.Loaded)
                {
                    string firstWord = GameManager.Instance.PlayerGPS.CurrentLocation.Name.Split(' ')[0];
                    if (firstWord == "Ruins")
                        locationName = "The " + GameManager.Instance.PlayerGPS.CurrentLocation.Name;
                    else
                        locationName = GameManager.Instance.PlayerGPS.CurrentLocation.Name;
                }

                if (logJourney)
                    GameManager.Instance.PlayerEntity.Notebook.AddNote("Travelled " + encounterDays.ToString() + " days and spent " + encounterCost.ToString() + " septims when something interesting happened in " + locationName);
            }
            else
            {
                string locationName = "the wilds of " + GameManager.Instance.PlayerGPS.CurrentRegionName;
                if (GameManager.Instance.PlayerGPS.CurrentLocation.Loaded)
                {
                    string firstWord = GameManager.Instance.PlayerGPS.CurrentLocation.Name.Split(' ')[0];
                    if (firstWord == "Ruins")
                        locationName = "The " + GameManager.Instance.PlayerGPS.CurrentLocation.Name;
                    else
                        locationName = GameManager.Instance.PlayerGPS.CurrentLocation.Name;
                }

                if (logJourney)
                    GameManager.Instance.PlayerEntity.Notebook.AddNote("Travelled " + journeyDays.ToString() + " days and spent " + journeyCost.ToString() + " septims to arrive at " + locationName + " without incident");

                lastDestination = null;

                if (shipTemporary)
                    ConfiscateTemporaryShip();

                if (RealisticWagonGameObject != null)
                    RealisticWagonGameObject.SetActive(true);

                RescueHorseAndCart();
            }
        }

        void RescueHorseAndCart()
        {
            if (lastTransportMode != TransportModes.Foot && GameManager.Instance.TransportManager.TransportMode == TransportModes.Foot && !GameManager.Instance.PlayerEnterExit.IsPlayerInside && !GameManager.Instance.TransportManager.IsOnShip())
                StartCoroutine(RescueHorseAndCartCoroutine());
        }

        IEnumerator RescueHorseAndCartCoroutine()
        {
            Debug.Log("FAST TRAVEL ENCOUNTERS - RUNNING RESCUE HORSE CODE!");

            yield return new WaitForSeconds(1);

            if (RealisticWagon != null)
            {
                if (!GameManager.Instance.TransportManager.HasHorse())
                    Wenzil.Console.Console.ExecuteCommand("horse_rescue");

                yield return new WaitForEndOfFrame();

                if (lastTransportMode == TransportModes.Cart && !GameManager.Instance.TransportManager.HasCart())
                    Wenzil.Console.Console.ExecuteCommand("wagon_rescue");
            }

            if (RPGHorses != null)
                Wenzil.Console.Console.ExecuteCommand("rpg_reposition");

            lastTransportMode = TransportModes.Foot;
        }

        void AssignTemporaryShip()
        {
            shipTemporary = true;
            DaggerfallBankManager.AssignShipToPlayer(ShipType.Large);
        }

        void ConfiscateTemporaryShip()
        {
            DFPosition shipCoords = DaggerfallBankManager.GetShipCoords();
            SaveLoadManager.StateManager.RemovePermanentScene(StreamingWorld.GetSceneName(5, 5));
            SaveLoadManager.StateManager.RemovePermanentScene(DaggerfallInterior.GetSceneName(2102157, BuildingDirectory.buildingKey0));
            DaggerfallBankManager.AssignShipToPlayer(ShipType.None);
            shipTemporary = false;
        }

        public void RebalanceGold()
        {
            GameManager.Instance.PlayerEntity.GoldPieces += journeyCost;
            GameManager.Instance.PlayerEntity.DeductGoldAmount(encounterCost);
        }

        int TravelTimeInDays(int minutes)
        {
            int travelTimeDays = (minutes / 1440);
            if ((minutes % 1440) > 0)
                travelTimeDays += 1;

            return travelTimeDays;
        }

        Vector2Int[] GetPathPixels(Vector2Int start, Vector2Int end)
        {
            List<Vector2Int> path = new List<Vector2Int>();

            Vector2Int current = start;
            int index = 0;
            while (current != end)
            {
                //get neighbors of previous
                List<Vector2Int> neighbors = new List<Vector2Int>();
                neighbors.Add(new Vector2Int(start.x - 1, start.y - 1));
                neighbors.Add(new Vector2Int(start.x, start.y - 1));
                neighbors.Add(new Vector2Int(start.x + 1, start.y - 1));
                neighbors.Add(new Vector2Int(start.x - 1, start.y));
                neighbors.Add(new Vector2Int(start.x + 1, start.y));
                neighbors.Add(new Vector2Int(start.x - 1, start.y + 1));
                neighbors.Add(new Vector2Int(start.x, start.y + 1));
                neighbors.Add(new Vector2Int(start.x + 1, start.y + 1));

                //find neighbor closest to end
                Vector2Int neighborClosest = -Vector2Int.one;
                float distanceClosest = Mathf.Infinity;
                foreach (Vector2Int neighbor in neighbors)
                {
                    float distanceCurrent = Vector2Int.Distance(neighbor, end);
                    if (distanceCurrent < distanceClosest)
                    {
                        neighborClosest = neighbor;
                        distanceClosest = distanceCurrent;
                    }
                }

                current = neighborClosest;

                //add closest neighbor to list
                path.Add(neighborClosest);
            }

            return path.ToArray();
        }

        bool CheckEncounter()
        {
            //reset variables
            surprised = false;

            //get current travel details
            journeyMinutes = (int)TravelTimeTotalMins.GetValue(travelPopUp);
            journeyDays = TravelTimeInDays(journeyMinutes);
            journeyCost = travelTimeCalculatorPopUp.TotalCost;

            //first leg of journey
            if (lastDestination == null)
            {
                DaggerfallUnity.Instance.ContentReader.HasLocation(travelPopUp.EndPos.X, travelPopUp.EndPos.Y, out ContentReader.MapSummary destinationSummary);
                DaggerfallUnity.Instance.ContentReader.GetLocation(destinationSummary.RegionIndex, destinationSummary.MapIndex, out DFLocation destinationLocation);

                string originName = "the wilds of " + GameManager.Instance.PlayerGPS.CurrentRegionName;
                if (GameManager.Instance.PlayerGPS.CurrentLocation.Loaded)
                {
                    string firstWord = GameManager.Instance.PlayerGPS.CurrentLocation.Name.Split(' ')[0];
                    if (firstWord == "Ruins")
                        originName = "The " + GameManager.Instance.PlayerGPS.CurrentLocation.Name;
                    else
                        originName = GameManager.Instance.PlayerGPS.CurrentLocation.Name;
                }

                string destinationName = "the wilds of " + destinationLocation.RegionName;
                if (destinationLocation.Loaded)
                {
                    string firstWord = destinationLocation.Name.Split(' ')[0];
                    if (firstWord == "Ruins")
                        destinationName = "The " + destinationLocation.Name;
                    else
                        destinationName = destinationLocation.Name;
                }

                if (logJourney)
                    GameManager.Instance.PlayerEntity.Notebook.AddNote("From " + originName + " in " + GameManager.Instance.PlayerGPS.CurrentLocalizedRegionName + ", began a " + journeyDays.ToString() + "-day journey to " + destinationName + " in " + destinationLocation.RegionName);
            }

            bool encounter = false;

            //derive encounter chance
            int chanceEncounterBase = travelPopUp.SpeedCautious ? chanceEncounterCautious : chanceEncounterReckless;
            int chanceEncounter = chanceEncounterBase;

            //camping increases encounter chance
            if (!travelPopUp.SleepModeInn)
                chanceEncounter +=  chanceEncounterBase;

            //has acute hearing
            if (GameManager.Instance.PlayerEntity.Career.AcuteHearing)
                chanceEncounter -= chanceEncounterBase;

            
            int luck = GameManager.Instance.PlayerEntity.Stats.GetLiveStatValue(DFCareer.Stats.Luck);
            //luck modifier, maximum of +-10 to total chance
            //chanceEncounter -= Mathf.CeilToInt(((float)luck - 50f) / 5f);
            //or
            //luck-based bonus/malus
            if (Dice100.SuccessRoll(luck))
            {
                //if player has better than average luck, give bonus
                if (luck > 60)
                    chanceEncounter -= chanceEncounterBase;
            }
            else
            {
                //if player has worse than average luck, give malus
                if (luck < 40)
                    chanceEncounter += chanceEncounterBase;
            }

            //clamp minimum and maximum chance
            if (chanceEncounter < 5)
                chanceEncounter = 5;
            else if (chanceEncounter > 95)
                chanceEncounter = 95;

            //get day of encounter
            Debug.Log("FAST TRAVEL ENCOUNTERS - ENCOUNTER CHANCE PER DAY OF TRAVEL IS " + chanceEncounter.ToString() + "%");
            for (int i = 0; i < journeyDays - 1; i++)
            {
                int chanceCurrent = chanceEncounter;

                if (Dice100.SuccessRoll(chanceCurrent))
                {
                    encounter = true;
                    encounterDay = i + 1;
                    break;
                }
            }

            return encounter;
        }

        bool CheckEncounterPath()
        {
            //reset variables
            surprised = false;

            //get current travel details
            journeyMinutes = (int)TravelTimeTotalMins.GetValue(travelPopUp);
            journeyDays = TravelTimeInDays(journeyMinutes);
            journeyCost = travelTimeCalculatorPopUp.TotalCost;

            //first leg of journey
            if (lastDestination == null)
            {
                DaggerfallUnity.Instance.ContentReader.HasLocation(travelPopUp.EndPos.X, travelPopUp.EndPos.Y, out ContentReader.MapSummary destinationSummary);
                DaggerfallUnity.Instance.ContentReader.GetLocation(destinationSummary.RegionIndex, destinationSummary.MapIndex, out DFLocation destinationLocation);

                string originName = "the wilds of " + GameManager.Instance.PlayerGPS.CurrentRegionName;
                if (GameManager.Instance.PlayerGPS.CurrentLocation.Loaded)
                {
                    string firstWord = GameManager.Instance.PlayerGPS.CurrentLocation.Name.Split(' ')[0];
                    if (firstWord == "Ruins")
                        originName = "The " + GameManager.Instance.PlayerGPS.CurrentLocation.Name;
                    else
                        originName = GameManager.Instance.PlayerGPS.CurrentLocation.Name;
                }

                string destinationName = "the wilds of " + destinationLocation.RegionName;
                if (destinationLocation.Loaded)
                {
                    string firstWord = destinationLocation.Name.Split(' ')[0];
                    if (firstWord == "Ruins")
                        destinationName = "The " + destinationLocation.Name;
                    else
                        destinationName = destinationLocation.Name;
                }

                if (logJourney)
                    GameManager.Instance.PlayerEntity.Notebook.AddNote("From " + originName + " in " + GameManager.Instance.PlayerGPS.CurrentLocalizedRegionName + ", began a " + journeyDays.ToString() + "-day journey to " + destinationName + " in " + destinationLocation.RegionName);
            }

            bool encounter = false;

            //GET ALL THE PIXELS ON THE WAY TO THE DESTINATION
            DFPosition position = TravelTimeCalculator.GetPlayerTravelPosition();
            Vector2Int start = new Vector2Int(position.X, position.Y);
            Vector2Int end = new Vector2Int(travelPopUp.EndPos.X, travelPopUp.EndPos.Y);
            journeyPath = GetPathPixels(start, end);

            //Go through path and check for encounter
            for (int i = 0; i < journeyPath.Length; i++)
            {
                //base encounter chance
                int chance = travelPopUp.SpeedCautious ? chanceEncounterCautious : chanceEncounterReckless;
                //camping increases encounter chance
                chance *= travelPopUp.SleepModeInn ? 1 : 2;

                //if land or sea
                int terrain = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetClimateIndex(journeyPath[i].x, journeyPath[i].y);
                if (terrain == (int)MapsFile.Climates.Ocean)
                {
                    //Travelling by ship
                    //Ship travel increases encounter chance
                    //Larger ship makes encounter more likely
                    if (DaggerfallBankManager.OwnedShip == ShipType.Small)
                        chance *= Mathf.CeilToInt((float)chance * 1.5f);
                    else
                        chance *= 3;
                }
                else
                {
                    //Travelling by foot/horse
                    //having a cart is a permanent malus to encounter avoidance
                    if (GameManager.Instance.TransportManager.HasCart())
                        chance *= 2;
                    else if (GameManager.Instance.TransportManager.HasHorse())
                        chance = Mathf.CeilToInt((float)chance * 0.5f);
                }

                if (Dice100.SuccessRoll(chance))
                {
                    encounter = true;
                    encounterDay = i;
                    break;
                }
            }

            return encounter;
        }

        void InterruptTravel()
        {
            DFPosition startPos = TravelTimeCalculator.GetPlayerTravelPosition();
            DFPosition endPos = travelPopUp.EndPos;

            //detects if first leg of journey
            if (lastDestination == null)
            {
                lastDestinationPosition = endPos;
                DaggerfallUnity.Instance.ContentReader.HasLocation(endPos.X, endPos.Y, out ContentReader.MapSummary lastDestinationMapSummary);
                DaggerfallUnity.Instance.ContentReader.GetLocation(lastDestinationMapSummary.RegionIndex, lastDestinationMapSummary.MapIndex, out DFLocation lastDestinationLocation);
                lastDestination = TextManager.Instance.GetLocalizedLocationName(lastDestinationLocation.MapTableData.MapId, lastDestinationLocation.Name);
                lastDestinationRegionName = TextManager.Instance.GetLocalizedRegionName(lastDestinationLocation.RegionIndex);
                lastDestinationRegionIndex = lastDestinationLocation.RegionIndex;
            }

            //get position a fraction between two points
            Vector2 start = new Vector2(startPos.X, startPos.Y);
            Vector2 end = new Vector2(endPos.X, endPos.Y);
            Vector2 vector = end - start;
            Vector2 pos = start + (vector * encounterDayFraction);
            DFPosition newPos = new DFPosition(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y));

            //get nearby locations
            if (locationSeekRange > 0)
            {
                List<Vector2Int> locations = new List<Vector2Int>();
                List<Vector2Int> roads = new List<Vector2Int>();
                for (int i = (int)pos.x - locationSeekRange; i < (int)pos.x + locationSeekRange + 1; i++)
                {
                    if (i < MapsFile.MinMapPixelX || i > MapsFile.MaxMapPixelX)
                        continue;

                    for (int ii = (int)pos.y - locationSeekRange; ii < (int)pos.y + locationSeekRange + 1; ii++)
                    {
                        if (ii < MapsFile.MinMapPixelY || ii > MapsFile.MaxMapPixelY)
                            continue;

                        Vector2Int location = new Vector2Int(i, ii);

                        //make sure this isn't the starting pixel
                        if (location == new Vector2Int((int)start.x, (int)start.y))
                            continue;

                        //make sure this pixel is "ahead" of the start pos
                        if (Vector2.Dot(vector.normalized, (location - start).normalized) < locationSeekDot)
                            continue;

                        ContentReader.MapSummary mapSummary;
                        bool hasLocation = DaggerfallUnity.Instance.ContentReader.HasLocation(i, ii, out mapSummary);
                        if (hasLocation)
                        {
                            if ((travelPopUp.SleepModeInn &&
                                (mapSummary.LocationType == DFRegion.LocationTypes.TownCity ||
                                mapSummary.LocationType == DFRegion.LocationTypes.TownHamlet ||
                                mapSummary.LocationType == DFRegion.LocationTypes.TownVillage ||
                                mapSummary.LocationType == DFRegion.LocationTypes.Tavern ||
                                (hasResidences && mapSummary.LocationType == DFRegion.LocationTypes.HomeWealthy) ||
                                (hasResidences && mapSummary.LocationType == DFRegion.LocationTypes.HomeFarms) ||
                                (hasResidences && mapSummary.LocationType == DFRegion.LocationTypes.HomePoor))) ||
                                (!travelPopUp.SleepModeInn &&
                                (mapSummary.LocationType == DFRegion.LocationTypes.DungeonRuin ||
                                mapSummary.LocationType == DFRegion.LocationTypes.DungeonLabyrinth ||
                                mapSummary.LocationType == DFRegion.LocationTypes.DungeonKeep))
                                )
                            {
                                if (travelPopUp.SleepModeInn && !hasResidences)
                                {
                                    DaggerfallUnity.Instance.ContentReader.GetLocation(mapSummary.RegionIndex, mapSummary.MapIndex, out DFLocation dfLocation);

                                    foreach (DFLocation.BuildingData building in dfLocation.Exterior.Buildings)
                                    {
                                        if (building.BuildingType == DFLocation.BuildingTypes.Tavern)
                                        {
                                            locations.Add(location);
                                            break;
                                        }
                                        continue;
                                    }
                                }
                                else
                                {
                                    locations.Add(location);
                                }
                                continue;
                            }
                        }

                        //if Basic Roads is detected, add roads and tracks to neighbors
                        if (BasicRoads != null && !hasLocation)
                        {
                            if (BasicRoadsUtils.IsRoad(i, ii, false))
                                roads.Add(location);
                        }
                    }
                }

                //prioritize locations over roads
                if (locations.Count > 0)
                {
                    Vector2Int closestLocation = -Vector2Int.one;
                    float closestDistance = Mathf.Infinity;
                    foreach (Vector2Int neighbor in locations)
                    {
                        float currentDistance = Vector2.Distance(start, (Vector2)neighbor);
                        if (currentDistance < closestDistance)
                        {
                            closestLocation = neighbor;
                            closestDistance = currentDistance;
                        }
                    }

                    if (closestLocation != -Vector2Int.one)
                        newPos = new DFPosition(closestLocation.x, closestLocation.y);
                }
                else if (roads.Count > 0)
                {
                    Vector2Int closestRoad = -Vector2Int.one;
                    float closestDistance = Mathf.Infinity;
                    foreach (Vector2Int road in roads)
                    {
                        float currentDistance = Vector2.Distance(start, (Vector2)road);
                        if (currentDistance < closestDistance)
                        {
                            closestRoad = road;
                            closestDistance = currentDistance;
                        }
                    }

                    if (closestRoad != -Vector2Int.one)
                        newPos = new DFPosition(closestRoad.x, closestRoad.y);
                }
            }

            Debug.Log("FAST TRAVEL ENCOUNTERS - START POS IS AT MAP PIXEL (" + start.x.ToString() + ", " + start.y.ToString() + ")");
            Debug.Log("FAST TRAVEL ENCOUNTERS - END POS IS AT MAP PIXEL (" + end.x.ToString() + ", " + end.y.ToString() + ")");
            Debug.Log("FAST TRAVEL ENCOUNTERS - NEW POS IS AT MAP PIXEL (" + newPos.X.ToString() + ", " + newPos.Y.ToString() + ")");

            EndPos.SetValue(travelPopUp, newPos);

            TravelPopUpUpdateLabels.Invoke(travelPopUp, null);

            //record the encounter travel scheme
            encounterMinutes = (int)TravelTimeTotalMins.GetValue(travelPopUp);
            encounterDays = TravelTimeInDays(encounterMinutes);
            encounterCost = travelTimeCalculatorPopUp.TotalCost;

            DaggerfallUI.Instance.FadeBehaviour.AllowFade = false;
        }

        public static void SpawnEncounter()
        {
            Instance.doEncounter = false;

            Instance.StartCoroutine(Instance.SpawnEncounterCoroutine());
        }

        public IEnumerator SpawnEncounterCoroutine()
        {
            if (RealisticWagonGameObject != null)
                RealisticWagonGameObject.SetActive(false);

            if (ClimatesAndCalories != null)
            {
                fade = true;
                DaggerfallUI.Instance.FadeBehaviour.AllowFade = true;
                DaggerfallUI.Instance.FadeBehaviour.FadeHUDFromBlack();

                yield return new WaitForSeconds(0.5f);
            }
            else
                yield return new WaitForSeconds(0.25f);

            //LoadInProgress.SetValue(GameManager.Instance.SaveLoadManager, true);
            //GameManager.Instance.EntityEffectBroker.enabled = false;
            //DaggerfallUnity.Instance.WorldTime.enabled = false;

            //get conditions
            GameObject playerObject = GameManager.Instance.PlayerObject;
            DaggerfallLocation currentLocation = GameManager.Instance.StreamingWorld.CurrentPlayerLocationObject;
            DaggerfallDateTime time = DaggerfallUnity.Instance.WorldTime.Now;
            DFPosition pos = GameManager.Instance.PlayerGPS.CurrentMapPixel;
            TransportManager transportManager = GameManager.Instance.TransportManager;

            bool atSea = GameManager.Instance.PlayerGPS.CurrentPoliticIndex == 64 ? true : false;
            bool isRoad = BasicRoads != null ? BasicRoadsUtils.IsRoad(pos.X, pos.Y) : false;
            bool inHouse = false;
            bool isNight = time.Hour < 8 || time.Hour > 16 ? true : false;

            //determine surprise, start with the base chance
            //base chance is incremented upwards/downwards for every condition met
            int chanceSurpriseBase = travelPopUp.SpeedCautious ? chanceSurpriseCautious : chanceSurpriseReckless;
            int chanceSurprise = chanceSurpriseBase;

            //camping out
            if (!travelPopUp.SleepModeInn)
                chanceSurprise += chanceSurpriseBase;

            //at night
            if (isNight)
                chanceSurprise += chanceSurpriseBase;

            //in bad weather
            if ((int)GameManager.Instance.PlayerObject.GetComponent<PlayerWeather>().WeatherType > 2)
                chanceSurprise += chanceSurpriseBase;

            //has acute hearing
            if (GameManager.Instance.PlayerEntity.Career.AcuteHearing)
                chanceSurprise -= chanceSurpriseBase;

            int luck = GameManager.Instance.PlayerEntity.Stats.GetLiveStatValue(DFCareer.Stats.Luck);
            //luck modifier, maximum of +-10 to total chance
            //chanceSurprise -= Mathf.CeilToInt(((float)luck - 50f) / 5f);
            //or
            //luck-based bonus/malus
            if (Dice100.SuccessRoll(luck))
            {
                //if player has better than average luck, give bonus
                if (luck > 60)
                    chanceSurprise -= chanceSurpriseBase;
            }
            else
            {
                //if player has worse than average luck, give malus
                if (luck < 40)
                    chanceSurprise += chanceSurpriseBase;
            }

            //clamp minimum and maximum chance
            if (chanceSurprise < 5)
                chanceSurprise = 5;
            else if (chanceSurprise > 95)
                chanceSurprise = 95;

            if (Dice100.SuccessRoll(chanceSurprise))
                surprised = true;

            //if at sea, teleport to a ship
            if (atSea)
            {
                DFPosition shipCoords = DaggerfallBankManager.GetShipCoords();

                //player doesn't own a ship
                if (shipCoords == null)
                {
                    AssignTemporaryShip();
                }

                if (RealisticWagonGameObject != null)
                    RealisticWagonGameObject.SetActive(false);

                GameManager.Instance.TransportManager.TransportMode = TransportModes.Ship;

                yield return new WaitForSeconds(0.2f);

                currentLocation = GameManager.Instance.StreamingWorld.CurrentPlayerLocationObject;

                if (surprised)
                {
                    if (RPGHorses != null)
                    {
                        if (transportManager.TransportMode != TransportModes.Foot)
                            transportManager.TransportMode = TransportModes.Foot;
                        yield return new WaitForSeconds(0.2f);
                    }

                    DaggerfallStaticDoors[] staticDoors = currentLocation.StaticDoorCollections;
                    if (staticDoors.Length > 0)
                    {
                        DaggerfallStaticDoors daggerfallStaticDoors = staticDoors[UnityEngine.Random.Range(0, staticDoors.Length)];
                        Transform doorOwner = daggerfallStaticDoors.transform;
                        StaticDoor staticDoor = daggerfallStaticDoors.Doors[UnityEngine.Random.Range(0, daggerfallStaticDoors.Doors.Length)];
                        /*GameManager.Instance.PlayerObject.transform.position = staticDoor.centre;
                        GameManager.Instance.PlayerObject.transform.forward = staticDoor.normal;*/

                        //place player inside ship interior
                        // Get building discovery data - this is added when player clicks door at exterior
                        GameManager.Instance.PlayerGPS.DiscoverBuilding(staticDoor.buildingKey);
                        GameManager.Instance.PlayerGPS.GetDiscoveredBuilding(staticDoor.buildingKey, out PlayerGPS.DiscoveredBuilding db);

                        // Perform transition
                        GameManager.Instance.PlayerEnterExit.BuildingDiscoveryData = db;
                        GameManager.Instance.PlayerEnterExit.IsPlayerInsideOpenShop = RMBLayout.IsShop(db.buildingType) && PlayerActivate.IsBuildingOpen(db.buildingType);
                        GameManager.Instance.PlayerEnterExit.IsPlayerInsideTavern = RMBLayout.IsTavern(db.buildingType);
                        GameManager.Instance.PlayerEnterExit.IsPlayerInsideResidence = RMBLayout.IsResidence(db.buildingType);
                        GameManager.Instance.PlayerEnterExit.TransitionInterior(doorOwner, staticDoor, false, false);

                        //align player to hull
                        if (DaggerfallBankManager.OwnedShip == ShipType.Small)
                            GameManager.Instance.PlayerMouseLook.SetHorizontalFacing(Vector3.left);
                        else
                        {
                            GameManager.Instance.PlayerMouseLook.SetHorizontalFacing(Vector3.back);
                            //GameManager.Instance.PlayerObject.transform.Translate(Vector3.down * 3);
                        }

                        //place player under a light
                        Vector3[] markers = GetShipSpawnMarkers();
                        if (markers.Length > 0)
                            GameManager.Instance.PlayerObject.transform.position = markers[UnityEngine.Random.Range(0, markers.Length)];
                    }
                }
                else
                {
                    //place player on the deck

                    //align player to hull
                    if (DaggerfallBankManager.OwnedShip == ShipType.Small)
                        GameManager.Instance.PlayerMouseLook.SetHorizontalFacing(Vector3.right);
                    else
                        GameManager.Instance.PlayerMouseLook.SetHorizontalFacing(Vector3.forward);


                    //if night and in the wilderness or has bad weather, spawn camp stuff
                    if (isNight || (int)GameManager.Instance.WeatherManager.PlayerWeather.WeatherType > 2)
                    {
                        //equip a light source
                        if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, (int)UselessItems2.Lantern))
                            GameManager.Instance.PlayerEntity.LightSource = GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.UselessItems2, (int)UselessItems2.Lantern);
                        else if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, (int)UselessItems2.Torch))
                            GameManager.Instance.PlayerEntity.LightSource = GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.UselessItems2, (int)UselessItems2.Torch);
                        else if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, (int)UselessItems2.Candle))
                            GameManager.Instance.PlayerEntity.LightSource = GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.UselessItems2, (int)UselessItems2.Candle);
                        else if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.ReligiousItems, (int)ReligiousItems.Holy_candle))
                            GameManager.Instance.PlayerEntity.LightSource = GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.ReligiousItems, (int)ReligiousItems.Holy_candle);
                    }
                    else
                    {
                        if (GameManager.Instance.PlayerEntity.LightSource != null)
                            GameManager.Instance.PlayerEntity.LightSource = null;
                    }
                }

                yield return new WaitForSeconds(0.2f);
            }
            else
            {
                Vector3 startPos = Vector3.zero;

                //if location is a dungeon or town, pick a start point?
                if (currentLocation != null)
                {
                    //startPos = currentLocation.StartMarkers[UnityEngine.Random.Range(0, currentLocation.StartMarkers.Length)].transform.position;
                    StreamingWorld.RepositionMethods reposition = StreamingWorld.RepositionMethods.DirectionFromStartMarker;

                    if (isNight)
                    {
                        if (currentLocation.Summary.HasDungeon)
                        {
                            //reposition = StreamingWorld.RepositionMethods.DungeonEntrance;

                            GameManager.Instance.StreamingWorld.SetAutoReposition(reposition, Vector3.zero);

                            yield return new WaitForSeconds(1f);

                            //Dismount the player
                            if (transportManager.TransportMode != TransportModes.Foot)
                                transportManager.TransportMode = TransportModes.Foot;
                        }
                        else
                        {
                            //probably a town with a tavern, so teleport to a tavern interior at night
                            inHouse = true;

                            GameManager.Instance.StreamingWorld.SetAutoReposition(reposition, Vector3.zero);

                            if (RPGHorses != null)
                            {
                                if (transportManager.TransportMode != TransportModes.Foot)
                                    transportManager.TransportMode = TransportModes.Foot;
                            }

                            yield return new WaitForSeconds(1f);

                            //how to get static doors from building type?
                            List<BuildingSummary> buildingSummaries = GameManager.Instance.StreamingWorld.GetCurrentBuildingDirectory().GetBuildingsOfType(DFLocation.BuildingTypes.Tavern);

                            if (hasResidences)
                            {
                                //if residences are installed, then there's a chance there is no tavern in location
                                if (buildingSummaries.Count < 1)
                                {
                                    Debug.Log("FAST TRAVEL ENCOUNTERS - NO TAVERNS FOUND! LOOKING FOR A HOUSE1!");
                                    buildingSummaries = GameManager.Instance.StreamingWorld.GetCurrentBuildingDirectory().GetBuildingsOfType(DFLocation.BuildingTypes.House1);
                                }

                                if (buildingSummaries.Count < 1)
                                {
                                    Debug.Log("FAST TRAVEL ENCOUNTERS - NO HOUSE1 FOUND! LOOKING FOR A HOUSE2!");
                                    buildingSummaries = GameManager.Instance.StreamingWorld.GetCurrentBuildingDirectory().GetBuildingsOfType(DFLocation.BuildingTypes.House2);
                                }

                                if (buildingSummaries.Count < 1)
                                {
                                    Debug.Log("FAST TRAVEL ENCOUNTERS - NO HOUSE2 FOUND! LOOKING FOR A HOUSE3!");
                                    buildingSummaries = GameManager.Instance.StreamingWorld.GetCurrentBuildingDirectory().GetBuildingsOfType(DFLocation.BuildingTypes.House3);
                                }
                            }

                            if (buildingSummaries.Count > 0)
                            {
                                //pick a random building
                                int buildingKey = buildingSummaries[UnityEngine.Random.Range(0, buildingSummaries.Count)].buildingKey;

                                //how to get building type from static doors?
                                foreach (DaggerfallStaticDoors staticDoors in currentLocation.StaticDoorCollections)
                                {
                                    foreach (StaticDoor staticDoor in staticDoors.Doors)
                                    {
                                        if (staticDoor.buildingKey == buildingKey)
                                        {
                                            StaticDoor door = staticDoor;

                                            Debug.Log("FAST TRAVEL ENCOUNTERS - FOUND A STATIC DOOR BELONGING TO THE BUILDING!");

                                            //place player inside tavern interior
                                            // Get building discovery data - this is added when player clicks door at exterior
                                            GameManager.Instance.PlayerGPS.DiscoverBuilding(door.buildingKey);
                                            GameManager.Instance.PlayerGPS.GetDiscoveredBuilding(door.buildingKey, out PlayerGPS.DiscoveredBuilding db);

                                            // Perform transition
                                            GameManager.Instance.PlayerEnterExit.BuildingDiscoveryData = db;
                                            GameManager.Instance.PlayerEnterExit.IsPlayerInsideOpenShop = RMBLayout.IsShop(db.buildingType) && PlayerActivate.IsBuildingOpen(db.buildingType);
                                            GameManager.Instance.PlayerEnterExit.IsPlayerInsideTavern = RMBLayout.IsTavern(db.buildingType);
                                            GameManager.Instance.PlayerEnterExit.IsPlayerInsideResidence = RMBLayout.IsResidence(db.buildingType);
                                            GameManager.Instance.PlayerEnterExit.TransitionInterior(staticDoors.transform, door, false, false);

                                            if (hasResidences)
                                                yield return new WaitForSeconds(5f);
                                            else
                                                yield return new WaitForSeconds(1f);

                                            Debug.Log("FAST TRAVEL ENCOUNTERS - RELOCATING TO VALID MARKER!");
                                            GameManager.Instance.PlayerEnterExit.Interior.FindMarker(out startPos, DaggerfallInterior.InteriorMarkerTypes.Rest, true);
                                            if (startPos == Vector3.zero)
                                                GameManager.Instance.PlayerEnterExit.Interior.FindMarker(out startPos, DaggerfallInterior.InteriorMarkerTypes.Enter, true);
                                            playerObject.transform.position = startPos;
                                            GameManager.Instance.PlayerMotor.FixStanding();

                                            RoomRental_v1 rentedRoom = GameManager.Instance.PlayerEntity.GetRentedRoom(GameManager.Instance.PlayerGPS.CurrentMapID, db.buildingKey);

                                            ulong seconds = 0;
                                            int minutes = 0;
                                            int hours = 0;
                                            if (time.Hour < 8)
                                                hours = 8 - time.Hour;
                                            else
                                                hours = 32 - time.Hour;

                                            if (time.Minute > 0)
                                            {
                                                minutes = 60 - time.Minute;
                                                hours -= 1;
                                            }
                                            seconds = (ulong)((DaggerfallDateTime.SecondsPerHour * hours) + (DaggerfallDateTime.SecondsPerMinute * minutes));
                                            RentRoom(db, rentedRoom, seconds);

                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        if (GameManager.Instance.PlayerEntity.LightSource != null)
                            GameManager.Instance.PlayerEntity.LightSource = null;
                    }
                    else
                    {

                        if (currentLocation.Summary.HasDungeon)
                        {
                            GameManager.Instance.StreamingWorld.SetAutoReposition(reposition, Vector3.zero);
                            //travelling through a dungeon
                        }
                        else
                        {
                            //travelling through a town
                            yield return new WaitForSeconds(0.2f);

                            if (surprised)
                            {
                                Debug.Log("WE WAS SUPRIZED!");
                                //get a door to a random tavern and put player in front of it
                                List<BuildingSummary> buildingSummaries = GameManager.Instance.StreamingWorld.GetCurrentBuildingDirectory().GetBuildingsOfType(DFLocation.BuildingTypes.Tavern);

                                if (hasResidences)
                                {
                                    //if residences are installed, then there's a chance there is no tavern in location
                                    if (buildingSummaries.Count < 1)
                                    {
                                        Debug.Log("FAST TRAVEL ENCOUNTERS - NO TAVERNS FOUND! LOOKING FOR A HOUSE1!");
                                        buildingSummaries = GameManager.Instance.StreamingWorld.GetCurrentBuildingDirectory().GetBuildingsOfType(DFLocation.BuildingTypes.House1);
                                    }
                                    if (buildingSummaries.Count < 1)
                                    {
                                        Debug.Log("FAST TRAVEL ENCOUNTERS - NO HOUSE2 FOUND! LOOKING FOR A HOUSE2!");
                                        buildingSummaries = GameManager.Instance.StreamingWorld.GetCurrentBuildingDirectory().GetBuildingsOfType(DFLocation.BuildingTypes.House2);
                                    }
                                    if (buildingSummaries.Count < 1)
                                    {
                                        Debug.Log("FAST TRAVEL ENCOUNTERS - NO HOUSE2 FOUND! LOOKING FOR A HOUSE3!");
                                        buildingSummaries = GameManager.Instance.StreamingWorld.GetCurrentBuildingDirectory().GetBuildingsOfType(DFLocation.BuildingTypes.House3);
                                    }
                                }

                                BuildingSummary building = buildingSummaries[UnityEngine.Random.Range(0, buildingSummaries.Count)];
                                DaggerfallStaticDoors tavernDoors = null;
                                List<StaticDoor> tavernDoorList = new List<StaticDoor>();
                                foreach (DaggerfallStaticDoors staticDoors in currentLocation.StaticDoorCollections)
                                {
                                    foreach (StaticDoor staticDoor in staticDoors.Doors)
                                    {
                                        if (staticDoor.buildingKey == building.buildingKey)
                                        {
                                            Debug.Log("FAST TRAVEL ENCOUNTERS - FOUND A STATIC DOOR BELONGING TO THE TAVERN!");
                                            if (tavernDoors == null)
                                                tavernDoors = staticDoors;

                                            tavernDoorList.Add(staticDoor);
                                        }
                                    }

                                    if (tavernDoors != null)
                                        break;
                                }

                                StaticDoor door = tavernDoorList[UnityEngine.Random.Range(0, tavernDoorList.Count)];
                                startPos = tavernDoors.transform.position + DaggerfallStaticDoors.GetDoorPosition(door) + (DaggerfallStaticDoors.GetDoorNormal(door) * 3);

                                playerObject.transform.position = startPos;
                                GameManager.Instance.PlayerMouseLook.SetHorizontalFacing(DaggerfallStaticDoors.GetDoorNormal(door));
                            }
                            else
                                GameManager.Instance.StreamingWorld.SetAutoReposition(reposition, Vector3.zero);
                        }
                    }
                }
                else
                {
                    float halfTerrain = 819.20f / 2;
                    Vector3 center = new Vector3(playerObject.transform.position.x + halfTerrain, playerObject.transform.position.y + 400, playerObject.transform.position.z + halfTerrain);

                    if (isRoad)
                    {
                        RoadData roadData = BasicRoadsUtils.GetRoadData(pos.X, pos.Y);

                        if (isNight)
                        {
                            Vector3 offset = GetDirectionFromRoad(BasicRoadsUtils.GetRoadDirections(roadData), false).normalized * 15f;

                            //reposition player to center-ish
                            Ray ray = new Ray(center + new Vector3(offset.x, 0, offset.y), Vector3.down);
                            RaycastHit hit = new RaycastHit();
                            if (Physics.Raycast(ray, out hit, 800f))
                                startPos = hit.point + (Vector3.up * 0.9f);
                            else
                                startPos = center;

                            playerObject.transform.position = startPos;

                            //set the player to look towards the center
                            GameManager.Instance.PlayerMouseLook.SetHorizontalFacing((center - startPos).normalized);

                            yield return new WaitForSeconds(1f);

                            //Dismount the player
                            if (transportManager.TransportMode != TransportModes.Foot)
                                transportManager.TransportMode = TransportModes.Foot;
                        }
                        else
                        {
                            Vector2Int dir2D = new Vector2Int(lastDestinationPosition.X, lastDestinationPosition.Y) - new Vector2Int(pos.X, pos.Y);
                            Vector3 dir = new Vector3(dir2D.x, 0, dir2D.y).normalized;
                            Vector3 offset = GetClosestDirection(BasicRoadsUtils.GetRoadDirections(roadData), dir);
                            //reposition player to center-ish
                            Ray ray = new Ray(center, Vector3.down);
                            RaycastHit hit = new RaycastHit();
                            if (Physics.Raycast(ray, out hit, 800f))
                                startPos = hit.point + (Vector3.up * 0.9f);
                            else
                                startPos = center;
                            playerObject.transform.position = startPos;

                            //set the player to look down the road
                            GameManager.Instance.PlayerMouseLook.SetHorizontalFacing(offset);
                        }
                    }
                    else
                    {
                        //reposition player to center-ish
                        Ray ray = new Ray(new Vector3(playerObject.transform.position.x + halfTerrain, playerObject.transform.position.y + 400, playerObject.transform.position.z + halfTerrain), Vector3.down);
                        RaycastHit hit = new RaycastHit();
                        if (Physics.Raycast(ray, out hit, 800f))
                            startPos = hit.point + (Vector3.up * 0.9f);
                        else
                            startPos = center;
                        playerObject.transform.position = startPos;

                        yield return new WaitForSeconds(1f);

                        if (isNight)
                        {
                            //Dismount the player
                            if (transportManager.TransportMode != TransportModes.Foot)
                                transportManager.TransportMode = TransportModes.Foot;
                        }
                    }
                }

                yield return new WaitForSeconds(0.2f);

                //if night and in the wilderness or in bad weather, spawn camp stuff
                if (isNight)
                    SpawnCamp();
                else if ((int)GameManager.Instance.WeatherManager.PlayerWeather.WeatherType > 2)
                {
                    //equip a light source
                    if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, (int)UselessItems2.Lantern))
                        GameManager.Instance.PlayerEntity.LightSource = GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.UselessItems2, (int)UselessItems2.Lantern);
                    else if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, (int)UselessItems2.Torch))
                        GameManager.Instance.PlayerEntity.LightSource = GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.UselessItems2, (int)UselessItems2.Torch);
                    else if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, (int)UselessItems2.Candle))
                        GameManager.Instance.PlayerEntity.LightSource = GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.UselessItems2, (int)UselessItems2.Candle);
                    else if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.ReligiousItems, (int)ReligiousItems.Holy_candle))
                        GameManager.Instance.PlayerEntity.LightSource = GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.ReligiousItems, (int)ReligiousItems.Holy_candle);
                }
                else
                {
                    if (GameManager.Instance.PlayerEntity.LightSource != null)
                        GameManager.Instance.PlayerEntity.LightSource = null;
                }

                yield return new WaitForSeconds(0.2f);
            }

            if (ClimatesAndCalories != null)
                fade = false;
            else
                DaggerfallUI.Instance.FadeBehaviour.AllowFade = true;

            DaggerfallUI.Instance.FadeBehaviour.FadeHUDFromBlack();

            yield return new WaitForSeconds(0.5f);

            //show message
            string message = "After travelling through " + GameManager.Instance.PlayerGPS.CurrentLocalizedRegionName + " for " + encounterDays.ToString();

            if (atSea)
                message = "After sailing across " + GameManager.Instance.PlayerGPS.CurrentLocalizedRegionName + " for " + encounterDays.ToString();

            if (encounterDays > 1)
                message += " days...";
            else
                message += " day...";

            ShowMessage(message, null, null);

            yield return new WaitForSeconds(0.1f);

            if (encounterPool == 2 && WarmAshesWilderness != null)
            {
                if (UnityEngine.Random.value > 0.5f)
                    WarmAshesEncounter(isNight, atSea, inHouse);
                else
                    VanillaEncounter(isNight, atSea, inHouse);
            }
            else if (encounterPool == 1 && WarmAshesWilderness != null)
                WarmAshesEncounter(isNight, atSea, inHouse);
            else
                VanillaEncounter(isNight, atSea, inHouse);

            if (RealisticWagonGameObject != null)
                RealisticWagonGameObject.SetActive(true);

            RescueHorseAndCart();

            canResume = true;
        }

        void WarmAshesEncounter(bool isNight, bool atSea, bool inHouse)
        {
            if (atSea)
            {
                VanillaEncounter(isNight, atSea, inHouse);
                Debug.Log("FAST TRAVEL ENCOUNTERS - NO WARM ASHES ENCOUNTER FOR SEAS");
                return;
            }

            int encounterRoll = Dice100.Roll();

            if (!GameManager.Instance.PlayerGPS.CurrentLocation.Loaded)
            {
                //is in wilderness or road
                if (isNight)
                {
                    //camping
                    if (encounterRoll < 20)
                        ModManager.Instance.SendModMessage(WarmAshesWilderness.Title, "summonNPC");
                    else if (encounterRoll < 60)
                        ModManager.Instance.SendModMessage(WarmAshesWilderness.Title, "summonQuest");
                    else
                        ModManager.Instance.SendModMessage(WarmAshesWilderness.Title, "summonFoe");
                }
                else
                {
                    //travelling
                    if (Dice100.Roll() < 80)
                        ModManager.Instance.SendModMessage(WarmAshesWilderness.Title, "summonDetail");

                    if (encounterRoll < 20)
                        ModManager.Instance.SendModMessage(WarmAshesWilderness.Title, "summonNPC");
                    else if (encounterRoll < 60)
                        ModManager.Instance.SendModMessage(WarmAshesWilderness.Title, "summonQuest");
                    else
                        ModManager.Instance.SendModMessage(WarmAshesWilderness.Title, "summonFoe");
                }
            }
            else
            {
                //is in dungeon or town
                if (isNight)
                {
                    if (inHouse)
                    {
                        //town night
                        ModManager.Instance.SendModMessage(WarmAshesWilderness.Title, "summonIntFoe");
                    }
                    else
                    {
                        //dungeon night
                        if (encounterRoll < 20)
                            ModManager.Instance.SendModMessage(WarmAshesWilderness.Title, "summonNPC");
                        else
                            ModManager.Instance.SendModMessage(WarmAshesWilderness.Title, "summonFoe");
                    }
                }
                else
                {
                    //travelling
                    if (GameManager.Instance.PlayerGPS.CurrentLocation.HasDungeon)
                    {
                        //dungeon day
                        if (encounterRoll < 20)
                            ModManager.Instance.SendModMessage(WarmAshesWilderness.Title, "summonNPC");
                        else
                            ModManager.Instance.SendModMessage(WarmAshesWilderness.Title, "summonFoe");
                    }
                    else
                    {
                        //town day
                        if (encounterRoll < 20)
                            ModManager.Instance.SendModMessage(WarmAshesWilderness.Title, "summonNPC");
                        else
                            ModManager.Instance.SendModMessage(WarmAshesWilderness.Title, "summonFoe");
                    }
                }
            }
        }

        void VanillaEncounter(bool isNight, bool atSea, bool inHouse)
        {
            //show message
            string line2 = "";
            string line3 = "";

            string locationName = "";
            if (GameManager.Instance.PlayerGPS.CurrentLocation.Loaded)
            {
                string firstWord = GameManager.Instance.PlayerGPS.CurrentLocation.Name.Split(' ')[0];
                if (firstWord == "Ruins")
                    locationName = "The " + GameManager.Instance.PlayerGPS.CurrentLocation.Name;
                else
                    locationName = GameManager.Instance.PlayerGPS.CurrentLocation.Name;
            }

            if (surprised)
            {
                if (atSea)
                {
                    line2 = "...your vessel was surreptitiously boarded";
                    line3 = "by an unknown danger...";
                }
                else if (inHouse)
                {
                    line2 = "...a loud commotion from nearby";
                    line3 = "rudely shocks you awake...";
                }
                else
                {
                    line2 = "...a loud cry alerts you to the danger";
                    if (isNight)
                    {
                        if (GameManager.Instance.PlayerGPS.CurrentLocation.Loaded)
                            line3 = "surrounding " + locationName + "...";
                        else
                            line3 = "surrounding your camp...";
                    }
                    else
                    {
                        if (GameManager.Instance.PlayerGPS.CurrentLocation.Loaded)
                        {
                            if (GameManager.Instance.PlayerGPS.CurrentLocation.HasDungeon)
                                line3 = "laying in wait around " + locationName + "...";
                            else
                                line3 = "flooding the streets of " + locationName + "...";
                        }
                        else
                            line3 = "all around you...";
                    }
                }
            }
            else
            {
                if (atSea)
                {
                    line2 = "...a lengthy pursuit results in battle";
                    line3 = "as attackers clamber aboard your vessel...";
                }
                else if (inHouse)
                {
                    line2 = "...you awaken as some trouble begins";
                    line3 = "to stir elsewhere in the building...";
                }
                else
                {
                    line2 = "...you see furtive shapes in the distance";
                    if (isNight)
                    {
                        if (GameManager.Instance.PlayerGPS.CurrentLocation.Loaded)
                            line3 = "approaching " + locationName + "...";
                        else
                            line3 = "approaching your camp...";
                    }
                    else
                    {
                        if (GameManager.Instance.PlayerGPS.CurrentLocation.Loaded)
                            line3 = "as you approach " + locationName + "...";
                        else
                            line3 = "heading your way...";
                    }
                }
            }

            ShowMessage(line2, line3, null);

            //if travel is cautious place enemies ahead of the player with a smaller radius
            float distance = 20f;
            float radius = 5f;

            if (atSea)
            {
                distance = 15f;
                radius = 2f;
            }

            //vanilla encounter starts here

            int level = levelWorld > 0 ? levelWorld : GameManager.Instance.PlayerEntity.Level + levelAdjustment;
            if (level < 1)
                level = 1;
            if (level > 30)
                level = 30;
            Debug.Log("FAST TRAVEL ENCOUNTERS - ENCOUNTER IS SCALED TO " + level.ToString());

            int levelMod = Mathf.CeilToInt((float)level / (float)2);
            int enemyCount = UnityEngine.Random.Range(Mathf.CeilToInt((float)levelMod/2f), levelMod + 1);

            if (vanillaEncounterGroup)
            {
                MobileTypes[] enemy = RandomEncounterTables.BuildEncounter(enemyCount,atSea);

                Debug.Log("FAST TRAVEL ENCOUNTERS - ENCOUNTER GROUP CONTAINS " + enemy.Length.ToString());

                Vector3 spawnPoint = GameManager.Instance.PlayerObject.transform.position;

                if (GameManager.Instance.PlayerEnterExit.IsPlayerInside)
                {
                    radius = 1f;

                    if (surprised)
                    {
                        CreateFoeSpawner(GameManager.Instance.PlayerObject.transform, false, enemy, radius, radius * 10);
                    }
                    else
                    {
                        GameManager.Instance.PlayerEnterExit.Interior.FindLowestOuterInteriorDoor(out spawnPoint, out Vector3 doorNormal);
                        spawnPoint += (doorNormal * 3f);
                        CreateFoeSpawner(spawnPoint, false, enemy, radius, radius * 10);
                    }
                }
                else
                {
                    spawnPoint = GameManager.Instance.PlayerObject.transform.position + (GameManager.Instance.PlayerObject.transform.forward * distance);

                    float height = atSea ? 5 : 100;
                    Ray ray = new Ray(spawnPoint + Vector3.up * height, Vector3.down);
                    RaycastHit hit = new RaycastHit();
                    if (Physics.Raycast(ray, out hit, 200))
                        spawnPoint = hit.point + (Vector3.up * 0.9f);

                    //if surprised, place enemies around the player with a bigger radius
                    if (surprised)
                    {
                        spawnPoint = GameManager.Instance.PlayerObject.transform.position;
                        radius = 10f;
                    }

                    CreateFoeSpawner(spawnPoint, false, enemy, radius, radius * 2);
                }
            }
            else
            {
                MobileTypes enemy = ChooseRandomEnemy(false);

                //reduce enemy count if enemy is a "boss" or "elite" or classed
                //double enemy count if enemy is a minion
                if (Array.Exists(bosses, x => x == enemy))
                    enemyCount = Mathf.CeilToInt((float)enemyCount / 4);
                else if (((int)enemy > 127 && (int)enemy < 147) || Array.Exists(elites, x => x == enemy))
                    enemyCount = Mathf.CeilToInt((float)enemyCount / 2);
                else if (Array.Exists(minions, x => x == enemy))
                    enemyCount = enemyCount * 2;

                List<MobileTypes> enemyList = new List<MobileTypes>();
                for (int i = 0; i < enemyCount; i++)
                    enemyList.Add(enemy);

                Vector3 spawnPoint = GameManager.Instance.PlayerObject.transform.position;

                if (GameManager.Instance.PlayerEnterExit.IsPlayerInside)
                {
                    radius = 1f;

                    if (surprised)
                    {
                        CreateFoeSpawner(GameManager.Instance.PlayerObject.transform, false, enemyList.ToArray(), radius, radius * 10);
                    }
                    else
                    {
                        GameManager.Instance.PlayerEnterExit.Interior.FindLowestOuterInteriorDoor(out spawnPoint, out Vector3 doorNormal);
                        spawnPoint += (doorNormal * 3f);
                        CreateFoeSpawner(spawnPoint, false, enemyList.ToArray(), radius, radius * 10);
                    }
                }
                else
                {
                    spawnPoint = GameManager.Instance.PlayerObject.transform.position + (GameManager.Instance.PlayerObject.transform.forward * distance);

                    float height = atSea ? 5 : 100;
                    Ray ray = new Ray(spawnPoint + Vector3.up * height, Vector3.down);
                    RaycastHit hit = new RaycastHit();
                    if (Physics.Raycast(ray, out hit, 200))
                        spawnPoint = hit.point + (Vector3.up * 0.9f);

                    //if surprised, place enemies around the player with a bigger radius
                    if (surprised)
                    {
                        spawnPoint = GameManager.Instance.PlayerObject.transform.position;
                        radius = 10f;
                    }

                    CreateFoeSpawner(spawnPoint, false, enemyList.ToArray(), radius, radius * 2);
                }
            }
        }

        void PlacePlayerInsideBuilding()
        {

        }

        void ShowMessage(string line1, string line2, string line3)
        {
            List<string> strings = new List<string>();
            if (line1 != null)
                strings.Add(line1);
            if (line2 != null)
                strings.Add(line2);
            if (line3 != null)
                strings.Add(line3);

            if (strings.Count > 0)
            {
                TextFile.Token[] texts = DaggerfallUnity.Instance.TextProvider.CreateTokens(TextFile.Formatting.JustifyCenter, strings.ToArray());
                DaggerfallUI.MessageBox(texts);
            }
        }

        void SpawnCamp()
        {
            if (GameManager.Instance.PlayerEnterExit.IsPlayerInside)
                return;

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

            if (ClimatesAndCalories != null)
            {
                //fireObject.GetComponentInChildren<Collider>().enabled = false;

                GameObject fireObjectCC = GameObjectHelper.CreateDaggerfallBillboardGameObject(210, 1, GameObjectHelper.GetBestParent());
                fireObjectCC.transform.position = firePos + ((Vector3.up * -0.3f));
                fireObjectCC.GetComponent<Renderer>().enabled = false;
                objectsSpawned.Add(fireObjectCC);
            }

            //add sound
            DaggerfallAudioSource fireAudioSource = fireObject.AddComponent<DaggerfallAudioSource>();
            fireAudioSource.AudioSource.dopplerLevel = 0;
            fireAudioSource.AudioSource.rolloffMode = AudioRolloffMode.Linear;
            fireAudioSource.AudioSource.maxDistance = 5f;
            fireAudioSource.AudioSource.volume = 0.7f;
            fireAudioSource.SetSound(SoundClips.Burning, AudioPresets.LoopIfPlayerNear);

            /*//Don't spawn tent if pixel has location
            if (GameManager.Instance.PlayerGPS.CurrentLocation.Loaded)
                return;*/

            //tent
            tentMatrix = playerTransform.localToWorldMatrix;
            uint tentModelID = tentModelIDs[tentModelIndex];
            GameObject tentObject = MeshReplacement.ImportCustomGameobject(tentModelID, GameObjectHelper.GetBestParent(), tentMatrix);
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

            if (GameManager.Instance.PlayerEntity.LightSource != null)
                GameManager.Instance.PlayerEntity.LightSource = null;
        }

        /// <summary>
        /// Collect markers inside a building.
        /// </summary>
        Vector3[] GetMarkers(Vector3 refPos)
        {
            List<Vector3> markersList = new List<Vector3>();

            DaggerfallBillboard[] billboards = FindObjectsOfType<DaggerfallBillboard>();

            foreach (DaggerfallBillboard billboard in billboards)
            {
                //add random flats, quests and entrances
                if (billboard.name == "DaggerfallBillboard [TEXTURE.199, Index=20]" || billboard.name == "DaggerfallBillboard [TEXTURE.199, Index=11]" || billboard.name == "DaggerfallBillboard [TEXTURE.199, Index=8]")
                    markersList.Add(billboard.transform.position);
            }

            //sort by distance to reference position?
            markersList.Sort((x, y) => { return (refPos - x).sqrMagnitude.CompareTo((refPos - y).sqrMagnitude); });

            return markersList.ToArray();
        }

        Vector3[] GetShipSpawnMarkers()
        {
            List<Vector3> markersList = new List<Vector3>();

            DaggerfallBillboard[] billboards = FindObjectsOfType<DaggerfallBillboard>();

            foreach (DaggerfallBillboard billboard in billboards)
            {
                //add random flats, quests and entrances
                if (billboard.name == "DaggerfallBillboard [TEXTURE.210, Index=26]" || billboard.name == "DaggerfallBillboard [TEXTURE.210, Index=27]")
                    markersList.Add(billboard.transform.position);
            }

            return markersList.ToArray();
        }

        public static MobileTypes ChooseRandomEnemy(bool chooseUnderWaterEnemy)
        {
            int encounterTableIndex = 0;
            PlayerEnterExit playerEnterExit = GameManager.Instance.PlayerEnterExit;
            PlayerGPS playerGPS = GameManager.Instance.PlayerGPS;

            if (!playerEnterExit || !playerGPS)
                return MobileTypes.None;

            int climate = playerGPS.CurrentClimateIndex;
            bool isDay = DaggerfallUnity.Instance.WorldTime.Now.IsDay;

            if (playerGPS.IsPlayerInLocationRect)
            {
                // Player in location rectangle
                switch (climate)
                {
                    case (int)MapsFile.Climates.Desert:
                    case (int)MapsFile.Climates.Desert2:
                        encounterTableIndex = 20;
                        break;
                    case (int)MapsFile.Climates.Mountain:
                        encounterTableIndex = 23;
                        break;
                    case (int)MapsFile.Climates.Rainforest:
                        encounterTableIndex = 26;
                        break;
                    case (int)MapsFile.Climates.Subtropical:
                        encounterTableIndex = 29;
                        break;
                    case (int)MapsFile.Climates.Swamp:
                    case (int)MapsFile.Climates.MountainWoods:
                    case (int)MapsFile.Climates.Woodlands:
                        encounterTableIndex = 32;
                        break;
                    case (int)MapsFile.Climates.HauntedWoodlands:
                        encounterTableIndex = 35;
                        break;

                    default:
                        encounterTableIndex = UnityEngine.Random.Range(39, 45);
                        break;
                }
            }
            else
            {
                if (isDay)
                {
                    // Player not in location rectangle, day
                    switch (climate)
                    {
                        case (int)MapsFile.Climates.Desert:
                        case (int)MapsFile.Climates.Desert2:
                            encounterTableIndex = 21;
                            break;
                        case (int)MapsFile.Climates.Mountain:
                            encounterTableIndex = 24;
                            break;
                        case (int)MapsFile.Climates.Rainforest:
                            encounterTableIndex = 27;
                            break;
                        case (int)MapsFile.Climates.Subtropical:
                            encounterTableIndex = 30;
                            break;
                        case (int)MapsFile.Climates.Swamp:
                        case (int)MapsFile.Climates.MountainWoods:
                        case (int)MapsFile.Climates.Woodlands:
                            encounterTableIndex = 33;
                            break;
                        case (int)MapsFile.Climates.HauntedWoodlands:
                            encounterTableIndex = 36;
                            break;

                        default:
                            encounterTableIndex = UnityEngine.Random.Range(39, 45);
                            break;
                    }
                }
                else
                {
                    // Player not in location rectangle, night
                    switch (climate)
                    {
                        case (int)MapsFile.Climates.Desert:
                        case (int)MapsFile.Climates.Desert2:
                            encounterTableIndex = 22;
                            break;
                        case (int)MapsFile.Climates.Mountain:
                            encounterTableIndex = 25;
                            break;
                        case (int)MapsFile.Climates.Rainforest:
                            encounterTableIndex = 28;
                            break;
                        case (int)MapsFile.Climates.Subtropical:
                            encounterTableIndex = 31;
                            break;
                        case (int)MapsFile.Climates.Swamp:
                        case (int)MapsFile.Climates.MountainWoods:
                        case (int)MapsFile.Climates.Woodlands:
                            encounterTableIndex = 34;
                            break;
                        case (int)MapsFile.Climates.HauntedWoodlands:
                            encounterTableIndex = 37;
                            break;
                        case (int)MapsFile.Climates.Ocean:
                            encounterTableIndex = UnityEngine.Random.Range(39, 45);
                            break;

                        default:
                            encounterTableIndex = UnityEngine.Random.Range(39, 45);
                            break;
                    }
                }
            }

            int roll = Dice100.Roll();
            int playerLevel = GameManager.Instance.PlayerEntity.Level;
            int min;
            int max;

            // Random/player level based adjustments from classic. These assume enemy lists of length 20.
            if (roll > 80)
            {
                if (roll > 95)
                {
                    if (playerLevel <= 5)
                    {
                        min = 0;
                        max = playerLevel + 2;
                    }
                    else
                    {
                        min = 0;
                        max = 19;
                    }
                }
                else
                {
                    min = 0;
                    max = playerLevel + 1;
                }
            }
            else
            {
                min = playerLevel - 3;
                max = playerLevel + 3;
            }
            if (min < 0)
            {
                min = 0;
                max = 5;
            }
            if (max > 19)
            {
                min = 14;
                max = 19;
            }

            RandomEncounterTable encounterTable = RandomEncounters.EncounterTables[encounterTableIndex];

            // Adding a check here (not in classic) for lists of shorter length than 20
            if (max + 1 > encounterTable.Enemies.Length)
            {
                max = encounterTable.Enemies.Length - 1;
                if (max >= 5)
                    min = max - 5;
                else
                    min = UnityEngine.Random.Range(0, max);
            }

            return encounterTable.Enemies[UnityEngine.Random.Range(min, max + 1)];
        }

        public class FastTravelEncountersSaveData : IHasModSaveData
        {
            public bool tempShip;

            public Type SaveDataType
            {
                get
                {
                    return typeof(FastTravelEncountersSaveData);
                }
            }

            public object NewSaveData()
            {
                FastTravelEncountersSaveData emptyData = new FastTravelEncountersSaveData();
                emptyData.tempShip = false;
                return emptyData;
            }
            public object GetSaveData()
            {
                FastTravelEncountersSaveData data = new FastTravelEncountersSaveData();
                data.tempShip = Instance.shipTemporary;
                return data;
            }

            public void RestoreSaveData(object dataIn)
            {
                FastTravelEncountersSaveData data = (FastTravelEncountersSaveData)dataIn;
                Instance.shipTemporary = tempShip;
            }
        }

        //CUSTOM FOE SPAWNER
        /// <summary>
        /// Create a new foe spawner.
        /// The spawner will self-destroy once it has emitted foes into world around player.
        /// </summary>
        /// <param name="lineOfSightCheck">Should spawner try to place outside of player's field of view.</param>
        /// <param name="foeType">Type of foe to spawn.</param>
        /// <param name="spawnCount">Number of duplicate foes to spawn.</param>
        /// <param name="minDistance">Minimum distance from player.</param>
        /// <param name="maxDistance">Maximum distance from player.</param>
        /// <param name="parent">Parent GameObject. If none specified the most suitable parent will be selected automatically.</param>
        /// <returns>FoeSpawner GameObject.</returns>
        public GameObject CreateFoeSpawner(Vector3 origin, bool lineOfSightCheck, MobileTypes[] foeTypes, float minDistance = 4, float maxDistance = 20, Transform parent = null, bool alliedToPlayer = false)
        {
            // Create new foe spawner
            GameObject go = new GameObject();
            FoeSpawner spawner = go.AddComponent<FoeSpawner>();
            spawner.LineOfSightCheck = lineOfSightCheck;
            spawner.FoeTypes = foeTypes;
            spawner.MinDistance = minDistance;
            spawner.MaxDistance = maxDistance;
            spawner.Parent = parent;
            spawner.AlliedToPlayer = alliedToPlayer;

            spawner.origin = origin;

            // Assign position on top of player
            // Spawner can be placed anywhere to work, but rest system considers a spawner to be an enemy "in potentia" for purposes of breaking rest and travel
            // Placing spawner on player at moment of creation will trigger the nearby enemy check even while spawn is pending
            spawner.transform.position = GameManager.Instance.PlayerObject.transform.position;

            return go;
        }
        public GameObject CreateFoeSpawner(Transform origin, bool lineOfSightCheck, MobileTypes[] foeTypes, float minDistance = 4, float maxDistance = 20, Transform parent = null, bool alliedToPlayer = false)
        {
            // Create new foe spawner
            GameObject go = new GameObject();
            FoeSpawner spawner = go.AddComponent<FoeSpawner>();
            spawner.LineOfSightCheck = lineOfSightCheck;
            spawner.FoeTypes = foeTypes;
            spawner.MinDistance = minDistance;
            spawner.MaxDistance = maxDistance;
            spawner.Parent = parent;
            spawner.AlliedToPlayer = alliedToPlayer;

            spawner.origin = origin.position;

            // Assign position on top of player
            // Spawner can be placed anywhere to work, but rest system considers a spawner to be an enemy "in potentia" for purposes of breaking rest and travel
            // Placing spawner on player at moment of creation will trigger the nearby enemy check even while spawn is pending
            spawner.transform.position = GameManager.Instance.PlayerObject.transform.position;

            return go;
        }

        //BASIC ROADS
        public Vector3 GetDirectionFromRoad(List<Vector3> directions, bool road = true)
        {
            Vector3 direction = Vector3.up;

            if (road)
            {
                direction = directions[UnityEngine.Random.Range(0, directions.Count)];
            }
            else
            {
                List<Vector3> antiDirections = new List<Vector3>();

                foreach (Vector3 vector in vectors)
                {
                    if (!directions.Contains(vector))
                        antiDirections.Add(vector);
                }

                if (antiDirections.Count > 0)
                    direction = antiDirections[UnityEngine.Random.Range(0, antiDirections.Count)];
            }

            return direction;
        }
        public Vector3 GetClosestDirection(List<Vector3> directions, Vector3 vector)
        {
            Vector3 direction = Vector3.up;

            float currentAngle = 0;
            float closestAngle = Mathf.Infinity;
            foreach (Vector3 dir in directions)
            {
                currentAngle = Vector3.Angle(vector, dir);
                if (currentAngle < closestAngle)
                {
                    direction = dir;
                    closestAngle = currentAngle;
                }
            }

            return direction;
        }
    }
}
