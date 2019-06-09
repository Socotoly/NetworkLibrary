using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using PacketFactory;

namespace Client
{
    class Client : UdpClient
    {
        public int ID;
        public int InterfaceID;
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

        public Client(IPEndPoint localEP, int ID, int InterfaceID) : base(localEP)
        {
            this.Client.IOControl(
                (IOControlCode)SIO_UDP_CONNRESET,
                new byte[] { 0, 0, 0, 0 },
                null
                );

            this.ID = ID;
            this.InterfaceID = InterfaceID;
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
                            case Packet.Type.ARQRequest:
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

            Packet LostPacket;
            SentPackets.TryGetValue(ARQPacket.SequenceNumber, out LostPacket);

            if(LostPacket != null)
            {
                var PacketToSend = new Packet(Packet.Type.ARQResponse,1 ,1, LostPacket.Data, LostPacket.RTT, LostPacket.SequenceNumber);

                Send(PacketToSend);
                Console.WriteLine("QRQ");
            }
            
            //SentPackets.Remove(ARQPacket.SequenceNumber);
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
                }
                if (Connected)
                {
                    var packet = new Packet(Packet.Type.KeepAlive,1,1);

                    RTTWatcher.Start();

                    Send(packet);
                }

                Thread.Sleep(1500);
                //NOP(1.5);
            }
            
        }

        public new void Connect(IPEndPoint ServerIP)
        {
            ServerEP = ServerIP;

            //SendThread = new Thread(TryToSend);
            //SendThread.Start();

            ConnectThread = new Thread(TryToConnect);
            ConnectThread.Start();

            ReceiveThread = new Thread(BeginReceiveCycle);
            ReceiveThread.Start();

            KeepAliveThread = new Thread(KeepAliveAsync);
            KeepAliveThread.Start();
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

        private void TryToConnect()
        {
            while (true) {
                if (!Connected)
                {
                    Console.WriteLine("Trying to Connect");

                    Send(new Packet(Packet.Type.Connect,1,1));

                    Thread.Sleep(20);

                    //var status = ProcessAcceptConnect();

                    if (Connected)
                    {
                        Console.WriteLine("Connected");
                    }
                    else
                    {
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
            base.SendAsync(Packet.Payload, Packet.Payload.Length, ServerEP);

            if (Packet.GetType == Packet.Type.Data)
            {
                if (!SentPackets.ContainsKey(Packet.SequenceNumber))
                {
                    SentPackets.Add(Packet.SequenceNumber, Packet);
                }
            }

            //Buffer.Enqueue(Packet);
        }

        private void TryToSend()
        {
            while (true)
            {
                if(Buffer.Count != 0)
                {
                    var Packet = Buffer.Dequeue();

                    base.SendAsync(Packet.Payload, Packet.Payload.Length, ServerEP);

                    if (Packet.GetType == Packet.Type.Data)
                    {
                        if (!SentPackets.ContainsKey(Packet.SequenceNumber))
                        {
                            SentPackets.Add(Packet.SequenceNumber, Packet);
                        }
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
