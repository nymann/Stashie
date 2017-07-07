#region Header

//-----------------------------------------------------------------
//   Class:          StashieLogic
//   Description:    PoeHUD plugin. Main plugin logic.
//   Author:         Stridemann, nymann        Date: 08.26.2017
//-----------------------------------------------------------------

#endregion

#define DebugMode
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using PoeHUD.Hud.Menu;
using PoeHUD.Hud.Settings;
using PoeHUD.Models.Enums;
using PoeHUD.Plugins;
using PoeHUD.Poe.Components;
using PoeHUD.Poe.Elements;
using PoeHUD.Poe.EntityComponents;
using PoeHUD.Poe.RemoteMemoryObjects;
using SharpDX;
using Stashie.Filters;
using Stashie.Settings;
using Stashie.Utils;
using MenuItem = PoeHUD.Hud.Menu.MenuItem;
using System.Reflection;

namespace Stashie
{
    public class StashieCore : BaseSettingsPlugin<StashieSettings>
    {
        public StashieCore()
        {
            PluginName = "Stashie (BETA)";
        }

        private const string FITERS_CONFIG_FILE = "FitersConfig.txt";

        private bool _bDropOnce;
        private Vector2 _clickWindowOffset;
        private List<FilterResult> _dropItems;
        private int[,] _ignoredCells;
        private List<ListIndexNode> _settingsListNodes;
        private Thread _tabNamesUpdaterThread;

        private const int WHILE_DELAY = 5;
        private const int INPUT_DELAY = 15;

        private List<CustomFilter> _customFilters;
        private List<RefillProcessor> _customRefills;

        private object FRSM_Instance;
        private event Action FRSM_UpdateStashesEvent = delegate { };
        private event Action FRSM_UpdateItemsSetsInfoEvent = delegate { };

        public override void Initialise()
        {
            Settings.Enable.OnValueChanged += SetupOrClose;
            SetupOrClose();
        }

        private void CreateFileAndAppendTextIfItDoesNotExitst(string path, string content)
        {
            if (File.Exists(path))
            {
                return;
            }

            using (var streamWriter = new StreamWriter(path, true))
            {
                streamWriter.Write(content);
                streamWriter.Close();
            }
        }

