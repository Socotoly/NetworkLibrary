using MPLATFORMLib;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace UdpServer
{
    class Program
    {
        public static Packet packet;
        public static IPEndPoint groupEP;
        public static bool rec = true;
        public static int i = 0;
        public static byte[] e = new byte[510];


        static void Main(string[] args)
        {
            var sentence = new String("hello vmre    reeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeedn vi oeri envoirfje itrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr trrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr trrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr trrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr jfievj ofj vkod fvd");
            var Data = Encoding.ASCII.GetBytes(sentence);
            
            var packet = new Packet(50845,Data,80,1000);
            Program.packet = packet;
            Console.WriteLine(packet.Payload.Length);

            Udp("192.168.31.136", "197.215.152.148", 8696, packet.Payload);

        }

        private static void Udp(String clientIp, String serverIp, int serverPort, byte[] data)
        {
            var localEndPoint = new IPEndPoint(IPAddress.Parse(clientIp), port: 0);
            var client = new UdpClient(localEndPoint);
            Console.WriteLine("Client is ready");

            var server = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);

            for (int i = 0; i <  500; i++)
            {
                try
                {
                    Console.WriteLine("Trying to send");

                    packet.Send(client, server);

                    Console.WriteLine("Data is sent");

                    rec = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                try
                {
                    client.Connect(server);

                }catch(Exception e)
                {

                }

                StartListener(client);

                client.Close();
                client = new UdpClient(localEndPoint);

            }
            int d = 0;
            for(int i = 0; i< e.Length; i++)
            {
                if (e[i].Equals(0))
                {
                    d++;
                }
            }
            Console.WriteLine(d);
            Console.ReadLine();
        }

        private static void Tcp(String clientIp, String serverIp, int serverPort)
        {
            var localEndPoint = new IPEndPoint(IPAddress.Parse(clientIp), port: 0);

            var client = new TcpClient(localEndPoint);
            Console.WriteLine("Client is ready");

           // var server = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);

            Console.WriteLine("Trying to Connect");

            client.Connect(serverIp, serverPort);
            
            Console.WriteLine(client.Connected);

            Console.ReadLine();
        }

        private static void StartListener(UdpClient client)
        {
            try
            {
                while (Program.rec)
                {
                    byte[] bytes;
                    var task = Task.Run(() => Program.dowork(client));

                    if (task.Wait(TimeSpan.FromSeconds(0.1)))
                    {
                        bytes = task.Result;

                        packet.watch.Stop();
                        var roundTripLatency = packet.watch.ElapsedMilliseconds;

                        Console.WriteLine(bytes.Length);


                        Console.WriteLine(i + " " + roundTripLatency + " ms");

                        i++;
                        e[i] = Convert.ToByte(roundTripLatency);
                        rec = false;
                    }
                    else
                    {
                        Console.WriteLine(i + " " + "0" + " ms");

                        i++;
                        e[i] = Convert.ToByte(0);
                        rec = false;
                    }

                    
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
            }

        }


        private static byte[] dowork(UdpClient client)
        {
            return client.Receive(ref Program.groupEP);
        }
    }
}
