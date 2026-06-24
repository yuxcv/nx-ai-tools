using System;
using System.Reflection;
using NXOpen;

public class AutoServer
{
    public static int Startup()
    {
        try
        {
            var t = Assembly.LoadFrom(@"C:\nx_custom\startup\Server.dll")
                           .GetType("NXOpenRemotingService");
            var m = t.GetMethod("Start");
            if(m != null) m.Invoke(null, null);
        }
        catch{} // Startup() 必须返回 0，吞掉所有异常防止 NX 启动中断
        return 0;
    }

    public static int GetUnloadOption(string arg)
    {
        return System.Convert.ToInt32(Session.LibraryUnloadOption.AtTermination);
    }
}
