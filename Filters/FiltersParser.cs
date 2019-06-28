using System;
using System.Collections.Generic;
using System.Linq;
using PoeHUD.Plugins;
using PoeHUD.Hud.Settings;

namespace Stashie.Filters
{
    public class FilterParser
    {
        private const char SYMBOL_COMMANDSDIVIDE = ',';
        private const char SYMBOL_COMMAND_FILTER_OR = '|';
        private const char SYMBOL_NAMEDIVIDE = ':';
        private const char SYMBOL_SUBMENUNAME = ':';
        private const char SYMBOL_NOT = '!';
        private const string COMMENTSYMBOL = "#";
        private const string COMMENTSYMBOLALT = "//";

        //String compare
        private const string PARAMETER_CLASSNAME = "classname";

        private const string PARAMETER_NAME = "name";
        private const string PARAMETER_BASENAME = "basename";
        private const string PARAMETER_PATH = "path";

        //Number compare
        private const string PARAMETER_QUALITY = "itemquality";

        private const string PARAMETER_RARITY = "rarity";
        private const string PARAMETER_ILVL = "ilvl";
        private const string PARAMETER_MAP_TIER = "tier";
        private const string PARAMETER_NUMBER_OF_SOCKETS = "numberofsockets";
        private const string PARAMETER_LARGEST_LINK_SIZE = "numberoflinks";

        //Boolean
        private const string PARAMETER_IDENTIFIED = "identified";

        private const string PARAMETER_ISELDER = "Elder";
        private const string PARAMETER_ISSHAPER = "Shaper";

        private const string PARAMETER_ISCORRUPTED = "corrupted";

        private const string PARAMETER_ISVEILED = "veiled";

        private const string PARAMETER_ISINCUBATOR = "incubator";

        //Operations
        private const string OPERATION_NONEQUALITY = "!=";

        private const string OPERATION_LESSEQUAL = "<=";
        private const string OPERATION_BIGGERQUAL = ">=";
        private const string OPERATION_EQUALITY = "=";
        private const string OPERATION_BIGGER = ">";
        private const string OPERATION_LESS = "<";
        private const string OPERATION_CONTAINS = "^";
        private const string OPERATION_NOTCONTAINS = "!^";

        private static readonly string[] Operations =
        {
            OPERATION_NONEQUALITY,
            OPERATION_LESSEQUAL,
            OPERATION_BIGGERQUAL,
            OPERATION_NOTCONTAINS,
            OPERATION_EQUALITY,
            OPERATION_BIGGER,
            OPERATION_LESS,
            OPERATION_CONTAINS
        };

        public static List<CustomFilter> Parse(string[] filtersLines)
        {
            var allFilters = new List<CustomFilter>();

            for (var i = 0; i < filtersLines.Length; ++i)
            {
                var filterLine = filtersLines[i];

                filterLine = filterLine.Replace("\t", "");

                if (filterLine.StartsWith(COMMENTSYMBOL))
                {
                    continue;
                }

                if (filterLine.StartsWith(COMMENTSYMBOLALT))
                {
                    continue;
                }

                if (filterLine.Replace(" ", "").Length == 0)
                {
                    continue;
                }

                var nameIndex = filterLine.IndexOf(SYMBOL_NAMEDIVIDE);
                if (nameIndex == -1)
                {
                    BasePlugin.LogMessage("Filter parser: Can't find filter name in line: " + (i + 1), 5);
                    continue;
                }

                var newFilter = new CustomFilter {Name = filterLine.Substring(0, nameIndex)};
                TrimName(ref newFilter.Name);

                var filterCommandsLine = filterLine.Substring(nameIndex + 1);

                var submenuIndex = filterCommandsLine.IndexOf(SYMBOL_SUBMENUNAME);
                if (submenuIndex != -1)
                {
                    newFilter.SubmenuName = filterCommandsLine.Substring(submenuIndex + 1);
                    filterCommandsLine = filterCommandsLine.Substring(0, submenuIndex);
                }

                var filterCommands = filterCommandsLine.Split(SYMBOL_COMMANDSDIVIDE);

                var filterErrorParse = false;

                foreach (var command in filterCommands)
                {
                    if (string.IsNullOrEmpty(command.Replace(" ", "")))
                    {
                        continue;
                    }

                    if (command.Contains(SYMBOL_COMMAND_FILTER_OR))
                    {
                        var orFilterCommands = command.Split(SYMBOL_COMMAND_FILTER_OR);
                        var newOrFilter = new BaseFilter {BAny = true};
                        newFilter.Filters.Add(newOrFilter);

                        foreach (var t in orFilterCommands)
                        {
                            if (ProcessCommand(newOrFilter, t))
                            {
                                continue;
                            }

                            filterErrorParse = true;
                            break;
                        }

                        if (filterErrorParse)
                        {
                            break;
                        }
                    }
                    else
                    {
                        if (ProcessCommand(newFilter, command))
                        {
                            continue;
                        }

                        filterErrorParse = true;
                        break;
                    }
                }

                if (!filterErrorParse)
                {
                    allFilters.Add(newFilter);
                }
            }

            return allFilters;
        }

