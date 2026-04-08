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
    public class ItemBoatDeed : DaggerfallUnityItem
    {
        public const int templateIndex = 1321;

        public ItemBoatDeed() : base(ItemGroups.UselessItems2, templateIndex)
        {
        }

        public override bool IsStackable()
        {
            return false;
        }

        public override ItemData_v1 GetSaveData()
        {
            ItemData_v1 data = base.GetSaveData();
            data.className = typeof(ItemBoatDeed).ToString();
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

            //TO-DO: If not within range of a port, abort and show message
            Boat placedBoat = ComeSailAway.Instance.GetPlacedBoatWithUID(UID);
            if (placedBoat != null)
            {
                if ((placedBoat.MapPixel.X != GameManager.Instance.PlayerGPS.CurrentMapPixel.X ||
                    placedBoat.MapPixel.Y != GameManager.Instance.PlayerGPS.CurrentMapPixel.Y) &&
                    !ComeSailAway.Instance.IsNearPort(ComeSailAway.Instance.portSearchRange))
                {
                    DaggerfallUI.SetMidScreenText("There is no port nearby or ship is in another location");
                    return false;
                }
            }
            else
            {
                if (!ComeSailAway.Instance.IsNearPort(ComeSailAway.Instance.portSearchRange))
                {
                    DaggerfallUI.SetMidScreenText("There is no port nearby");
                    return false;
                }
            }

            //enable placement mode
            ComeSailAway.Instance.StartPlacing(this,collection);

            return true;
        }
    }
}
