using NLog;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using static System.Formats.Asn1.AsnWriter;
using static TSB.Player;
using static TSB.SaveState;
using static TSB.Team;

namespace TSB
{
    public class TSB_App
    {
        public ROM Rom { get; private set; }

        internal Logger log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path">Path to the ROM (.nes) file</param>
        public TSB_App(string path)
        {
            try
            {
                Rom = new(path);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                throw;
            }
        }
    }

    // TSB_RosterRipper : TSB_App
    public class TSB_RosterRipper(string path) : TSB_App(path)
    {
        // Not a perfect match to previous format, but I think enough to move on.
        public void Rip()
        {
            StringBuilder sb = new();
            foreach (Team team in Rom.GetTeams())
            {
                foreach (Player p in team.Roster.Values)
                {
                    // my @PLAYER_ATTR = qw'Name Team PlayerPos TeamPos RS RP MS HP PS PC AP APB BC REC INT QK KKA AKKB PKA APKB';
                    // This was the format of the original "Roster Ripper", a good starting point
                    // E.g. Qb Bills,BUF,QB,BUFQB1,25,69,13,13,56,81,81,81,0,0,0,0,0,0,0,0
                    string team_code = team_code = team.Label[..3]; // "range operator"
                    string team_pos = team_code + p.RosterPosition.ToString();
                    string name = $"{p.FirstName} {p.LastName},{team_code},{p.RosterRole},{team_pos},";
                    StringBuilder stats = new();
                    foreach (Player.Attributes pa in p.dictAttributes.Keys)
                        stats.Append($"{p.dictAttributes[pa]},");

                    string s = stats.ToString()[..(stats.Length - 1)]; // Remove trailing comma
                    sb.AppendLine(name + s);
                }
            }

            // Output
            Directory.CreateDirectory("rips");
            string outputFileName = $".\\rips\\TSB_RosterRipper_{Rom.GetDisplayName()}_{DateTime.Now:yy-MM-dd_HH-mm-ss}.txt";
            try
            {
                File.WriteAllText(outputFileName, sb.ToString());
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
            }
        }
    }

    /// <summary>
    /// TSB_StatExtractor : TSB_App - Extracts stats from TSB save state files
    /// </summary>
    public class TSB_StatExtractor(string path) : TSB_App(path)
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path">Path to the save state file (e.g. .ns1, .ns2, .nst)</param>
        public void WriteStats(string path)
        {
            StringBuilder sb = new();
            SaveState save = new(Rom, path);

            sb.AppendLine(save.GetGameClockForDisplay());

            ExportTeamStatsToStringBuilder(save.homeTeamStats, sb);
            sb.Append(Environment.NewLine);

            ExportTeamStatsToStringBuilder(save.awayTeamStats, sb);

            // Output
            Directory.CreateDirectory("extracts");
            string outputFileName = $".\\extracts\\TSB_StatExtractor_{Rom.GetDisplayName()}_{DateTime.Now:yy-MM-dd_HH-mm-ss}.txt";
            try
            {
                File.WriteAllText(outputFileName, sb.ToString());
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
            }
        }

        public string ExportStats(string path)
        {
            StringBuilder sb = new();
            SaveState save = new(Rom, path);

            sb.AppendLine(save.GetGameClockForDisplay());

            ExportTeamStatsToStringBuilder(save.homeTeamStats, sb);
            sb.Append(Environment.NewLine);

            ExportTeamStatsToStringBuilder(save.awayTeamStats, sb);

            return sb.ToString();
        }

