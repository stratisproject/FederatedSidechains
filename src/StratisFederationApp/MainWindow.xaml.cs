using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NBitcoin.DataEncoders;
//using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Networks;
//using Stratis.Sidechains.Networks;

namespace StratisFederationApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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
                StartInfo = new ProcessStartInfo("cmd.exe", "/c dotnet netcoreapp2.1/FederationSetup.dll p -passphrase=\"test\"")
                //StartInfo = new ProcessStartInfo("dotnet", "netcoreapp2.1/FederationSetup.dll")

                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

        proc.Start();

        string sdtOut = null;

        //proc.WaitForExit(3500);
        sdtOut = proc.StandardOutput.ReadToEnd();

        proc.WaitForExit(3500);

            MessageBox.Show(sdtOut,
                "Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
        }

        private void ButtonGenKeys_ContextMenuClosing(object sender, ContextMenuEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
