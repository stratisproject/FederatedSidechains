using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Forms;

namespace StratisFederationApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string passPhrase = "DefaultPassphrase";
        private string dataDir = null;

        public MainWindow()
        {
            InitializeComponent();
            dataDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            TextBoxPassphrase.Text = passPhrase;
            TextBoxDir.Text = dataDir;
        }

        private void ButtonQuit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void ButtonQuit_Copy_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonmMinimise_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo("cmd.exe", $"/c dotnet netcoreapp2.1/FederationSetup.dll p -passphrase={passPhrase} -datadir={dataDir}")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            proc.Start();

            string sdtOut = null;

            sdtOut = proc.StandardOutput.ReadToEnd();

            proc.WaitForExit(3500);

            TextBoxMainOutput.Text = "";
            TextBoxMainOutput.Text = sdtOut;
        }

        private void ButtonGenKeys_ContextMenuClosing(object sender, ContextMenuEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void TextBoxPassphrase_TextChanged(object sender, TextChangedEventArgs e)
        {
            passPhrase = TextBoxPassphrase.Text;
        }

        private void ButtonDirectorySelect_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            dialog.ShowDialog();
            dataDir = dialog.SelectedPath;
            TextBoxDir.Text = dataDir;
        }
    }
}
