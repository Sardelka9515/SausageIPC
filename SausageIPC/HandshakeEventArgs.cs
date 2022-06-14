using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace SausageIPC
{
    public class HandshakeEventArgs : EventArgs
    {
        public string Alias { get;internal set; }
        public IPEndPoint EndPoint { get;internal set; }
        /// <summary>
        /// Deny the connection request
        /// </summary>
        /// <param name="reason"></param>
        public void Deny(string reason)
        {
            DenyReason=reason;
            Cancel=true;
        }
        /// <summary>
        /// Aprrove the connection request and send back a response
        /// </summary>
        /// <param name="response"></param>
        public void Aprrove(IpcMessage response=null)
        {
            ApproveResponse=response;
            Cancel=false;
        }
        internal IpcMessage ApproveResponse { get; set; }
        internal string DenyReason { get; set; }
        internal bool Cancel { get; set; } = false;
    }
}
