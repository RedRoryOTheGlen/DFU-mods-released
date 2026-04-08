using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using DaggerfallWorkshop.Game.Guilds;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using Wenzil.Console;

namespace PlayerDefeatMod
{
    public class PlayerDefeat : MonoBehaviour
    {
        private static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            mod.SaveDataInterface = new PlayerDefeatSaveData();

            var go = new GameObject(mod.Title);
            go.AddComponent<PlayerDefeat>();

            mod.IsReady = true;
        }

        public static PlayerDefeat Instance;

        PlayerEntity playerEntity;
        PlayerDeath playerDeath;

        DFPosition NewLocation;

        public static EventHandler OnPlayerDeath;

        ItemCollection items = new ItemCollection();

        IEnumerator defeated;

        float TimeSinceWakeUp;

        //settings
        int survivalChance = 20;
        int captureChance = 20;
        int rescueChance = 10;
        int diseaseChance = 5;
        int maxLocationRange = 10;
        float daysPassedMin = 2;
        float daysPassedMax = 4;

        //mod compatibility
        Mod FindingMyReligion;
        Mod ClimatesAndCalories;

        //reflection
        FieldInfo CustomItems;
        Dictionary<string, Type> customItems;

        //store the vampirism/lycanthropy stuff here
        RacialOverrideEffect racialOverrideEffect;

        [FullSerializer.fsObject("v1")]
        public class LostItemsData
        {
            public ItemData_v1[] items;
            //Location?
            //Time dropped?
        }

        public class PlayerDefeatSaveData : IHasModSaveData
        {
            public ItemData_v1[] items;

            public Type SaveDataType
            {
                get
                {
                    return typeof(PlayerDefeatSaveData);
                }
            }

            public object NewSaveData()
            {
                PlayerDefeatSaveData emptyData = new PlayerDefeatSaveData();
                return emptyData;
            }

            public object GetSaveData()
            {
                ItemData_v1[] newItems = Instance.items.SerializeItems();
                PlayerDefeatSaveData data = new PlayerDefeatSaveData();
                data.items = newItems;
                return data;
            }

