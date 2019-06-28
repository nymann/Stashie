using ImGuiNET;
using PoeHUD.Controllers;
using PoeHUD.Hud.Menu.SettingsDrawers;
using PoeHUD.Hud.PluginExtension;
using PoeHUD.Hud.Settings;
using PoeHUD.Hud.UI;
using PoeHUD.Models.Enums;
using PoeHUD.Models.Interfaces;
using PoeHUD.Plugins;
using PoeHUD.Poe.Components;
using PoeHUD.Poe.Elements;
using PoeHUD.Poe.RemoteMemoryObjects;
using SharpDX;
using Stashie.Filters;
using Stashie.Settings;
using Stashie.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using SharpDX.Direct3D9;
using System.Diagnostics;

namespace Stashie
{
    public class StashieCore : BaseSettingsPlugin<StashieSettings>
    {
        private const string FITERS_CONFIG_FILE = "FitersConfig.txt";

        private string RefillCurrencyTypesCfg => Path.Combine(PluginDirectory, "RefillCurrencyTypes.txt");

        private const int WHILE_DELAY = 5;
        private const int INPUT_DELAY = 15;
        private MethodInfo _callPluginEventMethod;
        private Vector2 _windowOffset;
        private List<string> RefillCurrencyNames = new List<string>();
        private List<CustomFilter> _customFilters;
        // Container for items that we either want to stash into specific tabs or simply drop into whatever window is open
        private List<FilterResult> _dropItems;
        private IngameState _ingameState;
        private bool _playerHasDropdownMenu;
        private Random _random;
        private bool _dumpToStash = true;

        public StashieCore()
        {
            PluginName = "Stashie";
        }

        public override void Initialise()
        {
            _random = new Random(DateTime.Today.Millisecond);
            _callPluginEventMethod = typeof(PluginExtensionPlugin).GetMethod("CallPluginEvent");
            _ingameState = GameController.Game.IngameState;

            SaveDefaultConfigsToDisk();

            var filtersLines = File.ReadAllLines(Path.Combine(PluginDirectory, FITERS_CONFIG_FILE));
            _customFilters = FilterParser.Parse(filtersLines);

            CheckRefillCurrencyTypes();
            _playerHasDropdownMenu = _ingameState.ServerData.StashPanel.TotalStashes > 10;
        }

        private void CheckRefillCurrencyTypes()
        {
            RefillCurrencyNames.Clear();

            if (File.Exists(RefillCurrencyTypesCfg))
            {
                var lines = File.ReadAllLines(RefillCurrencyTypesCfg);
                RefillCurrencyNames.AddRange(lines);
            }
            else
            {
                var bit = GameController.Files.BaseItemTypes.contents.Values.Where(x =>
                    x.ClassName == "StackableCurrency" &&
                    !x.BaseName.Contains("Essence") &&
                    !x.BaseName.Contains("Splinter") &&
                    !x.BaseName.Contains("Blessing") &&
                    !x.BaseName.Contains("Enchant") &&
                    !x.BaseName.Contains("Unshaping Orb") &&
                    !x.BaseName.Contains("Harbinger") &&
                    !x.BaseName.Contains("Engineer") &&
                    !x.BaseName.Contains("Ancient") &&
                    !x.BaseName.Contains("Imprint") &&
                    !x.BaseName.Contains("Fragment") &&
                    !x.BaseName.Contains("Perandus Coin") &&
                    !x.BaseName.Contains("Seal") &&
                    !x.BaseName.Contains("Event Coin") &&
                    !x.BaseName.Contains("Stacked Deck") &&
                    !x.BaseName.Contains("Mirror of Kalandra") &&
                    !x.BaseName.Contains("Shard")
                ).Select(y => y.BaseName).ToArray();

                RefillCurrencyNames.AddRange(bit);
                File.WriteAllLines(RefillCurrencyTypesCfg, bit);
            }

            RefillCurrencyNames =
                RefillCurrencyNames.Where(x => !string.IsNullOrEmpty(x.Trim())).ToList(); //remove empty lines
            RefillCurrencyNames.Sort();
        }

        private static void CreateFileAndAppendTextIfItDoesNotExitst(string path, string content)
        {
            if (File.Exists(path))
                return;

            using (var streamWriter = new StreamWriter(path, true))
            {
                streamWriter.Write(content);
            }
        }

