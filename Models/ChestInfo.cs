using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;

namespace ChestContents.Models
{
    public class ChestInfo
    {
        public Vector3 Position { get; }
        public int InstanceID { get; }
        public Quaternion Rotation { get; }
        public List<ItemDrop.ItemData> Contents { get; }
        public DateTime LastUpdated { get; }
        public int LastRevision { get; }

        public ChestInfo(Container container)
        {
            if (!container.enabled)
                throw new Exception("Container is not enabled");
            Position = container.transform.position;
            Rotation = container.transform.rotation;
            InstanceID = container.GetInstanceID();
            Contents = container.GetInventory().GetAllItemsInGridOrder();
            LastUpdated = DateTime.Now;
            object revObj = Traverse.Create(container).Field("m_lastRevision").GetValue();
            if (revObj is int revInt)
                LastRevision = revInt;
            else if (revObj is long revLong)
                LastRevision = (int)revLong;
            else if (revObj is uint revUInt)
                LastRevision = (int)revUInt;
            else if (revObj is short revShort)
                LastRevision = revShort;
            else if (revObj is byte revByte)
                LastRevision = revByte;
            else
                LastRevision = 0;
        }

        public ChestInfo(Vector3 position, int instanceId, Quaternion rotation, List<ItemDrop.ItemData> contents, DateTime lastUpdated, int lastRevision)
        {
            Position = position;
            InstanceID = instanceId;
            Rotation = rotation;
            Contents = contents;
            LastUpdated = lastUpdated;
            LastRevision = lastRevision;
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

