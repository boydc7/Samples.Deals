using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Rydr.ActiveCampaign
{
    public static class Extensions
    {
        public static string Left(this string source, int length)
            => string.IsNullOrEmpty(source)
                   ? string.Empty
                   : source.Length >= length
                       ? source.Substring(0, length)
                       : source;

        public static string ToShaBase64(this string toHash, Encoding encoding = null)
            => string.IsNullOrEmpty(toHash)
                   ? string.Empty
                   : (encoding ?? Encoding.UTF8).GetBytes(toHash).ToSha256Base64();

        public static string ToSha256Base64(this byte[] bytes)
        {
            var hash = bytes.ToSha256();

            if (hash == null || !hash.Any())
            {
                return null;
            }

            return ToBase64(hash);
        }

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

        public static string ToBase64(this byte[] bytes) => Convert.ToBase64String(bytes);
    }
}
