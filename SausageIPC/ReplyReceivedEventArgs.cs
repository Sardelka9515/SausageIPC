using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace SausageIPC
{
    internal class ReplyReceivedEventArgs:EventArgs
    {

        public Client Sender { get; set; }
        public IpcMessage Message { get; set; }
    }
}
