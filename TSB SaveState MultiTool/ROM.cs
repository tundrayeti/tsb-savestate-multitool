using NLog;
using System.Runtime.Intrinsics.Arm;

namespace TSB
{
    public class ROM
    {
        #region constants
        //## Constant Variables ##
        //# These are known as "SetOff X000 pointers"
        //# e.g. x07FF0 is x8000 - x10 (rom header size/length)
        const int PLYR_CONST = 32752;   //# x07FF0 -- distance between pointers and data x6DA + ? = x86CA (@ x48 ["CA86", but need to swap bytes]) -> x07FF0
        const int TEAM_CONST = 81936;   //# x14010 -- ^
        const int PLYR_CONST2 = 196624; //# x30010, calculated by finding distance between text and pointers. Holds the add'l 4 team's players name info in 32-team rom.

        //my $MEM_ADDR_PLYRATTR = 'x3010'; #(RP, RS), (MS, HP), (player face), [(PS, PC), (AP, APB)]
        const int MEM_ADDR_PLYRATTR = 0x3010; // 1 attribute after another, player after player

        //# Team Pointers (location of start of pointer banks)
        //my $PTR_TEAM_LABL = 'x01FC10'; # e.g. x01FC10 -> x01FD00
        const int PTR_TEAM_LABL = 0x01FC10;
        readonly int PTR_TEAM_CITY;
        readonly int PTR_TEAM_NAME;

        /// <summary>
        /// rename MEM_LOC_PTRS_PLYRNAME? (changes whether 28-team or 32-team)
        /// </summary>
        readonly int PTR_PLYRNAME;
        const int PTR_PLYRNAME2 = 0x3EB0; // 32-bit rom requires 2nd bank for player name data
        //# for reference from OG rom, not used currently...
        //my $MEM_ADDR_TEAM_LABL = 'x01FD00'; # e.g. BUF.
        //my $MEM_ADDR_TEAM_CITY = 'x01FD7C'; # e.g. BUFFALO
        //my $MEM_ADDR_TEAM_NAME = 'x01FE77'; # e.g. BILLS

        //# bytes per pointer offset. they come in pairs and are used to calculate distance in memory from start of set of data to start of next section
        static readonly int POINTER_LEN = 2;
        #endregion constants

        internal string FilePath { get; private set; } = string.Empty;

        private string Name { get; set; } = string.Empty;

        internal bool B32_TEAM_ROM { get; private set; } = false;

        // https://tecmobowl.org/forums/topic/9460-recording-tackles/page/2/
        // "JUMP TO TACKLE LOGIC"
        // SET (0x25aE9, 0x4C90FFEAEAEAEA) 
        /// <summary>
        /// Flag if the rom has the tackle stats hack applied or not
        /// </summary>
        internal bool TACKLE_HACK_ROM { get; private set; } = false;

        /// <summary>
        /// 28 or 32
        /// </summary>
        readonly int NUM_TEAMS;

        private byte[] FileBytes { get; set; } = [];

        /// <summary>
        /// List of Teams on ROM (maintains order)
        /// </summary>
        private readonly List<Team> Teams = [];

        private readonly List<Player> Players = [];

        readonly Logger log = LogManager.GetCurrentClassLogger();

        public ROM() { }

        /// <summary>
        /// Process ROM at path, SetTeams(), SetPlayers()
        /// </summary>
        /// <param name="path">Path of TSB ROM file</param>
        public ROM(string path)
        {
            log.Trace("ROM(string path)");
            FilePath = path;
            try {
                FileBytes = File.ReadAllBytes(FilePath);
            }
            catch(Exception ex)
            {
                log.Error(ex.Message);
                throw;
            }

            Name = Path.GetFileName(path);

            // Determine 32-team
            B32_TEAM_ROM = Check32Team();

            PTR_TEAM_CITY = B32_TEAM_ROM ? 0x01FC54 : 0x01FC50; // # bc LABL bank is 4 teams (bytes) bigger
            PTR_TEAM_NAME = B32_TEAM_ROM ? 0x01FC98 : 0x01FC90; // # bc LABL and CITY "
            PTR_PLYRNAME = B32_TEAM_ROM ? 0x0054 : 0x0048; // Decimal: 84, 72

            NUM_TEAMS = B32_TEAM_ROM ? 32 : 28;

            // This might not work for 32 Team rom (might be in an different spot)
            TACKLE_HACK_ROM = CheckTackleHack();

            SetTeams();
            SetPlayers();
        }

