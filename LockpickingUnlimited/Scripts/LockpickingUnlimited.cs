using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.Banking;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using Wenzil.Console;

namespace LockpickingUnlimitedMod
{
    public class LockpickingUnlimited : MonoBehaviour
    {
        private static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<LockpickingUnlimited>();

            mod.SaveDataInterface = new LockpickingUnlimitedSaveData();

            mod.IsReady = true;
        }

        public static LockpickingUnlimited Instance;

        Camera eye;
        LayerMask layerMask;
        float activationDistance;
        Rect screenRect;

        public List<DaggerfallActionDoor> actionDoors = new List<DaggerfallActionDoor>();
        public List<StaticBuilding> staticDoors = new List<StaticBuilding>();

        //list of disarmed traps to persist between saves while in the same dungeon
        //detection
        public bool highlighting = false;
        List<GameObject> flaggedObjects = new List<GameObject>();
        public List<DaggerfallActionDoor> masterHiddenDoors = new List<DaggerfallActionDoor>();
        public List<DaggerfallAction> masterHiddenSwitches = new List<DaggerfallAction>();
        public List<DaggerfallAction> masterTraps = new List<DaggerfallAction>();
        public List<ulong> flaggedLoadIDs = new List<ulong>();
        public List<ulong> disarmedLoadIDs = new List<ulong>();

        public static int itemThievesToolsTemplateIndex = 1340;

        //timer
        public float resetTime = 1;
        float resetTimer;
        public bool resetRequireTools = true;
        public bool resetAlert = false;
        public int resetAlertChanceMod = 0;
        public bool resetAlertVanillaFormula = false;
        public bool resetAlertMessage = false;

        //tools
        public int toolDamageMod = 5;

        //dispel
        public bool dispel = true;
        public bool dispelRequireTools = true;
        public int dispelLevel = 95;
        public int dispelChance = 0;

        //disarm
        public bool disarm = true;
        public bool disarmRequireTools = true;
        public bool disarmAlwaysTrigger = true;
        public int disarmChance = 0;

        //detection
        public bool detect = true;
        public bool detectSpeed = true;
        public int detectChance = 0;
        public float detectRange = 16;
        public KeyCode detectKeyCode = KeyCode.None;
        public bool detectHighlightToggle = true;
        public bool detectHighlightMessage = true;
        public float detectHighlightDuration = 2;
        public Color detectHighlightColor = Color.red;

        //bash
        bool bashOpen = false;
        bool bashDamage = false;
        bool bashAlert = false;
        bool bashReset = false;
        bool bashLockedOnly = false;
        FPSWeapon fpsWeapon;
        bool hitframe = false;
        int bashChanceMod = 0;
        int bashAlertChanceMod = 0;
        bool bashAlertMessage = false;
        int bashDamageMod = 5;

        //locking
        public bool locked = false;
        public int lockChance = 50;
        public bool lockNPCsHide = true;
        IEnumerator locking;

        //mod compatibility
        bool lockpickIsTool;

        MethodInfo ActivateStaticDoor;

