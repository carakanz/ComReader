using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Reader
{
    public class Reader
    {
        const int Size = 512 * 10;
        public int FrameCount { get; private set; } = 0;
        public class Frame
        {
            public int[] channels;
        }

        public delegate void ConnectionHandler();

        public bool Running
        {
            get => running;
            set
            {
                running = value;
                if (running)
                {
                    OnStart();
                }
                else
                {
                    OnStop();
                }    
            }
        }

        public event ConnectionHandler OnStart;
        public event ConnectionHandler OnStop;

        private static readonly Reader instance = new Reader();
        public static Reader GetInstance()
        {
            return instance;
        }

        private Reader()
        {
            Frames = Enumerable.Range(0, Size)
                .Select(_ => new Frame { channels = new int[8] })
                .ToArray();
            try
            {
                Connect();
            }
            catch (Exception exc)
            {
                Debug.LogWarning($"Connect faild: {exc.Message}");
            }
        }

        // Не передавайте сюда больше Size - 1024
        // Можно сделать иначе - брать всё фреймы, которые появились с последнего обращения.
        public Frame[] Take(int count)
        {
            // проверка на дурака.
            count = Math.Min(count, Size);
            // в index возможно прямо сейчас что-то пишется.
            int lastIndex = index - 1;
            return Enumerable.Range(0, count).Select(x => Frames[(Size + lastIndex - count + x) % Size]).ToArray();
        }

        public void Connect()
        {
            // При появлении блютуза переписать.
            var client = new TcpClient("192.168.88.2", 28832);
            var stream = client.GetStream();
            reader = new FrameReader(stream);
            Run();
            Running = true;
        }

        private Task Run()
        {
            return Task.Run(() =>
            {
                try
                {
                    while (true)
                    {
                        var data = reader.ReadFrame();
                        Frame frame = ParseFrame(data);
                        ++FrameCount;

                        index = (index + 1) % Size;
                        Frames[index] = frame;
                    }
                }
                catch (Exception exc)
                {
                    Debug.LogWarning($"Read frame exception: {exc.Message}");
                    Running = false;
                }
            });
        }

        private Frame[] Frames { get; set; }

        private int index;
        private FrameReader reader;
        private bool running;

        private Frame ParseFrame(byte[] data)
        {
            return new Frame()
            {
                channels = Enumerable.Range(0, 8)
                    .Select(x => ThreeBitToInt(data, 2 + (16 + x) * 3))
                    .ToArray()
            };
        }
        private int ThreeBitToInt(byte[] data, int offset)
        {
            return 256 * 256 * (sbyte)data[offset] + 256 * data[offset + 1] + data[offset + 2];
        }
    }
}