        private void SaveDefaultConfigsToDisk()
        {
            var path = $"{PluginDirectory}\\GitUpdateConfig.txt";
            const string gitUpdateConfig = "Owner:nymann\r\n" +
                                           "Name:Stashie\r\n" +
                                           "Release\r\n";
            CreateFileAndAppendTextIfItDoesNotExitst(path, gitUpdateConfig);

            path = $"{PluginDirectory}\\FitersConfig.txt";

            #region default config

            const string filtersConfig =
                "//FilterName(menu name):\tfilters\t\t:ParentMenu(optionaly, will be created automatially for grouping)\r\n" +
                "//Filter parts should divided by coma or | (for OR operation(any filter part can pass))\r\n" +
                "\r\n" +
                "////////////\tAvailable properties:\t/////////////////////\r\n" +
                "/////////\tString (name) properties:\r\n//classname\r\n" +
                "//basename\r\n" +
                "//path\r\n" +
                "/////////\tNumerical properties:\r\n" +
                "//itemquality\r\n" +
                "//rarity\r\n" +
                "//ilvl\r\n" +
                "/////////\tBoolean properties:\r\n" +
                "//identified\r\n" +
                "//Shaper\r\n" +
                "//Elder\r\n" +
                "//corrupted\r\n" +
                "/////////////////////////////////////////////////////////////\r\n" +
                "////////////\tAvailable operations:\t/////////////////////\r\n" +
                "/////////\tString (name) operations:\r\n" +
                "//!=\t(not equal)\r\n//=\t\t(equal)\r\n" +
                "//^\t\t(contains)\r\n//!^\t(not contains)\r\n" +
                "/////////\tNumerical operations:\r\n" +
                "//!=\t(not equal)\r\n//=\t\t(equal)\r\n" +
                "//>\t\t(bigger)\r\n//<\t\t(less)\r\n" +
                "//<=\t(less or equal)\r\n" +
                "//>=\t(greater or equal)\r\n" +
                "/////////\tBoolean operations:\r\n" +
                "//!\t\t(not/invert)\r\n" +
                "/////////////////////////////////////////////////////////////\r\n" +
                "\r\n" +
                "//Default Tabs\r\n" +
                "Currency:\t\t\tClassName=StackableCurrency,BaseName!^Remnant of Corruption,BaseName!^Essence,BaseName!^Fossil,BaseName!^Splinter of\t:Default Tabs\r\n" +
                "Divination Cards:\tClassName=DivinationCard\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t:Default Tabs\r\n" +
                "Essences:\t\t\tBaseName^Essence|BaseName^Remnant of Corruption,ClassName=StackableCurrency\t\t\t\t\t\t\t\t\t\t\t\t:Default Tabs\r\n" +
                "Splinters:\t\t\tBaseName^Splinter of,ClassName=StackableCurrency\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t:Default Tabs\r\n" +
                "Gems:\t\t\t\tClassName^Skill Gem,ItemQuality=0,!corrupted\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t:Default Tabs\r\n" +
                "Vaal Gems:\t\t\tClassName^Skill Gem,corrupted\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t:Default Tabs\r\n" +
                "Abyss Jewels:\t\tClassName=AbyssJewel\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t:Default Tabs\r\n" +
                "Jewels:\t\t\t\tClassName=Jewel\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t:Default Tabs\r\n" +
                "Flasks:\t\t\t\tClassName^Flask,ItemQuality=0\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t:Default Tabs\r\n" +
                "\r\n" +
                "//Delve Items\r\n" +
                "Fossils:\t\t\tClassName=StackableCurrency,BaseName^Fossil\t\t\t\t\t\t:Delve Items\r\n" +
                "Resonators:\t\t\tClassName=DelveSocketableCurrency\t\t\t\t\t\t\t\t:Delve Items\r\n" +
                "\r\n" +
                "//Chance Items\r\n" +
                "Sorcerer Boots:\t\tBaseName=Sorcerer Boots,Rarity=Normal\t:Chance Items\r\n" +
                "Leather Belt:\t\tBaseName=Leather Belt,Rarity=Normal\t\t:Chance Items\r\n" +
                "\r\n" +
                "//Vendor Recipes\r\n" +
                "Chisel Recipe 20Q:\tBaseName=Stone Hammer|BaseName=Rock Breaker|BaseName=Gavel,ItemQuality=20\t:Vendor Recipes\r\n" +
                "Chisel Recipe 0Q:\tBaseName=Stone Hammer|BaseName=Rock Breaker|BaseName=Gavel,Rarity=Normal\t:Vendor Recipes\r\n" +
                "Quality Gems:\t\tClassName^Skill Gem,ItemQuality>0\t\t\t\t\t\t\t\t\t\t\t:Vendor Recipes\r\n" +
                "Quality Flasks:\t\tClassName^Flask,ItemQuality>0\t\t\t\t\t\t\t\t\t\t\t\t:Vendor Recipes\r\n" +
                "\r\n" +
                "//Chaos Recipe LVL 2 (unindentified and ilvl 60 or above)\r\n" +
                "CR-Weapons:\t\t\t!identified,Rarity=Rare,ilvl>=60,ClassName^Two Hand|ClassName^One Hand|ClassName=Bow|ClassName=Staff|ClassName=Sceptre|ClassName=Wand|ClassName=Dagger|ClassName=Claw|ClassName=Shield|ClassName=Quiver :Chaos Recipe\r\n" +
                "CR-Amulets:\t\t\t!identified,Rarity=Rare,ilvl>=60,ClassName=Amulet \t\t\t\t:Chaos Recipe\r\n" +
                "CR-Rings:\t\t\t!identified,Rarity=Rare,ilvl>=60,ClassName=Ring \t\t\t\t:Chaos Recipe\r\n" +
                "CR-Belts:\t\t\t!identified,Rarity=Rare,ilvl>=60,ClassName=Belt \t\t\t\t:Chaos Recipe\r\n" +
                "CR-Helmets:\t\t\t!identified,Rarity=Rare,ilvl>=60,ClassName=Helmet \t\t\t\t:Chaos Recipe\r\n" +
                "CR-Chests:\t\t\t!identified,Rarity=Rare,ilvl>=60,ClassName=Body Armour \t\t\t:Chaos Recipe\r\n" +
                "CR-Boots:\t\t\t!identified,Rarity=Rare,ilvl>=60,ClassName=Boots \t\t\t\t:Chaos Recipe\r\n" +
                "CR-Gloves:\t\t\t!identified,Rarity=Rare,ilvl>=60,ClassName=Gloves \t\t\t\t:Chaos Recipe\r\n" +
                "\r\n" +
                "\r\n" +
                "// Non-Chaos Recipe Rares (eg. unindentified|ilvl<60)\r\n" +
                "Idd-Weapons:\t\tidentified|ilvl<60,Rarity=Rare,ClassName^Two Hand|ClassName^One Hand|ClassName=Bow|ClassName=Staff|ClassName=Sceptre|ClassName=Wand|ClassName=Dagger|ClassName=Claw|ClassName=Shield|ClassName=Quiver :Rares\r\n" +
                "Idd-Amulets:\t\tidentified|ilvl<60,Rarity=Rare,ClassName=Amulet \t\t\t\t:Rares\r\n" +
                "Idd-Rings:\t\t\tidentified|ilvl<60,Rarity=Rare,ClassName=Ring \t\t\t\t\t:Rares\r\n" +
                "Idd-Belts:\t\t\tidentified|ilvl<60,Rarity=Rare,ClassName=Belt \t\t\t\t\t:Rares\r\n" +
                "Idd-Helms:\t\t\tidentified|ilvl<60,Rarity=Rare,ClassName=Helmet \t\t\t\t:Rares\r\n" +
                "Idd-Body Armours:\tidentified|ilvl<60,Rarity=Rare,ClassName=Body Armour \t\t\t:Rares\r\n" +
                "Idd-Boots:\t\t\tidentified|ilvl<60,Rarity=Rare,ClassName=Boots \t\t\t\t\t:Rares\r\n" +
                "Idd-Gloves:\t\t\tidentified|ilvl<60,Rarity=Rare,ClassName=Gloves \t\t\t\t:Rares\r\n" +
                "\r\n" +
                "// Craftable stuff\r\n" +
                "Craft-Weapons:\t\t\tilvl>=84,Rarity=Normal|Rarity=Magic,\tClassName^Two Hand|ClassName^One Hand|ClassName=Bow|ClassName=Staff|ClassName=Sceptre|ClassName=Wand|ClassName=Dagger|ClassName=Claw|ClassName=Shield|ClassName=Quiver :Crafting\r\n" +
                "Craft-Amulets:\t\t\tilvl>=84,Rarity=Normal|Rarity=Magic,\tClassName=Amulet \t\t:Crafting\r\n" +
                "Craft-Rings:\t\t\tilvl>=84,Rarity=Normal|Rarity=Magic,\tClassName=Ring \t\t\t:Crafting\r\n" +
                "Craft-Belts:\t\t\tilvl>=84,Rarity=Normal|Rarity=Magic,\tClassName=Belt \t\t\t:Crafting\r\n" +
                "Craft-Helms:\t\t\tilvl>=84,Rarity=Normal|Rarity=Magic,\tClassName=Helmet \t\t:Crafting\r\n" +
                "Craft-Chests:\t\t\tilvl>=84,Rarity=Normal|Rarity=Magic,\tClassName=Body Armour \t:Crafting\r\n" +
                "Craft-Boots:\t\t\tilvl>=84,Rarity=Normal|Rarity=Magic,\tClassName=Boots \t\t:Crafting\r\n" +
                "Craft-Gloves:\t\t\tilvl>=84,Rarity=Normal|Rarity=Magic,\tClassName=Gloves \t\t:Crafting\r\n" +
                "\r\n" +
                "// Uniques\r\n" +
                "Uniques-Weapons:\t\t\tRarity=Unique,\tClassName^Two Hand|ClassName^One Hand|ClassName=Bow|ClassName=Staff|ClassName=Sceptre|ClassName=Wand|ClassName=Dagger|ClassName=Claw|ClassName=Shield|ClassName=Quiver :Uniques\r\n" +
                "Uniques-Amulets:\t\t\tRarity=Unique,\tClassName=Amulet \t\t:Uniques\r\n" +
                "Uniques-Rings:\t\t\tRarity=Unique,\tClassName=Ring \t\t\t\t:Uniques\r\n" +
                "Uniques-Belts:\t\t\tRarity=Unique,\tClassName=Belt \t\t\t\t:Uniques\r\n" +
                "Uniques-Helms:\t\t\tRarity=Unique,\tClassName=Helmet \t\t\t:Uniques\r\n" +
                "Uniques-Chests:\t\t\tRarity=Unique,\tClassName=Body Armour \t\t:Uniques\r\n" +
                "Uniques-Boots:\t\t\tRarity=Unique,\tClassName=Boots \t\t\t:Uniques\r\n" +
                "Uniques-Gloves:\t\t\tRarity=Unique,\tClassName=Gloves \t\t\t:Uniques\r\n" +
                "\r\n" +
                "\r\n" +
                "// Maps & Stuff\r\n" +
                "Maps:\t\t\t\tClassName=Map\t\t\t\t\t\t\t:Maps & Stuff\r\n" +
                "Fragments:\t\t\tClassName=MapFragment\t\t\t\t\t:Maps & Stuff\r\n" +
                "Labyrinth:\t\t\tClassName=LabyrinthMapItem\t\t\t\t:Maps & Stuff";

            #endregion default config

            CreateFileAndAppendTextIfItDoesNotExitst(path, filtersConfig);
        }

