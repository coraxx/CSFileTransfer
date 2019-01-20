using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace FileTransferClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {
        #region Fields and properties

        private readonly FileTransfer.Send _send;

        private double _progreesbarVal;
        public double ProgressbarVal
        {
            get => _progreesbarVal;
            set
            {
                _progreesbarVal = value;
                OnPropertyChanged();
            }
        }

        #endregion

        public MainWindow()
        {
            InitializeComponent();
            _send = new FileTransfer.Send();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            _send.ProgressPercent += delegate (object o, double progress) { ProgressbarVal = progress; };
            _send.StatusMessage += delegate (object o, string status) { UpdateStatus(status); };
            _send.Cleanup += delegate { SendButton.Dispatcher.BeginInvoke(new Action(() => { SendButton.IsEnabled = true; })); };
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendButton.IsEnabled = false;
            Send();
        }

        private void UpdateStatus(string status)
        {
            StatusLabel.Dispatcher.BeginInvoke(new Action(() => { StatusLabel.Content = status; }));
        }
        
        private void Send()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var result = dlg.ShowDialog();
            if (result == false) return;
            string filePath = dlg.FileName;

            Task.Run(delegate { _send.SendFile(IPAddress.Loopback, 8080, filePath); });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
