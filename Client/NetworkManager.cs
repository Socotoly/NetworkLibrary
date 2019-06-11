using PacketFactory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;

namespace Client
{
    class NetworkManager
    {
        public SortedList<int, IPEndPoint> Interfaces = new SortedList<int, IPEndPoint>();
        public SortedList<int, Client> Clients = new SortedList<int, Client>();
        public SortedList<int, int> Wheight = new SortedList<int, int>();
        private List<int> NextClient = new List<int>();
        private int ClientID;
        private int index = 0;
        private uint SequenceNumber = 0;
        private const int WheightVal = 10;
        public bool Connected = false;

        public NetworkManager()
        {
            GetUsableInterfaces();

            InitilaizeClients();

            PrintInfo();
        }

        public void Send(byte[] Data)
        {
            SequenceNumber++;
            var client = GetNextClient();
            var packet = GetPacket(Data, client);

            client.Send(packet);
        }

        public void Connect(string ServerIP, int ServerPort)
        {
            var ServerEP = new IPEndPoint(IPAddress.Parse(ServerIP), ServerPort);

            foreach (Client client in Clients.Values)
            {
                client.Connect(ServerEP, this);
            }
        }

        private Packet GetPacket(byte[] data, Client client)
        {
            return new Packet(Packet.Type.Data, client.ID, client.InterfaceID, data, 0, SequenceNumber);
        }

        private void GetUsableInterfaces()
        {
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();

            List<IPAddress> IPList = new List<IPAddress>();
            int i = 1;
            foreach (NetworkInterface adapter in adapters)
            {
                var gws = adapter.GetIPProperties().GatewayAddresses;
                var hasgw = false;
                foreach (var gw in gws)
                {
                    if (gw.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        hasgw = true;
                    }
                }

                var isUp = adapter.OperationalStatus;

                if (isUp == OperationalStatus.Up && hasgw)
                {
                    var ips = adapter.GetIPProperties().UnicastAddresses;
                    foreach (UnicastIPAddressInformation ip in ips)
                    {
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            IPList.Add(ip.Address);
                        }
                    }

                    if (IPList.Count > 0)
                    {
                        var port = Convert.ToInt32("1000" + i);
                        var EP = new IPEndPoint(IPList[0], port);
                        IPList.Clear();
                        Interfaces.Add(i, EP);
                        i++;
                    }
                }
            }
        }

        private void InitilaizeClients()
        {
            ClientID = new Random().Next(1, 50000);

            foreach (KeyValuePair<int, IPEndPoint> Interface in Interfaces)
            {
                var client = new Client(Interface.Value, ClientID, Interface.Key);

                Clients.Add(Interface.Key, client);
            }

            
        }

        private Client GetNextClient()
        {
            if(index == NextClient.Count - 1)
            {
                index = 0;
            }
            else
            {
                index++;
            }
            Console.WriteLine(index);
            Console.WriteLine(NextClient.Count);
            return Clients[NextClient[index]];
        }

        public void ClientConnected(int InterfaceID)
        {
            var wc = Wheight.Count;

            if(wc == 0)
            {
                Wheight.Add(InterfaceID, WheightVal);
            }
            else if (wc == 1)
            {
                Wheight.Add(InterfaceID, WheightVal / 2);
                Wheight.Values[0] = WheightVal / 2;
            }
            else
            {
                var val = WheightVal / (wc + 1);

                Wheight.Add(InterfaceID, val);

                for(int i = 0; i < wc; i++)
                {
                    Wheight.Values[i] = Wheight.Values[i] / WheightVal * (WheightVal - val);
                }
            }

            wc = Wheight.Count;

            for (int i = 0; i < wc; i++)
            {
                var val = Wheight.Values[i];

                for (int c = 0; c < val; c++)
                {
                    NextClient.Add(Wheight.Keys[i]);
                }
            }

            NextClient.Shuffle();

            Connected = true;
        }

        public void ClientDisconnected(int InterfaceID)
        {
            if (Wheight.Keys.Contains(InterfaceID))
            {
                var WheightVal = Wheight[InterfaceID];

                Wheight.Remove(InterfaceID);

                var wc = Wheight.Count;

                if (wc == 0)
                {
                    Connected = false;
                }
                else
                {
                    int TotalWheight = 0;

                    for(int i = 0; i < wc; i++)
                    {
                        TotalWheight += Wheight.Values[i];
                    }

                    for (int i = 0; i < wc; i++)
                    {
                        Wheight.Values[i] = ((Wheight.Values[i] / (TotalWheight) * 100) / 100) * 10;
                    }

                    for (int i = 0; i < wc; i++)
                    {
                        var val = Wheight.Values[i];

                        for (int c = 0; c < val; c++)
                        {
                            NextClient.Add(Wheight.Keys[i]);
                        }
                    }

                    NextClient.Shuffle();
                }
            }
        }
        private void PrintInfo()
        {
            foreach (KeyValuePair<int, IPEndPoint> Interface in Interfaces)
            {
                Console.WriteLine(Interface.Key + " " + Interface.Value);
            }
            foreach (KeyValuePair<int, Client> Interface in Clients)
            {
                Console.WriteLine(Interface.Key + " " + Interface.Value);
            }
            foreach (KeyValuePair<int, int> Interface in Wheight)
            {
                Console.WriteLine(Interface.Key + " " + Interface.Value);
            }
            foreach (int n in NextClient)
            {
                var c = GetNextClient();

                Console.WriteLine("Next client is: "+c.Client.LocalEndPoint.ToString());
            }
            Console.WriteLine("-------------------------------");

            foreach (int n in NextClient)
            {
                Console.WriteLine("Next client is: " + n);
            }
        }
    }

    static class ShuffleListExtension
    {
        public static class ThreadSafeRandom
        {
            [ThreadStatic] private static Random Local;

            public static Random ThisThreadsRandom
            {
                get { return Local ?? (Local = new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId))); }
            }
        }

        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = ThreadSafeRandom.ThisThreadsRandom.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

    }
}
