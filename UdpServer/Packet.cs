using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Reflection;

namespace UdpServer
{
    public class Packet
    {
        public enum Type : byte
        {
            KeepAlive=0,
            Data=1,
            ARQ=2,
            Connect=3,
            AcceptConnect=4
        }

        public uint SequenceNumber;
        public uint RTT;
        private byte[] SequenceNumberByte = new byte[4];
        private byte[] RTTByte = new byte[2];
        private byte[] TypeByte = new byte[1];
        public byte[] Data;

        public Packet(Type Type, byte[] Data = null, uint RTT = 0, uint SequenceNumber = 0)
        {
            if(Type == Type.KeepAlive)
            {
                Payload = new byte[] { (byte)Type };
            }
            if (Type == Type.Data)
            {
                this.Data = Data;
                this.SequenceNumber = SequenceNumber;
                this.RTT = RTT;

                SequenceNumberByte = BitConverter.GetBytes(SequenceNumber);
                RTTByte = BitConverter.GetBytes(RTT);
                TypeByte = BitConverter.GetBytes((byte)Type);

                Payload = new byte[RTTByte.Length + SequenceNumberByte.Length + Data.Length + TypeByte.Length];
                Console.WriteLine(Payload.Length);
                this.Payload = Combine(TypeByte, SequenceNumberByte, RTTByte, Data);
            }
            if (Type == Type.ARQ)
            {

            }
            if(Type == Type.Connect)
            {
                Payload = new byte[] { (byte)Type };
            }
            if (Type == Type.AcceptConnect)
            {
                Payload = new byte[] { (byte)Type };
            }


        }

        public byte[] Payload { get; }

        public uint Size { get => (uint)Payload.Length; }

        private byte[] Combine(params byte[][] arrays)
        {
            byte[] rv = new byte[Size];
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
