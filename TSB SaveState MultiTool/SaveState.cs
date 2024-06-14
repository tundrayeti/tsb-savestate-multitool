using NLog;
using System.Collections;
using System.Data;
using System.Text;
using static TSB.SaveState;
using static TSB.Team;

namespace TSB
{
    public class SaveState
    {
        #region enums
        public enum Stats
        {
            Pass, PassComplete, PassTDs, PassINTs, PassYards,
            Rush, RushYards, RushTDs,
            Recs, RecYards, RecTDs,
            KR_Attempts, KR_Yards, KR_TDs,
            PR_Attempts, PR_Yards, PR_TDs,
            Sacks, INTs, INT_Yards, INT_TDs,
            XP_Attempts, XP_Hits, FG_Attempts, FG_Hits,
            Punts, PuntYards
        }

        internal enum Stadium
        {
            Home, Away
        }
        #endregion enums

        //#region constants
        /// <summary>
        /// This is a place where it is stored on the blue game stats screen at end of game, i.e. NOT a reliable place to determine team
        /// </summary>
        const int MEM_LOC_HOME_TEAM_LABEL = 3087;
        const int MEM_LOC_AWAY_TEAM_LABEL = 3119;

        // E.g. OG rom -> 0=BUF, 1B(27)=ATL
        const int MEM_LOC_HOME_TEAM_IDX = 0xA4;
        const int MEM_LOC_AWAY_TEAM_IDX = 0xA5;

        const int MEM_LOC_HOME_TEAM_SCORE = 977; // Also 2779 but stored as display character, e.g. x37 for "7"
        const int MEM_LOC_AWAY_TEAM_SCORE = 982;

        const int MEM_LOC_PLYR_STATS = 5781;

        const int MemOffsetQbStats = 10; // find something more clever?
        const int MemOffsetOffStats = 16; // find something more clever?
        const int MemOffsetDefStats = 5; // find something more clever?
        const int MemOffsetK_Stats = 4; // find something more clever?
        const int MemOffsetToAwayStats = 22; // find something more clever?

        // tackles
        const int MEM_LOC_TACKLES = 12815; // tackles_start

        // injuries and conditions
        const int MEM_LOC_HOME_TEAM_INJ = 6031; // not sure where the disconnet is, according to jstout on forums = 0x500;
        const int MEM_LOC_AWAY_TEAM_INJ = 6292; // 0x605;

        const int MEM_LOC_POSESSION_STATUS = 0xA8;

        const int MEM_LOC_P1_OFFENSE_STARTERS = 0x178B;
        const int MEM_LOC_P2_OFFENSE_STARTERS = 0x1890;

        // TODO
        // in-game... not score screen
        // 3000 generally seems to be about where "on screen" text can be found
        const int MEM_LOC_HOME_TEAM_CITY = 2734;
        const int MEM_LOC_CURR_GAME_CLOCK = 2747;
        const int MEM_LOC_AWAY_TEAM_CITY = 2754;
        const int MEM_LOC_PB_RUN_TXT = 2832;

        #region StatsRosterPositions
        static internal List<Team.RosterPosition> StatsRosterPositions = new([Team.RosterPosition.QB1,
            Team.RosterPosition.QB2,
            Team.RosterPosition.RB1,
            Team.RosterPosition.RB2,
            Team.RosterPosition.RB3,
            Team.RosterPosition.RB4,
            Team.RosterPosition.WR1,
            Team.RosterPosition.WR2,
            Team.RosterPosition.WR3,
            Team.RosterPosition.WR4,
            Team.RosterPosition.TE1,
            Team.RosterPosition.TE2,
            Team.RosterPosition.DL1,
            Team.RosterPosition.DL2,
            Team.RosterPosition.DL3,
            Team.RosterPosition.LB1,
            Team.RosterPosition.LB2,
            Team.RosterPosition.LB3,
            Team.RosterPosition.LB4,
            Team.RosterPosition.DB1,
            Team.RosterPosition.DB2,
            Team.RosterPosition.DB3,
            Team.RosterPosition.DB4,
            Team.RosterPosition.K1,
            Team.RosterPosition.P1
        ]);
        #endregion StatsRosterPositions

        //#endregion constants

        private ROM Rom { get; set; }

        internal string Path { get; private set; }

        private byte[] FileBytes { get; set; } = [];

        internal TeamStats homeTeamStats = new();

        internal TeamStats awayTeamStats = new();

        internal Dictionary<string, RosterPosition> P1OffensiveStarters = [];

        internal Dictionary<string, RosterPosition> P2OffensiveStarters = [];

        readonly Logger log = LogManager.GetCurrentClassLogger();