        private void Start()
        {
            if (Instance == null)
                Instance = this;

            eye = GameManager.Instance.MainCamera;
            layerMask = ~(1 << LayerMask.NameToLayer("Player"));
            layerMask = layerMask & ~(1 << LayerMask.NameToLayer("Ignore Raycast"));
            activationDistance = PlayerActivate.DoorActivationDistance;

            fpsWeapon = GameManager.Instance.RightHandWeapon;

            DaggerfallUnity.Instance.ItemHelper.RegisterCustomItem(itemThievesToolsTemplateIndex, ItemGroups.UselessItems2, typeof(ItemThievesTools));

            PlayerEnterExit.OnTransitionDungeonInterior += OnTransitionDungeonInterior;
            PlayerEnterExit.OnTransitionInterior += OnTransitionInterior;
            PlayerEnterExit.OnTransitionDungeonExterior += OnTransitionExterior;
            PlayerEnterExit.OnTransitionExterior += OnTransitionExterior;
            SaveLoadManager.OnLoad += OnLoad;

            EntityEffectBroker.OnNewMagicRound += OnNewMagicRound;

            //register console commands
            ConsoleCommandsDatabase.RegisterCommand(GiveMeTools.name, GiveMeTools.description, GiveMeTools.usage, GiveMeTools.Execute);

            ActivateStaticDoor = GameManager.Instance.PlayerActivate.GetType().GetMethod("ActivateStaticDoor", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.Instance);
            if (ActivateStaticDoor != null)
                Debug.Log("LOCKPICKING UNLIMITED - FOUND ACTIVATESTATICDOOR METHOD");

            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();
        }
        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("Reset"))
            {
                resetTime = settings.GetValue<float>("Reset", "Interval");
                resetRequireTools = settings.GetValue<bool>("Reset", "RequireTools");
                resetAlert = settings.GetValue<bool>("Reset", "Alert");
                resetAlertChanceMod = settings.GetValue<int>("Reset", "BaseAlertChance");
                resetAlertVanillaFormula = settings.GetValue<bool>("Reset", "VanillaAlertFormula");
                resetAlertMessage = settings.GetValue<bool>("Reset", "ShowAlertMessage");
            }
            if (change.HasChanged("Dispel"))
            {
                dispel = settings.GetValue<bool>("Dispel", "Enable");
                dispelLevel = settings.GetValue<int>("Dispel", "SkillRequirement");
                dispelChance = settings.GetValue<int>("Dispel", "BaseChance");
                dispelRequireTools = settings.GetValue<bool>("Dispel", "RequireTools");
            }
            if (change.HasChanged("Disarm"))
            {
                disarm = settings.GetValue<bool>("Disarm", "Enable");
                disarmChance = settings.GetValue<int>("Disarm", "BaseChance");
                disarmRequireTools = settings.GetValue<bool>("Disarm", "RequireTools");
                disarmAlwaysTrigger = settings.GetValue<bool>("Disarm", "AlwaysTriggerOnFail");
            }
            if (change.HasChanged("Detection"))
            {
                detect = settings.GetValue<bool>("Detection", "Enable");
                detectSpeed = settings.GetValue<bool>("Detection", "SpeedLimit");
                detectRange = ((float)settings.GetValue<int>("Detection", "Range") + 1f) * 8f;
                detectChance = settings.GetValue<int>("Detection", "BaseChance");
                detectKeyCode = SetKeyFromText(settings.GetValue<string>("Detection", "HighlightInput"));
                detectHighlightToggle = settings.GetValue<bool>("Detection", "ToggleHighlight");
                detectHighlightMessage = settings.GetValue<bool>("Detection", "ShowHighlightMessage");
                detectHighlightDuration = settings.GetValue<float>("Detection", "HighlightDuration");
                detectHighlightColor = settings.GetColor("Detection", "HighlightColor");
            }
            if (change.HasChanged("Bash"))
            {
                bashOpen = settings.GetValue<bool>("Bash", "Open");
                bashChanceMod = settings.GetValue<int>("Bash", "BaseOpenChance");
                bashAlert = settings.GetValue<bool>("Bash", "Alert");
                bashAlertChanceMod = settings.GetValue<int>("Bash", "BaseAlertChance");
                bashDamage = settings.GetValue<bool>("Bash", "Damage");
                bashDamageMod = settings.GetValue<int>("Bash", "DamageMultiplier");
                bashLockedOnly = settings.GetValue<bool>("Bash", "LockedOnly");
                bashReset = settings.GetValue<bool>("Bash", "ResetPickAttempt");
                bashAlertMessage = settings.GetValue<bool>("Bash", "ShowAlertMessage");
            }
            if (change.HasChanged("RandomLockedDoors"))
            {
                locked = settings.GetValue<bool>("RandomLockedDoors", "Enabled");
                lockChance = settings.GetValue<int>("RandomLockedDoors", "LockChance");
                lockNPCsHide = settings.GetValue<bool>("RandomLockedDoors", "HidePeopleFlats");
            }
            if (change.HasChanged("Thieves'Tools"))
            {
                toolDamageMod = settings.GetValue<int>("Thieves'Tools", "ConditionDamage");
            }
            if (change.HasChanged("Compatibility"))
            {
                lockpickIsTool = settings.GetValue<bool>("Compatibility", "SkulduggeryLockpick");
            }
        }

        private void Update()
        {
            if (GameManager.IsGamePaused ||
                InputManager.Instance.IsPaused ||
                SaveLoadManager.Instance.LoadInProgress)
                return;

            //run timer for reset
            if (actionDoors.Count > 0 || staticDoors.Count > 0)
            {
                if (resetTimer >= resetTime)
                {
                    ResetLockedDoors();
                    resetTimer = 0;
                }
                else
                    resetTimer += Time.deltaTime;
            }

            if (bashOpen || bashAlert || bashDamage || bashReset)
            {
                //check bash
                if (fpsWeapon.IsAttacking() && fpsWeapon.WeaponType != WeaponTypes.Bow)
                {
                    if (fpsWeapon.GetCurrentFrame() == 1 && hitframe)
                        hitframe = false;
                    else if (fpsWeapon.GetCurrentFrame() == fpsWeapon.GetHitFrame() && !hitframe)
                    {
                        //Debug.Log("LOCKPICKING UNLIMITED - PLAYER ATTACKED");
                        hitframe = true;

                        // Fire ray along player facing using weapon range
                        RaycastHit hit;
                        Ray ray = new Ray(eye.transform.position, eye.transform.forward);
                        if (Physics.SphereCast(ray, 0.25f, out hit, 2.5f, layerMask))
                        {
                            if (GameManager.Instance.PlayerEnterExit.IsPlayerInside)
                            {
                                // Check if hit has an DaggerfallActionDoor component
                                DaggerfallActionDoor actionDoor = hit.collider.gameObject.GetComponent<DaggerfallActionDoor>();
                                if (actionDoor)
                                {
                                    //Debug.Log("LOCKPICKING UNLIMITED - PLAYER BASHED");

                                    //custom bash on non-magically locked doors
                                    if ((!bashLockedOnly || actionDoor.CurrentLockValue > 0) && actionDoor.CurrentLockValue < 20)
                                    {
                                        PlayerEntity player = GameManager.Instance.PlayerEntity;
                                        PlayerEnterExit enterExit = GameManager.Instance.PlayerEnterExit;

                                        //reset failed attempt
                                        if (bashReset && actionDoor.CurrentLockValue > 0 && actionDoor.FailedSkillLevel > 0)
                                            actionDoor.FailedSkillLevel = 0;

                                        //start with vanilla bash chance value
                                        int bashChance = 20 - actionDoor.CurrentLockValue;

                                        //increase chance based on variables
                                        //character stats
                                        bashChance += Mathf.RoundToInt((float)(player.Stats.LiveStrength - 50) / 5f); //+-2% chance for every 10 STR above/below 50, 10% bonus at 100 STR
                                        bashChance += Mathf.RoundToInt((float)(player.Stats.LiveLuck - 50) / 10f); //+-1% chance for every 10 STR above/below 50, 5% bonus at 100 LUK

                                        //weapon
                                        DaggerfallUnityItem weapon = fpsWeapon.SpecificWeapon;
                                        if (weapon != null)
                                        {
                                            //using a weapon
                                            //weapon skills
                                            DaggerfallConnect.DFCareer.Skills weaponSkill = weapon.GetWeaponSkillID();
                                            bashChance += Mathf.RoundToInt((float)(player.Skills.GetLiveSkillValue(weaponSkill) - 50) / 5f); //+-2% chance for every 10% above/below 50&, 10% bonus at 100% weapon skill

                                            //weapon type
                                            switch (weaponSkill)
                                            {
                                                case DaggerfallConnect.DFCareer.Skills.Axe:
                                                    bashChance += 10;
                                                    break;
                                                case DaggerfallConnect.DFCareer.Skills.ShortBlade:
                                                    bashChance -= 10;
                                                    break;
                                            }

                                            switch (fpsWeapon.MetalType)
                                            {
                                                case MetalTypes.Adamantium:
                                                    bashChance += 10;
                                                    break;
                                                case MetalTypes.Silver:
                                                    bashChance -= 10;
                                                    break;
                                            }

                                            if (bashDamage)
                                            {
                                                //damage the weapon used in a bash
                                                int damage = -weapon.NativeMaterialValue + (actionDoor.CurrentLockValue * bashDamageMod);

                                                if (weaponSkill == DaggerfallConnect.DFCareer.Skills.Axe)
                                                    damage = Mathf.RoundToInt(damage * 0.5f);

                                                if (fpsWeapon.MetalType == MetalTypes.Adamantium)
                                                    damage = Mathf.RoundToInt(damage * 0.5f);

                                                if (damage < 1)
                                                    damage = 1;

                                                weapon.LowerCondition(damage);
                                                Debug.Log("LOCKPICKING UNLIMITED - " + weapon.shortName + " CONDITION IS " + weapon.currentCondition.ToString() + " out of " + weapon.maxCondition.ToString());
                                            }
                                        }
                                        else
                                        {
                                            //using unarmed
                                            bashChance += Mathf.RoundToInt((float)(player.Skills.GetLiveSkillValue(DaggerfallConnect.DFCareer.Skills.HandToHand) - 50) / 5f); //+-2% chance for every 10% above/below 50&, 10% bonus at 100% Hand-to-Hand skill

                                            if (bashDamage)
                                            {
                                                //damage the player when trying to punch/kick open a door
                                                int damage = actionDoor.CurrentLockValue * bashDamageMod;

                                                //equipped gloves or boots absorbs some of the damage scaling with material
                                                DaggerfallUnityItem gear = null;
                                                switch (fpsWeapon.WeaponState)
                                                {
                                                    case WeaponStates.StrikeUp:
                                                    case WeaponStates.StrikeDown:
                                                    case WeaponStates.StrikeLeft:
                                                        gear = player.ItemEquipTable.GetItem(EquipSlots.Feet);
                                                        break;
                                                    default:
                                                        gear = player.ItemEquipTable.GetItem(EquipSlots.Gloves);
                                                        break;
                                                }
                                                if (gear != null)
                                                {
                                                    damage = -gear.NativeMaterialValue + (actionDoor.CurrentLockValue * bashDamageMod);
                                                    if (damage < 1)
                                                        damage = 1;
                                                    gear.LowerCondition(damage);
                                                    Debug.Log("LOCKPICKING UNLIMITED - " + gear.shortName + " CONDITION IS " + gear.currentCondition.ToString() + " out of " + gear.maxCondition.ToString());
                                                }

                                                if (damage < 1)
                                                    damage = 1;

                                                player.EntityBehaviour.GetComponent<ShowPlayerDamage>().Flash();
                                                player.DecreaseHealth(damage);
                                            }
                                        }

                                        bashChance += bashChanceMod;

                                        //roll for bash to open the door
                                        if (bashOpen && bashChance > 0)
                                        {
                                            Debug.Log("LOCKPICKING UNLIMITED - BASHING CHANCE IS " + bashChance.ToString() + "%!");
                                            if (Dice100.SuccessRoll(bashChance))
                                            {
                                                actionDoor.CurrentLockValue = 0;
                                                actionDoor.ToggleDoor(true);
                                            }
                                        }

                                        if (bashAlert)
                                        {
                                            //chance that something happens when bashing
                                            int alertChance = 10;
                                            if (enterExit.IsPlayerInsideDungeon)
                                            {
                                                //alert chance based on dungeon activity
                                                switch (enterExit.Dungeon.Summary.DungeonType)
                                                {
                                                    case DaggerfallConnect.DFRegion.DungeonTypes.OrcStronghold:
                                                    case DaggerfallConnect.DFRegion.DungeonTypes.HumanStronghold:
                                                    case DaggerfallConnect.DFRegion.DungeonTypes.GiantStronghold:
                                                    case DaggerfallConnect.DFRegion.DungeonTypes.BarbarianStronghold:
                                                    case DaggerfallConnect.DFRegion.DungeonTypes.Prison:
                                                    case DaggerfallConnect.DFRegion.DungeonTypes.DesecratedTemple:
                                                    case DaggerfallConnect.DFRegion.DungeonTypes.Coven:
                                                    case DaggerfallConnect.DFRegion.DungeonTypes.VampireHaunt:
                                                    case DaggerfallConnect.DFRegion.DungeonTypes.Laboratory:
                                                        alertChance += 10;
                                                        break;
                                                    case DaggerfallConnect.DFRegion.DungeonTypes.Crypt:
                                                    case DaggerfallConnect.DFRegion.DungeonTypes.HarpyNest:
                                                    case DaggerfallConnect.DFRegion.DungeonTypes.SpiderNest:
                                                    case DaggerfallConnect.DFRegion.DungeonTypes.ScorpionNest:
                                                    case DaggerfallConnect.DFRegion.DungeonTypes.DragonsDen:
                                                        alertChance += 5;
                                                        break;
                                                    case DaggerfallConnect.DFRegion.DungeonTypes.Mine:
                                                    case DaggerfallConnect.DFRegion.DungeonTypes.NaturalCave:
                                                    case DaggerfallConnect.DFRegion.DungeonTypes.RuinedCastle:
                                                    case DaggerfallConnect.DFRegion.DungeonTypes.VolcanicCaves:
                                                    case DaggerfallConnect.DFRegion.DungeonTypes.Cemetery:
                                                        alertChance += 0;
                                                        break;
                                                    default:
                                                        alertChance += 0;
                                                        break;
                                                }
                                            }
                                            else if (enterExit.IsPlayerInsideBuilding)
                                            {
                                                //higher building quality increases alert chance
                                                alertChance += enterExit.BuildingDiscoveryData.quality;
                                            }

                                            alertChance += bashAlertChanceMod;

                                            //roll for alert
                                            if (alertChance > 0 && Dice100.SuccessRoll(alertChance))
                                            {
                                                if (enterExit.IsPlayerInsideDungeon)
                                                {
                                                    //spawn enemy nearby
                                                    if (bashAlertMessage)
                                                        DaggerfallUI.Instance.DaggerfallHUD.SetMidScreenText("Drawn by the noise, something approaches...");
                                                    GameObjectHelper.CreateFoeSpawner(false, RandomEncounters.ChooseRandomEnemy(false), 1, 4);
                                                }
                                                else if (enterExit.IsPlayerInsideBuilding)
                                                {
                                                    //make sure player isn't in a player-owned house or ship
                                                    int buildingKey = enterExit.BuildingDiscoveryData.buildingKey;
                                                    if (enterExit.BuildingType != DaggerfallConnect.DFLocation.BuildingTypes.Ship && !DaggerfallBankManager.IsHouseOwned(buildingKey))
                                                    {
                                                        //Alert the guards and flag the player with a crime
                                                        if (bashAlertMessage)
                                                            DaggerfallUI.Instance.DaggerfallHUD.SetMidScreenText("You hear guards rapidly approaching...");
                                                        if (!enterExit.IsPlayerInsideOpenShop)
                                                            player.CrimeCommitted = PlayerEntity.Crimes.Breaking_And_Entering;
                                                        else
                                                            player.CrimeCommitted = PlayerEntity.Crimes.Assault;
                                                        SpawnCityGuards();
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                StaticBuilding building = new StaticBuilding();
                                Transform buildingOwner;
                                DaggerfallStaticBuildings buildings = GameManager.Instance.PlayerActivate.GetBuildings(hit.transform, out buildingOwner);
                                if (buildings && buildings.HasHit(hit.point, out building))
                                {
                                    // Get building directory for location
                                    BuildingDirectory buildingDirectory = GameManager.Instance.StreamingWorld.GetCurrentBuildingDirectory();
                                    if (!buildingDirectory)
                                        return;
                                    // Get detailed building data from directory
                                    BuildingSummary buildingSummary;
                                    if (!buildingDirectory.GetBuildingSummary(building.buildingKey, out buildingSummary))
                                        return;

                                    if (!GameManager.Instance.PlayerActivate.BuildingIsUnlocked(buildingSummary))
                                    {
                                        // Check for a static door hit
                                        Transform doorOwner;
                                        DaggerfallStaticDoors doors = GameManager.Instance.PlayerActivate.GetDoors(hit.transform, out doorOwner);
                                        if (doors)
                                        {
                                            PlayerEntity player = GameManager.Instance.PlayerEntity;
                                            PlayerEnterExit playerGPS = GameManager.Instance.PlayerEnterExit;
                                            int lockLevel = GameManager.Instance.PlayerActivate.GetBuildingLockValue(buildingSummary);

                                            //start with vanilla bash chance value
                                            int bashChance = 20 - lockLevel;

                                            //increase chance based on variables
                                            //character stats
                                            bashChance += Mathf.RoundToInt((float)(player.Stats.LiveStrength - 50) / 5f); //+-2% chance for every 10 STR above/below 50, 10% bonus at 100 STR
                                            bashChance += Mathf.RoundToInt((float)(player.Stats.LiveLuck - 50) / 10f); //+-1% chance for every 10 STR above/below 50, 5% bonus at 100 LUK

                                            //reset failed attempt
                                            if (bashReset && GameManager.Instance.PlayerGPS.GetLastLockpickAttempt(building.buildingKey) > 0)
                                                GameManager.Instance.PlayerGPS.SetLastLockpickAttempt(building.buildingKey, 0);

                                            //weapon
                                            DaggerfallUnityItem weapon = fpsWeapon.SpecificWeapon;
                                            if (weapon != null)
                                            {
                                                //using a weapon
                                                //weapon skills
                                                DaggerfallConnect.DFCareer.Skills weaponSkill = weapon.GetWeaponSkillID();
                                                bashChance += Mathf.RoundToInt((float)(player.Skills.GetLiveSkillValue(weaponSkill) - 50) / 5f); //+-2% chance for every 10% above/below 50&, 10% bonus at 100% weapon skill

                                                //weapon type
                                                switch (weaponSkill)
                                                {
                                                    case DaggerfallConnect.DFCareer.Skills.Axe:
                                                        bashChance += 10;
                                                        break;
                                                    case DaggerfallConnect.DFCareer.Skills.ShortBlade:
                                                        bashChance -= 10;
                                                        break;
                                                }

                                                switch (fpsWeapon.MetalType)
                                                {
                                                    case MetalTypes.Adamantium:
                                                        bashChance += 10;
                                                        break;
                                                    case MetalTypes.Silver:
                                                        bashChance -= 10;
                                                        break;
                                                }

                                                if (bashDamage)
                                                {
                                                    //damage the weapon used in a bash
                                                    int damage = -weapon.NativeMaterialValue + (lockLevel * bashDamageMod);

                                                    if (weaponSkill == DaggerfallConnect.DFCareer.Skills.Axe)
                                                        damage = Mathf.RoundToInt(damage * 0.5f);

                                                    if (fpsWeapon.MetalType == MetalTypes.Adamantium)
                                                        damage = Mathf.RoundToInt(damage * 0.5f);

                                                    if (damage < 1)
                                                        damage = 1;

                                                    weapon.LowerCondition(damage);
                                                    Debug.Log("LOCKPICKING UNLIMITED - " + weapon.shortName + " CONDITION IS " + weapon.currentCondition.ToString() + " out of " + weapon.maxCondition.ToString());
                                                }
                                            }
                                            else
                                            {
                                                //using unarmed
                                                bashChance += Mathf.RoundToInt((float)(player.Skills.GetLiveSkillValue(DaggerfallConnect.DFCareer.Skills.HandToHand) - 50) / 5f); //+-2% chance for every 10% above/below 50&, 10% bonus at 100% Hand-to-Hand skill

                                                if (bashDamage)
                                                {
                                                    //damage the player when trying to punch/kick open a door
                                                    int damage = lockLevel * bashDamageMod;

                                                    //equipped gloves or boots absorbs some of the damage scaling with material
                                                    DaggerfallUnityItem gear = null;
                                                    switch (fpsWeapon.WeaponState)
                                                    {
                                                        case WeaponStates.StrikeUp:
                                                        case WeaponStates.StrikeDown:
                                                        case WeaponStates.StrikeLeft:
                                                            gear = player.ItemEquipTable.GetItem(EquipSlots.Feet);
                                                            break;
                                                        default:
                                                            gear = player.ItemEquipTable.GetItem(EquipSlots.Gloves);
                                                            break;
                                                    }
                                                    if (gear != null)
                                                    {
                                                        damage = -gear.NativeMaterialValue + (lockLevel * bashDamageMod);
                                                        if (damage < 1)
                                                            damage = 1;
                                                        gear.LowerCondition(damage);
                                                        Debug.Log("LOCKPICKING UNLIMITED - " + gear.shortName + " CONDITION IS " + gear.currentCondition.ToString() + " out of " + gear.maxCondition.ToString());
                                                    }

                                                    if (damage < 1)
                                                        damage = 1;

                                                    player.EntityBehaviour.GetComponent<ShowPlayerDamage>().Flash();
                                                    player.DecreaseHealth(damage);
                                                }
                                            }

                                            bashChance += bashChanceMod;

                                            //roll for bash to open the door
                                            if (bashOpen && bashChance > 0)
                                            {
                                                Debug.Log("LOCKPICKING UNLIMITED - BASHING CHANCE IS " + bashChance.ToString() + "%!");
                                                if (Dice100.SuccessRoll(bashChance))
                                                {
                                                    //GameManager.Instance.PlayerActivate.ActivateStaticDoor(doors, hit, true, building, buildingSummary.BuildingType, lockLevel > 0, lockLevel, doorOwner, true);
                                                    //TO-DO: make custom bash open method for exterior doors here because default method is not public
                                                    //OR use reflection
                                                    object[] args = new object[9]
                                                        {
                                                        doors,
                                                        hit,
                                                        true,
                                                        building,
                                                        buildingSummary.BuildingType,
                                                        lockLevel > 0,
                                                        lockLevel,
                                                        doorOwner,
                                                        true
                                                        };
                                                    ActivateStaticDoor.Invoke(GameManager.Instance.PlayerActivate, args);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            //check highlight input
            if (detect)
            {
                if (detectHighlightToggle)
                {
                    if (InputManager.Instance.GetKeyUp(detectKeyCode))
                    {
                        ToggleHighlightFlags(!highlighting);
                        if (highlighting && detectHighlightMessage)
                            DaggerfallUI.AddHUDText("Highlighting detected objects");
                    }
                }
                else
                {
                    if (InputManager.Instance.GetKeyDown(detectKeyCode))
                    {
                        ToggleHighlightFlags(true);
                        if (detectHighlightMessage)
                            DaggerfallUI.AddHUDText("Highlighting detected objects");
                    }

                    if (InputManager.Instance.GetKeyUp(detectKeyCode))
                        ToggleHighlightFlags(false);
                }
            }

            //check activate input
            if (InputManager.Instance.ActionStarted(InputManager.Actions.ActivateCenterObject))
            {
                if (GameManager.Instance.PlayerActivate.CurrentMode != PlayerActivateModes.Steal || GameManager.Instance.PlayerEffectManager.HasReadySpell)
                    return;

                Ray ray = new Ray(eye.transform.position, eye.transform.forward);
                RaycastHit hit = new RaycastHit();

                //shoot ray at cursor while cursor mode is enabled
                if (GameManager.Instance.PlayerMouseLook.cursorActive)
                {
                    if (DaggerfallUI.Instance.CustomScreenRect != null)
                        screenRect = DaggerfallUI.Instance.CustomScreenRect.Value;
                    else
                        screenRect = new Rect(0, 0, Screen.width, Screen.height);

                    Vector2 mousePosition = new Vector2((InputManager.Instance.MousePosition.x - screenRect.x) / screenRect.width, (InputManager.Instance.MousePosition.y - screenRect.y) / screenRect.height);
                    if (DaggerfallUnity.Settings.LargeHUD)
                        mousePosition.y = (InputManager.Instance.MousePosition.y - screenRect.y - DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.ScreenHeight) / screenRect.height;
                    Vector3 forward = GameManager.Instance.MainCamera.ViewportPointToRay(mousePosition).direction;
                    ray.direction = forward;
                }

                if (Physics.Raycast(ray, out hit, activationDistance, layerMask))
                {
                    if (GameManager.Instance.PlayerEnterExit.IsPlayerInside)
                    {
                        //if inside only look for action doors
                        DaggerfallActionDoor actionDoor = hit.collider.gameObject.GetComponent<DaggerfallActionDoor>();
                        if (actionDoor != null)
                        {
                            //door is locked and isn't already in the list
                            if (actionDoor.CurrentLockValue > 0 && !actionDoors.Contains(actionDoor))
                            {
                                //door is magically locked
                                if (actionDoor.CurrentLockValue >= 20 && dispel)
                                    AttemptDispelMagicalLock(actionDoor);
                                else
                                    actionDoors.Add(actionDoor);
                            }
                        }

                        //check for traps
                        DaggerfallAction action = hit.collider.gameObject.GetComponent<DaggerfallAction>();
                        if (action != null && IsTrap(action))
                        {
                            //check if trap is already disarmed
                            ulong trapLoadID = action.LoadID;
                            if (!disarmedLoadIDs.Contains(trapLoadID))
                            {
                                AttemptDisarmTrap(action);
                            }
                        }
                    }
                    else
                    {
                        StaticBuilding building = new StaticBuilding();
                        Transform buildingOwner;
                        DaggerfallStaticBuildings buildings = GameManager.Instance.PlayerActivate.GetBuildings(hit.transform, out buildingOwner);
                        if (buildings && buildings.HasHit(hit.point, out building))
                        {
                            if (staticDoors.Contains(building))
                                return;

                            // Get building directory for location
                            BuildingDirectory buildingDirectory = GameManager.Instance.StreamingWorld.GetCurrentBuildingDirectory();
                            if (!buildingDirectory)
                                return;

                            // Get detailed building data from directory
                            BuildingSummary buildingSummary;
                            if (!buildingDirectory.GetBuildingSummary(building.buildingKey, out buildingSummary))
                                return;

                            if (!GameManager.Instance.PlayerActivate.BuildingIsUnlocked(buildingSummary))
                            {
                                // Check for a static door hit
                                Transform doorOwner;
                                DaggerfallStaticDoors doors = GameManager.Instance.PlayerActivate.GetDoors(hit.transform, out doorOwner);
                                if (doors)
                                    staticDoors.Add(building);
                            }
                        }
                    }
                }
            }
        }

        void AttemptDispelMagicalLock(DaggerfallActionDoor actionDoor)
        {
            //chance to dispel the Lock spell effect if Lockpicking skill is above [dispelLevel]
            if (GameManager.Instance.PlayerEntity.Skills.GetLiveSkillValue(DaggerfallConnect.DFCareer.Skills.Lockpicking) >= dispelLevel)
            {
                DaggerfallUnityItem tools = null;
                if (dispelRequireTools)
                {
                    tools = Instance.GetTools();
                    if (tools == null)
                    {
                        DaggerfallUI.Instance.DaggerfallHUD.SetMidScreenText("No Thieves' Tools equipped");
                        return;
                    }
                }

                int chance = GetDispelChance(dispelChance, actionDoor.CurrentLockValue);
                if (Dice100.SuccessRoll(chance))
                {
                    DaggerfallUI.Instance.DaggerfallHUD.SetMidScreenText("The warding spell dissipates");

                    //automagically pick door
                    actionDoor.CurrentLockValue = 0;
                }
                else
                    actionDoors.Add(actionDoor);

                if (tools != null)
                {
                    int damage = 50 - chance;
                    if (damage < 0)
                        damage = 1;
                    tools.LowerCondition(damage, GameManager.Instance.PlayerEntity);
                }
            }
        }

        void AttemptDisarmTrap(DaggerfallAction action)
        {
            DaggerfallUnityItem tools = null;
            if (disarmRequireTools)
            {
                tools = Instance.GetTools();
                if (tools == null)
                {
                    DaggerfallUI.Instance.DaggerfallHUD.SetMidScreenText("No Thieves' Tools equipped");
                    return;
                }
            }

            //check to disarm trap
            ulong trapLoadID = action.LoadID;
            int chance = GetDisarmChance(disarmChance);
            if (Dice100.SuccessRoll(chance))
            {
                DaggerfallUI.Instance.DaggerfallHUD.SetMidScreenText("The trap has been disarmed");
                disarmedLoadIDs.Add(trapLoadID);
                DisarmTrap(action);

                //unflag trap if previously flagged
                FlagObject(action, false);
            }
            else
            {
                //flag trap if not yet flagged
                FlagObject(action);

                //proc trap on player if rolls more than LUK
                if (disarmAlwaysTrigger || (!disarmAlwaysTrigger && Dice100.FailedRoll(GameManager.Instance.PlayerEntity.Stats.GetLiveStatValue(DaggerfallConnect.DFCareer.Stats.Luck))))
                    action.Receive(GameManager.Instance.PlayerEntityBehaviour.gameObject);
            }

            if (tools != null)
            {
                int damage = 50 - chance;
                if (damage < 0)
                    damage = 1;
                tools.LowerCondition(damage, GameManager.Instance.PlayerEntity);
            }
        }

        void ResetLockedDoors()
        {
            //check if player has tools equipped or if mode is set to tool-less
            //if Player is inside a building, look for daggerfallActionDoor components in scene
            //else, check if night and then look for static building doors if so
            DaggerfallUnityItem tools = null;

            if (GameManager.Instance.PlayerEnterExit.IsPlayerInside)
            {
                if (Instance.staticDoors.Count > 0)
                    Instance.staticDoors.Clear();

                if (Instance.resetRequireTools)
                {
                    tools = Instance.GetTools();
                    if (tools == null)
                        return;
                }

                if (Instance.actionDoors.Count > 0)
                {
                    for (int i = Instance.actionDoors.Count-1; i > -1; i--)
                    {
                        if (Instance.actionDoors[i] == null)
                        {
                            Instance.actionDoors.RemoveAt(i);
                            continue;
                        }

                        if (!Instance.resetRequireTools || tools != null)
                        {

                            if (Instance.actionDoors[i].FailedSkillLevel > 0)
                            {
                                if (tools != null)
                                    tools.LowerCondition(GetToolsDamage(toolDamageMod, Instance.actionDoors[i].CurrentLockValue), GameManager.Instance.PlayerEntity);

                                Instance.actionDoors[i].FailedSkillLevel = 0;

                                //check for alert here
                                if (resetAlert)
                                {
                                    PlayerEntity player = GameManager.Instance.PlayerEntity;
                                    PlayerEnterExit enterExit = GameManager.Instance.PlayerEnterExit;

                                    bool alert = false;
                                    if (resetAlertVanillaFormula)
                                    {
                                        alert = Dice100.SuccessRoll(Mathf.RoundToInt(33f * ((100f - player.Skills.GetLiveSkillValue(DaggerfallConnect.DFCareer.Skills.Stealth)) / 100f)));
                                        player.TallySkill(DaggerfallConnect.DFCareer.Skills.Stealth, 1);
                                    }
                                    else
                                    {
                                        alert = Dice100.SuccessRoll(
                                            25 +
                                            Instance.actionDoors[i].CurrentLockValue +
                                            resetAlertChanceMod -
                                            Mathf.RoundToInt((player.Skills.GetLiveSkillValue(DaggerfallConnect.DFCareer.Skills.Stealth) - 50) / 2) -
                                            Mathf.RoundToInt((player.Skills.GetLiveSkillValue(DaggerfallConnect.DFCareer.Skills.Lockpicking) - 50) / 2));
                                        player.TallySkill(DaggerfallConnect.DFCareer.Skills.Stealth, 1);
                                        player.TallySkill(DaggerfallConnect.DFCareer.Skills.Lockpicking, 1);
                                    }

                                    if (alert)
                                    {
                                        if (enterExit.IsPlayerInsideBuilding)
                                        {
                                            //make sure player isn't in a player-owned house or ship
                                            int buildingKey = enterExit.BuildingDiscoveryData.buildingKey;
                                            if (enterExit.BuildingType != DaggerfallConnect.DFLocation.BuildingTypes.Ship && !DaggerfallBankManager.IsHouseOwned(buildingKey))
                                            {
                                                //Alert the guards and flag the player with a crime
                                                if (resetAlertMessage)
                                                    DaggerfallUI.Instance.DaggerfallHUD.SetMidScreenText("You hear guards rapidly approaching...");
                                                if (!enterExit.IsPlayerInsideOpenShop)
                                                    player.CrimeCommitted = PlayerEntity.Crimes.Breaking_And_Entering;
                                                else
                                                    player.CrimeCommitted = PlayerEntity.Crimes.Assault;
                                                SpawnCityGuards();
                                            }
                                        }
                                    }
                                }
                            }
                            Instance.actionDoors.RemoveAt(i);
                        }
                    }
                }
            }
            else
            {
                if (Instance.actionDoors.Count > 0)
                    Instance.actionDoors.Clear();

                if (Instance.resetRequireTools)
                {
                    tools = Instance.GetTools();
                    if (tools == null)
                        return;
                }

                if (Instance.staticDoors.Count > 0)
                {
                    for (int i = Instance.staticDoors.Count - 1; i > -1; i--)
                    {
                        if (!Instance.resetRequireTools || tools != null)
                        {
                            if (GameManager.Instance.PlayerGPS.GetLastLockpickAttempt(Instance.staticDoors[i].buildingKey) > 0)
                            {
                                BuildingSummary buildingSummary;
                                GameManager.Instance.StreamingWorld.GetCurrentBuildingDirectory().GetBuildingSummary(Instance.staticDoors[i].buildingKey, out buildingSummary);
                                int lockValue = GameManager.Instance.PlayerActivate.GetBuildingLockValue(buildingSummary);
                                if (tools != null)
                                {
                                    tools.LowerCondition(GetToolsDamage(toolDamageMod, lockValue), GameManager.Instance.PlayerEntity);
                                }

                                GameManager.Instance.PlayerGPS.SetLastLockpickAttempt(Instance.staticDoors[i].buildingKey, 0);

                                //check for alert here
                                if (resetAlert)
                                {
                                    PlayerEntity player = GameManager.Instance.PlayerEntity;
                                    PlayerEnterExit enterExit = GameManager.Instance.PlayerEnterExit;

                                    bool alert = false;
                                    if (resetAlertVanillaFormula)
                                    {
                                        alert = Dice100.SuccessRoll(Mathf.RoundToInt(33f*((100f- player.Skills.GetLiveSkillValue(DaggerfallConnect.DFCareer.Skills.Stealth))/100f)));
                                        player.TallySkill(DaggerfallConnect.DFCareer.Skills.Stealth,1);
                                    }
                                    else
                                    {
                                        alert = Dice100.SuccessRoll(
                                            25 +
                                            lockValue +
                                            resetAlertChanceMod -
                                            Mathf.RoundToInt((player.Skills.GetLiveSkillValue(DaggerfallConnect.DFCareer.Skills.Stealth)-50)/2) -
                                            Mathf.RoundToInt((player.Skills.GetLiveSkillValue(DaggerfallConnect.DFCareer.Skills.Lockpicking)-50)/2));
                                        player.TallySkill(DaggerfallConnect.DFCareer.Skills.Stealth, 1);
                                        player.TallySkill(DaggerfallConnect.DFCareer.Skills.Lockpicking, 1);
                                    }

                                    if (alert)
                                    {
                                        //Alert the guards and flag the player with a crime
                                        if (resetAlertMessage)
                                            DaggerfallUI.Instance.DaggerfallHUD.SetMidScreenText("You hear guards rapidly approaching...");
                                        player.CrimeCommitted = PlayerEntity.Crimes.Attempted_Breaking_And_Entering;
                                        SpawnCityGuards();
                                    }
                                }
                            }
                            Instance.staticDoors.RemoveAt(i);
                        }
                    }
                }
            }
        }

        DaggerfallUnityItem GetTools()
        {
            ItemEquipTable itemEquipTable = GameManager.Instance.PlayerEntity.ItemEquipTable;

            DaggerfallUnityItem item = itemEquipTable.GetItem(EquipSlots.Ring0);
            if (item != null)
            {
                if (item.TemplateIndex == itemThievesToolsTemplateIndex || (item.TemplateIndex == 546 && lockpickIsTool))
                    return item;
            }

            item = itemEquipTable.GetItem(EquipSlots.Ring1);
            if (item != null)
            {
                if (item.TemplateIndex == itemThievesToolsTemplateIndex || (item.TemplateIndex == 546 && lockpickIsTool))
                    return item;
            }

            //look for Skeleton Key
            item = itemEquipTable.GetItem(EquipSlots.Amulet0);
            if (item != null)
            {
                if (item.IsArtifact && ItemHelper.GetArtifactSubType(item) == ArtifactsSubTypes.Skeletons_Key)
                    return item;
            }

            item = itemEquipTable.GetItem(EquipSlots.Amulet1);
            if (item != null)
            {
                if (item.IsArtifact && ItemHelper.GetArtifactSubType(item) == ArtifactsSubTypes.Skeletons_Key)
                    return item;
            }

            return null;
        }

        int GetToolsDamage(int modDamage = 5, int lockLevel = 1)
        {
            int skillLevel = GameManager.Instance.PlayerEntity.Skills.GetLiveSkillValue(DaggerfallConnect.DFCareer.Skills.Lockpicking);

            int damage = lockLevel * modDamage;
            damage = Mathf.CeilToInt((float)damage * (1f-((float)skillLevel / 100f)));

            Debug.Log("LOCKPICKING UNLIMITED - TOOLS DAMAGE IS " + damage.ToString() + "!");

            return damage;
        }

        int GetDispelChance(int baseChance = 0, int lockLevel = 1)
        {
            int chance = baseChance;

            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;

            chance += Mathf.RoundToInt(((float)(playerEntity.Stats.LiveAgility - 50) / 10f));  //-5% at 0, +5% at 100 
            chance += Mathf.RoundToInt(((float)(playerEntity.Stats.LiveIntelligence - 50) / 10f));  //-5% at 0, +5% at 100 
            chance += Mathf.RoundToInt(((float)(playerEntity.Stats.LiveLuck - 50) / 10f));  //-5% at 0, +5% at 100
            chance += 15 * (playerEntity.Skills.GetLiveSkillValue(DaggerfallConnect.DFCareer.Skills.Lockpicking) - dispelLevel);
            chance -= lockLevel - 20; //if lock level is 25, then chance penalty is -5

            if (chance < 0)
                chance = 0;
            else
                playerEntity.TallySkill(DaggerfallConnect.DFCareer.Skills.Lockpicking, 2);

            Debug.Log("LOCKPICKING UNLIMITED - DISPEL CHANCE IS " + chance.ToString() + "%!");

            return chance;
        }

        int GetDisarmChance(int baseChance = 0)
        {
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            int chance = baseChance;
            chance += player.Skills.GetLiveSkillValue(DaggerfallConnect.DFCareer.Skills.Lockpicking) / 2;    //50% base chance to disarm at 100 Lockpicking
            chance += Mathf.RoundToInt(((float)player.Stats.GetLiveStatValue(DaggerfallConnect.DFCareer.Stats.Intelligence) - 50f) / 2.5f);  //-20% to +20% chance from INT
            chance += Mathf.RoundToInt(((float)player.Stats.GetLiveStatValue(DaggerfallConnect.DFCareer.Stats.Agility) - 50f) / 2.5f);       //-20% to +20% chance from AGI
            chance += Mathf.RoundToInt(((float)player.Stats.GetLiveStatValue(DaggerfallConnect.DFCareer.Stats.Luck) - 50f) / 5f);       //-10% to +10% chance from LUK

            //cap chance
            if (chance < 5)
                chance = 5;
            if (chance > 95)
                chance = 95;

            //tally lockpicking skill on disarm attempt
            player.TallySkill(DaggerfallConnect.DFCareer.Skills.Lockpicking, 1);

            Debug.Log("LOCKPICKING UNLIMITED - DISARM CHANCE IS " + chance.ToString() + "%");

            return chance;
        }

        void SpawnCityGuards()
        {
            int maxActiveGuardSpawns = 5-GameManager.Instance.HowManyEnemiesOfType(MobileTypes.Knight_CityWatch, false, true);

            // Only spawn if player is not in a dungeon, and if there are fewer than max active guards
            // Handle indoor guard spawning
            PlayerEntity entity = GameManager.Instance.PlayerEntity;
            PlayerEnterExit enterExit = GameManager.Instance.PlayerEnterExit;
            if (enterExit.IsPlayerInside)
            {
                if (enterExit.Interior.FindLowestOuterInteriorDoor(out Vector3 lowestDoorPos, out Vector3 lowestDoorNormal))
                {
                    lowestDoorPos += lowestDoorNormal * (GameManager.Instance.PlayerController.radius + 0.1f);
                    int guardCount = UnityEngine.Random.Range(1, maxActiveGuardSpawns+1);
                    for (int i = 0; i < guardCount; i++)
                    {
                        entity.SpawnCityGuard(lowestDoorPos, Vector3.forward);
                    }
                }
            }
            else
            {
                entity.SpawnCityGuards(true);
            }
        }

        IEnumerator LockDoorsCoroutine(int maxLockLevel = 11, bool hideNPCs = false)
        {
            Debug.Log("LOCKPICKING UNLIMITED - LOCKING DOORS!");

            bool questPresent = false;

            yield return new WaitForSeconds(0.1f);
            if (hideNPCs)
            {
                foreach (GameObject npc in ActiveGameObjectDatabase.GetActiveStaticNPCObjects())
                {
                    //hide static NPCs that are not related to quests
                    QuestResourceBehaviour questResourceBehaviour = npc.GetComponent<QuestResourceBehaviour>();
                    if (questResourceBehaviour)
                        questPresent = true;
                    else
                        npc.SetActive(false);
                }
            }

            //TO DO: Handle quest target presence somehow

            foreach (DaggerfallActionDoor actionDoor in ActiveGameObjectDatabase.GetActiveActionDoors())
            {
                if (actionDoor.CurrentLockValue == 0)
                {
                    if (Dice100.SuccessRoll(lockChance))
                        actionDoor.CurrentLockValue = UnityEngine.Random.Range(1, maxLockLevel);
                }
            }

            locking = null;
        }

        static void OnTransitionDungeonInterior(PlayerEnterExit.TransitionEventArgs args)
        {
            if (Instance.locked)
            {
                if (SaveLoadManager.Instance.LoadInProgress)
                    return;

                Instance.LockDungeonDoors(args);
            }

            Instance.FillMasterLists();
        }

        static void OnTransitionInterior(PlayerEnterExit.TransitionEventArgs args)
        {
            if (Instance.locked)
            {
                if (SaveLoadManager.Instance.LoadInProgress)
                    return;

                Instance.LockBuildingDoors(args);
            }
        }

        //clear list of disarmed traps when leaving interior/dungeon
        static void OnTransitionExterior(PlayerEnterExit.TransitionEventArgs args)
        {
            Instance.flaggedLoadIDs.Clear();
            Instance.flaggedObjects.Clear();

            Instance.disarmedLoadIDs.Clear();

            Instance.masterHiddenDoors.Clear();
            Instance.masterTraps.Clear();
            Instance.masterHiddenSwitches.Clear();
        }

        static void OnLoad(SaveData_v1 data)
        {
            if (Instance.detect)
            {
                //disarm/flag all actions with stored loadID
                DaggerfallAction[] actionObjects = FindObjectsOfType<DaggerfallAction>();
                foreach (DaggerfallAction actionObject in actionObjects)
                {
                    if (actionObject == null || actionObject.LoadID == 0)
                        continue;

                    if (Instance.disarmedLoadIDs.Contains(actionObject.LoadID))
                        Instance.DisarmTrap(actionObject);
                }

                Instance.SyncFlaggedObjects();
            }

            Instance.FillMasterLists();
        }

        public void DisarmTrap(DaggerfallAction action)
        {
            //set action to none
            action.ActionFlag = DaggerfallConnect.DFBlock.RdbActionFlags.None;
            action.PlaySound = false;

            //check for triggers
            /*DaggerfallActionCollision actionCollision = action.GetComponent<DaggerfallActionCollision>();
            if (actionCollision != null)
                actionCollision.enabled = false;*/
        }

        public void FlagObject(DaggerfallAction action, bool flag = true)
        {
            if (flag)
            {
                if (!flaggedLoadIDs.Contains(action.LoadID))
                {
                    flaggedLoadIDs.Add(action.LoadID);
                    if (!flaggedObjects.Contains(action.gameObject))
                        flaggedObjects.Add(action.gameObject);
                    //flash the flagged object red
                    if (!highlighting)
                        StartCoroutine(FlashFlaggedObject(action.gameObject, detectHighlightDuration));
                    else
                        SetHighlightOnFlaggedObject(action.gameObject, flag);
                }
            }
            else
            {
                if (flaggedLoadIDs.Contains(action.LoadID))
                {
                    flaggedLoadIDs.Remove(action.LoadID);
                    if (flaggedObjects.Contains(action.gameObject))
                        flaggedObjects.Remove(action.gameObject);
                    if (highlighting)
                        SetHighlightOnFlaggedObject(action.gameObject, flag);
                }
            }
        }

        public void FlagObject(DaggerfallActionDoor actionDoor, bool flag = true)
        {
            if (flag)
            {
                if (!flaggedLoadIDs.Contains(actionDoor.LoadID))
                {
                    flaggedLoadIDs.Add(actionDoor.LoadID);
                    if (!flaggedObjects.Contains(actionDoor.gameObject))
                        flaggedObjects.Add(actionDoor.gameObject);
                    //flash the flagged object red
                    if (!highlighting)
                        StartCoroutine(FlashFlaggedObject(actionDoor.gameObject, detectHighlightDuration));
                    else
                        SetHighlightOnFlaggedObject(actionDoor.gameObject, flag);
                }
            }
            else
            {
                if (flaggedLoadIDs.Contains(actionDoor.LoadID))
                {
                    flaggedLoadIDs.Remove(actionDoor.LoadID);
                    if (flaggedObjects.Contains(actionDoor.gameObject))
                        flaggedObjects.Remove(actionDoor.gameObject);
                    if (highlighting)
                        SetHighlightOnFlaggedObject(actionDoor.gameObject, flag);
                }
            }
        }

        void SyncFlaggedObjects()
        {
            flaggedObjects.Clear();

            if (flaggedLoadIDs.Count < 1)
                return;

            DaggerfallAction[] actions = FindObjectsOfType<DaggerfallAction>();
            foreach (DaggerfallAction action in actions)
            {
                if (flaggedLoadIDs.Contains(action.LoadID) && !flaggedObjects.Contains(action.gameObject))
                    flaggedObjects.Add(action.gameObject);
                else if (!flaggedLoadIDs.Contains(action.LoadID) && flaggedObjects.Contains(action.gameObject))
                    flaggedObjects.Remove(action.gameObject);
            }

            foreach (DaggerfallActionDoor actionDoor in ActiveGameObjectDatabase.GetActiveActionDoors())
            {
                if (flaggedLoadIDs.Contains(actionDoor.LoadID) && !flaggedObjects.Contains(actionDoor.gameObject))
                    flaggedObjects.Add(actionDoor.gameObject);
                else if (!flaggedLoadIDs.Contains(actionDoor.LoadID) && flaggedObjects.Contains(actionDoor.gameObject))
                    flaggedObjects.Remove(actionDoor.gameObject);
            }

            ToggleHighlightFlags(highlighting);
        }

        void ToggleHighlightFlags(bool highlight)
        {
            highlighting = highlight;

            if (flaggedObjects.Count < 1)
                return;

            foreach (GameObject flaggedObject in flaggedObjects)
            {
                SetHighlightOnFlaggedObject(flaggedObject, highlight);
            }
        }

        IEnumerator FlashFlaggedObject(GameObject target, float duration = 1)
        {
            DaggerfallActionCollision actionCollision = target.GetComponent<DaggerfallActionCollision>();

            //set meshrenderer materials
            Renderer rendererMain = target.GetComponent<Renderer>();
            Renderer[] rendererChildren = target.GetComponentsInChildren<Renderer>();

            Color colorStart = detectHighlightColor;
            Color colorEnd = Color.white;
            Color colorCurrent = colorStart;

            float time = 0;
            float t = time;

            while (time < duration)
            {
                t = time / duration;
                colorCurrent = Color.Lerp(colorStart, colorEnd, t*t);

                foreach (Material material in rendererMain.materials)
                {
                    if (actionCollision != null)
                    {
                        //if activated by walking on, tint only the floor material
                        if (material.name.Contains("[Index=2]"))
                        {
                            material.SetColor("_Color", colorCurrent);
                            material.SetColor("_EmissionColor", colorCurrent);
                        }
                    }
                    else
                    {
                        material.SetColor("_Color", colorCurrent);
                        material.SetColor("_EmissionColor", colorCurrent);
                    }
                }

                time += Time.deltaTime;

                yield return new WaitForEndOfFrame();
            }

            //bool emission = material.IsKeywordEnabled(KeyWords.Emission)
        }

        public void SetHighlightOnFlaggedObject(GameObject target, bool flag = true)
        {
            DaggerfallActionCollision actionCollision = target.GetComponent<DaggerfallActionCollision>();

            //set meshrenderer materials
            Renderer rendererMain = target.GetComponent<Renderer>();
            Renderer[] rendererChildren = target.GetComponentsInChildren<Renderer>();
            if (rendererMain != null)
            {
                foreach (Material material in rendererMain.materials)
                {
                    if (actionCollision != null)
                    {
                        //if activated by walking on, tint only the floor material
                        if (material.name.Contains("[Index=2]"))
                        {
                            if (flag)
                            {
                                material.SetColor("_Color", detectHighlightColor);
                                material.SetColor("_EmissionColor", detectHighlightColor);
                            }
                            else
                            {
                                material.SetColor("_Color", Color.white);
                                material.SetColor("_EmissionColor", Color.white);
                            }
                        }
                    }
                    else
                    {
                        if (flag)
                        {
                            material.SetColor("_Color", detectHighlightColor);
                            material.SetColor("_EmissionColor", detectHighlightColor);
                        }
                        else
                        {
                            material.SetColor("_Color", Color.white);
                            material.SetColor("_EmissionColor", Color.white);
                        }
                    }
                }
            }
            if (rendererChildren.Length > 0)
            {
                foreach (Renderer renderer in rendererChildren)
                {
                    foreach (Material material in renderer.materials)
                    {
                        if (actionCollision != null)
                        {
                            //if activated by walking on, tint only the floor material
                            if (material.name.Contains("[Index=2]"))
                            {
                                if (flag)
                                {
                                    material.SetColor("_Color", detectHighlightColor);
                                    material.SetColor("_EmissionColor", detectHighlightColor);
                                }
                                else
                                {
                                    material.SetColor("_Color", Color.white);
                                    material.SetColor("_EmissionColor", Color.white);
                                }
                            }
                        }
                        else
                        {
                            if (flag)
                            {
                                material.SetColor("_Color", detectHighlightColor);
                                material.SetColor("_EmissionColor", detectHighlightColor);
                            }
                            else
                            {
                                material.SetColor("_Color", Color.white);
                                material.SetColor("_EmissionColor", Color.white);
                            }
                        }
                    }
                }
            }
        }

        public void LockDungeonDoors(PlayerEnterExit.TransitionEventArgs args)
        {
            if (locking != null)
                return;

            locking = LockDoorsCoroutine();
            StartCoroutine(locking);
        }

        public void LockBuildingDoors(PlayerEnterExit.TransitionEventArgs args)
        {
            if (locking != null)
                return;

            //make sure player isn't in a player-owned house or ship
            PlayerEnterExit enterExit = GameManager.Instance.PlayerEnterExit;
            int buildingKey = enterExit.BuildingDiscoveryData.buildingKey;
            if (enterExit.BuildingType == DaggerfallConnect.DFLocation.BuildingTypes.Ship || DaggerfallBankManager.IsHouseOwned(buildingKey))
                return;

            //check if interior is closed shop/guild hall or residence at night
            if (((RMBLayout.IsShop(enterExit.BuildingType) || enterExit.BuildingType == DaggerfallConnect.DFLocation.BuildingTypes.GuildHall) && !PlayerActivate.IsBuildingOpen(enterExit.BuildingType)) ||
                (RMBLayout.IsResidence(enterExit.BuildingType) && DaggerfallUnity.Instance.WorldTime.Now.IsNight)
                )
            {
                locking = LockDoorsCoroutine(enterExit.BuildingDiscoveryData.quality, lockNPCsHide);
                StartCoroutine(locking);
            }
        }

        void FillMasterLists()
        {
            masterHiddenDoors.Clear();
            masterHiddenSwitches.Clear();
            masterTraps.Clear();

            //Find hidden doors
            foreach (DaggerfallActionDoor door in ActiveGameObjectDatabase.GetActiveActionDoors())
            {
                if (IsHiddenDoor(door))
                    masterHiddenDoors.Add(door);
            }

            //Find traps and hidden switches
            DaggerfallAction[] actions = FindObjectsOfType<DaggerfallAction>();
            foreach (DaggerfallAction action in actions)
            {
                if (IsTrap(action))
                {
                    Instance.masterTraps.Add(action);
                    continue;
                }
                if (IsHiddenSwitch(action))
                {
                    Instance.masterHiddenSwitches.Add(action);
                    continue;
                }
            }
        }

        bool IsTrap(DaggerfallAction target)
        {
            if (target.ActionFlag == DaggerfallConnect.DFBlock.RdbActionFlags.Hurt21 ||
                target.ActionFlag == DaggerfallConnect.DFBlock.RdbActionFlags.Hurt22 ||
                target.ActionFlag == DaggerfallConnect.DFBlock.RdbActionFlags.Hurt23 ||
                target.ActionFlag == DaggerfallConnect.DFBlock.RdbActionFlags.Hurt24 ||
                target.ActionFlag == DaggerfallConnect.DFBlock.RdbActionFlags.Hurt25 ||
                target.ActionFlag == DaggerfallConnect.DFBlock.RdbActionFlags.DrainMagicka ||
                target.ActionFlag == DaggerfallConnect.DFBlock.RdbActionFlags.Poison ||
                target.ActionFlag == DaggerfallConnect.DFBlock.RdbActionFlags.Unknown27 ||
                (target.ActionFlag == DaggerfallConnect.DFBlock.RdbActionFlags.CastSpell && target.Index != 4) ||  //exclude Levitation casters from traps
                target.ModelDescription == "TRP")
                return true;

            return false;
        }

        bool IsHiddenSwitch(DaggerfallAction target)
        {
            //includes Levitate spell objects
            //a switch can only be activated by clicking on it
            if (target.TriggerFlag == DaggerfallConnect.DFBlock.RdbTriggerFlags.Direct)
            {
                DaggerfallBillboard billboard = target.GetComponent<DaggerfallBillboard>();
                if (billboard != null)
                {
                    //if it can be activated and is a billboard, it is automatically a hidden switch
                    return true;
                }

                string meshFilterName = target.GetComponent<MeshFilter>().name;
                if (!meshFilterName.Contains("61027") &&    //long lever
                    !meshFilterName.Contains("61028") &&    //short lever
                    !meshFilterName.Contains("61032"))      //wheel
                {
                    //if not a billboard, it is hidden switch if the mesh is not a lever/wheel/button/etc
                    return true;
                }
            }

            return false;
        }

        bool IsHiddenDoor(DaggerfallActionDoor target)
        {
            string meshFilterName = target.GetComponent<MeshFilter>().name;

            //if door does not use the overt door mesh, it is hidden
            if (!meshFilterName.Contains("55000") &&
                !meshFilterName.Contains("55001") &&
                !meshFilterName.Contains("55002") &&
                !meshFilterName.Contains("55003") &&
                !meshFilterName.Contains("55004") &&
                !meshFilterName.Contains("55005"))
                return true;

            return false;
        }

        static void OnNewMagicRound()
        {
            if (Instance.detect)
                Instance.CheckForHiddenThings();
        }

        public void CheckForHiddenThings()
        {
            //do not check for hidden things when outside
            if (!GameManager.Instance.PlayerEnterExit.IsPlayerInside)
                return;

            //do not check for hidden things if moving at more than half-speed
            if (detectSpeed && !GameManager.Instance.PlayerMotor.IsMovingLessThanHalfSpeed)
                return;

            Debug.Log("LOCKPICKING UNLIMITED - CHECKING FOR HIDDEN THINGS");
            //iterate through hidden doors and traps list

            //hidden doors
            if (masterHiddenDoors.Count > 0)
            {
                foreach (DaggerfallActionDoor door in masterHiddenDoors)
                {
                    //check if not yet flagged
                    //check if closed (do not test on an open hidden door)
                    //do a LOS and proximity check
                    if (!flaggedLoadIDs.Contains(door.LoadID) && door.IsClosed && IsDetected(door.gameObject))
                    {
                        DaggerfallUI.Instance.DaggerfallHUD.SetMidScreenText("You notice something peculiar...");
                        FlagObject(door);
                    }
                }
            }

            //hidden switches
            if (masterHiddenSwitches.Count > 0)
            {
                foreach (DaggerfallAction hiddenSwitch in masterHiddenSwitches)
                {
                    if (!flaggedLoadIDs.Contains(hiddenSwitch.LoadID) && IsDetected(hiddenSwitch.gameObject))
                    {
                        DaggerfallUI.Instance.DaggerfallHUD.SetMidScreenText("You notice something peculiar...");
                        FlagObject(hiddenSwitch);
                    }
                }
            }

            //traps
            if (masterTraps.Count > 0)
            {
                foreach (DaggerfallAction trap in masterTraps)
                {
                    if (!flaggedLoadIDs.Contains(trap.LoadID) && !disarmedLoadIDs.Contains(trap.LoadID) && IsDetected(trap.gameObject))
                    {
                        DaggerfallUI.Instance.DaggerfallHUD.SetMidScreenText("You notice something peculiar...");
                        FlagObject(trap);
                    }
                }
            }
        }

        bool IsDetected(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();

            Vector3 origin = GameManager.Instance.MainCameraObject.transform.position;
            Vector3 vector = collider.bounds.center - origin;
            Vector3 direction = vector.normalized;
            float distance = vector.magnitude;

            if (distance > detectRange)
                return false;

            Ray ray = new Ray(origin, direction);
            RaycastHit hit = new RaycastHit();
            if (Physics.Raycast(ray, out hit, 320, layerMask))
            {
                if (hit.collider.gameObject == target && Dice100.SuccessRoll(GetDetectChance(detectChance)))
                {
                    //hit the target
                    Debug.DrawLine(origin, hit.point, Color.green, 1, false);
                    return true;
                }
                else
                {
                    //hit something else
                    Debug.DrawLine(origin, hit.point, Color.red, 1, false);
                    return false;
                }
            }

            //didn't hit anything
            Debug.DrawRay(origin, direction * distance, Color.yellow, 1, false);

            return false;
        }

        int GetDetectChance(int baseChance = 0)
        {
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            int chance = baseChance;

            chance += Mathf.RoundToInt(((float)player.Stats.GetLiveStatValue(DaggerfallConnect.DFCareer.Stats.Intelligence)/2));  //0% to 50% chance from INT

            //add bonuses based on context (light source, acute hearing advantage, lycanthropy etc)
            if (player.LightSource != null)
                chance += 20;
            if (player.Career.AcuteHearing)
                chance += 20;
            if (GameManager.Instance.PlayerEffectManager.HasLycanthropy())
                chance += 20;

            chance += Mathf.RoundToInt(((float)player.Stats.GetLiveStatValue(DaggerfallConnect.DFCareer.Stats.Luck) - 50f) / 5f);       //-10% to +10% chance from LUK

            //cap chance
            if (chance < 5)
                chance = 5;
            if (chance > 95)
                chance = 95;

            Debug.Log("LOCKPICKING UNLIMITED - DETECT CHANCE IS " + chance.ToString() + "%");

            return chance;
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

        private static class GiveMeTools
        {
            public static readonly string name = "givetools";
            public static readonly string description = "Adds Thieves' Tools to the player's inventory";
            public static readonly string usage = "givetools";

            public static string Execute(params string[] args)
            {
                string result = "";
                result = "Thieves' Tools added to player's inventory";
                DaggerfallUnityItem newToolsItem = ItemBuilder.CreateItem(ItemGroups.UselessItems2, LockpickingUnlimited.itemThievesToolsTemplateIndex);
                GameManager.Instance.PlayerEntity.Items.AddItem(newToolsItem);
                return result;
            }
        }
    }

    public class ItemThievesTools : DaggerfallUnityItem
    {
        ItemEquipTable equipTable = GameManager.Instance.PlayerEntity.ItemEquipTable;

        public ItemThievesTools() : base(ItemGroups.UselessItems2, LockpickingUnlimited.itemThievesToolsTemplateIndex)
        {

        }

        public override EquipSlots GetEquipSlot()
        {
            Debug.Log("LOCKPICKING UNLIMITED - GET EQUIP SLOT!");
            return equipTable.GetFirstSlot(EquipSlots.Ring0, EquipSlots.Ring1);
        }

        public override SoundClips GetEquipSound()
        {
            return SoundClips.EquipPlate;
        }
        public override ItemData_v1 GetSaveData()
        {
            ItemData_v1 data = base.GetSaveData();
            data.className = typeof(ItemThievesTools).ToString();
            return data;
        }

    }

    //doesn't actually save a reference to the door
    public class LockpickingUnlimitedSaveData : IHasModSaveData
    {
        public List<ulong> disarmedLoadIDs;
        public List<ulong> flaggedLoadIDs;

        public Type SaveDataType
        {
            get
            {
                return typeof(LockpickingUnlimitedSaveData);
            }
        }

        public object NewSaveData()
        {
            LockpickingUnlimitedSaveData emptyData = new LockpickingUnlimitedSaveData();
            emptyData.disarmedLoadIDs = new List<ulong>();
            emptyData.flaggedLoadIDs = new List<ulong>();
            return emptyData;
        }
        public object GetSaveData()
        {
            LockpickingUnlimitedSaveData data = new LockpickingUnlimitedSaveData();
            data.disarmedLoadIDs = LockpickingUnlimited.Instance.disarmedLoadIDs;
            data.flaggedLoadIDs = LockpickingUnlimited.Instance.flaggedLoadIDs;

            if (data.disarmedLoadIDs == null)
                data.disarmedLoadIDs = new List<ulong>();
            if (data.flaggedLoadIDs == null)
                data.flaggedLoadIDs = new List<ulong>();

            return data;
        }

        public void RestoreSaveData(object dataIn)
        {
            LockpickingUnlimitedSaveData data = (LockpickingUnlimitedSaveData)dataIn;
            LockpickingUnlimited.Instance.disarmedLoadIDs = data.disarmedLoadIDs;
            LockpickingUnlimited.Instance.flaggedLoadIDs = data.flaggedLoadIDs;

            if (LockpickingUnlimited.Instance.disarmedLoadIDs == null)
                LockpickingUnlimited.Instance.disarmedLoadIDs = new List<ulong>();
            if (LockpickingUnlimited.Instance.flaggedLoadIDs == null)
                LockpickingUnlimited.Instance.flaggedLoadIDs = new List<ulong>();
        }
    }
}