        private void SaveDefaultConfigsToDisk()
        {
            var path = $"{PluginDirectory}\\GitUpdateConfig.txt";
            const string gitUpdateConfig = "Owner:nymann\r\n" +
                                           "Name:Stashie\r\n" +
                                           "Release\r\n";
            CreateFileAndAppendTextIfItDoesNotExitst(path, gitUpdateConfig);

            path = $"{PluginDirectory}\\RefillCurrency.txt";

            const string refillCurrency =
                "//MenuName:\t\t\tClassName,\t\t\tStackSize,\tInventoryX,\tInventoryY\r\n" +
                "Portal Scrolls:\t\tPortal Scroll,\t\t40,\t\t\t12,\t\t\t1\r\n" +
                "Scrolls of Wisdom:\tScroll of Wisdom,\t40,\t\t\t12,\t\t\t2\r\n" +
                "//Chances:\t\t\tOrb of Chance,\t\t20,\t\t\t12,\t\t\t3";

            CreateFileAndAppendTextIfItDoesNotExitst(path, refillCurrency);


            path = $"{PluginDirectory}\\FitersConfig.txt";

            const string filtersConfig =
                "//FilterName(menu name):\tfilters\t\t:ParentMenu(optionaly, will be created automatially for grouping)\r\n" +
                "//Filter parts should divided by coma or | (for OR operation(any filter part can pass))\r\n" +
                "\r\n" +
                "////////////\tAvailable properties:\t/////////////////////\r\n" +
                "/////////\tString (name) properties:\r\n" +
                "//classname\r\n" +
                "//basename\r\n" +
                "//path\r\n" +
                "/////////\tNumerical properties:\r\n" +
                "//itemquality\r\n//rarity\r\n//ilvl\r\n" +
                "/////////\tBoolean properties:\r\n" +
                "//identified\r\n" +
                "/////////////////////////////////////////////////////////////\r\n" +
                "////////////\tAvailable operations:\t/////////////////////\r\n" +
                "/////////\tString (name) operations:\r\n" +
                "//!=\t(not equal)\r\n" +
                "//=\t\t(equal)\r\n" +
                "//^\t\t(contains)\r\n" +
                "//!^\t(not contains)\r\n" +
                "/////////\tNumerical operations:\r\n" +
                "//!=\t(not equal)\r\n" +
                "//=\t\t(equal)\r\n" +
                "//>\t\t(bigger)\r\n" +
                "//<\t\t(less)\r\n//<=\t(less or qual)\r\n//>=\t(bigger or qual)\r\n/////////\tBoolean operations:\r\n//!\t\t(not/invert)\r\n/////////////////////////////////////////////////////////////\r\n\r\n//Default Tabs\r\nDivination Cards:\tClassName=DivinationCard\t\t\t\t\t:Default Tabs\r\nGems:\t\t\t\tClassName^Skill Gem,ItemQuality=0\t\t\t:Default Tabs\r\nCurrency:\t\t\tClassName=StackableCurrency,path!^Essence\t:Default Tabs\r\nLeaguestones:\t\tClassName=Leaguestone\t\t\t\t\t\t:Default Tabs\r\nEssences:\t\t\tBaseName^Essence,ClassName=StackableCurrency:Default Tabs\r\nJewels:\t\t\t\tClassName=Jewel\t\t\t\t\t\t\t\t:Default Tabs\r\nFlasks:\t\t\t\tClassName^Flask,ItemQuality=0\t\t\t\t:Default Tabs\r\nTalisman:\t\t\tClassName=Amulet,BaseName^Talisman\t\t\t:Default Tabs\r\nJewelery:\t\t\tClassName=Amulet|ClassName=Ring\t\t\t\t:Default Tabs\r\n//White Items:\t\tRarity=Normal\t\t\t\t\t\t\t\t:Default Tabs\r\n\r\n//Chance Items\r\nSorcerer Boots:\tBaseName=Sorcerer Boots,Rarity=Normal\t:Chance Items\r\nLeather Belt:\tBaseName=Leather Belt,Rarity=Normal\t\t:Chance Items\r\n\r\n//Vendor Recipes\r\nChisel Recipe:\t\tBaseName=Stone Hammer|BaseName=Rock Breaker,ItemQuality=20\t:Vendor Recipes\r\nQuality Gems:\t\tClassName^Skill Gem,ItemQuality>0\t\t\t\t\t\t\t:Vendor Recipes\r\nQuality Flasks:\t\tClassName^Flask,ItemQuality>0\t\t\t\t\t\t\t\t:Vendor Recipes\r\n\r\n//Maps\r\nShore Shaped:\tClassName=Map,BaseName=Shaped Shore Map\t:Maps\r\nStrand Shaped:\tClassName=Map,BaseName=Shaped Strand Map:Maps\r\nShaped Maps:\tClassName=Map,BaseName^Shaped\t\t\t:Maps\r\nUniq Maps:\t\tClassName=Map,Rarity=Unique\t\t\t\t:Maps\r\nOther Maps:\t\tClassName=Map\t\t\t\t\t\t\t:Maps\r\n\r\n//Chaos Recipe LVL 2 (unindentified and ilvl 60 or above)\r\nWeapons:\t\t!identified,Rarity=Rare,ilvl>=60,ClassName^Two Hand|ClassName^One Hand|ClassName=Bow|ClassName=Staff|ClassName=Sceptre|ClassName=Wand|ClassName=Dagger|ClassName=Claw|ClassName=Shield :Chaos Recipe\r\nJewelry:\t\t!identified,Rarity=Rare,ilvl>=60,ClassName=Ring|ClassName=Amulet \t:Chaos Recipe\r\nBelts:\t\t\t!identified,Rarity=Rare,ilvl>=60,ClassName=Belt \t\t\t\t\t:Chaos Recipe\r\nHelms:\t\t\t!identified,Rarity=Rare,ilvl>=60,ClassName=Helmet \t\t\t\t\t:Chaos Recipe\r\nBody Armours:\t!identified,Rarity=Rare,ilvl>=60,ClassName=Body Armour \t\t\t\t:Chaos Recipe\r\nBoots:\t\t\t!identified,Rarity=Rare,ilvl>=60,ClassName=Boots \t\t\t\t\t:Chaos Recipe\r\n" +
                "Gloves:\t\t\t!identified,Rarity=Rare,ilvl>=60,ClassName=Gloves \t\t\t\t\t:Chaos Recipe";

            CreateFileAndAppendTextIfItDoesNotExitst(path, filtersConfig);
        }

