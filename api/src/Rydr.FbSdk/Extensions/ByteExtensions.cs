using System.Security.Cryptography;
using System.Text;
using ServiceStack;

namespace Rydr.FbSdk.Extensions;

public static class ByteExtensions
{
    public static byte[] ToSha256(this byte[] bytes)
    {
        if (bytes == null || !bytes.Any())
        {
            return null;
        }

        var ha = SHA256.Create();

        if (ha == null)
        {
            return null;
        }

        var hashValue = ha.ComputeHash(bytes);
        ha.Clear();

        return hashValue;
    }

    public static string ToSha256Base64(this byte[] bytes)
    {
        var hash = bytes.ToSha256();

        if (hash == null || !hash.Any())
        {
            return null;
        }

        return ToBase64(hash);
    }

    public static long ToLongHashCode(this string source)
        => source.IsNullOrEmpty()
               ? 0
               : source.Length > 100 || source.Length < 8
                   ? ToLongHashCode(ToSha256(Encoding.UTF8.GetBytes(source)))
                   : ToLongHashCode(Encoding.UTF8.GetBytes(source));

    private static long ToLongHashCode(this byte[] bytes, int startIndex = 0)
        => BitConverter.ToInt64(bytes, startIndex);

    public static string ToBase64(this byte[] bytes) => Convert.ToBase64String(bytes);

    public static byte[] FromBase64(this string base64String) => Convert.FromBase64String(base64String);

    public static string ToStringEncoded(this byte[] bytes, Encoding encoding = null) => bytes == null
                                                                                             ? null
                                                                                             : (encoding ?? Encoding.UTF8).GetString(bytes);

    internal static string GenerateFacebookSecretProof(string fbAccessToken, string fbAppSecret)
    {
        var appSecretBytes = Encoding.UTF8.GetBytes(fbAppSecret);
        var accessTokenBytes = Encoding.UTF8.GetBytes(fbAccessToken);

        byte[] hashBytes;

        using(var hmacsha256 = new HMACSHA256(appSecretBytes))
        {
            hashBytes = hmacsha256.ComputeHash(accessTokenBytes);
        }

        var sb = new StringBuilder();

        foreach (var hashByte in hashBytes)
        {
            sb.Append(hashByte.ToString("x2"));
        }

        return sb.ToString();
    }
}
