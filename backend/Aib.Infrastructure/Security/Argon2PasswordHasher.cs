using System.Security.Cryptography;
using System.Text;
using Aib.Application.Abstractions;
using Konscious.Security.Cryptography;

namespace Aib.Infrastructure.Security;

/// <summary>
/// Argon2id password hashing. Encoded string format:
/// <c>argon2id$m=&lt;kib&gt;,t=&lt;iters&gt;,p=&lt;lanes&gt;$&lt;salt_b64&gt;$&lt;hash_b64&gt;</c>.
/// </summary>
public sealed class Argon2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MemoryKib = 19_456; // 19 MiB (OWASP baseline)
    private const int Iterations = 2;
    private const int DegreeOfParallelism = 1;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt, MemoryKib, Iterations, DegreeOfParallelism, HashSize);
        return $"argon2id$m={MemoryKib},t={Iterations},p={DegreeOfParallelism}$" +
               $"{Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string encoded)
    {
        try
        {
            var parts = encoded.Split('$');
            if (parts.Length != 4 || parts[0] != "argon2id")
                return false;

            var (m, t, p) = ParseParams(parts[1]);
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Derive(password, salt, m, t, p, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] Derive(string password, byte[] salt, int memoryKib, int iterations, int lanes, int size)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKib,
            Iterations = iterations,
            DegreeOfParallelism = lanes
        };
        return argon2.GetBytes(size);
    }

    private static (int m, int t, int p) ParseParams(string segment)
    {
        int m = MemoryKib, t = Iterations, p = DegreeOfParallelism;
        foreach (var kv in segment.Split(','))
        {
            var pair = kv.Split('=');
            if (pair.Length != 2) continue;
            var value = int.Parse(pair[1]);
            switch (pair[0])
            {
                case "m": m = value; break;
                case "t": t = value; break;
                case "p": p = value; break;
            }
        }
        return (m, t, p);
    }
}
