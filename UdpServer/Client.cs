using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UdpServer
{
    class Client : UdpClient
    {
        public uint RTT = 0;
        private Stopwatch RTTWatcher = new Stopwatch();
        private Stopwatch KeepAliveTimeoutWatcher = new Stopwatch();
        public const int SIO_UDP_CONNRESET = -1744830452;
        private Task ReceiveTask;
        public Timer ConnectAttempt;
        private bool isBusy = false;


        public Client(IPEndPoint localEP) : base(localEP)
        {
            this.Client.IOControl(
                (IOControlCode)SIO_UDP_CONNRESET,
                new byte[] { 0, 0, 0, 0 },
                null
                );
        }

        private async Task BeginReceiveCycle()
        {
            while(true)
            {
                try
                {
                    var task = await ReceiveAsync();

                    if (task.RemoteEndPoint.Address.ToString() == ServerEP.Address.ToString())
                    {
                        var type = task.Buffer[0].ToString();

                        if (type == "0")
                        {
                            ProcessKeepAlive(task.Buffer);

                           // Console.WriteLine(RTT);
                        }
                        if (type == "4")
                        {
                            Connected = true;

                            KeepAliveTimeoutWatcher.Restart();
                        }
                    }
                }
                catch (SocketException e)
                {
                    Console.WriteLine(e.ErrorCode);
                }

            }
        }

        private void ProcessKeepAlive(byte[] packet)
        {
            KeepAliveTimeoutWatcher.Restart();

            RTT = (uint)RTTWatcher.ElapsedMilliseconds;

            RTTWatcher.Reset();
        }
        
        public async void KeepAliveAsync(Object state)
        {
            while (true)
            {
                if (Connected && (uint)KeepAliveTimeoutWatcher.ElapsedMilliseconds >= (uint)KeepAliveTimeout)
                {
                Console.WriteLine((uint)KeepAliveTimeoutWatcher.ElapsedMilliseconds);
                Console.WriteLine((uint)KeepAliveTimeout);
                Connected = false;
                }
                if (Connected)
                {
                var packet = new Packet(Packet.Type.KeepAlive);

                RTTWatcher.Start();
                //Console.WriteLine("Sending KeepAlive");
                Send(packet);
                }

                Thread.Sleep(1500);
                //NOP(1.5);
            }
            
        }

        public new void Connect(IPEndPoint ServerIP)
        {
            ServerEP = ServerIP;

            ReceiveTask = Task.Run(()=>BeginReceiveCycle());

            Task.Run(() => ConnectAttempt = new Timer(TryToConnect, null, 0, KeepAliveTimeout));

            //var status = TryToConnect();

            //base.Connect(ServerIP);

            //return this;
        }

        public void Disconnect()
        {
            if (Connected)
            {
                Connected = false;

                ReceiveTask.Dispose();

                ConnectAttempt.Dispose();
            }
        }

        private void TryToConnect(Object state)
        {
            if (!Connected)
            {
                Console.WriteLine("Trying to Connect");

                Send(new Packet(Packet.Type.Connect));

                Thread.Sleep(5);

                //Send(new Packet(Packet.Type.Connect));

                //var status = ProcessAcceptConnect();
                //Thread.Sleep(5);

                if (Connected)
                {
                    Console.WriteLine("Connected");

                    Connected = true;

                    KeepAliveTimeoutWatcher.Start();

                    KeepAliveAsync(null);
                }
                else
                {
                    //ReceiveTask.Dispose();
                    Console.WriteLine("No response");

                    Connected = false;
                }
            }
        }

        private bool ProcessAcceptConnect()
        {
            Thread.Sleep(10);

            if (Connected)
            {
                return true;
            }
            else
            {
                Thread.Sleep(50);
            }
            if (Connected)
            {
                return true;
            }
            else
            {
                Thread.Sleep(100);
            }
            if (Connected)
            {
                return true;
            }
            else
            {
                Thread.Sleep(2000);
            }
            if (Connected)
            {
                return true;
            }
            else
            {
                //Connected = false;

                return false;
            }
        }

        public bool Connected { get; private set; }

        public int KeepAliveInterval { get; set; } = 1500;

        public int KeepAliveTimeout { get; set; } = 5000;

        public IPEndPoint ServerEP { get; private set; }

        public void Send(Packet Packet)
        {
            base.Send(Packet.Payload, Packet.Payload.Length, ServerEP);
        }

        private static void NOP(double durationSeconds)
        {
            var durationTicks = Math.Round(durationSeconds * Stopwatch.Frequency);
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedTicks < durationTicks)
            {

            }
        }
    }
}
