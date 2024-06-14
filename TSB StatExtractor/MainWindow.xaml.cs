using Microsoft.Win32;
using System.IO;
using System.Reflection;
using System.Runtime.Intrinsics.Arm;
using System.Windows;
using TSB;

namespace TSB_Stat_Extractor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string SaveStateFileName = string.Empty;
        private string RomFileName = string.Empty;

        private TSB.TSB_StatExtractor? StatExtractor = null;

        public MainWindow()
        {
            InitializeComponent();

            // Display version in southeast corner
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            lblVersion.Content = $"v.{version}";
        }

        private void btnSaveState_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new();
            if (openFileDialog.ShowDialog() == true)
            {
                SaveStateFileName = txtSaveState.Text = openFileDialog.FileName;
            }
        }

        private void btnRom_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new();
            if (openFileDialog.ShowDialog() == true)
            {
                RomFileName = txtRom.Text = openFileDialog.FileName;
            }
        }

        private void btnExtract_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SaveStateFileName))
            {
                _ = MessageBox.Show("Please provide a Save State file", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.Yes);
                return;
            }
            else if (string.IsNullOrEmpty(RomFileName))
            {
                _ = MessageBox.Show("Please provide a ROM file", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.Yes);
                return;
            }

            StatExtractor = new(RomFileName);
            string s = StatExtractor.ExportStats(SaveStateFileName);
            txtStats.Text = s;

            // Enable export
            btnExport.IsEnabled = true;
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            DirectoryInfo dir_extract = Directory.CreateDirectory("extracts");
            string rom_name = StatExtractor != null ? StatExtractor.Rom.GetDisplayName() : string.Empty;
            string save_state_name = Path.GetFileNameWithoutExtension(SaveStateFileName);
            //string outputFileName = $"TSB_StatExtractor-{rom_name}-{save_state_name}-{DateTime.Now:yyMMdd_HHmmss}";
            string outputFileName = $"TSB_StatExtractor-{save_state_name}-{DateTime.Now:yyMMdd_HHmmss}";

            SaveFileDialog dlg = new()
            {
                FileName = outputFileName, // Default file name
                DefaultExt = ".txt", // Default file extension
                Filter = "Text documents (.txt)|*.txt", // Filter files by extension
                InitialDirectory = dir_extract.FullName
            };
            //dlg.RestoreDirectory = true;

            // Show save file dialog box
            if (dlg.ShowDialog() == true)
            {
                // Process save file dialog box results
                File.WriteAllText(dlg.FileName, txtStats.Text);
            }
        }
    }
}