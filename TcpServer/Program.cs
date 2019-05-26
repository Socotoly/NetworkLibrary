using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace TcpServer
{
    class Program
    {
        static void Main(string[] args)
        {
            TcpListener server;
            
            server = new TcpListener(IPAddress.Any, 8696);
        
            server.Start();

            while (true)
            {
                var client = server.AcceptTcpClient();

                var networkStream = client.GetStream();
                var pi = networkStream.GetType().GetProperty("Socket", BindingFlags.NonPublic | BindingFlags.Instance);
                var socketIp = ((Socket)pi.GetValue(networkStream, null)).RemoteEndPoint.ToString();

                Console.WriteLine(socketIp);
            }
        }
    }
}
