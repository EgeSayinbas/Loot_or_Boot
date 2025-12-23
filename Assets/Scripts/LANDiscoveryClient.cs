using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class LanDiscoveryClient : MonoBehaviour
{
    [Header("Discovery Settings")]
    [SerializeField] private int discoveryPort = 47777;

    private CancellationTokenSource _cts;
    private UdpClient _udp;

    private const string AnnouncePrefix = "KEMPS_ANNOUNCE|";
    private const string QueryPrefix = "KEMPS_QUERY|";
    private const string ReplyPrefix = "KEMPS_REPLY|";

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        StopClientDiscovery();
    }

    public void StartClientDiscovery()
    {
        if (_udp != null) return;

        _cts = new CancellationTokenSource();

        _udp = new UdpClient(AddressFamily.InterNetwork);
        _udp.EnableBroadcast = true;
        _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udp.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));

        _ = Task.Run(() => ReceiveLoop(_cts.Token));

        Debug.Log($"[LanDiscoveryClient] Started discovery listen on UDP {discoveryPort}");
    }

    public void StopClientDiscovery()
    {
        try { _cts?.Cancel(); } catch { }
        _cts = null;

        try { _udp?.Close(); } catch { }
        _udp = null;
    }

    /// <summary>
    /// CODE için broadcast query atar. (Host varsa unicast reply döner)
    /// </summary>
    public void QueryCode(string code)
    {
        if (_udp == null) StartClientDiscovery();

        code = LanLobbyCodeUtil.Normalize(code);
        string msg = $"{QueryPrefix}{code}";
        byte[] data = Encoding.UTF8.GetBytes(msg);

        try
        {
            _udp.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, discoveryPort));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LanDiscoveryClient] Query send failed: {e.Message}");
        }
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
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

                // announce: "KEMPS_ANNOUNCE|CODE|PORT"
                if (txt.StartsWith(AnnouncePrefix))
                {
                    string body = txt.Substring(AnnouncePrefix.Length);
                    var parts = body.Split('|');
                    if (parts.Length >= 2)
                    {
                        string code = LanLobbyCodeUtil.Normalize(parts[0]);
                        if (ushort.TryParse(parts[1], out ushort port))
                        {
                            // announcer'ýn IP'si = UDP remote endpoint address
                            string ip = r.RemoteEndPoint.Address.ToString();
                            if (LobbyCodeService.Instance != null)
                                LobbyCodeService.Instance.PutMapping(code, ip, port);
                        }
                    }
                    continue;
                }

                // reply: "KEMPS_REPLY|CODE|IP|PORT"
                if (txt.StartsWith(ReplyPrefix))
                {
                    string body = txt.Substring(ReplyPrefix.Length);
                    var parts = body.Split('|');
                    if (parts.Length >= 3)
                    {
                        string code = LanLobbyCodeUtil.Normalize(parts[0]);
                        string ip = parts[1];

                        if (ushort.TryParse(parts[2], out ushort port))
                        {
                            if (LobbyCodeService.Instance != null)
                                LobbyCodeService.Instance.PutMapping(code, ip, port);
                        }
                    }
                    continue;
                }
            }
            catch { /* ignore */ }
        }
    }
}
