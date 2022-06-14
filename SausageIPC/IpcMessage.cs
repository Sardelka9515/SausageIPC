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
        Message = 1,
        Query = 2,
        Reply = 3,
    }
    public enum ReplyStatus : byte
    {
        Error = 1,
        Unhandled = 2,
        Sucess = 0,
        Unused = 3,
    }
    public class IpcMessage {
        public IpcMessage() { }
        internal ReplyStatus ReplyStatus { get; set; } = ReplyStatus.Unused;
        internal bool IsValid { get; set; } = true;
        public byte[] Data { get; set; }
        internal IpcMessage(NetIncomingMessage msg)
        {
            Deserialize(msg);
        }
        internal MessageType MessageType { get; set; } = MessageType.Message;
        internal int QueryID { get; set; }
        public Dictionary<string, string> MetaData { get; set; } = new Dictionary<string, string>();
        internal byte[] Serialize()
        {
            List<byte> data = new List<byte>();
            switch (MessageType)
            {
                case MessageType.Query:
                    data.AddInt(QueryID);
                    break;
                case MessageType.Reply:
                    data.Add((byte)ReplyStatus);
                    data.AddInt(QueryID);
                    break ;
            }

            data.AddInt(MetaData.Count);
            foreach(var kv in MetaData)
            {
                data.AddString(kv.Key);
                data.AddString(kv.Value);
            }
            data.AddRange(Data);
            return data.ToArray();
        }
        internal void Deserialize(NetIncomingMessage msg)
        {
            if (msg.Data.Length==0) {IsValid=false; return; }
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