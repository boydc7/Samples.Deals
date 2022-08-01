using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Enums;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc
{
    public class DynMediaLabel : DynItem
    {
        // Hash/Id: 64-bit hash of the label name (not the fully qualified name)
        // Range/Edge: Fully qualified image taxonomy label, concatenated by |'s, prefixed with typeId
        // RefId:
        // StatusId:
        // Owner:
        // Workspace:

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public string FullyQualifiedName
        {
            get => EdgeId.Substring(EdgeId.IndexOf('|') + 1);
            set => EdgeId = BuildEdgeId(value);
        }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public string Name => GetFinalEdgeSegment(EdgeId);

        public bool Ignore { get; set; }
        public long ProcessCount { get; set; }
        public string RewriteName { get; set; }
        public string RewriteParent { get; set; }

        public static string BuildEdgeId(string fullyQualifiedName) => string.Concat((int)DynItemType.MediaLabel, "|", fullyQualifiedName);
    }
}
