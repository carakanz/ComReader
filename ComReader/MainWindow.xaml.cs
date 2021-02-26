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
                PlotPoints[i] = new ObservableCollection<DataPoint>();
                for (int j = 0; j < Frequency / 32 * 30; ++j)
                {
                    PlotPoints[i].Add(new DataPoint(((double) j * 32) / Frequency, 20));
                }
            }

            Task.Run(() =>
            {
                while (true)
                {
                    Dispatcher.BeginInvoke((Action)(() =>
                    {

                        ComBuffer.Content = $"{comBuffer} ({(comBuffer - comBufferLast) * 2}/sec) byte";
                        comBufferLast = comBuffer;

                        FrameCount.Content = $"{frameCount:#,#} ({(frameCount - frameCountLast) * 2}/sec) frame";
                        frameCountLast = frameCount;

                        QueueSize.Content = $"{Data.Count} ({(Data.Count - dataSizeLast) * 2}/sec) frame";
                        dataSizeLast = Data.Count;

                        ClientsCount.Content = $"{Clients.Count}";

                    }));
                    Thread.Sleep(500);
                }
            });

            int[] enabled_plot_indexes = Enumerable.Range(0, ChannelCount)
                .Where(x => Convert.ToBoolean(ConfigurationManager.AppSettings.Get("Plot" + Convert.ToString(x))))
                .ToArray();

            Task.Run(() =>
            {
                while (true)
                {
                    Frame frame = PlotData.Take();
                    ++_timeIndex;
                    if (_timeIndex % 32 != 0)
                    {
                        continue;
                    }
                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        foreach (int index in enabled_plot_indexes)
                        {
                            PlotPoints[index].RemoveAt(0);
                            PlotPoints[index].Add(new DataPoint(((double)_timeIndex) / Frequency, frame.channels[index]));
                        }
                    }));
                }
            });

            Task.Run(() =>
            {
                ConcurrentBag<TcpClient> errorClient = new ConcurrentBag<TcpClient>(); 
                while (true)
                {
                    var data = Data.Take();
                    lock (Clients)
                    {
                        Clients.AsParallel().ForAll(client => 
                        {
                            try
                            {
                                client.Value.Write(data);
                            }
                            catch (Exception ex)
                            {
                                AddLogAsync(ex.Message);
                                errorClient.Add(client.Key);
                            }
                        });

                        while (errorClient.TryTake(out var client))
                        {
                            AddLogAsync("remove client");
                            Clients.Remove(client);
                        }
                    }
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

        public int ChannelCount { get; set; } = Convert.ToInt32(ConfigurationManager.AppSettings.Get("ChannelCount"));

        struct Frame
        {
            public int[] channels;
        }

        private Frame ParseFrame(byte[] data)
        {
            return new Frame()
            {
                channels = Enumerable.Range(0, ChannelCount)
                    .Select(x => ThreeBitToInt(data, 2 + (16 + x) * 3))
                    .ToArray()
            };
        }

        private Dictionary<TcpClient, NetworkStream> Clients { get; set; } =
            new Dictionary<TcpClient, NetworkStream>();

        private BlockingCollection<byte[]> Data { get; set; } =
            new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());
        private BlockingCollection<Frame> PlotData { get; set; } =
            new BlockingCollection<Frame>(new ConcurrentQueue<Frame>());
        public ObservableCollection<DataPoint>[] PlotPoints { get; set; } =
            new ObservableCollection<DataPoint>[8];

        private int _timeIndex = Frequency * 30;
        private int frameCount = 0;
        private int frameCountLast = 0;

        private int dataSizeLast = 0;

        private int comBuffer = 0;
        private int comBufferLast = 9;

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
                        comBuffer = serialPort.BytesToRead;
                        var data = serialPortReader.ReadFrame();
                        
                        Frame frame = ParseFrame(data);
                        ++frameCount;
                        Data.Add(data);
                        PlotData.Add(frame);

                        if (virtualSerialPortWriter != null)
                        {
                            virtualSerialPortWriter.Write(data);
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
                    var stream = client.GetStream();

                   var writer = new BinaryWriter(client.GetStream());
                    while (true)
                    {
                        var data = Data.Take();
                        stream.Write(data);
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

            TcpPortOppenButton.IsEnabled = false;

            AddLog($"Start listening for connections.");
            while (true)
            {
                try
                {
                    AddLog($"Listening for connections...");
                    var client = await listener.AcceptTcpClientAsync();
                    lock (Clients)
                    {
                        Clients[client] = client.GetStream();
                    }
                    IPEndPoint iPEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
                    AddLog($"Connect to {iPEndPoint.Address}:{iPEndPoint.Port}");
                }
                catch (Exception ex)
                {
                    ShowError(ex.Message);
                }
            }
        }

        private int ThreeBitToInt(byte[] data, int offset)
        {
            return 256 * 256 * (sbyte)data[offset] + 256 * data[offset + 1] + data[offset + 2];
        }
    }
}