            public void RestoreSaveData(object dataIn)
            {
                PlayerDefeatSaveData data = (PlayerDefeatSaveData)dataIn;
                ItemData_v1[] savedItems = data.items;

                Instance.items.Clear();
                Instance.items.DeserializeItems(savedItems);
            }
        }

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
        }

        public static void PlayerDeath_OnPlayerDeathHandler(object sender, EventArgs e)
        {
            Debug.Log("PLAYER DEFEAT - DEFEATED!");

            if (!GameManager.Instance.WeaponManager.Sheathed)
                GameManager.Instance.WeaponManager.SheathWeapons();
            if (GameManager.Instance.PlayerEntity.LightSource != null)
                GameManager.Instance.PlayerEntity.LightSource = null;

            //check if player survived
            if (Instance.SurvivalCheck())
            {
                //if success, run survival coroutine
                Debug.Log("PLAYER DEFEAT - PLAYER SURVIVED!");
                Instance.defeated = Instance.DefeatCoroutine();
                Instance.StartCoroutine(Instance.defeated);
            }
            else
            {
                //else, run vanilla player death
                Debug.Log("PLAYER DEFEAT - PLAYER DIED!");
                Instance.Death();
            }
        }

        public void UpdateRacialOverrideEffect()
        {
            if (defeated != null)
                return;

            racialOverrideEffect = GameManager.Instance.PlayerEffectManager.GetRacialOverrideEffect();

            if (racialOverrideEffect != null)
            {
                Debug.Log("PLAYER DEFEAT - PLAYER HAS A RACIAL OVERRIDE EFFECT!");

                if (racialOverrideEffect is VampirismEffect)
                    Debug.Log("PLAYER DEFEAT - PLAYER IS A VAMPIRE!");
                else if (racialOverrideEffect is LycanthropyEffect)
                    Debug.Log("PLAYER DEFEAT - PLAYER IS A LYCANTHROPE!");
            }
        }

        public static void PlayerEnterExit_OnPreTransition(PlayerEnterExit.TransitionEventArgs args)
        {
            Debug.Log("PLAYER DEFEAT - PLAYER STARTED TRANSITION!");

            //check if current character is a vampire or a lycanthrope here
            Instance.UpdateRacialOverrideEffect();
        }

        public static void PlayerEnterExit_OnTransitionExterior(PlayerEnterExit.TransitionEventArgs args)
        {
            Debug.Log("PLAYER DEFEAT - PLAYER TRANSITIONED TO EXTERIOR!");

            //give player back their lost items when exiting any dungeon.
            Instance.RecoverPlayerInventory();
        }

        public static void StartGameBehaviour_OnStartMenu(object sender, EventArgs e)
        {
            GameManager.Instance.PlayerDeath.enabled = false;

            if (Instance.defeated != null)
            {
                Instance.StopCoroutine(Instance.defeated);
                Instance.defeated = null;
            }

            Instance.TimeSinceWakeUp = -10;
        }

        public static void SaveLoadManager_OnLoad(SaveData_v1 saveData)
        {
            GameManager.Instance.PlayerDeath.enabled = false;

            if (Instance.defeated != null)
            {
                Instance.StopCoroutine(Instance.defeated);
                Instance.defeated = null;
            }

            //check if current character is a vampire or a lycanthrope here
            Instance.UpdateRacialOverrideEffect();

            Instance.TimeSinceWakeUp = -10;
        }

        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("Main"))
            {
                survivalChance = settings.GetValue<int>("Main", "SurvivalBaseChance");
                captureChance = settings.GetValue<int>("Main", "CaptureBaseChance");
                rescueChance = settings.GetValue<int>("Main", "RescueBaseChance");
                diseaseChance = settings.GetValue<int>("Main", "DiseaseBaseChance");
                maxLocationRange = settings.GetValue<int>("Main", "MaxLocationRange");
                daysPassedMin = settings.GetTupleInt("Main", "DaysPassedRange").First;
                daysPassedMax = settings.GetTupleInt("Main", "DaysPassedRange").Second;
            }
        }
        private void Start()
        {
            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();

            playerEntity = GameManager.Instance.PlayerEntity;
            playerDeath = GameManager.Instance.PlayerDeath;

            OnPlayerDeath = PlayerDeath.OnPlayerDeath;
            PlayerDeath.OnPlayerDeath = null;

            PlayerDeath.OnPlayerDeath += PlayerDeath_OnPlayerDeathHandler;
            PlayerEnterExit.OnTransitionDungeonExterior += PlayerEnterExit_OnTransitionExterior;
            PlayerEnterExit.OnTransitionExterior += PlayerEnterExit_OnTransitionExterior;
            PlayerEnterExit.OnPreTransition += PlayerEnterExit_OnPreTransition;
            StartGameBehaviour.OnStartMenu += StartGameBehaviour_OnStartMenu;

            SaveLoadManager.OnLoad += SaveLoadManager_OnLoad;

            ModCompatibilityChecking();

            CustomItems = typeof(ItemCollection).GetField("customItems", BindingFlags.NonPublic | BindingFlags.Static);
            if (CustomItems != null)
                Debug.Log("PLAYER DEFEAT - FOUND CUSTOMITEMS FIELD.");

            ConsoleCommandsDatabase.RegisterCommand(FixMyArmor.name, FixMyArmor.description, FixMyArmor.usage, FixMyArmor.Execute);
        }

        private void ModCompatibilityChecking()
        {
            FindingMyReligion = ModManager.Instance.GetModFromGUID("b8644991-c0ef-419a-b334-9eacb39b304a");
            ClimatesAndCalories = ModManager.Instance.GetModFromGUID("7975b109-1381-485b-bdfd-8d076bb5d0c9");
        }

        private static class FixMyArmor
        {
            public static readonly string name = "fixmyarmor";
            public static readonly string description = "resets the player's bugged armor values";
            public static readonly string usage = "fixmyarmor";

            public static string Execute(params string[] args)
            {
                Instance.ResetPlayerArmorValues();
                return "Fixing armor values. Re-equip your gear.";
            }
        }

        private void Update()
        {
            if (defeated != null)
                GameManager.Instance.PlayerMotor.CancelMovement = true;
        }

        void Death()
        {
            Debug.Log("PLAYER DEFEAT - DEATH!");

            if (OnPlayerDeath != null)
                OnPlayerDeath(this, null);

            playerDeath.enabled = true;
        }

        bool SurvivalCheck()
        {
            //if defeated within a short time of waking up, always die
            if (Time.time - TimeSinceWakeUp < 5)
            {
                Debug.Log("PLAYER DEFEAT - BREAK THE LOOP!");
                return false;
            }

            if (survivalChance == 100)
                return true;

            if (survivalChance == 0)
                return false;

            //If Stendarr's Blessing procs, ensure survival
            if (GameManager.Instance.GuildManager.AvoidDeath())
            {
                Debug.Log("PLAYER DEFEAT - STENDARR'S BLESSING!");
                return true;
            }

            //base chance from mod settings
            int chance = survivalChance;

            //100 ENDURANCE adds 50% to survival chance
            chance += Mathf.FloorToInt(playerEntity.Stats.GetLiveStatValue(DFCareer.Stats.Endurance) / 2);

            //100 WILLPOWER adds 25% to survival chance
            chance += Mathf.FloorToInt(playerEntity.Stats.GetLiveStatValue(DFCareer.Stats.Willpower) / 4);

            //100 LUCK adds 10% to survival chance
            chance += Mathf.FloorToInt(playerEntity.Stats.GetLiveStatValue(DFCareer.Stats.Luck) / 10);

            Debug.Log("PLAYER DEFEAT - SURVIVAL CHANCE IS " + chance.ToString() + "%!");
            return Dice100.SuccessRoll(chance);
        }

        bool CaptureCheck(EnemyEntity enemy)
        {
            if (enemy == null)
                return false;

            if (captureChance == 100)
                return true;

            if (captureChance == 0)
                return false;

            //base chance from mod settings
            int chance = captureChance;

            //humanoid enemies adds 20% to capture chance
            if (enemy.EntityType == EntityTypes.EnemyClass)
            {
                chance += 20;

                //Add 1% per guild membership rank to capture chance
                List<IGuild> memberships = GameManager.Instance.GuildManager.GetMemberships();
                if (memberships.Count > 0)
                {
                    foreach (IGuild membership in memberships)
                    {
                        chance += membership.Rank;
                    }
                }
            }

            //100 LUCK subtracts 20% from capture chance
            chance -= Mathf.FloorToInt(playerEntity.Stats.GetLiveStatValue(DFCareer.Stats.Luck) / 5);

            Debug.Log("PLAYER DEFEAT - CAPTURE CHANCE IS " + chance.ToString() + "%!");

            return Dice100.SuccessRoll(chance);
        }

        bool RescueCheck(bool friendliesNearby = false)
        {
            if (rescueChance == 100)
                return true;

            if (rescueChance == 0)
                return false;

            //base chance from mod settings
            int chance = rescueChance;

            //nearby pacified monsters adds 20% to rescue chance
            if (friendliesNearby)
                chance += 20;

            //if current location is a town (does not have a dungeon)
            if (GameManager.Instance.PlayerGPS.CurrentLocation.Loaded && !GameManager.Instance.PlayerGPS.CurrentLocation.HasDungeon)
                chance += 20;

            //100 LUCK adds 20% to rescue chance
            chance += Mathf.FloorToInt(playerEntity.Stats.GetLiveStatValue(DFCareer.Stats.Luck) / 5);

            //Add 1% per guild membership rank to rescue chance
            List<IGuild> memberships = GameManager.Instance.GuildManager.GetMemberships();
            if (memberships.Count > 0)
            {
                foreach (IGuild membership in memberships)
                {
                    chance += membership.Rank;
                }
            }

            Debug.Log("PLAYER DEFEAT - RESCUE CHANCE IS " + chance.ToString() + "%!");

            return Dice100.SuccessRoll(chance);
        }

        bool DiseaseCheck()
        {
            if (playerEntity.IsImmuneToDisease)
                return false;

            if (diseaseChance == 100)
                return true;

            if (diseaseChance == 0)
                return false;

            //base chance from mod settings
            int chance = diseaseChance;

            //100 LUCK subtracts 5% to disease chance
            chance -= Mathf.FloorToInt(playerEntity.Stats.GetLiveStatValue(DFCareer.Stats.Luck) / 20);

            Debug.Log("PLAYER DEFEAT - DISEASE CHANCE IS " + chance.ToString() + "%!");

            return Dice100.SuccessRoll(chance);
        }

        IEnumerator DefeatCoroutine()
        {
            //GameManager.Instance.PlayerMouseLook.ForceHideCursor(true);

            //initialize death camera variables
            float startCameraY = GameManager.Instance.MainCamera.transform.localPosition.y;
            float endCameraY = playerEntity.EntityBehaviour.GetComponent<CharacterController>().height - (playerEntity.EntityBehaviour.GetComponent<CharacterController>().height * 1.25f);
            Vector3 currentCameraPos = Vector3.zero;

            //play death camera animation
            float currentCameraY = startCameraY;
            while (currentCameraY > endCameraY)
            {
                currentCameraY = Mathf.MoveTowards(currentCameraY, endCameraY, Time.deltaTime);

                currentCameraPos = GameManager.Instance.MainCamera.transform.localPosition;
                currentCameraPos.y = currentCameraY;
                GameManager.Instance.MainCamera.transform.localPosition = currentCameraPos;

                yield return new WaitForEndOfFrame();
            }

            yield return new WaitForSeconds(1);

            //do teleport and other things here

            //check if the player died due to stat loss from poison or disease
            //death via stat loss cannot be survived
            if (PlayerDiedFromStatLoss())
            {
                //do vanilla death instead
                Debug.Log("PLAYER DEFEAT - STAT IS AT ZERO! PLAYER DIED!");
                Death();

                defeated = null;
                yield break;
            }

            //cure any poisons (to prevent repeated dying due to health loss)
            GameManager.Instance.PlayerEffectManager.CureAllPoisons();

            //check for nearest enemy
            EnemyEntity nearestEnemy = GetNearestEnemy();

            //if nearest enemy is pacified, increase the chances of being rescued
            bool friendliesNearby = false;
            if (nearestEnemy != null)
                friendliesNearby = !nearestEnemy.EntityBehaviour.GetComponent<EnemyMotor>().IsHostile;

            bool captured = CaptureCheck(nearestEnemy);
            bool rescued = RescueCheck(friendliesNearby);

            string message = "";
            NewLocation = null;

            playerEntity.IsResting = true;
            playerEntity.PreventEnemySpawns = true;

            if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeon && (!rescued || GameManager.Instance.PlayerEnterExit.Dungeon.Summary.DungeonType == DFRegion.DungeonTypes.Prison))
            {
                //if player is killed in a dungeon, teleport them to a random quest marker in the same dungeon
                //if player is in a Prison dungeon, they cannot be rescued
                //no warping required
                message = "You regain consciousness after some time...";
                yield return TrueGrit();
            }
            else if ((GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeonCastle && !rescued) || playerEntity.CrimeCommitted != PlayerEntity.Crimes.None)
            {
                //if player is killed in a dungeon castle or with an active crime, warp them to a random prison dungeon
                NewLocation = GetDungeonLocation(DFRegion.DungeonTypes.Prison);
                Debug.Log("PLAYER DEFEAT - PLAYER WAS IMPRISONED!");
                message = "As you come to your senses, you hear someone sneer: 'You'll rot in here forever, scum!'";

                if (NewLocation != null)
                {
                    yield return WarpToPosition(NewLocation);
                }
                else
                {
                    //do vanilla death instead
                    Debug.Log("PLAYER DEFEAT - NO LOCATIONS FOUND! PLAYER DIED!");
                    Death();

                    defeated = null;
                    yield break;
                }
            }
            else if (nearestEnemy != null && !friendliesNearby && captured)
            {
                //if player is killed outside a dungeon, get captured by nearest enemy
                Debug.Log("PLAYER DEFEAT - PLAYER WAS CAPTURED!");
                NewLocation = GetDungeonLocation(GetEnemyDungeonTypes(nearestEnemy));
                message = "You've met with a terrible fate, haven't you..?";

                if (NewLocation != null)
                {
                    Debug.Log("PLAYER DEFEAT - LOCATION FOUND! WARPING!");
                    yield return WarpToPosition(NewLocation);
                }
                else
                {
                    //do vanilla death instead
                    Debug.Log("PLAYER DEFEAT - NO LOCATIONS FOUND! PLAYER DIED!");
                    Death();

                    defeated = null;
                    yield break;
                }
            }
            else if (rescued)
            {
                //player was rescued, warp them to a nearby safe location
                Debug.Log("PLAYER DEFEAT - PLAYER WAS RESCUED!");

                NewLocation = GetSafeLocation();
                message = "You wake up in a safe place...";

                //Cure any diseases at a safe haven
                //GameManager.Instance.PlayerEffectManager.CureAllDiseases();

                if (NewLocation != null)
                {
                    Debug.Log("PLAYER DEFEAT - LOCATION FOUND! WARPING!");
                    yield return WarpToPosition(NewLocation);
                }
                else
                {

                    message = "You regain consciousness after some time...";
                    yield return TrueGrit();
                }
            }
            else
            {

                message = "You regain consciousness after some time...";
                yield return TrueGrit();
            }

            //check if player contracts a disease
            if (DiseaseCheck())
            {
                EntityEffectBundle bundle = GameManager.Instance.PlayerEffectManager.CreateDisease((Diseases)UnityEngine.Random.Range(0, 17));
                GameManager.Instance.PlayerEffectManager.AssignBundle(bundle);
            }

            ApplyEnemySpecialResults(nearestEnemy);

            yield return new WaitForSeconds(1);

            //open your eyes
            DaggerfallUI.Instance.FadeBehaviour.FadeHUDFromBlack(2);

            yield return new WaitForSeconds(0.5f);

            playerEntity.IsResting = false;
            playerEntity.PreventEnemySpawns = false;

            //if racial override effect is not null, restore it
            if (racialOverrideEffect != null)
            {
                if (racialOverrideEffect is VampirismEffect)
                {
                    Debug.Log("PLAYER DEFEAT - PLAYER IS A VAMPIRE! REAPPLYING VAMPIRISM!");
                    VampirismEffect racialOverrideVampirismEffect = racialOverrideEffect as VampirismEffect;
                    EntityEffectBundle vampirismEffectBundle = null;
                    vampirismEffectBundle = GameManager.Instance.PlayerEffectManager.CreateVampirismCurse();
                    GameManager.Instance.PlayerEffectManager.AssignBundle(vampirismEffectBundle, AssignBundleFlags.BypassSavingThrows);
                    VampirismEffect vampirismEffect = (VampirismEffect)GameManager.Instance.PlayerEffectManager.FindIncumbentEffect<VampirismEffect>();
                    if (vampirismEffect != null)
                    {
                        vampirismEffect.VampireClan = racialOverrideVampirismEffect.VampireClan;
                        FieldInfo sourceBool = racialOverrideVampirismEffect.GetType().GetField("hasStartedInitialVampireQuest", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.Instance);
                        FieldInfo targetBool = vampirismEffect.GetType().GetField("hasStartedInitialVampireQuest", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.Instance);
                        if (sourceBool != null && targetBool != null)
                            targetBool.SetValue(vampirismEffect, sourceBool.GetValue(racialOverrideVampirismEffect));
                        else
                            Debug.Log("PLAYER DEFEAT - UNABLE TO REAPPLY VAMPIRISM BOOLS DUE TO MISSING REFLECTION CALL!");
                    }
                }
                else if (racialOverrideEffect is LycanthropyEffect)
                {
                    EntityEffectBundle lycanthropyEffectBundle = null;
                    lycanthropyEffectBundle = GameManager.Instance.PlayerEffectManager.CreateLycanthropyCurse();
                    GameManager.Instance.PlayerEffectManager.AssignBundle(lycanthropyEffectBundle, AssignBundleFlags.BypassSavingThrows);
                    LycanthropyEffect lycanthropyEffect = (LycanthropyEffect)GameManager.Instance.PlayerEffectManager.FindIncumbentEffect<LycanthropyEffect>();
                    if (lycanthropyEffect != null)
                    {
                        lycanthropyEffect.InfectionType = (racialOverrideEffect as LycanthropyEffect).InfectionType;
                        //player already has lycanthropy spell
                        //GameManager.Instance.PlayerEntity.AssignPlayerLycanthropySpell();
                    }
                }
                racialOverrideEffect = GameManager.Instance.PlayerEffectManager.GetRacialOverrideEffect();
            }


            //reverse death camera animation somehow
            currentCameraY = endCameraY;
            while (currentCameraY < startCameraY)
            {
                currentCameraY = Mathf.MoveTowards(currentCameraY, startCameraY, Time.deltaTime);

                currentCameraPos = GameManager.Instance.MainCamera.transform.localPosition;
                currentCameraPos.y = currentCameraY;
                GameManager.Instance.MainCamera.transform.localPosition = currentCameraPos;

                yield return new WaitForEndOfFrame();
            }

            //add scenario message here?
            DaggerfallUI.MessageBox(message,true);

            //give the player back control
            playerDeath.ClearDeathAnimation();
            playerEntity.SetHealth(Mathf.Clamp((int)(playerEntity.MaxHealth*UnityEngine.Random.value),1,playerEntity.MaxHealth), true);

            TimeSinceWakeUp = Time.time;

            //GameManager.Instance.PlayerMouseLook.ForceHideCursor(false);

            defeated = null;
        }

        IEnumerator TrueGrit()
        {
            if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeon)
            {
                //place player on a random quest marker
                Vector3[] markers = GetMarkers(MarkerTypes.QuestSpawn);
                if (markers.Length < 1)
                    markers = GetMarkers(MarkerTypes.QuestItem);
                if (markers.Length < 1)
                    GameManager.Instance.PlayerObject.transform.position = GameManager.Instance.PlayerEnterExit.Dungeon.StartMarker.transform.position;
                else
                    GameManager.Instance.PlayerObject.transform.position = markers[UnityEngine.Random.Range(0, markers.Length)];
                GameManager.Instance.PlayerMotor.FixStanding();
                StripPlayerInventory();
            }
            else
            {
                //player wakes up at a random start marker or in the same place

                if (GameManager.Instance.PlayerGPS.HasCurrentLocation && !GameManager.Instance.PlayerEnterExit.IsPlayerInside)
                    GameManager.Instance.StreamingWorld.SetAutoReposition(StreamingWorld.RepositionMethods.RandomStartMarker, Vector3.zero);
            }

            yield return new WaitForSeconds(1);

            if (ClimatesAndCalories != null)
            {
                //wait for next magic round to make sure C&C (and maybe R&R Encumberance Effects) are correctly updated before performing time skip
                yield return new WaitForSeconds(5);
            }

            float skipTimeSeconds = 86400f * UnityEngine.Random.Range(0.1f, 1f);
            int skipTimeHours = (int)(skipTimeSeconds / 3600);

            Debug.Log("PLAYER DEFEAT - PLAYER WAS LEFT FOR DEAD! TIME SKIP IS " + skipTimeHours.ToString() + " HOURS!");

            DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.RaiseTime(skipTimeSeconds);
        }

        bool PlayerDiedFromStatLoss()
        {
            bool loss = false;

            if (playerEntity.Stats.GetLiveStatValue(DFCareer.Stats.Agility) < 1 ||
                playerEntity.Stats.GetLiveStatValue(DFCareer.Stats.Endurance) < 1 ||
                playerEntity.Stats.GetLiveStatValue(DFCareer.Stats.Intelligence) < 1 ||
                playerEntity.Stats.GetLiveStatValue(DFCareer.Stats.Luck) < 1 ||
                playerEntity.Stats.GetLiveStatValue(DFCareer.Stats.Personality) < 1 ||
                playerEntity.Stats.GetLiveStatValue(DFCareer.Stats.Speed) < 1 ||
                playerEntity.Stats.GetLiveStatValue(DFCareer.Stats.Strength) < 1 ||
                playerEntity.Stats.GetLiveStatValue(DFCareer.Stats.Willpower) < 1
                )
                loss = true;

            return loss;
        }

        EnemyEntity GetNearestEnemy()
        {
            EnemyEntity nearestEnemy = null;
            float closestDistance = Mathf.Infinity;

            foreach (DaggerfallEntityBehaviour entityBehaviour in ActiveGameObjectDatabase.GetActiveEnemyBehaviours())
            {
                if (entityBehaviour.EntityType == EntityTypes.EnemyMonster || entityBehaviour.EntityType == EntityTypes.EnemyClass)
                {
                    EnemyMotor enemyMotor = entityBehaviour.GetComponent<EnemyMotor>();
                    EnemyEntity enemyEntity = entityBehaviour.Entity as EnemyEntity;

                    float currentDistance = Vector3.Distance(GameManager.Instance.PlayerObject.transform.position, entityBehaviour.transform.position);
                    if (currentDistance < closestDistance)
                    {
                        nearestEnemy = enemyEntity;
                        closestDistance = currentDistance;
                    }
                }
            }

            return nearestEnemy;
        }

        void ApplyEnemySpecialResults(EnemyEntity enemy)
        {
            if (enemy == null)
                return;

            EntityEffectManager effectManager = GameManager.Instance.PlayerEffectManager;

            //make sure player isn't already a lycanthrope or vampire
            if (!effectManager.HasLycanthropy() && !effectManager.HasVampirism())
            {
                EntityEffectBundle bundle = null;
                if (enemy.MobileEnemy.ID == (int)MobileTypes.Werewolf)
                    bundle = effectManager.CreateLycanthropyDisease(LycanthropyTypes.Werewolf);

                if (enemy.MobileEnemy.ID == (int)MobileTypes.Wereboar)
                    bundle = effectManager.CreateLycanthropyDisease(LycanthropyTypes.Wereboar);

                if (enemy.MobileEnemy.ID == (int)MobileTypes.Vampire || enemy.MobileEnemy.ID == (int)MobileTypes.VampireAncient)
                    bundle = effectManager.CreateVampirismDisease();

                if (bundle != null)
                    effectManager.AssignBundle(bundle, AssignBundleFlags.BypassSavingThrows);
            }
        }

        List<DFRegion.DungeonTypes> GetEnemyDungeonTypes(EnemyEntity enemy)
        {
            List<DFRegion.DungeonTypes> dungeonTypes = new List<DFRegion.DungeonTypes>();

            if (enemy.Team == MobileTeams.Vermin)
            {
                dungeonTypes.Add(DFRegion.DungeonTypes.NaturalCave);
            }
            else if (enemy.Team == MobileTeams.Spriggans)
            {
                dungeonTypes.Add(DFRegion.DungeonTypes.NaturalCave);
                dungeonTypes.Add(DFRegion.DungeonTypes.SpiderNest);
            }
            else if (enemy.Team == MobileTeams.Bears)
            {
                dungeonTypes.Add(DFRegion.DungeonTypes.Mine);
                dungeonTypes.Add(DFRegion.DungeonTypes.NaturalCave);

            }
            else if (enemy.Team == MobileTeams.Tigers)
            {
                dungeonTypes.Add(DFRegion.DungeonTypes.NaturalCave);
                dungeonTypes.Add(DFRegion.DungeonTypes.VolcanicCaves);
            }
            else if (enemy.Team == MobileTeams.Spiders)
            {
                dungeonTypes.Add(DFRegion.DungeonTypes.Crypt);
                dungeonTypes.Add(DFRegion.DungeonTypes.Mine);
                dungeonTypes.Add(DFRegion.DungeonTypes.NaturalCave);
                dungeonTypes.Add(DFRegion.DungeonTypes.SpiderNest);
            }
            else if (enemy.Team == MobileTeams.Orcs)
            {
                dungeonTypes.Add(DFRegion.DungeonTypes.DesecratedTemple);
                dungeonTypes.Add(DFRegion.DungeonTypes.GiantStronghold);
                dungeonTypes.Add(DFRegion.DungeonTypes.NaturalCave);
                dungeonTypes.Add(DFRegion.DungeonTypes.OrcStronghold);
                dungeonTypes.Add(DFRegion.DungeonTypes.RuinedCastle);
            }
            else if (enemy.Team == MobileTeams.Centaurs)
            {
                dungeonTypes.Add(DFRegion.DungeonTypes.BarbarianStronghold);
                dungeonTypes.Add(DFRegion.DungeonTypes.HumanStronghold);
            }
            else if (enemy.Team == MobileTeams.Werecreatures)
            {
                dungeonTypes.Add(DFRegion.DungeonTypes.BarbarianStronghold);
                dungeonTypes.Add(DFRegion.DungeonTypes.GiantStronghold);
                dungeonTypes.Add(DFRegion.DungeonTypes.NaturalCave);
                dungeonTypes.Add(DFRegion.DungeonTypes.RuinedCastle);
                dungeonTypes.Add(DFRegion.DungeonTypes.VampireHaunt);
                dungeonTypes.Add(DFRegion.DungeonTypes.VolcanicCaves);
            }
            else if (enemy.Team == MobileTeams.Nymphs)
            {
                dungeonTypes.Add(DFRegion.DungeonTypes.Coven);
                dungeonTypes.Add(DFRegion.DungeonTypes.DragonsDen);
            }
            else if (enemy.Team == MobileTeams.Harpies)
            {
                dungeonTypes.Add(DFRegion.DungeonTypes.Coven);
                dungeonTypes.Add(DFRegion.DungeonTypes.DesecratedTemple);
                dungeonTypes.Add(DFRegion.DungeonTypes.DragonsDen);
                dungeonTypes.Add(DFRegion.DungeonTypes.HarpyNest);
                dungeonTypes.Add(DFRegion.DungeonTypes.NaturalCave);
                dungeonTypes.Add(DFRegion.DungeonTypes.VolcanicCaves);
            }
            else if (enemy.Team == MobileTeams.Undead)
            {
                dungeonTypes.Add(DFRegion.DungeonTypes.Crypt);
                dungeonTypes.Add(DFRegion.DungeonTypes.DesecratedTemple);
                dungeonTypes.Add(DFRegion.DungeonTypes.RuinedCastle);
                dungeonTypes.Add(DFRegion.DungeonTypes.VampireHaunt);
            }
            else if (enemy.Team == MobileTeams.Giants)
            {
                dungeonTypes.Add(DFRegion.DungeonTypes.GiantStronghold);
                dungeonTypes.Add(DFRegion.DungeonTypes.Mine);
                dungeonTypes.Add(DFRegion.DungeonTypes.OrcStronghold);
                dungeonTypes.Add(DFRegion.DungeonTypes.RuinedCastle);
                dungeonTypes.Add(DFRegion.DungeonTypes.VolcanicCaves);
            }
            else if (enemy.Team == MobileTeams.Scorpions)
            {
                dungeonTypes.Add(DFRegion.DungeonTypes.Crypt);
                dungeonTypes.Add(DFRegion.DungeonTypes.Mine);
                dungeonTypes.Add(DFRegion.DungeonTypes.NaturalCave);
                dungeonTypes.Add(DFRegion.DungeonTypes.ScorpionNest);
            }
            else if (enemy.Team == MobileTeams.Magic)
            {
                dungeonTypes.Add(DFRegion.DungeonTypes.Coven);
                dungeonTypes.Add(DFRegion.DungeonTypes.DesecratedTemple);
                dungeonTypes.Add(DFRegion.DungeonTypes.Laboratory);
            }
            else if (enemy.Team == MobileTeams.Daedra)
            {
                dungeonTypes.Add(DFRegion.DungeonTypes.Coven);
                dungeonTypes.Add(DFRegion.DungeonTypes.DesecratedTemple);
            }
            else if (enemy.Team == MobileTeams.Dragonlings)
            {
                dungeonTypes.Add(DFRegion.DungeonTypes.DesecratedTemple);
                dungeonTypes.Add(DFRegion.DungeonTypes.GiantStronghold);
                dungeonTypes.Add(DFRegion.DungeonTypes.NaturalCave);
            }
            else if (enemy.Team == MobileTeams.KnightsAndMages)
            {
                dungeonTypes.Add(DFRegion.DungeonTypes.BarbarianStronghold);
                dungeonTypes.Add(DFRegion.DungeonTypes.Coven);
                dungeonTypes.Add(DFRegion.DungeonTypes.DragonsDen);
                dungeonTypes.Add(DFRegion.DungeonTypes.HumanStronghold);
                dungeonTypes.Add(DFRegion.DungeonTypes.Laboratory);
            }
            else if (enemy.Team == MobileTeams.Criminals)
            {
                dungeonTypes.Add(DFRegion.DungeonTypes.BarbarianStronghold);
                dungeonTypes.Add(DFRegion.DungeonTypes.DragonsDen);
                dungeonTypes.Add(DFRegion.DungeonTypes.HumanStronghold);
                dungeonTypes.Add(DFRegion.DungeonTypes.Mine);
                dungeonTypes.Add(DFRegion.DungeonTypes.SpiderNest);
            }

            return dungeonTypes;
        }

        DFPosition GetSafeLocation()
        {
            DFPosition startPos = TravelTimeCalculator.GetPlayerTravelPosition();
            DFPosition newPos;

            //get nearby locations
            List<Vector2Int> locations = new List<Vector2Int>();
            for (int x = startPos.X - maxLocationRange; x < startPos.X + maxLocationRange + 1; x++)
            {
                if (x < MapsFile.MinMapPixelX || x > MapsFile.MaxMapPixelX)
                    continue;

                for (int y = startPos.Y - maxLocationRange; y < startPos.Y + maxLocationRange + 1; y++)
                {
                    if (y < MapsFile.MinMapPixelY || y > MapsFile.MaxMapPixelY)
                        continue;

                    Vector2Int location = new Vector2Int(x, y);

                    ContentReader.MapSummary mapSummary;
                    bool hasLocation = DaggerfallUnity.Instance.ContentReader.HasLocation(x, y, out mapSummary);
                    if (hasLocation)
                    {
                        DaggerfallUnity.Instance.ContentReader.GetLocation(mapSummary.RegionIndex, mapSummary.MapIndex, out DFLocation dfLocation);

                        foreach (DFLocation.BuildingData building in dfLocation.Exterior.Buildings)
                        {
                            if (building.BuildingType == DFLocation.BuildingTypes.Tavern || (building.BuildingType == DFLocation.BuildingTypes.Temple && FindingMyReligion != null))
                            {
                                locations.Add(location);
                                break;
                            }
                            continue;
                        }
                    }
                }
            }

            if (locations.Count > 0)
            {
                //get closest safe location
                Vector2Int closestLocation = -Vector2Int.one;
                float closestDistance = Mathf.Infinity;
                Vector2 startPosV = new Vector2(startPos.X, startPos.Y);
                foreach (Vector2Int location in locations)
                {
                    float currentDistance = Vector2.Distance(startPosV, (Vector2)location);
                    if (currentDistance < closestDistance)
                    {
                        closestLocation = location;
                        closestDistance = currentDistance;
                    }
                }

                newPos = new DFPosition(closestLocation.x, closestLocation.y);

                return newPos;
            }
            else
            {
                //handle no safe locations in range
                return null;
            }
        }

        DFPosition GetDungeonLocation(DFRegion.DungeonTypes dungeonType)
        {
            DFPosition startPos = TravelTimeCalculator.GetPlayerTravelPosition();
            DFPosition newPos;

            //get nearby locations
            List<Vector2Int> locations = new List<Vector2Int>();
            for (int i = (int)startPos.X - maxLocationRange; i < (int)startPos.X + maxLocationRange + 1; i++)
            {
                if (i < MapsFile.MinMapPixelX || i > MapsFile.MaxMapPixelX)
                    continue;

                for (int ii = (int)startPos.Y - maxLocationRange; ii < (int)startPos.Y + maxLocationRange + 1; ii++)
                {
                    if (ii < MapsFile.MinMapPixelY || ii > MapsFile.MaxMapPixelY)
                        continue;

                    Vector2Int location = new Vector2Int(i, ii);

                    ContentReader.MapSummary mapSummary;
                    bool hasLocation = DaggerfallUnity.Instance.ContentReader.HasLocation(i, ii, out mapSummary);
                    if (hasLocation)
                    {
                        if (mapSummary.DungeonType == dungeonType)
                        {
                            locations.Add(location);
                        }
                    }
                }
            }

            if (locations.Count > 0)
            {
                //get closest location
                Vector2Int closestLocation = -Vector2Int.one;
                float closestDistance = Mathf.Infinity;
                Vector2 startPosV = new Vector2(startPos.X, startPos.Y);
                foreach (Vector2Int neighbor in locations)
                {
                    float currentDistance = Vector2.Distance(startPosV, (Vector2)neighbor);
                    if (currentDistance < closestDistance)
                    {
                        closestLocation = neighbor;
                        closestDistance = currentDistance;
                    }
                }

                newPos = new DFPosition(closestLocation.x, closestLocation.y);

                return newPos;
            }
            else
            {
                //handle no safe locations in range
                return null;
            }
        }

        DFPosition GetDungeonLocation(List<DFRegion.DungeonTypes> dungeonTypes)
        {
            if (dungeonTypes.Count < 1)
                return null;

            DFPosition startPos = TravelTimeCalculator.GetPlayerTravelPosition();
            DFPosition newPos;

            //get nearby locations
            List<Vector2Int> locations = new List<Vector2Int>();
            for (int i = (int)startPos.X - maxLocationRange; i < (int)startPos.X + maxLocationRange + 1; i++)
            {
                if (i < MapsFile.MinMapPixelX || i > MapsFile.MaxMapPixelX)
                    continue;

                for (int ii = (int)startPos.Y - maxLocationRange; ii < (int)startPos.Y + maxLocationRange + 1; ii++)
                {
                    if (ii < MapsFile.MinMapPixelY || ii > MapsFile.MaxMapPixelY)
                        continue;

                    Vector2Int location = new Vector2Int(i, ii);

                    ContentReader.MapSummary mapSummary;
                    bool hasLocation = DaggerfallUnity.Instance.ContentReader.HasLocation(i, ii, out mapSummary);
                    if (hasLocation)
                    {
                        if (dungeonTypes.Contains(mapSummary.DungeonType))
                        {
                            locations.Add(location);
                        }
                    }
                }
            }

            if (locations.Count > 0)
            {
                //get closest location
                Vector2Int closestLocation = -Vector2Int.one;
                float closestDistance = Mathf.Infinity;
                Vector2 startPosV = new Vector2(startPos.X, startPos.Y);
                foreach (Vector2Int neighbor in locations)
                {
                    float currentDistance = Vector2.Distance(startPosV, (Vector2)neighbor);
                    if (currentDistance < closestDistance)
                    {
                        closestLocation = neighbor;
                        closestDistance = currentDistance;
                    }
                }

                newPos = new DFPosition(closestLocation.x, closestLocation.y);

                return newPos;
            }
            else
            {
                //handle no safe locations in range
                return null;
            }
        }

        IEnumerator WarpToPosition(DFPosition position)
        {
            //put teleporting here

            //check if already in same place
            //if in same place just skip to warping
            if (GameManager.Instance.PlayerGPS.CurrentMapPixel != position)
            {
                // Cache scene first, if fast travelling while on ship.
                if (GameManager.Instance.TransportManager.IsOnShip())
                    SaveLoadManager.CacheScene(GameManager.Instance.StreamingWorld.SceneName);
                GameManager.Instance.StreamingWorld.RestoreWorldCompensationHeight(0);
                GameManager.Instance.StreamingWorld.TeleportToCoordinates((int)position.X, (int)position.Y, StreamingWorld.RepositionMethods.RandomStartMarker);

                yield return new WaitForSeconds(1);
            }

            Vector3 playerPos = Vector3.zero;
            DaggerfallLocation currentLocation = GameManager.Instance.StreamingWorld.CurrentPlayerLocationObject;
            DaggerfallDateTime currentTime = DaggerfallUnity.Instance.WorldTime.Now;

            if (currentLocation.Summary.HasDungeon)
            {
                //dungeon

                if (!GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeon)
                {
                    //get any door to the dungeon and activate it
                    DaggerfallStaticDoors staticDoorCollection = currentLocation.StaticDoorCollections[0];
                    StaticDoor staticDoor = staticDoorCollection.Doors[0];
                    GameManager.Instance.PlayerEnterExit.TransitionDungeonInterior(staticDoorCollection.transform, staticDoor, GameManager.Instance.PlayerGPS.CurrentLocation);

                    yield return new WaitForSeconds(0.2f);
                }

                //place player on a random quest marker
                Vector3[] markers = GetMarkers(MarkerTypes.QuestSpawn);
                if (markers.Length < 1)
                    markers = GetMarkers(MarkerTypes.QuestItem);

                //if no quest markers are found, place player at start marker
                if (markers.Length < 1)
                    playerPos = GameManager.Instance.PlayerEnterExit.Dungeon.StartMarker.transform.position;
                else
                    playerPos = markers[UnityEngine.Random.Range(0, markers.Length)];

                GameManager.Instance.PlayerObject.transform.position = playerPos;
                GameManager.Instance.PlayerMotor.FixStanding();

                StripPlayerInventory();
            }
            else
            {
                //safe location

                //place player inside tavern
                List<BuildingSummary> buildingSummaries = GameManager.Instance.StreamingWorld.GetCurrentBuildingDirectory().GetBuildingsOfType(DFLocation.BuildingTypes.Tavern);
                if (FindingMyReligion != null)
                    buildingSummaries.AddRange(GameManager.Instance.StreamingWorld.GetCurrentBuildingDirectory().GetBuildingsOfType(DFLocation.BuildingTypes.Temple));
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

                                //place player inside tavern interior
                                // Get building discovery data - this is added when player clicks door at exterior
                                GameManager.Instance.PlayerGPS.DiscoverBuilding(door.buildingKey);
                                GameManager.Instance.PlayerGPS.GetDiscoveredBuilding(door.buildingKey, out PlayerGPS.DiscoveredBuilding db);

                                if (GameManager.Instance.PlayerEnterExit.BuildingDiscoveryData.buildingKey != db.buildingKey)
                                {
                                    // Perform transition
                                    GameManager.Instance.PlayerEnterExit.BuildingDiscoveryData = db;
                                    GameManager.Instance.PlayerEnterExit.IsPlayerInsideOpenShop = RMBLayout.IsShop(db.buildingType) && PlayerActivate.IsBuildingOpen(db.buildingType);
                                    GameManager.Instance.PlayerEnterExit.IsPlayerInsideTavern = RMBLayout.IsTavern(db.buildingType);
                                    GameManager.Instance.PlayerEnterExit.IsPlayerInsideResidence = RMBLayout.IsResidence(db.buildingType);
                                    GameManager.Instance.PlayerEnterExit.TransitionInterior(staticDoors.transform, door, false, false);

                                    yield return new WaitForSeconds(0.2f);
                                }

                                GameManager.Instance.PlayerEnterExit.Interior.FindMarker(out playerPos, DaggerfallInterior.InteriorMarkerTypes.Rest, true);
                                if (playerPos == Vector3.zero)
                                    GameManager.Instance.PlayerEnterExit.Interior.FindMarker(out playerPos, DaggerfallInterior.InteriorMarkerTypes.Enter, true);
                                GameManager.Instance.PlayerObject.transform.position = playerPos;
                                GameManager.Instance.PlayerMotor.FixStanding();

                                RoomRental_v1 rentedRoom = GameManager.Instance.PlayerEntity.GetRentedRoom(GameManager.Instance.PlayerGPS.CurrentMapID, db.buildingKey);

                                ulong seconds = 0;
                                int minutes = 0;
                                int hours = 0;
                                if (currentTime.Hour < 8)
                                    hours = 8 - currentTime.Hour;
                                else
                                    hours = 32 - currentTime.Hour;

                                if (currentTime.Minute > 0)
                                {
                                    minutes = 60 - currentTime.Minute;
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

            if (ClimatesAndCalories != null)
            {
                //wait for next magic round to make sure C&C (and maybe R&R Encumberance Effects) are correctly updated before performing time skip
                yield return new WaitForSeconds(5);
            }

            float skipTimeSeconds = 86400f * UnityEngine.Random.Range(daysPassedMin, daysPassedMax);
            int skipTimeHours = (int)(skipTimeSeconds / 3600);

            Debug.Log("PLAYER DEFEAT - PLAYER WARPED TO LOCATION! TIME SKIP IS " + skipTimeHours.ToString() + " HOURS!");

            DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.RaiseTime(skipTimeSeconds);
        }

        void StripPlayerInventory()
        {
            //unequip all items
            DaggerfallUnityItem[] equippedItems = playerEntity.ItemEquipTable.EquipTable;
            foreach (DaggerfallUnityItem equippedItem in equippedItems)
            {
                if (playerEntity.ItemEquipTable.UnequipItem(equippedItem))
                    playerEntity.UpdateEquippedArmorValues(equippedItem, false);
            }

            //record gold
            int goldCount = playerEntity.GoldPieces;

            if (playerEntity.Items.Count > 0 || goldCount > 0)
            {
                //reserve spellbook
                DaggerfallUnityItem spellbook = playerEntity.Items.GetItem(ItemGroups.MiscItems, (int)MiscItems.Spellbook);
                playerEntity.Items.RemoveItem(spellbook);

                items.TransferAll(playerEntity.Items);

                //transfer player gold into loot
                playerEntity.GoldPieces = 0;
                items.AddItem(ItemBuilder.CreateGoldPieces(goldCount));

                //return the spellbook
                playerEntity.Items.AddItem(spellbook);

                /*//place player inventory near the entrance
                Vector3 offset = new Vector3(UnityEngine.Random.Range(0, 2) * 2 - 1, 0, UnityEngine.Random.Range(0, 2) * 2 - 1);
                DaggerfallLoot loot = GameObjectHelper.CreateLootContainer(LootContainerTypes.DroppedLoot, InventoryContainerImages.Chest, marker.position + offset, marker, DaggerfallLootDataTables.randomTreasureArchive, DaggerfallLootDataTables.randomTreasureIconIndices[UnityEngine.Random.Range(0, DaggerfallLootDataTables.randomTreasureIconIndices.Length)]);

                loot.Items.TransferAll(playerEntity.Items);

                //transfer player gold into loot
                playerEntity.GoldPieces = 0;
                loot.Items.AddItem(ItemBuilder.CreateGoldPieces(goldCount));

                //fix loot pile position
                Billboard billboard = loot.gameObject.GetComponent<Billboard>();
                if (billboard)
                    GameObjectHelper.AlignBillboardToGround(billboard.gameObject, billboard.Summary.Size, 4);*/
            }

            GivePlayerSimpleClothing();
        }

        void ResetPlayerArmorValues()
        {
            //unequip all items
            DaggerfallUnityItem[] equippedItems = playerEntity.ItemEquipTable.EquipTable;
            foreach (DaggerfallUnityItem equippedItem in equippedItems)
            {
                if (playerEntity.ItemEquipTable.UnequipItem(equippedItem))
                    playerEntity.UpdateEquippedArmorValues(equippedItem, false);
            }

            for (int i = 0; i < playerEntity.ArmorValues.Length; i++)
            {
                playerEntity.ArmorValues[i] = 100;
            }
        }

        void RecoverPlayerInventory()
        {
            if (items.Count > 0)
            {
                ReducePlayerInventory();

                int goldCount = 0;
                DaggerfallUnityItem goldItem = items.GetItem(ItemGroups.Currency, (int)Currency.Gold_pieces);
                if (goldItem != null)
                {
                    goldCount = goldItem.stackCount;
                    items.RemoveItem(goldItem);

                    if (goldCount > 0)
                        playerEntity.GoldPieces += goldCount;
                }

                playerEntity.Items.TransferAll(items);

                //message
                string message = "You manage to recover some of your items...";
                DaggerfallUI.MessageBox(message, true);
            }
        }

        void ReducePlayerInventory(float min = 0, float max = 0.5f)
        {
            //get ItemData from collection
            ItemData_v1[] itemDataArray = items.SerializeItems();

            //Convert ItemData back to DaggerfallUnityIem and put in a list
            List<DaggerfallUnityItem> itemList = new List<DaggerfallUnityItem>();
            foreach (ItemData_v1 itemData in itemDataArray)
            {
                //check if it is a custom item
                if (itemData.className != null)
                {
                    Type itemClassType;
                    customItems = CustomItems.GetValue(null) as Dictionary<string, Type>;
                    customItems.TryGetValue(itemData.className, out itemClassType);
                    if (itemClassType != null)
                    {
                        //item is of a custom item type
                        DaggerfallUnityItem modItem = (DaggerfallUnityItem)Activator.CreateInstance(itemClassType);

                        //modItem.FromItemData(itemData);
                        MethodInfo FromItemData = modItem.GetType().GetMethod("FromItemData", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.Instance);
                        if (FromItemData != null)
                        {
                            Debug.Log("PLAYER DEFEAT - FOUND FROMITEMDATA METHOD.");
                            FromItemData.Invoke(modItem, new object[]
                            {
                                itemData
                            });
                        }

                        itemList.Add(modItem);
                        continue;
                    }
                }
                else
                    itemList.Add(new DaggerfallUnityItem(itemData));
            }

            //Do the thing
            foreach (DaggerfallUnityItem item in itemList)
            {
                //if a stackable item, like gold or potions, remove some of the stack
                if (item.IsAStack())
                    item.stackCount -= (int)(UnityEngine.Random.Range(min, item.stackCount * max));
                else
                    item.LowerCondition((int)UnityEngine.Random.Range(min, item.maxCondition * max));
            }

            //clear old collection and replace with new one
            items.Clear();
            items.AddItems(itemList);
        }

        void GivePlayerSimpleClothing()
        {
            List<DaggerfallUnityItem> newItems = new List<DaggerfallUnityItem>();
            if (playerEntity.Gender == Genders.Male)
            {
                //give men a loincloth
                newItems.Add(ItemBuilder.CreateMensClothing(MensClothing.Loincloth, playerEntity.Race, 0, DyeColors.Grey));
            }
            else
            {
                //women get the open shirt with tail
                newItems.Add(ItemBuilder.CreateWomensClothing(WomensClothing.Short_shirt, playerEntity.Race, 1, DyeColors.Grey));
            }

            playerEntity.Items.AddItems(newItems);

            foreach (DaggerfallUnityItem newItem in newItems)
            {
                playerEntity.ItemEquipTable.EquipItem(newItem, true, false);
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

        /// <summary>
        /// Collect markers inside a building.
        /// </summary>
        Vector3[] GetMarkers(MarkerTypes markerType)
        {
            List<Vector3> markersList = new List<Vector3>();

            DaggerfallBillboard[] billboards = FindObjectsOfType<DaggerfallBillboard>();

            foreach (DaggerfallBillboard billboard in billboards)
            {
                //add random flats, quests and entrances
                if ((markerType == MarkerTypes.QuestSpawn && billboard.name == "DaggerfallBillboard [TEXTURE.199, Index=11]") || (markerType == MarkerTypes.QuestItem && billboard.name == "DaggerfallBillboard [TEXTURE.199, Index=18]"))
                    markersList.Add(billboard.transform.position);
            }

            return markersList.ToArray();
        }
    }
}
