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
        private const int SIO_UDP_CONNRESET = -1744830452;
        private Thread ReceiveThread;
        private Thread ConnectThread;
        private Thread KeepAliveThread;
        private ushort RemoteRate;
        private uint sleepAfterSend = 2;
        private Queue<Packet> Buffer = new Queue<Packet>();
        private Thread SendThread;
        private SortedList<uint, Packet> SentPackets = new SortedList<uint, Packet>();

        public Client(IPEndPoint localEP) : base(localEP)
        {
            this.Client.IOControl(
                (IOControlCode)SIO_UDP_CONNRESET,
                new byte[] { 0, 0, 0, 0 },
                null
                );
        }

        private async void BeginReceiveCycle()
        {
            while(true)
            {
                try
                {
                    var task = await ReceiveAsync();

                    if (task.RemoteEndPoint.Address.ToString() == ServerEP.Address.ToString())
                    {
                        var Buffer = task.Buffer;
                        var type = (Packet.Type)Buffer[0];

                        switch (type)
                        {
                            case Packet.Type.KeepAlive:
                                ProcessKeepAlive(Buffer);
                                break;
                            case Packet.Type.Data:
                                break;
                            case Packet.Type.ARQ:
                                ProcessARQ(Buffer);
                                break;
                            case Packet.Type.Connect:
                                break;
                            case Packet.Type.AcceptConnect:
                                Connected = true;
                                KeepAliveTimeoutWatcher.Restart();
                                break;
                            case Packet.Type.Rate:
                                break;
                        }
                    }
                }
                catch (SocketException e)
                {
                    Console.WriteLine(e.ErrorCode);
                }

            }
        }

        private async void ProcessARQ(byte[] buffer)
        {
            var ARQPacket = await Packet.Serialize(buffer);
            var PacketToSend = SentPackets.GetValueOrDefault(ARQPacket.SequenceNumber);

            Send(PacketToSend);
        }

        private void ProcessKeepAlive(byte[] packet)
        {
            KeepAliveTimeoutWatcher.Restart();

            RTT = (uint)RTTWatcher.ElapsedMilliseconds;

            RTTWatcher.Reset();
        }
        
        public void KeepAliveAsync()
        {
            while (true)
            {
                if (Connected && (uint)KeepAliveTimeoutWatcher.ElapsedMilliseconds >= (uint)KeepAliveTimeout)
                {
                    Connected = false;

                    //SendThread.Abort();
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


            //ConnectTask = Task.Run(()=> TryToConnect());

            SendThread = new Thread(TryToSend);
            SendThread.Start();

            ConnectThread = new Thread(TryToConnect);
            ConnectThread.Start();

            //ConnectTask = Task.Run(() => TryToConnect(null));

            ReceiveThread = new Thread(BeginReceiveCycle);
            ReceiveThread.Start();

            KeepAliveThread = new Thread(KeepAliveAsync);
            KeepAliveThread.Start();

            //new Thread(AdjustPacketRate).Start();

            //KeepAliveTask = Task.Run(()=> KeepAliveAsync());

            //var status = TryToConnect();

            //base.Connect(ServerIP);

            //return this;
        }

        public void Disconnect()
        {
            if (Connected)
            {
                Connected = false;

                ReceiveThread.Abort();

                //ConnectTask.Dispose();
            }
        }

        private void TryToConnect(object state)
        {
            while (true) {

                if (!Connected)
                {
                    Console.WriteLine("Trying to Connect");

                    Send(new Packet(Packet.Type.Connect));

                    Thread.Sleep(5);

                    //Send(new Packet(Packet.Type.Connect));

                    var status = ProcessAcceptConnect();
                    //Thread.Sleep(5);

                    if (status)
                    {
                        Console.WriteLine("Connected");

                        Connected = true;

                        KeepAliveTimeoutWatcher.Start();

                        //Task.Run(() => KeepAliveTask = KeepAliveAsync());
                    }
                    else
                    {
                        //ReceiveTask.Dispose();
                        Console.WriteLine("No response");

                        Connected = false;
                    }
                }

                Thread.Sleep(2500);

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
        public uint Rate { get => 1000 / SleepAfterSend; }

        public uint SleepAfterSend
        {
            get
            {
                return sleepAfterSend;
            }
            private set
            {

                sleepAfterSend = 1000 / value;
            }
        }
        public int KeepAliveInterval { get; set; } = 1500;

        public int KeepAliveTimeout { get; set; } = 5000;

        public IPEndPoint ServerEP { get; private set; }

        public void Send(Packet Packet)
        {
            Buffer.Enqueue(Packet);
        }

        private void TryToSend()
        {
            while (true)
            {
                if(Buffer.Count > 0 && Connected)
                {
                    var Packet = Buffer.Dequeue();

                    base.Send(Packet.Payload, Packet.Payload.Length, ServerEP);

                    if (Packet.GetType == Packet.Type.Data)
                    {
                        SentPackets.Add(Packet.SequenceNumber, Packet);
                    }
                }
            }
        }

        private void AdjustPacketRate()
        {
            while (true)
            {
                if (Connected && Rate > 0)
                {
                    Thread.Sleep(3000);

                    var rate = Rate;
                    Console.WriteLine("Prev Rate:" + Rate);
                    rate = (uint)(rate + (rate * 0.1));

                    SleepAfterSend = rate;
                    Console.WriteLine("New Rate:" + Rate);

                }
            }
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
