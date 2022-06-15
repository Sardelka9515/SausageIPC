using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using Lidgren.Network;
using System.Security.Cryptography;

namespace SausageIPC
{
    public class IpcServer
    {

        public IPEndPoint EndPoint;
        private NetServer _server;
        public string Alias;
        public NetDeliveryMethod DefaultDeliveryMethod = NetDeliveryMethod.ReliableOrdered;
        public event EventHandler<MessageReceivedEventArgs> OnMessageReceived;
        public event EventHandler<HandshakeEventArgs> OnHandshake;
        public event EventHandler<Client> OnClientConnected;
        public event EventHandler<Client> OnClientDisonnected;
        public event EventHandler<QueryEventArgs> OnQuerying;
        
        private Thread NetworkThread;
        private event EventHandler<ReplyReceivedEventArgs> OnReplyReceived;
        private HashSet<int> InProgreeQueries=new HashSet<int>();
        // private Dictionary<string, EventHandler<QueryEventArgs>> QueryHandlers=new Dictionary<string, EventHandler<QueryEventArgs>>();
        public Dictionary<IPEndPoint,Client> Clients=new Dictionary<IPEndPoint,Client>();

        private bool Stopping = false;
        private Logger logger;
        #region Constructors
        /// <summary>
        /// Start an IPC server.
        /// </summary>
        /// <param name="_endPoint">IPEndPoint to listen for connections</param>
        /// <param name="Alias"></param>
        /// <param name="UseAlternatePort">Increase port number and retry if the server failed to start.</param>
        public IpcServer(IPEndPoint _endPoint,string alias=null,Logger _logger=null,bool UseAlternatePort=false)
        {
            logger= _logger;
        retry:
            try
            {

                EndPoint = _endPoint;
                logger?.Info("Staring IPC server at "+EndPoint.ToString());

                var config = new NetPeerConfiguration("SausageIPC")
                {
                    AutoFlushSendQueue = true,
                    Port=EndPoint.Port,
                };
                config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
                config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
                _server=new NetServer(config);
                _server.Start();
                NetworkThread=new Thread(() =>
                {
                    while (!Stopping)
                    {
                        Process(_server.WaitMessage(200));
                    }
                });
                NetworkThread.Start();
                if (alias == null) { alias = EndPoint.ToString(); }
                Alias = alias;

            }
            catch (Exception ex)
            {
                logger.Error(ex);
                

                if (UseAlternatePort) { EndPoint.Port++; goto retry; }
                else { throw ex; }
            }
        }

        private void Process(NetIncomingMessage msg)
        {
            if(msg == null) { return; }
            // logger?.Info($"message received:{msg.MessageType},{msg.SequenceChannel}");
            switch (msg.MessageType)
            {
                case NetIncomingMessageType.ConnectionApproval:
                    {
                        // logger?.Info(msg.SenderEndPoint.ToString()+" is attempting to connect.");
                        var message = new IpcMessage(msg);
                        var args = new HandshakeEventArgs()
                        {
                            EndPoint=msg.SenderEndPoint,
                            Alias=message.MetaData["Alias"]
                        };
                        OnHandshake?.Invoke(this, args);
                        if (args.Cancel)
                        {
                            msg.SenderConnection.Deny(args.DenyReason);
                        }
                        else
                        {
                            var response = _server.CreateMessage();
                            args.ApproveResponse.Serialize(response);
                            msg.SenderConnection.Approve(response);
                            Clients.Add(msg.SenderEndPoint, new Client()
                            {
                                Alias=args.Alias,
                                Connection=msg.SenderConnection,
                                EndPoint=msg.SenderEndPoint,
                            });
                        }
                        break;
                    }
                case NetIncomingMessageType.ConnectionLatencyUpdated:
                    {
                        Client c;
                        if (Clients.TryGetValue(msg.SenderEndPoint, out c))
                        {
                            c.Latency=msg.ReadFloat();
                        }
                        else
                        {
                            msg.SenderConnection.Disconnect("Unauthorized");
                        }
                        break;
                    }
                case NetIncomingMessageType.StatusChanged:
                    {
                        NetConnectionStatus status = (NetConnectionStatus)msg.ReadByte();
                        // logger?.Trace($"Status changed:{status}");
                        if (status == NetConnectionStatus.Disconnected)
                        {
                            Client c;
                            if (Clients.TryGetValue(msg.SenderEndPoint,out c))
                            {
                                logger?.Debug($"{msg.SenderEndPoint} disconnected");
                                OnClientDisonnected?.Invoke(this, c);
                                Clients.Remove(msg.SenderEndPoint);
                            }
                        }
                        else if (status == NetConnectionStatus.Connected)
                        {
                            Client c;
                            if(Clients.TryGetValue(msg.SenderEndPoint,out c))
                            {
                                logger?.Debug($"{msg.SenderEndPoint} [{c.Alias}] connected");
                                OnClientConnected?.Invoke(this,c);
                            }
                            else
                            {
                                msg.SenderConnection.Disconnect("Unauthorized");
                            }
                        }
                        break;
                    }
                case NetIncomingMessageType.Data:
                    {
                        Client sender;

                        // Don't respond to shit
                        if (!Clients.TryGetValue(msg.SenderEndPoint, out sender))
                        {
                            break;
                        }
                        var message=new IpcMessage(msg);
                        switch (message.MessageType)
                        {
                            case MessageType.Message:
                                OnMessageReceived?.Invoke(this,
                                new MessageReceivedEventArgs()
                                {
                                    Sender = sender,
                                    Message=message
                                });
                                break;
                            case MessageType.Query:
                                {
                                    var args = new QueryEventArgs(message);
                                    OnQuerying?.Invoke(this, args);
                                    args.ReplyMessage.MessageType=MessageType.Reply;
                                    Send(args.ReplyMessage, sender);
                                    break;
                                }
                            case MessageType.Reply:
                                {
                                    OnReplyReceived?.Invoke(this, new ReplyReceivedEventArgs()
                                    {
                                        Sender=sender,
                                        Message=message
                                    });
                                    break;
                                }
                        }
                        break;
                    }
                case NetIncomingMessageType.DebugMessage:
                    {
                        logger?.Debug(msg.ReadString());
                        break;
                    }
            }
        }
        public void Stop(string byeMessage)
        {
            Stopping=true;
            _server.Shutdown(byeMessage);
            NetworkThread.Join();
        }

