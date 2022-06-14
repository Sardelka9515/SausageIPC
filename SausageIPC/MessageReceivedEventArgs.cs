using System;
using System.Collections.Generic;
using System.Text;

namespace SausageIPC
{
    public class MessageReceivedEventArgs:EventArgs
    {
        public IpcMessage Message { get; internal set; }
        public Client Sender { get; internal set; }
    }
}
