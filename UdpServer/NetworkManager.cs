using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace Client
{
    class NetworkManager
    {
        private SortedList<int, IPEndPoint> Interfaces = new SortedList<int, IPEndPoint>();

        public NetworkManager()
        {
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();

            GetUsableInterfaces(adapters);
        }

        private void GetUsableInterfaces(NetworkInterface[] adapters)
        {
            List<IPAddress> IPList = new List<IPAddress>();
            int i = 1;
            foreach (NetworkInterface adapter in adapters)
            {
                var canRoute = false;
                try
                {
                    canRoute = adapter.GetIPProperties().GetIPv4Properties().IsForwardingEnabled;

                }
                catch (Exception e)
                {
                    continue;
                }

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

            foreach (IPEndPoint ip in this.Interfaces.Values)
            {
                Console.WriteLine(ip.ToString());
            }

            Console.ReadLine();
        }
    }
}
