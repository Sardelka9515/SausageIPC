using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Lidgren.Network;

namespace SausageIPC
{
    public class Client
    {
        public IPEndPoint EndPoint { get;set; }
        public string Alias { get;set; }
        internal NetConnection Connection { get;set; }
        public float Latency { get;internal set; }

        /// <summary>
        /// You can store any client-specific informations here
        /// </summary>
        public object Context { get;set; }
    }
}
