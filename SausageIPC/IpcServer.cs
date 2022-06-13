using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace SausageIPC
{
    public class IpcServer
    {

        public IPEndPoint EndPoint;
        private TcpClient server;
        private IPEndPoint InvalidEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 0);
        public string Alias;
        public event EventHandler<IpcMessage> OnInfoReceived;
        public event EventHandler<QueryEventArgs> OnQuerying;
        private Dictionary<string, EventHandler<QueryEventArgs>> QueryHandlers=new Dictionary<string, EventHandler<QueryEventArgs>>();
        /// <summary>
        /// If set to true,the client is allowed to connect with alias already present on the server. Could cause issue when forwarding messages.
        /// </summary>
        public bool AllowDuplicateAlias=false;
        public List<IpcClientInfo> Clients=new List<IpcClientInfo>();

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
                server = new TcpClient(_endPoint);
                server.Client.BeginAccept(,)
                Task.Run(() =>
                {
                    while (true)
                    {
                        Clients.Add(new IpcClientInfo()
                        {
                            Client = server.Client.acc()
                        });
                    }
                });
                _server = new SimpleTcpServer(EndPoint.ToString());
                _server.Events.ClientConnected += ClientConnected;
                _server.Events.ClientDisconnected += ClientDisconnected;
                _server.Events.DataReceived += DataReceived;
                _server.Start();
                if (alias == null) { alias = EndPoint.ToString(); }
                Alias = alias;

                RegisterQueryHandler("!If.GetClients", (s, e) => 
                {
                    foreach(IpcClientInfo client in Clients)
                    {
                        e.Reply.Parameters.Set(client.EndPoint.ToString(), client.Alias);
                        e.Reply.Status = ReplyStatus.Succeeded;
                    }
                });
            }
            catch (Exception ex)
            {
                IpcDebug.Write(ex, "StartIPC", LogType.Warning);
                

                if (UseAlternatePort) { EndPoint.Port++; goto retry; }
                else { throw ex; }
            }
        }
        public IpcServer(string ipport, string alias = null, bool UseAlternatePort = false) : this(Helper.StringToEP(ipport),alias , UseAlternatePort) { }
        #endregion
        private void ClientConnected(object sender, ClientConnectedEventArgs e)
        {
            Task.Run(() =>
            {
                if (!QueryClientInfo(e.EndPoint)) { return; }
                e.ClientInfo = GetClientInfo(e.EndPoint);
                OnClientConnected?.Invoke(this, e);
            });
            
        }

        

        private void ClientDisconnected(object sender, ClientDisconnectedEventArgs e)
        {
            
            Task.Run(() => { 
                OnClientDisconnected?.Invoke(this, e);

                if (Clients.Contains(GetClientInfo(e.EndPoint)))
                {
                    Clients.Remove(GetClientInfo(e.EndPoint));
                }

                    
            });
        }
        private bool QueryClientInfo(IPEndPoint endPoint)
        {
            string alias = Query("!",endPoint).SenderAlias;
            try
            {
                if (!AllowDuplicateAlias)
                {
                    foreach (IpcClientInfo client in Clients)
                    {
                        if (client.Alias == alias) { _server.DisconnectClient(endPoint.ToString()); return false; }
                    }
                }
                if (!Clients.Contains(GetClientInfo(endPoint)))
                {
                    Clients.Add(new IpcClientInfo(endPoint, alias));
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                IpcDebug.Write(ex, "QueryClientInfo");
                return false;
            }
        }
        public IpcClientInfo GetClientInfo(IPEndPoint ep)
        {
            foreach(var client in Clients)
            {
                if(Equals(client.EndPoint.ToString(), ep.ToString())) { return client; }
            }
            return new IpcClientInfo(Helper.StringToEP("127.0.0.1:0"), "NonExistent");
        }
        public IPEndPoint GetClientByAlias(string alias)
        {
            foreach (var client in Clients)
            {
                if (client.Alias ==alias) { return client.EndPoint; }
            }
            throw new KeyNotFoundException("No client associated with alias:"+alias+" was found");
        }
        private void DataReceived(object sender, DataReceivedEventArgs e)
        {
            

            IpcDebug.RestartCounter();
            IpcDebug.Record("Message received");
            IpcMessage msg;

            

            try
            {
                msg= new IpcMessage(Encoding.UTF8.GetString(e.Data));
                msg.Sender = e.EndPoint;
            }
            catch (Exception ex)
            {
                IpcDebug.Write(ex, "ParseMessage");
                return;
            }
            IpcDebug.Record("Message parsed");
            HandleData(msg);
            
            IpcDebug.Record("processing completed");
        }
        void HandleData(IpcMessage msg)
        {
            try
            {
                

                if (msg.Type==MessageType.Query)
                {
                    try
                    {
                        IpcDebug.Record("Processing query");

                        //Handle forward
                        string FAlias = msg.ForwardTargetAlias;
                        string FT=msg.ForwardTarget;
                        if ((FAlias != "")||(FT!=""))
                        {
                            if (FAlias != "")
                            {
                                FT = GetClientByAlias(FAlias).ToString();
                            }
                            IPEndPoint Target = Helper.StringToEP(FT);
                            IpcDebug.Write("Forwarding query from " + msg.Sender + " to " + msg.ForwardTargetAlias, "Forward", LogType.Info);
                            msg.Recipient = Target;
                            IpcMessage Reply = Query(msg);

                            Reply.Recipient = msg.Sender;

                            Send(Reply);
                            IpcDebug.Write("Forward completed", "Forward", LogType.Info);

                            return;
                        }

                        
                        QueryEventArgs args = new QueryEventArgs(msg);
                        OnQuerying?.Invoke(this, args);

                        if (QueryHandlers.ContainsKey(msg.Header)) { QueryHandlers[msg.Header].Invoke(this,args); }

                        IpcDebug.Record("Sending Reply");
                        args.Reply.SenderAlias = Alias;
                        Send(args.Reply);
                    }
                    catch(Exception ex)
                    {

                        try
                        {
                            //Try sending the error message back to client
                            IpcMessage ErrorReply = new IpcMessage(MessageType.Reply, ReplyStatus.Error, recipient: msg.Sender, header: "Server Error");
                            ErrorReply.Parameters.Set("Error", ex.ToString());
                            ErrorReply.Parameters.Set("Error message", ex.Message);
                            ErrorReply.QueryID = msg.QueryID;
                            Send(ErrorReply);
                        }
                        catch(Exception exc)
                        {
                            IpcDebug.Write(exc, "ReplyError");
                        }
                        IpcDebug.Write(ex, "HandleQuery");
                    }
                }
                if (msg.Type == MessageType.Info)
                {
                    //Handle forward
                    string FAlias = msg.ForwardTargetAlias;
                    string FT = msg.ForwardTarget;
                    if ((FAlias != "") || (FT != ""))
                    {
                        if (FAlias != "")
                        {
                            FT = GetClientByAlias(FAlias).ToString();
                        }
                        IPEndPoint Target = Helper.StringToEP(FT);
                        IpcDebug.Write("Forwarding info from " + msg.Sender + " to " + msg.ForwardTargetAlias, "Forward", LogType.Info);
                        msg.Recipient = Target;
                        Send(msg);

                        return;
                    }
                    OnInfoReceived?.Invoke(this, msg);
                }

            }
            catch (Exception ex)
            {
                IpcDebug.Write(ex, "IpcServer.HandleData");
            }

        }
        public IpcMessage Query(IpcMessage message,TimeSpan Timeout = default)
        {
            if (Timeout == default)
            {
                Timeout = TimeSpan.FromMilliseconds(5000);
            }

            message.Type = MessageType.Query;
            if (message.QueryID == "") { message.QueryID = GetRandomString(10); }
            message.SenderAlias = Alias;

            IpcMessage Reply=null;
            
            AutoResetEvent Replied = new AutoResetEvent(false);
            EventHandler<DataReceivedEventArgs> handler = new EventHandler<DataReceivedEventArgs>((s, args) =>
            {
                IpcMessage reply = new IpcMessage(Encoding.UTF8.GetString(args.Data));

                //check query id
                if (reply.QueryID == message.QueryID)
                {


                    Reply = reply;
                    Replied.Set();
                }
            });
            _server.Events.DataReceived += handler;
            Send(message);

            Replied.WaitOne(Timeout);
            _server.Events.DataReceived -= handler;
            return Reply;
        }
        public IpcMessage Query(string header,IPEndPoint target, TimeSpan Timeout = default)
        {
            return Query(new IpcMessage(header:header,recipient:target),Timeout);
        }

        /// <summary>
        /// Register an EventHandler to handle query with certain headers. One header can only have one handler.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="handler"></param>
        /// <returns>Indicates whether the handler is registered successfully</returns>
        public bool RegisterQueryHandler(string header, EventHandler<QueryEventArgs> handler)
        {
            if (QueryHandlers.ContainsKey(header)) { return false; }
            else { QueryHandlers.Add(header, handler);return true; }
        }
        /// <summary>
        /// Unregister the associated handler
        /// </summary>
        /// <param name="header"></param>
        public void UnregisterQueryHandler(string header)
        {
            if (QueryHandlers.ContainsKey(header)) { QueryHandlers.Remove(header); }
        }
        public void Send(IpcMessage message)
        {


            if (String.IsNullOrEmpty(message.SenderAlias)) { message.SenderAlias = Alias; }
            _server.Send(message.Recipient.ToString(), Encoding.UTF8.GetBytes(message.Raw));



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
