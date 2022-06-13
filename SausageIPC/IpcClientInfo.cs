using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace SausageIPC
{
    public class IpcClientInfo
    {
        public IpcClientInfo(IPEndPoint endPoint,TcpClient client,string alias=null)
        {
            EndPoint= endPoint;
            if (string.IsNullOrEmpty(alias)) { alias = endPoint.ToString(); }
            Alias= alias;
            Client = client;
        }
        public IPEndPoint EndPoint { get;set; }
        public string Alias { get;set; }
        internal TcpClient Client { get; set; }
    }
}
