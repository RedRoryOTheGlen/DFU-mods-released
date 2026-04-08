using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.Serialization;
using Wenzil.Console;

namespace VanillaArmorReplacer
{
    public class VanillaArmorReplacer : MonoBehaviour
    {
        static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]

        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<VanillaArmorReplacer>();
        }

        public static VanillaArmorReplacer Instance;

        public bool initializeOnLoad;
        public bool feedbackMessage;

        //use to install or uninstall
        //0 = do nothing
        //1 = modded
        //2 = vanilla
        public int convertTo = 0;

        //determine modded armor appearance
        //0 = vanilla textures
        //1 = vanilla textures with leather and chain
        //2 = custom texture archive
        public int textureArchive = 0;

        //armor type settings
        public int armorAssignment = 0; //0 = material, 1 = universal
        public int armorIron = 0;
        public int armorSteel = 0;
        public int armorSilver = 0;
        public int armorElven = 0;
        public int armorDwarven = 0;
        public int armorMithril = 0;
        public int armorAdamantium = 0;
        public int armorEbony = 0;
        public int armorOrcish = 0;
        public int armorDaedric = 0;

        //armor name settings
        public string nameCuirassLeather;
        public string nameGauntletsLeather;
        public string nameGreavesLeather;
        public string namePauldronLeather;
        public string nameHelmLeather;
        public string nameBootsLeather;
        public string nameCuirassChain;
        public string nameGauntletsChain;
        public string nameGreavesChain;
        public string namePauldronChain;
        public string nameHelmChain;
        public string nameBootsChain;
        public string nameCuirassPlate;
        public string nameGauntletsPlate;
        public string nameGreavesPlate;
        public string namePauldronPlate;
        public string nameHelmPlate;
        public string nameBootsPlate;

        //controls
        public bool useCycleVariant;

        //experimental
        public bool UniversalArmorTypes;

        IEnumerator conflict;

        //custom texture archive stuff

        CifRciFile cifFile;

        public bool buffed;

        //custom dye stuff
        byte[] dyeLeather = new byte[] { 0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E, 0x4F };
        byte[] dyeChain = new byte[] { 0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F };
        byte[] dyeIron = new byte[] { 0x77, 0x78, 0x57, 0x79, 0x58, 0x59, 0x7A, 0x5A, 0x7B, 0x5B, 0x7C, 0x5C, 0x7D, 0x5D, 0x5E, 0x5F };
        byte[] dyeSteel = new byte[] { 0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x7B, 0x7C, 0x7D, 0x7E, 0x7F };
        byte[] dyeSilver = new byte[] { 0xE0, 0x70, 0x50, 0x71, 0x51, 0x72, 0x73, 0x52, 0x74, 0x53, 0x75, 0x54, 0x55, 0x56, 0x57, 0x58 };
        byte[] dyeElven = new byte[] { 0xE0, 0x70, 0x50, 0x71, 0x51, 0x72, 0x73, 0x52, 0x74, 0x53, 0x75, 0x54, 0x55, 0x56, 0x57, 0x58 };
        byte[] dyeDwarven = new byte[] { 0x90, 0x91, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0x9B, 0x9C, 0x9D, 0x9E, 0x9F };
        byte[] dyeMithril = new byte[] { 0x67, 0x68, 0x69, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x6F, 0xD8, 0xD9, 0xDA, 0xDB, 0xDC, 0xDD, 0xDE };
        byte[] dyeAdamantium = new byte[] { 0x5A, 0x5B, 0x7C, 0x5C, 0x7D, 0x5D, 0x7E, 0x5E, 0x7F, 0xD8, 0xD9, 0xDA, 0xDB, 0xDC, 0xDD, 0xDE };
        byte[] dyeEbony = new byte[] { 0x77, 0x78, 0x79, 0x7A, 0x7B, 0x7C, 0x7D, 0x7E, 0x7F, 0xD8, 0xD9, 0xDA, 0xDB, 0xDC, 0xDD, 0xDE };
        byte[] dyeOrcish = new byte[] { 0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xCB, 0xCC, 0xCD, 0xCE, 0xCF, 0xD8, 0xD9, 0xDA, 0xDB, 0xDC, 0xDD };
        byte[] dyeDaedric = new byte[] { 0xEF, 0xF0, 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8, 0xF9, 0xFA, 0xFB, 0xFC, 0xFD, 0xFE };

        public int GetNativeMaterialValueBitwise(int nativeMaterialValue, int spoofMaterialValue)
        {
            if (nativeMaterialValue == (int)ArmorMaterialTypes.Leather || nativeMaterialValue == (int)ArmorMaterialTypes.Chain)
                return nativeMaterialValue;

            if (spoofMaterialValue == (int)ArmorMaterialTypes.Chain)
                return nativeMaterialValue - 0x0100;
            else if (spoofMaterialValue == (int)ArmorMaterialTypes.Leather)
                return nativeMaterialValue - 0x0200;
            else
                return spoofMaterialValue;
        }

        public int GetLeatherMaterialValue(int nativeMaterialValue)
        {
            if (nativeMaterialValue == (int)ArmorMaterialTypes.Leather)
                return nativeMaterialValue;

            return nativeMaterialValue - 0x0200;
        }

        public int GetChainMaterialValue(int nativeMaterialValue)
        {
            if (nativeMaterialValue == (int)ArmorMaterialTypes.Chain)
                return nativeMaterialValue;

            return nativeMaterialValue - 0x0100;
        }

        void Awake()
        {
            ModSettings settings = mod.GetSettings();

            Instance = this;

            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();

            InitMod();

            mod.IsReady = true;
        }

        private void Start()
        {
            Mod buffedMod = ModManager.Instance.GetModFromGUID("2d70df14-c379-420e-8ded-07761ae04545");
            if (buffedMod != null)
                buffed = true;

            ConsoleCommandsDatabase.RegisterCommand(FixMyArmor.name, FixMyArmor.description, FixMyArmor.usage, FixMyArmor.Execute);
        }

        private static void InitMod()
        {
            Instance.InitCustomTextureVariants();

            DaggerfallUnity.Instance.ItemHelper.RegisterCustomItem(ItemCuirass.templateIndex, ItemGroups.Armor, typeof(ItemCuirass));
            DaggerfallUnity.Instance.ItemHelper.RegisterCustomItem(ItemGauntlets.templateIndex, ItemGroups.Armor, typeof(ItemGauntlets));
            DaggerfallUnity.Instance.ItemHelper.RegisterCustomItem(ItemGreaves.templateIndex, ItemGroups.Armor, typeof(ItemGreaves));
            DaggerfallUnity.Instance.ItemHelper.RegisterCustomItem(ItemPauldronLeft.templateIndex, ItemGroups.Armor, typeof(ItemPauldronLeft));
            DaggerfallUnity.Instance.ItemHelper.RegisterCustomItem(ItemPauldronRight.templateIndex, ItemGroups.Armor, typeof(ItemPauldronRight));
            DaggerfallUnity.Instance.ItemHelper.RegisterCustomItem(ItemHelm.templateIndex, ItemGroups.Armor, typeof(ItemHelm));
            DaggerfallUnity.Instance.ItemHelper.RegisterCustomItem(ItemBoots.templateIndex, ItemGroups.Armor, typeof(ItemBoots));

            PlayerActivate.OnLootSpawned += OnLootSpawned;
            LootTables.OnLootSpawned += OnDungeonLootSpawned;
            EnemyDeath.OnEnemyDeath += OnEnemyDeath;

            StartGameBehaviour.OnNewGame += OnNewGame;
            SaveLoadManager.OnLoad += OnLoad;
        }

        //variant controls
        public int[] variantsLeather = new int[7];
        public int[] variantsChain = new int[7];
        public int[] variantsPlate = new int[7];

        void InitCustomTextureVariants()
        {
            Texture2D tex;
            for (int x = 0; x < 7; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    if (!TextureReplacement.TryImportTexture(112350, x + (y * 1000), 0, out tex))
                    {
                        variantsLeather[x] = y;
                        break;
                    }
                }
            }

            for (int x = 0; x < 7; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    if (!TextureReplacement.TryImportTexture(112350, 100 + x + (y * 1000), 0, out tex))
                    {
                        variantsChain[x] = y;
                        break;
                    }
                }
            }

            for (int x = 0; x < 7; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    if (!TextureReplacement.TryImportTexture(112350, 200 + x + (y * 1000), 0, out tex))
                    {
                        variantsPlate[x] = y;
                        break;
                    }
                }
            }
        }

        public static void OnNewGame()
        {
            Instance.ConvertAllArmor();
        }

        public static void OnLoad(SaveData_v1 saveData)
        {
            if (Instance.initializeOnLoad)
                Instance.ConvertAllArmor();
        }

        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("Initialization"))
            {
                initializeOnLoad = settings.GetValue<bool>("Initialization", "InitializeOnLoad");
                feedbackMessage = settings.GetValue<bool>("Initialization", "ShowMessages");
            }

            if (change.HasChanged("Replacement"))
            {
                convertTo = settings.GetValue<int>("Replacement", "ConvertInventory");
            }

            if (change.HasChanged("Controls"))
            {
                useCycleVariant = settings.GetValue<bool>("Controls", "CycleVariantOnUse");
            }

            if (change.HasChanged("Textures"))
            {
                textureArchive = settings.GetValue<int>("Textures", "TextureArchive");
            }

            if (change.HasChanged("Types"))
            {
                armorIron = settings.GetValue<int>("Types", "Iron");
                armorSteel = settings.GetValue<int>("Types", "Steel");
                armorSilver = settings.GetValue<int>("Types", "Silver");
                armorElven = settings.GetValue<int>("Types", "Elven");
                armorDwarven = settings.GetValue<int>("Types", "Dwarven");
                armorMithril = settings.GetValue<int>("Types", "Mithril");
                armorAdamantium = settings.GetValue<int>("Types", "Adamantium");
                armorEbony = settings.GetValue<int>("Types", "Ebony");
                armorOrcish = settings.GetValue<int>("Types", "Orcish");
                armorDaedric = settings.GetValue<int>("Types", "Daedric");
            }

            if (change.HasChanged("Names"))
            {
                nameCuirassLeather = settings.GetString("Names", "LeatherCuirass");
                nameGauntletsLeather = settings.GetString("Names", "LeatherGauntlets");
                nameGreavesLeather = settings.GetString("Names", "LeatherGreaves");
                namePauldronLeather = settings.GetString("Names", "LeatherPauldron");
                nameHelmLeather = settings.GetString("Names", "LeatherHelm");
                nameBootsLeather = settings.GetString("Names", "LeatherBoots");
                nameCuirassChain = settings.GetString("Names", "ChainCuirass");
                nameGauntletsChain = settings.GetString("Names", "ChainGauntlets");
                nameGreavesChain = settings.GetString("Names", "ChainGreaves");
                namePauldronChain = settings.GetString("Names", "ChainPauldron");
                nameHelmChain = settings.GetString("Names", "ChainHelm");
                nameBootsChain = settings.GetString("Names", "ChainBoots");
                nameCuirassPlate = settings.GetString("Names", "PlateCuirass");
                nameGauntletsPlate = settings.GetString("Names", "PlateGauntlets");
                nameGreavesPlate = settings.GetString("Names", "PlateGreaves");
                namePauldronPlate = settings.GetString("Names", "PlatePauldron");
                nameHelmPlate = settings.GetString("Names", "PlateHelm");
                nameBootsPlate = settings.GetString("Names", "PlateBoots");
            }

            bool UAT = settings.GetValue<bool>("Experimental", "UniversalArmorTypes");
            if (textureArchive == 3)
                UniversalArmorTypes = UAT;
            else if (UAT)
            {
                UniversalArmorTypes = false;
                SettingConflict();
            }

            if (change.HasChanged("Replacement"))
                ConvertAllArmor();
            else if (change.HasChanged("Textures") || change.HasChanged("Types") || change.HasChanged("Names") || change.HasChanged("Experimental"))
                UpdateAllArmor();




        }

        public static void OnLootSpawned(object sender, ContainerLootSpawnedEventArgs e)
        {
            if (Instance.convertTo == 1)
                Instance.ReplaceVanillaArmor(e.Loot);
        }

        public static void OnDungeonLootSpawned(object sender, TabledLootSpawnedEventArgs e)
        {
            if (Instance.convertTo == 1)
                Instance.ReplaceVanillaArmor(e.Items);
        }

        public static void OnEnemyDeath(object sender, EventArgs e)
        {
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
                                Instance.ReplaceVanillaArmor(items);
                            }
                        }
                    }
                }
            }
        }

        public void ConvertAllArmor()
        {
            if (convertTo == 1)
            {
                if (feedbackMessage)
                    DaggerfallUI.Instance.PopupMessage("Replacing vanilla armor");

                //change textures of existing modded armors
                UpdateCustomArmor(GameManager.Instance.PlayerEntity.Items, true);
                //replace existing vanilla armor with modded
                ReplaceVanillaArmor(GameManager.Instance.PlayerEntity.Items, true);
                ReplaceVanillaArmor(GameManager.Instance.PlayerEntity.WagonItems);
                DaggerfallLoot[] loots = GameObject.FindObjectsOfType<DaggerfallLoot>();
                if (loots.Length > 0)
                {
                    foreach (DaggerfallLoot loot in loots)
                    {
                        if (loot.Items.Count > 0)
                        {
                            //change textures of existing modded armors
                            UpdateCustomArmor(loot.Items);
                            ReplaceVanillaArmor(loot.Items);
                        }
                    }
                }
            }
            else if (convertTo == 0)
            {
                if (feedbackMessage)
                    DaggerfallUI.Instance.PopupMessage("Restoring vanilla armor");

                //replace existing modded armor with vanilla
                ReplaceCustomArmor(GameManager.Instance.PlayerEntity.Items, true);
                ReplaceCustomArmor(GameManager.Instance.PlayerEntity.WagonItems);
                DaggerfallLoot[] loots = GameObject.FindObjectsOfType<DaggerfallLoot>();
                if (loots.Length > 0)
                {
                    foreach (DaggerfallLoot loot in loots)
                    {
                        if (loot.Items.Count > 0)
                            ReplaceCustomArmor(loot.Items);
                    }
                }
            }
        }
        public void UpdateAllArmor()
        {
            if (feedbackMessage)
                DaggerfallUI.Instance.PopupMessage("Updating armor textures");

            //change textures of existing modded armors
            UpdateCustomArmor(GameManager.Instance.PlayerEntity.Items, true);
            UpdateCustomArmor(GameManager.Instance.PlayerEntity.WagonItems);
            DaggerfallLoot[] loots = GameObject.FindObjectsOfType<DaggerfallLoot>();
            if (loots.Length > 0)
            {
                foreach (DaggerfallLoot loot in loots)
                {
                    if (loot.Items.Count > 0)
                        UpdateCustomArmor(loot.Items);
                }
            }
        }

        void ReplaceVanillaArmor(ItemCollection collection, bool inventory = false)
        {
            if (collection.Count < 1)
                return;

            List<DaggerfallUnityItem> itemsToRemove = new List<DaggerfallUnityItem>();

            PlayerEntity player = GameManager.Instance.PlayerEntity;

            for (int i = 0; i < collection.Count; i++)
            {
                DaggerfallUnityItem item = collection.GetItem(i);

                if (item.IsArtifact)
                    continue;

                int templateIndex = item.TemplateIndex;

                //check if vanilla armor
                if (templateIndex >= 102 && templateIndex <= 108)
                {
                    itemsToRemove.Add(item);

                    DaggerfallUnityItem newItem = null;

                    bool equipped = item.IsEquipped;

                    if (equipped)
                    {
                        player.ItemEquipTable.UnequipItem(item);
                        player.UpdateEquippedArmorValues(item, false);
                    }

                    switch (templateIndex)
                    {
                        case 102:
                            newItem = ItemBuilder.CreateItem(ItemGroups.Armor, 1300);
                            break;  //cuirass
                        case 103:
                            newItem = ItemBuilder.CreateItem(ItemGroups.Armor, 1301);
                            break;  //gauntlets
                        case 104:
                            newItem = ItemBuilder.CreateItem(ItemGroups.Armor, 1302);
                            break;  //greaves
                        case 105:
                            newItem = ItemBuilder.CreateItem(ItemGroups.Armor, 1303);
                            break;  //left pauldron
                        case 106:
                            newItem = ItemBuilder.CreateItem(ItemGroups.Armor, 1304);
                            break;  //right pauldron
                        case 107:
                            newItem = ItemBuilder.CreateItem(ItemGroups.Armor, 1305);
                            break;  //helm
                        case 108:
                            newItem = ItemBuilder.CreateItem(ItemGroups.Armor, 1306);
                            break;  //boots
                    }

                    if (newItem != null)
                    {
                        //save the variant
                        if (textureArchive != 3)
                            newItem.message = SetVariantToMessage(newItem.message, item.CurrentVariant);
                        else
                            newItem.message = -1;

                        /*//save the variant
                        if (UniversalArmorTypes)
                            newItem.message = SetTypeToMessage(newItem.message, UnityEngine.Random.Range(0,3));*/

                        //Debug.Log(item.ItemName + " variant is " + item.CurrentVariant.ToString());

                        ItemBuilder.ApplyArmorSettings(newItem, player.Gender, player.Race, (ArmorMaterialTypes)item.nativeMaterialValue,item.CurrentVariant);
                        newItem.currentCondition = item.currentCondition;

                        //update weight
                        if (UniversalArmorTypes)
                            newItem.weightInKg = GetWeightOfTypedArmor(newItem);

                        if (item.HasLegacyEnchantments || item.HasCustomEnchantments)
                        {
                            if (item.HasLegacyEnchantments)
                                newItem.legacyMagic = item.legacyMagic;

                            if (item.HasCustomEnchantments)
                                newItem.customMagic = item.customMagic;

                            newItem.shortName = item.shortName;
                        }

                        collection.AddItem(newItem);

                        if (item.IsIdentified)
                            newItem.IdentifyItem();

                        if (equipped && inventory)
                        {
                            //player.ItemEquipTable.UnequipItem(item);
                            player.ItemEquipTable.EquipItem(newItem,true, false);
                            player.UpdateEquippedArmorValues(newItem, true);
                        }
                    }
                }
            }
            if (itemsToRemove.Count > 0)
            {
                foreach (DaggerfallUnityItem item in itemsToRemove)
                    collection.RemoveItem(item);
            }
        }

        //Do this before uninstalling
        void ReplaceCustomArmor(ItemCollection collection, bool inventory = false)
        {
            if (collection.Count < 1)
                return;

            List<DaggerfallUnityItem> itemsToRemove = new List<DaggerfallUnityItem>();

            PlayerEntity player = GameManager.Instance.PlayerEntity;
            int offset = 235 + (int)ItemBuilder.GetBodyMorphology(player.Race);

            for (int i = 0; i < collection.Count; i++)
            {
                DaggerfallUnityItem item = collection.GetItem(i);

                if (item.IsArtifact)
                    continue;

                int templateIndex = item.TemplateIndex;

                //check if custom armor
                if (templateIndex >= 1300 && templateIndex <= 1306)
                {
                    itemsToRemove.Add(item);

                    DaggerfallUnityItem newItem = null;

                    bool equipped = item.IsEquipped;

                    if (equipped)
                    {
                        player.ItemEquipTable.UnequipItem(item);
                        player.UpdateEquippedArmorValues(item, false);
                    }

                    switch (templateIndex)
                    {
                        case 1300:
                            //newItem = ItemBuilder.CreateArmor(player.Gender, player.Race, Armor.Cuirass, (ArmorMaterialTypes)item.nativeMaterialValue);
                            newItem = ItemBuilder.CreateItem(ItemGroups.Armor, 102);
                            break;  //cuirass
                        case 1301:
                            //newItem = ItemBuilder.CreateArmor(player.Gender, player.Race, Armor.Gauntlets, (ArmorMaterialTypes)item.nativeMaterialValue);
                            newItem = ItemBuilder.CreateItem(ItemGroups.Armor, 103);
                            break;  //gauntlets
                        case 1302:
                            //newItem = ItemBuilder.CreateArmor(player.Gender, player.Race, Armor.Greaves, (ArmorMaterialTypes)item.nativeMaterialValue);
                            newItem = ItemBuilder.CreateItem(ItemGroups.Armor, 104);
                            break;  //greaves
                        case 1303:
                            //newItem = ItemBuilder.CreateArmor(player.Gender, player.Race, Armor.Left_Pauldron, (ArmorMaterialTypes)item.nativeMaterialValue);
                            newItem = ItemBuilder.CreateItem(ItemGroups.Armor, 105);
                            break;  //left pauldron
                        case 1304:
                            //newItem = ItemBuilder.CreateArmor(player.Gender, player.Race, Armor.Right_Pauldron, (ArmorMaterialTypes)item.nativeMaterialValue);
                            newItem = ItemBuilder.CreateItem(ItemGroups.Armor, 106);
                            break;  //right pauldron
                        case 1305:
                            //newItem = ItemBuilder.CreateArmor(player.Gender, player.Race, Armor.Helm, (ArmorMaterialTypes)item.nativeMaterialValue);
                            newItem = ItemBuilder.CreateItem(ItemGroups.Armor, 107);
                            break;  //helm
                        case 1306:
                            //newItem = ItemBuilder.CreateArmor(player.Gender, player.Race, Armor.Boots, (ArmorMaterialTypes)item.nativeMaterialValue);
                            newItem = ItemBuilder.CreateItem(ItemGroups.Armor, 108);
                            break;  //boots
                    }

                    if (newItem != null)
                    {
                        //set the variant
                        int variant = GetVariantFromMessage(item.message);

                        //Debug.Log(newItem.ItemName + " variant is " + variant.ToString());

                        ItemBuilder.ApplyArmorSettings(newItem, player.Gender, player.Race, (ArmorMaterialTypes)item.nativeMaterialValue, variant);

                        //newItem.LowerCondition(item.maxCondition - item.currentCondition);
                        //newItem.currentCondition = newItem.maxCondition * (item.currentCondition / item.maxCondition);
                        newItem.currentCondition = item.currentCondition;

                        if (item.HasLegacyEnchantments || item.HasCustomEnchantments)
                        {
                            if (item.HasLegacyEnchantments)
                                newItem.legacyMagic = item.legacyMagic;

                            if (item.HasCustomEnchantments)
                                newItem.customMagic = item.customMagic;

                            newItem.shortName = item.shortName;
                        }

                        collection.AddItem(newItem);

                        if (item.IsIdentified)
                            newItem.IdentifyItem();

                        if (equipped && inventory)
                        {
                            player.ItemEquipTable.EquipItem(newItem, true, false);
                            player.UpdateEquippedArmorValues(newItem, true);
                        }

                        newItem.message = -1;
                    }
                }
            }

            if (itemsToRemove.Count > 0)
            {
                foreach (DaggerfallUnityItem item in itemsToRemove)
                    collection.RemoveItem(item);
            }
        }
        void UpdateCustomArmor(ItemCollection collection, bool inventory = false)
        {
            if (collection.Count < 1)
                return;

            List<DaggerfallUnityItem> itemsToRemove = new List<DaggerfallUnityItem>();

            PlayerEntity player = GameManager.Instance.PlayerEntity;
            int offset = 235 + (int)ItemBuilder.GetBodyMorphology(player.Race);

            for (int i = 0; i < collection.Count; i++)
            {
                DaggerfallUnityItem item = collection.GetItem(i);

                int templateIndex = item.TemplateIndex;

                //check if custom armor
                if (templateIndex >= 1300 && templateIndex <= 1306)
                {
                    //set the variant
                    int variant = -1;
                    int type = -1;

                    variant = GetVariantFromMessage(item.message);
                    type = GetTypeFromMessage(item.message);

                    //item.SetItem(ItemGroups.None,templateIndex);
                    //ItemBuilder.ApplyArmorSettings(item, player.Gender, player.Race, (ArmorMaterialTypes)item.nativeMaterialValue, variant);

                    item.CurrentVariant = variant;

                    //Debug.Log("VANILLA ARMOR REPLACER - " + ((ArmorMaterialTypes)item.nativeMaterialValue).ToString() + " " + item.ItemName + " VARIANT IS " + variant.ToString() + " AND TYPE IS " + type.ToString());
                }
                else if (IsCustomArmor(item))
                {
                    //is outdated custom armor
                    //regenerate it
                    itemsToRemove.Add(item);

                    DaggerfallUnityItem newItem = null;

                    bool equipped = item.IsEquipped;

                    if (equipped)
                    {
                        player.ItemEquipTable.UnequipItem(item);
                        player.UpdateEquippedArmorValues(item, false);
                    }

                    if (item is ItemCuirass)
                        newItem = ItemBuilder.CreateItem(ItemGroups.Armor, 1300);
                    else if (item is ItemGauntlets)
                        newItem = ItemBuilder.CreateItem(ItemGroups.Armor, 1301);
                    else if (item is ItemGreaves)
                        newItem = ItemBuilder.CreateItem(ItemGroups.Armor, 1302);
                    else if (item is ItemPauldronLeft)
                        newItem = ItemBuilder.CreateItem(ItemGroups.Armor, 1303);
                    else if (item is ItemPauldronRight)
                        newItem = ItemBuilder.CreateItem(ItemGroups.Armor, 1304);
                    else if (item is ItemHelm)
                        newItem = ItemBuilder.CreateItem(ItemGroups.Armor, 1305);
                    else if (item is ItemBoots)
                        newItem = ItemBuilder.CreateItem(ItemGroups.Armor, 1306);

                    if (newItem != null)
                    {
                        //set the variant
                        int variant = GetVariantFromMessage(item.message);

                        //Debug.Log(newItem.ItemName + " variant is " + variant.ToString());

                        ItemBuilder.ApplyArmorSettings(newItem, player.Gender, player.Race, (ArmorMaterialTypes)item.nativeMaterialValue, variant);

                        newItem.currentCondition = item.currentCondition;

                        if (item.HasLegacyEnchantments || item.HasCustomEnchantments)
                        {
                            if (item.HasLegacyEnchantments)
                                newItem.legacyMagic = item.legacyMagic;

                            if (item.HasCustomEnchantments)
                                newItem.customMagic = item.customMagic;

                            newItem.shortName = item.shortName;
                        }

                        collection.AddItem(newItem);

                        if (item.IsIdentified)
                            newItem.IdentifyItem();

                        if (equipped && inventory)
                        {
                            player.ItemEquipTable.EquipItem(newItem, true, false);
                            player.UpdateEquippedArmorValues(newItem, true);
                        }
                    }
                }
            }

            if (itemsToRemove.Count > 0)
            {
                foreach (DaggerfallUnityItem item in itemsToRemove)
                    collection.RemoveItem(item);
            }
        }

        public bool IsPlayerBuffed()
        {
            if (buffed && GameManager.Instance.PlayerEntity.Gender == Genders.Female &&
                (GameManager.Instance.PlayerEntity.RaceTemplate.Name == "Orc" ||
                GameManager.Instance.PlayerEntity.RaceTemplate.Name == "Nord"))
                return true;

            return false;
        }

        bool IsCustomArmor(DaggerfallUnityItem armor)
        {
            if (armor is ItemCuirass ||
                armor is ItemGauntlets ||
                armor is ItemGreaves ||
                armor is ItemPauldronLeft ||    
                armor is ItemPauldronRight ||
                armor is ItemHelm ||
                armor is ItemBoots
                )
                return true;

            return false;
        }

        public float GetWeightOfTypedArmor(DaggerfallUnityItem item)
        {
            float typeMod = 1;
            float materialMod = 1;

            int type = GetTypeFromMessage(item.message);
            switch (type)
            {
                case 0:
                    typeMod = 0.5f;
                    break;
                case 1:
                    typeMod = 0.75f;
                    break;
                case 2:
                    typeMod = 1f;
                    break;
            }

            int material = item.nativeMaterialValue;
            switch (material)
            {
                case (int)ArmorMaterialTypes.Leather:
                    materialMod = 4;
                    break;
                case (int)ArmorMaterialTypes.Chain:
                case (int)ArmorMaterialTypes.Chain2:
                    materialMod = 4;
                    break;
                case (int)ArmorMaterialTypes.Iron:
                    materialMod = 4;
                    break;
                case (int)ArmorMaterialTypes.Steel:
                    materialMod = 5;
                    break;
                case (int)ArmorMaterialTypes.Silver:
                    materialMod = 4;
                    break;
                case (int)ArmorMaterialTypes.Elven:
                    materialMod = 4;
                    break;
                case (int)ArmorMaterialTypes.Dwarven:
                    materialMod = 3;
                    break;
                case (int)ArmorMaterialTypes.Mithril:
                    materialMod = 4;
                    break;
                case (int)ArmorMaterialTypes.Adamantium:
                    materialMod = 4;
                    break;
                case (int)ArmorMaterialTypes.Ebony:
                    materialMod = 2;
                    break;
                case (int)ArmorMaterialTypes.Orcish:
                    materialMod = 4;
                    break;
                case (int)ArmorMaterialTypes.Daedric:
                    materialMod = 5;
                    break;
            }

            return 0.25f * Mathf.Round((((item.ItemTemplate.baseWeight * typeMod) * materialMod)/4)/0.25f);
        }

        public float GetValueOfTypedArmor(DaggerfallUnityItem item)
        {
            float typeMod = 1;
            float materialMod = 1;

            int type = GetTypeFromMessage(item.message);
            switch (type)
            {
                case 0:
                    typeMod = 0.5f;
                    break;
                case 1:
                    typeMod = 0.75f;
                    break;
                case 2:
                    typeMod = 1f;
                    break;
            }

            int material = item.nativeMaterialValue;
            switch (material)
            {
                case (int)ArmorMaterialTypes.Leather:
                    materialMod = 4;
                    break;
                case (int)ArmorMaterialTypes.Chain:
                case (int)ArmorMaterialTypes.Chain2:
                    materialMod = 4;
                    break;
                case (int)ArmorMaterialTypes.Iron:
                    materialMod = 4;
                    break;
                case (int)ArmorMaterialTypes.Steel:
                    materialMod = 5;
                    break;
                case (int)ArmorMaterialTypes.Silver:
                    materialMod = 4;
                    break;
                case (int)ArmorMaterialTypes.Elven:
                    materialMod = 4;
                    break;
                case (int)ArmorMaterialTypes.Dwarven:
                    materialMod = 3;
                    break;
                case (int)ArmorMaterialTypes.Mithril:
                    materialMod = 4;
                    break;
                case (int)ArmorMaterialTypes.Adamantium:
                    materialMod = 4;
                    break;
                case (int)ArmorMaterialTypes.Ebony:
                    materialMod = 2;
                    break;
                case (int)ArmorMaterialTypes.Orcish:
                    materialMod = 4;
                    break;
                case (int)ArmorMaterialTypes.Daedric:
                    materialMod = 5;
                    break;
            }

            return 0.25f * Mathf.Round((((item.ItemTemplate.baseWeight * typeMod) * materialMod) / 4) / 0.25f);
        }

        public int GetChainArmorValue(int material)
        {
            int value = 0;
            switch (material)
            {
                case (int)ArmorMaterialTypes.Steel:
                case (int)ArmorMaterialTypes.Silver:
                    value = 8;
                    break;
                case (int)ArmorMaterialTypes.Elven:
                    value = 10;
                    break;
                case (int)ArmorMaterialTypes.Dwarven:
                    value = 12;
                    break;
                case (int)ArmorMaterialTypes.Mithril:
                case (int)ArmorMaterialTypes.Adamantium:
                    value = 14;
                    break;
                case (int)ArmorMaterialTypes.Ebony:
                    value = 16;
                    break;
                case (int)ArmorMaterialTypes.Orcish:
                    value = 18;
                    break;
                case (int)ArmorMaterialTypes.Daedric:
                    value = 20;
                    break;
            }
            return value;
        }

        public int GetLeatherArmorValue(int material)
        {
            int value = 0;
            switch (material)
            {
                case (int)ArmorMaterialTypes.Steel:
                case (int)ArmorMaterialTypes.Silver:
                    value = 5;
                    break;
                case (int)ArmorMaterialTypes.Elven:
                    value = 7;
                    break;
                case (int)ArmorMaterialTypes.Dwarven:
                    value = 9;
                    break;
                case (int)ArmorMaterialTypes.Mithril:
                case (int)ArmorMaterialTypes.Adamantium:
                    value = 11;
                    break;
                case (int)ArmorMaterialTypes.Ebony:
                    value = 13;
                    break;
                case (int)ArmorMaterialTypes.Orcish:
                    value = 15;
                    break;
                case (int)ArmorMaterialTypes.Daedric:
                    value = 17;
                    break;
            }
            return value;
        }

        public int GetVariantFromMessage(int message)
        {
            //returns the last digit of the value (eg, the variant)
            //Debug.Log("VANILLA ARMOR REPLACER - GET VARIANT - MESSAGE IS " + message.ToString());

            int variant = Mathf.FloorToInt(message % 10);

            if (message < 0)
                variant = -1;
            else
                variant = Mathf.FloorToInt(message % 10);

            //Debug.Log("VANILLA ARMOR REPLACER - GET VARIANT - VARIANT IS " + variant.ToString());
            return variant;
        }

        public int SetVariantToMessage(int message, int variant)
        {
            //replaces the value in the last digit as the variant
            //Debug.Log("VANILLA ARMOR REPLACER - SET VARIANT - MESSAGE IS " + message.ToString() + " AND VARIANT IS " + variant.ToString());

            if (message < 10)
            {
                Debug.Log("VANILLA ARMOR REPLACER - SET VARIANT - NEW MESSAGE IS " + variant.ToString());
                return variant;
            }

            int newMessage = (Mathf.FloorToInt((message / 10) % 10)*10) + variant;
            //Debug.Log("VANILLA ARMOR REPLACER - SET VARIANT - NEW MESSAGE IS " + newMessage.ToString());

            return newMessage;
        }

        public int GetTypeFromMessage(int message)
        {
            //returns the 10s of the value (eg, the type)
            //Debug.Log("VANILLA ARMOR REPLACER - GET TYPE - MESSAGE IS " + message.ToString());

            int type;

            if (message < 10)
            {
                type = 0;
                Debug.Log("VANILLA ARMOR REPLACER - GET TYPE - TYPE IS " + type.ToString());
                return type;
            }

            type = Mathf.FloorToInt((message / 10) % 10);
            //Debug.Log("VANILLA ARMOR REPLACER - GET TYPE - TYPE IS " + type.ToString());
            return type;
        }

        public int SetTypeToMessage(int message, int type)
        {
            //replaces the 10s value as the type
            //Debug.Log("VANILLA ARMOR REPLACER - SET TYPE - MESSAGE IS " + message.ToString() + " AND TYPE IS " + type.ToString());

            int newMessage;

            if (message < 0)
                message = 0;

            if (message < 10)
            {
                newMessage = message + (type * 10);
                Debug.Log("VANILLA ARMOR REPLACER - SET TYPE - NEW MESSAGE IS " + newMessage.ToString());
                return newMessage;
            }

            newMessage = Mathf.FloorToInt(message % 10) + (type * 10);
            //Debug.Log("VANILLA ARMOR REPLACER - SET TYPE - NEW MESSAGE IS " + newMessage.ToString());
            return newMessage;
        }

        public void SettingConflict()
        {
            if (conflict != null)
                return;

            conflict = MessageSettingConflict();
            StartCoroutine(conflict);
        }

        IEnumerator MessageSettingConflict()
        {
            yield return new WaitForSeconds(1);

            string[] strings = new string[3];
            strings[0] = "< CONFIGURABLE ARMOR PROGRESSION >";
            strings[1] = "Texture Archive is not set to Custom Typed.";
            strings[2] = "'Universal Armor Types' has been disabled";
            TextFile.Token[] texts = DaggerfallUnity.Instance.TextProvider.CreateTokens(TextFile.Formatting.JustifyCenter, strings);

            DaggerfallUI.MessageBox(texts);

            yield return new WaitForSeconds(1);

            conflict = null;
        }
        void ResetPlayerArmorValues()
        {
            //unequip all items
            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
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

        //old experiment to recolor objects with custom dyes
        public DFBitmap ChangeArmorDye(DFBitmap srcBitmap, ArmorMaterialTypes metal)
        {
            // Clone bitmap and get colour table for swaps
            DFBitmap dstBitmap = DFBitmap.CloneDFBitmap(srcBitmap, false);
            byte[] swaps = GetMetalColorTable(metal);

            //Starting range for weapons and armor
            int start = 0x70;

            // Swap indices start through start + 15 with colour table
            int rowPos;
            for (int y = 0; y < srcBitmap.Height; y++)
            {
                rowPos = y * srcBitmap.Width;
                for (int x = 0; x < srcBitmap.Width; x++)
                {
                    int srcOffset = rowPos + x;
                    byte index = srcBitmap.Data[srcOffset];

                    if (index >= start && index <= start + 0x0f)
                    {
                        int tintOffset = index - start;
                        dstBitmap.Data[srcOffset] = swaps[tintOffset];
                    }
                    else
                    {
                        dstBitmap.Data[srcOffset] = index;
                    }
                }
            }

            return dstBitmap;
        }
        /// <summary>
        /// Gets colour table for each metal type.
        /// </summary>
        public byte[] GetMetalColorTable(ArmorMaterialTypes metal)
        {
            byte[] indices;
            switch (metal)
            {
                case ArmorMaterialTypes.Leather:
                    indices = dyeLeather;
                    break;
                case ArmorMaterialTypes.Chain:
                    indices = dyeChain;
                    break;
                case ArmorMaterialTypes.Chain2:
                    indices = dyeChain;
                    break;
                case ArmorMaterialTypes.Iron:
                    indices = dyeIron;
                    break;
                case ArmorMaterialTypes.Steel:
                    indices = dyeSteel;
                    break;
                case ArmorMaterialTypes.Silver:
                    indices = dyeSilver;
                    break;
                case ArmorMaterialTypes.Elven:
                    indices = dyeElven;
                    break;
                case ArmorMaterialTypes.Dwarven:
                    indices = dyeDwarven;
                    break;
                case ArmorMaterialTypes.Mithril:
                    indices = dyeMithril;
                    break;
                case ArmorMaterialTypes.Adamantium:
                    indices = dyeAdamantium;
                    break;
                case ArmorMaterialTypes.Ebony:
                    indices = dyeEbony;
                    break;
                case ArmorMaterialTypes.Orcish:
                    indices = dyeOrcish;
                    break;
                case ArmorMaterialTypes.Daedric:
                    indices = dyeDaedric;
                    break;
                default:
                    indices = new byte[] { 0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x7B, 0x7C, 0x7D, 0x7E, 0x7F };
                    break;
            }

            return indices;
        }
    }
}