        private static bool ProcessCommand(BaseFilter newFilter, string command)
        {
            TrimName(ref command);

            if (command.Contains(PARAMETER_IDENTIFIED))
            {
                var identCommand = new IdentifiedItemFilter {BIdentified = command[0] != SYMBOL_NOT};
                newFilter.Filters.Add(identCommand);
                return true;
            }

            if (command.Contains(PARAMETER_ISCORRUPTED))
            {
                var corruptedCommand = new CorruptedItemFilter {BCorrupted = command[0] != SYMBOL_NOT};
                newFilter.Filters.Add(corruptedCommand);
                return true;
            }

            if (command.Contains(PARAMETER_ISELDER))
            {
                var elderCommand = new ElderItemFiler {IsElder = command[0] != SYMBOL_NOT};
                newFilter.Filters.Add(elderCommand);
                return true;
            }

            if (command.Contains(PARAMETER_ISSHAPER))
            {
                var shaperCommand = new ShaperItemFiler {IsShaper = command[0] != SYMBOL_NOT};
                newFilter.Filters.Add(shaperCommand);
                return true;
            }

            if (command.Contains(PARAMETER_ISVEILED))
            {
                var veiledCommand = new VeiledItemFilter { IsVeiled = command[0] != SYMBOL_NOT };
                newFilter.Filters.Add(veiledCommand);
                return true;
            }

            if (command.Contains(PARAMETER_ISINCUBATOR))
            {
                var incubatorCommand = new IncubatorItemFilter { IsIncubator = command[0] != SYMBOL_NOT };
                newFilter.Filters.Add(incubatorCommand);
                return true;
            }

            if (!ParseCommand(command, out var parameter, out var operation, out var value))
            {
                BasePlugin.LogMessage("Filter parser: Can't parse filter part: " + command, 5);
                return false;
            }

            var stringComp = new FilterParameterCompare {CompareString = value};
            var isNumeric = int.TryParse(value, out var n);
            if (isNumeric)
            {
                stringComp.CompareInt = n;
            }

            switch (parameter.ToLower())
            {
                case PARAMETER_CLASSNAME:
                    stringComp.StringParameter = data => data.ClassName;
                    break;

                case PARAMETER_NAME:
                    stringComp.StringParameter = data => data.Name;
                    break;

                case PARAMETER_BASENAME:
                    stringComp.StringParameter = data => data.BaseName;
                    break;

                case PARAMETER_PATH:
                    stringComp.StringParameter = data => data.Path;
                    break;

                case PARAMETER_RARITY:
                    stringComp.StringParameter = data => data.Rarity.ToString();
                    break;

                case PARAMETER_QUALITY:
                    stringComp.IntParameter = data => data.ItemQuality;
                    stringComp.StringParameter = data => data.ItemQuality.ToString();
                    break;

                case PARAMETER_MAP_TIER:
                    stringComp.IntParameter = data => data.MapTier;
                    stringComp.StringParameter = data => data.MapTier.ToString();
                    break;

                case PARAMETER_ILVL:
                    stringComp.IntParameter = data => data.ItemLevel;
                    stringComp.StringParameter = data => data.ItemLevel.ToString();
                    break;

                case PARAMETER_NUMBER_OF_SOCKETS:
                    stringComp.IntParameter = data => data.NumberOfSockets;
                    stringComp.StringParameter = data => data.NumberOfSockets.ToString();
                    break;

                case PARAMETER_LARGEST_LINK_SIZE:
                    stringComp.IntParameter = data => data.LargestLinkSize;
                    stringComp.StringParameter = data => data.LargestLinkSize.ToString();
                    break;

                default:
                    BasePlugin.LogMessage(
                        $"Filter parser: Parameter is not defined in code: {parameter}", 10);
                    return false;
            }

            switch (operation.ToLower())
            {
                case OPERATION_EQUALITY:
                    stringComp.CompDeleg = data => stringComp.StringParameter(data).Equals(stringComp.CompareString);
                    break;

                case OPERATION_NONEQUALITY:
                    stringComp.CompDeleg = data => !stringComp.StringParameter(data).Equals(stringComp.CompareString);
                    break;

                case OPERATION_CONTAINS:
                    stringComp.CompDeleg = data => stringComp.StringParameter(data).Contains(stringComp.CompareString);
                    break;

                case OPERATION_NOTCONTAINS:
                    stringComp.CompDeleg = data => !stringComp.StringParameter(data).Contains(stringComp.CompareString);
                    break;

                case OPERATION_BIGGER:
                    if (stringComp.IntParameter == null)
                    {
                        BasePlugin.LogMessage(
                            $"Filter parser error: Can't compare string parameter with {OPERATION_BIGGER} (numerical) operation. Statement: {command}",
                            10);
                        return false;
                    }

                    stringComp.CompDeleg = data => stringComp.IntParameter(data) > stringComp.CompareInt;
                    break;

                case OPERATION_LESS:
                    if (stringComp.IntParameter == null)
                    {
                        BasePlugin.LogMessage(
                            $"Filter parser error: Can't compare string parameter with {OPERATION_LESS} (numerical) operation. Statement: {command}",
                            10);
                        return false;
                    }

                    stringComp.CompDeleg = data => stringComp.IntParameter(data) < stringComp.CompareInt;
                    break;

                case OPERATION_LESSEQUAL:
                    if (stringComp.IntParameter == null)
                    {
                        BasePlugin.LogMessage(
                            $"Filter parser error: Can't compare string parameter with {OPERATION_LESSEQUAL} (numerical) operation. Statement: {command}",
                            10);
                        return false;
                    }

                    stringComp.CompDeleg = data => stringComp.IntParameter(data) <= stringComp.CompareInt;
                    break;

                case OPERATION_BIGGERQUAL:
                    if (stringComp.IntParameter == null)
                    {
                        BasePlugin.LogMessage(
                            $"Filter parser error: Can't compare string parameter with {OPERATION_BIGGERQUAL} (numerical) operation. Statement: {command}",
                            10);
                        return false;
                    }

                    stringComp.CompDeleg = data => stringComp.IntParameter(data) >= stringComp.CompareInt;
                    break;

                default:
                    BasePlugin.LogMessage(
                        $"Filter parser: Operation is not defined in code: {operation}", 10);
                    return false;
            }

            newFilter.Filters.Add(stringComp);
            return true;
        }

