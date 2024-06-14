using Microsoft.Win32;
using NLog;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TSB;
using static TSB.Player;
using static TSB.SaveState;
using static TSB.Team;

namespace TSB_Condition_Checker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private TSB_ConditionChecker conditionChecker;
        
        private int OffenseColumn = 0;
        private int DefenseColumn = 1;
        /* This is to be able to display in a console window instead //
        // P/Invoke required:
        private const UInt32 StdOutputHandle = 0xFFFFFFF5;
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(UInt32 nStdHandle);
        [DllImport("kernel32.dll")]
        private static extern void SetStdHandle(UInt32 nStdHandle, IntPtr handle);
        [DllImport("kernel32")]
        static extern bool AllocConsole();
        */

        public MainWindow()
        {
            InitializeComponent();

            //AllocConsole();

            Logger log = LogManager.GetCurrentClassLogger();
            log.Trace("Hello, World!");

            /// TSB Condition Checker ///
            string romFileName = string.Empty;
            string saveStateFileName;

            /* // Short-cut for testing:
            conditionChecker = new(@"E:\Media\ROMs\NES\TPC_TSB_tapmeter.nes");
            //conditionChecker.Start(saveStateFileName);
            conditionChecker.Start(@"D:\Program Files\anticheat V2\states\TPC_TSB_tapmeter.ns1", testingly);
            */

            MessageBoxButton buttons = MessageBoxButton.OK;

            string message = "Select ROM...";
            string caption = "Select ROM";
            _ = MessageBox.Show(message, caption, buttons);
            OpenFileDialog openFileDialog = new();
            if (openFileDialog.ShowDialog() == true)
            {
                romFileName = openFileDialog.FileName;
            }

            message = "Select save state...";
            caption = "Select save state";
            _ = MessageBox.Show(message, caption, buttons);
            openFileDialog = new();
            if (openFileDialog.ShowDialog() == true)
            {
                saveStateFileName = openFileDialog.FileName;
                conditionChecker = new(romFileName);
                conditionChecker.Start(saveStateFileName, updatedisplay);
            }
            
            log.Trace("El fin.");
        }

        public string updatedisplay()
        {
            SaveState save_state = conditionChecker.saveState;

            bool home_team_has_ball = save_state.DoesHomeTeamHaveBall();
            //
            SaveState.TeamStats home_teamStats = save_state.GetHomeTeamStats();
            SaveState.TeamStats away_teamStats = save_state.GetAwayTeamStats();

            lbl_homeTeam.Content = home_teamStats.TeamLabel + (home_team_has_ball ? " - OFFENSE" : " - DEFENSE");
            lbl_awayTeam.Content = away_teamStats.TeamLabel + (!home_team_has_ball ? " - OFFENSE" : " - DEFENSE");

            if (home_team_has_ball)
            {
                OffenseColumn = 0;
                DefenseColumn = 1;
                displayOffense(home_teamStats);
                displayDefense(away_teamStats);
            }
            else
            {
                OffenseColumn = 1;
                DefenseColumn = 0; 
                displayOffense(home_teamStats);
                displayDefense(away_teamStats);
            }

            return "hello worldz."; // any string will do
        }

        internal void displayPlayer(TeamStats teamStats, Team.RosterPosition rosterPosition, Label labelName, Label labelAttr, int column)
        {
            string team_label = teamStats.TeamLabel;
            Player player = getRomPlayer(team_label, rosterPosition);
            labelName.Content = player.FirstName + " " + player.LastName;
            labelName.SetValue(Grid.ColumnProperty, column);

            // Starters
            SaveState save_state = conditionChecker.saveState;
            Dictionary<string, RosterPosition> _starters = save_state.DoesHomeTeamHaveBall() ? save_state.GetP1Starters() : save_state.GetP2Starters();
            // Determine if starting (and where)
            string starting_pos = string.Empty;
            if (rosterPosition == _starters["QB"])
                starting_pos = "QB";
            else if (rosterPosition == _starters["RB1"])
                starting_pos = "RB1";
            else if (rosterPosition == _starters["RB2"])
                starting_pos = "RB2";
            else if (rosterPosition == _starters["WR1"])
                starting_pos = "WR1";
            else if (rosterPosition == _starters["WR2"])
                starting_pos = "WR2";
            else if (rosterPosition == _starters["TE"])
                starting_pos = "TE";

            labelAttr.Content = $"{starting_pos}  ";
            PlayerCondition playerCondition = teamStats.GetRosterPositionPlayerCondition(rosterPosition);
            List<Player.Attributes> attributes = conditionChecker.GetDisplayAttributes(player.RosterRole);
            foreach (Player.Attributes pa in attributes)
            {
                var x = player.GetAttributeValue(pa);
                int attr_val = GetAttributeValueAdjustedForCondition(x, playerCondition);
                labelAttr.Content += $"{attr_val} ";
            }
            labelAttr.SetValue(Grid.ColumnProperty, column);

            // TODO define colors
            if (playerCondition == PlayerCondition.Bad)
            {
                labelName.Foreground = labelAttr.Foreground = Brushes.Red;
            }
            else if (playerCondition == PlayerCondition.Good)
            {
                labelName.Foreground = labelAttr.Foreground = Brushes.Green;
            }
            else if (playerCondition == PlayerCondition.Excellent)
            {
                labelName.Foreground = labelAttr.Foreground = Brushes.Yellow;
            }

            // TODO - injury
            /*InjuryStatus injuryStatus = teamStats.GetRosterPositionInjuryStatus(Team.RosterPosition.QB1);
            if (injuryStatus.Equals(InjuryStatus.NotInjured))
            {
                //sb.Append($"({playerCond})");
            }
            else
                labelAttr.Content += $" ({injuryStatus})";
            */
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="team_stats"></param>
        /// <param name="offense_column">"0" or "1" depending on whether home or away team is on offense</param>
        internal void displayOffense(SaveState.TeamStats team_stats)
        {
            string title = string.Empty;
            foreach (Player.Attributes pa in conditionChecker.QbDisplayPlayerAttributes)
                title += $"{pa} ";
            lbl_QbStats.Content = title;
            lbl_QbStats.SetValue(Grid.ColumnProperty, OffenseColumn);
            
            displayPlayer(team_stats, Team.RosterPosition.QB1, lbl_Qb1, lbl_Qb1Attr, OffenseColumn);
            displayPlayer(team_stats, Team.RosterPosition.QB2, lbl_Qb2, lbl_Qb2Attr, OffenseColumn);

            title = string.Empty;
            foreach (Player.Attributes pa in conditionChecker.HandsDisplayPlayerAttributes)
                title += $"{pa} ";
            lbl_HandsStats.Content = title;

            displayPlayer(team_stats, Team.RosterPosition.RB1, lbl_Rb1, lbl_Rb1Attr, OffenseColumn);
            displayPlayer(team_stats, Team.RosterPosition.RB2, lbl_Rb2, lbl_Rb2Attr, OffenseColumn);
            displayPlayer(team_stats, Team.RosterPosition.RB3, lbl_Rb3, lbl_Rb3Attr, OffenseColumn);
            displayPlayer(team_stats, Team.RosterPosition.RB4, lbl_Rb4, lbl_Rb4Attr, OffenseColumn);

            displayPlayer(team_stats, Team.RosterPosition.WR1, lbl_Wr1, lbl_Wr1Attr, OffenseColumn);
            displayPlayer(team_stats, Team.RosterPosition.WR2, lbl_Wr2, lbl_Wr2Attr, OffenseColumn);
            displayPlayer(team_stats, Team.RosterPosition.WR3, lbl_Wr3, lbl_Wr3Attr, OffenseColumn);
            displayPlayer(team_stats, Team.RosterPosition.WR4, lbl_Wr4, lbl_Wr4Attr, OffenseColumn);

            displayPlayer(team_stats, Team.RosterPosition.TE1, lbl_Te1, lbl_Te1Attr, OffenseColumn);
            displayPlayer(team_stats, Team.RosterPosition.TE2, lbl_Te2, lbl_Te2Attr, OffenseColumn);

            title = string.Empty;
            foreach (Player.Attributes pa in conditionChecker.K_DisplayPlayerAttributes)
                title += $"{pa} ";
            lbl_KStats.Content = title;

            displayPlayer(team_stats, Team.RosterPosition.K1, lbl_K1, lbl_K1Attr, OffenseColumn);
        }

        internal void displayDefense(SaveState.TeamStats team_stats)
        {
            string title = string.Empty;
            foreach (Player.Attributes pa in conditionChecker.DefenseDisplayPlayerAttributes)
                title += $"{pa} ";
            lbl_DefStats.Content = title;

            displayPlayer(team_stats, Team.RosterPosition.DL1, lbl_Dl1, lbl_Dl1Attr, DefenseColumn);
            displayPlayer(team_stats, Team.RosterPosition.DL2, lbl_Dl2, lbl_Dl2Attr, DefenseColumn);
            displayPlayer(team_stats, Team.RosterPosition.DL3, lbl_Dl3, lbl_Dl3Attr, DefenseColumn);

            displayPlayer(team_stats, Team.RosterPosition.LB1, lbl_Lb1, lbl_Lb1Attr, DefenseColumn);
            displayPlayer(team_stats, Team.RosterPosition.LB2, lbl_Lb2, lbl_Lb2Attr, DefenseColumn);
            displayPlayer(team_stats, Team.RosterPosition.LB3, lbl_Lb3, lbl_Lb3Attr, DefenseColumn);
            displayPlayer(team_stats, Team.RosterPosition.LB4, lbl_Lb4, lbl_Lb4Attr, DefenseColumn);

            displayPlayer(team_stats, Team.RosterPosition.DB1, lbl_Db1, lbl_Db1Attr, DefenseColumn);
            displayPlayer(team_stats, Team.RosterPosition.DB2, lbl_Db2, lbl_Db2Attr, DefenseColumn);
            displayPlayer(team_stats, Team.RosterPosition.DB3, lbl_Db3, lbl_Db3Attr, DefenseColumn);
            displayPlayer(team_stats, Team.RosterPosition.DB4, lbl_Db4, lbl_Db4Attr, DefenseColumn);

            // KR/PR
            PlayerCondition plyCond = team_stats.GetRosterPositionPlayerCondition(RosterPosition.OL5);
            Player player = getRomPlayer(team_stats.TeamLabel, RosterPosition.OL5);
            int val = player.GetAttributeValue(Attributes.MS);
            int attr_val = GetAttributeValueAdjustedForCondition(val, plyCond);
            lbl_Kr.Content = $"KR MS (OL5): {attr_val,2} ({plyCond})";

            plyCond = team_stats.GetRosterPositionPlayerCondition(RosterPosition.DB4);
            player = getRomPlayer(team_stats.TeamLabel, RosterPosition.DB4);
            val = player.GetAttributeValue(Attributes.MS);
            attr_val = GetAttributeValueAdjustedForCondition(val, plyCond);
            lbl_Pr.Content = $"PR MS (DB4): {attr_val,2} ({plyCond})";
        }

        private Player getRomPlayer(string teamLabel, Team.RosterPosition rosterPosition)
        {
            return conditionChecker.Rom.GetPlayers().Single(p => p.TeamLabel.Equals(teamLabel) && p.RosterPosition == rosterPosition);
        }
    }
}