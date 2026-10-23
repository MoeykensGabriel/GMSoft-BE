using System.Security.Cryptography;
using System.Text;
using GMSoft.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace GMSoft.Data.Services;

/// <summary>
/// AES-GCM con una clave que vive en las variables de entorno, no en la base: con solo
/// una copia de la base no se leen las contraseñas. Usa PASSWORD_VIEW_KEY si existe; si
/// no, una clave derivada de la del JWT. Si esa clave cambia, las contraseñas guardadas
/// dejan de poder leerse y se ven vacías hasta volver a ponerlas.
/// </summary>
public class PasswordVault(IConfiguration configuration) : IPasswordVault
{
    private byte[] Key()
    {
        var secreto = Environment.GetEnvironmentVariable("PASSWORD_VIEW_KEY")
                   ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
                   ?? configuration["JwtSettings:SecretKey"]
                   ?? throw new InvalidOperationException("Falta la clave para guardar las contraseñas visibles.");
        return SHA256.HashData(Encoding.UTF8.GetBytes("gmsoft-clave-visible|" + secreto));
    }

    public string Protect(string password)
    {
        var plano = Encoding.UTF8.GetBytes(password);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var cifrado = new byte[plano.Length];
        using (var aes = new AesGcm(Key(), 16)) aes.Encrypt(nonce, plano, cifrado, tag);
        return Convert.ToBase64String([.. nonce, .. tag, .. cifrado]);
    }

    public string? Reveal(string? cipher)
    {
        if (string.IsNullOrEmpty(cipher)) return null;
        try
        {
            var datos = Convert.FromBase64String(cipher);
            var plano = new byte[datos.Length - 28];
            using var aes = new AesGcm(Key(), 16);
            aes.Decrypt(datos.AsSpan(0, 12), datos.AsSpan(28), datos.AsSpan(12, 16), plano);
            return Encoding.UTF8.GetString(plano);
        }
        catch (Exception e) when (e is FormatException or CryptographicException or ArgumentException)
        {
            return null;
        }
    }
}