        private static bool ParseCommand(string command, out string parameter, out string operation, out string value)
        {
            parameter = "";
            operation = "";
            value = "";

            var operationIndex = -1;
            foreach (var t in Operations)
            {
                operationIndex = command.IndexOf(t, StringComparison.Ordinal);

                if (operationIndex == -1)
                {
                    continue;
                }

                operation = t;
                break;
            }

            if (operationIndex == -1)
            {
                return false;
            }

            parameter = command.Substring(0, operationIndex);
            TrimName(ref parameter);

            value = command.Substring(operationIndex + operation.Length);
            TrimName(ref value); //Should I do this?
            return true;
        }

        private static void TrimName(ref string name)
        {
            name = name.TrimEnd(' ');
            name = name.TrimStart(' ');
        }
    }

    public class CustomFilter : BaseFilter
    {
        public string Name;
        public StashTabNode StashIndexNode;
        public string SubmenuName;

        public bool AllowProcess => StashIndexNode.Exist;
    }

    public class BaseFilter : IIFilter
    {
        public bool BAny;
        public List<IIFilter> Filters = new List<IIFilter>();

        public bool CompareItem(ItemData itemData)
        {
            return BAny ? Filters.Any(x => x.CompareItem(itemData)) : Filters.All(x => x.CompareItem(itemData));
        }
    }

    public class IdentifiedItemFilter : IIFilter
    {
        public bool BIdentified;

        public bool CompareItem(ItemData itemData)
        {
            return itemData.BIdentified == BIdentified;
        }
    }

    public class CorruptedItemFilter : IIFilter
    {
        public bool BCorrupted;

        public bool CompareItem(ItemData itemData)
        {
            return itemData.IsCorrupted == BCorrupted;
        }
    }

    public class ElderItemFiler : IIFilter
    {
        public bool IsElder;

        public bool CompareItem(ItemData itemData)
        {
            return itemData.IsElder == IsElder;
        }
    }

    public class ShaperItemFiler : IIFilter
    {
        public bool IsShaper;

        public bool CompareItem(ItemData itemData)
        {
            return itemData.IsShaper == IsShaper;
        }
    }
    public class VeiledItemFilter : IIFilter
    {
        public bool IsVeiled;

        public bool CompareItem(ItemData itemData)
        {
            return itemData.VeiledMods?.Length > 0;
        }
    }

    public class IncubatorItemFilter : IIFilter
    {
        public bool IsIncubator;
        public bool CompareItem(ItemData itemData)
        {
            return itemData.IsIncubator == IsIncubator;
        }
    }

    public class FilterParameterCompare : IIFilter
    {
        public int CompareInt;
        public string CompareString;
        public Func<ItemData, bool> CompDeleg;
        public Func<ItemData, int> IntParameter;
        public Func<ItemData, string> StringParameter;

        public bool CompareItem(ItemData itemData)
        {
            return CompDeleg(itemData);
        }
    }

    public interface IIFilter
    {
        bool CompareItem(ItemData itemData);
    }
}