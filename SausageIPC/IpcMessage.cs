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
        public byte[] Data { get; set; }=new byte[0];
        internal IpcMessage(NetIncomingMessage msg)
        {
            Deserialize(msg);
        }
        internal MessageType MessageType { get; set; } = MessageType.Message;
        internal int QueryID { get; set; }
        public Dictionary<string, string> MetaData { get; set; } = new Dictionary<string, string>();
        internal void Serialize(NetOutgoingMessage msg)
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
            Console.WriteLine($"serialized:{data.Count}");
            msg.Write(data.Count);
            msg.Write(data.ToArray());
        }
        internal void Deserialize(NetIncomingMessage msg)
        {
            if (msg.LengthBytes==0) {IsValid=false; return; }
            Console.WriteLine($"Deserializing message:{msg.LengthBytes}");
            int size;
            Console.WriteLine($"packet size:{size=msg.ReadInt32()}");
            var reader=new BitReader(msg.ReadBytes(size));
            
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
            Data = reader.ReadByteArray(size-reader.CurrentIndex);
        }
        /// <summary>
        /// Dump entire message to a human-readable format, used for debugging.
        /// </summary>
        /// <returns></returns>
        public string Dump()
        {
            var s = $"[Type]\n{MessageType}\n";
            if(MessageType == MessageType.Query)
            {
                s+=$"[QueryID]\n{QueryID}\n";
            }
            else if(MessageType == MessageType.Reply)
            {
                s+=$"[ReplyStatus]\n{ReplyStatus}\n";
            }
            s+="[MetaData]\n";
            foreach(var kv in MetaData)
            {
                s+=$"{kv.Key}={kv.Value}\n";
            }
            s+="[Data]\n{";
            s+=string.Join(",", Data)+"}\n";
            return s;
        }
    }
}