using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SausageIPC
{

    public class QueryEventArgs:EventArgs
    {
        public QueryEventArgs(IpcMessage qmsg)
        {
            Query = qmsg;
            ReplyMessage=new IpcMessage()
            {
                ReplyStatus=ReplyStatus.Unhandled,
                MessageType=MessageType.Reply,
            };
        }
        public void Reply(IpcMessage msg,ReplyStatus statusCode=ReplyStatus.Sucess)
        {
            if(msg == null) { throw new ArgumentNullException("Reply message was null."); }
            ReplyMessage= msg;
            msg.ReplyStatus=statusCode;
        }
        public IpcMessage Query { get; internal set; }
        internal IpcMessage ReplyMessage { get; set; }
    }
}
