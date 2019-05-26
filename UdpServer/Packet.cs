using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Diagnostics;

namespace UdpServer
{
    class Packet
    {
        public long TimeStamp = 0;
        public int SequenceNumber;
        public int RTT;
        private byte[] SequenceNumberByte = new byte[4];
        private byte[] RTTByte = new byte[4];
        private byte[] TimeStampByte = new byte[8];
        public byte[] Data;
        public int Size;
        public Stopwatch watch;

        public Packet(int SequenceNumber, byte[] Data, int RTT, int size)
        {
            this.Data = Data;
            this.SequenceNumber = SequenceNumber;
            this.RTT = RTT;
            this.Size = size;

            Payload = new byte[size];
            //Console.WriteLine("empty packet" + Payload.Length);
            SequenceNumberByte = BitConverter.GetBytes(SequenceNumber);
            RTTByte = BitConverter.GetBytes(RTT);
            TimeStampByte = BitConverter.GetBytes(TimeStamp);

            this.Payload = Combine(SequenceNumberByte, RTTByte, TimeStampByte, Data);
            //Console.WriteLine("packet" + Payload.Length);

        }

        public byte[] Payload { get; }

        public void Send(UdpClient Client, IPEndPoint RemoteIp)
        {
            this.TimeStamp = TimestampNow();
            Console.WriteLine(TimeStamp);
            TimeStampByte = BitConverter.GetBytes(TimeStamp);
            Console.WriteLine(Payload.Length);
            Console.WriteLine(TimeStampByte.Length);
            int e = 0;
            for (int i = 8; i < 16; i++)
            {
                Payload[i] = TimeStampByte[e];
                //Console.WriteLine("i=" + i + " e=" + e);
                e++;
            }
            watch = new Stopwatch();
            watch.Start();
            
            Client.Send(this.Payload, Payload.Length, RemoteIp);
        }

        public long TimestampNow()
        {
            return (long) DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        private byte[] Combine(params byte[][] arrays)
        {
            byte[] rv = new byte[this.Size];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                System.Buffer.BlockCopy(array, 0, rv, offset, array.Length);
                offset += array.Length;
            }
            return rv;
        }

    }
}
