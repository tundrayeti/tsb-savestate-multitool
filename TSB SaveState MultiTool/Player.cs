using System.Text;

namespace TSB
{
    /// <summary>
    /// 
    /// </summary>
    public class Player(string teamLabel, string rosterNum, string firstName, string lastName, Team.RosterPosition rosterPosition)
    {
        public enum Attributes
        {
            /// <summary>
            /// Rushing Speed
            /// </summary>
            RS, 
            /// <summary>
            /// Rushing Power
            /// </summary>
            RP, 
            /// <summary>
            /// Maximum Speed
            /// </summary>
            MS, 
            /// <summary>
            /// Hitting Power
            /// </summary>
            HP, PS, PC, AP, APB, BC, REC, INT, QK, KA, AKKB, PKA, APKB, race, face
        }

        /// <summary>
        /// hex -> decimal int value (x9 -> 63)
        /// </summary>
        internal static Dictionary<int, int> AttributeValues = new() {{0x0,6}, {0x1,13}, {0x2,19}, {0x3,25}, {0x4,31},
            {0x5,38}, {0x6,44}, {0x7,50}, {0x8,56}, {0x9,63}, {0xA,69}, {0xB,75}, {0xC,81}, {0xD,88} , {0xE,94} , {0xF, 100}};
        /// <summary>
        /// E.g. 63 -> x9
        /// </summary>
        internal static Dictionary<int, int> ReverseAttributeValues = AttributeValues.ToDictionary(x => x.Value, x => x.Key);

        /*
         * 	QB  => [qw'RP RS MS HP race face PS PC AP APB'],
         * 	REC => [qw'RP RS MS HP race face BC REC'],
         * 	OL  => [qw'RP RS MS HP race face'],
         * 	DEF => [qw'RP RS MS HP race face INT QK'],
         * 	K   => [qw'RP RS MS HP race face KKA AKKB'],
         * 	P   => [qw'RP RS MS HP race face PKA APKB'],
         */
        internal static List<Attributes> CoreAttributes = [Attributes.RP,
            Attributes.RS,
            Attributes.MS,
            Attributes.HP,
            Attributes.race,
            Attributes.face];

        internal static List<Attributes> QbAttributes = new(CoreAttributes);

        internal static List<Attributes> RecAttributes = new(CoreAttributes);

        internal static List<Attributes> OlAttributes = new(CoreAttributes);

        internal static List<Attributes> DefAttributes = new(CoreAttributes);

        internal static List<Attributes> K_Attributes = new(CoreAttributes);

        internal static List<Attributes> P_Attributes = new(CoreAttributes);

        public string TeamLabel { get; private set; } = teamLabel;

        /// <summary>
        /// two-digit
        /// </summary>
        internal string RosterNumber { get; private set; } = rosterNum;

        /// <summary>
        /// 
        /// </summary>
        public string FirstName { get; private set; } = firstName;

        /// <summary>
        /// 
        /// </summary>
        public string LastName { get; private set; } = lastName;

        /// <summary>
        /// Enum
        /// </summary>
        public Team.RosterPosition RosterPosition { get; private set; } = rosterPosition;

        /// <summary>
        /// Enum
        /// </summary>
        public Team.RosterRole RosterRole { get; internal set; }

        internal Dictionary<Attributes, int> dictAttributes = [];

        /// <summary>
        /// Static constructor is called at most one time, before any instance constructor is invoked or member is accessed. 
        /// </summary>
        static Player()
        {
            // Hack/less lines than defining
            QbAttributes.AddRange([Attributes.PS, Attributes.PC, Attributes.AP, Attributes.APB]);
            RecAttributes.AddRange([Attributes.BC, Attributes.REC]);
            DefAttributes.AddRange([Attributes.INT, Attributes.QK]);
            K_Attributes.AddRange([Attributes.KA, Attributes.AKKB]);
            P_Attributes.AddRange([Attributes.PKA, Attributes.APKB]);
        }

        public int GetAttributeValue(Attributes attr)
        {
            return dictAttributes[attr];
        }

        internal void Display()
        {            
            Console.WriteLine($"{RosterNumber} {FirstName} {LastName} ({RosterPosition})");
        }

        internal void DisplayDetails()
        {
            StringBuilder sb = new($"{RosterNumber} {FirstName} {LastName} ({RosterPosition}) - ");
            foreach (var attr in dictAttributes)
            {
                sb.Append($"{attr.Key}: {attr.Value} ");
            }
            Console.WriteLine(sb);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        public static List<Attributes> GetAttributes(Team.RosterRole role)
        {
            List<Attributes> returnAttribues = new(CoreAttributes);

            switch (role)
            {
                case Team.RosterRole.QB:
                    returnAttribues = QbAttributes;
                    break;

                case Team.RosterRole.RB:
                case Team.RosterRole.WR:
                case Team.RosterRole.TE:
                    returnAttribues = RecAttributes;
                    break;

                case Team.RosterRole.OL:
                    returnAttribues = OlAttributes;
                    break;

                case Team.RosterRole.DL:
                case Team.RosterRole.LB:
                case Team.RosterRole.DB:
                    returnAttribues = DefAttributes;
                    break;

                case Team.RosterRole.K:
                    returnAttribues = K_Attributes;
                    break;

                case Team.RosterRole.P:
                    returnAttribues = P_Attributes;
                    break;

                default:
                    Console.WriteLine($"ERROR");
                    break;
            }

            return returnAttribues;
        }

        // TODO probably delete
        /// <summary>
        /// E.g. "A" -> 69
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        internal static int MapAttributeValueFromHex(string hex)
        {
            // my %ATTRIB = (0=>6, 1=>13, 2=>19, 3=>25, 4=>31, 5=>38, 6=>44, 7=>50, 8=>56, 9=>63, A=>69, B=>75, C=>81, D=>88, E=>94, F=>100);
            if (string.Equals(hex, "0"))
                return 6;
            else if (string.Equals(hex, "1"))
                return 13;
            else if (string.Equals(hex, "2"))
                return 19;
            else if (string.Equals(hex, "3"))
                return 25;
            else if (string.Equals(hex, "4"))
                return 31;
            else if (string.Equals(hex, "5"))
                return 38;
            else if (string.Equals(hex, "6"))
                return 44;
            else if (string.Equals(hex, "7"))
                return 50;
            else if (string.Equals(hex, "8"))
                return 56;
            else if (string.Equals(hex, "9"))
                return 63;
            else if (string.Equals(hex, "A"))
                return 69;
            else if (string.Equals(hex, "B"))
                return 75;
            else if (string.Equals(hex, "C"))
                return 81;
            else if (string.Equals(hex, "D"))
                return 88;
            else if (string.Equals(hex, "E"))
                return 94;
            else if (string.Equals(hex, "F"))
                return 100;
            return 0;
        }
    }
}
