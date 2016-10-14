using forwarder;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
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

namespace monitor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private ObservableCollection<SocketView> _sockets = new ObservableCollection<SocketView>();
        private int _total;

        public MainWindow()
        {
            InitializeComponent();

            TcpForwarderSlim forwarderSlim = new TcpForwarderSlim(SyncSockets, UpdateRequest);
            new Thread(() =>
            {
                forwarderSlim.Start(new IPEndPoint(IPAddress.Any, 8090),
                    new IPEndPoint(IPAddress.Parse("10.30.138.135"), 8090));
            }).Start();
        }

        public void SyncSockets()
        {
            Dispatcher.Invoke(() =>
            {
                for (int i = 0; i < TcpForwarderSlim.Sockets.Count; i++)
                {
                    var socket = TcpForwarderSlim.Sockets[i];
                    if (Sockets.Count < i + 1)
                        Sockets.Add(new SocketView() { Handle = socket.Handle, Socket = socket });
                    else if (Sockets[i].Handle != socket.Handle)
                    {
                        Sockets.RemoveAt(i);
                        Sockets.Insert(i, new SocketView() { Handle = socket.Handle, Socket = socket });
                    }
                }
                while (Sockets.Count > TcpForwarderSlim.Sockets.Count)
                {
                    Sockets.RemoveAt(Sockets.Count - 1);
                }
                Total = Sockets.Count;
            });
        }

        public void UpdateRequest(IntPtr handler, string request)
        {
            Dispatcher.Invoke(() =>
            {
                var socket = Sockets.First(x => x.Handle == handler);
                socket.Request = request;
            });
        }

        public ObservableCollection<SocketView> Sockets
        {
            get { return _sockets; }
            set { _sockets = value; }
        }

        public int Total
        {
            get { return _total; }
            set
            {
                _total = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SocketView : INotifyPropertyChanged
    {
        private string _request;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Request
        {
            get { return _request; }
            set
            {
                _request = value;
                OnPropertyChanged();
            }
        }

        public IntPtr Handle { get; set; }
        public Socket Socket { get; set; }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}