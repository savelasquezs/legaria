using System.Security.Cryptography;
using System.Text;
using Legaria.Application.Notifications;
using Microsoft.Extensions.Configuration;

namespace Legaria.Infrastructure.Notifications;

public sealed class IntegrationSecretProtector(IConfiguration configuration) : IIntegrationSecretProtector
{
    public string Protect(string value)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var plain = Encoding.UTF8.GetBytes(value);
        var cipher = new byte[plain.Length];
        using var aes = new AesGcm(GetKey(), 16);
        aes.Encrypt(nonce, plain, cipher, tag);
        return $"v1.{Convert.ToBase64String(nonce)}.{Convert.ToBase64String(tag)}.{Convert.ToBase64String(cipher)}";
    }

    public string Unprotect(string value)
    {
        var parts = value.Split('.');
        if (parts.Length != 4 || parts[0] != "v1") throw new CryptographicException("Formato de credencial cifrada inválido.");
        var nonce = Convert.FromBase64String(parts[1]);
        var tag = Convert.FromBase64String(parts[2]);
        var cipher = Convert.FromBase64String(parts[3]);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(GetKey(), 16);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    private byte[] GetKey()
    {
        var value = configuration["Integrations:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Configure Integrations__EncryptionKey.");
        return SHA256.HashData(Encoding.UTF8.GetBytes(value));
    }
}
