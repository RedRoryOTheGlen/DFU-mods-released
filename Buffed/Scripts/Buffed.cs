using System;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;

namespace BuffedMod
{
    public class Buffed : MonoBehaviour
    {
        private static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<Buffed>();

            mod.IsReady = true;
        }

        public static Buffed Instance;

        PlayerEntity player;

        bool replacement;
        int textureArchive = 1;

        List<int> itemTemplatesBlocked;

        private void Awake()
        {
            Instance = this;

            player = GameManager.Instance.PlayerEntity;

            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();

            PlayerActivate.OnLootSpawned += OnLootSpawned;
            LootTables.OnLootSpawned += OnDungeonLootSpawned;
            EnemyDeath.OnEnemyDeath += OnEnemyDeath;
            StartGameBehaviour.OnNewGame += OnNewGame;
            SaveLoadManager.OnLoad += OnLoad;
        }
        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("Main"))
            {
                replacement = settings.GetValue<bool>("Main", "Enable");
                textureArchive = settings.GetValue<int>("Main", "TextureArchive");
            }
            if (change.HasChanged("VanillaTextureReplacement"))
            {
                itemTemplatesBlocked = new List<int>();
                itemTemplatesBlocked.Add(107);          //immediately block helm

                if (!settings.GetValue<bool>("VanillaTextureReplacement", "Cuirass"))
                    itemTemplatesBlocked.Add(102);
                if (!settings.GetValue<bool>("VanillaTextureReplacement", "Brassiere"))
                    itemTemplatesBlocked.Add(182);
                if (!settings.GetValue<bool>("VanillaTextureReplacement", "FormalBrassiere"))
                    itemTemplatesBlocked.Add(183);
                if (!settings.GetValue<bool>("VanillaTextureReplacement", "PeasantBlouse"))
                    itemTemplatesBlocked.Add(184);
                if (!settings.GetValue<bool>("VanillaTextureReplacement", "Eodoric"))
                    itemTemplatesBlocked.Add(185);
                if (!settings.GetValue<bool>("VanillaTextureReplacement", "FormalEodoric"))
                    itemTemplatesBlocked.Add(194);
                if (!settings.GetValue<bool>("VanillaTextureReplacement", "EveningGown"))
                    itemTemplatesBlocked.Add(195);
                if (!settings.GetValue<bool>("VanillaTextureReplacement", "DayGown"))
                    itemTemplatesBlocked.Add(196);
                if (!settings.GetValue<bool>("VanillaTextureReplacement", "CasualDress"))
                    itemTemplatesBlocked.Add(197);
                if (!settings.GetValue<bool>("VanillaTextureReplacement", "StraplessDress"))
                    itemTemplatesBlocked.Add(198);
                if (!settings.GetValue<bool>("VanillaTextureReplacement", "ShortShirtOpened"))
                    itemTemplatesBlocked.Add(202);
                if (!settings.GetValue<bool>("VanillaTextureReplacement", "ShortShirtOpenedBelted"))
                    itemTemplatesBlocked.Add(203);
                if (!settings.GetValue<bool>("VanillaTextureReplacement", "LongShirtOpened"))
                    itemTemplatesBlocked.Add(204);
                if (!settings.GetValue<bool>("VanillaTextureReplacement", "LongShirtOpenedBelted"))
                    itemTemplatesBlocked.Add(205);
                if (!settings.GetValue<bool>("VanillaTextureReplacement", "ShortShirtClosed"))
                    itemTemplatesBlocked.Add(206);
                if (!settings.GetValue<bool>("VanillaTextureReplacement", "ShortShirtClosedBelted"))
                    itemTemplatesBlocked.Add(207);
                if (!settings.GetValue<bool>("VanillaTextureReplacement", "LongShirtClosed"))
                    itemTemplatesBlocked.Add(208);
                if (!settings.GetValue<bool>("VanillaTextureReplacement", "LongShirtClosedBelted"))
                    itemTemplatesBlocked.Add(209);
                if (!settings.GetValue<bool>("VanillaTextureReplacement", "OpenTunic"))
                    itemTemplatesBlocked.Add(210);
                if (!settings.GetValue<bool>("VanillaTextureReplacement", "ShortShirtFormal"))
                    itemTemplatesBlocked.Add(214);
                if (!settings.GetValue<bool>("VanillaTextureReplacement", "LongShirtFormal"))
                    itemTemplatesBlocked.Add(215);
                if (!settings.GetValue<bool>("VanillaTextureReplacement", "Vest"))
                    itemTemplatesBlocked.Add(216);
            }
            if (change.HasChanged("Main") ||
                (change.HasChanged("VanillaTextureReplacement") && textureArchive == 0 && replacement))
            {
                ReplaceAll();
            }
        }

        public static void OnNewGame()
        {
            if (Instance.IsPlayerBuffed() && Instance.replacement)
                Instance.ReplaceAll();
        }

        public static void OnLoad(SaveData_v1 saveData)
        {
            if (Instance.IsPlayerBuffed() && Instance.replacement)
                Instance.ReplaceAll();
        }

        public static void OnLootSpawned(object sender, ContainerLootSpawnedEventArgs e)
        {
            if (Instance.IsPlayerBuffed() && Instance.replacement)
                Instance.ReplaceCollection(e.Loot);
        }

        public static void OnDungeonLootSpawned(object sender, TabledLootSpawnedEventArgs e)
        {
            if (Instance.IsPlayerBuffed() && Instance.replacement)
                Instance.ReplaceCollection(e.Items);
        }

        public static void OnEnemyDeath(object sender, EventArgs e)
        {
            if (!Instance.IsPlayerBuffed() || !Instance.replacement)
                return;

            EnemyDeath enemyDeath = sender as EnemyDeath;

            if (enemyDeath != null)
            {
                DaggerfallEntityBehaviour entityBehaviour = enemyDeath.GetComponent<DaggerfallEntityBehaviour>();
                if (entityBehaviour != null)
                {
                    EnemyEntity enemyEntity = entityBehaviour.Entity as EnemyEntity;
                    if (enemyEntity != null)
                    {
                        if (entityBehaviour.CorpseLootContainer != null)
                        {
                            ItemCollection items = entityBehaviour.CorpseLootContainer.Items;
                            if (items != null)
                            {
                                Instance.ReplaceCollection(items);
                            }
                        }
                    }
                }
            }
        }
        private void Start()
        {

        }

        public bool IsPlayerBuffed()
        {
            if (player.Gender == Genders.Female &&
                (player.RaceTemplate.Name == "Orc" ||
                player.RaceTemplate.Name == "Nord"))
                return true;

            return false;
        }

        public void ReplaceAll()
        {
            if (!IsPlayerBuffed())
                return;

            ReplaceCollection(GameManager.Instance.PlayerEntity.Items, replacement);
            ReplaceCollection(GameManager.Instance.PlayerEntity.WagonItems, replacement);
            DaggerfallLoot[] loots = GameObject.FindObjectsOfType<DaggerfallLoot>();
            if (loots.Length > 0)
            {
                foreach (DaggerfallLoot loot in loots)
                {
                    if (loot.Items.Count > 0)
                        ReplaceCollection(loot.Items, replacement);
                }
            }
        }

        public void ReplaceCollection(ItemCollection collection, bool modded = true)
        {
            if (collection.Count < 1)
                return;

            int offset = (int)ItemBuilder.GetBodyMorphology(player.Race);

            for (int i = 0; i < collection.Count; i++)
            {
                DaggerfallUnityItem item = collection.GetItem(i);

                int templateIndex = item.TemplateIndex;

                if (modded)
                {
                    if (item.IsArtifact)
                    {
                        ArtifactsSubTypes artifactType = ItemHelper.GetArtifactSubType(item);
                        if (artifactType == ArtifactsSubTypes.Lords_Mail || artifactType == ArtifactsSubTypes.Ebony_Mail)
                        {
                            if (textureArchive == 1 && item.TemplateIndex == 102) //cuirass
                                item.PlayerTextureArchive = 112397;
                            else if (!itemTemplatesBlocked.Contains(item.TemplateIndex))
                                item.PlayerTextureArchive = 432;
                            else
                                item.PlayerTextureArchive = 433;
                        }
                        else
                            item.PlayerTextureArchive = 433;
                    }
                    else if (item.ItemGroup == ItemGroups.Armor)
                    {
                        if (item.TemplateIndex > 101 && item.TemplateIndex < 109)
                        {
                            if (textureArchive == 1 && item.TemplateIndex == 102) //cuirass
                                item.PlayerTextureArchive = 112396;
                            else if (!itemTemplatesBlocked.Contains(item.TemplateIndex))
                                item.PlayerTextureArchive = 250;
                            else
                                item.PlayerTextureArchive = 245 + offset;
                        }
                    }
                    else if (item.ItemGroup == ItemGroups.Weapons)
                    {
                        if (item.TemplateIndex > 112 && item.TemplateIndex < 131)
                            item.PlayerTextureArchive = 234;
                    }
                    else if (item.ItemGroup == ItemGroups.WomensClothing)
                    {
                        if (textureArchive == 1)
                        {
                            if (item.TemplateIndex > 181 && item.TemplateIndex < 217)
                               item.PlayerTextureArchive = 112395;
                        }
                        else
                        {
                            if (item.TemplateIndex > 181 && item.TemplateIndex < 217 && !itemTemplatesBlocked.Contains(item.TemplateIndex))
                                item.PlayerTextureArchive = 240;
                            else
                                item.PlayerTextureArchive = 235 + offset;
                        }
                    }
                }
                else
                {
                    if (item.IsArtifact)
                    {
                        item.PlayerTextureArchive = 433;
                    }
                    else if (item.ItemGroup == ItemGroups.Armor)
                    {
                        if (item.TemplateIndex > 101 && item.TemplateIndex < 109)
                            item.PlayerTextureArchive = 245 + offset;
                    }
                    else if (item.ItemGroup == ItemGroups.Weapons)
                    {
                        if (item.TemplateIndex > 112 && item.TemplateIndex < 131)
                            item.PlayerTextureArchive = 233;
                    }
                    else if (item.ItemGroup == ItemGroups.WomensClothing)
                    {
                        if (item.TemplateIndex > 181 && item.TemplateIndex < 217)
                            item.PlayerTextureArchive = 235 + offset;
                    }
                }
            }

        }
    }
}
