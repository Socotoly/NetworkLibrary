using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UdpServer
{
    class Client : UdpClient
    {

        public Client(IPEndPoint ClientIP)
        {
            new UdpClient(ClientIP);
        }

        public Client Connect(IPEndPoint ServerIP, int ServerPort = 0)
        {
            this.Connect(ServerIP);

            return this;
        }

        public Client Send(IPEndPoint ClientIP, IPEndPoint ServerIP, byte[] Payload)
        {
            this.Send(Payload, Payload.Length, ServerIP);

            return this;
        }
    }
}
