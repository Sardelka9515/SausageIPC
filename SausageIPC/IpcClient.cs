using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Lidgren.Network;
using System.Net;
using System.Security.Cryptography;

namespace SausageIPC
{
    public class IpcClient
    {

        public IPEndPoint EndPoint;
        private NetClient _client;
        public string Alias { get;private set; }
        public float Latency;
        public NetDeliveryMethod DefaultDeliveryMethod = NetDeliveryMethod.ReliableOrdered;
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
        private Logger logger;
        private bool Stopping { get; set; } = false;
        public IPEndPoint ServerEndpoint { get { return _client.ServerConnection?.RemoteEndPoint; } }
        public IPEndPoint LocalEndpoint;
        public IpcClient(IPEndPoint local=null,string alias=null,Logger _logger=null)
        {
            logger = _logger;
            NetPeerConfiguration config = new NetPeerConfiguration("SausageIPC")
            {
                AutoFlushSendQueue = true,
            };
            if(local != null)
            {
                config.LocalAddress = local.Address;
                config.Port = local.Port;
            }
            config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);

            _client = new NetClient(config);
            _client.Start();

            NetworkThread=new Thread(() =>
            {
                while (!Stopping)
                {
                    Process(_client.WaitMessage(200));
                }
            });
            NetworkThread.Start();
            if (string.IsNullOrEmpty(alias)) { alias=LocalEndpoint.ToString(); }
            Alias = alias;
        }
        private void Process(NetIncomingMessage msg)
        {
            if (msg == null) { return; }
            // logger?.Info($"message received:{msg.MessageType},{msg.SequenceChannel}");
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
                            OnConnected?.Invoke(this, new IpcMessage(msg.SenderConnection.RemoteHailMessage));
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
        /// <summary>
        /// Send a message to specified client(non-blocking).
        /// </summary>
        /// <param name="message"></param>
        /// <param name="recepient"></param>
        /// <param name="deliveryMethod"></param>
        public void Send(IpcMessage message, NetDeliveryMethod deliveryMethod = NetDeliveryMethod.Unknown)
        {
            if (deliveryMethod== NetDeliveryMethod.Unknown) { deliveryMethod=DefaultDeliveryMethod; }
            if (message==null) { return; }
            var msg = _client.CreateMessage();
            message.Serialize(msg);
            _client.SendMessage(msg, deliveryMethod, (int)message.MessageType);
        }

        /// <summary>
        /// Query a message and get response from the server(blocking).
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="timeout">Timeout in milliseconds</param>
        /// <param name="deliveryMethod"></param>
        /// <returns>The response from the client if received in specified time window; otherwise, null.</returns>
        public IpcMessage Query(IpcMessage message, int timeout=5000, NetDeliveryMethod deliveryMethod = NetDeliveryMethod.Unknown)
        {
            message.MessageType = MessageType.Query;
            InProgreeQueries.Add(message.QueryID = GetQueryID());
            IpcMessage Reply = null;
            AutoResetEvent Replied = new AutoResetEvent(false);
            var handler = new EventHandler<IpcMessage>((s, msg) =>
            {
                IpcMessage reply = msg;

                // check query id
                if (reply.QueryID == message.QueryID)
                {
                    Reply = reply;
                    Replied.Set();
                }
            });
            OnReplyReceived += handler;
            Send(message, deliveryMethod);
            Replied.WaitOne(timeout);
            OnReplyReceived -= handler;
            InProgreeQueries.Remove(message.QueryID);
            return Reply;
        }
        public IpcMessage Connect(string host,int port,int timeout=5000,IpcMessage connectMessage=null)
        {
            if(Status==NetConnectionStatus.Connected|| Status==NetConnectionStatus.InitiatedConnect)
            {
                throw new InvalidOperationException("Disconnect from the server before connecting");
            }
            AutoResetEvent connected=new AutoResetEvent(false); 
            IpcMessage reply=null;
            var handler = new EventHandler<IpcMessage>((s, message) =>
            {
                
                reply= message;
                connected.Set();
            });
            OnConnected+=handler;
            var msg = _client.CreateMessage();
            connectMessage.MetaData.Add("Alias",Alias);
            connectMessage.Serialize(msg);
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
        public void Disconnect(string byeMessage)
        {
            _client.Disconnect(byeMessage);
        }
        private int GetQueryID()
        {
            int ID = 0;
            while ((ID==0)
                || InProgreeQueries.Contains(ID))
            {
                byte[] rngBytes = new byte[4];

                RandomNumberGenerator.Create().GetBytes(rngBytes);

                // Convert the bytes into an integer
                ID = BitConverter.ToInt32(rngBytes, 0);
            }
            return ID;
        }
    }
}
