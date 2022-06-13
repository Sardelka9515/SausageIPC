using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;
using System.Web;
using System.Linq;
using System.Net;

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
        public MessageType MessageType { get; set; }
        public ReplyStatus ReplyStatus { get; set; }
        public Dictionary<string, string> MetaData { get; set; } = new Dictionary<string, string>();
        public byte[] Data { get; set; }
        internal byte[] Serialize()
        {
            List<byte> data = new List<byte>();
            data.Add((byte)MessageType);
            if(MessageType== MessageType.Reply)
                data.Add((byte)ReplyStatus);

            data.AddInt(MetaData.Count);
            foreach(var kv in MetaData)
            {
                data.AddString(kv.Key);
                data.AddString(kv.Value);
            }
            return data.ToArray();
        }
        internal void Deserialize(byte[] data)
        {
            var reader=new BitReader(data);
            
            if((MessageType = (MessageType)reader.ReadByte()) == MessageType.Reply)
            {
                ReplyStatus = (ReplyStatus)reader.ReadByte();
            }
            var count = reader.ReadInt();
            for(int i=0;i<count; i++)
            {
                MetaData.Add(reader.ReadString(),reader.ReadString());
            }
            Data = data.Skip(reader.CurrentIndex).ToArray();
        }
    }
    /*
    public class IpcMessage
    {
        public IpcMessage(string RawMessage)
        {
            Parameters=new IpcPrameter(EncodedDictionary:RawMessage);

            if (Recipient == null) { Recipient = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 0); } 
            if(Sender==null){ Sender = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 0); }
        }
        public IpcMessage(MessageType type=MessageType.Info, ReplyStatus status = ReplyStatus.Unused, string Queryid = "", IPEndPoint recipient=null,IPEndPoint sender=null, string header = "")
        {
            Parameters = new IpcPrameter();
            Type =type;
            Status=status;  
            if (recipient == null) { recipient = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 0); }
            if (sender == null) { sender = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 0); }
            if(header!= "") { Header = header; }
            if (Queryid != ""){QueryID = Queryid;}
            Recipient = recipient;
            Sender=sender;
        }
        /// <summary>
        /// All parameters within this message. Values with keys starting with "!If" shall not be modified.
        /// </summary>
        public IpcPrameter Parameters { get; set; }
        /// <summary>
        /// Can be "Info", "Query", or "Reply".
        /// </summary>
        public MessageType Type
        {
            get { return (MessageType)int.Parse(Parameters.Get("!If.Te")) ; }
            set { Parameters.SetInfo("!If.Te", ((int)value).ToString()); }
        }
        /// <summary>
        /// Used to indicate query result in a Reply message, can be "Error", "Unhandled", "Succeeded" or "Unused".
        /// </summary>
        public ReplyStatus Status
        {
            get { return (ReplyStatus)int.Parse(Parameters.Get("!If.Ss")); }
            set { Parameters.SetInfo("!If.Ss", ((int)value).ToString()); }
        }
        public string QueryID
        {
            get { return Parameters.Get("!If.QID"); }
            set { Parameters.SetInfo("!If.QID", value); }
        }
        public string Header
        {
            get { return Parameters.Get("!If.Hr"); }
            set { Parameters.SetInfo("!If.Hr", value); }
        }
        public string ForwardTarget
        {
            get { return Parameters.Get("!If.FT"); }
            set { Parameters.SetInfo("!If.FT", value); }
        }
        public string ForwardTargetAlias
        {
            get { return Parameters.Get("!If.FTA"); }
            set { Parameters.SetInfo("!If.FTA", value); }
        }
        /// <summary>
        /// No need to specify.
        /// </summary>
        public IPEndPoint Sender
        {
            get {
                string[] ss = Regex.Split(Parameters.Get("!If.Sr"), ":");
                return new IPEndPoint(IPAddress.Parse(ss[0]),int.Parse(ss[1])); 
            }
            set { Parameters.SetInfo("!If.Sr", value.ToString()); }
        }
        /// <summary>
        /// IpcServer will take care of this property. No need to specify.
        /// </summary>
        public string SenderAlias
        {
            get { return Parameters.Get("!If.SA"); }
            set { Parameters.SetInfo("!If.SA", value); }
        }
        public IPEndPoint Recipient
        {
            get {
                string[] ss = Regex.Split(Parameters.Get("!If.Rt"), ":");
                return new IPEndPoint(IPAddress.Parse(ss[0]), int.Parse(ss[1]));
            }
            set { Parameters.SetInfo("!If.Rt", value.ToString()); }
        }
        public string RecipientAlias
        {
            get { return Parameters.Get("!If.RA"); }
            set { Parameters.SetInfo("!If.RA", value); }
        }
        public string Raw
        {
            get { return Parameters.DumpToString(); }
        }
        /// <summary>
        /// get all parameters in human-readable format
        /// </summary>
        /// <returns></returns>
        public string DumpReadable(bool UseHeaderFooter = true, string header = "DumpBeginHere", string footer = "DumpEndHere")
        {
            return Parameters.DumpKVPair(UseHeaderFooter,header,footer);
        }
    }








































    public class IpcMessageOld
    {
        public IpcMessageOld(string RawMessage)
        {
            try
            {
                string[] sections = Regex.Split(RawMessage, "/");
                Type = (MessageType)Enum.Parse(typeof(MessageType), HttpUtility.UrlDecode(sections[0]));
                Status = (ReplyStatus)Enum.Parse(typeof(ReplyStatus), HttpUtility.UrlDecode(sections[1]));
                Body = HttpUtility.UrlDecode(sections[2]);
                QueryID = HttpUtility.UrlDecode(sections[3]);
            }
            catch (Exception ex)
            {
                IpcDebug.Write(ex, "MessageDecode");
                throw ex;
            }
        }
        public IpcMessageOld(MessageType type, string[] messages, ReplyStatus status = ReplyStatus.Unused, string Queryid = "UNUSED")
        {
            Type = type;
            List<string> ss = new List<string>() { };
            foreach (string s in messages)
            {
                ss.Add(HttpUtility.UrlEncode(s));
            }
            Body = string.Join("/", ss);
            Status = status;
            QueryID = Queryid;
        }
        public IpcMessageOld(MessageType type, string body, ReplyStatus status = ReplyStatus.Unused, string Queryid = "UNUSED")
        {
            Type = type;
            List<string> ss = new List<string>() { };
            Body = body;
            Status = status;
            QueryID = Queryid;
        }

        /// <summary>
        /// Can be "Inform", "Query", or "Reply".
        /// Value can only be changed using the constructor.
        /// </summary>
        public MessageType Type { get; private set; }

        /// <summary>
        /// Can be "Error", "Unhandled", "Succeeded" or "Unused".
        /// Value can only be changed using the constructor.
        /// </summary>
        public ReplyStatus Status { get; private set; }
        /// <summary>
        /// Contains the message body.
        /// Value can only be changed using the constructor.
        /// </summary>
        public string Body { get; private set; }
        /// <summary>
        /// Values are extracted from the "Body" property, readonly.
        /// </summary>
        public string[] Messages
        {
            get
            {
                try
                {
                    List<string> ss = new List<string>() { };
                    foreach (string s in Regex.Split(Body, "/"))
                    {
                        ss.Add(HttpUtility.UrlDecode(s));
                    }

                    return ss.ToArray();
                }
                catch { return new string[] { Body }; }
            }
        }

        /// <summary>
        /// Used to distinguish between diffrent replies. 
        /// </summary>
        public string QueryID { get; set; }

        /// <summary>
        /// The message encoded into an single string.
        /// </summary>
        public string Output
        {
            get
            {
                string etype = HttpUtility.UrlEncode(Type.ToString());
                string estatus = HttpUtility.UrlEncode(Status.ToString());
                string ebody = HttpUtility.UrlEncode(Body);
                string equeryid = HttpUtility.UrlEncode(QueryID.ToString());
                return string.Join("/", etype, estatus, ebody, equeryid);
            }
        }

    }

}

    */
}