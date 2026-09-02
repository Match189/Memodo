using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Memodo.Windows.Services;

/// <summary>
/// 同步载荷端到端加密（E2EE）：双端共用同一口令（passphrase），云端只见密文。
/// 格式 V1：base64("MEMODO1" + salt[16] + nonce[12] + AES-256-GCM(ciphertext))
/// 密钥派生 PBKDF2-HMAC-SHA256（210k 迭代，OWASP 2023 推荐），口令不进同步协议、不落云端。
/// 口令为空 = 不加密（明文快照，向后兼容旧数据）。
/// Android 端同构实现见 memodo-android SyncCrypto.kt（javax.crypto）。
/// </summary>
public static class SyncCrypto
{
    private const string Magic = "MEMODO1";
    private const int SaltLen = 16, NonceLen = 12, KeyLen = 32, Iterations = 210_000;

    /// <summary>加密明文 JSON；口令为空返回原文（明文模式）。</summary>
    public static string Encrypt(string plainJson, string passphrase)
    {
        if (string.IsNullOrEmpty(passphrase)) return plainJson;
        var salt = RandomNumberGenerator.GetBytes(SaltLen);
        var nonce = RandomNumberGenerator.GetBytes(NonceLen);
        var key = Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, Iterations, HashAlgorithmName.SHA256, KeyLen);
        var plain = Encoding.UTF8.GetBytes(plainJson);
        var cipher = new byte[plain.Length + 16]; // + GCM tag
        using var aes = new AesGcm(key, 16);
        // 密文与 tag 必须分别切片传入：整段缓冲区当 ciphertext 会因长度≠明文长度抛异常
        aes.Encrypt(nonce, plain, cipher.AsSpan(0, plain.Length), cipher.AsSpan(plain.Length));
        using var ms = new MemoryStream();
        ms.Write(Encoding.ASCII.GetBytes(Magic));
        ms.Write(salt);
        ms.Write(nonce);
        ms.Write(cipher);
        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>解密；非本格式（明文快照/旧数据）原样返回。口令错误或损坏返回 null。</summary>
    public static string? TryDecrypt(string payload, string passphrase)
    {
        if (string.IsNullOrEmpty(passphrase)) return payload;
        byte[] data;
        try { data = Convert.FromBase64String(payload.Trim()); } catch { return payload; } // 不是 base64 → 明文快照
        var magic = Encoding.ASCII.GetBytes(Magic);
        if (data.Length < magic.Length + SaltLen + NonceLen + 16
            || !data.AsSpan(0, magic.Length).SequenceEqual(magic))
            return payload; // 无魔数 → 明文快照（口令刚启用时远端仍是旧明文）
        try
        {
            var salt = data.AsSpan(magic.Length, SaltLen).ToArray();
            var nonce = data.AsSpan(magic.Length + SaltLen, NonceLen).ToArray();
            var cipher = data.AsSpan(magic.Length + SaltLen + NonceLen, data.Length - magic.Length - SaltLen - NonceLen - 16).ToArray();
            var tag = data.AsSpan(data.Length - 16, 16).ToArray();
            var key = Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, Iterations, HashAlgorithmName.SHA256, KeyLen);
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (CryptographicException)
        {
            return null; // 口令错误（GCM tag 校验失败）
        }
    }

    /// <summary>载荷是否为本加密格式（用于诊断提示）。注意 payload 是 base64 文本，
    /// 必须解码后比对魔数字节——直接对文本做 StartsWith("MEMODO1") 永远为 false
    /// （base64("MEMODO1…") 开头是 "TUVNT0RP"）。</summary>
    public static bool IsEncrypted(string payload)
    {
        try
        {
            var data = Convert.FromBase64String(payload.Trim());
            var magic = Encoding.ASCII.GetBytes(Magic);
            return data.Length >= magic.Length + SaltLen + NonceLen + 16
                && data.AsSpan(0, magic.Length).SequenceEqual(magic);
        }
        catch { return false; }
    }
}