        /**
         * Process inventory items with the given dropToStash directive.
         * True - drops items to stash constrained by Filter rules.
         * False - ignores filter rules.  Instead, inventory items will be collated and dropped in the open window.
         */
        private void ProcessInventoryItems(bool dropToStash)
        {
            var inventory =
                _ingameState.IngameUi.InventoryPanel[
                    InventoryIndex.PlayerInventory];

            var invItems = inventory.VisibleInventoryItems;

            if (invItems == null)
            {
                LogMessage("Player inventory->VisibleInventoryItems is null!", 5);
                return;
            }

            _dropItems = new List<FilterResult>();
            _windowOffset = GameController.Window.GetWindowRectangle().TopLeft;

            foreach (var invItem in invItems)
            {
                if (invItem.Item == null)
                    continue;

                if (string.IsNullOrEmpty(invItem.Item.Path))
                {
                    LogMessage(
                        $"Bugged item detected on X:{invItem.InventPosX}, Y:{invItem.InventPosY}, skipping.. Change location to fix this or restart the game (exit to character selection).",
                        5);
                    continue;
                }

                if (CheckIgnoreCells(invItem))
                    continue;

                var baseItemType = GameController.Files.BaseItemTypes.Translate(invItem.Item.Path);
                if (dropToStash)
                {
                    var testItem = new ItemData(invItem, baseItemType);

                    var result = CheckFilters(testItem);

                    if (result != null)
                        _dropItems.Add(result);
                }
                else
                {
                    _dropItems.Add(new FilterResult(new StashTabNode(), new ItemData(invItem, baseItemType)));
                }
            }
        }
        private bool CheckIgnoreCells(NormalInventoryItem inventItem)
        {
            var inventPosX = inventItem.InventPosX;
            var inventPosY = inventItem.InventPosY;

            if (Settings.RefillCurrency.Value &&
                Settings.Refills.Any(x =>
                    (int) x.InventPosX.Value == inventPosX && (int) x.InventPosY.Value == inventPosY))
                return true;

            if (inventPosX < 0 || inventPosX >= 12)
                return true;
            if (inventPosY < 0 || inventPosY >= 5)
                return true;

            return Settings.IgnoredCells[inventPosY, inventPosX] != 0; //No need to check all item size
        }