        public IpcServer(string ipport, string alias = null,Logger _logger=null, bool UseAlternatePort = false) : this(Helper.StringToEP(ipport),alias , _logger,UseAlternatePort) { }
        #endregion

        public IPEndPoint GetClientByAlias(string alias)
        {
            foreach (var client in Clients.Values)
            {
                if (client.Alias ==alias) { return client.EndPoint; }
            }
            throw new KeyNotFoundException("No client associated with alias:"+alias+" was found");
        }


        /// <summary>
        /// Send a message to specified client(non-blocking).
        /// </summary>
        /// <param name="message"></param>
        /// <param name="recepient"></param>
        /// <param name="deliveryMethod"></param>
        public void Send(IpcMessage message,Client recepient, NetDeliveryMethod deliveryMethod=NetDeliveryMethod.Unknown)
        {
            if(deliveryMethod== NetDeliveryMethod.Unknown) { deliveryMethod=DefaultDeliveryMethod; }
            var msg = _server.CreateMessage();
            message.Serialize(msg);
            _server.SendMessage(msg, recepient.Connection, deliveryMethod,(int)message.MessageType);
        }

        /// <summary>
        /// Query a message and get response from client(blocking).
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="target">The client to get response from</param>
        /// <param name="timeout">Timeout in milliseconds</param>
        /// <param name="deliveryMethod"></param>
        /// <returns>The response from the client if received in specified time window; otherwise, null.</returns>
        public IpcMessage Query(IpcMessage message, Client target,int timeout, NetDeliveryMethod deliveryMethod = NetDeliveryMethod.Unknown)
        {
            message.MessageType = MessageType.Query;
            InProgreeQueries.Add(message.QueryID = GetQueryID());
            IpcMessage Reply = null;
            AutoResetEvent Replied = new AutoResetEvent(false);
            var handler = new EventHandler<ReplyReceivedEventArgs>((s, args) =>
            {
                IpcMessage reply = args.Message;

                // check query id
                if (args.Sender==target && reply.QueryID == message.QueryID)
                {
                    Reply = reply;
                    Replied.Set();
                }
            });
            OnReplyReceived += handler;
            Send(message, target, deliveryMethod);
            Replied.WaitOne(timeout);
            OnReplyReceived -= handler;
            InProgreeQueries.Remove(message.QueryID);
            return Reply;
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
        private static string GetRandomString(int length)
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var stringChars = new char[length];
            var random = new Random();

            for (int i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }

            var finalString = new String(stringChars);
            return finalString;
        }
    }
}