        /// <summary>
        ///   Constructor
        /// </summary>
        /// <param name="rom">This is where the player data is pulled from; not in save state file</param>
        /// <param name="path">path to the TSB save state file (e.g. .ns1)</param>
        internal SaveState(ROM rom, string path)
        {
            this.Rom = rom;
            this.Path = path;

            // Check file size
            long length = new System.IO.FileInfo(Path).Length;
            if (length < 10000 || length > 15000)
            {
                Console.WriteLine("Wrong file size!");
                return;
            }

            // Read in file data as bytes
            try
            {
                FileBytes = System.IO.File.ReadAllBytes(Path);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }

            //#region team labels and scores
            // Team labels
            int home_team_offset = FileBytes[MEM_LOC_HOME_TEAM_IDX]; // x1B -> 27 = ATL (OG ROM)
            int away_team_offset = FileBytes[MEM_LOC_AWAY_TEAM_IDX];
            string home_team_label = Rom.GetTeams()[home_team_offset].Label;
            string away_team_label = Rom.GetTeams()[away_team_offset].Label;

            // Team scores
            string hscore = BitConverter.ToString([FileBytes[MEM_LOC_HOME_TEAM_SCORE]]); // x84 -> "84"
            int home_team_score = int.Parse(hscore);

            string ascore = BitConverter.ToString([FileBytes[MEM_LOC_AWAY_TEAM_SCORE]]);
            int away_team_score = int.Parse(ascore);
            //log.Debug($"home_team_label: {home_team_label} home_team_score: {home_team_score} - away_team_label: {away_team_label} away_team_score: {away_team_score}");

            // TeamStats
            homeTeamStats = new()
            {
                Rom = rom,
                StadiumStatus = Stadium.Home,
                TeamLabel = home_team_label,
                TeamScore = home_team_score
            };
            awayTeamStats = new()
            {
                Rom = rom,
                StadiumStatus = Stadium.Away,
                TeamLabel = away_team_label,
                TeamScore = away_team_score
            };

            // Get, process, and store player stats
            int new_mem_loc = SetPlayerStatsOnTeamStats(MEM_LOC_PLYR_STATS, homeTeamStats);
            new_mem_loc += MemOffsetToAwayStats; // probably just a pseudo-random bit of left-over space or buffer in regard to the particular size of it
            _ = SetPlayerStatsOnTeamStats(new_mem_loc, awayTeamStats); // 6042

            if (Rom.TACKLE_HACK_ROM || Rom.B32_TEAM_ROM)
                ProcessTackles();

            ProcessInjuriesAndConditions();

            P1OffensiveStarters = GetStartingLineup(MEM_LOC_P1_OFFENSE_STARTERS);
            P2OffensiveStarters = GetStartingLineup(MEM_LOC_P2_OFFENSE_STARTERS);
        }

        //#region Statistics
        //#region ProcessStats
        /// <summary>
        /// Maps the data from the file to BaseStats objects on TeamStats instance
        /// </summary>
        /// <param name="memoryIndex"></param>
        /// <param name="teamStats"></param>
        /// <returns>Current memory index</returns>
        internal int SetPlayerStatsOnTeamStats(int memoryIndex, TeamStats teamStats)
        {
            // Starting at QBs, keep track of our location so that we can move relatively vs. absolute addresses
            int curr_mem_loc = memoryIndex;

            foreach (Team.RosterPosition pos in StatsRosterPositions)
            {
                curr_mem_loc = SetRosterPositionStats(pos, curr_mem_loc, teamStats);
            }
            // Initialize these for OL to store INJ
            for (RosterPosition i = RosterPosition.OL1; i <= RosterPosition.OL5; i++)
            {
                teamStats.SetRosterPosStats(i, new BaseStats(teamStats.TeamLabel, i));
            }

            return curr_mem_loc;
        }

        /// <summary>
        /// This is the "hub" where the other methods get called
        /// </summary>
        /// <param name="rp"></param>
        /// <param name="currentMemoryIndex"></param>
        /// <param name="teamStats"></param>
        /// <returns></returns>
        internal int SetRosterPositionStats(RosterPosition rp, int currentMemoryIndex, TeamStats teamStats)
        {
            RosterRole role = Team.MapPositionToRole(rp);

            BaseStats stats;
            if (role == RosterRole.QB)
                stats = ProcessQbStats(currentMemoryIndex, teamStats.TeamLabel, rp);
            else if (role == RosterRole.K)
                stats = ProcessKickerStats(currentMemoryIndex, teamStats.TeamLabel, rp);
            else if (role == RosterRole.P)
                stats = ProcessPunterStats(currentMemoryIndex, teamStats.TeamLabel, rp);
            else if (role == RosterRole.RB || role == RosterRole.WR || role == RosterRole.TE)
            {
                stats = ProcessOffenseStats(currentMemoryIndex, teamStats.TeamLabel, rp);
            }
            else
                stats = ProcessDefenseStats(currentMemoryIndex, teamStats.TeamLabel, rp);

            teamStats.SetRosterPosStats(rp, stats);

            int mem_offset = GetMemoryOffestFromRosterPosition(rp);
            return currentMemoryIndex + mem_offset;
        }

