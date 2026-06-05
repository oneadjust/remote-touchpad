using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace RemoteTouchpad.Web;

public static class AccessUrlProvider
{
    public static IReadOnlyList<string> GetAccessUrls(int port)
    {
        var localUrls = GetLocalIpAddresses()
            .OrderBy(GetAddressPriority)
            .ThenBy(address => address.ToString(), StringComparer.Ordinal)
            .Select(address => $"http://{address}:{port}/")
            .ToList();

        localUrls.Add($"http://127.0.0.1:{port}/");
        return localUrls;
    }

    public static string GetPrimaryAccessUrl(int port)
    {
        return GetAccessUrls(port).First();
    }

    public static string GetLocalQrPageUrl(int port)
    {
        return $"http://127.0.0.1:{port}/qr.html";
    }

    private static IEnumerable<IPAddress> GetLocalIpAddresses()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up)
            .Where(adapter => adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
            .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(address => address.Address)
            .Where(IsUsableAddress);
    }

    private static bool IsUsableAddress(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes is [127, _, _, _] or [169, 254, _, _] or [0, _, _, _])
        {
            return false;
        }

        if (bytes[0] >= 224 || bytes is [198, >= 18 and <= 19, _, _])
        {
            return false;
        }

        return IsPrivateAddress(bytes);
    }

    private static bool IsPrivateAddress(byte[] bytes)
    {
        return bytes[0] == 10
            || bytes is [192, 168, _, _]
            || bytes[0] == 172 && bytes[1] is >= 16 and <= 31;
    }

    private static int GetAddressPriority(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes switch
        {
            [192, 168, _, _] => 0,
            [10, _, _, _] => 1,
            [172, >= 16 and <= 31, _, _] => 2,
            _ => 9
        };
    }
}
