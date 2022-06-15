using SausageIPC;
var logger = new Logger(true)
{
    UseConsole = true,
    LogLevel=0
};
var clogger = new Logger(true)
{
    UseConsole = true,
    LogLevel=0,
    Name="Client"
};
try
{

    var server = new IpcServer("0.0.0.0:5688", "IpcServer", logger);
    server.OnMessageReceived+=(s, e) => { Console.WriteLine(e.Message.Dump()); };
    var client = new IpcClient(null, "IpcClient",clogger);
    client.Connect("127.0.0.1", 5688, 5000, new IpcMessage());
    var msg = new IpcMessage();
    msg.MetaData.Add("Header", "Hello");
    msg.MetaData.Add("Peep", "Poop");
    msg.MetaData.Add("Boom", "Poop");
    msg.MetaData.Add("Blah", "Poop");
    msg.Data=new byte[10] { 1,2,3,4,5,6,7,8,9,10};
    client.Send(msg);
    var w = new System.Diagnostics.Stopwatch();
    while (Console.ReadLine()!="exit")
    {
        w.Restart();
        var result = client.Query(msg);
        long ticks = w.ElapsedTicks;
        long ms=w.ElapsedMilliseconds;
        Console.WriteLine($"QueryResult[{ticks}/{ms}]:\n"+result.Dump());
    }
    client.Disconnect("Bye");
}
catch(Exception ex)
{
    Console.WriteLine(ex.ToString());
}
/*
*/