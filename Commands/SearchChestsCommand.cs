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
        private readonly Func<IndicatedChestList> _getIndicatedList;
        private readonly Func<Dictionary<int, ChestInfo>> _getChestInfoDict;
        private readonly Func<Dictionary<string, List<ItemLocationInfo>>> _getItemNameIndex;
        private readonly Func<int> _getLastTotalChestCount;

        public SearchChestsCommand(
            Func<IndicatedChestList> getIndicatedList,
            Func<Dictionary<int, ChestInfo>> getChestInfoDict,
            Func<Dictionary<string, List<ItemLocationInfo>>> getItemNameIndex,
            Func<int> getLastTotalChestCount,
            string name = "searchchests")
        {
            _name = name;
            _getIndicatedList = getIndicatedList;
            _getChestInfoDict = getChestInfoDict;
            _getItemNameIndex = getItemNameIndex;
            _getLastTotalChestCount = getLastTotalChestCount;
        }

        public override string Help => "Search Chests for an item";
        public override string Name => _name;
        public override bool IsNetwork => true;

        public override void Run(string[] args)
        {
            var indicatedList = _getIndicatedList();
            var chestInfoDict = _getChestInfoDict();
            var itemNameIndex = _getItemNameIndex();
            var lastTotalChestCount = _getLastTotalChestCount();

            if (args.Length == 0)
            {
                indicatedList?.Clear();
                var chestCount = chestInfoDict.Count;
                var itemTypes = itemNameIndex.Count;
                var totalItems = itemNameIndex.Values.SelectMany(x => x).Sum(x => x.Stack);
                var allChests = lastTotalChestCount;
                var meta =
                    $"Chests indexed: {chestCount}\nAll chests: {allChests}\nUnique item types: {itemTypes}\nTotal items: {totalItems}";
                PopupManager.ShowMetaPopup(meta);
                return;
            }

            var partialItemName = args[0].ToLowerInvariant();
            var foundItems = new List<ItemLocationInfo>();
            foreach (var kvp in itemNameIndex)
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
            ChestInfo chestInfo = null;
            chestInfoDict.TryGetValue(topEntry.ChestId, out chestInfo);
            if (chestInfo != null && chestInfo.Contents != null)
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
            if (indicatedList != null)
            {
                indicatedList.Clear();
                if (chestInfo != null && chestInfo.Position != Vector3.zero)
                    indicatedList.Add(chestInfo);
                else
                    indicatedList.Add(new ChestInfo(
                        topEntry.Position,
                        topEntry.ChestId,
                        Quaternion.identity,
                        new List<ItemDrop.ItemData>(),
                        DateTime.Now,
                        0));
            }
        }
    }
}
