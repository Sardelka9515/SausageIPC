using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Lidgren.Network;
using System.Net;

namespace SausageIPC
{
    public class IpcClient
    {

        public IPEndPoint EndPoint;
        private NetClient _client;
        public string Alias { get;private set; }
        public float Latency;
        public NetDeliveryMethod DefaultDeliveryMethod = NetDeliveryMethod.Unreliable;
        public NetConnectionStatus Status
        {
            get { return _client.ConnectionStatus; }
        }
        public event EventHandler<IpcMessage> OnMessageReceived;
        public event EventHandler<IpcMessage> OnConnected;
        public event EventHandler<string> OnDisonnected;
        public event EventHandler<QueryEventArgs> OnQuerying;
        private event EventHandler<IpcMessage> OnReplyReceived;
        private HashSet<int> InProgreeQueries = new HashSet<int>();
        private Thread NetworkThread;
        private bool Stopping { get; set; } = false;
        public IPEndPoint ServerEndpoint;
        public IPEndPoint LocalEndpoint;
        public IpcClient(IPEndPoint local,string alias)
        {
            NetPeerConfiguration config = new NetPeerConfiguration("623c92c287cc392406e7aaaac1c0f3b0")
            {
                AutoFlushSendQueue = true,
                LocalAddress = local.Address,
                Port = local.Port,
            };
            config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);

            _client = new NetClient(config);
            _client.Start();

            NetworkThread=new Thread(() =>
            {
                while (!Stopping)
                {
                    Process(_client.WaitMessage(20));
                }
                NetworkThread.Start();
            });
            if (string.IsNullOrEmpty(alias)) { alias=LocalEndpoint.ToString(); }
            Alias = alias;
        }
        private void Process(NetIncomingMessage msg)
        {
            if (msg == null) { return; }
            switch (msg.MessageType)
            {
                case NetIncomingMessageType.ConnectionLatencyUpdated:
                    {
                        Latency=msg.ReadFloat();
                        break;
                    }
                case NetIncomingMessageType.StatusChanged:
                    {
                        NetConnectionStatus status = (NetConnectionStatus)msg.ReadByte();

                        if (status == NetConnectionStatus.Disconnected)
                        {
                            OnDisonnected?.Invoke(this, msg.ReadString());
                        }
                        else if (status == NetConnectionStatus.Connected)
                        {
                            OnConnected?.Invoke(this, new IpcMessage(msg));
                        }
                        break;
                    }
                case NetIncomingMessageType.Data:
                    {
                        var message = new IpcMessage(msg);
                        switch (message.MessageType)
                        {
                            case MessageType.Message:
                                OnMessageReceived?.Invoke(this, message);
                                break;
                            case MessageType.Query:
                                {
                                    OnQuerying?.Invoke(this, new QueryEventArgs(message));
                                    break;
                                }
                            case MessageType.Reply:
                                {
                                    OnReplyReceived?.Invoke(this, message);
                                    break;
                                }
                        }
                        break;
                    }
            }
        }
        public IpcMessage Connect(string host,int port,int timeout,IpcMessage connectMessage)
        {
            AutoResetEvent connected=new AutoResetEvent(false); 
            IpcMessage reply=null;
            var handler = new EventHandler<IpcMessage>((s, message) =>
            {
                reply= message;
                connected.Set();
            });
            OnConnected+=handler;
            var msg = _client.CreateMessage();
            msg.Data=connectMessage.Serialize();
            _client.Connect(host, port, msg);
            if (connected.WaitOne(timeout))
            {
                OnConnected-=handler;
                if (reply.IsValid)
                {
                    return reply;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                OnConnected-=handler;
                throw new TimeoutException("Server did not respond in specified time window.");
            }
        }

    }
}
