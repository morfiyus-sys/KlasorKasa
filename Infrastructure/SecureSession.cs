using System.Security.Cryptography;

namespace KlasorKasa.Infrastructure;

public sealed class SecureSession
{
    private byte[]? _masterKey;
    public bool IsUnlocked => _masterKey is { Length: 32 };
    public void SetMasterKey(byte[] key)
    {
        Clear();
        _masterKey = key.ToArray();
    }
    public byte[] GetMasterKeyCopy() => _masterKey?.ToArray() ?? throw new InvalidOperationException("Oturum kilitli.");
    public void Clear()
    {
        if (_masterKey is not null) CryptographicOperations.ZeroMemory(_masterKey);
        _masterKey = null;
    }
}
