using System;
using System.Reflection;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;

namespace ActiveQuestorsMod
{
    public class ActiveQuestors : MonoBehaviour
    {
        private static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<ActiveQuestors>();

            mod.IsReady = true;
        }

        public static ActiveQuestors Instance;

        //gender preference
        //0 = no preference, 1 = same gender, 2 = opposite gender, 3 = men, 4 = women
        int preferredGender = 0;

        public string[] questsCommoners = new string[7]
            {
                "PROST000",
                "PROST005",
                "PROST006",
                "PROST007",
                "PROST008",
                "PROST016",
                "PROST017"
            };
        public string[] questsNobles = new string[5]
            {
                "PROST009",
                "PROST010",
                "PROST011",
                "PROST012",
                "PROST019"
            };
        public string[] questsProstitutes = new string[4]
            {
                "PROST001",
                "PROST002",
                "PROST003",
                "PROST004"
            };

        List<QuestData> questDataProstitutes = new List<QuestData>();

        public string[] qualitiesCommoners = new string[10]
            {
                "a comely-looking",
                "a crude-looking",
                "a homely",
                "a gruff-mannered",
                "a curt",
                "an earnestly-dressed",
                "an austerely-dressed",
                "a brusque",
                "a modestly-dressed",
                "a coarse-looking"
            };
        public string[] qualitiesNobles = new string[7]
            {
                "a fancily-dressed",
                "an elegantly-dressed",
                "a lofty-mannered",
                "a posh-looking",
                "a lavishly decorated",
                "an opulently-attired",
                "a gaudily-dressed"
            };
        public string[] qualitiesProstitute = new string[6]
            {
                "a sultry-looking",
                "a scantily-clad",
                "a sensuous",
                "a salacious",
                "a raunchy",
                "a provocatively-dressed"
            };

        public string[] mannersCommoners = new string[6]
            {
                "timidly",
                "conspirationally",
                "hesitantly",
                "apprehensively",
                "nervously",
                "excitedly",
            };
        public string[] mannersNobles = new string[6]
            {
                "imperiously",
                "stiffly",
                "grandly",
                "regally",
                "augustly",
                "reservedly",
            };
        public string[] mannersProstitute = new string[6]
            {
                "suggestively",
                "seductively",
                "wickedly",
                "immodestly",
                "lecherously",
                "indelicately"
            };

        int chanceBase = 5;

        int bonusValue = 5;
        int bonusMax = 5;
        int bonus = 1;

        int cooldownTime = 1;
        int cooldown = 0;

        bool EntryQuestors = true;
        bool ForeignQuestors = false;
        bool StreetQuestors = false;

        Quest offeredQuest;

        bool hasProstitutesAndLovers = false;

        //reflection
        FieldInfo LastNPCClicked;

