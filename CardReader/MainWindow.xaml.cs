using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using PCSC;

namespace CardReader
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public IList<string> Readers { get; set; }
        public IList<string> Logs { get; set; }

        public string CurrentReader
        {
            get { return _currentReader; }
            set
            {
                _currentReader = value;
                OnPropertyChanged();
            }
        }

        public string Status
        {
            get { return _status; }
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        private readonly SCardReader _sCardReader;
        private IntPtr _protocol;
        private readonly SCardContext _sCardContext;
        private string _status;
        private string _currentReader;

        public MainWindow()
        {
            // Example of reading sms
            var msg = new byte[] { 0xD7, 0x27, 0xD3, 0x78, 0x0C, 0x3A, 0x8F, 0xFF };
            byte[] unpackedBytes = PduBitPacker.UnpackBytes(msg);
            var decodedMsg = Encoding.UTF8.GetString(unpackedBytes);

            InitializeComponent();

            Dispatcher.UnhandledException += LogError;
            DataContext = this;

            Readers = new ObservableCollection<string>();
            Logs = new ObservableCollection<string>();
            Status = "NotConnected";

            _sCardContext = new SCardContext();
            _sCardContext.Establish(SCardScope.System);
            _sCardReader = new SCardReader(_sCardContext);
        }

        private void LogError(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log("Error. Exception: {0} with message: {1}", e.Exception.GetType().Name, e.Exception.Message);
            e.Handled = true;
        }

        private void LoadReaders(object sender, EventArgs e)
        {
            var readers = _sCardContext.GetReaders();

            Log("Card readers loaded.");

            if (readers.Length > 0)
            {
                CurrentReader = readers[0];
            }

            Readers.Clear();

            foreach (var reader in readers)
            {
                Readers.Add(reader);
            }
        }

        private void ConnectCard(object sender, EventArgs e)
        {
            var result = _sCardReader.Connect(CurrentReader,
                SCardShareMode.Shared,
                SCardProtocol.Any);

            Status = result.ToString();

            Log("Card connect attempt: " + Status);

            if (result == SCardError.Success)
            {
                SetProtocol();
            }
        }

        private void SetProtocol()
        {
            switch (_sCardReader.ActiveProtocol)
            {
                case SCardProtocol.T0:
                    _protocol = SCardPCI.T0;
                    break;
                case SCardProtocol.T1:
                    _protocol = SCardPCI.T1;
                    break;
                default:
                    throw new PCSCException(SCardError.ProtocolMismatch, "Not supported protocol: " + _sCardReader.ActiveProtocol.ToString());
            }

            SendCommand(new byte[] { 0xA0, 0xA4, 0x00, 0x00, 0x02, 0x7F, 0x10 }); //przykladowa komenda
        }

        private void Log(string log, params object[] param)
        {
            Logs.Insert(0, string.Format(log + " #" + DateTime.Now, param));
        }

        private void SendCommand(byte[] command)
        {
            Log("Sending command in bytes: {0} in string: {1}", string.Join(" ", command.Select(x => string.Format("{0:X2} ", x)), Encoding.UTF8.GetString(command)));

            byte[] response = new byte[256];
            _sCardReader.Transmit(_protocol, command, ref response);

            Log("Command response in bytes: {0} in string {1}", string.Join(" ", response.Select(x => string.Format("{0:X2} ", x)), Encoding.UTF8.GetString(response)));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected override void OnClosed(EventArgs e)
        {
            _sCardContext.Dispose();
            base.OnClosed(e);
        }
    }
}
