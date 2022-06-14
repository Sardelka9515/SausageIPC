using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;
using System.Web;
using System.Linq;
using System.Net;
using Lidgren.Network;

namespace SausageIPC
{
    public enum MessageType : byte
    {
        /// <summary>
        /// A message that doesn't need a reply or correspond to a query.
        /// </summary>
        Info = 1,
        Query = 2,
        Reply = 3,
    }
    public enum ReplyStatus : byte
    {
        Error = 1,
        Unhandled = 2,
        Succeeded = 0,
        Unused = 3,
    }
    public class IpcMessage {
        public IpcMessage() { }
        public ReplyStatus ReplyStatus { get; set; } = ReplyStatus.Unused;
        public byte[] Data { get; set; }
        internal IpcMessage(NetIncomingMessage msg)
        {
            Deserialize(msg);
        }
        internal MessageType MessageType { get; set; } = MessageType.Info;
        internal int QueryID { get; set; }
        public Dictionary<string, string> MetaData { get; set; } = new Dictionary<string, string>();
        internal byte[] Serialize()
        {
            List<byte> data = new List<byte>();

            if(MessageType== MessageType.Reply)
            {
                data.Add((byte)ReplyStatus);
                data.AddInt(QueryID);
            }
            else if (MessageType==MessageType.Query)
            {
                data.AddInt(QueryID);
            }

            data.AddInt(MetaData.Count);
            foreach(var kv in MetaData)
            {
                data.AddString(kv.Key);
                data.AddString(kv.Value);
            }
            return data.ToArray();
        }
        internal void Deserialize(NetIncomingMessage msg)
        {
            var reader=new BitReader(msg.Data);
            
            if((MessageType = (MessageType)msg.SequenceChannel) == MessageType.Reply)
            {
                ReplyStatus = (ReplyStatus)reader.ReadByte();
                QueryID=reader.ReadInt();
            }
            else if(MessageType == MessageType.Query)
            {
                QueryID=reader.ReadInt();
            }
            var count = reader.ReadInt();
            for(int i=0;i<count; i++)
            {
                MetaData.Add(reader.ReadString(),reader.ReadString());
            }
            Data = msg.Data.Skip(reader.CurrentIndex).ToArray();
        }
    }
}