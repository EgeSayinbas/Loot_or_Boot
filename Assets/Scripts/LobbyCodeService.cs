using System;
using System.Collections.Generic;
using UnityEngine;

public class LobbyCodeService : MonoBehaviour
{
    public static LobbyCodeService Instance { get; private set; }

    [Header("Runtime")]
    [SerializeField] private string currentHostCode = "";

    // CODE -> (ip, port, lastSeen)
    private readonly Dictionary<string, (string ip, ushort port, float lastSeen)> _map
        = new Dictionary<string, (string ip, ushort port, float lastSeen)>();

    public string CurrentHostCode => currentHostCode;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetHostCode(string code)
    {
        currentHostCode = LanLobbyCodeUtil.Normalize(code);
    }

    public void EnsureHostCode(int length = 6)
    {
        if (LanLobbyCodeUtil.IsValid(currentHostCode, length)) return;
        currentHostCode = LanLobbyCodeUtil.GenerateCode(length);
    }

    public void PutMapping(string code, string ip, ushort port)
    {
        code = LanLobbyCodeUtil.Normalize(code);
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(ip)) return;

        _map[code] = (ip, port, Time.realtimeSinceStartup);
    }

    public bool TryGetMapping(string code, out string ip, out ushort port)
    {
        code = LanLobbyCodeUtil.Normalize(code);
        if (_map.TryGetValue(code, out var v))
        {
            ip = v.ip;
            port = v.port;
            return true;
        }

        ip = null;
        port = 0;
        return false;
    }

    public void CleanupOld(float maxAgeSeconds = 15f)
    {
        float now = Time.realtimeSinceStartup;
        var toRemove = new List<string>();

        foreach (var kv in _map)
        {
            if (now - kv.Value.lastSeen > maxAgeSeconds)
                toRemove.Add(kv.Key);
        }

        for (int i = 0; i < toRemove.Count; i++)
            _map.Remove(toRemove[i]);
    }

    public void ClearMappings()
    {
        _map.Clear();
    }
}