        private FilterResult CheckFilters(ItemData itemData)
        {
            foreach (var filter in _customFilters)
            {
                if (!filter.AllowProcess)
                    continue;

                if (filter.CompareItem(itemData))
                    return new FilterResult(filter.StashIndexNode, itemData);
            }

            return null;
        }

        private void DumpToWindow(Func<bool> validState)
        {
            var items = _dropItems.Select(filter => filter.ClickPos);
            Keyboard.KeyDown(Keys.LControlKey);
            try
            {
                Thread.Sleep(GetLatency(true) + Settings.ExtraDelay.Value);
                foreach (var vec in items)
                {
                    try
                    {
                        Mouse.SetCursorPosAndLeftClick(vec, Settings.ExtraDelay, _windowOffset);
                        Thread.Sleep(GetLatency(true) + Settings.ExtraDelay.Value);
                    }
                    catch (Exception e)
                    {
                        LogError($"{e}. Could not move the item {vec.ToString()}", 1);
                    }
                }
            }
            catch (Exception vectorize)
            {
                LogError($"{vectorize} Failed to project player inventory to Vector positions", 2);
            }
            finally
            {
                Keyboard.KeyUp(Keys.LControlKey);
            }
        }

        private void DropToStash(Func<bool> validState)
        {
            if (Settings.BlockInput.Value)
                WinApi.BlockInput(true);

            if (_dropItems.Count > 0)
            {
                // Dictionary where key is the index (stashtab index) and Value is the items to drop.
                var itemsToDrop = (from dropItem in _dropItems
                                   group dropItem by dropItem.StashNode.VisibleIndex
                    into itemsToDropByTab
                                   select itemsToDropByTab).ToDictionary(tab => tab.Key, tab => tab.ToList());

                var stashPanel = IngameState.IngameUi.StashElement;

                foreach (var stashResults in itemsToDrop)
                {
                    if (!SwitchToTab(stashResults.Value[0].StashNode, validState))
                        continue;
                    Keyboard.KeyDown(Keys.LControlKey);
                    try
                    {
                        Thread.Sleep(GetLatency(true) + Settings.ExtraDelay.Value);

                        foreach (var stashResult in stashResults.Value)
                        {
                            if (!stashPanel.IsVisible)
                            {
                                break;
                            }

                            Mouse.SetCursorPosAndLeftClick(stashResult.ClickPos, Settings.ExtraDelay, _windowOffset);
                            Thread.Sleep(GetLatency(true) + Settings.ExtraDelay.Value);
                        }
                    }
                    finally
                    {
                        Keyboard.KeyUp(Keys.LControlKey);
                    }

                    if (!stashPanel.IsVisible)
                    {
                        break;
                    }

                    // QVIN's version of Hud doesn't support Subscription events, so we use reflection.
                    if (_callPluginEventMethod != null)
                    {
                        _callPluginEventMethod.Invoke(API, new object[] {"StashUpdate", new object[0]});
                    }
                }

                Keyboard.KeyUp(Keys.LControlKey);
            }

            ProcessRefills(validState);

            // TODO:Go back to a specific tab, if user has that setting enabled.
            if (Settings.VisitTabWhenDone.Value)
                SwitchToTab(Settings.TabToVisitWhenDone, validState, false);

            if (Settings.BlockInput.Value)
            {
                WinApi.BlockInput(false);
                Thread.Sleep(INPUT_DELAY);
            }
        }

        /**
         * Get current latency an add some entropy to help deter possible "correlation attacks" by GGG.
         */
        private int GetLatency(bool addRand) => addRand ? (int)_ingameState.CurLatency :
            (int)_ingameState.CurLatency + _random.Next(((int)_ingameState.CurLatency)<<2);

