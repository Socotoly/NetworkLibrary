using System;
using System.Threading.Tasks;

namespace PacketFactory
{
    [Serializable]
    public class Packet
    {
        public enum Type : byte
        {
            KeepAlive=0,
            Data=1,
            ARQRequest=2,
            Connect=3,
            AcceptConnect=4,
            Rate=5,
            ARQResponse=6
        }

        public uint SequenceNumber;
        public uint RTT;
        public byte[] Data;
        public new Type GetType;
        public int ClientID;
        public int InterfaceID;
        private byte[] SequenceNumberByte = new byte[4];
        private byte[] RTTByte = new byte[2];
        private byte[] TypeByte = new byte[1];
        private byte[] RateByte = new byte[2];
        private byte[] ClientIDByte = new byte[4];
        private byte[] InterfaceIDByte = new byte[4];
        

        public Packet(Type Type, int ClientID, int InterfaceID, byte[] Data = null, uint RTT = 0, uint SequenceNumber = 0)
        {
            GetType = Type;
            this.ClientID = ClientID;
            this.InterfaceID = InterfaceID;

            TypeByte = BitConverter.GetBytes((byte)Type);
            ClientIDByte = BitConverter.GetBytes(ClientID);
            InterfaceIDByte = BitConverter.GetBytes(InterfaceID);

            this.Data = Data;
            this.SequenceNumber = SequenceNumber;
            this.RTT = RTT;

            SequenceNumberByte = BitConverter.GetBytes(SequenceNumber);
            RTTByte = BitConverter.GetBytes(RTT);

            switch (Type)
            {
                case Type.KeepAlive:
                    {
                        Payload = new byte[] { (byte)Type };
                    }
                    break;
                case Type.Data:
                    {
                        Payload = new byte[TypeByte.Length + ClientIDByte.Length + InterfaceIDByte.Length + RTTByte.Length + SequenceNumberByte.Length + Data.Length];
                        Payload = Combine(TypeByte, ClientIDByte, InterfaceIDByte, SequenceNumberByte, RTTByte, Data);
                    }
                    break;
                case Type.ARQRequest:
                    {
                        Payload = new byte[TypeByte.Length + ClientIDByte.Length + InterfaceIDByte.Length + SequenceNumberByte.Length];
                        Payload = Combine(TypeByte, ClientIDByte, InterfaceIDByte, SequenceNumberByte);
                    }
                    break;
                case Type.Connect:
                    {
                        Payload = new byte[10];
                        Payload = Combine(TypeByte, ClientIDByte, InterfaceIDByte);
                    }
                    break;
                case Type.AcceptConnect:
                    {
                        Payload = new byte[] { (byte)Type };
                    }
                    break;
                case Type.Rate:
                    {
                        Payload = new byte[4];
                        Payload = Combine(TypeByte, Data);
                    }
                    break;
                case Type.ARQResponse:
                    {
                        Payload = new byte[RTTByte.Length + SequenceNumberByte.Length + Data.Length + TypeByte.Length];
                        Payload = Combine(TypeByte, ClientIDByte,InterfaceIDByte, SequenceNumberByte, RTTByte, Data);
                    }
                    break;
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

        public async static Task<Packet> Serialize(byte[] Payload)
        {
            var type = (Type) Payload[0];
            var clientId = BitConverter.ToInt32(new byte[4] { Payload[2], Payload[3], Payload[4], Payload[5] });
            var interfaceId = BitConverter.ToInt32(new byte[4] { Payload[6], Payload[7], Payload[8], Payload[9] });

            switch (type)
            {
                case Type.KeepAlive:
                    return new Packet(type, clientId, interfaceId);
                case Type.Data:
                    {
                        var seq = BitConverter.ToUInt32(new byte[4] { Payload[10], Payload[11], Payload[12], Payload[13] });
                        var rtt = BitConverter.ToUInt16(new byte[2] { Payload[14], Payload[15] });
                        var Data = new byte[Payload.Length - 4 - 2 - 2 - 4 - 4];

                        int c = 16;
                        for (int i = 0; i < Data.Length; i++)
                        {
                            Data[i] = Payload[c];
                            c++;
                        }
                        return new Packet(type, clientId, interfaceId, Data, rtt, seq);
                    }
                case Type.ARQRequest:
                    {
                        var seq = BitConverter.ToUInt32(new byte[4] { Payload[10], Payload[11], Payload[12], Payload[13] });

                        return new Packet(type, clientId, interfaceId, null, 0, seq);
                    }
                case Type.Connect:
                    {
                        return new Packet(type, clientId, interfaceId);
                    }
                case Type.AcceptConnect:
                    return new Packet(type, clientId, interfaceId);
                case Type.Rate:
                    return new Packet(type, clientId, interfaceId);
                case Type.ARQResponse:
                    {
                        var seq = BitConverter.ToUInt32(new byte[4] { Payload[10], Payload[11], Payload[12], Payload[13] });
                        var rtt = BitConverter.ToUInt16(new byte[2] { Payload[14], Payload[15] });
                        var Data = new byte[Payload.Length - 4 - 2 - 2 - 4 - 4];

                        int c = 16;
                        for (int i = 0; i < Data.Length; i++)
                        {
                            Data[i] = Payload[c];
                            c++;
                        }

                        return new Packet(type, clientId, interfaceId, Data, rtt, seq);
                    }
                default:
                    return new Packet(type, clientId, interfaceId);
            }
        }
    }
}
