using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server
{
    class Server
    {
        UdpClient listener;
        IPEndPoint groupEP;

        public Server(int port)
        {
            listener = new UdpClient(port);
            //  groupEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), port);

            StartListener(port);
        }

        public void Close()
        {
            listener.Close();
        }

        private void StartListener(int port)
        {

            try
            {
                while (true)
                {
                    Console.WriteLine("server is ready");

                    byte[] bytes = listener.Receive(ref groupEP);

                    listener.Send(bytes, bytes.Length, groupEP);
                    Console.WriteLine(bytes.Length);

                    byte[] timeByte = new byte[8];
                    int e = 0;
                    for (int i = 8; i < 16; i++)
                    {
                        timeByte[e] = bytes[i];
                        Console.WriteLine(i);
                        e++;
                    }

                    var timestamp = BitConverter.ToInt64(timeByte);
                    var localtime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    var diff = localtime - timestamp;
                   

                    Console.WriteLine($"Received broadcast from {groupEP} :");
                    Console.WriteLine(timestamp + "   " + localtime);
                    Console.WriteLine("diff="+diff);
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
            }
           
        }

    }
}
