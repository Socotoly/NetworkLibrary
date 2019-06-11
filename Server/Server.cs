using PacketFactory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Server
{
    class Server
    {
        const int id = 1;
        const int interfaceid = 1;
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
        private Queue<byte[]> ProccessBuffer = new Queue<byte[]>();
        private SortedList<int, SortedList<int, IPEndPoint>> Clients = new SortedList<int, SortedList<int, IPEndPoint>>();
        public Server(int port)
        {
            listener = new UdpClient(port);
            Console.WriteLine("server is ready");

            //new Thread(SendARQ).Start();
            //new Thread(CleanUpLostPackets).Start();
            new Thread(StartListener).Start();
            //new Thread(ProcessDataPacket).Start();



            //Thread.Sleep(20000);

            Console.ReadLine();
            //StartListener(port);
        }

        public void Close()
        {
            listener.Close();
        }

        private async void SendARQ()
        {
            //while (true)
            //{
                //if(LostPackets.Count > 0)
                //{
                    //LostPackets.Sort();
                    
                    var packet = new Packet(Packet.Type.ARQRequest, id, interfaceid, null, 0, LostPackets[0]);

                    if (! groupEP.Equals(null))
                    {
                        listener.Send(packet.Payload, packet.Payload.Length, groupEP);
                    }

                    LostPackets.Remove(LostPackets[0]);

                    //LostPackets.Sort();
                //}
            //}
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

                        var packet = new Packet(Packet.Type.Rate, id, interfaceid, data);

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

                    var type = (Packet.Type)bytes[0];

                    switch (type)
                    {
                        case Packet.Type.KeepAlive:
                            {
                                var packet = new Packet(Packet.Type.KeepAlive, id, interfaceid);

                                listener.Send(packet.Payload, packet.Payload.Length, groupEP);
                            }
                            break;
                        case Packet.Type.Data:
                            {
                                Rate1s++;
                                Rate8s++;
                                DataPackets++;
                                //RewriteLine(2, "Packets: " + DataPackets);
                                //RewriteLine(3, "Rate1s: " + Rate1s);
                                //RewriteLine(4, "Rate8s: " + Rate8s / 8);

                                ProccessBuffer.Enqueue(bytes);

                                ProcessDataPacket();

                                if (ProccessBuffer.Count > 1000)
                                {
                                    ProccessBuffer.Dequeue();
                                }
                            }
                            break;
                        case Packet.Type.ARQRequest:
                            break;
                        case Packet.Type.Connect:
                            {
                                ProccessConnectPacket(bytes, groupEP);
                            }
                            break;
                        case Packet.Type.AcceptConnect:
                            break;
                        case Packet.Type.Rate:
                            break;
                        case Packet.Type.ARQResponse:
                            {
                                var task = Packet.Serialize(bytes);
                                task.Wait();
                                var packet = task.Result;

                                if (!Buffer.ContainsKey(packet.SequenceNumber))
                                {
                                    Buffer.Add(packet.SequenceNumber, packet);
                                }
                            }
                            break;
                    }
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
            }
        }

        private async void ProccessConnectPacket(byte[] data, IPEndPoint clientEP)
        {
            var packet = await Packet.Serialize(data);

            var id = packet.ClientID;

            if(Clients.Keys.Contains(id))
            {
                if (!Clients[id].Values.Contains(clientEP))
                {
                    AddClient(clientEP, id);
                }
            }
            else
            {
                AddClient(clientEP, id);
            }
        }

        private void AddClient(IPEndPoint ClientEP, int ClientID)
        {
            Packet AcceptConnectPacket = new Packet(Packet.Type.AcceptConnect, id, interfaceid);

            listener.Send(AcceptConnectPacket.Payload, AcceptConnectPacket.Payload.Length, groupEP);

            Thread.Sleep(5);

            listener.Send(AcceptConnectPacket.Payload, AcceptConnectPacket.Payload.Length, groupEP);

            var list = new SortedList<int, IPEndPoint>();
            list.Add(ClientID, ClientEP);

            Clients.Add(ClientID, list);
        }

        private async void ProcessDataPacket()
        {
            //while (true)
            //{
                //if(ProccessBuffer.Count > 0)
                //{
                    var bytes = ProccessBuffer.Dequeue();

                    var packet = await Packet.Serialize(bytes);

                    if (!Buffer.ContainsKey(packet.SequenceNumber))
                    {
                        Buffer.Add(packet.SequenceNumber, packet);
                    }

                    if (Buffer.Count > 1000)
                    {
                        Buffer.RemoveAt(0);
                    }

                    CurrPacket = packet.SequenceNumber;
                    
                    if (!(CurrPacket == PrevPacket + 1))
                    {
                        var diff = CurrPacket - PrevPacket;

                        for (uint i = 1; i < diff; i++)
                        {
                            LostPackets.Add(PrevPacket + i);
                            SendARQ();
                        }
                    }

                    PrevPacket = CurrPacket;
                    CurrPacket = 0;
               // }
            //}
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
