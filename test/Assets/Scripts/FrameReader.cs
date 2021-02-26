using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Reader
{
    public class FrameReader
    {
        private readonly BinaryReader _binaryReader;

        public const byte START_OF_FRAME = 0xA0;
        public const byte END_OF_FRAME = 0xC0;
        public const byte SIZE_OF_FRAME = 105;

        public delegate void BalanserHandler(byte[] badFrame);
        public event BalanserHandler OnBalans;

        public FrameReader(Stream input)
        {
            _binaryReader = new BinaryReader(input);
        }

        public byte[] ReadFrame()
        {
            var frame = _binaryReader.ReadBytes(SIZE_OF_FRAME);
            while (!ChechFrame(frame))
            {
                OnBalans?.Invoke(frame);
                Balance();
                frame = _binaryReader.ReadBytes(SIZE_OF_FRAME);
            }
            return frame;
        }

        public static bool ChechFrame(byte[] frame) =>
            frame.Length == SIZE_OF_FRAME &&
            frame[0] == START_OF_FRAME &&
            frame[102] == 0 &&
            frame[103] == 0 &&
            frame[104] == END_OF_FRAME;

        private void Balance()
        {
            byte current = 0x00;
            byte last;
            do
            {
                last = current;
                current = _binaryReader.ReadByte();
            } while (END_OF_FRAME != last || START_OF_FRAME != current);
            _binaryReader.ReadBytes(SIZE_OF_FRAME - 1);
        }
    }
}
