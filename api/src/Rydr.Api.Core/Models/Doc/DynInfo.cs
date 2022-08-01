using Rydr.Api.Core.Models.Supporting;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Core.Models.Doc
{
    public class DynInfo : DynItem
    {
        [ExcludeNullValue]
        public CompressedString Info { get; set; }
    }
}