        private void Start()
        {
            Instance = this;

            /*EndRest = DaggerfallUI.Instance..GetType().GetMethod("EndRest", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.Instance);
            if (TravelPopUpUpdateLabels != null)
                Debug.Log("FAST TRAVEL ENCOUNTER - FOUND UPDATELABELS METHOD");*/

            PlayerEnterExit.OnTransitionInterior += OnTransition_Interior;

            WorldTime.OnNewHour += OnNewHour;

            Quest prostitutesQuest = GameManager.Instance.QuestListsManager.GetQuest("PROST000");
            if (prostitutesQuest != null)
            {
                hasProstitutesAndLovers = true;
                string fileName = Path.Combine(GameManager.Instance.QuestListsManager.QuestPacksFolder, "Harbinger451", "ProstitutesAndLovers Quests", "QuestList-ProstitutesAndLovers.txt");
                ParseQuestList(fileName, ref questDataProstitutes);
            }

            LastNPCClicked = GameManager.Instance.QuestMachine.GetType().GetField("lastNPCClicked", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.Instance);
            if (LastNPCClicked != null)
                Debug.Log("P&L - FOUND LASTNPCCLICKED FIELD");
            Debug.Log("P&L - FOUND LASTNPCCLICKED FIELD");

            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();
        }

        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("Main"))
            {
                chanceBase = settings.GetValue<int>("Main", "BaseChance");
                bonusValue = settings.GetValue<int>("Main", "ChanceBonus");
                bonusMax = settings.GetValue<int>("Main", "MaximumChanceBonusStacks");
                preferredGender = settings.GetValue<int>("Main", "PreferredGender");
                EntryQuestors = settings.GetValue<bool>("Main", "EntryQuestors");
                ForeignQuestors = settings.GetValue<bool>("Main", "ForeignQuestors");
                StreetQuestors = settings.GetValue<bool>("Main", "StreetQuestors");
            }
        }

        public static void OnNewHour()
        {
            if (Instance.cooldown > 0)
            {
                Instance.cooldown--;
                return;
            }

            if (!GameManager.Instance.SaveLoadManager.LoadInProgress)
            {
                if (DaggerfallUI.Instance.UserInterfaceManager.TopWindow is DaggerfallRestWindow && GameManager.Instance.PlayerEntity.CurrentRestMode != DaggerfallRestWindow.RestModes.Loiter)
                    return;

                if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideTavern)
                {
                    if (Instance.StartCheck(!Instance.ForeignQuestors))
                    {
                        if (DaggerfallUI.Instance.UserInterfaceManager.TopWindow is DaggerfallRestWindow)
                            (DaggerfallUI.Instance.UserInterfaceManager.TopWindow as DaggerfallRestWindow).CloseWindow();
                    }
                }
                else if (Instance.StreetQuestors &&
                    !GameManager.Instance.PlayerEnterExit.IsPlayerInside
                    && GameManager.Instance.PlayerGPS.CurrentLocation.Loaded
                    && (!GameManager.Instance.PlayerGPS.CurrentLocation.HasDungeon
                    || (GameManager.Instance.PlayerGPS.CurrentLocation.HasDungeon && DaggerfallDungeon.IsMainStoryDungeon(GameManager.Instance.PlayerGPS.CurrentLocation.MapTableData.MapId))
                    ))
                {
                    if (Instance.StartCheck(false))
                    {
                        if (DaggerfallUI.Instance.UserInterfaceManager.TopWindow is DaggerfallRestWindow)
                            (DaggerfallUI.Instance.UserInterfaceManager.TopWindow as DaggerfallRestWindow).CloseWindow();
                    }
                }
            }
        }

        public static void OnTransition_Interior(PlayerEnterExit.TransitionEventArgs args)
        {
            //check if building is a Tavern
            //run code
            //if (args.DaggerfallInterior.BuildingData.BuildingType == DaggerfallConnect.DFLocation.BuildingTypes.Tavern && !GameManager.Instance.SaveLoadManager.LoadInProgress)

            //when the player enters any building (including shops and guild halls) and EntryQuestors is enabled
            if (!GameManager.Instance.SaveLoadManager.LoadInProgress && Instance.EntryQuestors)
            {
                Instance.StartCheck();
            }
        }

        bool IsPreferredGender(DaggerfallWorkshop.Game.Entity.Genders gender)
        {
            if (preferredGender == 1 && GameManager.Instance.PlayerEntity.Gender == gender)
                return true;

            if (preferredGender == 2 && GameManager.Instance.PlayerEntity.Gender != gender)
                return true;

            if (preferredGender == 3 && gender == DaggerfallWorkshop.Game.Entity.Genders.Male)
                return true;

            if (preferredGender == 4 && gender == DaggerfallWorkshop.Game.Entity.Genders.Female)
                return true;

            return false;
        }

        bool StartCheck(bool local = true)
        {
            LastNPCClicked.SetValue(GameManager.Instance.QuestMachine, null);

            //prioritize NPCs with pre-generated quest
            List<StaticNPC> questGivers = new List<StaticNPC>();
            foreach (StaticNPC staticNPC in ActiveGameObjectDatabase.GetActiveStaticNPCs())
            {
                LastNPCClicked.SetValue(GameManager.Instance.QuestMachine, staticNPC);
                //if not a child or existing quest target, add to list
                if (!staticNPC.IsChildNPC && staticNPC.gameObject.GetComponent<QuestResourceBehaviour>() == null && (TalkManager.Instance.IsNpcOfferingQuest(staticNPC.Data.nameSeed) || TalkManager.Instance.IsCastleNpcOfferingQuest(staticNPC.Data.nameSeed)))
                {
                    questGivers.Add(staticNPC);

                    if (hasProstitutesAndLovers && staticNPC.Data.factionID == 512)  //has P&L and NPC is a prostitute, add them three more times
                    {
                        questGivers.Add(staticNPC);
                        questGivers.Add(staticNPC);
                        questGivers.Add(staticNPC);
                    }
                    else if (IsPreferredGender(staticNPC.Data.gender))  //is a preferred gender, add them again
                    {
                        questGivers.Add(staticNPC);
                    }
                }
            }

            if (questGivers.Count < 1)
            {
                //no npcs with pregenerated quests
                //roll if quest can start
                int chanceFinal = chanceBase + (bonusValue * bonus);

                //add PER (+1% chance every 5 points of PER)
                //100 PER gives +20% chance
                chanceFinal += Mathf.RoundToInt(GameManager.Instance.PlayerEntity.Stats.GetLiveStatValue(DaggerfallConnect.DFCareer.Stats.Personality) / 5);

                //add LUK (+1% chance every 10 points of LUK)
                //100 LUK gives +10% chance
                chanceFinal += Mathf.RoundToInt(GameManager.Instance.PlayerEntity.Stats.GetLiveStatValue(DaggerfallConnect.DFCareer.Stats.Luck) / 10);

                //add regional legal reputation (+1% chance every 2 points of positive legal reputation)
                //100 legal rep adds +25% chance
                chanceFinal += Mathf.RoundToInt(GameManager.Instance.PlayerEntity.RegionData[GameManager.Instance.PlayerGPS.CurrentRegionIndex].LegalRep / 4);

                //halve chance during nighttime
                if (DaggerfallUnity.Instance.WorldTime.Now.IsNight)
                    chanceFinal = Mathf.RoundToInt(chanceFinal * 0.5f);

                Debug.Log("ADVENTURE FINDS YOU - FINAL CHANCE IS " + chanceFinal.ToString() + "%");

                if (Dice100.FailedRoll(chanceFinal))
                {
                    if (bonus < bonusMax)
                        bonus++;
                    return false;
                }
            }

            //if quest can start, assemble quest

            //pick a quest giver
            StaticNPC questGiver = null;
            Quest quest = null;

            if (local)
            {
                if (questGivers.Count < 1 && GameManager.Instance.PlayerEnterExit.IsPlayerInsideTavern)
                {
                    //no npcs with pre-generated quests
                    //get random static npc in same building if it is a tavern
                    foreach (StaticNPC staticNPC in ActiveGameObjectDatabase.GetActiveStaticNPCs())
                    {
                        //if not a child or existing quest target, add to list
                        if (!staticNPC.IsChildNPC && staticNPC.gameObject.GetComponent<QuestResourceBehaviour>() == null)
                        {
                            questGivers.Add(staticNPC);

                            if (hasProstitutesAndLovers && staticNPC.Data.factionID == 512)  //has P&L and NPC is a prostitute, add them three more times
                            {
                                questGivers.Add(staticNPC);
                                questGivers.Add(staticNPC);
                                questGivers.Add(staticNPC);
                            }
                            else if (IsPreferredGender(staticNPC.Data.gender))  //is a preferred gender, add them again
                            {
                                questGivers.Add(staticNPC);
                            }
                        }
                    }
                }

                if (questGivers.Count > 0)
                    questGiver = questGivers[UnityEngine.Random.Range(0, questGivers.Count)];

                if (questGiver == null)
                {
                    Debug.Log("ADVENTURE FINDS YOU - NO VALID LOCAL QUEST GIVERS FOUND");
                    return false;
                }

                //if NPC has a pre-generated quest, remove it
                if (TalkManager.Instance.IsNpcOfferingQuest(questGiver.Data.nameSeed) || TalkManager.Instance.IsCastleNpcOfferingQuest(questGiver.Data.nameSeed))
                    TalkManager.Instance.RemoveNpcQuestor(questGiver.Data.nameSeed);

                QuestMachine.Instance.SetLastNPCClicked(questGiver);

                //pick a quest pool depending on quest giver
                FactionFile.FactionData npcFactionData;
                GameManager.Instance.PlayerEntity.FactionData.GetFactionData(questGiver.Data.factionID, out npcFactionData);
                if (hasProstitutesAndLovers && questGiver.Data.factionID == 512)   //has P&L and  quest-giver is a prostitute, give only P&L prostitute quests
                    quest = GetProstituteQuest();
                else
                {
                    quest = GameManager.Instance.QuestListsManager.GetSocialQuest(
                        (FactionFile.SocialGroups)npcFactionData.sgroup,
                        questGiver.Data.factionID,
                        questGiver.Data.gender,
                        GameManager.Instance.PlayerEntity.FactionData.GetReputation(questGiver.Data.factionID),
                        GameManager.Instance.PlayerEntity.Level);
                }

                if (quest == null)
                {
                    Debug.Log("ADVENTURE FINDS YOU - QUEST IS NULL");
                    return false;
                }

                offeredQuest = quest;
                Debug.Log("ADVENTURE FINDS YOU - OFFERED QUEST IS " + offeredQuest.QuestName);

                //start coroutine
                StartCoroutine(HelperCoroutine((FactionFile.SocialGroups)npcFactionData.sgroup));
                return true;
            }
            else
            {
                //get random npc

                FactionFile.SocialGroups socialGroup = FactionFile.SocialGroups.Commoners;
                int factionID = GameManager.Instance.PlayerGPS.GetPeopleOfCurrentRegion();

                float value = UnityEngine.Random.value;
                if (value > 0.8f)
                {
                    socialGroup = FactionFile.SocialGroups.Nobility;
                    factionID = GameManager.Instance.PlayerGPS.GetCourtOfCurrentRegion();
                }
                else if(value > 0.6f)
                {
                    socialGroup = FactionFile.SocialGroups.Merchants;
                }

                quest = GameManager.Instance.QuestListsManager.GetSocialQuest(
                    socialGroup,
                    factionID,
                    Genders.Male,
                    GameManager.Instance.PlayerEntity.FactionData.GetReputation(factionID),
                    GameManager.Instance.PlayerEntity.Level);

                if (quest == null)
                {
                    Debug.Log("ADVENTURE FINDS YOU - QUEST IS NULL");
                    return false;
                }

                offeredQuest = quest;
                Debug.Log("ADVENTURE FINDS YOU - OFFERED QUEST IS " + offeredQuest.QuestName);

                //start coroutine
                StartCoroutine(HelperCoroutine(socialGroup, local));
                return true;
            }
        }

        IEnumerator HelperCoroutine(FactionFile.SocialGroups sGroup, bool local = true)
        {
            cooldown = cooldownTime;

            Quest quest = offeredQuest;

            yield return new WaitForSeconds(1);

            if (local)
            {
                StaticNPC questGiver = QuestMachine.Instance.LastNPCClicked;
                StaticNPC.NPCData questGiverData = questGiver.Data;

                //play message
                string race = questGiverData.race.ToString();
                string gender = questGiverData.gender == Genders.Female ? "woman" : "man";
                string quality = sGroup == FactionFile.SocialGroups.Nobility ? qualitiesNobles[UnityEngine.Random.Range(0, qualitiesNobles.Length)] : qualitiesCommoners[UnityEngine.Random.Range(0, qualitiesCommoners.Length)];   //maybe add a list of qualities to pick from?
                string manner = sGroup == FactionFile.SocialGroups.Nobility ? mannersNobles[UnityEngine.Random.Range(0, mannersNobles.Length)] : mannersCommoners[UnityEngine.Random.Range(0, mannersCommoners.Length)];   //maybe add a list of qualities to pick from?
                if (hasProstitutesAndLovers && questGiverData.factionID == 512)   //is a prostitute
                {
                    quality = qualitiesProstitute[UnityEngine.Random.Range(0, qualitiesProstitute.Length)];
                    manner = mannersProstitute[UnityEngine.Random.Range(0, mannersProstitute.Length)];
                }

                if (UnityEngine.Random.value > 0.5f)
                {
                    //called by quest giver
                    string[] strings = new string[]
                    {
                        questGiver.DisplayName + ", " + quality + " " + race + " " + gender + ", ",
                        manner + " gestures for you to approach..."
                    };
                    TextFile.Token[] texts = DaggerfallUnity.Instance.TextProvider.CreateTokens(TextFile.Formatting.JustifyCenter, strings);
                    DaggerfallMessageBox approach = CreateMessagePrompt(texts);
                    //DaggerfallMessageBox approach = CreateMessagePrompt(questGiver.DisplayName + ", " + quality + " " + race + " " + gender + ", " + manner + " gestures for you to approach...");

                    if (approach != null)
                    {
                        approach.OnButtonClick += Approach_OnButtonClick;
                        approach.Show();
                    }
                }
                else
                {
                    //approached by quest-giver
                    string[] strings = new string[]
                    {
                        questGiver.DisplayName + ", " + quality + " " + race + " " + gender + ", ",
                        "approaches you " + manner + "..."
                    };
                    TextFile.Token[] texts = DaggerfallUnity.Instance.TextProvider.CreateTokens(TextFile.Formatting.JustifyCenter, strings);

                    DaggerfallUI.MessageBox(texts);

                    yield return new WaitForSeconds(0.2f);

                    OfferQuest(quest);
                }
            }
            else
            {
                //somehow this always returns null
                /*Person questGiver = GetPerson(quest);
                string gender = questGiver.Gender == Genders.Female ? "woman" : "man";*/

                //play message
                string quality = sGroup == FactionFile.SocialGroups.Nobility ? qualitiesNobles[UnityEngine.Random.Range(0, qualitiesNobles.Length)] : qualitiesCommoners[UnityEngine.Random.Range(0, qualitiesCommoners.Length)];   //maybe add a list of qualities to pick from?
                quality = char.ToUpper(quality[0]) + quality.Substring(1);
                string manner = sGroup == FactionFile.SocialGroups.Nobility ? mannersNobles[UnityEngine.Random.Range(0, mannersNobles.Length)] : mannersCommoners[UnityEngine.Random.Range(0, mannersCommoners.Length)];   //maybe add a list of qualities to pick from?
                string entrance = GameManager.Instance.PlayerEnterExit.IsPlayerInside ? " figure steps in from outside and " : " figure steps out from a corner and ";
                if (UnityEngine.Random.value > 0.5f)
                {
                    //called by quest giver
                    string[] strings = new string[]
                    {
                        //quality + " " + gender + entrance,
                        quality + entrance,
                        manner + " gestures for you to approach..."
                    };
                    TextFile.Token[] texts = DaggerfallUnity.Instance.TextProvider.CreateTokens(TextFile.Formatting.JustifyCenter, strings);
                    DaggerfallMessageBox approach = CreateMessagePrompt(texts);
                    //DaggerfallMessageBox approach = CreateMessagePrompt(questGiver.DisplayName + ", " + quality + " " + race + " " + gender + ", " + manner + " gestures for you to approach...");

                    if (approach != null)
                    {
                        approach.OnButtonClick += Approach_OnButtonClick;
                        approach.Show();
                    }
                }
                else
                {
                    //approached by quest-giver
                    string[] strings = new string[]
                    {
                        //quality + " " + gender + entrance,
                        quality + entrance,
                        "approaches you " + manner + "..."
                    };
                    TextFile.Token[] texts = DaggerfallUnity.Instance.TextProvider.CreateTokens(TextFile.Formatting.JustifyCenter, strings);

                    DaggerfallUI.MessageBox(texts);

                    yield return new WaitForSeconds(0.2f);

                    OfferQuest(quest);
                }
            }

            //a quest was offered, reset bonus
            bonus = 0;
        }

        public Quest GetProstituteQuest()
        {
            if (questDataProstitutes.Count < 1)
            {
                Debug.Log("ADVENTURE FINDS YOU - PROSTITUTE QUEST DATA IS EMPTY");
                return null;
            }

            int factionID = 512;
            int rep = GameManager.Instance.PlayerEntity.FactionData.GetReputation(factionID);
            int level = GameManager.Instance.PlayerEntity.Level;
            Genders gender = GameManager.Instance.PlayerEntity.Gender;

            List<QuestData> pool = new List<QuestData>();
            foreach (QuestData questData in questDataProstitutes)
            {
                if (((questData.minReq < 10 && questData.minReq <= level) || rep >= questData.minReq) &&
                    (questData.membership == 'N' ||
                     (questData.membership == 'M' && gender == Genders.Male) ||
                     (questData.membership == 'F' && gender == Genders.Female)))
                {
                    if (!questData.adult || DaggerfallUnity.Settings.PlayerNudity)
                        pool.Add(questData);
                }
            }

            if (pool.Count > 0)
                return GameManager.Instance.QuestListsManager.SelectQuest(pool, 512);
            else
            {
                Debug.Log("ADVENTURE FINDS YOU - NO VALID PROSTITUTE QUESTS FOUND");
                return null;
            }
        }

        void OfferQuest(Quest quest)
        {
            //offer quest
            DaggerfallMessageBox messageBox = QuestMachine.Instance.CreateMessagePrompt(quest, (int)QuestMachine.QuestMessages.QuestorOffer);
            if (messageBox != null)
            {
                messageBox.OnButtonClick += OfferQuest_OnButtonClick;
                messageBox.Show();
            }
        }

        void OfferQuest_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {
            if (messageBoxButton == DaggerfallMessageBox.MessageBoxButtons.Yes)
            {
                // Show accept message, add quest
                sender.CloseWindow();
                ShowQuestPopupMessage(offeredQuest, (int)QuestMachine.QuestMessages.AcceptQuest);
                QuestMachine.Instance.StartQuest(offeredQuest);
            }
            else
            {
                // inform TalkManager so that it can remove the quest topics that have been added
                // (note by Nystul: I know it is a bit ugly that it is added in the first place at all, but didn't find a good way to do it differently -
                // may revisit this later)
                GameManager.Instance.TalkManager.RemoveQuestInfoTopicsForSpecificQuest(offeredQuest.UID);

                // remove quest rumors (rumor mill command) for this quest from talk manager
                GameManager.Instance.TalkManager.RemoveQuestRumorsFromRumorMill(offeredQuest.UID);

                // remove quest progress rumors for this quest from talk manager
                GameManager.Instance.TalkManager.RemoveQuestProgressRumorsFromRumorMill(offeredQuest.UID);

                // Show refuse message
                sender.CloseWindow();
                ShowQuestPopupMessage(offeredQuest, (int)QuestMachine.QuestMessages.RefuseQuest, false);
            }
        }

        // Show a popup such as accept/reject message close guild window
        void ShowQuestPopupMessage(Quest quest, int id, bool exitOnClose = true)
        {
            // Get message resource
            Message message = quest.GetMessage(id);
            if (message == null)
                return;

            // Setup popup message
            TextFile.Token[] tokens = message.GetTextTokens();
            DaggerfallMessageBox messageBox = new DaggerfallMessageBox(DaggerfallUI.UIManager);
            messageBox.SetTextTokens(tokens, this.offeredQuest.ExternalMCP);
            messageBox.ClickAnywhereToClose = true;
            messageBox.AllowCancel = true;
            messageBox.ParentPanel.BackgroundColor = Color.clear;

            // Exit menu on close if requested
            if (exitOnClose)
                messageBox.OnClose += QuestPopupMessage_OnClose;

            // Present popup message
            messageBox.Show();
        }

        void QuestPopupMessage_OnClose()
        {
            DaggerfallUI.UIManager.TopWindow.Value.CloseWindow();
        }

        /// <summary>
        /// Creates a yes/no prompt from quest message.
        /// Caller must set events and call Show() when ready.
        /// </summary>
        public DaggerfallMessageBox CreateMessagePrompt(TextFile.Token[] texts)
        {
            DaggerfallMessageBox messageBox = new DaggerfallMessageBox(DaggerfallUI.UIManager, DaggerfallMessageBox.CommonMessageBoxButtons.YesNo, texts);
            messageBox.ClickAnywhereToClose = false;
            messageBox.AllowCancel = false;
            messageBox.ParentPanel.BackgroundColor = Color.clear;

            return messageBox;
        }

        void Approach_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {
            if (messageBoxButton == DaggerfallMessageBox.MessageBoxButtons.Yes)
            {
                // Show accept message, add quest
                sender.CloseWindow();
                OfferQuest(offeredQuest);
            }
            else
            {
                // Show refuse message
                sender.CloseWindow();
                StaticNPC questGiver = QuestMachine.Instance.LastNPCClicked;
                if (questGiver != null)
                {
                    string pronoun = questGiver.Data.gender == Genders.Male ? "his" : "her";
                    DaggerfallUI.MessageBox(questGiver.DisplayName + " shrugs and turns " + pronoun + " attention away from you.");
                }
                else
                {
                    DaggerfallUI.MessageBox("You wave them away...");
                }
            }
        }

        private void ParseQuestList(string fileName, ref List<QuestData> questDataList)
        {
            string questsPath = fileName.Substring(0, fileName.LastIndexOf(Path.DirectorySeparatorChar));
            Debug.Log("ADVENTURE FINDS YOU - QUEST LIST FILENAME IS: " + fileName + " AND PATH IS: " + questsPath);

            // Seek from mods using pattern: QuestList-<packName>.txt
            Table questsTable = new Table(QuestMachine.Instance.GetTableSourceText(fileName));

            for (int i = 0; i < questsTable.RowCount; i++)
            {
                QuestData questData = new QuestData();
                questData.path = questsPath;
                string minRep = questsTable.GetValue("minReq", i);
                int d = 0;
                if (int.TryParse(minRep, out d))
                {
                    questData.name = questsTable.GetValue("name", i);
                    questData.group = questsTable.GetValue("group", i);
                    questData.membership = questsTable.GetValue("membership", i)[0];
                    questData.minReq = d;
                    char flag = questsTable.GetValue("flag", i)[0];
                    questData.oneTime = (flag == '1');
                    questData.adult = (flag == 'X');
                    if (Enum.IsDefined(typeof(FactionFile.SocialGroups), questData.group) && Array.Exists(questsProstitutes, x => x == questData.name))
                        questDataList.Add(questData);
                }
            }
        }

        Person GetPerson(Quest quest)
        {
            Symbol[] symbols = quest.GetQuestors();
            foreach (Symbol symbol in symbols)
            {
                Person person = quest.GetPerson(symbol);
                if (person == null)
                    continue;

                Debug.Log("ADVENTURE FINDS YOU - QUEST PERSON FOUND");
                return person;
            }

            Debug.Log("ADVENTURE FINDS YOU - QUEST PERSON NOT FOUND");
            return null;
        }

        IEnumerator PNLCheckCoroutine()
        {
            yield return new WaitForSeconds(1);

            string message = "P&L FOUND!";

            if (!hasProstitutesAndLovers)
                message = "P&L NOT FOUND!";

            DaggerfallUI.MessageBox(message);
        }
    }
}
