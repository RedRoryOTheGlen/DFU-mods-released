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

namespace ComeSailAwayMod
{
    public class ItemBoatParts : DaggerfallUnityItem
    {
        public const int templateIndex = 1320;

        public ItemBoatParts() : base(ItemGroups.UselessItems2, templateIndex)
        {
        }

        public override bool IsStackable()
        {
            return false;
        }

        public override ItemData_v1 GetSaveData()
        {
            ItemData_v1 data = base.GetSaveData();
            data.className = typeof(ItemBoatParts).ToString();
            return data;
        }

        public override bool UseItem(ItemCollection collection)
        {
            Debug.Log("COME SAIL AWAY - USING BOAT PARTS!");

            if (GameManager.Instance.PlayerEnterExit.IsPlayerInside)
                return false;

            //close inventory
            DaggerfallInventoryWindow inventoryWindow = DaggerfallUI.UIManager.TopWindow as DaggerfallInventoryWindow;
            if (inventoryWindow != null)
                inventoryWindow.CloseWindow();

            //enable placement mode
            ComeSailAway.Instance.StartPlacing(this,collection);

            return true;
        }
    }
}
