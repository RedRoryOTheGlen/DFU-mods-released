// Project:         RoleplayRealism:Items mod for Daggerfall Unity (http://www.dfworkshop.net)
// Copyright:       Copyright (C) 2020 Hazelnut
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Author:          Hazelnut

using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop;

namespace VanillaArmorReplacer
{
    public class ItemGreaves : DaggerfallUnityItem
    {
        public const int templateIndex = 1302;
        public const int textureRecord = 3;

        public ItemGreaves() : base(ItemGroups.Armor, templateIndex)
        {
        }
        public override int CurrentVariant
        {
            set
            {
                drawOrder = ItemTemplate.drawOrderOrEffect;
                if (VanillaArmorReplacer.Instance.textureArchive == 3)
                {
                    base.CurrentVariant = 0;

                    //set variant
                    int messageVariant = VanillaArmorReplacer.Instance.GetVariantFromMessage(message);
                    bool reset = false;
                    if (NativeMaterialValue == VanillaArmorReplacer.Instance.GetLeatherMaterialValue(nativeMaterialValue) || nativeMaterialValue == (int)ArmorMaterialTypes.Leather)
                    {
                        //is leather or spoofed leather
                        if (messageVariant < 0 || messageVariant > VanillaArmorReplacer.Instance.variantsLeather[textureRecord] - 1)
                        {
                            message = VanillaArmorReplacer.Instance.SetVariantToMessage(message, Random.Range(0, VanillaArmorReplacer.Instance.variantsLeather[textureRecord]));
                            reset = true;
                        }
                    }
                    else if (NativeMaterialValue == VanillaArmorReplacer.Instance.GetChainMaterialValue(nativeMaterialValue) || nativeMaterialValue == (int)ArmorMaterialTypes.Chain)
                    {
                        //is chain or spoofed chain
                        if (messageVariant < 0 || messageVariant > VanillaArmorReplacer.Instance.variantsChain[textureRecord] - 1)
                        {
                            message = VanillaArmorReplacer.Instance.SetVariantToMessage(message, Random.Range(0, VanillaArmorReplacer.Instance.variantsChain[textureRecord]));
                            reset = true;
                        }
                    }
                    else
                    {
                        //is plate
                        if (messageVariant < 0 || messageVariant > VanillaArmorReplacer.Instance.variantsPlate[textureRecord] - 1)
                        {
                            message = VanillaArmorReplacer.Instance.SetVariantToMessage(message, Random.Range(0, VanillaArmorReplacer.Instance.variantsPlate[textureRecord]));
                            reset = true;
                        }
                    }

                    //set type if necessary
                    if (reset && VanillaArmorReplacer.Instance.UniversalArmorTypes)
                    {
                        int type = Random.Range(0, 3);

                        if (nativeMaterialValue == (int)ArmorMaterialTypes.Leather)
                            type = 0;
                        if (nativeMaterialValue == (int)ArmorMaterialTypes.Chain || nativeMaterialValue == (int)ArmorMaterialTypes.Chain2)
                            type = 1;
                        if (nativeMaterialValue == (int)ArmorMaterialTypes.Iron)
                            type = 2;

                        message = VanillaArmorReplacer.Instance.SetTypeToMessage(message, type);
                        Debug.Log("VANILLA ARMOR REPLACER - NO TYPE DETECTED, SETTING TYPE TO " + type.ToString());
                    }

                    if (NativeMaterialValue == VanillaArmorReplacer.Instance.GetLeatherMaterialValue(nativeMaterialValue))
                    {
                        if (!HasCustomEnchantments && !HasLegacyEnchantments)
                            shortName = VanillaArmorReplacer.Instance.nameGreavesLeather;
                    }
                    else if (NativeMaterialValue == VanillaArmorReplacer.Instance.GetChainMaterialValue(nativeMaterialValue))
                    {
                        if (!HasCustomEnchantments && !HasLegacyEnchantments)
                            shortName = VanillaArmorReplacer.Instance.nameGreavesChain;
                    }
                    else
                    {
                        if (!HasCustomEnchantments && !HasLegacyEnchantments)
                            shortName = VanillaArmorReplacer.Instance.nameGreavesPlate;
                    }
                }
                else if (VanillaArmorReplacer.Instance.textureArchive == 2 && (nativeMaterialValue == (int)ArmorMaterialTypes.Leather || nativeMaterialValue == (int)ArmorMaterialTypes.Chain))
                {
                    base.CurrentVariant = 0;
                    if (NativeMaterialValue == VanillaArmorReplacer.Instance.GetLeatherMaterialValue(nativeMaterialValue))
                    {
                        if (!HasCustomEnchantments && !HasLegacyEnchantments)
                            shortName = VanillaArmorReplacer.Instance.nameGreavesLeather;
                    }
                    else if (NativeMaterialValue == VanillaArmorReplacer.Instance.GetChainMaterialValue(nativeMaterialValue))
                    {
                        if (!HasCustomEnchantments && !HasLegacyEnchantments)
                            shortName = VanillaArmorReplacer.Instance.nameGreavesChain;
                    }
                    else
                    {
                        base.CurrentVariant = Mathf.Clamp(value, 2, 5);
                        if (!HasCustomEnchantments && !HasLegacyEnchantments)
                            shortName = VanillaArmorReplacer.Instance.nameGreavesPlate;
                    }
                }
                else if (VanillaArmorReplacer.Instance.textureArchive == 1)
                {
                    if (NativeMaterialValue == VanillaArmorReplacer.Instance.GetLeatherMaterialValue(nativeMaterialValue))
                    {
                        base.CurrentVariant = Random.Range(0, 2);
                        if (!HasCustomEnchantments && !HasLegacyEnchantments)
                            shortName = VanillaArmorReplacer.Instance.nameGreavesLeather;
                    }
                    else if (NativeMaterialValue == VanillaArmorReplacer.Instance.GetChainMaterialValue(nativeMaterialValue))
                    {
                        base.CurrentVariant = 6;
                        if (!HasCustomEnchantments && !HasLegacyEnchantments)
                            shortName = VanillaArmorReplacer.Instance.nameGreavesChain;
                    }
                    else
                    {
                        base.CurrentVariant = Mathf.Clamp(value, 2, 5);
                        if (!HasCustomEnchantments && !HasLegacyEnchantments)
                            shortName = VanillaArmorReplacer.Instance.nameGreavesPlate;
                    }
                }
                else
                {
                    if (nativeMaterialValue == (int)ArmorMaterialTypes.Leather)
                    {
                        base.CurrentVariant = Random.Range(0, 2);
                        if (!HasCustomEnchantments && !HasLegacyEnchantments)
                            shortName = VanillaArmorReplacer.Instance.nameGreavesLeather;
                    }
                    else if (nativeMaterialValue == (int)ArmorMaterialTypes.Chain)
                    {
                        base.CurrentVariant = 6;
                        if (!HasCustomEnchantments && !HasLegacyEnchantments)
                            shortName = VanillaArmorReplacer.Instance.nameGreavesChain;
                    }
                    else
                    {
                        base.CurrentVariant = Mathf.Clamp(value, 2, 5);
                        if (!HasCustomEnchantments && !HasLegacyEnchantments)
                        {
                            if (NativeMaterialValue == VanillaArmorReplacer.Instance.GetLeatherMaterialValue(nativeMaterialValue))
                                shortName = VanillaArmorReplacer.Instance.nameGreavesLeather;
                            else if (NativeMaterialValue == VanillaArmorReplacer.Instance.GetChainMaterialValue(nativeMaterialValue))
                                shortName = VanillaArmorReplacer.Instance.nameGreavesChain;
                            else
                                shortName = VanillaArmorReplacer.Instance.nameGreavesPlate;
                        }
                    }
                }

                //shortName += " " + message.ToString();
            }
        }