        public override void Render()
        {
            try
            {
                if (!Settings.Enable)
                    return;

                var cursorPosPreMoving = Mouse.GetCursorPosition();
                try
                {
                    Func<bool> inventoryOpen = () => { return _ingameState.IngameUi.InventoryPanel.IsVisible; };
                    Func<bool> stashOpen = () => { return _ingameState.ServerData.StashPanel.IsVisible; };

                    if (inventoryOpen())
                    {
#if DEBUG_DEEP
                            var dropdownMenu = _ingameState.ServerData.StashPanel.ViewAllStashPanel;
                            if (dropdownMenu.IsVisible)
                            {
                                for (var index = 0; index < dropdownMenu.Children.Count; index++)
                                {
                                    var dropdown = dropdownMenu.Children[index];
                                    for (var i = 0; i < dropdown.Children.Count; i++)
                                    {
                                        var child = dropdown.Children[i];
                                        Graphics.DrawBox(child.GetClientRect(), Color.DarkRed);
                                        var pos2 = new Vector2(child.GetClientRect().Center.X, child.GetClientRect().Top);
                                        Graphics.DrawText($"[{index}][{i}]", 20, pos2, Color.White, FontDrawFlags.Center);
                                    }
                                }
                            }
#endif
                        if (!Keyboard.IsKeyToggled(Settings.DropHotkey.Value) && Settings.RequireHotkey == true)
                        {
                            // skip
                        }
                        else
                        {
                            ProcessInventoryItems(_ingameState.ServerData.StashPanel.IsVisible);

                            if (_dropItems.Count == 0)
                            {
                                ProcessRefills(() => inventoryOpen() && stashOpen());
                            }
                            else
                            {
                                if (stashOpen())
                                {
                                    DropToStash(() => inventoryOpen() && stashOpen());
                                }
                                else if (GameController.Game.IngameState.UIRoot.Children?[1].Children?[74]?.IsVisible ?? false )
                                {
                                    // A left panel window (NPC, player, etc...) other than the stash is open...
                                    // just dump the items and ignore filter rules.
                                    DumpToWindow(inventoryOpen);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    Mouse.SetCursorPos(cursorPosPreMoving.X, cursorPosPreMoving.Y);
                    if (Keyboard.IsKeyToggled(Settings.DropHotkey.Value))
                    {
                        // This effectively "swallows" the configured hotkey making exclusive use of it...is that ok?  Should we just allow
                        // the key to pass in order to allow other plugins to use the key? ie use the hotkey when iventory isn't open?
                        LogWarning($"{Settings.DropHotkey.Value} key is toggled, disabling it.", 5);
                        Keyboard.KeyPress(Settings.DropHotkey.Value);
                    }
                }
            }
            catch(Exception catchAll)
            {
                LogError($"{catchAll} general failure in Render.", 5);
            }
        }

#region Loads

        public override void InitializeSettingsMenu()
        {
            base.InitializeSettingsMenu();
            GenerateStashieSettingsMenu();
        }

        private BaseSettingsDrawer FiltersMenuRootMenu;
        private BaseSettingsDrawer RefillMenuRootMenu;
        private List<StashTabNode> StashTabNodes = new List<StashTabNode>(); //This is for hot reload, we will unload it

        private void GenerateStashieSettingsMenu() //Separate func cuz we can call it in anu moment to reload all menu
        {
            if (FiltersMenuRootMenu != null)
                SettingsDrawers.Remove(FiltersMenuRootMenu);
            if (RefillMenuRootMenu != null)
                SettingsDrawers.Remove(RefillMenuRootMenu);

            FiltersMenuRootMenu = new BaseSettingsDrawer("Filters", GetUniqDrawerId());
            SettingsDrawers.Add(FiltersMenuRootMenu);

            var submenu = new Dictionary<string, BaseSettingsDrawer>();
            foreach (var customFilter in _customFilters)
            {
                if (!Settings.FilterOptions.TryGetValue(customFilter.Name, out var tabNode))
                {
                    tabNode = new StashTabNode();
                    Settings.FilterOptions.Add(customFilter.Name, tabNode);
                }

                StashTabNodes.Add(tabNode);
                StashTabController.RegisterStashNode(tabNode);

                var filterParent = FiltersMenuRootMenu;
                if (!string.IsNullOrEmpty(customFilter.SubmenuName))
                {
                    if (!submenu.TryGetValue(customFilter.SubmenuName, out filterParent))
                    {
                        filterParent = new BaseSettingsDrawer(customFilter.SubmenuName, GetUniqDrawerId());
                        FiltersMenuRootMenu.Children.Add(filterParent);
                        submenu.Add(customFilter.SubmenuName, filterParent);
                    }
                }

                filterParent.Children.Add(new StashTabNodeSettingDrawer(tabNode, customFilter.Name, GetUniqDrawerId()));
                customFilter.StashIndexNode = tabNode;
            }

            RefillMenuRootMenu =
                new CheckboxSettingDrawer(Settings.RefillCurrency, "Refill Currency", GetUniqDrawerId());
            SettingsDrawers.Add(RefillMenuRootMenu);

            RefillMenuRootMenu.Children.Add(new StashTabNodeSettingDrawer(Settings.CurrencyStashTab, "Currency Tab",
                GetUniqDrawerId()));
            StashTabController.RegisterStashNode(Settings.CurrencyStashTab);
            RefillMenuRootMenu.Children.Add(new CheckboxSettingDrawer(Settings.AllowHaveMore, "Allow Have More",
                GetUniqDrawerId()));

            var refillRoot = new BaseSettingsDrawer("Refills:", GetUniqDrawerId());
            RefillMenuRootMenu.Children.Add(refillRoot);

            var addTabButton = new ButtonNode();
            var addTabButtonDrawer = new ButtonSettingDrawer(addTabButton, "Add Refill", GetUniqDrawerId());
            RefillMenuRootMenu.Children.Add(addTabButtonDrawer);

            addTabButton.OnPressed += delegate
            {
                var newRefill = new RefillProcessor();
                AddRefill(newRefill);
                Settings.Refills.Add(newRefill);
            };

            foreach (var refill in Settings.Refills)
            {
                AddRefill(refill);
            }
        }

        public override void DrawSettingsMenu()
        {
            DrawIgnoredCellsSettings();
            base.DrawSettingsMenu();
        }

        public void SaveIgnoredSLotsFromInventoryTemplate()
        {
            Settings.IgnoredCells = new[,]
            {
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0}
            };
            try
            {
                foreach (var inventories in GameController.Game.IngameState.ServerData.PlayerInventories)
                {
                    if (inventories.Inventory.InventSlot != InventorySlotE.MainInventory1)
                        return;
                    var inventory = inventories.Inventory.InventorySlotItems;
                    foreach (var item in inventory)
                    {
                        var baseC = item.Item.GetComponent<Base>();
                        var itemSizeX = baseC.ItemCellsSizeX;
                        var itemSizeY = baseC.ItemCellsSizeY;
                        var inventPosX = item.PosX;
                        var inventPosY = item.PosY;
                        for (var y = 0; y < itemSizeY; y++)
                        {
                            for (var x = 0; x < itemSizeX; x++)
                                Settings.IgnoredCells[y + inventPosY, x + inventPosX] = 1;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogError($"{e}", 5);
            }
        }

        private void DrawIgnoredCellsSettings()
        {
            ImGuiNative.igGetContentRegionAvail(out var newcontentRegionArea);
            ImGui.BeginChild("##IgnoredCellsMain", new System.Numerics.Vector2(newcontentRegionArea.X, 183), true,
                WindowFlags.NoScrollWithMouse);
            ImGui.Text("Ignored Inventory Slots");
            ImGuiExtension.ToolTip(
                $"Checked = Item will be ignored{Environment.NewLine}UnChecked = Item will be processed");
            ImGui.Text("    ");
            ImGui.SameLine();
            ImGuiNative.igGetContentRegionAvail(out newcontentRegionArea);
            ImGui.BeginChild("##IgnoredCellsCels",
                new System.Numerics.Vector2(newcontentRegionArea.X, newcontentRegionArea.Y), true,
                WindowFlags.NoScrollWithMouse);
            try
            {
                if (ImGui.Button("Copy Inventory"))
                {
                    SaveIgnoredSLotsFromInventoryTemplate();
                }
            }
            catch (Exception e)
            {
                LogError(e, 10);
            }

            var _numb = 1;
            for (var i = 0; i < 5; i++)
            {
                for (var j = 0; j < 12; j++)
                {
                    var toggled = Convert.ToBoolean(Settings.IgnoredCells[i, j]);
                    if (ImGui.Checkbox($"##{_numb}IgnoredCells", ref toggled))
                    {
                        Settings.IgnoredCells[i, j] ^= 1;
                    }

                    if ((_numb - 1) % 12 < 11)
                    {
                        ImGui.SameLine();
                    }

                    _numb += 1;
                }
            }

            ImGui.EndChild();
            ImGui.EndChild();
        }

        private void AddRefill(RefillProcessor refill)
        {
            refill.CurrencyClass.Values = RefillCurrencyNames;

            var refillRoot = new BaseSettingsDrawer("", GetUniqDrawerId());
            RefillMenuRootMenu.Children.Insert(RefillMenuRootMenu.Children.Count - 1, refillRoot);

            refillRoot.Children.Add(new ComboBoxSettingDrawer(refill.CurrencyClass, "Currency", GetUniqDrawerId()));

            refill.Amount.Max = refill.MaxStackAmount;
            refillRoot.Children.Add(new IntegerSettingsDrawer(refill.Amount, "Amount", GetUniqDrawerId()));

            refillRoot.Children.Add(new IntegerSettingsDrawer(refill.InventPosX, "Inventory Pos X", GetUniqDrawerId()));
            refillRoot.Children.Add(new IntegerSettingsDrawer(refill.InventPosY, "Inventory Pos Y", GetUniqDrawerId()));

            var removeButton = new ButtonNode();
            var removeButtonDrawer = new ButtonSettingDrawer(removeButton, "Delete Refill", GetUniqDrawerId());
            refillRoot.Children.Add(removeButtonDrawer);

            removeButton.OnPressed += delegate
            {
                RefillMenuRootMenu.Children.Remove(refillRoot);
                Settings.Refills.Remove(refill);
            };
        }

#endregion Loads

#region Refill

        private void ProcessRefills(Func<bool> validState)
        {
            if (!Settings.RefillCurrency.Value || Settings.Refills.Count == 0)
                return;
            if (!Settings.CurrencyStashTab.Exist)
            {
                LogError("Can't process refill: CurrencyStashTab is not set.", 5);
                return;
            }

            var delay = (int) _ingameState.CurLatency + Settings.ExtraDelay.Value;
            var currencyTabVisible = false;

            var inventory = _ingameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory];
            var inventoryItems = inventory.VisibleInventoryItems;

            if (inventoryItems == null)
            {
                LogError("Can't process refill: VisibleInventoryItems is null!", 5);
                return;
            }

            Settings.Refills.ForEach(x => x.Clear());

            var filledCells = new int[5, 12];

            foreach (var inventItem in inventoryItems)
            {
                var item = inventItem.Item;
                if (item == null)
                    continue;

                if (!Settings.AllowHaveMore.Value)
                    filledCells = GetFilledCells(filledCells, inventItem, item);

                if (!item.HasComponent<Stack>())
                    continue;

                foreach (var refill in Settings.Refills)
                {
                    var bit = GameController.Files.BaseItemTypes.Translate(item.Path);
                    if (bit.BaseName != refill.CurrencyClass)
                        continue;

                    var stack = item.GetComponent<Stack>();
                    refill.OwnedCount = stack.Size;
                    refill.ClickPos = inventItem.GetClientRect().Center;

                    var maxSt = stack.Info.MaxStackSize;

                    if (maxSt > 99)
                        maxSt = 99;

                    if (refill.MaxStackAmount != maxSt)
                    {
                        LogMessage(
                            $"Fixed refill: {refill.CurrencyClass.Value} stacksize from {refill.Amount.Max} to {maxSt}.",
                            5);
                        refill.MaxStackAmount = refill.Amount.Max = maxSt;
                    }

                    break;
                }
            }

            var inventoryRec = inventory.InventoryUiElement.GetClientRect();
            var cellSize = inventoryRec.Width / 12;

            var freeCellFound = false;
            var freeCellPos = new Point();

            if (!Settings.AllowHaveMore.Value)
                FreeCellFound(filledCells, ref freeCellFound, ref freeCellPos);

            foreach (var refill in Settings.Refills)
            {
                if (refill.OwnedCount == -1)
                    continue;

                if (refill.OwnedCount == refill.Amount.Value)
                    continue;

                if (refill.OwnedCount < refill.Amount.Value)
                {
#region Refill
                    if (!currencyTabVisible)
                    {
                        if (!SwitchToTab(Settings.CurrencyStashTab, validState))
                            continue;
                        currencyTabVisible = true;
                        Thread.Sleep(delay);
                    }

                    var moveCount = refill.Amount.Value - refill.OwnedCount;

                    var currStashItems = _ingameState.ServerData.StashPanel.VisibleStash
                        .VisibleInventoryItems;
                    var foundSourceOfRefill = currStashItems?
                        .Where(x => GameController?.Files?.BaseItemTypes?.Translate(x?.Item?.Path).BaseName ==
                                    refill.CurrencyClass).ToList();

                    foreach (var sourceOfRefill in foundSourceOfRefill)
                    {
                        var stack = sourceOfRefill.Item.GetComponent<Stack>();
                        var stackSize = stack.Size;

                        if (refill.MaxStackAmount != stack.Info.MaxStackSize)
                        {
                            var maxSt = stack.Info.MaxStackSize;

                            if (maxSt > 99)
                                maxSt = 99;

                            if (refill.Amount.Max != maxSt)
                            {
                                LogMessage(
                                    $"Fixed refill: {refill.CurrencyClass.Value} stacksize from {refill.Amount.Max} to {maxSt}.",
                                    5);
                                refill.MaxStackAmount = refill.Amount.Max = maxSt;
                            }
                        }

                        var getCurCount = moveCount > stack.Size ? stack.Size : moveCount;

                        var destination = refill.ClickPos;

                        if (refill.OwnedCount == 0)
                        {
                            destination = GetInventoryClickPosByCellIndex(inventory, (int) refill.InventPosX.Value,
                                (int) refill.InventPosY.Value, cellSize);

                            // If cells is not free then continue.
                            if (_ingameState.ServerData.GetPlayerInventoryBySlot(InventorySlotE.MainInventory1)
                                    [(int) refill.InventPosX.Value, (int) refill.InventPosY.Value] != null)
                            {
                                moveCount--;
                                LogMessage(
                                    $"Inventoy ({refill.InventPosX.Value}, {refill.InventPosY.Value}) is occupied by the wrong item!",
                                    5);
                                continue;
                            }
                        }

                        SplitStack(moveCount, sourceOfRefill.GetClientRect().Center, destination, stackSize);
                        moveCount -= getCurCount;

                        if (moveCount == 0)
                            break;
                    }

                    if (moveCount > 0)
                        LogMessage(
                            $"Not enough currency (need {moveCount} more) to fill {refill.CurrencyClass.Value} stack",
                            5);

#endregion Refill
                }
                else if (!Settings.AllowHaveMore.Value && refill.OwnedCount > refill.Amount.Value)
                {
#region Remove excess items

                    if (!freeCellFound)
                    {
                        LogMessage(@"Can't find free cell in player inventory to move excess currency.", 5);
                        continue;
                    }

                    if (!currencyTabVisible)
                    {
                        if (!SwitchToTab(Settings.CurrencyStashTab, validState))
                            continue;
                        currencyTabVisible = true;
                        Thread.Sleep(delay);
                    }

                    var destination =
                        GetInventoryClickPosByCellIndex(inventory, freeCellPos.X, freeCellPos.Y, cellSize);
                    var moveCount = refill.OwnedCount - refill.Amount.Value;

                    Thread.Sleep(delay);
                    SplitStack(moveCount, refill.ClickPos, destination, 0);
                    Thread.Sleep(delay);

                    Keyboard.KeyDown(Keys.LControlKey);
                    Mouse.SetCursorPosAndLeftClick(destination, Settings.ExtraDelay.Value, _windowOffset);
                    Keyboard.KeyUp(Keys.LControlKey);

                    Thread.Sleep(delay);

#endregion Remove excess items
                }
            }
        }

        private static void FreeCellFound(int[,] filledCells, ref bool freeCellFound, ref Point freeCellPos)
        {
            for (var x = 0; x <= 11; x++)
            {
                for (var y = 0; y <= 4; y++)
                {
                    if (filledCells[y, x] != 0)
                        continue;

                    freeCellFound = true;
                    freeCellPos = new Point(x, y);
                    break;
                }

                if (freeCellFound)
                    break;
            }
        }

        private static int[,] GetFilledCells(int[,] filledCells, NormalInventoryItem inventItem, IEntity item)
        {
            var iPosX = inventItem.InventPosX;
            var iPosY = inventItem.InventPosY;
            var iBase = item.GetComponent<Base>();

            for (var x = iPosX; x <= iPosX + iBase.ItemCellsSizeX - 1; x++)
            for (var y = iPosY; y <= iPosY + iBase.ItemCellsSizeY - 1; y++)
                if (x >= 0 && x <= 11 && y >= 0 && y <= 4)
                    filledCells[y, x] = 1;
                else
                    LogMessage($"Out of range: {x} {y}", 10);

            return filledCells;
        }

        private static Vector2 GetInventoryClickPosByCellIndex(Inventory inventory, int indexX, int indexY,
            float cellSize)
        {
            return inventory.InventoryUiElement.GetClientRect().TopLeft +
                   new Vector2(cellSize * (indexX + 0.5f), cellSize * (indexY + 0.5f));
        }

        private void SplitStack(int amount, Vector2 from, Vector2 to, int staskSize)
        {
            var delay = (int) _ingameState.CurLatency * 2 + Settings.ExtraDelay;

            if (staskSize == 1)
            {
                Mouse.SetCursorPosAndLeftClick(from, Settings.ExtraDelay.Value, _windowOffset);
                Thread.Sleep(INPUT_DELAY);
                Thread.Sleep(delay + 50);
                Mouse.SetCursorPosAndLeftClick(to, Settings.ExtraDelay.Value, _windowOffset);
                Thread.Sleep(delay + 50);
            }
            else
            {
                Keyboard.KeyDown(Keys.ShiftKey);

                while (!Keyboard.IsKeyDown((int) Keys.ShiftKey))
                    Thread.Sleep(WHILE_DELAY);

                Mouse.SetCursorPosAndLeftClick(from, Settings.ExtraDelay.Value, _windowOffset);
                Thread.Sleep(INPUT_DELAY);
                Keyboard.KeyUp(Keys.ShiftKey);
                Thread.Sleep(delay + 50);
                if (amount > 99)
                {
                    LogMessage("Can't select amount more than 99, current value: " + amount, 5);
                    amount = 99;
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

                Mouse.SetCursorPosAndLeftClick(to, Settings.ExtraDelay.Value, _windowOffset);
                Thread.Sleep(delay + 50);
            }
        }

#endregion Refill

#region Switching between StashTabs

        public bool SwitchToTabViaDropdownMenu(StashTabNode tabNode, Func<bool> validState)
        {
            var latency = (int) _ingameState.CurLatency;
            var stashPanel = _ingameState.ServerData.StashPanel;
            var stashCount = 0;
            // We want to maximum wait 20 times the Current Latency before giving up in our while loops.
            var maxNumberOfTries = latency * 20 > 2000 ? latency * 20 / WHILE_DELAY : 2000 / WHILE_DELAY;
            try
            {
                var viewAllTabsButton = _ingameState.ServerData.StashPanel.ViewAllStashButton;

                if (stashPanel.IsVisible && !viewAllTabsButton.IsVisible)
                    return SwitchToTabViaArrowKeys(tabNode, validState);
                var dropdownMenu = _ingameState.ServerData.StashPanel.ViewAllStashPanel;

                Func<bool> dropDownMenuVisible = () => { return dropdownMenu.IsVisible && dropdownMenu.isHighlighted; } ;

                if (!dropDownMenuVisible())
                {
                    var pos = viewAllTabsButton.GetClientRect();
                    Mouse.SetCursorPosAndLeftClick(pos.Center, Settings.ExtraDelay, _windowOffset);

                    // Make sure that we are scrolled to the top in the menu.
                    if (_ingameState.ServerData.StashPanel.TotalStashes > 30)
                    {
                        Thread.Sleep(latency);
                        Mouse.VerticalScroll(true, 10);
                        Thread.Sleep(latency);
                        Mouse.VerticalScroll(true, 10);
                    }
                }

                // Dropdown menu have the following children: 0, 1, 2.
                // Where:
                // 0 is the icon (fx. chaos orb).
                // 1 is the name of the tab.
                // 2 is the slider.
                var slider = dropdownMenu.Children[1].ChildCount == _ingameState.ServerData.StashPanel.TotalStashes;

                var noSlider = dropdownMenu.Children[2].ChildCount == _ingameState.ServerData.StashPanel.TotalStashes;
                RectangleF tabPos;
                if (slider)
                {
                    tabPos = dropdownMenu.GetChildAtIndex(1).GetChildAtIndex(tabNode.VisibleIndex).GetClientRect();
                }
                else if (noSlider)
                {
                    tabPos = dropdownMenu.GetChildAtIndex(2).GetChildAtIndex(tabNode.VisibleIndex).GetClientRect();
                }
                else
                {
                    LogMessage("Couldn't detect steam/non-steam, contact administrator", 3);
                    return false;
                    //tabPos = dropdownMenu.GetChildAtIndex(tabNode.VisibleIndex).GetChildAtIndex(1).GetClientRect();
                }

                Mouse.SetCursorPosAndLeftClick(tabPos.Center, Settings.ExtraDelay, _windowOffset);
                Thread.Sleep(latency);
                return stashPanel.IndexVisibleStash == tabNode.VisibleIndex;
            }
            catch (Exception e)
            {
                LogError($"Error in GoToTab {tabNode.Name}: {e.Message}", 5);
                return false;
            }
        }

        public bool SwitchToTab(StashTabNode tabNode, Func<bool> validState, bool waitForTabToBeReady = true)
        {
            var stashPanel = _ingameState.ServerData.StashPanel;
            //var lastIndex = stashPanel.IndexVisibleStash;
            bool returnValue = true;
            var sw = Stopwatch.StartNew();
            Func<bool> failSafe = () =>  sw.ElapsedMilliseconds < 3000 && validState(); // sw.ElapsedMilliseconds < 1500 &&

            while (stashPanel.IndexVisibleStash != tabNode.VisibleIndex && failSafe())
            {
                if (!_playerHasDropdownMenu)
                    returnValue = SwitchToTabViaArrowKeys(tabNode, validState);
                else
                {
                    var indexOfVisibleStash = stashPanel.IndexVisibleStash;
                    var travelDistance = Math.Abs(tabNode.VisibleIndex - indexOfVisibleStash);

                    if (tabNode.VisibleIndex > 30 && indexOfVisibleStash < 30)
                    {
                        SwitchToTab(new StashTabNode { VisibleIndex = 30 }, validState);
                        returnValue = SwitchToTabViaArrowKeys(tabNode, validState);
                    }
                    else
                    {
                        if (travelDistance > 6)
                            returnValue = SwitchToTabViaDropdownMenu(tabNode, validState);
                        else
                            returnValue = SwitchToTabViaArrowKeys(tabNode, validState);
                    }
                }
            }
            if (returnValue && waitForTabToBeReady)
            {
                // need to do this to make sure tab is able to accept drop items, etc...
                // there is discernable lag with certain tabs like map and divination tabs (when man items are present)
                WaitUntil(() => { return stashPanel?.VisibleStash?.VisibleInventoryItems != null; });
            }
            return returnValue;
        }

        private bool SwitchToTabViaArrowKeys(StashTabNode tabNode, Func<bool> validState)
        {
            var latency = (int) _ingameState.CurLatency;
            var indexOfCurrentVisibleTab = _ingameState.ServerData.StashPanel.IndexVisibleStash;
            var difference = tabNode.VisibleIndex - indexOfCurrentVisibleTab;
            var tabIsToTheLeft = difference < 0;

            for (var i = 0; i < Math.Abs(difference) && validState(); i++)
            {
                Keyboard.KeyPress(tabIsToTheLeft ? Keys.Left : Keys.Right);
                Thread.Sleep(latency);
            }

            return true;
        }

        #endregion Switching between StashTabs
        private bool WaitUntil(Func<bool> IsDone, int maxMilliSecsToWait = 500)
        {
            var stopWatch = Stopwatch.StartNew();
            do
            {
                if (stopWatch.ElapsedMilliseconds > maxMilliSecsToWait)
                {
                    return false;
                }
            } while (!IsDone());
            return true;
        }

        public override void OnPluginDestroyForHotReload()
        {
            StashTabNodes.ForEach(StashTabController.UnregisterStashNode);
        }
    }
}
