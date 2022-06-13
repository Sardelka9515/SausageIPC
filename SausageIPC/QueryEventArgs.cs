using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SettingsProvider;

namespace SausageIPC
{

    public class QueryEventArgs:EventArgs
    {
        public QueryEventArgs(IpcMessage qmsg)
        {
            Query = qmsg;
            Reply=new IpcMessage(MessageType.Reply,ReplyStatus.Unhandled,Query.QueryID,Query.Sender);

        }
        public IpcMessage Query;
        public IpcMessage Reply;
    }
}
