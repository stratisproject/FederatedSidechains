using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA;
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonGenKeys_ContextMenuClosing(object sender, ContextMenuEventArgs e)
        {
            var mnemonicForSigningKey = new Mnemonic(Wordlist.English, WordCount.Twelve);
            PubKey signingPubKey = mnemonicForSigningKey.DeriveExtKey("test").PrivateKey.PubKey;

            // Generate keys for migning.
            var tool = new KeyTool(new DataFolder(System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)));
            NBitcoin.Key key = tool.GeneratePrivateKey();

            string savePath = tool.GetPrivateKeySavePath();
            tool.SavePrivateKey(key);
            PubKey miningPubKey = key.PubKey;

            MessageBox.Show("Do you want to close this window?",
                "Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var mnemonicForSigningKey = new Mnemonic(Wordlist.English, WordCount.Twelve);
            PubKey signingPubKey = mnemonicForSigningKey.DeriveExtKey("test").PrivateKey.PubKey;

            // Generate keys for migning.
            var tool = new KeyTool(new DataFolder(System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)));
            NBitcoin.Key key = tool.GeneratePrivateKey();

            string savePath = tool.GetPrivateKeySavePath();
            tool.SavePrivateKey(key);
            PubKey miningPubKey = key.PubKey;

            MessageBox.Show($"1. Your signing pubkey: {Encoders.Hex.EncodeData(signingPubKey.ToBytes(false))}",
                "Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
        }
    }
}
