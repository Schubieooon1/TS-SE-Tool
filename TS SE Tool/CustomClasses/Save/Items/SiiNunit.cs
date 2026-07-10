using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TS_SE_Tool.Save.Items
{
    /// <summary>
    /// Parses and writes the root SiiNunit save container.
    ///
    /// Newer game versions regularly add blocks and fields. The editor therefore keeps
    /// the original representation of every block and only patches fields that were
    /// actually changed by the editor. Unknown data stays equivalent at line level
    /// instead of being discarded.
    /// </summary>
    class SiiNunit
    {
        internal Dictionary<string, dynamic> SiiNitems = new Dictionary<string, dynamic>();

        internal string EconomyNameless = "";

        // Kept for API compatibility. Unknown blocks are now safely preserved and no
        // longer treated as a reason to block editing or show a warning dialog.
        internal List<string> UnidentifiedBlocks = new List<string>();
        internal List<string> NamelessControlList = new List<string>();

        private readonly List<string> BlockOrder = new List<string>();
        private readonly Dictionary<string, List<string>> OriginalBlocks = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, string> BaselineBlocks = new Dictionary<string, string>();

        internal Economy Economy
        {
            get => (Economy)SiiNitems[EconomyNameless];
            set => SiiNitems[EconomyNameless] = value;
        }

        internal Bank Bank
        {
            get => (Bank)SiiNitems[Economy.bank];
            set => SiiNitems[Economy.bank] = value;
        }

        internal Player Player
        {
            get => (Player)SiiNitems[Economy.player];
            set => SiiNitems[Economy.player] = value;
        }

        internal Player_Job Player_Job
        {
            get => Player.current_job != "null" ? (Player_Job)SiiNitems[Player.current_job] : null;
            set => SiiNitems[Player.current_job] = value;
        }

        internal Economy_event_Queue Economy_event_Queue
        {
            get => (Economy_event_Queue)SiiNitems[Economy.event_queue];
            set => SiiNitems[Economy.event_queue] = value;
        }

        internal SiiNunit()
        { }

        internal SiiNunit(string[] input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            ParseBlocks(input);
            CaptureBaselines();
        }

        private void ParseBlocks(string[] input)
        {
            for (int line = 0; line < input.Length; line++)
            {
                string tagLine;
                string nameless;

                if (!TryParseBlockHeader(input[line], out tagLine, out nameless))
                    continue;

                List<string> rawBlock = ReadBlock(input, ref line);
                string[] parserBlock = PrepareBlockForTypedParser(tagLine, rawBlock);
                dynamic parsedBlock;
                bool knownBlock;

                try
                {
                    parsedBlock = CreateBlock(tagLine, parserBlock, rawBlock, out knownBlock);
                }
                catch (Exception ex)
                {
                    knownBlock = false;
                    parsedBlock = new Unidentified(rawBlock);
                    Utilities.IO_Utilities.ErrorLogWriter(
                        "Save | Falling back to raw block | " + tagLine + Environment.NewLine + ex.Message);
                }

                if (tagLine == "economy")
                    EconomyNameless = nameless;

                BlockOrder.Add(nameless);
                NamelessControlList.Add(nameless);
                OriginalBlocks[nameless] = new List<string>(rawBlock);

                if (SiiNitems.ContainsKey(nameless))
                {
                    Utilities.IO_Utilities.ErrorLogWriter(
                        "Save | Duplicate block identifier replaced | " + nameless);
                    SiiNitems[nameless] = parsedBlock;
                }
                else
                {
                    SiiNitems.Add(nameless, parsedBlock);
                }

                if (!knownBlock)
                {
                    // Compatibility mode deliberately does not populate UnidentifiedBlocks;
                    // the old UI used that collection to show a warning dialog. The raw
                    // serializer below can now preserve these blocks safely.
                    Utilities.IO_Utilities.ErrorLogWriter(
                        "Save | Preserving unknown data block | " + tagLine + Environment.NewLine +
                        string.Join(Environment.NewLine, rawBlock));
                }
            }
        }

        private static bool TryParseBlockHeader(string line, out string tagLine, out string nameless)
        {
            tagLine = "";
            nameless = "";

            if (string.IsNullOrWhiteSpace(line))
                return false;

            int colon = line.IndexOf(':');
            int openingBrace = line.LastIndexOf('{');

            if (colon <= 0 || openingBrace <= colon)
                return false;

            tagLine = line.Substring(0, colon).Trim();
            nameless = line.Substring(colon + 1, openingBrace - colon - 1).Trim();

            return tagLine.Length > 0 && nameless.Length > 0;
        }

        private static List<string> ReadBlock(string[] input, ref int line)
        {
            List<string> data = new List<string>();
            int depth = 0;
            bool started = false;

            for (; line < input.Length; line++)
            {
                string current = input[line];
                data.Add(current);

                int delta = CountStructuralBraces(current);
                if (delta > 0)
                    started = true;

                depth += delta;
                if (started && depth <= 0)
                    break;
            }

            return data;
        }

        private static int CountStructuralBraces(string line)
        {
            int result = 0;
            bool quoted = false;
            bool escaped = false;

            foreach (char character in line)
            {
                if (character == '"' && !escaped)
                    quoted = !quoted;

                if (!quoted)
                {
                    if (character == '{')
                        result++;
                    else if (character == '}')
                        result--;
                }

                if (character == '\\' && !escaped)
                    escaped = true;
                else
                    escaped = false;
            }

            return result;
        }

        private static string[] PrepareBlockForTypedParser(string tagLine, List<string> rawBlock)
        {
            string[] result = rawBlock.ToArray();

            // Current ETS2/ATS saves may store this optional numeric value as "nil".
            // Feed a neutral value to the legacy typed parser so it can continue parsing
            // the rest of the economy block. The original nil value remains in rawBlock
            // and is written back unchanged unless the field itself is edited.
            if (tagLine == "economy")
            {
                for (int i = 0; i < result.Length; i++)
                {
                    string key;
                    string value;
                    if (TryParseField(result[i], out key, out value) &&
                        key == "game_time_initial" &&
                        string.Equals(value, "nil", StringComparison.OrdinalIgnoreCase))
                    {
                        int colon = result[i].IndexOf(':');
                        result[i] = result[i].Substring(0, colon + 1) + " 0";
                    }
                }
            }

            return result;
        }

        private static dynamic CreateBlock(
            string tagLine,
            string[] parserBlock,
            List<string> rawBlock,
            out bool knownBlock)
        {
            knownBlock = true;

            switch (tagLine)
            {
                case "economy": return new Economy(parserBlock);
                case "bank": return new Bank(parserBlock);
                case "bank_loan": return new Bank_Loan(parserBlock);
                case "player": return new Player(parserBlock);
                case "trailer": return new Trailer(parserBlock);
                case "trailer_utilization_log": return new Trailer_Utilization_log(parserBlock);
                case "trailer_utilization_log_entry": return new Trailer_Utilization_log_Entry(parserBlock);
                case "trailer_def": return new Trailer_Def(parserBlock);
                case "player_job": return new Player_Job(parserBlock);
                case "vehicle": return new Vehicle(parserBlock);
                case "vehicle_accessory": return new Vehicle_Accessory(parserBlock);
                case "vehicle_addon_accessory": return new Vehicle_Addon_Accessory(parserBlock);
                case "vehicle_drv_plate_accessory": return new Vehicle_Drv_plate_Accessory(parserBlock);
                case "vehicle_wheel_accessory": return new Vehicle_Wheel_Accessory(parserBlock);
                case "vehicle_paint_job_accessory": return new Vehicle_Paint_job_Accessory(parserBlock);
                case "vehicle_sound_accessory": return new Vehicle_Sound_Accessory(parserBlock);
                case "vehicle_cargo_accessory": return new Vehicle_Cargo_Accessory(parserBlock);
                case "profit_log": return new Profit_log(parserBlock);
                case "profit_log_entry": return new Profit_log_Entry(parserBlock);
                case "driver_player": return new Driver_Player(parserBlock);
                case "driver_ai": return new Driver_AI(parserBlock);
                case "job_info": return new Job_Info(parserBlock);
                case "company": return new Company(parserBlock);
                case "job_offer_data": return new Job_offer_Data(parserBlock);
                case "garage": return new Garage(parserBlock);
                case "game_progress": return new Game_Progress(parserBlock);
                case "registry": return new Registry(parserBlock);
                case "transport_data": return new Transport_Data(parserBlock);
                case "economy_event_queue": return new Economy_event_Queue(parserBlock);
                case "economy_event": return new Economy_event(parserBlock);
                case "mail_ctrl": return new Mail_Ctrl(parserBlock);
                case "mail_def": return new Mail_Def(parserBlock);
                case "oversize_job_save": return new Oversize_Job_save(parserBlock);
                case "trajectory_orders_save": return new Trajectory_orders_Save(parserBlock);
                case "oversize_block_rule_save": return new Oversize_Block_rule_Save(parserBlock);
                case "police_ctrl": return new Police_Ctrl(parserBlock);
                case "oversize_offer_ctrl": return new Oversize_offer_Ctrl(parserBlock);
                case "oversize_route_offers": return new Oversize_Route_offers(parserBlock);
                case "oversize_offer": return new Oversize_Offer(parserBlock);
                case "delivery_log": return new Delivery_log(parserBlock);
                case "delivery_log_entry": return new Delivery_log_Entry(parserBlock);
                case "ferry_log": return new Ferry_log(parserBlock);
                case "ferry_log_entry": return new Ferry_log_Entry(parserBlock);
                case "gps_waypoint_storage": return new GPS_waypoint_Storage(parserBlock);
                case "map_action": return new Map_action(parserBlock);
                case "bus_stop": return new Bus_stop(parserBlock);
                case "bus_job_log": return new Bus_job_Log(parserBlock);
                default:
                    knownBlock = false;
                    return new Unidentified(rawBlock);
            }
        }

        private void CaptureBaselines()
        {
            foreach (string nameless in BlockOrder.Distinct())
            {
                string serialized = SerializeCurrentBlock(nameless);
                if (serialized != null)
                    BaselineBlocks[nameless] = NormalizeText(serialized);
            }
        }

        internal string PrintOut(uint version)
        {
            StringBuilder output = new StringBuilder();
            HashSet<string> written = new HashSet<string>();

            output.AppendLine("SiiNunit");
            output.AppendLine("{");

            foreach (string nameless in BlockOrder)
            {
                if (written.Contains(nameless) || !SiiNitems.ContainsKey(nameless))
                    continue;

                AppendBlock(output, BuildOutputBlock(nameless));
                written.Add(nameless);
            }

            // Objects created by editor features do not exist in the original order list.
            // Append them after all original blocks; SII references are identifier based.
            foreach (KeyValuePair<string, dynamic> item in SiiNitems)
            {
                if (written.Contains(item.Key))
                    continue;

                AppendBlock(output, SerializeCurrentBlock(item.Key));
                written.Add(item.Key);
            }

            output.Append("}");
            return output.ToString();
        }

        private string BuildOutputBlock(string nameless)
        {
            string current = SerializeCurrentBlock(nameless);
            if (current == null)
                return GetOriginalBlock(nameless);

            List<string> original;
            string baseline;
            if (!OriginalBlocks.TryGetValue(nameless, out original) ||
                !BaselineBlocks.TryGetValue(nameless, out baseline))
            {
                return current;
            }

            string normalizedCurrent = NormalizeText(current);
            if (string.Equals(normalizedCurrent, baseline, StringComparison.Ordinal))
                return string.Join(Environment.NewLine, original);

            return MergeChangedFields(original, baseline, normalizedCurrent);
        }

        private string SerializeCurrentBlock(string nameless)
        {
            dynamic item;
            if (!SiiNitems.TryGetValue(nameless, out item) || item == null)
                return null;

            try
            {
                return item.PrintOut(0, nameless);
            }
            catch (Exception ex)
            {
                Utilities.IO_Utilities.ErrorLogWriter(
                    "Save | Could not serialize block, original preserved | " + nameless + Environment.NewLine + ex.Message);
                return null;
            }
        }

        private string GetOriginalBlock(string nameless)
        {
            List<string> original;
            return OriginalBlocks.TryGetValue(nameless, out original)
                ? string.Join(Environment.NewLine, original)
                : null;
        }

        private static string MergeChangedFields(
            List<string> original,
            string baseline,
            string current)
        {
            BlockGroups baselineGroups = ParseGroups(baseline);
            BlockGroups currentGroups = ParseGroups(current);
            HashSet<string> changedGroups = new HashSet<string>();
            HashSet<string> allGroups = new HashSet<string>(baselineGroups.Order);

            foreach (string group in currentGroups.Order)
                allGroups.Add(group);

            foreach (string group in allGroups)
            {
                List<string> before;
                List<string> after;
                baselineGroups.Lines.TryGetValue(group, out before);
                currentGroups.Lines.TryGetValue(group, out after);

                if (!SequenceEqual(before, after))
                    changedGroups.Add(group);
            }

            if (changedGroups.Count == 0)
                return string.Join(Environment.NewLine, original);

            List<string> merged = new List<string>();
            HashSet<string> inserted = new HashSet<string>();
            bool closingBraceFound = false;

            foreach (string line in original)
            {
                if (IsClosingBrace(line))
                {
                    AppendMissingChangedGroups(merged, inserted, changedGroups, currentGroups);
                    merged.Add(line);
                    closingBraceFound = true;
                    continue;
                }

                string key;
                string value;
                if (TryParseField(line, out key, out value))
                {
                    string group = GetGroupKey(key);
                    if (changedGroups.Contains(group))
                    {
                        if (inserted.Add(group))
                        {
                            List<string> replacement;
                            if (currentGroups.Lines.TryGetValue(group, out replacement))
                                merged.AddRange(replacement);
                        }

                        continue;
                    }
                }

                merged.Add(line);
            }

            if (!closingBraceFound)
                AppendMissingChangedGroups(merged, inserted, changedGroups, currentGroups);

            return string.Join(Environment.NewLine, merged);
        }

        private static void AppendMissingChangedGroups(
            List<string> destination,
            HashSet<string> inserted,
            HashSet<string> changedGroups,
            BlockGroups currentGroups)
        {
            foreach (string group in currentGroups.Order)
            {
                if (!changedGroups.Contains(group) || !inserted.Add(group))
                    continue;

                destination.AddRange(currentGroups.Lines[group]);
            }
        }

        private static BlockGroups ParseGroups(string block)
        {
            BlockGroups result = new BlockGroups();
            foreach (string line in SplitLines(block))
            {
                string key;
                string value;
                if (!TryParseField(line, out key, out value))
                    continue;

                string group = GetGroupKey(key);
                List<string> lines;
                if (!result.Lines.TryGetValue(group, out lines))
                {
                    lines = new List<string>();
                    result.Lines.Add(group, lines);
                    result.Order.Add(group);
                }

                lines.Add(line);
            }

            return result;
        }

        private static bool TryParseField(string line, out string key, out string value)
        {
            key = "";
            value = "";

            if (string.IsNullOrWhiteSpace(line))
                return false;

            string trimmed = line.Trim();
            if (trimmed == "}" || trimmed.EndsWith("{", StringComparison.Ordinal))
                return false;

            int colon = line.IndexOf(':');
            if (colon <= 0)
                return false;

            key = line.Substring(0, colon).Trim();
            value = line.Substring(colon + 1).Trim();
            return key.Length > 0;
        }

        private static string GetGroupKey(string key)
        {
            int bracket = key.IndexOf('[');
            return bracket > 0 ? key.Substring(0, bracket) : key;
        }

        private static bool SequenceEqual(List<string> left, List<string> right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || left.Count != right.Count)
                return false;

            for (int i = 0; i < left.Count; i++)
            {
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private static bool IsClosingBrace(string line)
        {
            return line != null && line.Trim() == "}";
        }

        private static string NormalizeText(string text)
        {
            return string.Join("\n", SplitLines(text)).TrimEnd('\n');
        }

        private static List<string> SplitLines(string text)
        {
            if (text == null)
                return new List<string>();

            return text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split(new[] { '\n' }, StringSplitOptions.None)
                .ToList();
        }

        private static void AppendBlock(StringBuilder destination, string block)
        {
            if (string.IsNullOrWhiteSpace(block))
                return;

            List<string> lines = SplitLines(block);
            while (lines.Count > 0 && lines[lines.Count - 1].Length == 0)
                lines.RemoveAt(lines.Count - 1);

            foreach (string line in lines)
                destination.AppendLine(line);
        }

        private sealed class BlockGroups
        {
            internal readonly Dictionary<string, List<string>> Lines = new Dictionary<string, List<string>>();
            internal readonly List<string> Order = new List<string>();
        }
    }
}
