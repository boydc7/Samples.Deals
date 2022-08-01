using System;
using System.IO;
using Amazon.S3.Model;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Services.Internal;

namespace Rydr.Api.Core.Models.Supporting
{
    public class S3BucketPrefixKey
    {
        public S3BucketPrefixKey(string bucketName, S3Object s3Object) : this(Path.Combine(bucketName, s3Object.Key), false, s3Object.Size) { }

        public S3BucketPrefixKey(FileMetaData meta) : this(meta.FullName) { }

        public S3BucketPrefixKey(string fullPathAndFileName, bool terminateWithPathDelimiter = false, long size = 0)
        {
            Guard.Against(string.IsNullOrEmpty(fullPathAndFileName), "fullPathAndFileName must not be empty.");

            Key = string.Empty;
            FileName = string.Empty;
            Prefix = string.Empty;
            Size = size;

            fullPathAndFileName = fullPathAndFileName.Replace("\\", "/");

            if (terminateWithPathDelimiter && !fullPathAndFileName.EndsWith("/"))
            {
                fullPathAndFileName = string.Concat(fullPathAndFileName, "/");
            }

            var split = fullPathAndFileName.Split(new[]
                                                  {
                                                      '/'
                                                  }, StringSplitOptions.RemoveEmptyEntries);

            BucketName = split[0];

            if (split.Length > 1)
            {
                Key = string.Join("/", split, 1, split.Length - 1);

                if (fullPathAndFileName.EndsWith("/"))
                {
                    Key = Key + "/";
                    Prefix = Key;
                }
                else
                {
                    FileName = split[split.GetUpperBound(0)];

                    if (split.Length > 2)
                    {
                        Prefix = string.Join("/", split, 1, split.Length - 2) + "/";
                    }
                }
            }
            else
            {
                IsBucketObject = true;
            }
        }

        public string BucketName { get; }
        public string Prefix { get; }
        public string Key { get; }
        public string FileName { get; }
        public long Size { get; }

        public bool IsBucketObject { get; }

        public bool HasPrefix => !string.IsNullOrEmpty(Prefix);

        public string FullName => string.Concat("/", ToString());

        public bool Equals(S3BucketPrefixKey other) => other != null && ToString().EqualsOrdinalCi(other.ToString());

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is S3BucketPrefixKey bpkObj && Equals(bpkObj);
        }

        public override string ToString() => string.Concat(BucketName, "/", Key);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(ToString());
    }
}
