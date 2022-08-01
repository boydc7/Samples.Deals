using System.Text;

namespace Rydr.FbSdk.Extensions
{
    public static class StringExtensions
    {
        public static string ToShaBase64(this string toHash, Encoding encoding = null)
            => string.IsNullOrEmpty(toHash)
                   ? string.Empty
                   : (encoding ?? Encoding.UTF8).GetBytes(toHash).ToSha256Base64();

        public static string ToSafeSha64(this string toHash, Encoding encoding = null)
        {
            if (string.IsNullOrEmpty(toHash))
            {
                return string.Empty;
            }

            var base64 = ToShaBase64(toHash, encoding);

            var safeBase64 = base64.Replace('+', '-').Replace('/', '_').Replace('=', '~');

            return safeBase64;
        }

        public static string Left(this string source, int length)
            => string.IsNullOrEmpty(source)
                   ? string.Empty
                   : source.Length >= length
                       ? source.Substring(0, length)
                       : source;

        public static string Right(this string source, int length)
            => string.IsNullOrEmpty(source)
                   ? string.Empty
                   : source.Length >= length
                       ? source.Substring(source.Length - length)
                       : source;
    }
}