        private void LoadCustomFilters()
        {
            var filterPath = Path.Combine(PluginDirectory, FITERS_CONFIG_FILE);

            var filtersLines = File.ReadAllLines(filterPath);

            var unused = new FilterParser();

            _customFilters = FilterParser.Parse(filtersLines);

            var submenu = new Dictionary<string, MenuItem>();


            foreach (var customFilter in _customFilters)
            {
                ListIndexNode indexNode;


                if (!Settings.CustomFilterOptions.TryGetValue(customFilter.Name, out indexNode))
                {
                    indexNode = new ListIndexNode
                    {
                        Value = "Ignore",
                        Index = -1
                    };
                    Settings.CustomFilterOptions.Add(customFilter.Name, indexNode);
                }

                var parentMenu = PluginSettingsRootMenu;

                if (!string.IsNullOrEmpty(customFilter.SubmenuName))
                {
                    if (!submenu.TryGetValue(customFilter.SubmenuName, out parentMenu))
                    {
                        parentMenu = MenuPlugin.AddChild(PluginSettingsRootMenu, customFilter.SubmenuName);
                        submenu.Add(customFilter.SubmenuName, parentMenu);
                    }
                }
                MenuPlugin.AddChild(parentMenu, customFilter.Name, indexNode);

                customFilter.StashIndexNode = indexNode;

                _settingsListNodes.Add(indexNode);
            }
        }

        private void LoadCustomRefills()
        {
            _customRefills = RefillParser.Parse(PluginDirectory);

            if (_customRefills.Count == 0)
            {
                return;
            }

            var refillMenu = MenuPlugin.AddChild(PluginSettingsRootMenu, "Refill Currency", Settings.RefillCurrency);
            MenuPlugin.AddChild(refillMenu, "Currency Tab", Settings.CurrencyStashTab);
            MenuPlugin.AddChild(refillMenu, "Allow Have More", Settings.AllowHaveMore);

            foreach (var refill in _customRefills)
            {
                RangeNode<int> amountOption;

                if (!Settings.CustomRefillOptions.TryGetValue(refill.MenuName, out amountOption))
                {
                    amountOption = new RangeNode<int>(0, 0, refill.StackSize);
                    Settings.CustomRefillOptions.Add(refill.MenuName, amountOption);
                }
                amountOption.Max = refill.StackSize;

                refill.AmountOption = amountOption;
                MenuPlugin.AddChild(refillMenu, refill.MenuName, amountOption);
            }

            _settingsListNodes.Add(Settings.CurrencyStashTab);
        }

        private void LoadIgnoredCells()
        {
            const string fileName = @"/IgnoredCells.json";
            var filePath = PluginDirectory + fileName;

            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                try
                {
                    _ignoredCells = JsonConvert.DeserializeObject<int[,]>(json);


                    var ignoredHeight = _ignoredCells.GetLength(0);
                    var ignoredWidth = _ignoredCells.GetLength(1);

                    if (ignoredHeight != 5 || ignoredWidth != 12)
                    {
                        LogError("Stashie: Wrong IgnoredCells size! Should be 12x5. Reseting to default..", 5);
                    }
                    else
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    LogError(
                        "Stashie: Can't decode IgnoredCells settings in " + fileName +
                        ". Reseting to default. Error: " + ex.Message, 5);
                }
            }


            _ignoredCells = new[,]
            {
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1}
            };

