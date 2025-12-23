using System;
using System.Security.Cryptography;
using System.Text;

public static class LanLobbyCodeUtil
{
    // 6 haneli, okunabilir: 0/O, 1/I gibi karýþanlarý çýkarýyoruz
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    public static string GenerateCode(int length = 6)
    {
        length = Math.Clamp(length, 4, 12);

        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
            sb.Append(Alphabet[RandomNumberGenerator.GetInt32(0, Alphabet.Length)]);
        return sb.ToString();
    }

    public static string Normalize(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        return code.Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");
    }

    public static bool IsValid(string code, int length = 6)
    {
        code = Normalize(code);
        if (code.Length != length) return false;

        for (int i = 0; i < code.Length; i++)
            if (!Alphabet.Contains(code[i]))
                return false;

        return true;
    }
}
