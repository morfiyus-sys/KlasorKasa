using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace KlasorKasa.Services;

public sealed class EncryptionService
{
    private static readonly byte[] FileMagic = Encoding.ASCII.GetBytes("KKFILE01");
    private static readonly byte[] DataMagic = Encoding.ASCII.GetBytes("KKDATA01");
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int ChunkSize = 1024 * 1024;

    public byte[] GenerateMasterKey() => RandomNumberGenerator.GetBytes(32);
    public byte[] GenerateNonce() => RandomNumberGenerator.GetBytes(NonceSize);

    public byte[] EncryptBytes(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> key, ReadOnlySpan<byte> associatedData = default)
    {
        ValidateKey(key);
        var nonce = GenerateNonce();
        var cipher = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, cipher, tag, associatedData);
        var result = new byte[DataMagic.Length + NonceSize + TagSize + cipher.Length];
        DataMagic.CopyTo(result, 0);
        nonce.CopyTo(result, DataMagic.Length);
        tag.CopyTo(result, DataMagic.Length + NonceSize);
        cipher.CopyTo(result, DataMagic.Length + NonceSize + TagSize);
        return result;
    }

    public byte[] DecryptBytes(ReadOnlySpan<byte> envelope, ReadOnlySpan<byte> key, ReadOnlySpan<byte> associatedData = default)
    {
        ValidateKey(key);
        if (envelope.Length < DataMagic.Length + NonceSize + TagSize || !envelope[..DataMagic.Length].SequenceEqual(DataMagic))
            throw new CryptographicException("Geçersiz şifreli veri biçimi.");
        var nonce = envelope.Slice(DataMagic.Length, NonceSize);
        var tag = envelope.Slice(DataMagic.Length + NonceSize, TagSize);
        var cipher = envelope[(DataMagic.Length + NonceSize + TagSize)..];
        var plaintext = new byte[cipher.Length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plaintext, associatedData);
        return plaintext;
    }

    public async Task<string> EncryptFileAsync(string inputPath, string outputPath, byte[] key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var temp = outputPath + ".tmp-" + Guid.NewGuid().ToString("N");
        var noncePrefix = RandomNumberGenerator.GetBytes(8);
        var length = new FileInfo(inputPath).Length;
        var header = BuildHeader(length, noncePrefix);
        try
        {
            string digest;
            {
                using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkSize, true);
                using var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, ChunkSize, true);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                await output.WriteAsync(header, cancellationToken);
                var buffer = new byte[ChunkSize];
                var index = 0;
                int read;
                using var aes = new AesGcm(key, TagSize);
                while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    var cipher = new byte[read];
                    var tag = new byte[TagSize];
                    var nonce = BuildChunkNonce(noncePrefix, index);
                    var aad = BuildChunkAad(header, index, read);
                    aes.Encrypt(nonce, buffer.AsSpan(0, read), cipher, tag, aad);
                    await output.WriteAsync(BitConverter.GetBytes(read), cancellationToken);
                    await output.WriteAsync(cipher, cancellationToken);
                    await output.WriteAsync(tag, cancellationToken);
                    CryptographicOperations.ZeroMemory(cipher);
                    index++;
                }
                CryptographicOperations.ZeroMemory(buffer);
                await output.FlushAsync(cancellationToken);
                output.Flush(true);
                digest = Convert.ToHexString(hash.GetHashAndReset());
            }
            File.Move(temp, outputPath, true);
            return digest;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public async Task<string> DecryptFileAsync(string inputPath, string outputPath, byte[] key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        try
        {
            using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkSize, true);
            var header = new byte[28];
            await ReadExactlyAsync(input, header, cancellationToken);
            ValidateHeader(header, out var totalLength, out var noncePrefix);
            using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, ChunkSize, true);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using var aes = new AesGcm(key, TagSize);
            long written = 0;
            var index = 0;
            while (written < totalLength)
            {
                var lengthBytes = new byte[4];
                await ReadExactlyAsync(input, lengthBytes, cancellationToken);
                var length = BitConverter.ToInt32(lengthBytes);
                if (length <= 0 || length > ChunkSize || written + length > totalLength)
                    throw new CryptographicException("Geçersiz şifreli dosya bölümü.");
                var cipher = new byte[length];
                var tag = new byte[TagSize];
                await ReadExactlyAsync(input, cipher, cancellationToken);
                await ReadExactlyAsync(input, tag, cancellationToken);
                var plain = new byte[length];
                aes.Decrypt(BuildChunkNonce(noncePrefix, index), cipher, tag, plain, BuildChunkAad(header, index, length));
                hash.AppendData(plain);
                await output.WriteAsync(plain, cancellationToken);
                CryptographicOperations.ZeroMemory(plain);
                written += length;
                index++;
            }
            if (input.Position != input.Length) throw new CryptographicException("Şifreli dosyada beklenmeyen veri bulundu.");
            await output.FlushAsync(cancellationToken);
            output.Flush(true);
            return Convert.ToHexString(hash.GetHashAndReset());
        }
        catch
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
            throw;
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public async Task<bool> VerifyFileAsync(string encryptedPath, byte[] key, string expectedSha256, CancellationToken cancellationToken = default)
    {
        var temp = Path.Combine(Path.GetTempPath(), "KlasorKasa-verify-" + Guid.NewGuid().ToString("N"));
        try
        {
            var actual = await DecryptFileAsync(encryptedPath, temp, key, cancellationToken);
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual), Convert.FromHexString(expectedSha256));
        }
        catch (CryptographicException) { return false; }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static byte[] BuildHeader(long length, byte[] noncePrefix)
    {
        var header = new byte[28];
        FileMagic.CopyTo(header, 0);
        BitConverter.GetBytes(ChunkSize).CopyTo(header, 8);
        BitConverter.GetBytes(length).CopyTo(header, 12);
        noncePrefix.CopyTo(header, 20);
        return header;
    }

    private static void ValidateHeader(byte[] header, out long length, out byte[] noncePrefix)
    {
        if (!header.AsSpan(0, 8).SequenceEqual(FileMagic) || BitConverter.ToInt32(header, 8) != ChunkSize)
            throw new CryptographicException("Geçersiz KlasörKasa dosyası.");
        length = BitConverter.ToInt64(header, 12);
        if (length < 0) throw new CryptographicException("Geçersiz dosya uzunluğu.");
        noncePrefix = header[20..28];
    }

    private static byte[] BuildChunkNonce(byte[] prefix, int index)
    {
        var nonce = new byte[NonceSize];
        prefix.CopyTo(nonce, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(nonce.AsSpan(8), index);
        return nonce;
    }

    private static byte[] BuildChunkAad(byte[] header, int index, int length)
    {
        var aad = new byte[header.Length + 8];
        header.CopyTo(aad, 0);
        BitConverter.GetBytes(index).CopyTo(aad, header.Length);
        BitConverter.GetBytes(length).CopyTo(aad, header.Length + 4);
        return aad;
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken token)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), token);
            if (read == 0) throw new EndOfStreamException("Şifreli dosya tamamlanmamış.");
            offset += read;
        }
    }

    private static void ValidateKey(ReadOnlySpan<byte> key)
    {
        if (key.Length != 32) throw new CryptographicException("AES-256 anahtarı geçersiz.");
    }
}
