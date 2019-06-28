using PoeHUD.Models;
using PoeHUD.Models.Enums;
using PoeHUD.Poe.Components;
using PoeHUD.Poe.Elements;
using SharpDX;
using System.Linq;
using Map = PoeHUD.Poe.Components.Map;

namespace Stashie.Filters
{
    public class ItemData
    {
        private readonly NormalInventoryItem _inventoryItem;
        public string BaseName;
        public string Name;
        public string ProphecyName;
        public bool BIdentified;
        public string ClassName;
        public bool IsElder;
        public bool IsShaper;
        public bool IsCorrupted;
        public int ItemLevel;
        public int NumberOfSockets;
        public int LargestLinkSize;
        public int ItemQuality;
        public int MapTier;
        public bool IsIncubator;

        public string Path;
        public ItemRarity Rarity;
        public class VeilMod
        {
            public enum VeiledAttributeTypes
            {
                IMPLICIT,
                PREFIX,
                SUFFIX,
                UNKNOWN
            }
            private string InMemoryVeilId = "Veiled";
            private string InMemoryPrefixId = "Prefix";
            private string InMemorySuffixId = "Suffix";
            private string RawName { get; set; } = null;
            public VeiledAttributeTypes VeiledAttributeType { get; set; } = VeiledAttributeTypes.UNKNOWN;
            public bool IsVeiled { get; set; }
            public VeilMod(string rawName)
            {
                RawName = rawName;
                Initialize();
            }
            private void Initialize()
            {
                if (VeiledAttributeType == VeiledAttributeTypes.UNKNOWN && RawName != null)
                {
                    // FIXME: create grammer
                    var index = RawName.IndexOf(InMemoryVeilId);
                    if (index >= 0)
                    {
                        IsVeiled = true;
                        VeiledAttributeType = RawName.Substring(index)?.IndexOf(InMemorySuffixId) >= 0 ?
                            VeiledAttributeTypes.SUFFIX : VeiledAttributeTypes.PREFIX;
                    }
                }
            }
        };
        public VeilMod[] VeiledMods { get; set; }

        public ItemData(NormalInventoryItem inventoryItem, BaseItemType baseItemType)
        {
            _inventoryItem = inventoryItem;
            var item = inventoryItem.Item;
            Path = item.Path;
            var @base = item.GetComponent<Base>();
            IsElder = @base.isElder;
            IsShaper = @base.isShaper;
            IsCorrupted = @base.isCorrupted;
            var mods = item.GetComponent<Mods>();
            Rarity = mods.ItemRarity;
            BIdentified = mods.Identified;
            ItemLevel = mods.ItemLevel;

            var sockets = item.GetComponent<Sockets>();
            NumberOfSockets = sockets.NumberOfSockets;
            LargestLinkSize = sockets.LargestLinkSize;

            var quality = item.GetComponent<Quality>();
            ItemQuality = quality.ItemQuality;
            ClassName = baseItemType.ClassName;
            BaseName = baseItemType.BaseName;
            VeiledMods = mods.ItemMods.Select(itemMod => new VeilMod(itemMod.Name)).Where(veilMod => veilMod.IsVeiled).ToArray();
            IsIncubator = Path.Contains("Incubation");

            if (@base.Name == "Prophecy")
            {
                var @prophParse = item.GetComponent<Prophecy>();
                ProphecyName = @prophParse.DatProphecy.Name.ToLower();
                ProphecyName = ProphecyName.Replace(" ", "");
                ProphecyName = ProphecyName.Replace(",", "");
                ProphecyName = ProphecyName.Replace("'", "");
                Name = ProphecyName;
                BaseName = "Prophecy";
            }

            MapTier = item.HasComponent<Map>() ? item.GetComponent<Map>().Tier : 0;
        }

        public Vector2 GetClickPos()
        {
            return _inventoryItem.GetClientRect().Center;
        }
    }
}