        // Getters...?
        public string GetDisplayName()
        {
            string return_name = Path.GetFileNameWithoutExtension(Name);
            if (return_name.Length > 16)
                return_name = return_name[..16];
            return return_name;
        }
        public List<Player> GetPlayers() { return Players; }
        internal List<Team> GetTeams() { return Teams; }

        /// <summary>
        /// Set Teams and info like Label (e.g. "BUF."), City (e.g. "BUFFALO), and Name ("BILLS")
        /// </summary>
        private void SetTeams()
        {
            log.Trace(System.Reflection.MethodBase.GetCurrentMethod()?.Name.ToString() + "()");

            // Set the initial location for the "current" pointers (i.e. like a cursor)
            int currTeamLabelPointer = PTR_TEAM_LABL;
            int currTeamCityPointer = PTR_TEAM_CITY;
            int currTeamNamePointer = PTR_TEAM_NAME;

            for (int i = 0; i < NUM_TEAMS; i++) // Could +2 for AFC, NFC
            {
                string team_label = GetTextFromPointer(currTeamLabelPointer, TEAM_CONST);
                log.Debug($"team_label: {team_label}");
                currTeamLabelPointer += POINTER_LEN; // To next label pointer

                string team_city = GetTextFromPointer(currTeamCityPointer, TEAM_CONST);
                log.Debug($"team_city: {team_city}");
                currTeamCityPointer += POINTER_LEN; // To next city pointer

                string team_name = GetTextFromPointer(currTeamNamePointer, TEAM_CONST);
                log.Debug($"team_name: {team_name}");
                currTeamNamePointer += POINTER_LEN; // To next name pointer

                Team team = new(team_label, team_city, team_name);
                Teams.Add(team);
            }
        }

