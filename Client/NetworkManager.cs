using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace Client
{
    class NetworkManager
    {
        public SortedList<int, IPEndPoint> Interfaces = new SortedList<int, IPEndPoint>();
        public SortedList<int, Client> Clients = new SortedList<int, Client>();
        public SortedList<int, int> Wheight = new SortedList<int, int>();
        private int ClientID;

        public NetworkManager()
        {
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();

            GetUsableInterfaces(adapters);

            InitilaizeClients();

            PrintInfo();
        }

        public void Send(byte[] Data)
        {



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
                var client = new Client(Interface.Value, ClientID);

                Clients.Add(Interface.Key, client);
            }

            var w = 10 / Clients.Count;

            foreach (KeyValuePair<int, Client> client in Clients)
            {
                Wheight.Add(client.Key, w);
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
        }
    }
}