        private static int GetMemoryOffestFromRosterPosition(RosterPosition rosterPos)
        {
            int returnVal = 0;
            RosterRole role = Team.MapPositionToRole(rosterPos);
            switch (role)
            {
                case RosterRole.QB:
                    returnVal = MemOffsetQbStats;
                    break;

                case RosterRole.RB:
                case RosterRole.WR:
                case RosterRole.TE:
                    returnVal = MemOffsetOffStats;
                    break;

                case RosterRole.DL:
                case RosterRole.LB:
                case RosterRole.DB:
                    returnVal = MemOffsetDefStats;
                    break;

                case RosterRole.K:
                    returnVal = MemOffsetK_Stats;
                    break;

                case RosterRole.P:
                    returnVal = 0; // Last RosterPosition to be processed, handle moving cursor elsewhere... for now?
                    break;

                default:
                    Console.Write("ERROR");
                    break;
            }
            return returnVal;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="idx">Current location within the save state file, i.e. index of byte array</param>
        /// <returns></returns>
        internal QbStats ProcessQbStats(int idx, string teamLabel, Team.RosterPosition rosterPos)
        {
            int qbPassAttempts = FileBytes[idx + 0];
            int qbPassComplets = FileBytes[idx + 1];
            int qbPassTDs = FileBytes[idx + 2];
            int qbPassINTs = FileBytes[idx + 3];
            int qbPassYards1 = FileBytes[idx + 4];
            int qbPassYards2 = FileBytes[idx + 5];
            int qbPassYards = GetCombinedYards(qbPassYards1, qbPassYards2);

            int rushAttempts = FileBytes[idx + 6];
            int rushYards1 = FileBytes[idx + 7];
            int rushYards2 = FileBytes[idx + 8];
            int rushYards = GetCombinedYards(rushYards1, rushYards2);
            int rushTDs = FileBytes[idx + 9];

            QbStats qbStats = new(teamLabel, rosterPos, qbPassAttempts, qbPassComplets, qbPassTDs, qbPassINTs, qbPassYards, rushAttempts, rushYards, rushTDs);
            return qbStats;
        }

        private OffenseStats ProcessOffenseStats(int idx, string teamLabel, Team.RosterPosition rosterPos)
        {
            int recs = FileBytes[idx + 0];
            int recYards1 = FileBytes[idx + 1];
            int recYards2 = FileBytes[idx + 2];
            int recYards = GetCombinedYards(recYards1, recYards2);
            int recTDs = FileBytes[idx + 3];

            int krAttempts = FileBytes[idx + 4];
            int krYards1 = FileBytes[idx + 5];
            int krYards2 = FileBytes[idx + 6];
            int krYards = GetCombinedYards(krYards1, krYards2);
            int krTDs = FileBytes[idx + 7];

            int prAttempts = FileBytes[idx + 8];
            int prYards1 = FileBytes[idx + 9];
            int prYards2 = FileBytes[idx + 10];
            int prYards = GetCombinedYards(prYards1, prYards2);
            int prTDs = FileBytes[idx + 11];

            int rushAttempts = FileBytes[idx + 12];
            int rushYards1 = FileBytes[idx + 13];
            int rushYards2 = FileBytes[idx + 14];
            int rushYards = GetCombinedYards(rushYards1, rushYards2);
            int rushTDs = FileBytes[idx + 15];

            OffenseStats offenseStats = new(teamLabel, rosterPos, recs, recYards, recTDs, krAttempts, krYards, krTDs, prAttempts, prYards, prTDs, rushAttempts, rushYards, rushTDs);
            return offenseStats;
        }

        private DefenseStats ProcessDefenseStats(int idx, string teamLabel, Team.RosterPosition rosterPos)
        {
            int sacks = FileBytes[idx + 0];
            int ints = FileBytes[idx + 1];
            int intYards1 = FileBytes[idx + 2];
            int intYards2 = FileBytes[idx + 3];
            int intYards = GetCombinedYards(intYards1, intYards2);
            int intTDs = FileBytes[idx + 4];

            DefenseStats defenseStats = new(teamLabel, rosterPos, sacks, ints, intYards, intTDs);
            return defenseStats;
        }

        private KickerStats ProcessKickerStats(int idx, string teamLabel, Team.RosterPosition rosterPos)
        {
            int xp_attempts = FileBytes[idx + 0];
            int xp_hits = FileBytes[idx + 1];
            int fg_attempts = FileBytes[idx + 2];
            int fg_hits = FileBytes[idx + 3];

            KickerStats kickerStats = new(teamLabel, rosterPos, xp_attempts, xp_hits, fg_attempts, fg_hits);
            return kickerStats;
        }

        private PunterStats ProcessPunterStats(int idx, string teamLabel, Team.RosterPosition rosterPos)
        {
            int punts = FileBytes[idx + 0];
            int punt_yards = FileBytes[idx + 1];

            PunterStats punterStats = new(teamLabel, rosterPos, punts, punt_yards);
            return punterStats;
        }
        //#endregion ProcessStats

        //#endregion Statistics

        #region utility functions (non-TSB)
        public static int GetCombinedYards(int y1, int y2)
        {
            // A byte's value can only be 2^8 or 256 yards (or 255 since 0-based, etc.)
            // So yards are stored across 2 bytes and this is the formula for getting the total
            int y;
            if (y2 < 128)
            {
                y = (y2 * 256) + y1;
            }
            else
            {
                y = (256 - y2) * (y2 * -1) + y1;
            }

            return y;
        }

        private static int GetIntFromTwoBitsofBitArray(BitArray bitArray, int index)
        {
            bool bit1 = bitArray.Get(index);
            bool bit2 = bitArray.Get(index + 1);

            // Least sig on right
            // 00 = Bad, 01 = Average, 10 = Good, and 11 = Excellent
            if (bit1 && bit2) // 11
                return 3;
            else if (bit1) // 10
                return 2;
            else if (bit2) // 01
                return 1;
            else
                return 0; // 11
        }

        private static void ReverseBitarray(BitArray bitArray)
        {
            int length = bitArray.Length;
            int mid = (length / 2);

            for (int i = 0; i < mid; i++)
            {
                bool bit = bitArray[i];
                bitArray[i] = bitArray[length - i - 1];
                bitArray[length - i - 1] = bit;
            }
        }

        /// <summary>
        /// Get text from the specified section of the save state file (binary file stored as byte array)
        /// </summary>
        /// <param name="startingIndex"></param>
        /// <param name="field_length"></param>
        /// <returns>ASCII encoded text</returns>
        internal string GetText(int startingIndex, int field_length)
        {
            byte[] data = new byte[field_length];
            for (int i = 0; i < field_length; i++)
            {
                data[i] = FileBytes[startingIndex + i];
            }
            string text = System.Text.Encoding.ASCII.GetString(data);
            return text;
        }
        #endregion utility functions (non-TSB)        

        internal string GetGameClockForDisplay()
        {
            int mem_loc_seconds = 162;
            int mem_loc_minutes = 163;
            int mem_loc_quarter = 174; // 0-based

            string seconds = BitConverter.ToString([FileBytes[mem_loc_seconds]]);
            string minutes = BitConverter.ToString([FileBytes[mem_loc_minutes]]);
            int quarter = FileBytes[mem_loc_quarter] + 1; // 0 based

            return $"Q{quarter}: {minutes}:{seconds}";
        }

        // Set Tackle data on PlayerStats records
        // I don't think this works for 32-team (diff mem loc for hack?)
        internal void ProcessTackles()
        {
            int mem_idx = MEM_LOC_TACKLES; // 12815
            int nDefPlyrs = 11;
            // 11 defensive players on home team and away team
            for (RosterPosition i = RosterPosition.DL1; i < RosterPosition.DL1 + nDefPlyrs; i++)
            {
                homeTeamStats._dictStatsByRosterPos[i].Tackles = FileBytes[mem_idx];
                awayTeamStats._dictStatsByRosterPos[i].Tackles = FileBytes[mem_idx + nDefPlyrs];
                mem_idx++;
            }
        }

        //#region Injury enum and process f()s
        public enum InjuryStatus
        {
            NotInjured, ProbablReturn, Doubtful, Questionable
        }
        public enum PlayerCondition
        {
            Bad, Average, Good, Excellent
        }
        internal void ProcessInjuriesAndConditions()
        {
            /* Technical notes
            // Injuries and conditions are represented per player by 2 bits
            // First 3 bytes are injuries (only 12 offensive players)...
            // https://tecmobowl.org/forums/topic/4363-save-states-and-injuries/
            // 00 = not injured, 01 = probable return, 10 = doubtful, 11 = questionable
            // ... and after that conditions
            // https://tecmobowl.org/forums/topic/7855-nesticle-save-state-format/
            // QB1, QB2, RB1, RB2 // RB3, RB4, WR1, WR2 // WR3, WR4, TE1, TE2
            // 00 = Bad, 01 = Average, 10 = Good, and 11 = Excellent
            */
            //#region injuries
            // BitArray -- LEAST SIGNIFICANT FIRST, e.g. 1 = 10000000, bitArray[0]=1
            // Let's build a BitArray 3 bytes long to cover all injuries
            // Idk why it seems that new(new byte[] { int } => least sig at front/left (as "expected"), but new(new byte[] { byte } => least sig at end/right
            int nBytesInj = 3;
            BitArray bitArrayInjHome = new(new byte[nBytesInj]);
            BitArray bitArrayInjAway = new(new byte[nBytesInj]);
            for (int i = 0; i < nBytesInj; i++)
            {
                BitArray bitRevH = new(new byte[1] { FileBytes[MEM_LOC_HOME_TEAM_INJ + i] });
                BitArray bitRevA = new(new byte[1] { FileBytes[MEM_LOC_AWAY_TEAM_INJ + i] });
                ReverseBitarray(bitRevH); // The bits are in "expected" order with int[], but opposite for byte[]
                ReverseBitarray(bitRevA);
                int len = bitRevH.Length; // 8
                for (int j = 0; j < len; j++)
                {
                    int idx = (len * i) + j; // 0..7, 8..15, 16..23
                    bitArrayInjHome[idx] = bitRevH[j];
                    bitArrayInjAway[idx] = bitRevA[j];
                }
            }

            // testing (can skip reverse above)
            // *NOTE* This works because it seems to put the bit in expected order, least sig first
            //byte[] testBytes1 = new byte[3] { 1, 1, 1 };  // QB1, RB3, WR3 is INJ Questionable
            //byte[] testBytes2 = [12, 12, 12]; // QB2, RB4, WR4 is INJ Questionable
            //byte[] testBytes3 = new byte[3] { 48, 48, 48 }; // RB1, WR1, TE1 is INJ Questionable
            //byte[] testBytes4 = new byte[3] { 192, 192, 192 }; // RB2, WR2, TE2 is INJ Questionable
            //bitArrayInjHome = new(testBytes2);

            for (RosterPosition i = RosterPosition.QB1; i <= RosterPosition.TE2; i++)
            {
                // Each player gets 2 bits, so we need to double
                int idx = (int)i * 2;
                homeTeamStats._dictStatsByRosterPos[i].injuryStatus = (InjuryStatus)GetIntFromTwoBitsofBitArray(bitArrayInjHome, idx);
                awayTeamStats._dictStatsByRosterPos[i].injuryStatus = (InjuryStatus)GetIntFromTwoBitsofBitArray(bitArrayInjAway, idx);
            }
            //#endregion injuries

            //#region conditions
            // conditions
            int mem_loc_home = MEM_LOC_HOME_TEAM_INJ + 3;
            int mem_loc_away = MEM_LOC_AWAY_TEAM_INJ + 3;
            // 30 players per team / 4 players per byte = 8 rounding up
            int tot_bytes_needed = 8;

            BitArray bitArrayConditionsHome = new(new byte[tot_bytes_needed]);
            BitArray bitArrayConditionsAway = new(new byte[tot_bytes_needed]);

            // Copy what we need and format for processing
            byte[] home_plyr_cond = new byte[tot_bytes_needed];
            byte[] away_plyr_cond = new byte[tot_bytes_needed];
            for (int i = 0; i < tot_bytes_needed; i++)
            {
                home_plyr_cond[i] = FileBytes[mem_loc_home + i];
                away_plyr_cond[i] = FileBytes[mem_loc_away + i];

                BitArray bitRevH = new(new byte[1] { home_plyr_cond[i] });
                BitArray bitRevA = new(new byte[1] { away_plyr_cond[i] });

                ReverseBitarray(bitRevH); // The bits are in "expected" order with int[], but opposite for byte[]
                ReverseBitarray(bitRevA); // The bits are in "expected" order with int[], but opposite for byte[]

                int len = bitRevH.Length; // 8
                for (int j = 0; j < len; j++)
                {
                    int idx = (len * i) + j; // 0..7, 8..15, 16..23...
                    bitArrayConditionsHome[idx] = bitRevH[j];
                    bitArrayConditionsAway[idx] = bitRevA[j];
                }
            }

            for (RosterPosition i = RosterPosition.QB1; i <= RosterPosition.P1; i++)
            {
                // Each player gets 2 bits, so we need to double
                int idx = (int)i * 2;
                homeTeamStats._dictStatsByRosterPos[i].playerCondition = (PlayerCondition)GetIntFromTwoBitsofBitArray(bitArrayConditionsHome, idx);
                awayTeamStats._dictStatsByRosterPos[i].playerCondition = (PlayerCondition)GetIntFromTwoBitsofBitArray(bitArrayConditionsAway, idx);
            }
            //#endregion conditions
        }

        public bool DoesHomeTeamHaveBall()
        {
            // Determine who has possession of ball
            string hex = BitConverter.ToString([FileBytes[MEM_LOC_POSESSION_STATUS]]); // for debugging mostly
            log.Debug($"MEM_LOC_POSESSION_STATUS: {hex}");
            BitArray ba = new(new byte[] { FileBytes[MEM_LOC_POSESSION_STATUS] });
            return !ba[6];
            /* details on bits
             * 7 - LAST HAD BALL (when 7 and 6 are different (XOR) implies possession change)
             * 6 - HAS_BALL
             * 5 - QB_HAS_BALL - only for MAN players, sort of flag for WR arrows, etc.
             * 4 - FG_ATT - x10 mask
             * 3 - KICK - x08 mask
             * 2 - SPECIAL - Punt x0C mask (KICK + SPECIAL), XP x14 mask (FG_ATT + SPECIAL), INT
             * 1 - PALLETE TO USE
             * 0 - JOYPAD INDEX
             * e.g. (least significant on the right, like decimal, hex, etc.)
             * x00 = 00000000 - p1 has ball, 1st and 10, after p2 punt. Also after run play. Also start of game. Also p2 about to kick off to start game.
             * x04 = 00000100 - p1 QB (CPU) w/ ball
             * x08 = 00001000 - p1 KR (p2 has kicked off)
             * x0C = 00001100 - p1 punt (special kick)
             * x10 - 00010000 - p1 FG_ATT
             * x14 = 00010100 - p1 XP (special FG)
             * x24 = 00100100 - p1 QB (MAN) w/ ball and pass target arrows up during pass play
             * x44 = 01000100 - p2 INT (from p1 not MAN)
             * x64 = 01100100 - p2 INT (from p1 MAN)
             * x4C = 01001100 - p2 REC PUNT (after p1 kick)
             * x48 = 01001000 - p2 kickoff
             * x80 = 10000000 - p1 about to kick off to start game
             * x8B = 10001011 - p1 kickoff
             * x87 = 10000111 - p1 INT (from p2 not MAN)
             * x8F = 10001111 - p1 REC punt (after p2 kick)
             * xA7 = 10100111 - p1 INT (from p2 MAN)
             * xC3 = 11000011 - p2 RB w/ ball. Also "basic" p2 posession, e.g. after INT.
             * xC7 = 11000111 - p2 QB (CPU) w/ ball
             * xCF = 11001111 - p2 punt (special kick)
             * xCB = 11001011 - p2 KR
             * xD0 = 11010000 - p2 FG_ATT
             * xD7 = 11010111 - p2 XP (special FG)
             * xE7 = 11100111 - p2 QB (MAN) w/ ball and pass target arrows up during pass play
             */
        }

        /// <summary>
        /// E.g. if a player in AVG has PC=81, in BAD=75, etc.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="playerCondition"></param>
        /// <returns></returns>
        public static int GetAttributeValueAdjustedForCondition(int value, PlayerCondition playerCondition)
        {
            int temp_val = 0;
            switch (playerCondition)
            {
                case PlayerCondition.Average:
                    temp_val = value;
                    break;

                case PlayerCondition.Bad:
                    temp_val = value - 1;
                    break;

                case PlayerCondition.Good:
                    temp_val = value + 1;
                    break;

                case PlayerCondition.Excellent:
                    temp_val = value + 2;
                    break;

                default:
                    Console.Write("ERROR");
                    break;
            }

            // edge check
            if (temp_val < 0)
                temp_val = 0;
            else if (temp_val > 15)
                temp_val = 15;

            int return_val = Player.AttributeValues[temp_val];
            return return_val;
        }

        internal Dictionary<string, RosterPosition> GetStartingLineup(int memoryLocation)
        {
            Dictionary<string, RosterPosition> starters = [];
            
            int mem_loc = memoryLocation;
            string hex = BitConverter.ToString([FileBytes[mem_loc++]]); // xF1 -> "F1"
            string pos1 = hex[..1]; // hex.Substring(0, 1) -- "F"
            string pos2 = hex.Substring(1, 1); // "1"
            starters["QB"]  = RosterPosition.QB1 + Convert.ToInt32(pos1, 16);
            starters["RB1"] = RosterPosition.QB1 + Convert.ToInt32(pos2, 16);

            hex = BitConverter.ToString([FileBytes[mem_loc++]]);
            pos1 = hex[..1];
            pos2 = hex.Substring(1, 1);
            starters["RB2"] = RosterPosition.QB1 + Convert.ToInt32(pos1, 16);
            starters["WR1"] = RosterPosition.QB1 + Convert.ToInt32(pos2, 16);

            hex = BitConverter.ToString([FileBytes[mem_loc++]]);
            pos1 = hex[..1];
            pos2 = hex.Substring(1, 1);
            starters["WR2"] = RosterPosition.QB1 + Convert.ToInt32(pos1, 16);
            starters["TE"] = RosterPosition.QB1 + Convert.ToInt32(pos2, 16);

            hex = BitConverter.ToString([FileBytes[mem_loc++]]);
            pos1 = hex[..1];
            pos2 = hex.Substring(1, 1);
            starters["KR"] = RosterPosition.QB1 + Convert.ToInt32(pos1, 16);
            starters["PR"] = RosterPosition.QB1 + Convert.ToInt32(pos2, 16);

            return starters;
        }

        public TeamStats GetHomeTeamStats()
        {
            return homeTeamStats;
        }

        public TeamStats GetAwayTeamStats()
        {
            return awayTeamStats;
        }

        public Dictionary<string, RosterPosition> GetP1Starters()
        {
            return P1OffensiveStarters;
        }

        public Dictionary<string, RosterPosition> GetP2Starters()
        {
            return P2OffensiveStarters;
        }

        //#endregion Injury enum and process f()s

        /*
         * BaseStats and friends
         * 
         * These are "stats" classes that are used to store and manage statistics from the TSB save state file.
         * Different players have different stats (e.g. QB passing), and the different classes reflect that
         */
        //#region stats classes
        // base class
        //#region BaseStats classes and child classes
        internal class BaseStats
        {
            internal List<Stats> listStatsEnums = [];

            internal Dictionary<Stats, int> _dictStats = [];

            internal string TeamLabel;

            internal Team.RosterPosition RosterPosition;

            internal int Tackles;

            internal PlayerCondition playerCondition;

            public InjuryStatus injuryStatus { get; internal set; } // This only needs to be on OffenseStats, but complicated to figure out w/ inheritance

            internal BaseStats(string teamLabel, Team.RosterPosition rosterPosition)
            {
                TeamLabel = teamLabel;
                RosterPosition = rosterPosition;
            }

            internal virtual void ExportToStringBuilder(bool bTackleHackRom, StringBuilder _sb)
            {
                // Let's not display KR and PR data unless there is some
                bool noKR = true;
                bool noPR = true;

                foreach (Stats stat in listStatsEnums)
                {
                    int val = _dictStats[stat];

                    // Don't display empty KR or PR data
                    if (stat == Stats.KR_Attempts)
                        if (val > 0)
                            noKR = false;
                        else
                            continue;
                    else if ((stat == Stats.KR_Yards || stat == Stats.KR_TDs) && noKR)
                        continue;
                    else if (stat == Stats.PR_Attempts)
                        if (val > 0)
                            noPR = false;
                        else
                            continue;
                    else if ((stat == Stats.PR_Yards || stat == Stats.PR_TDs) && noPR)
                        continue;

                    _sb.Append($"{stat}: {val}, ");
                }

                // INJ/condition
                if (injuryStatus.Equals(InjuryStatus.NotInjured))
                    _sb.AppendLine($"COND: {playerCondition}, ");
                else
                    _sb.AppendLine($"COND: {injuryStatus}, ");
            }
        }

        internal class OffenseStats : BaseStats
        {
            internal OffenseStats(string teamLabel, Team.RosterPosition rosterPosition,
                int receptions, int recYards, int recTDs, int krAttempts, int krYards, int krTDs, int prAttempts, int prYards, int prTDs,
                    int rushAttempts, int rushYards, int rushTDs) : base(teamLabel, rosterPosition)
            {
                listStatsEnums = new([Stats.Recs, Stats.RecYards, Stats.RecTDs,
                    Stats.KR_Attempts, Stats.KR_Yards, Stats.KR_TDs,
                        Stats.PR_Attempts, Stats.PR_Yards, Stats.PR_TDs,
                            Stats.Rush, Stats.RushYards, Stats.RushTDs]);

                _dictStats[Stats.Recs] = receptions;
                _dictStats[Stats.RecYards] = recYards;
                _dictStats[Stats.RecTDs] = recTDs;

                _dictStats[Stats.KR_Attempts] = krAttempts;
                _dictStats[Stats.KR_Yards] = krYards;
                _dictStats[Stats.KR_TDs] = krTDs;

                _dictStats[Stats.PR_Attempts] = prAttempts;
                _dictStats[Stats.PR_Yards] = prYards;
                _dictStats[Stats.PR_TDs] = prTDs;

                _dictStats[Stats.Rush] = rushAttempts;
                _dictStats[Stats.RushYards] = rushYards;
                _dictStats[Stats.RushTDs] = rushTDs;
            }
        }

        internal class QbStats : BaseStats
        {
            internal QbStats(string teamLabel, Team.RosterPosition rosterPosition,
                int passAttempts, int passCompletions, int passTDs, int passINTs, int passYards, int rushAttempts, int rushYards, int rushTDs) : base(teamLabel, rosterPosition)
            {
                listStatsEnums = new([Stats.Pass,
                    Stats.PassComplete,
                    Stats.PassTDs,
                    Stats.PassINTs,
                    Stats.PassYards,
                    Stats.Rush,
                    Stats.RushYards,
                    Stats.RushTDs]);

                _dictStats[Stats.Pass] = passAttempts;
                _dictStats[Stats.PassComplete] = passCompletions;
                _dictStats[Stats.PassTDs] = passTDs;
                _dictStats[Stats.PassINTs] = passINTs;
                _dictStats[Stats.PassYards] = passYards;

                _dictStats[Stats.Rush] = rushAttempts;
                _dictStats[Stats.RushYards] = rushYards;
                _dictStats[Stats.RushTDs] = rushTDs;
            }
        }

        internal class DefenseStats : BaseStats
        {
            // TODO tackles
            internal DefenseStats(string teamLabel, Team.RosterPosition rosterPos, int sacks, int ints, int intYards, int intTDs) : base(teamLabel, rosterPos)
            {
                listStatsEnums = new([Stats.Sacks, Stats.INTs, Stats.INT_Yards, Stats.INT_TDs]);

                _dictStats[Stats.Sacks] = sacks;
                _dictStats[Stats.INTs] = ints;
                _dictStats[Stats.INT_Yards] = intYards;
                _dictStats[Stats.INT_TDs] = intTDs;
            }

            internal override void ExportToStringBuilder(bool bTackleHackRom, StringBuilder _sb)
            {
                base.ExportToStringBuilder(bTackleHackRom, _sb);
                if (bTackleHackRom)
                    _sb.AppendLine($"tackles: {Tackles}");
            }
        }

        internal class KickerStats : BaseStats
        {
            // TODO tackles
            internal KickerStats(string teamLabel, Team.RosterPosition rosterPos, int xpAttempts, int xpHits, int fgAttempts, int fgHits) : base(teamLabel, rosterPos)
            {
                listStatsEnums = new([Stats.XP_Attempts, Stats.XP_Hits, Stats.FG_Attempts, Stats.FG_Hits]);

                _dictStats[Stats.XP_Attempts] = xpAttempts;
                _dictStats[Stats.XP_Hits] = xpHits;
                _dictStats[Stats.FG_Attempts] = fgAttempts;
                _dictStats[Stats.FG_Hits] = fgHits;
            }
        }

        internal class PunterStats : BaseStats
        {
            internal PunterStats(string teamLabel, Team.RosterPosition rosterPos, int punts, int puntYards) : base(teamLabel, rosterPos)
            {
                listStatsEnums = new([Stats.Punts, Stats.PuntYards]);

                _dictStats[Stats.Punts] = punts;
                _dictStats[Stats.PuntYards] = puntYards;
            }
        }
        //#endregion BaseStats classes and child classes

        // TeamStats contains BaseStats objects, but doesn't derive from
        public class TeamStats
        {
            internal SaveState.Stadium StadiumStatus;
            /// <summary>
            /// 
            /// </summary>
            public string TeamLabel = string.Empty;
            internal int TeamScore;

            internal Dictionary<Team.RosterPosition, BaseStats> _dictStatsByRosterPos = [];

            internal ROM Rom = new();

            internal void SetRosterPosStats(RosterPosition rosterPosition, BaseStats stats)
            {
                _dictStatsByRosterPos[rosterPosition] = stats;
            }

            public int GetRosterPositionStat(RosterPosition rosterPosition, SaveState.Stats stat)
            {
                return _dictStatsByRosterPos[rosterPosition]._dictStats[stat];
            }

            public PlayerCondition GetRosterPositionPlayerCondition(RosterPosition rosterPosition)
            {
                return _dictStatsByRosterPos[rosterPosition].playerCondition;
            }

            public InjuryStatus GetRosterPositionInjuryStatus(RosterPosition rosterPosition)
            {
                return _dictStatsByRosterPos[rosterPosition].injuryStatus;
            }
        }
        //#endregion stats classes
    }
}