using PacketFactory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
        private SortedList<int, Packet> Buffer = new SortedList<int, Packet>();
        public uint ARQDelay = 1000;
        private int PrevPacket = 0;
        private int CurrPacket = 0;
        private List<int> LostPackets = new List<int>();
        private List<int> SentLostPackets = new List<int>();
        private Queue<byte[]> ProccessBuffer = new Queue<byte[]>();
        private SortedList<int, SortedList<int, IPEndPoint>> Clients = new SortedList<int, SortedList<int, IPEndPoint>>();
        private int lost = 0;

        public Server(int port)
        {
            listener = new UdpClient(port);
            Console.WriteLine("server is ready");

            //new Thread(SendARQ).Start();
            //new Thread(CleanUpLostPackets).Start();
            new Thread(StartListener).Start();
            //new Thread(ProcessDataPacket).Start();

            new Thread(ARQThread).Start();

            while (Buffer.Count == 0)
            {

            }
            Thread.Sleep(20000);

            Console.WriteLine("Lost packets: " + lost);
            Console.WriteLine("Total packets: " + Buffer.Count);


            Console.ReadLine();
            //StartListener(port);
        }

        public void Close()
        {
            listener.Close();
        }

        private void ARQThread()
        {
            SendARQ();
        }

        private async void SendARQ()
        {
            try
            {
                if (Buffer.Count > 0)
                {
                    IList<int> keys;

                    lock (Buffer)
                    {
                        keys = Buffer.Keys;
                    }

                    var k = new List<int>();
                    k.AddRange(keys);

                    var list = Enumerable.Range(k.First(), k.Last()).Except(keys);

                    var l = list.ToList();

                    for (int i = 0; i < l.Count(); i++)
                    {
                        var packet = new Packet(Packet.Type.ARQRequest, id, interfaceid, Packet.Key, null, 0, l[i]);

                        if (!groupEP.Equals(null))
                        {
                            await listener.SendAsync(packet.Payload, packet.Payload.Length, groupEP);

                            Thread.Sleep(5);

                            await listener.SendAsync(packet.Payload, packet.Payload.Length, groupEP);

                            SentLostPackets.Add(l[i]);
                        }
                    }
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            Thread.Sleep(250);

            SendARQ();
        }

        private void CalculateDataRate()
        {
            while (true)
            {
                if (DataPackets > 0)
                {
                    if (!WatchRate1s.IsRunning)
                    {
                        WatchRate1s.Start();
                        //WatchRate8s.Start();
                    }
                    if (WatchRate1s.ElapsedMilliseconds > 5000)
                    {
                        var rate1s = Rate1s / 5;
                        Console.WriteLine(WatchRate1s.ElapsedMilliseconds);
                        Console.WriteLine(rate1s);
                        Rate1s = 0;
                        WatchRate1s.Restart();

                        var RateByte = new byte[2];

                        RateByte = BitConverter.GetBytes(rate1s);

                        var data = new byte[] { 1, RateByte[0], RateByte[1] };

                        var packet = new Packet(Packet.Type.Rate, id, interfaceid, Packet.Key, data);

                        listener.Send(packet.Payload, packet.Payload.Length, groupEP);

                    }
                }

            }
        }

        private async void StartListener()
        {
            try
            {
                while (true)
                {
                    var task = await listener.ReceiveAsync();
                    byte[] bytes = task.Buffer;

                    var RemoteEP = task.RemoteEndPoint;
                    //var passkey = bytes[];

                    if (!Authenticated(bytes))
                    {
                        continue;
                    }

                    var type = (Packet.Type)bytes[0];

                    switch (type)
                    {
                        case Packet.Type.KeepAlive:
                            {
                                var packet = new Packet(Packet.Type.KeepAlive, id, interfaceid);

                                listener.Send(packet.Payload, packet.Payload.Length, RemoteEP);
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
                                ProccessConnectPacket(bytes, RemoteEP);
                            }
                            break;
                        case Packet.Type.AcceptConnect:
                            break;
                        case Packet.Type.Rate:
                            break;
                        case Packet.Type.ARQResponse:
                            {
                                var packet = await Packet.Serialize(bytes);

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

        private bool Authenticated(byte[] bytes)
        {
            if (bytes.Length >= 26)
            {
                var key = Encoding.Unicode.GetString(new byte[16] { bytes[10], bytes[11], bytes[12], bytes[13], bytes[14], bytes[15], bytes[16], bytes[17], bytes[18], bytes[19], bytes[20], bytes[21], bytes[22], bytes[23], bytes[24], bytes[25] });

                return key.Equals(Packet.Key);
            }
            else
            {
                return false;
            }
        }

        private async void ProccessConnectPacket(byte[] data, IPEndPoint clientEP)
        {
            var packet = await Packet.Serialize(data);

            var clientid = packet.ClientID;
            var interfaceid = packet.InterfaceID;

            SendAcceptConnect(clientEP, clientid, interfaceid);
            Console.WriteLine("Accept sent");
            if (Clients.Keys.Contains(clientid))
            {
                if (!Clients[clientid].Keys.Contains(interfaceid))
                {
                    var list = Clients[clientid];

                    list.Add(interfaceid, clientEP);
                }
            }
            else
            {
                var list = new SortedList<int, IPEndPoint>();

                list.Add(interfaceid, clientEP);

                Clients.Add(clientid, list);

                //SendAcceptConnect(clientEP, clientid, interfaceid);
            }
        }

        private async void SendAcceptConnect(IPEndPoint ClientEP, int ClientID, int InterfaceID)
        {
            Packet AcceptConnectPacket = new Packet(Packet.Type.AcceptConnect, ClientID, InterfaceID);

            await listener.SendAsync(AcceptConnectPacket.Payload, AcceptConnectPacket.Payload.Length, ClientEP);

            Thread.Sleep(5);

            await listener.SendAsync(AcceptConnectPacket.Payload, AcceptConnectPacket.Payload.Length, ClientEP);
        }

        private async void ProcessDataPacket()
        {
            var bytes = ProccessBuffer.Dequeue();

            var packet = await Packet.Serialize(bytes);

            if (!Buffer.ContainsKey(packet.SequenceNumber))
            {
                Buffer.Add(packet.SequenceNumber, packet);
            }

            if (Buffer.Count > 10000)
            {
                Buffer.RemoveAt(0);
            }

            CurrPacket = packet.SequenceNumber;

            if (CurrPacket < PrevPacket)
            {

            }
            else
            {
                if (!(CurrPacket == PrevPacket + 1))
                {
                    var diff = CurrPacket - PrevPacket;

                    for (int i = 1; i < diff; i++)
                    {
                        LostPackets.Add(PrevPacket + i);
                        lost++;
                    }

                    PrevPacket = CurrPacket;
                }
            }

            CurrPacket = 0;
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
                if (LostPackets.Count > 0)
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

        /// <summary>
        /// <para>For two sorted IEnumerable&lt;T&gt; (superset and subset),</para>
        /// <para>returns the values in superset which are not in subset.</para>
        /// </summary>
        public static IEnumerable<T> CompareSortedEnumerables<T>(IEnumerable<T> superset, IEnumerable<T> subset)
            where T : IComparable
        {
            IEnumerator<T> supersetEnumerator = superset.GetEnumerator();
            IEnumerator<T> subsetEnumerator = subset.GetEnumerator();
            bool itemsRemainingInSubset = subsetEnumerator.MoveNext();

            // handle the case when the first item in subset is less than the first item in superset
            T firstInSuperset = superset.First();
            while (itemsRemainingInSubset && supersetEnumerator.Current.CompareTo(subsetEnumerator.Current) >= 0)
                itemsRemainingInSubset = subsetEnumerator.MoveNext();

            while (supersetEnumerator.MoveNext())
            {
                int comparison = supersetEnumerator.Current.CompareTo(subsetEnumerator.Current);
                if (!itemsRemainingInSubset || comparison < 0)
                {
                    yield return supersetEnumerator.Current;
                }
                else if (comparison >= 0)
                {
                    while (itemsRemainingInSubset && supersetEnumerator.Current.CompareTo(subsetEnumerator.Current) >= 0)
                        itemsRemainingInSubset = subsetEnumerator.MoveNext();
                }
            }
        }
    }
}
