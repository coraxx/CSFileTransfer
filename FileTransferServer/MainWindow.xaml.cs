using System;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;

namespace FileTransferServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {
        #region Fields and properties

        private readonly FileTransfer.Receive _receive;

        private double _progreesbarVal;
        public double ProgressbarVal
        {
            get { return _progreesbarVal; }
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
            _receive = new FileTransfer.Receive();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            _receive.ProgressPercent += delegate (object o, double progress) { ProgressbarVal = progress; };
            _receive.StatusMessage += delegate (object o, string status) { UpdateStatus(status); };
            _receive.Cleanup += delegate { ReceiveButton.Dispatcher.BeginInvoke(new Action(() => { ReceiveButton.IsEnabled = true; })); };
        }

        private void ReceiveButton_Click(object sender, RoutedEventArgs e)
        {
            ReceiveButton.IsEnabled = false;
            Listen();
        }

        private void UpdateStatus(string status)
        {
            StatusLabel.Dispatcher.BeginInvoke(new Action(() => { StatusLabel.Content = status; }));
        }

        private void Listen()
        {
            Task.Run(delegate { _receive.ReceiveFile(IPAddress.Loopback, 8080, AppDomain.CurrentDomain.BaseDirectory); });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