        //set gender and phenotype

        //set gender and phenotype
        public override int InventoryTextureArchive
        {
            get
            {
                if (
                    VanillaArmorReplacer.Instance.textureArchive == 3 ||
                    (VanillaArmorReplacer.Instance.textureArchive == 2 && (nativeMaterialValue == (int)ArmorMaterialTypes.Leather || nativeMaterialValue == (int)ArmorMaterialTypes.Chain))
                    )
                {
                    if (VanillaArmorReplacer.Instance.IsPlayerBuffed())
                        return 112350;

                    int offset = PlayerTextureArchive - ItemBuilder.firstFemaleArchive;

                    if (offset < 4)
                        return 112354;
                    else
                        return 112350;
                }
                else
                {
                    if (VanillaArmorReplacer.Instance.IsPlayerBuffed())
                        return 250;

                    return base.InventoryTextureArchive;
                }
            }
        }

        public override int InventoryTextureRecord
        {
            get
            {
                if (VanillaArmorReplacer.Instance.textureArchive == 3 ||
                    (VanillaArmorReplacer.Instance.textureArchive == 2 && (nativeMaterialValue == (int)ArmorMaterialTypes.Leather || nativeMaterialValue == (int)ArmorMaterialTypes.Chain))
                    )
                {
                    int offset = textureRecord;

                    if (VanillaArmorReplacer.Instance.textureArchive == 3)
                    {
                        //get Typed archive
                        if (VanillaArmorReplacer.Instance.UniversalArmorTypes)
                        {
                            int type = VanillaArmorReplacer.Instance.GetTypeFromMessage(message);
                            if (type == 2)
                                offset += 200;
                            else if (type == 1)
                                offset += 100;
                        }
                        else
                        {
                            if (NativeMaterialValue >= (int)ArmorMaterialTypes.Iron)
                                offset += 200;
                            else if (NativeMaterialValue == VanillaArmorReplacer.Instance.GetChainMaterialValue(nativeMaterialValue) || nativeMaterialValue == (int)ArmorMaterialTypes.Chain)
                                offset += 100;
                        }

                        //get variant
                        //if (message != -1)
                        offset += 1000 * VanillaArmorReplacer.Instance.GetVariantFromMessage(message);
                    }
                    else
                    {
                        //get custom archive
                        if (nativeMaterialValue == (int)ArmorMaterialTypes.Daedric)
                            offset += 1100;
                        else if (nativeMaterialValue == (int)ArmorMaterialTypes.Orcish)
                            offset += 1000;
                        else if (nativeMaterialValue == (int)ArmorMaterialTypes.Ebony)
                            offset += 900;
                        else if (nativeMaterialValue == (int)ArmorMaterialTypes.Adamantium)
                            offset += 800;
                        else if (nativeMaterialValue == (int)ArmorMaterialTypes.Mithril)
                            offset += 700;
                        else if (nativeMaterialValue == (int)ArmorMaterialTypes.Dwarven)
                            offset += 600;
                        else if (nativeMaterialValue == (int)ArmorMaterialTypes.Elven)
                            offset += 500;
                        else if (nativeMaterialValue == (int)ArmorMaterialTypes.Silver)
                            offset += 400;
                        else if (nativeMaterialValue == (int)ArmorMaterialTypes.Steel)
                            offset += 300;
                        else if (nativeMaterialValue == (int)ArmorMaterialTypes.Iron)
                            offset += 200;
                        else if (nativeMaterialValue == (int)ArmorMaterialTypes.Chain)
                            offset += 100;
                    }


                    //get Material variant
                    if (nativeMaterialValue == (int)ArmorMaterialTypes.Daedric)
                        offset += 90;
                    else if (nativeMaterialValue == (int)ArmorMaterialTypes.Orcish)
                        offset += 80;
                    else if (nativeMaterialValue == (int)ArmorMaterialTypes.Ebony)
                        offset += 70;
                    else if (nativeMaterialValue == (int)ArmorMaterialTypes.Adamantium)
                        offset += 60;
                    else if (nativeMaterialValue == (int)ArmorMaterialTypes.Mithril)
                        offset += 50;
                    else if (nativeMaterialValue == (int)ArmorMaterialTypes.Dwarven)
                        offset += 40;
                    else if (nativeMaterialValue == (int)ArmorMaterialTypes.Elven)
                        offset += 30;
                    else if (nativeMaterialValue == (int)ArmorMaterialTypes.Silver)
                        offset += 20;
                    else if (nativeMaterialValue == (int)ArmorMaterialTypes.Steel)
                        offset += 10;

                    dyeColor = DyeColors.Silver;

                    return offset;
                }
                else
                {
                    dyeColor = DaggerfallUnity.Instance.ItemHelper.GetArmorDyeColor((ArmorMaterialTypes)nativeMaterialValue);
                    return base.InventoryTextureRecord;
                }
            }
        }

