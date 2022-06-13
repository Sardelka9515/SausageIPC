using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text;
using System.Net;

namespace SausageIPC
{
    public static class Helper
    {
        public static IPEndPoint StringToEP(string s)
        {
            try
            {
                string[] ss = Regex.Split(s, ":");
                return new IPEndPoint(IPAddress.Parse(ss[0]), int.Parse(ss[1]));
            }
            catch
            {
                throw new ArgumentException("The given string is not a valid IpPort combination.");
            }
        }
        public static string GetString(this byte[] b)
        {
            return Encoding.UTF8.GetString(b);
        }
        public static byte[] GetBytes(this string s)
        {
            return Encoding.UTF8.GetBytes(s);
        }
        public static void AddInt(this List<byte> bytes, int i)
        {
            bytes.AddRange(BitConverter.GetBytes(i));
        }
        public static void AddString(this List<byte> bytes,string s)
        {
            var bs = s.GetBytes();
            bytes.AddInt(bs.Length);
            bytes.AddRange(bs);
        }
    }
}
