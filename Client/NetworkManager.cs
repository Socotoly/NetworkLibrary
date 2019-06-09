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

        public NetworkManager()
        {
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();

            GetUsableInterfaces(adapters);

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
                client.Connect(ServerEP);
            }
        }

        private Packet GetPacket(byte[] data, Client client)
        {
            return new Packet(Packet.Type.Data, client.ID, client.InterfaceID, data, 0, SequenceNumber);
        }

        private void GetUsableInterfaces(NetworkInterface[] adapters)
        {
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

            var w = 10 / Clients.Count;
            //var wh = new SortedList<int, int>();

            foreach (KeyValuePair<int, Client> client in Clients)
            {
                Wheight.Add(client.Key, w);
                //wh.Add(client.Key, w);
            }

            foreach (KeyValuePair<int, int> whe in Wheight )
            {
                var c = whe.Value;
                for(int i = 0; i < c; i++)
                {
                    NextClient.Add(whe.Key);
                }
            }

            NextClient.Shuffle();
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

            return Clients[NextClient[index]];
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
