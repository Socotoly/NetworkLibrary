﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using PacketFactory;

namespace Client
{
    class Client : UdpClient
    {
        public int ID;
        public int InterfaceID;
        public uint RTT = 0;
        public IPEndPoint LocalIPEndPoint;
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
        private SortedList<int, Packet> SentPackets = new SortedList<int, Packet>();
        private NetworkManager NetworkManager;

        public Client(IPEndPoint localEP, int ID, int InterfaceID) : base(localEP)
        {
            this.Client.IOControl(
                (IOControlCode)SIO_UDP_CONNRESET,
                new byte[] { 0, 0, 0, 0 },
                null
                );

            this.ID = ID;
            this.InterfaceID = InterfaceID;
            this.LocalIPEndPoint = localEP;
        }

        private async void BeginReceiveCycle()
        {
            while (true)
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
                                {
                                    ProcessAcceptConnect();
                                }
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

            if (LostPacket != null)
            {
                var PacketToSend = new Packet(Packet.Type.ARQResponse, ID, InterfaceID, Packet.Key, LostPacket.Data, LostPacket.RTT, LostPacket.SequenceNumber);

                await Send(PacketToSend);
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

        public async void KeepAliveAsync()
        {
            while (true)
            {
                if (Connected && (uint)KeepAliveTimeoutWatcher.ElapsedMilliseconds >= (uint)KeepAliveTimeout)
                {
                    ProccessKeepAliveTimeout();
                }
                if (Connected)
                {
                    var packet = new Packet(Packet.Type.KeepAlive, 1, 1);

                    RTTWatcher.Start();

                    await Send(packet);
                }

                Thread.Sleep(1500);
                //NOP(1.5);
            }

        }

        private void ProccessKeepAliveTimeout()
        {
            Connected = false;

            this.NetworkManager.ClientDisconnected(InterfaceID);
        }

        public void Connect(IPEndPoint ServerIP, NetworkManager networkManager)
        {
            ServerEP = ServerIP;
            NetworkManager = networkManager;

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

        private async void TryToConnect()
        {
            while (true)
            {
                if (!Connected)
                {
                    Console.WriteLine("Trying to Connect");

                    await Send(new Packet(Packet.Type.Connect, 1, 1));

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

        private void ProcessAcceptConnect()
        {
            if (!Connected)
            {
                Connected = true;

                KeepAliveTimeoutWatcher.Restart();

                this.NetworkManager.ClientConnected(this.InterfaceID);
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

        public async Task Send(Packet Packet)
        {
            await SendAsync(Packet.Payload, Packet.Payload.Length, ServerEP);

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
                if (Buffer.Count != 0)
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
