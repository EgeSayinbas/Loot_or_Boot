using System.Net;
using System.Net.Sockets;

public static class LanIpUtil
{
    public static string GetLocalIPv4()
    {
        try
        {
            foreach (var ni in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ni.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ni))
                    return ni.ToString();
            }
        }
        catch { }

        return "127.0.0.1";
    }
}