        private void SetPlayers()
        {
            int currPlayerNamePointer = PTR_PLYRNAME;
            int currPlayerAttributeOffset = 0;
            bool bMode32 = false;

            int nTeam = 0;
            foreach (Team team in Teams)
            {
                // 32 team stuff //
                // These teams don't have players set like regular league teams
                if (team.Name.Equals("AFC") || team.Name.Equals("NFC")) {
                    nTeam++;
                    continue;
                }
                if (!bMode32 && nTeam >= 29 && NUM_TEAMS == 32)
                {
                    // Players on teams beyond the OG 28 + AFC + NFC are stored in a different location
                    currPlayerNamePointer = PTR_PLYRNAME2;
                    bMode32 = true;
                }

                foreach (Team.RosterPosition roster_position in Enum.GetValues(typeof(Team.RosterPosition)))
                {
                    //log.Debug($"roster_position: {roster_position}");

                    byte[] byteArray = [];
                    if (bMode32)
                    {
                        byteArray = GetCharacterBytesFromPointerLocation(currPlayerNamePointer, PLYR_CONST2);
                    }
                    else
                    {
                        byteArray = GetCharacterBytesFromPointerLocation(currPlayerNamePointer, -PLYR_CONST); // PLYR_CONST needs to be subtracted 
                    }

                    // Roster number
                    string roster_number = BitConverter.ToString([byteArray[0]]); // x84 -> "84" (e.g. Sterling Sharpe)
                    //log.Debug($"roster_number: {roster_number}");

                    // firstnameLASTNAME
                    string playerData = System.Text.Encoding.ASCII.GetString(byteArray[1..]);

                    string first_name = string.Empty; string last_name = string.Empty;
                    bool modeLN = false; // Have we reached LastName? (all caps)
                    foreach (char c in playerData)
                    {
                        int codeAscii = (int)c;
                        //log.Debug($"codeAscii: {codeAscii}");

                        if (modeLN || (codeAscii >= 65 && codeAscii <= 90))
                        {
                            last_name += c.ToString();
                            modeLN = true;
                        }
                        else
                            first_name += c.ToString();
                    }

                    /// Role, Attributes ///
                    /*
                     * # e.g. QBBills = A3 11 52 8C CC
                     * # RP=A, RS=3, MS=1, HP=1, race=5, face=2, PS=8, PC=C, PA=C, APB=C
                     * # RP=69, RS=25, MS=13, HP=13, race=38, face=19, PS=56, PC=81, PA=81, APB=81
                     */
                    Player player = new(team.Label, roster_number, first_name, last_name, roster_position)
                    {
                        RosterRole = Team.MapPositionToRole(roster_position)
                    };
                    //player.Display();

                    List<Player.Attributes> attributes = Player.GetAttributes(player.RosterRole);
                    int nBytes = attributes.Count / 2; // # 2 attribute values per byte

                    // Get attribute values
                    int memory_index = MEM_ADDR_PLYRATTR + currPlayerAttributeOffset; // # measured in bytes
                    List<string> attributeValues = []; // String(ascii?) version of hex, e.g. "A
                    for (int k = 0; k < nBytes; k++)
                    {
                        string hex = BitConverter.ToString([FileBytes[memory_index + k]]); // xF1 -> "F1"
                        string attrVal1 = hex[..1]; // hex.Substring(0, 1) -- "F"
                        string attrVal2 = hex.Substring(1, 1); // "1"

                        attributeValues.AddRange([attrVal1, attrVal2]);

                        currPlayerAttributeOffset++;
                    }

                    // Map attribute values to attribute, assign to player
                    for (int j = 0; j < attributes.Count; j++)
                    {
                        // Convert string representation of hex, "A", to int, 10
                        string x = attributeValues[j];
                        int val = Convert.ToInt32(x, 16);
                        player.dictAttributes.Add(attributes[j], val);
                        //log.Debug($"{attributes[j]}: {val}");
                    }

                    // Add player to Players list
                    Players.Add(player);
                    // Add player to Team
                    team.Roster.Add(roster_position, player);

                    currPlayerNamePointer += POINTER_LEN; // To next pointer
                }
                nTeam++;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pointerLocation"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        private byte[] GetCharacterBytesFromPointerLocation(int pointerLocation, int offset)
        {
            // Each offset needs to be constructed with the next byte first "swap bytes" (Idk/forget why - memory address in assembly hting)
            string s0 = BitConverter.ToString([FileBytes[pointerLocation + 0]]); // F0
            string s1 = BitConverter.ToString([FileBytes[pointerLocation + 1]]); // BC
            string s2 = BitConverter.ToString([FileBytes[pointerLocation + 2]]); // F4
            string s3 = BitConverter.ToString([FileBytes[pointerLocation + 3]]); // BC
            string hexOffset1 = s1 + s0; // string representation of hex, e.g. "BCF0"
            string hexOffset2 = s3 + s2;

            // Parse an int value from what it thinks is a Hex
            int pointer1 = int.Parse(hexOffset1, System.Globalization.NumberStyles.HexNumber); // current team's pointer
            int pointer2 = int.Parse(hexOffset2, System.Globalization.NumberStyles.HexNumber); // next team's pointer
            int length = pointer2 - pointer1; // Should always be 4, all labels are "BUF.", "N.O.", etc.

            int idxText = pointer1 + offset; // This is the location in the ROM where the text starts
            byte[] characters = new byte[length];
            for (int j = 0; j < length; j++)
            {
                characters[j] = FileBytes[idxText + j];
            }
            return characters;
        }

        /// <summary>
        /// Infers 32-teams by size of 
        /// </summary>
        /// <returns></returns>
        private bool Check32Team()
        {
            /*
             * # Checking the 2nd half of pointer value (1st in order), i.e. the '44' in '4480'. (1st half is the same, '80')
             * #  that is then swapped to x8044 and lessed by player address offset (x7ff0) to get start of player name pointers x54 for 32, x48 for OG
             * i.e. slight difference because the 32 team pointer has a larger pointer bank (more teams->more player) to get past before data
             * (if I understand correctly)
             */
            // OG='3880', 32 team='4480'
            if (FileBytes[16] == 0x44)
                return true;
            return false;
        }

        /// <summary>
        /// Checks ROM for "jump" portion of tackle hack
        /// </summary>
        /// <returns></returns>
        private bool CheckTackleHack()
        {
            // https://tecmobowl.org/forums/topic/9460-recording-tackles/page/2/
            // JUMP TO TACKLE LOGIC
            // SET (0x25aE9, 0x4C90FFEAEAEAEA)
            byte[] hack_val = [0x4C, 0x90, 0xFF, 0xEA, 0xEA, 0xEA, 0xEA];
            // We're going to check for the "jump" portion of the tackle hack to make our determination. AFAIK, worse case scenario is false positive and reporting all 0s.
            int mem_loc = 154345; // 154,345 == 0x25aE9

            int len = hack_val.Length;
            for (int i = 0; i < len; i++)
            {
                int rom_val = FileBytes[mem_loc + i];
                if (rom_val != hack_val[i])
                    return false;
            }

            return true; // The rom's values match the tackle hack
        }

        private string GetTextFromPointer(int pointerLocation, int offset)
        {
            byte[] byteArray = GetCharacterBytesFromPointerLocation(pointerLocation, offset);
            string text = System.Text.Encoding.ASCII.GetString(byteArray);
            return text;
        }
    }
}
