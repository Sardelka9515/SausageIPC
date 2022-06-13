using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using SimpleTcp;
using System.Net;

namespace SausageIPC
{
    public class IpcClient
    {
        SimpleTcpClient _client;
        public string Alias;
        public event EventHandler<IpcMessage> OnInfoReceived;
        public event EventHandler<QueryEventArgs> OnQuerying;
        public event EventHandler<ClientDisconnectedEventArgs> OnDisconnected;
        private Dictionary<string, EventHandler<QueryEventArgs>> QueryHandlers = new Dictionary<string, EventHandler<QueryEventArgs>>();

        public IPEndPoint ServerEndpoint;
        public IPEndPoint LocalEndpoint;
        public IpcClient(IPEndPoint server, string alias)
        {
            
            _client = new SimpleTcpClient(server.ToString());
            _client.Events.DataReceived += DataReceived;
            _client.Events.Disconnected += Disconnected; ;
            _client.Connect();

            ServerEndpoint = server;
            LocalEndpoint=_client.LocalEndpoint;
            if (String.IsNullOrEmpty(alias)) { alias=LocalEndpoint.ToString(); }
            Alias = alias;
        }

        private void Disconnected(object sender, ClientDisconnectedEventArgs e)
        {
            OnDisconnected?.Invoke(this, e);
        }

        public IpcClient(string serverIpPort, string alias) : this(Helper.StringToEP(serverIpPort),alias) { }
        private void DataReceived(object sender, DataReceivedEventArgs e)
        {
            IpcMessage msg;
            try
            {
                msg = new IpcMessage(Encoding.UTF8.GetString(e.Data));
                msg.Parameters.SetInfo("!Info.Sr", e.EndPoint.ToString());
            }
            catch (Exception ex)
            {
                IpcDebug.Write(ex, "ParseMessage");
                return;
            }
            HandleData(msg);
        }
        void HandleData(IpcMessage msg)
        {
            try
            {
                if (msg.Type == MessageType.Query)
                {

                    QueryEventArgs args = new QueryEventArgs(msg);
                    OnQuerying?.Invoke(this, args);
                    OnQuerying?.Invoke(this, args);

                    if (QueryHandlers.ContainsKey(msg.Header)) { QueryHandlers[msg.Header].Invoke(this, args); }

                    args.Reply.SenderAlias = Alias;
                    Send(args.Reply);
                }
                if (msg.Type == MessageType.Info)
                {

                    OnInfoReceived?.Invoke(this, msg);
                }

            }
            catch (Exception ex)
            {
                IpcDebug.Write(ex, "IpcClient.HandleData");
            }

        }

        /// <summary>
        /// Send a Query to server
        /// </summary>
        /// <param name="message">The message to send. No metadata need to be set, but ForwardTarget and ForwardTargetAlias can be set to send the query another client.</param>
        /// <param name="Timeout"></param>
        /// <returns>Returns the response from server.(or client if you have forward parameter set.)</returns>
        public IpcMessage Query(IpcMessage message, TimeSpan Timeout = default)
        {
            if (Timeout == default)
            {
                Timeout = TimeSpan.FromMilliseconds(5000);
            }
            IpcDebug.Record("Setting up query message");

            message.Type = MessageType.Query;
            message.QueryID = GetRandomString(10);
            message.SenderAlias = Alias;
            message.Recipient = ServerEndpoint;

            IpcMessage Reply = null;
            IpcDebug.Record("Registering callback event");
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
            _client.Events.DataReceived += handler;
            IpcDebug.Record("Preparing to send message");
            Send(message);

            Replied.WaitOne(Timeout);
            
            _client.Events.DataReceived -= handler;
            if (Reply == null) { throw new TimeoutException("Server did not respond in specified interval."); }
            return Reply;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg">The message to send. No metadata need to be set.</param>
        /// <param name="TargetAlias">The alias of another client you want to query.</param>
        /// <param name="Timeout"></param>
        /// <returns>Returns the response from another client.</returns>
        public IpcMessage Query(IpcMessage msg, string TargetAlias, TimeSpan Timeout = default)
        {
            msg.ForwardTargetAlias = TargetAlias;
            return Query(msg, Timeout);
        }

        /// <summary>
        /// Send a Info, Query or Reply. The metadata won't be altered.
        /// </summary>
        /// <param name="message"></param>
        public void Send(IpcMessage message)
        {
            if (String.IsNullOrEmpty(message.SenderAlias)) { message.SenderAlias = Alias; }
            message.Recipient = ServerEndpoint;
            IpcDebug.Record("Sending message");
            _client.Send(Encoding.UTF8.GetBytes(message.Raw));
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
            else { QueryHandlers.Add(header, handler); return true; }
        }
        /// <summary>
        /// Unregister the associated handler
        /// </summary>
        /// <param name="header"></param>
        public void UnregisterQueryHandler(string header)
        {
            if (QueryHandlers.ContainsKey(header)) { QueryHandlers.Remove(header); }
        }
        public List<IpcClientInfo> GetClients()
        {
            IpcMessage msg = Query(new IpcMessage(header:"!If.GetClients"));
            List<KeyValuePair<string, string>> clients = Helper.RemoveMd(msg.Parameters.Variables.ToList());
            List<IpcClientInfo> Clients= new List<IpcClientInfo>();
            foreach (KeyValuePair<string,string> pair in clients)
            {
                try
                {
                    Clients.Add(new IpcClientInfo(Helper.StringToEP(pair.Key), pair.Value));
                }
                catch {}
            }
            return Clients;
        }

        static string GetRandomString(int length)
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
