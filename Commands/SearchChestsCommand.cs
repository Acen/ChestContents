using System;
using System.Collections.Generic;
using System.Linq;
using ChestContents.Managers;
using ChestContents.Models;
using ChestContents.UI;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace ChestContents.Commands
{
    public class SearchChestsCommand : ConsoleCommand
    {
        private readonly string _name;

        public SearchChestsCommand(string name = "searchchests")
        {
            _name = name;
        }

        public override string Help => "Search Chests for an item";
        public override string Name => _name;
        public override bool IsNetwork => true;

        public override void Run(string[] args)
        {
            if (args.Length == 1 && args[0].ToLowerInvariant() == "config")
            {
                if (ChestContentsPlugin.ConfigPanelManagerInstance == null)
                {
                    ChestContentsPlugin.Logger.LogWarning("Config panel is not available yet. Try again in a few seconds after entering the world.");
                    return;
                }
                ChestContentsPlugin.ConfigPanelManagerInstance.ShowPanel();
                return;
            }

            if (args.Length == 0)
            {
                ChestContentsPlugin.IndicatedList?.Clear();
                var chestCount = ChestContentsPlugin.ChestInfoDict.Count;
                var itemTypes = ChestContentsPlugin.ItemNameIndex.Count;
                var totalItems = ChestContentsPlugin.ItemNameIndex.Values.SelectMany(x => x).Sum(x => x.Stack);
                var allChests = ChestContentsPlugin.LastTotalChestCount;
                var meta =
                    $"Chests indexed: {chestCount}\nAll chests: {allChests}\nUnique item types: {itemTypes}\nTotal items: {totalItems}";
                PopupManager.ShowMetaPopup(meta);
                return;
            }

            var partialItemName = args[0].ToLowerInvariant();
            var foundItems = new List<ItemLocationInfo>();
            foreach (var kvp in ChestContentsPlugin.ItemNameIndex)
                if (kvp.Key.Contains(partialItemName))
                    foundItems.AddRange(kvp.Value);

            if (foundItems.Count == 0)
            {
                ChestContentsPlugin.Logger.LogInfo($"No items found matching '{partialItemName}'.");
                return;
            }

            var topEntry = foundItems.OrderByDescending(x => x.Stack).First();
            ChestContentsPlugin.Logger.LogInfo(
                $"Found '{topEntry.ItemName}' x{topEntry.Stack} in chest {topEntry.ChestId} at {topEntry.Position}");
            var displayName = topEntry.ItemName;
            ChestInfo chestInfo;
            if (ChestContentsPlugin.ChestInfoDict.TryGetValue(topEntry.ChestId, out chestInfo) &&
                chestInfo.Contents != null)
            {
                var item = chestInfo.Contents.FirstOrDefault(i => i.m_shared.m_name == topEntry.ItemName);
                if (item != null && !string.IsNullOrEmpty(item.m_shared.m_name))
                {
                    var translated = LocalizationManager.Instance.TryTranslate(item.m_shared.m_name);
                    displayName = string.IsNullOrEmpty(translated) ? item.m_shared.m_name : translated;
                }
            }
            else if (displayName.StartsWith("$"))
            {
                var translated = LocalizationManager.Instance.TryTranslate(displayName);
                displayName = string.IsNullOrEmpty(translated) ? displayName.TrimStart('$') : translated;
            }

            PopupManager.ShowSearchResultsPopup(displayName, topEntry.Position, topEntry.Stack);
            if (ChestContentsPlugin.IndicatedList != null)
            {
                ChestContentsPlugin.IndicatedList.Clear();
                if (chestInfo.Position != Vector3.zero)
                    ChestContentsPlugin.IndicatedList.Add(chestInfo);
                else
                    ChestContentsPlugin.IndicatedList.Add(new ChestInfo
                    {
                        Position = topEntry.Position,
                        InstanceID = topEntry.ChestId,
                        Rotation = Quaternion.identity,
                        Contents = new List<ItemDrop.ItemData>(),
                        LastUpdated = DateTime.Now
                    });
            }
        }
    }
}
