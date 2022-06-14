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
        private IPEndPoint InvalidEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 0);
        public string Alias;
        public NetDeliveryMethod DefaultDeliveryMethod = NetDeliveryMethod.Unreliable;
        public event EventHandler<IpcMessage> OnMessageReceived;
        public event EventHandler<HandshakeEventArgs> OnHandshake;
        public event EventHandler<Client> OnConnected;
        public event EventHandler<Client> OnDisonnected;
        public event EventHandler<QueryEventArgs> OnQuery;
        private event EventHandler<ReplyReceivedEventArgs> OnReplyReceived;
        private HashSet<int> InProgreeQueries=new HashSet<int>();
        private Dictionary<string, EventHandler<QueryEventArgs>> QueryHandlers=new Dictionary<string, EventHandler<QueryEventArgs>>();
        public Dictionary<IPEndPoint,Client> Clients=new Dictionary<IPEndPoint,Client>();
        /// <summary>
        /// If set to true,the client is allowed to connect with alias already present on the server. Could cause issue when forwarding messages.
        /// </summary>
        public bool AllowDuplicateAlias=false;

        private bool Stopping = false;

        #region Constructors
        /// <summary>
        /// Start an IPC server.
        /// </summary>
        /// <param name="_endPoint">IPEndPoint to listen for connections</param>
        /// <param name="Alias"></param>
        /// <param name="UseAlternatePort">Increase port number and retry if the server failed to start.</param>
        public IpcServer(IPEndPoint _endPoint,string alias=null,bool UseAlternatePort=false)
        {
            
        retry:
            try
            {

                EndPoint = _endPoint;
                IpcDebug.Write("Staring IPC server at "+EndPoint.ToString(),"StartIPC",LogType.Info);

                var config = new NetPeerConfiguration("Sardelka9515.SausageIPC")
                {
                    AutoFlushSendQueue = true,
                    LocalAddress =EndPoint.Address,
                    Port=EndPoint.Port,
                };
                config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
                config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
                _server=new NetServer(config);
                _server.Start();
                Task.Run(() =>
                {
                    while (!Stopping)
                    {
                        PollEvents(_server.WaitMessage(200));
                    }
                });
                if (alias == null) { alias = EndPoint.ToString(); }
                Alias = alias;

            }
            catch (Exception ex)
            {
                IpcDebug.Write(ex, "StartIPC", LogType.Warning);
                

                if (UseAlternatePort) { EndPoint.Port++; goto retry; }
                else { throw ex; }
            }
        }

        private void PollEvents(NetIncomingMessage msg)
        {
            if(msg == null) { return; }
            switch (msg.MessageType)
            {
                case NetIncomingMessageType.ConnectionApproval:
                    {
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
                            if (args.ApproveResponse==null)
                            {
                                msg.SenderConnection.Approve();
                            }
                            else
                            {
                                var response = _server.CreateMessage();
                                response.Data = args.ApproveResponse.Serialize();
                                msg.SenderConnection.Approve(response);
                            }
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

                        if (status == NetConnectionStatus.Disconnected)
                        {
                            Client c;
                            if (Clients.TryGetValue(msg.SenderEndPoint,out c))
                            {
                                OnDisonnected?.Invoke(this, c);
                                Clients.Remove(msg.SenderEndPoint);
                            }
                        }
                        else if (status == NetConnectionStatus.Connected)
                        {
                            Client c;
                            if(Clients.TryGetValue(msg.SenderEndPoint,out c))
                            {
                                OnConnected?.Invoke(this,c);
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
                        var message=new IpcMessage(msg);
                        switch (message.MessageType)
                        {
                            case MessageType.Info:
                                OnMessageReceived?.Invoke(this, message);
                                break;
                            case MessageType.Query:
                                {
                                    OnQuery?.Invoke(this, new QueryEventArgs(message));
                                    break;
                                }
                            case MessageType.Reply:
                                {
                                    OnReplyReceived?.Invoke(this, new ReplyReceivedEventArgs()
                                    {
                                        SenderEndPoint= msg.SenderEndPoint,
                                        Message=message
                                    });
                                    break;
                                }
                        }
                        break;
                    }
            }
        }

        public IpcServer(string ipport, string alias = null, bool UseAlternatePort = false) : this(Helper.StringToEP(ipport),alias , UseAlternatePort) { }
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
        /// Register an EventHandler to handle query with specified headers. One header can have multiple handlers.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="handler"></param>
        public void RegisterQueryHandler(string header, EventHandler<QueryEventArgs> handler)
        {
            if (!QueryHandlers.ContainsKey(header))
            {
                QueryHandlers.Add(header, handler);
            }
        }
        /// <summary>
        /// Unregister the associated handlers
        /// </summary>
        /// <param name="header"></param>
        public void UnregisterQueryHandler(string header)
        {
            if (QueryHandlers.ContainsKey(header)) { QueryHandlers.Remove(header); }
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
            msg.Data=message.Serialize();
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
                if (args.SenderEndPoint==target.EndPoint && reply.QueryID == message.QueryID)
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
