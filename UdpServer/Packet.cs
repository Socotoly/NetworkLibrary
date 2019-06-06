using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace UdpServer
{
    [Serializable]
    public class Packet
    {
        public enum Type : byte
        {
            KeepAlive=0,
            Data=1,
            ARQ=2,
            Connect=3,
            AcceptConnect=4,
            Rate=5
        }

        public uint SequenceNumber;
        public uint RTT;
        private byte[] SequenceNumberByte = new byte[4];
        private byte[] RTTByte = new byte[2];
        private byte[] TypeByte = new byte[1];
        private byte[] RateByte = new byte[2];
        public byte[] Data;
        public new Type GetType;

        public Packet(Type Type, byte[] Data = null, uint RTT = 0, uint SequenceNumber = 0)
        {
            switch (Type)
            {
                case Type.KeepAlive:
                    Payload = new byte[] { (byte)Type };
                    break;
                case Type.Data:
                    break;
                case Type.ARQ:
                    this.SequenceNumber = SequenceNumber;
                    TypeByte = BitConverter.GetBytes((byte)Type);
                    SequenceNumberByte = BitConverter.GetBytes(SequenceNumber);
                    Payload = new byte[TypeByte.Length + SequenceNumberByte.Length];
                    Payload = Combine(TypeByte, SequenceNumberByte);
                    break;
                case Type.Connect:
                    Payload = new byte[] { (byte)Type };
                    break;
                case Type.AcceptConnect:
                    Payload = new byte[] { (byte)Type };
                    break;
                case Type.Rate:
                    break;
            }

           
            if (Type == Type.Data)
            {
                this.Data = Data;
                this.SequenceNumber = SequenceNumber;
                this.RTT = RTT;
                GetType = Type;

                SequenceNumberByte = BitConverter.GetBytes(SequenceNumber);
                RTTByte = BitConverter.GetBytes(RTT);
                TypeByte = BitConverter.GetBytes((byte)Type);

                Payload = new byte[RTTByte.Length + SequenceNumberByte.Length + Data.Length + TypeByte.Length];
                //Console.WriteLine(Payload.Length);
                this.Payload = Combine(TypeByte, SequenceNumberByte, RTTByte, Data);
            }
            
           
            if (Type == Type.Rate)
            {
                TypeByte = new byte[] { (byte)Type };

                Payload = new byte[4];

                Payload = Combine(TypeByte, Data);
                //Payload = new byte[] { (byte)Type + Data };
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

        public async static Task<Packet> Serialize(byte[] data)
        {
            var type = (Type) data[0];

            switch (type)
            {
                case Type.KeepAlive:
                    return new Packet(type);
                case Type.Data:
                    var seq = BitConverter.ToUInt32( new byte[4] { data[2], data[3], data[4], data[5] });
                    //var seq = (uint) (data[1] + data[2] + data[3] + data[4]);
                    var rtt = BitConverter.ToUInt16(new byte[2] { data[6], data[7] });
                    var Data = new byte[data.Length - 4 - 2 - 2];

                    //Console.WriteLine("Size is: /" + Data.Length + "/");

                    int c = 8;
                    for(int i = 0; i < Data.Length; i++)
                    {
                        Data[i] = data[c];
                        c++;
                    }

                    return new Packet(type, Data, rtt, seq);
                case Type.ARQ:
                    var se = BitConverter.ToUInt32(new byte[4] { data[2], data[3], data[4], data[5] });

                    return new Packet(type, null, 0, se);
                case Type.Connect:
                    return new Packet(type);
                case Type.AcceptConnect:
                    return new Packet(type);
                case Type.Rate:
                    return new Packet(type);
                default:
                    return new Packet(type);
            }
        }
    }
}
