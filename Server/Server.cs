using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UdpServer;

namespace Server
{
    class Server
    {
        UdpClient listener;
        IPEndPoint groupEP;
        int DataPackets = 0;
        private UInt16 Rate1s = 0;
        private uint Rate8s = 0;
        private Stopwatch WatchRate1s = new Stopwatch();
        private Stopwatch WatchRate8s = new Stopwatch();
        private SortedList<uint, Packet> Buffer = new SortedList<uint, Packet>();
        public uint ARQDelay = 1000;
        private uint PrevPacket = 0;
        private uint CurrPacket = 0;
        private List<uint> LostPackets = new List<uint>();
        private Queue<byte[]> ProccessPacketBuffer = new Queue<byte[]>();

        public Server(int port)
        {
            listener = new UdpClient(port);
            Console.WriteLine("server is ready");

            new Thread(SendARQ).Start();

            var sentence = new string("sawefwewffffffffffffffffffffffffffffffffffffffffffffffffffffffffw sawefwewffffffffffffffffffffffffffffffffffffffffffffffffffffffffw sawefwewffffffffffffffffffffffffffffffffffffffffffffffffffffffffw sawefwewffffffffffffffffffffffffffffffffffffffffffffffffffffffffw sawefwewffffffffffffffffffffffffffffffffffffffffffffffffffffffffw sawefwewffffffffffffffffffffffffffffffffffffffffffffffffffffffffw sawefwewffffffffffffffffffffffffffffffffffffffffffffffffffffffffw sawefwewffffffffffffffffffffffffffffffffffffffffffffffffffffffffw");
            var Data = Encoding.ASCII.GetBytes(sentence);
            //Console.WriteLine(Data.Length);

            for (int i = 1; i < 1000000; i++)
            {
                Random rnd = new Random();
                var seq = rnd.Next(1, 10000);
                var packet = new Packet(Packet.Type.Data, Data, 80, (uint)seq);

                LostPackets.Add(packet.SequenceNumber);
            }

            long size = 0;
            object o = LostPackets;
            using (Stream s = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(s, o);
                size = s.Length;
            }

            //new Thread(CleanUpLostPackets).Start();
            new Thread(StartListener).Start();
            new Thread(ProcessDataPacket).Start();

            //StartListener(port);
        }

        public void Close()
        {
            listener.Close();
        }

        private void SendARQ()
        {
            while (true)
            {
                if(LostPackets.Count > 0)
                {
                    LostPackets.Sort();
                    
                    var packet = new Packet(Packet.Type.ARQ, null, 0, LostPackets[0]);

                    //listener.Send(packet.Payload, packet.Payload.Length, groupEP);

                    LostPackets.RemoveAt(0);

                    LostPackets.Sort();

                }
            }
        }

        private void CalculateDataRate() {
            while (true) {
                if (DataPackets > 0) {
                    if (!WatchRate1s.IsRunning) {
                        WatchRate1s.Start();
                        //WatchRate8s.Start();
                    }
                    if (WatchRate1s.ElapsedMilliseconds > 5000) {
                        var rate1s = Rate1s / 5;
                        Console.WriteLine(WatchRate1s.ElapsedMilliseconds);
                        Console.WriteLine(rate1s);
                        Rate1s = 0;
                        WatchRate1s.Restart();

                        var RateByte = new byte[2];

                        RateByte = BitConverter.GetBytes(rate1s);

                        var data = new byte[] { 1, RateByte[0], RateByte[1] };

                        var packet = new Packet(Packet.Type.Rate, data);

                        listener.Send(packet.Payload, packet.Payload.Length, groupEP);

                    }
                }

            }
        }

        private void StartListener()
        {
            try
            {
                while (true)
                {
                    byte[] bytes = listener.Receive(ref groupEP);

                    if(bytes[0] == 0)
                    {
                        var packet = new Packet(Packet.Type.KeepAlive);

                        listener.Send(packet.Payload, packet.Payload.Length, groupEP);
                    }
                    if (bytes[0] == 3)
                    {
                        var packet = new Packet(Packet.Type.AcceptConnect);

                        listener.Send(packet.Payload, packet.Payload.Length,groupEP);

                        Thread.Sleep(5);

                        listener.Send(packet.Payload, packet.Payload.Length, groupEP);
                    }
                    if (bytes[0] == 1)
                    {
                        Rate1s++;
                        Rate8s++;
                        DataPackets++;
                        //RewriteLine(2, "Packets: " + DataPackets);
                        //RewriteLine(3, "Rate1s: " + Rate1s);
                        //RewriteLine(4, "Rate8s: " + Rate8s / 8);

                        ProccessPacketBuffer.Enqueue(bytes);

                        if(ProccessPacketBuffer.Count > 1000)
                        {
                            ProccessPacketBuffer.Dequeue();
                        }
                    }
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
            }
        }

        private async void ProcessDataPacket()
        {
            while (true)
            {
                if(ProccessPacketBuffer.Count > 0)
                {
                    var bytes = ProccessPacketBuffer.Dequeue();

                    var packet = await Packet.Serialize(bytes);

                    if (!Buffer.ContainsKey(packet.SequenceNumber))
                    {
                        Buffer.Add(packet.SequenceNumber, packet);
                    }

                    if (Buffer.Count > 1000)
                    {
                        Buffer.RemoveAt(0);
                    }

                    if (PrevPacket == 0)
                    {
                        PrevPacket = packet.SequenceNumber;
                    }

                    CurrPacket = packet.SequenceNumber;

                    if (CurrPacket == PrevPacket)
                    {

                    }
                    else
                    {
                        if (!(CurrPacket == PrevPacket + 1))
                        {
                            var diff = CurrPacket - PrevPacket - 1;

                            for (uint i = 1; i < diff; i++)
                            {
                                LostPackets.Add(PrevPacket + i);
                            }
                        }
                    }

                    PrevPacket = CurrPacket;
                    CurrPacket = 0;
                }
            }
            
        }

        public static void RewriteLine(int lineNumber, String newText)
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, lineNumber);
            Console.Write(newText); Console.WriteLine(new string(' ', Console.WindowWidth - newText.Length));
            Console.SetCursorPosition(0, currentLineCursor);
        }

        private void CleanUpLostPackets()
        {
            while (true)
            {
                if(LostPackets.Count > 0)
                {
                    for (int x = LostPackets.Count - 1; x > -1; x--)
                    {
                        if (Buffer.ContainsKey(LostPackets[x]))
                        {
                            LostPackets.RemoveAt(x);
                        }
                    }
                }
            }
        }
    }
}
