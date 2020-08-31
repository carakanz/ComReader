using ComReader.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.VisualBasic;
using OxyPlot;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
using System.Windows.Threading;

namespace ComReader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public const int Frequency = 512;
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            AddLog("Start. Find ports:");
            string[] ports = SerialPort.GetPortNames();
            AddLog(string.Join(" ", ports));

            for (int i = 0; i < 8; ++i)
            {
                _dbFramePlotQueues[i] = new ObservableCollection<DataPoint>();
                for (int j = 0; j < Frequency / 32 * 30; ++j)
                {
                    _dbFramePlotQueues[i].Add(new DataPoint(((double) j * 32) / Frequency, 0));
                }
            }

            Task.Run(() =>
            {
                while (true)
                {
                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        ComReaded.Content = $"{_comReaded:#,#} ({(_comReaded - _comReadedLast) * 2}/sec) frame";
                        _comReadedLast = _comReaded;

                        ComBuffer.Content = $"{_comBuffer} ({(_comBuffer - _comBufferLast) * 2}/sec) byte";
                        _comBufferLast = _comBuffer;

                        QueueSize.Content = $"{_frameQueue.Count} ({(_frameQueue.Count - _queueSizeLast) * 2}/sec) frame";
                        _queueSizeLast = _frameQueue.Count;

                        EventNumber.Content = $"{_eventQueue.Count} / {_countEvent}";

                        LastEvent.Content = _lastEvent;

                        DbCount.Content = $"{_dbCount} ({(_dbCount - _dbCountLast) * 2}/sec) frame";
                        _dbCountLast = _dbCount;

                        FrameQueueSize.Content = $"{_dbFrameQueue.Count} ({(_dbFrameQueue.Count - _dbFrameQueueSizeLast) * 2}/sec) frame";
                        _dbFrameQueueSizeLast = _dbFrameQueue.Count;

                        EventQueueSize.Content = $"{_dbEventQueue.Count} ({(_dbEventQueue.Count - _dbEventQueueSizeLast) * 2}/sec) event";
                        _dbEventQueueSizeLast = _dbEventQueue.Count;

                        while (_dbEventLogQueue.TryDequeue(out DBEvent dBEvent))
                        {
                            EventLogTextBox.Text += $"{dBEvent.EventDate.TimeOfDay}: {dBEvent.Type}\n";
                        }

                        //PlotChannel0.Series[0].ItemsSource = _dbFramePlotQueues[0];
                    }));
                    Thread.Sleep(500);
                }
            });

            bool plot0 = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("Plot0"));
            bool plot1 = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("Plot1"));
            bool plot2 = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("Plot2"));
            bool plot3 = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("Plot3"));
            bool plot4 = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("Plot4"));
            bool plot5 = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("Plot5"));
            bool plot6 = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("Plot6"));
            bool plot7 = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("Plot7"));

            Task.Run(() =>
            {
                while (true)
                {
                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        while (_dbFrameLogQueue.TryDequeue(out DBFrame dBEvent))
                        {
                            if (_timeIndex % 32 != 0)
                            {
                                ++_timeIndex;
                                continue;
                            }
                            if (plot0)
                            {
                                _dbFramePlotQueues[0].RemoveAt(0);
                                _dbFramePlotQueues[0].Add(new DataPoint(((double)_timeIndex) / Frequency, dBEvent.Channel0));
                            }
                            if (plot1)
                            {
                                _dbFramePlotQueues[1].RemoveAt(0);
                                _dbFramePlotQueues[1].Add(new DataPoint(((double)_timeIndex) / Frequency, dBEvent.Channel1));
                            }
                            if (plot2)
                            {
                                _dbFramePlotQueues[2].RemoveAt(0);
                                _dbFramePlotQueues[2].Add(new DataPoint(((double)_timeIndex) / Frequency, dBEvent.Channel2));
                            }
                            if (plot3)
                            {
                                _dbFramePlotQueues[3].RemoveAt(0);
                                _dbFramePlotQueues[3].Add(new DataPoint(((double)_timeIndex) / Frequency, dBEvent.Channel3));
                            }
                            if (plot4)
                            {
                                _dbFramePlotQueues[4].RemoveAt(0);
                                _dbFramePlotQueues[4].Add(new DataPoint(((double)_timeIndex) / Frequency, dBEvent.Channel4));
                            }
                            if (plot5)
                            {
                                _dbFramePlotQueues[5].RemoveAt(0);
                                _dbFramePlotQueues[5].Add(new DataPoint(((double)_timeIndex) / Frequency, dBEvent.Channel5));
                            }
                            if (plot6)
                            {
                                _dbFramePlotQueues[6].RemoveAt(0);
                                _dbFramePlotQueues[6].Add(new DataPoint(((double)_timeIndex) / Frequency, dBEvent.Channel6));
                            }
                            if (plot7)
                            {
                                _dbFramePlotQueues[7].RemoveAt(0);
                                _dbFramePlotQueues[7].Add(new DataPoint(((double)_timeIndex) / Frequency, dBEvent.Channel7));
                            }
                            ++_timeIndex;
                        }
                    }));
                    Thread.Sleep(500);
                }
            });
        }

        public string ComPort { get; set; } =
            ConfigurationManager.AppSettings.Get("ComPort");
        public string TcpPort { get; set; } =
            ConfigurationManager.AppSettings.Get("TcpPort");
        public string ConnectString { get; set; } =
            ConfigurationManager.AppSettings.Get("ConnectString");
        public string SeriesName { get; set; } = DateTime.Now.ToString();

        public class Frame
        {
            public byte[] data;
            public DateTime time;
        }

        private BlockingCollection<Frame> _frameQueue { get; set; } =
            new BlockingCollection<Frame>(new ConcurrentQueue<Frame>());

        public class EventType
        {
            public int type;
            public DateTime time;
        }
        private ConcurrentQueue<EventType> _eventQueue { get; set; } =
            new ConcurrentQueue<EventType>();

        private BlockingCollection<DBFrame> _dbFrameQueue { get; set; } =
            new BlockingCollection<DBFrame>(new ConcurrentQueue<DBFrame>());

        private BlockingCollection<DBEvent> _dbEventQueue { get; set; } =
            new BlockingCollection<DBEvent>(new ConcurrentQueue<DBEvent>());

        private ConcurrentQueue<DBEvent> _dbEventLogQueue { get; set; } =
            new ConcurrentQueue<DBEvent>();

        private ConcurrentQueue<DBFrame> _dbFrameLogQueue { get; set; } =
            new ConcurrentQueue<DBFrame>();
        public ObservableCollection<DataPoint>[] _dbFramePlotQueues { get; set; } =
            new ObservableCollection<DataPoint>[8];

        private int _timeIndex = Frequency * 30;
        private int _comReaded = 0;
        private int _comReadedLast = 0;
        private int _comBuffer = 0;
        private int _comBufferLast = 0;
        private int _queueSizeLast = 0;
        private int _countEvent = 0;
        private int _lastEvent = 0;
        private int _dbCount = 0;
        private int _dbCountLast = 0;

        private int _dbFrameQueueSizeLast = 0;
        private int _dbEventQueueSizeLast = 0;


        private ApplicationContext _applicationContext { get; set; }
        private DBSeries _series { get; set; }

        public void AddLog(string text)
        {
            LogTextBox.Text += $"{DateTime.Now.TimeOfDay}: {text}\n";
        }
        public async void AddLogAsync(string text)
        {
            await Dispatcher.BeginInvoke((Action)(() =>
            {
                AddLog(text);
            }));
        }

        public void ShowError(string text)
        {
            MessageBox.Show(text, "Error");
        }

        public async void ShowErrorAsync(string text)
        {
            await Dispatcher.BeginInvoke((Action)(() =>
            {
                AddLog(text);
                ShowError(text);
            }));
        }

        private SerialPort OpenSerialPort(string name)
        {
            AddLog($"Open port {name}");
            var serialPort = new SerialPort()
            {
                PortName = name,
                BaudRate = Convert.ToInt32(ConfigurationManager.AppSettings.Get("BaudRate")),
                ReadBufferSize = 1048576
            };
            // serialPort.WriteBufferSize = 1048576;
            serialPort.Open();
            AddLog($"Port {name} opened");
            return serialPort;
        }

        private async void ComConnect_Click(object sender, RoutedEventArgs e)
        {
            FrameReader serialPortReader;
            SerialPort serialPort;
            SerialPort virtualSerialPort;
            BinaryWriter virtualSerialPortWriter = null;
            try
            {
                serialPort = OpenSerialPort(ComPort);
                serialPortReader = new FrameReader(serialPort.BaseStream);
                serialPortReader.OnBalans +=
                    (byte[] frame) => AddLogAsync($"Balanse: {BitConverter.ToString(frame)}");

                string virtualSerialPortName = ConfigurationManager.AppSettings.Get("VComPort");
                if (virtualSerialPortName.Length != 0)
                {
                    virtualSerialPort = OpenSerialPort(virtualSerialPortName);
                    virtualSerialPortWriter = new BinaryWriter(virtualSerialPort.BaseStream);
                }
            }
            catch (Exception exp)
            {
                ShowError(exp.Message);
                return;
            }
            ComPortConnectButton.IsEnabled = false;
            TcpPortOppenButton.IsEnabled = true;

            await Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        _comBuffer = serialPort.BytesToRead;
                        var frame = serialPortReader.ReadFrame();
                        var frameWithDate = new Frame
                        {
                            data = frame,
                            time = DateTime.Now
                        };

                        _frameQueue.Add(frameWithDate);

                        _comReaded += 1;
                        if (virtualSerialPortWriter != null)
                        {
                            virtualSerialPortWriter.Write(frame);
                        }
                    }
                    catch (Exception exp)
                    {
                        ShowErrorAsync(exp.Message);
                    }
                }
            });
        }

        private void ReadClient(TcpClient client)
        {
            Task.Run(() =>
            {
                try
                {
                    var reader = new BinaryReader(client.GetStream());
                    while (true)
                    {
                        int eventNumber = reader.ReadInt32();
                        _eventQueue.Enqueue(new EventType()
                        {
                            type = eventNumber,
                            time = DateTime.Now
                        });
                        _countEvent++;
                        _lastEvent = eventNumber;
                    }
                }
                catch (Exception ex)
                {
                    ShowErrorAsync(ex.Message);
                }
            });
        }

        private async void TcpPortOppenButton_Click(object sender, RoutedEventArgs e)
        {
            AddLog($"Open tcp port {TcpPort}");
            TcpListener listener;
            try
            {
                listener = new TcpListener(IPAddress.Any, Convert.ToUInt16(TcpPort));
                listener.Start();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
                return;
            }

            AddLog($"Start listening for connections.");
            TcpPortOppenButton.IsEnabled = false;
            BDConnectButton.IsEnabled = true;
            while (true)
            {
                try
                {
                    AddLog($"Listening for connections...");
                    var client = await listener.AcceptTcpClientAsync();
                    IPEndPoint iPEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
                    AddLog($"Connect to {iPEndPoint.Address}:{iPEndPoint.Port}");
                    ReadClient(client);
                }
                catch (Exception ex)
                {
                    ShowError(ex.Message);
                }
            }
        }

        private void DBConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog($"Connect to mysql database\n{ConnectString}");
                var builder = new DbContextOptionsBuilder<Models.ApplicationContext>();
                builder.UseMySql(ConnectString);
                _applicationContext = new Models.ApplicationContext(builder.Options);
                BDConnectButton.IsEnabled = false;
                NameOkButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void NameOkButton_Click(object sender, RoutedEventArgs e)
        {
            AddLog($"Find name {SeriesName}");
            try
            {
                _series = _applicationContext.Series.Where(s => s.Name == SeriesName).FirstOrDefault();
                if (_series is null)
                {
                    AddLog($"Add series {SeriesName}");
                    _series = new DBSeries()
                    {
                        Name = SeriesName,
                        CreatedDate = DateTime.Now
                    };
                    _applicationContext.Series.Add(_series);
                    _applicationContext.SaveChanges();
                    AddLog($"Added series {SeriesName}");
                }
                else
                {
                    AddLog($"Found series {SeriesName}");
                }

                Task.Run(SaveFrame);

                NameOkButton.IsEnabled = false;
                StartButton.IsEnabled = true;
            }
            catch (Exception exp)
            {
                ShowError(exp.Message);
            }
        }

        private bool _runing = false;
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            _runing = !_runing;
            if (_runing)
            {
                Start();
                StartButton.Content = "Stop";
            }
            else
            {
                Stop();
                StartButton.Content = "Start";
            }
        }

        private void Stop()
        {
            _eventQueue.Enqueue(new EventType()
            {
                type = -2,
                time = DateTime.Now
            });
        }

        private int ThreeBitToInt(byte[] data, int offset)
        {
            byte[] fourBitData = new byte[] 
            { 
                data[offset],
                data[offset + 1],
                data[offset + 2],
                0
            };

            return BinaryPrimitives.ReadInt32BigEndian(fourBitData)/256;
        }

        private void SaveFrame()
        {
            while (true)
            {
                try
                {
                    var listDBFrame = new List<DBFrame>
                {
                    _dbFrameQueue.Take()
                };
                    while (_dbFrameQueue.TryTake(out DBFrame dBFrame))
                    {
                        listDBFrame.Add(dBFrame);
                        _dbFrameLogQueue.Enqueue(dBFrame);
                    }

                    var listDBEvent = new List<DBEvent>();
                    while (_dbEventQueue.TryTake(out DBEvent dBEvent))
                    {
                        listDBEvent.Add(dBEvent);
                        _dbEventLogQueue.Enqueue(dBEvent);
                    }

                    _applicationContext.Frames.AddRange(listDBFrame);
                    _applicationContext.Eventes.AddRange(listDBEvent);
                    _applicationContext.SaveChanges();
                    _dbCount += 1;
                }
                catch (Exception ex)
                {
                    ShowErrorAsync(ex.Message);
                }
            }
        }

        private void Start()
        {
            Task.Run(() =>
            {
                _comReaded = 0;
                _comReadedLast = 0;
                _comBuffer = 0;
                _comBufferLast = 0;
                _queueSizeLast = 0;
                _countEvent = 0;
                _lastEvent = 0;
                while (_frameQueue.TryTake(out Frame frame));
                _eventQueue.Clear();
                _eventQueue.Enqueue(new EventType()
                {
                    type = -1,
                    time = DateTime.Now
                });
                while (_runing)
                {
                    try
                    {
                        var frame = _frameQueue.Take();
                        Models.DBFrame dbFrame = new Models.DBFrame()
                        {
                            Channel0 = ThreeBitToInt(frame.data, 2 + (16 + 0) * 3),
                            Channel1 = ThreeBitToInt(frame.data, 2 + (16 + 1) * 3),
                            Channel2 = ThreeBitToInt(frame.data, 2 + (16 + 2) * 3),
                            Channel3 = ThreeBitToInt(frame.data, 2 + (16 + 3) * 3),
                            Channel4 = ThreeBitToInt(frame.data, 2 + (16 + 4) * 3),
                            Channel5 = ThreeBitToInt(frame.data, 2 + (16 + 5) * 3),
                            Channel6 = ThreeBitToInt(frame.data, 2 + (16 + 6) * 3),
                            Channel7 = ThreeBitToInt(frame.data, 2 + (16 + 7) * 3),
                            FrameTime = frame.time,
                            CreatedDate = DateTime.Now,
                            Series = _series
                        };
                        if (_eventQueue.TryPeek(out EventType eventType) &&
                            eventType.time < frame.time &&
                            _eventQueue.TryDequeue(out eventType))
                        {
                            dbFrame.EventNumber = eventType.type;
                            dbFrame.EventTime = eventType.time;
                            _dbEventQueue.Add(new Models.DBEvent()
                            {
                                Series = _series,
                                Type = eventType.type,
                                EventDate = eventType.time,
                                CreatedDate = DateTime.Now,
                            });
                        }
                        else
                        {
                            dbFrame.EventNumber = 0;
                            dbFrame.EventTime = frame.time;
                        }

                        _dbFrameQueue.Add(dbFrame);
                    }
                    catch (Exception ex)
                    {
                        ShowErrorAsync(ex.Message);
                    }
                }
            });
        }
    }
}