        public override int NativeMaterialValue
        {
            get
            {
                if (VanillaArmorReplacer.Instance.UniversalArmorTypes)
                {
                    int armorType = VanillaArmorReplacer.Instance.GetTypeFromMessage(message);

                    if (nativeMaterialValue == (int)ArmorMaterialTypes.Leather || nativeMaterialValue == (int)ArmorMaterialTypes.Chain || nativeMaterialValue == (int)ArmorMaterialTypes.Iron)
                        return nativeMaterialValue;
                    else
                    {
                        if (armorType == 0)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Leather);
                        else if (armorType == 1)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Chain);
                        else
                            return nativeMaterialValue;
                    }
                }
                else
                {
                    if (nativeMaterialValue == (int)ArmorMaterialTypes.Leather || nativeMaterialValue == (int)ArmorMaterialTypes.Chain)
                        return nativeMaterialValue;
                    else if (nativeMaterialValue == (int)ArmorMaterialTypes.Iron)
                    {
                        if (VanillaArmorReplacer.Instance.armorIron == 1)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Chain);
                        else
                        if (VanillaArmorReplacer.Instance.armorIron == 2)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Leather);
                        else
                            return nativeMaterialValue;
                    }
                    else if (nativeMaterialValue == (int)ArmorMaterialTypes.Steel)
                    {
                        if (VanillaArmorReplacer.Instance.armorSteel == 1)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Chain);
                        else
                        if (VanillaArmorReplacer.Instance.armorSteel == 2)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Leather);
                        else
                            return nativeMaterialValue;
                    }
                    else if (nativeMaterialValue == (int)ArmorMaterialTypes.Silver)
                    {
                        if (VanillaArmorReplacer.Instance.armorSilver == 1)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Chain);
                        else
                        if (VanillaArmorReplacer.Instance.armorSilver == 2)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Leather);
                        else
                            return nativeMaterialValue;
                    }
                    else if (nativeMaterialValue == (int)ArmorMaterialTypes.Elven)
                    {
                        if (VanillaArmorReplacer.Instance.armorElven == 1)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Chain);
                        else
                        if (VanillaArmorReplacer.Instance.armorElven == 2)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Leather);
                        else
                            return nativeMaterialValue;
                    }
                    else if (nativeMaterialValue == (int)ArmorMaterialTypes.Dwarven)
                    {
                        if (VanillaArmorReplacer.Instance.armorDwarven == 1)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Chain);
                        else
                        if (VanillaArmorReplacer.Instance.armorDwarven == 2)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Leather);
                        else
                            return nativeMaterialValue;
                    }
                    else if (nativeMaterialValue == (int)ArmorMaterialTypes.Mithril)
                    {
                        if (VanillaArmorReplacer.Instance.armorMithril == 1)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Chain);
                        else
                        if (VanillaArmorReplacer.Instance.armorMithril == 2)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Leather);
                        else
                            return nativeMaterialValue;
                    }
                    else if (nativeMaterialValue == (int)ArmorMaterialTypes.Adamantium)
                    {
                        if (VanillaArmorReplacer.Instance.armorAdamantium == 1)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Chain);
                        else
                        if (VanillaArmorReplacer.Instance.armorAdamantium == 2)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Leather);
                        else
                            return nativeMaterialValue;
                    }
                    else if (nativeMaterialValue == (int)ArmorMaterialTypes.Ebony)
                    {
                        if (VanillaArmorReplacer.Instance.armorEbony == 1)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Chain);
                        else
                        if (VanillaArmorReplacer.Instance.armorEbony == 2)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Leather);
                        else
                            return nativeMaterialValue;
                    }
                    else if (nativeMaterialValue == (int)ArmorMaterialTypes.Orcish)
                    {
                        if (VanillaArmorReplacer.Instance.armorOrcish == 1)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Chain);
                        else
                        if (VanillaArmorReplacer.Instance.armorOrcish == 2)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Leather);
                        else
                            return nativeMaterialValue;
                    }
                    else if (nativeMaterialValue == (int)ArmorMaterialTypes.Daedric)
                    {
                        if (VanillaArmorReplacer.Instance.armorDaedric == 1)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Chain);
                        else
                        if (VanillaArmorReplacer.Instance.armorDaedric == 2)
                            return VanillaArmorReplacer.Instance.GetNativeMaterialValueBitwise(nativeMaterialValue, (int)ArmorMaterialTypes.Leather);
                        else
                            return nativeMaterialValue;
                    }
                    else
                        return nativeMaterialValue;
                }
            }
        }

        public override EquipSlots GetEquipSlot()
        {
            return EquipSlots.LegsArmor;
        }

        public override bool UseItem(ItemCollection collection)
        {
            if (VanillaArmorReplacer.Instance.useCycleVariant)
            {
                //change variant on use
                if (VanillaArmorReplacer.Instance.textureArchive == 3)
                {
                    //texture archive is Custom-Typed

                    if (VanillaArmorReplacer.Instance.UniversalArmorTypes && InputManager.Instance.GetKey(KeyCode.LeftShift))
                    {
                        int types = 3;

                        if (IsEquipped)
                            GameManager.Instance.PlayerEntity.UpdateEquippedArmorValues(this, false);

                        int offset = VanillaArmorReplacer.Instance.GetTypeFromMessage(message) + 1;
                        if (offset >= types)
                            message = VanillaArmorReplacer.Instance.SetTypeToMessage(message, 0);
                        else
                            message = VanillaArmorReplacer.Instance.SetTypeToMessage(message, offset);

                        weightInKg = VanillaArmorReplacer.Instance.GetWeightOfTypedArmor(this);

                        if (IsEquipped)
                            GameManager.Instance.PlayerEntity.UpdateEquippedArmorValues(this, true);

                        CurrentVariant = VanillaArmorReplacer.Instance.GetVariantFromMessage(message);
                    }
                    else
                    {
                        int variants = 1;
                        if (VanillaArmorReplacer.Instance.UniversalArmorTypes)
                        {
                            int type = VanillaArmorReplacer.Instance.GetTypeFromMessage(message);
                            if (type == 2)
                            {
                                //item is plate
                                variants = VanillaArmorReplacer.Instance.variantsPlate[textureRecord];
                            }
                            else if (type == 1)
                            {
                                //item is chain or spoofed chain
                                variants = VanillaArmorReplacer.Instance.variantsChain[textureRecord];
                            }
                            else
                            {
                                //item is leather or spoofed leather
                                variants = VanillaArmorReplacer.Instance.variantsLeather[textureRecord];
                            }
                        }
                        else
                        {
                            if (nativeMaterialValue == (int)ArmorMaterialTypes.Leather || NativeMaterialValue == VanillaArmorReplacer.Instance.GetLeatherMaterialValue(nativeMaterialValue))
                            {
                                //item is leather or spoofed leather
                                variants = VanillaArmorReplacer.Instance.variantsLeather[textureRecord];
                            }
                            else if (nativeMaterialValue == (int)ArmorMaterialTypes.Chain || NativeMaterialValue == VanillaArmorReplacer.Instance.GetChainMaterialValue(nativeMaterialValue))
                            {
                                //item is chain or spoofed chain
                                variants = VanillaArmorReplacer.Instance.variantsChain[textureRecord];
                            }
                            else
                            {
                                //item is plate
                                variants = VanillaArmorReplacer.Instance.variantsPlate[textureRecord];
                            }
                        }

                        int offset = VanillaArmorReplacer.Instance.GetVariantFromMessage(message) + 1;
                        if (offset >= variants)
                            message = VanillaArmorReplacer.Instance.SetVariantToMessage(message, 0);
                        else
                            message = VanillaArmorReplacer.Instance.SetVariantToMessage(message, offset);

                        CurrentVariant = VanillaArmorReplacer.Instance.GetVariantFromMessage(message);
                    }

                    DaggerfallUI.Instance.InventoryWindow.Refresh(true);
                }
                else if (VanillaArmorReplacer.Instance.textureArchive == 2)
                {

                }
                else if (VanillaArmorReplacer.Instance.textureArchive == 1)
                {
                    //texture archive is Vanilla-Typed
                    if (nativeMaterialValue == (int)ArmorMaterialTypes.Leather || NativeMaterialValue == VanillaArmorReplacer.Instance.GetLeatherMaterialValue(nativeMaterialValue))
                    {
                        //item is leather or spoofed leather
                    }
                    else if (nativeMaterialValue == (int)ArmorMaterialTypes.Chain || NativeMaterialValue == VanillaArmorReplacer.Instance.GetChainMaterialValue(nativeMaterialValue))
                    {
                        //item is chain or spoofed chain
                    }
                    else
                    {
                        //item is plate
                        int offset = base.CurrentVariant + 1;
                        if (offset >= 4)
                            base.CurrentVariant = 1;
                        else
                            base.CurrentVariant = offset;

                        DaggerfallUI.Instance.InventoryWindow.Refresh(true);
                        message = VanillaArmorReplacer.Instance.SetVariantToMessage(message, CurrentVariant);
                    }
                }
                else if (VanillaArmorReplacer.Instance.textureArchive == 0)
                {
                    //texture archive is Vanilla
                    if (nativeMaterialValue == (int)ArmorMaterialTypes.Leather)
                    {
                        //item is leather
                    }
                    else if (nativeMaterialValue == (int)ArmorMaterialTypes.Chain)
                    {
                        //item is chain
                    }
                    else
                    {
                        //item is plate
                        int offset = base.CurrentVariant + 1;
                        if (offset >= 4)
                            base.CurrentVariant = 1;
                        else
                            base.CurrentVariant = offset;

                        DaggerfallUI.Instance.InventoryWindow.Refresh(true);
                        message = VanillaArmorReplacer.Instance.SetVariantToMessage(message, CurrentVariant);
                    }
                }
            }


            return base.UseItem(collection);
        }

        public override int GetMaterialArmorValue()
        {
            if (VanillaArmorReplacer.Instance.UniversalArmorTypes)
            {
                if (nativeMaterialValue == (int)ArmorMaterialTypes.Leather || nativeMaterialValue == (int)ArmorMaterialTypes.Chain || nativeMaterialValue == (int)ArmorMaterialTypes.Iron)
                    return base.GetMaterialArmorValue();
                else
                {
                    int armorType = VanillaArmorReplacer.Instance.GetTypeFromMessage(message);
                    if (armorType == 2)
                        return base.GetMaterialArmorValue();
                    else if (armorType == 1)
                        return VanillaArmorReplacer.Instance.GetChainArmorValue(nativeMaterialValue);
                    else
                        return VanillaArmorReplacer.Instance.GetLeatherArmorValue(nativeMaterialValue);
                }
            }
            else
                return base.GetMaterialArmorValue();
        }

        public override int GetEnchantmentPower()
        {
            float multiplier = FormulaHelper.GetArmorEnchantmentMultiplier((ArmorMaterialTypes)nativeMaterialValue);
            return enchantmentPoints + Mathf.FloorToInt(enchantmentPoints * multiplier);
        }

        public override SoundClips GetEquipSound()
        {
            return SoundClips.EquipLeather;
        }

        public override ItemData_v1 GetSaveData()
        {
            ItemData_v1 data = base.GetSaveData();
            data.className = typeof(ItemGreaves).ToString();
            return data;
        }

    }
}

