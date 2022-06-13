using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace SausageIPC
{
    public enum LogType
    {
        Info,
        Warning,
        Error
    }
    public static class IpcDebug
    {

        private static Stopwatch Counter=new Stopwatch();
        public static bool EnablePerformanceCounter=false;
        public static Dictionary<string, long> RecordedEvents=new Dictionary<string, long>();
        public static event EventHandler<string> MessageReceived;
        public static void Write(Exception ex,string Interpretation="Unspecified", LogType logType=LogType.Error)
        {
            Write(ex.GetType() + "=>" + ex.Message, Interpretation, logType);
        }
        public static void Write(string Message, string Interpretation = "Unspecified",LogType logType = LogType.Error)
        {
            Task.Run(() =>
            {
                string output = string.Format("[{0}]\t|{2}<={1}|\t\t{3}", DateTime.Now.ToString(), Interpretation, logType.ToString(), Message);
                MessageReceived?.Invoke(null, output);
            });
        }

        public static void Record(string name)
        {
            if (!EnablePerformanceCounter) { return; }
            if (!Counter.IsRunning) { Counter.Restart(); }
            if (RecordedEvents.ContainsKey(name)) { RecordedEvents[name] = Counter.ElapsedTicks; }
            else { RecordedEvents.Add(name, Counter.ElapsedTicks); }
        }
        public static void RestartCounter()
        {
            if (!(Counter == null)) { Counter.Restart(); }
        }
        public static void PrintRecord()
        {
            foreach (KeyValuePair<string, long> kvp in RecordedEvents.ToArray())
            {
                Console.WriteLine("{0} : {1} ticks", kvp.Key, kvp.Value);
            }
        }

    }
}
