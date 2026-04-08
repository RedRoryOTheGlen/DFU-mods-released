using UnityEngine;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.UserInterface;
using System.Collections.Generic;
using DaggerfallWorkshop;
using DaggerfallConnect.Arena2;

namespace PhysicalHUDMod
{
    public class ItemCompass : DaggerfallUnityItem
    {
        public const int templateIndex = 1330;

        public ItemCompass() : base(ItemGroups.UselessItems2, templateIndex)
        {
        }

        public override bool IsStackable()
        {
            return false;
        }

        public override ItemData_v1 GetSaveData()
        {
            ItemData_v1 data = base.GetSaveData();
            data.className = typeof(ItemCompass).ToString();
            return data;
        }

        public override bool UseItem(ItemCollection collection)
        {
            //close inventory
            DaggerfallInventoryWindow inventoryWindow = DaggerfallUI.UIManager.TopWindow as DaggerfallInventoryWindow;
            if (inventoryWindow != null)
                inventoryWindow.CloseWindow();

            //view when used
            PhysicalHUD.Instance.ToggleViewing();

            return true;
        }
    }
}
