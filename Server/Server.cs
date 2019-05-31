using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UdpServer;

namespace Server
{
    class Server
    {
        UdpClient listener;
        IPEndPoint groupEP;
        int DataPackets = 0;

        public Server(int port)
        {
            listener = new UdpClient(port);
            //  groupEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), port);
            Console.WriteLine("server is ready");
            Console.WriteLine("Packets: " + DataPackets);

            StartListener(port);
        }

        public void Close()
        {
            listener.Close();
        }

        public static void RewriteLine(int lineNumber, String newText)
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, currentLineCursor - lineNumber);
            Console.Write(newText); Console.WriteLine(new string(' ', Console.WindowWidth - newText.Length));
            Console.SetCursorPosition(0, currentLineCursor);
        }

        private void StartListener(int port)
        {

            try
            {
                while (true)
                {

                    byte[] bytes = listener.Receive(ref groupEP);

                    //Console.WriteLine(bytes);
                    //Console.WriteLine(bytes[0]);
                    //Console.WriteLine(groupEP.Address);
                    //Console.WriteLine(groupEP.Port);

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
                        DataPackets++;
                        RewriteLine(2, "Packets: " + DataPackets);
                    }

                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
            }
           
        }

    }
}