        private void ExportTeamStatsToStringBuilder(SaveState.TeamStats teamStats, StringBuilder _sb)
        {
            // Team
            _sb.AppendLine($"{teamStats.StadiumStatus}");
            _sb.AppendLine($"{teamStats.TeamLabel}");
            _sb.AppendLine($"{teamStats.TeamScore}");

            // Have the player at each roster position display their info
            foreach (Team.RosterPosition pos in SaveState.StatsRosterPositions)
            {
                Player? player = Rom.GetPlayers().FirstOrDefault(p => p.TeamLabel.Equals(teamStats.TeamLabel) && p.RosterPosition == pos);
                if (player != null)
                {
                    _sb.AppendLine($"{pos} {player.FirstName} {player.LastName}");
                }
                else
                    log.Error("ERROR");
                teamStats._dictStatsByRosterPos[pos].ExportToStringBuilder(Rom.TACKLE_HACK_ROM, _sb);
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="path">ROM path</param>
    public class TSB_ConditionChecker(string path) : TSB_App(path)
    {
        private class ConsoleColorString(string s, ConsoleColor cc = ConsoleColor.White)
        {
            internal readonly string text = s;
            internal readonly ConsoleColor consoleColor = cc;
        }

        private string PathSaveState = string.Empty;

        public SaveState saveState;

        // Bruddog's Condition Checker shows these
        public readonly List<Attributes> DefenseDisplayPlayerAttributes = [Attributes.RS, Attributes.RP, Attributes.HP, Attributes.INT];
        public readonly List<Attributes> QbDisplayPlayerAttributes = [Attributes.MS, Attributes.PS, Attributes.PC];
        public readonly List<Attributes> HandsDisplayPlayerAttributes = [Attributes.MS, Attributes.HP, Attributes.REC];
        public readonly List<Attributes> OlDisplayPlayerAttributes = [Attributes.MS, Attributes.HP];
        public readonly List<Attributes> K_DisplayPlayerAttributes = [Attributes.KA];
        public readonly List<Attributes> P_DisplayPlayerAttributes = [Attributes.PKA];

        public List<Attributes> GetDisplayAttributes(RosterRole role)
        {
            List<Attributes> result = new List<Attributes>();
            switch (role)
            {
                case RosterRole.QB:
                    result = QbDisplayPlayerAttributes;
                    break;
                case RosterRole.RB:
                case RosterRole.WR:
                case RosterRole.TE:
                    result = HandsDisplayPlayerAttributes;
                    break;
                case RosterRole.DL:
                case RosterRole.LB:
                case RosterRole.DB:
                    result = DefenseDisplayPlayerAttributes;
                    break;
                case RosterRole.OL:
                    result = OlDisplayPlayerAttributes;
                    break;
                case RosterRole.K:
                    result = K_DisplayPlayerAttributes;
                    break;
                case RosterRole.P:
                    result = P_DisplayPlayerAttributes;
                    break;
            }

            return result;
        }

        private void PrintColoredStrings (List<ConsoleColorString> ConsoleColorStrings)
        {
            foreach (ConsoleColorString cc_str in ConsoleColorStrings)
            {
                Console.ForegroundColor = cc_str.consoleColor;
                Console.WriteLine(cc_str.text);
            }
        }
        // TODO return success flag?
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path">save state</param>
        public void Start(string path, Func<string> myMethodName)
        //public void Start(string path)
        {
            log.Trace(System.Reflection.MethodBase.GetCurrentMethod()?.Name.ToString() + "()");

            PathSaveState = path;
            string filename = Path.GetFileName(path);
            string? directory = Path.GetDirectoryName(path);
            if (directory == null)
                return;
            log.Trace($"directory: '{directory}', filename: '{filename}'");

            saveState = new SaveState(Rom, PathSaveState);

            // create a new instance
            try
            {
                using var watcher = new FileSystemWatcher(directory);

                // subscribe to events
                watcher.NotifyFilter = NotifyFilters.LastWrite;
                // define event handlers
                watcher.Changed += OnChanged;
                watcher.Filter = filename;
                // Finally, start the FileSystemWatcher by setting the EnableRaisingEvents property to true
                watcher.EnableRaisingEvents = true;

                //UpdateConsoleDisplay();

                _ = myMethodName();

                Console.ReadLine();
            }
            catch (Exception ex)
            {
                log.Error(ex.Message); return;
            }
        }

        /// <summary>
        /// Arbitrary length for display formatting
        /// </summary>
        private readonly int len_plyrName = 16; // arbitrary
        private string GetPlayerAttributesDisplay(Player player, List<Attributes> attributes, InjuryStatus injStatus, PlayerCondition playerCond, string starting = "")
        {
            StringBuilder sb = new ();

            string name = $"{player.FirstName} {player.LastName}";
            string formatted_name = name.PadRight(len_plyrName)[..len_plyrName];
            sb.Append($"{formatted_name} {starting.PadLeft(3)} ");

            foreach (Player.Attributes pa in attributes)
            {
                var x = player.dictAttributes[pa];
                int attr_val = GetAttributeValueAdjustedForCondition(x, playerCond);
                sb.Append($"{attr_val.ToString().PadLeft(2)} ");
            }

            if (injStatus.Equals(InjuryStatus.NotInjured))
            {
                //sb.Append($"({playerCond})");
            }
            else
                sb.Append($"({injStatus})");

            return sb.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="colorStrings"></param>
        /// <param name="teamStats">TeamStats object from SaveState</param>
        /// <param name="posGrpStart">e.g. RosterPosition.QB1</param>
        /// <param name="posGrpEnd">e.g. RosterPosition.QB1</param>
        /// <param name="plyrAttributes">List of player attributes to process, e.g. MS, HP</param>
        /// <param name="starting_pos"></param>
        /// <returns></returns>
        private void UpdateDisplayPosGroup(List<ConsoleColorString> colorStrings, TeamStats teamStats, RosterPosition posGrpStart, RosterPosition posGrpEnd,
            List<Attributes> plyrAttributes, Dictionary<string, RosterPosition> offensiveStarters)
        {
            for (RosterPosition i = posGrpStart; i <= posGrpEnd; i++)
            {
                Player? player = Rom.GetPlayers().FirstOrDefault(p => p.TeamLabel.Equals(teamStats.TeamLabel) && p.RosterPosition == i);
                if (player != null)
                {
                    InjuryStatus inj = teamStats._dictStatsByRosterPos[i].injuryStatus;
                    PlayerCondition cond = teamStats._dictStatsByRosterPos[i].playerCondition;

                    // Determine if starting (and where)
                    string starting_pos = string.Empty;
                    if (i == offensiveStarters["QB"])
                        starting_pos = "QB";
                    else if (i == offensiveStarters["RB1"])
                        starting_pos = "RB1";
                    else if (i == offensiveStarters["RB2"])
                        starting_pos = "RB2";
                    else if (i == offensiveStarters["WR1"])
                        starting_pos = "WR1";
                    else if (i == offensiveStarters["WR2"])
                        starting_pos = "WR2";
                    else if (i == offensiveStarters["TE"])
                        starting_pos = "TE";

                    ConsoleColor cc = ConsoleColor.White;
                    if (cond == PlayerCondition.Bad)
                        cc = ConsoleColor.Red;
                    else if (cond == PlayerCondition.Good)
                        cc = ConsoleColor.Green;
                    else if (cond == PlayerCondition.Excellent)
                        cc = ConsoleColor.Yellow;

                    colorStrings.Add(new ConsoleColorString(GetPlayerAttributesDisplay(player, plyrAttributes, inj, cond, starting_pos), cc));

                    // For starters, let's get access to the tackles, but still not sure e.g.
                    // How do we want to display data? Same line or second? Always or just if data?
                    // AND it seems that the answer to these questions will help inform the code, so...
                    if (i >= RosterPosition.DL1 && i != RosterPosition.K1)
                    {
                        string display_string = string.Empty;
                        int nTackles = teamStats._dictStatsByRosterPos[i].Tackles;
                        if (nTackles > 0)
                            display_string += $"tackles: {nTackles}";

                        int sacks = teamStats._dictStatsByRosterPos[i]._dictStats[Stats.Sacks];
                        if (sacks > 0)
                        {
                            if (!string.IsNullOrEmpty(display_string))
                                display_string += ", ";
                            display_string += $"sacks: {sacks}";
                        }

                        int interceptions = teamStats._dictStatsByRosterPos[i]._dictStats[Stats.INTs];
                        if (interceptions > 0)
                        {
                            if (!string.IsNullOrEmpty(display_string))
                                display_string += ", ";
                            display_string += $"INTs: {interceptions}";
                        }

                        if (!string.IsNullOrEmpty(display_string))
                        {
                            display_string = $"({display_string})";
                            colorStrings.Add(new ConsoleColorString(display_string, cc));
                        }
                    }
                    else if (i == RosterPosition.QB1 || i == RosterPosition.QB2)
                    {
                        int pass_trys = teamStats._dictStatsByRosterPos[i]._dictStats[Stats.Pass];
                        if (pass_trys > 0)
                        {
                            int pass_compl = teamStats._dictStatsByRosterPos[i]._dictStats[Stats.PassComplete];
                            int pass_tds = teamStats._dictStatsByRosterPos[i]._dictStats[Stats.PassTDs];
                            int pass_yds = teamStats._dictStatsByRosterPos[i]._dictStats[Stats.PassYards];
                            int pass_ints = teamStats._dictStatsByRosterPos[i]._dictStats[Stats.PassINTs];
                            string _display = $"({pass_compl}/{pass_trys}, {pass_yds} YDS, {pass_tds} TD, {pass_ints} INT)";
                            colorStrings.Add(new ConsoleColorString(_display, cc));
                        }
                    }
                    else if (i >= RosterPosition.RB1 && i <= RosterPosition.TE2)
                    {
                        int rush_trys = teamStats._dictStatsByRosterPos[i]._dictStats[Stats.Rush];
                        if (rush_trys > 0)
                        {
                            int rush_yards = teamStats._dictStatsByRosterPos[i]._dictStats[Stats.RushYards];
                            int rush_tds = teamStats._dictStatsByRosterPos[i]._dictStats[Stats.RushTDs];
                            string _display = $"({rush_trys} RUN / {rush_yards} YDS, {rush_tds} TD)";
                            colorStrings.Add(new ConsoleColorString(_display, cc));
                        }

                        int recs = teamStats._dictStatsByRosterPos[i]._dictStats[Stats.Recs];
                        if (recs > 0)
                        {
                            int recs_yards = teamStats._dictStatsByRosterPos[i]._dictStats[Stats.RecYards];
                            int recs_tds = teamStats._dictStatsByRosterPos[i]._dictStats[Stats.RecTDs];
                            string _display = $"({recs} REC / {recs_yards} YDS, {recs_tds} TD)";
                            colorStrings.Add(new ConsoleColorString(_display, cc));
                        }
                    }
                    else if (i == RosterPosition.K1)
                    {
                        int xp_trys = teamStats._dictStatsByRosterPos[i]._dictStats[Stats.XP_Attempts];
                        int fg_trys = teamStats._dictStatsByRosterPos[i]._dictStats[Stats.FG_Attempts];
                        if (xp_trys + fg_trys > 0)
                        {
                            int xp_hits = teamStats._dictStatsByRosterPos[i]._dictStats[Stats.XP_Hits];
                            int fg_hits = teamStats._dictStatsByRosterPos[i]._dictStats[Stats.FG_Hits];
                            string _display = $"({xp_hits}/{xp_trys} XP, {fg_hits}/{fg_trys} FG)";
                            colorStrings.Add(new ConsoleColorString(_display, cc));
                        }
                    }
                }
                else
                    log.Error("ERROR, unable to find player.");
            }
        }

        private void UpdateWindowDisplay()
        {
            bool home_team_has_ball = saveState.DoesHomeTeamHaveBall();
            TeamStats offense_teamStats = home_team_has_ball ? saveState.homeTeamStats : saveState.awayTeamStats;
            TeamStats defense_teamStats = home_team_has_ball ? saveState.awayTeamStats : saveState.homeTeamStats;
        }

        private void UpdateConsoleDisplay()
        {
            log.Trace(System.Reflection.MethodBase.GetCurrentMethod()?.Name.ToString() + "()");

            bool home_team_has_ball = saveState.DoesHomeTeamHaveBall();
            TeamStats offense_teamStats = home_team_has_ball ? saveState.homeTeamStats : saveState.awayTeamStats;
            TeamStats defense_teamStats = home_team_has_ball ? saveState.awayTeamStats : saveState.homeTeamStats;

            ConsoleColor ccAttr = ConsoleColor.DarkYellow;
            ConsoleColor ccTitle = ConsoleColor.DarkCyan;

            // Offense //
            List<ConsoleColorString> dictCcStringOff = [];
            Dictionary<string, RosterPosition> _starters = home_team_has_ball ? saveState.P1OffensiveStarters : saveState.P2OffensiveStarters;

            // title
            dictCcStringOff.Add(new ConsoleColorString(offense_teamStats.TeamLabel + " - OFFENSE", ccTitle));

            // QB
            string title = string.Empty;
            foreach (Player.Attributes pa in QbDisplayPlayerAttributes)
                title += $"{pa} ";
            dictCcStringOff.Add(new ConsoleColorString(title.PadLeft(title.Length + len_plyrName + 5), ccAttr)); // 1 trailing space + 4 from starting, e.g. " RB1 "
            UpdateDisplayPosGroup(dictCcStringOff, offense_teamStats, RosterPosition.QB1, RosterPosition.QB2, QbDisplayPlayerAttributes, _starters);

            // Hands
            title = string.Empty;
            foreach (Player.Attributes pa in HandsDisplayPlayerAttributes)
                title += $"{pa} ";
            dictCcStringOff.Add(new ConsoleColorString(title.PadLeft(title.Length + len_plyrName + 5), ccAttr)); // 1 trailing space + 4 from starting, e.g. " RB1 "
            UpdateDisplayPosGroup(dictCcStringOff, offense_teamStats, RosterPosition.RB1, RosterPosition.RB4, HandsDisplayPlayerAttributes, _starters);
            dictCcStringOff.Add(new ConsoleColorString(string.Empty));
            UpdateDisplayPosGroup(dictCcStringOff, offense_teamStats, RosterPosition.WR1, RosterPosition.WR4, HandsDisplayPlayerAttributes, _starters);
            dictCcStringOff.Add(new ConsoleColorString(string.Empty)); 
            UpdateDisplayPosGroup(dictCcStringOff, offense_teamStats, RosterPosition.TE1, RosterPosition.TE2, HandsDisplayPlayerAttributes, _starters);

            // OL
            title = string.Empty;
            foreach (Player.Attributes pa in OlDisplayPlayerAttributes)
                title += $"{pa} ";
            dictCcStringOff.Add(new ConsoleColorString(title.PadLeft(title.Length + len_plyrName + 5), ccAttr));
            UpdateDisplayPosGroup(dictCcStringOff, offense_teamStats, RosterPosition.OL1, RosterPosition.OL5, OlDisplayPlayerAttributes, _starters);

            // K (S/T)
            title = string.Empty;
            foreach (Player.Attributes pa in K_DisplayPlayerAttributes)
                title += $"{pa} ";
            dictCcStringOff.Add(new ConsoleColorString(title.PadLeft(title.Length + len_plyrName + 5), ccAttr));
            UpdateDisplayPosGroup(dictCcStringOff, offense_teamStats, RosterPosition.K1, RosterPosition.K1, K_DisplayPlayerAttributes, _starters);

            // Defense //
            List<ConsoleColorString> dictCcStringDef = [];
            title = string.Empty;
            dictCcStringDef.Add(new ConsoleColorString(defense_teamStats.TeamLabel + " - DEFENSE", ccTitle));
            foreach (Player.Attributes pa in DefenseDisplayPlayerAttributes)
                title += $"{pa} ";
            dictCcStringDef.Add(new ConsoleColorString(title.PadLeft(title.Length + len_plyrName + 5), ccAttr));
            // def players
            UpdateDisplayPosGroup(dictCcStringDef, defense_teamStats, RosterPosition.DL1, RosterPosition.DL3, DefenseDisplayPlayerAttributes, _starters);
            dictCcStringDef.Add(new ConsoleColorString(string.Empty)); 
            UpdateDisplayPosGroup(dictCcStringDef, defense_teamStats, RosterPosition.LB1, RosterPosition.LB4, DefenseDisplayPlayerAttributes, _starters);
            dictCcStringDef.Add(new ConsoleColorString(string.Empty)); 
            UpdateDisplayPosGroup(dictCcStringDef, defense_teamStats, RosterPosition.DB1, RosterPosition.DB4, DefenseDisplayPlayerAttributes, _starters);
            // KR/PR
            dictCcStringDef.Add(new ConsoleColorString(string.Empty));
            PlayerCondition plyCond = defense_teamStats._dictStatsByRosterPos[RosterPosition.OL5].playerCondition;
            int val = Rom.GetTeams().Where(x => x.Label == defense_teamStats.TeamLabel).Single().Roster[RosterPosition.OL5].dictAttributes[Attributes.MS];
            int attr_val = GetAttributeValueAdjustedForCondition(val, plyCond);
            dictCcStringDef.Add(new ConsoleColorString($"KR MS (OL5): {attr_val.ToString().PadLeft(2)} ({plyCond})", ccAttr));
            plyCond = defense_teamStats._dictStatsByRosterPos[RosterPosition.DB4].playerCondition;
            val = Rom.GetTeams().Where(x => x.Label == defense_teamStats.TeamLabel).Single().Roster[RosterPosition.DB4].dictAttributes[Attributes.MS];
            attr_val = GetAttributeValueAdjustedForCondition(val, plyCond);
            dictCcStringDef.Add(new ConsoleColorString($"PR MS (DB4): {attr_val.ToString().PadLeft(2)} ({plyCond})", ccAttr));
            Console.Clear();

            if (home_team_has_ball)
            {
                PrintColoredStrings(dictCcStringOff);
                Console.WriteLine();
                PrintColoredStrings(dictCcStringDef);
            }
            else
            {
                PrintColoredStrings(dictCcStringDef);
                Console.WriteLine();
                PrintColoredStrings(dictCcStringOff);
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(Environment.NewLine);
            Console.WriteLine("Watching...");
            Console.WriteLine("Press enter to exit.");
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            log.Trace($"Changed: {e}");
            UpdateConsoleDisplay();
        }
    }
}