            var defaultSettings = JsonConvert.SerializeObject(_ignoredCells);
            defaultSettings = defaultSettings.Replace("[[", "[\n[");
            defaultSettings = defaultSettings.Replace("],[", "],\n[");
            defaultSettings = defaultSettings.Replace("]]", "]\n]");
            File.WriteAllText(filePath, defaultSettings);
        }

        public override void Render()
        {
            if (!Settings.Enable)
            {
                return;
            }

            var uiTabsOpened = GameController.Game.IngameState.IngameUi.InventoryPanel.IsVisible &&
                               GameController.Game.IngameState.ServerData.StashPanel.IsVisible;

            if (!uiTabsOpened)
            {
                _bDropOnce = false;
                return;
            }

            if (Settings.RequireHotkey.Value && !Keyboard.IsKeyDown((int) Settings.DropHotkey.Value))
            {
                _bDropOnce = false;
                return;
            }

            if (_bDropOnce)
            {
                return;
            }

            _bDropOnce = true;
            ProcessInventoryItems();
        }

        private void ProcessInventoryItems()
        {
            var inventory =
                GameController.Game.IngameState.IngameUi.InventoryPanel[
                    InventoryIndex.PlayerInventory];

            var invItems = inventory.VisibleInventoryItems;

            if (invItems == null)
            {
                LogMessage("Player inventory->VisibleInventoryItems is null!", 5);
            }
            else
            {
                _dropItems = new List<FilterResult>();
                _clickWindowOffset = GameController.Window.GetWindowRectangle().TopLeft;

                foreach (var invItem in invItems)
                {
                    if (invItem.Item == null)
                    {
                        continue;
                    }

                    if (CheckIgnoreCells(invItem))
                    {
                        continue;
                    }

                    var baseItemType = GameController.Files.BaseItemTypes.Translate(invItem.Item.Path);
                    var testItem = new ItemData(invItem, baseItemType);

                    var result = CheckFilters(testItem);

                    if (result != null)
                    {
                        _dropItems.Add(result);
                    }
                }
                DropToStash();
            }
        }

        private bool CheckIgnoreCells(NormalInventoryItem inventItem)
        {
            var inventPosX = inventItem.InventPosX;
            var inventPosY = inventItem.InventPosY;

            if (_customRefills.Any(x => x.InventPos.X == inventPosX && x.InventPos.Y == inventPosY))
            {
                return true;
            }

            if (inventPosX < 0 || inventPosX >= 12)
            {
                return true;
            }
            if (inventPosY < 0 || inventPosY >= 5)
            {
                return true;
            }

            return _ignoredCells[inventPosY, inventPosX] != 0; //No need to check all item size
        }

        private FilterResult CheckFilters(ItemData itemData)
        {
            foreach (var filter in _customFilters)
            {
                if (!filter.AllowProcess)
                {
                    continue;
                }

                if (filter.CompareItem(itemData))
                {
                    return new FilterResult(filter.StashIndexNode, itemData);
                }
            }

            return null;
        }

        private void DropToStash()
        {
            var cursorPosPreMoving = Mouse.GetCursorPosition();

            if (Settings.BlockInput.Value)
            {
                WinApi.BlockInput(true);
            }

            if (_dropItems.Count > 0)
            {
                var sortedByStash = (from itemResult in _dropItems
                    group itemResult by itemResult.StashIndex
                    into groupedDemoClass
                    select groupedDemoClass).ToDictionary(gdc => gdc.Key, gdc => gdc.ToList());

                var latency = (int) GameController.Game.IngameState.CurLatency + Settings.ExtraDelay;

                Keyboard.KeyDown(Keys.LControlKey);
                Thread.Sleep(INPUT_DELAY);

                foreach (var stashResults in sortedByStash)
                {
                    if (!SwitchToTab(stashResults.Key))
                    {
                        continue;
                    }

                    foreach (var stashResult in stashResults.Value)
                    {
                        Mouse.SetCursorPosAndLeftClick(stashResult.ClickPos + _clickWindowOffset,
                            Settings.ExtraDelay);
                        Thread.Sleep(latency);
                    }

                    FRSM_UpdateStashesEvent();
                }

                Keyboard.KeyUp(Keys.LControlKey);
                FRSM_UpdateItemsSetsInfoEvent();
            }

            ProcessRefills();
            Mouse.SetCursorPos(cursorPosPreMoving.X, cursorPosPreMoving.Y);

            if (Settings.BlockInput.Value)
            {
                WinApi.BlockInput(false);
                Keyboard.KeyUp(Settings.DropHotkey.Value);
                Thread.Sleep(INPUT_DELAY);
            }
        }

        #region Refill

        private void ProcessRefills()
        {
            if (!Settings.RefillCurrency.Value || _customRefills.Count == 0)
            {
                return;
            }
            if (Settings.CurrencyStashTab.Index == -1)
            {
                LogError("Can't process refill: CurrencyStashTab is not set.", 5);
                return;
            }

            var delay = (int) GameController.Game.IngameState.CurLatency + Settings.ExtraDelay.Value;
            var currencyTabVisible = false;

            var inventory = GameController.Game.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory];
            var stashItems = inventory.VisibleInventoryItems;

            if (stashItems == null)
            {
                LogError("Can't process refill: VisibleInventoryItems is null!", 5);
                return;
            }

            _customRefills.ForEach(x => x.Clear());

            var filledCells = new int[5, 12];

            foreach (var inventItem in stashItems)
            {
                var item = inventItem.Item;
                if (item == null)
                {
                    continue;
                }

                if (!Settings.AllowHaveMore.Value)
                {
                    var iPosX = inventItem.InventPosX;
                    var iPosY = inventItem.InventPosY;
                    var iBase = item.GetComponent<Base>();

                    for (var x = iPosX; x <= iPosX + iBase.ItemCellsSizeX - 1; x++)
                    {
                        for (var y = iPosY; y <= iPosY + iBase.ItemCellsSizeY - 1; y++)
                        {
                            if (x >= 0 && x <= 11 && y >= 0 && y <= 4)
                            {
                                filledCells[y, x] = 1;
                            }
                            else
                            {
                                LogMessage($"Out of range: {x} {y}", 10);
                            }
                        }
                    }
                }

                if (!item.HasComponent<Stack>())
                {
                    continue;
                }

                foreach (var refill in _customRefills)
                {
                    //if (refill.AmountOption.Value == 0) continue;

                    var bit = GameController.Files.BaseItemTypes.Translate(item.Path);
                    if (bit.BaseName != refill.CurrencyClass)
                    {
                        continue;
                    }

                    var stack = item.GetComponent<Stack>();
                    refill.OwnedCount = stack.Size;
                    refill.ClickPos = inventItem.GetClientRect().Center;

                    if (refill.OwnedCount < 0 || refill.OwnedCount > 40)
                    {
                        LogError(
                            $"Ignoring refill: {refill.CurrencyClass}: Stacksize {refill.OwnedCount} not in range 0-40 ",
                            5);
                        refill.OwnedCount = -1;
                    }
                    break;
                }
            }

            var inventoryRec = inventory.InventoryUiElement.GetClientRect();
            var cellSize = inventoryRec.Width / 12;


            var freeCellFound = false;
            var freeCelPos = new Point();

            if (!Settings.AllowHaveMore.Value)
            {
                for (var x = 0; x <= 11; x++)
                {
                    for (var y = 0; y <= 4; y++)
                    {
                        if (filledCells[y, x] != 0)
                        {
                            continue;
                        }

                        freeCellFound = true;
                        freeCelPos = new Point(x, y);
                        break;
                    }
                    if (freeCellFound)
                    {
                        break;
                    }
                }
            }

            foreach (var refill in _customRefills)
            {
                if (refill.OwnedCount == -1)
                {
                    continue;
                }

                if (refill.OwnedCount == refill.AmountOption.Value)
                {
                    continue;
                }
                if (refill.OwnedCount < refill.AmountOption.Value)

                    #region Refill

                {
                    if (!currencyTabVisible)
                    {
                        if (!SwitchToTab(Settings.CurrencyStashTab.Index))
                        {
                            continue;
                        }
                        currencyTabVisible = true;
                        Thread.Sleep(delay);
                    }

                    var moveCount = refill.AmountOption.Value - refill.OwnedCount;

                    var currStashItems = GameController.Game.IngameState.ServerData.StashPanel.VisibleStash
                        .VisibleInventoryItems;
                    var foundSourceOfRefill = currStashItems
                        .Where(x => GameController.Files.BaseItemTypes.Translate(x.Item.Path).BaseName ==
                                    refill.CurrencyClass).ToList();

                    foreach (var sourceOfRefill in foundSourceOfRefill)
                    {
                        var stackSize = sourceOfRefill.Item.GetComponent<Stack>().Size;
                        var getCurCount = moveCount > stackSize ? stackSize : moveCount;

                        var destination = refill.ClickPos;

                        if (refill.OwnedCount == 0)
                        {
                            destination = GetInventoryClickPosByCellIndex(inventory, refill.InventPos.X,
                                refill.InventPos.Y, cellSize);

                            // If cells is not free then continue.
                            if (GameController.Game.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory]
                                    [refill.InventPos.X, refill.InventPos.Y, 12] != null)
                            {
                                moveCount--;
                                LogMessage(
                                    $"Inventoy ({refill.InventPos.X}, {refill.InventPos.Y}) is occupied by the wrong item!",
                                    5);
                                continue;
                            }
                        }

                        SplitStack(moveCount, sourceOfRefill.GetClientRect().Center, destination);
                        moveCount -= getCurCount;

                        if (moveCount == 0)
                        {
                            break;
                        }
                    }

                    if (moveCount > 0)
                    {
                        LogMessage(
                            $"Not enough currency (need {moveCount} more) to fill {refill.CurrencyClass} stack", 5);
                    }
                }

                #endregion

                else if (!Settings.AllowHaveMore.Value && refill.OwnedCount > refill.AmountOption.Value)

                    #region Devastate

                {
                    if (!freeCellFound)
                    {
                        LogMessage("Can\'t find free cell in player inventory to move excess currency.", 5);
                        continue;
                    }

                    if (!currencyTabVisible)
                    {
                        if (!SwitchToTab(Settings.CurrencyStashTab.Index))
                        {
                            continue;
                        }
                        currencyTabVisible = true;
                        Thread.Sleep(delay);
                    }

                    var destination = GetInventoryClickPosByCellIndex(inventory, freeCelPos.X, freeCelPos.Y, cellSize) +
                                      _clickWindowOffset;
                    var moveCount = refill.OwnedCount - refill.AmountOption.Value;

                    Thread.Sleep(delay);
                    SplitStack(moveCount, refill.ClickPos, destination);
                    Thread.Sleep(delay);

                    Keyboard.KeyDown(Keys.LControlKey);
                    Mouse.SetCursorPosAndLeftClick(destination + _clickWindowOffset, Settings.ExtraDelay.Value);
                    Keyboard.KeyUp(Keys.LControlKey);

                    Thread.Sleep(delay);
                }

                #endregion
            }
        }

        private Vector2 GetInventoryClickPosByCellIndex(Inventory inventory, int indexX, int indexY, float cellSize)
        {
            return inventory.InventoryUiElement.GetClientRect().TopLeft +
                   new Vector2(cellSize * (indexX + 0.5f), cellSize * (indexY + 0.5f));
        }

        private void SplitStack(int amount, Vector2 from, Vector2 to)
        {
            var delay = (int) GameController.Game.IngameState.CurLatency * 2 + Settings.ExtraDelay;

            Keyboard.KeyDown(Keys.ShiftKey);

            while (!Keyboard.IsKeyDown((int) Keys.ShiftKey))
            {
                Thread.Sleep(WHILE_DELAY);
            }

            Mouse.SetCursorPosAndLeftClick(from + _clickWindowOffset, Settings.ExtraDelay.Value);
            Thread.Sleep(INPUT_DELAY);
            Keyboard.KeyUp(Keys.ShiftKey);
            Thread.Sleep(delay + 50);
            if (amount > 40)
            {
                LogMessage("Can't select amount more than 40, current value: " + amount, 5);
                amount = 40;
            }

            if (amount < 10)
            {
                var keyToPress = (int) Keys.D0 + amount;
                Keyboard.KeyPress((Keys) keyToPress);
            }
            else
            {
                var keyToPress = (int) Keys.D0 + amount / 10;
                Keyboard.KeyPress((Keys) keyToPress);
                Thread.Sleep(delay);
                keyToPress = (int) Keys.D0 + amount % 10;
                Keyboard.KeyPress((Keys) keyToPress);
            }
            Thread.Sleep(delay);
            Keyboard.KeyPress(Keys.Enter);
            Thread.Sleep(delay + 50);

            Mouse.SetCursorPosAndLeftClick(to + _clickWindowOffset, Settings.ExtraDelay.Value);
            Thread.Sleep(delay + 50);
        }

        #endregion

        #region Switching between StashTabs

        public bool SwitchToTab(int tabIndex)
        {
            var latency = (int) GameController.Game.IngameState.CurLatency;
            // We don't want to Switch to a tab that we are already on
            var openLeftPanel = GameController.Game.IngameState.IngameUi.OpenLeftPanel;
            try
            {
                var stashTabToGoTo =
                    GameController.Game.IngameState.ServerData.StashPanel.GetStashInventoryByIndex(tabIndex)
                        .InventoryUiElement;

                if (stashTabToGoTo.IsVisible)
                {
                    return true;
                }
            }
            catch
            {
                // Nothing to see here officer.
            }

            // We want to maximum wait 20 times the Current Latency before giving up in our while loops.
            var maxNumberOfTries = latency * 20 > 2000 ? latency * 20 / WHILE_DELAY : 2000 / WHILE_DELAY;

            if (tabIndex > 30)
            {
                return SwitchToTabViaArrowKeys(tabIndex);
            }

            var stashPanel = GameController.Game.IngameState.ServerData.StashPanel;
            try
            {
                // Obs, this method only works with 31 stashtabs on 1920x1080, since you have to scroll at 32 tabs, and the frame stays in place.
                var viewAllTabsButton = GameController.Game.IngameState.ServerData.StashPanel.ViewAllStashButton;

                if (stashPanel.IsVisible && !viewAllTabsButton.IsVisible)
                {
                    // The user doesn't have a view all tabs button, eg. 4 tabs.
                    return SwitchToTabViaArrowKeys(tabIndex);
                }

                var parent = openLeftPanel.Children[2].Children[0].Children[1].Children[3];
                var dropDownTabElements = parent.Children[2];

                var totalStashes = GameController.Game.IngameState.ServerData.StashPanel.TotalStashes;
                if (totalStashes > 30)
                {
                    dropDownTabElements = parent.Children[1];
                }

                if (!dropDownTabElements.IsVisible)
                {
                    var pos = viewAllTabsButton.GetClientRect();
                    Mouse.SetCursorPosAndLeftClick(pos.Center + _clickWindowOffset, Settings.ExtraDelay);

                    var brCounter = 0;

                    while (!dropDownTabElements.IsVisible)
                    {
                        Thread.Sleep(WHILE_DELAY);

                        if (brCounter++ <= maxNumberOfTries)
                        {
                            continue;
                        }
                        LogMessage($"1. Error in SwitchToTab: {tabIndex}.", 5);
                        return false;
                    }

                    if (totalStashes > 30)
                    {
                        // TODO:Zafaar implemented something that allows us to get in contact with the ScrollBar.
                        Mouse.VerticalScroll(true, 5);
                        Thread.Sleep(latency + 50);
                    }
                }

                var tabPos = dropDownTabElements.Children[tabIndex].GetClientRect();

                Mouse.SetCursorPosAndLeftClick(tabPos.Center + _clickWindowOffset, Settings.ExtraDelay);
                Thread.Sleep(latency);
            }
            catch (Exception e)
            {
                LogError($"Error in GoToTab {tabIndex}: {e.Message}", 5);
                return false;
            }

            Inventory stash;

            var counter = 0;

            do
            {
                Thread.Sleep(WHILE_DELAY);
                stash = stashPanel.VisibleStash;

                if (counter++ <= maxNumberOfTries)
                {
                    continue;
                }
                LogMessage("2. Error opening stash: " + tabIndex, 5);
                return false;
            } while (stash?.VisibleInventoryItems == null);
            return true;
        }

        private bool SwitchToTabViaArrowKeys(int tabIndex)
        {
            var latency = (int) GameController.Game.IngameState.CurLatency;
            var indexOfCurrentVisibleTab = GetIndexOfCurrentVisibleTab();
            var difference = tabIndex - indexOfCurrentVisibleTab;
            var negative = difference < 0;

            for (var i = 0; i < Math.Abs(difference); i++)
            {
                Keyboard.KeyPress(negative ? Keys.Left : Keys.Right);
                Thread.Sleep(latency);
            }

            return true;
        }

        private int GetIndexOfCurrentVisibleTab()
        {
            var openLeftPanel = GameController.Game.IngameState.IngameUi.OpenLeftPanel;
            var totalStashes = GameController.Game.IngameState.ServerData.StashPanel.TotalStashes;

            for (var i = 0; i < totalStashes; i++)
            {
                var stashTabToGoTo = openLeftPanel
                    .Children[2]
                    .Children[0]
                    .Children[1]
                    .Children[1]
                    .Children[i]
                    .Children[0];

                if (stashTabToGoTo.IsVisible)
                {
                    return i;
                }
            }

            return -1;
        }

        #endregion

        #region Stashes update

        private void OnSettingsStashNameChanged(ListIndexNode node, string newValue)
        {
            node.Index = GetInventIndexByStashName(newValue);
        }

        public override void OnClose()
        {
            CloseThreads();
        }

        private void SetupOrClose()
        {
            if (!Settings.Enable.Value)
            {
                CloseThreads();
                return;
            }

            SaveDefaultConfigsToDisk();

            _settingsListNodes = new List<ListIndexNode>();

            LoadCustomRefills();
            LoadCustomFilters();

            var names = GameController.Game.IngameState.ServerData.StashPanel.AllStashNames;
            UpdateStashNames(names);

            foreach (var lOption in _settingsListNodes)
            {
                var option = lOption; //Enumerator delegate fix
                option.OnValueSelected += delegate (string newValue) { OnSettingsStashNameChanged(option, newValue); };
            }

            LoadIgnoredCells();

            _tabNamesUpdaterThread = new Thread(StashTabNamesUpdater_Thread);
            _tabNamesUpdaterThread.Start();

            foreach (var plugin in PoeHUD.Hud.PluginExtension.PluginExtensionPlugin.Plugins)
            {
                var pluginType = plugin.GetType();
                if (pluginType.Name == "FullRareSetManager")
                {
                    FRSM_Instance = plugin;
                    var FRSM_UpdateStashesMInfo = pluginType.GetMethod("UpdateStashes", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    var FRSM_UpdateItemsSetsInfoMInfo = pluginType.GetMethod("UpdateItemsSetsInfo", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                    if (FRSM_UpdateStashesMInfo != null)
                        FRSM_UpdateStashesEvent = delegate
                        {
                            try
                            {
                                Thread.Sleep(200);//Sometimes it miss the last item after drop->UpdateStashes
                                FRSM_UpdateStashesMInfo.Invoke(FRSM_Instance, new object[0]);
                            }
                            catch (Exception ex)
                            {
                                LogError("Error while invoking RareSetManager->UpdateStashes: " + ex.Message, 5);
                            }
                        };

                    if (FRSM_UpdateItemsSetsInfoMInfo != null)
                        FRSM_UpdateItemsSetsInfoEvent = delegate
                        {
                            try
                            {
                                //Thread.Sleep(200);    //Not sure here should be any delay, should work without it
                                FRSM_UpdateItemsSetsInfoMInfo.Invoke(FRSM_Instance, new object[0]);
                            }
                            catch (Exception ex)
                            {
                                LogError("Error while invoking RareSetManager->UpdateItemsSetsInfo: " + ex.Message, 5);
                            }
                        };

                    break;
                }
            }
        }

        private int GetInventIndexByStashName(string name)
        {
            var index = _renamedAllStashNames.IndexOf(name);
            if (index != -1)
            {
                index--;
            }
            return index;
        }

        private List<string> _renamedAllStashNames;

        private void UpdateStashNames(List<string> newNames)
        {
            Settings.AllStashNames = newNames;
            _renamedAllStashNames = new List<string> {"Ignore"};

            for (var i = 0; i < Settings.AllStashNames.Count; i++)
            {
                var realStashName = Settings.AllStashNames[i];

                if (_renamedAllStashNames.Contains(realStashName))
                {
                    realStashName += " (" + i + ")";
#if DebugMode
                    LogMessage("Stashie: fixed same stash name to: " + realStashName, 3);
#endif
                }

                _renamedAllStashNames.Add(realStashName);
            }

            Settings.AllStashNames.Insert(0, "Ignore");

            foreach (var lOption in _settingsListNodes)
            {
                lOption.SetListValues(_renamedAllStashNames);

                var inventoryIndex = GetInventIndexByStashName(lOption.Value);

                if (inventoryIndex == -1) //If the value doesn't exist in list (renamed)
                {
                    if (lOption.Index != -1) //If the value doesn't exist in list and the value was not Ignore
                    {
#if DebugMode
                        LogMessage(
                            "Tab renamed : " + lOption.Value + " to " + _renamedAllStashNames[lOption.Index + 1], 5);
#endif

                        if (lOption.Index >= _renamedAllStashNames.Count)
                        {
                            lOption.Index = -1;
                            lOption.Value = _renamedAllStashNames[0];
                        }
                        else
                        {
                            lOption.Value = _renamedAllStashNames[lOption.Index + 1]; //    Just update it's name
                        }
                    }
                    else
                    {
                        lOption.Value =
                            _renamedAllStashNames[0]; //Actually it was "Ignore", we just update it (can be removed)
                    }
                }
                else //tab just change it's index
                {
#if DebugMode
                    if (lOption.Index != inventoryIndex)
                    {
                        LogMessage("Tab moved: " + lOption.Index + " to " + inventoryIndex, 5);
                    }
#endif
                    lOption.Index = inventoryIndex;
                    lOption.Value = _renamedAllStashNames[inventoryIndex + 1];
                }
            }
        }

        private void CloseThreads()
        {
            if (_tabNamesUpdaterThread != null && _tabNamesUpdaterThread.IsAlive)
            {
                _tabNamesUpdaterThread.IsBackground = true;
            }
        }

        public void StashTabNamesUpdater_Thread()
        {
            while (!_tabNamesUpdaterThread.IsBackground)
            {
                if (!GameController.Game.IngameState.InGame)
                {
                    Thread.Sleep(500);
                    continue;
                }

                var stashPanel = GameController.Game.IngameState.ServerData.StashPanel;
                if (!stashPanel.IsVisible)
                {
                    Thread.Sleep(500);
                    continue;
                }

                var cachedNames = Settings.AllStashNames;
                var realNames = stashPanel.AllStashNames;

                if (realNames.Count + 1 != cachedNames.Count)
                {
                    UpdateStashNames(realNames);
                    continue;
                }

                for (var index = 0; index < realNames.Count; ++index)
                {
                    var cachedName = cachedNames[index + 1];
                    if (cachedName.Equals(realNames[index]))
                    {
                        continue;
                    }

                    UpdateStashNames(realNames);
                    break;
                }

                Thread.Sleep(300);
            }

            _tabNamesUpdaterThread.Interrupt();
        }

        #endregion
    }
}