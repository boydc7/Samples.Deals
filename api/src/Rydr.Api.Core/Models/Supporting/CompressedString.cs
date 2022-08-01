using System.Text;
using Rydr.Api.Core.Extensions;
using ServiceStack;

namespace Rydr.Api.Core.Models.Supporting
{
    public class CompressedString
    {
        private const int _minDataLengthToCompress = 1024 * 2; // 2Kb

        private string _decompressedValue;

        // Needed for serialization only, should not be used
        public CompressedString() { }

        public CompressedString(string source)
        {
            IsCompressed = source is { Length: >= _minDataLengthToCompress };

            Value = IsCompressed
                        ? source.CompressGzip64(Encoding.UTF8)
                        : source;
        }

        public string Value { get; set; }
        public bool IsCompressed { get; set; }

        public static implicit operator string(CompressedString cs)
            => cs?.ToString();

        public static implicit operator CompressedString(string ss)
            => new CompressedString(ss);

        public override string ToString()
        {
            if (!IsCompressed || Value.IsNullOrEmpty())
            {
                return Value.ToNullIfEmpty();
            }

            return _decompressedValue ??= Value.DecompressGzip64(Encoding.UTF8);
        }
    }
}
