using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class LanDiscoveryHost : MonoBehaviour
{
    [Header("Discovery Settings")]
    [SerializeField] private int discoveryPort = 47777;     // UDP discovery port
    [SerializeField] private float announceInterval = 0.5f; // saniye
    [SerializeField] private ushort gamePort = 7777;        // UnityTransport port

    private CancellationTokenSource _cts;
    private UdpClient _udp;

    private string _codeCached = "";

    private const string AnnouncePrefix = "KEMPS_ANNOUNCE|";
    private const string QueryPrefix = "KEMPS_QUERY|";
    private const string ReplyPrefix = "KEMPS_REPLY|";

    private void OnDestroy()
    {
        StopHostDiscovery();
    }

    public void StartHostDiscovery(string lobbyCode, ushort port)
    {
        StopHostDiscovery();

        _codeCached = LanLobbyCodeUtil.Normalize(lobbyCode);
        gamePort = port;

        _cts = new CancellationTokenSource();

        // bind: discoveryPort dinle
        _udp = new UdpClient(AddressFamily.InterNetwork);
        _udp.EnableBroadcast = true;
        _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udp.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));

        _ = Task.Run(() => ReceiveLoop(_cts.Token));
        _ = Task.Run(() => AnnounceLoop(_cts.Token));

        Debug.Log($"[LanDiscoveryHost] Started. Code={_codeCached} discoveryPort={discoveryPort} gamePort={gamePort}");
    }

    public void StopHostDiscovery()
    {
        try { _cts?.Cancel(); } catch { }
        _cts = null;

        try { _udp?.Close(); } catch { }
        _udp = null;
    }

    private async Task AnnounceLoop(CancellationToken ct)
    {
        // broadcast: "KEMPS_ANNOUNCE|CODE|PORT"
        var ep = new IPEndPoint(IPAddress.Broadcast, discoveryPort);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                string msg = $"{AnnouncePrefix}{_codeCached}|{gamePort}";
                byte[] data = Encoding.UTF8.GetBytes(msg);
                await _udp.SendAsync(data, data.Length, ep);
            }
            catch { /* ignore */ }

            try { await Task.Delay(TimeSpan.FromSeconds(announceInterval), ct); }
            catch { /* ignore */ }
        }
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        // query: "KEMPS_QUERY|CODE"
        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult r;
            try
            {
                r = await _udp.ReceiveAsync();
            }
            catch
            {
                if (!ct.IsCancellationRequested) { }
                continue;
            }

            try
            {
                string txt = Encoding.UTF8.GetString(r.Buffer);
                if (!txt.StartsWith(QueryPrefix)) continue;

                string requestedCode = LanLobbyCodeUtil.Normalize(txt.Substring(QueryPrefix.Length));
                if (requestedCode != _codeCached) continue;

                // reply: "KEMPS_REPLY|CODE|IP|PORT"
                string myIp = LanIpUtil.GetLocalIPv4();
                string reply = $"{ReplyPrefix}{_codeCached}|{myIp}|{gamePort}";

                byte[] data = Encoding.UTF8.GetBytes(reply);
                await _udp.SendAsync(data, data.Length, r.RemoteEndPoint);
            }
            catch { /* ignore */ }
        }
    }
}
