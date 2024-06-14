namespace TSB
{
    public class Team(string label, string city, string name)
    {
        #region enums
        public enum RosterRole
        {
            QB, RB, WR, TE, OL, DL, LB, DB, K, P
        }

        public enum RosterPosition
        {
            QB1, QB2,
            RB1, RB2, RB3, RB4,
            WR1, WR2, WR3, WR4,
            TE1, TE2,
            OL1, OL2, OL3, OL4, OL5,
            DL1, DL2, DL3,
            LB1, LB2, LB3, LB4,
            DB1, DB2, DB3, DB4,
            K1, P1
        }
        #endregion enums

        public string Label { get; private set; } = label;
        public string City { get; private set; } = city;
        public string Name { get; private set; } = name;

        public Dictionary<Team.RosterPosition, Player> Roster = [];

        #region display methods
        public void Display()
        {
            Console.WriteLine($"{City} {Name} ({Label})");
        }

        public void DisplayRoster()
        {
            foreach (Player player in Roster.Values)
            {
                player.Display();
            }
        }

        public void DisplayRosterDetails()
        {
            foreach (Player player in Roster.Values)
            {
                player.DisplayDetails();
            }
        }
        #endregion display methods

        /// <summary>
        /// Maps 'RosterPosition' to 'RosterRole', e.g. QB1 -> QB
        /// </summary>
        /// <param name="rosterPosition">Enum</param>
        /// <returns></returns>
        public static RosterRole MapPositionToRole(RosterPosition rosterPosition)
        {
            RosterRole role = RosterRole.QB;

            switch (rosterPosition)
            {
                case RosterPosition.QB1:
                case RosterPosition.QB2:
                    role = RosterRole.QB;
                    break;

                case RosterPosition.RB1:
                case RosterPosition.RB2:
                case RosterPosition.RB3:
                case RosterPosition.RB4:
                    role = RosterRole.RB;
                    break;

                case RosterPosition.WR1:
                case RosterPosition.WR2:
                case RosterPosition.WR3:
                case RosterPosition.WR4:
                    role = RosterRole.WR;
                    break;

                case RosterPosition.TE1:
                case RosterPosition.TE2:
                    role = RosterRole.TE;
                    break;

                case RosterPosition.OL1:
                case RosterPosition.OL2:
                case RosterPosition.OL3:
                case RosterPosition.OL4:
                case RosterPosition.OL5:
                    role = RosterRole.OL;
                    break;

                case RosterPosition.DL1:
                case RosterPosition.DL2:
                case RosterPosition.DL3:
                    role = RosterRole.DL;
                    break;

                case RosterPosition.LB1:
                case RosterPosition.LB2:
                case RosterPosition.LB3:
                case RosterPosition.LB4:
                    role = RosterRole.LB;
                    break;

                case RosterPosition.DB1:
                case RosterPosition.DB2:
                case RosterPosition.DB3:
                case RosterPosition.DB4:
                    role = RosterRole.DB;
                    break;

                case RosterPosition.K1:
                    role = RosterRole.K;
                    break;

                case RosterPosition.P1:
                    role = RosterRole.P;
                    break;
                
                default:
                    Console.WriteLine($"ERROR");
                    break;
            }

            return role;
        }
    }
}
