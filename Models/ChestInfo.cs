using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChestContents.Models
{
    public struct ChestInfo
    {
        public Vector3 Position;
        public int InstanceID;
        public Quaternion Rotation;
        public List<ItemDrop.ItemData> Contents;
        public DateTime LastUpdated;

        public ChestInfo(Container container)
        {
            if (!container.enabled)
                throw new Exception("Container is not enabled");
            Position = container.transform.position;
            Rotation = container.transform.rotation;
            InstanceID = container.GetInstanceID();
            Contents = container.GetInventory().GetAllItemsInGridOrder();
            LastUpdated = DateTime.Now;
        }
    }

    public struct ItemLocationInfo
    {
        public string ItemName;
        public int Stack;
        public int ChestId;
        public Vector3 Position;
    }
}