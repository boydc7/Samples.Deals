using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Rekognition.Model;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Caching;

namespace Rydr.Api.Core.Services.Filters
{
    public class TimestampedLabelTaxonomyProcessingFilter : TimestampCachedServiceBase<DynMediaLabel>, ILabelTaxonomyProcessingFilter
    {
        private readonly IPocoDynamo _dynamoDb;

        public TimestampedLabelTaxonomyProcessingFilter(ICacheClient cacheClient, IPocoDynamo dynamoDb)
            : base(cacheClient, 300)
        {
            _dynamoDb = dynamoDb;
        }

        public Task<DynMediaLabel> TryGetMediaLabelAsync(string name, string parentName)
        {
            var fullyQualifiedName = GetLabelFullyQualifiedName(name, parentName);
            var (labelIdHash, labelEdgeId) = GetLabelIdEdge(fullyQualifiedName);

            return GetModelAsync(fullyQualifiedName,
                                 () => _dynamoDb.GetItemAsync<DynMediaLabel>(labelIdHash, labelEdgeId));
        }

        public async Task<Label> LookupLabelAsync(Label labelEntity)
        {
            var parentName = labelEntity.Parents?.FirstOrDefault()?.Name;

            var labelModel = await TryGetMediaLabelAsync(labelEntity.Name, parentName);

            if (labelModel != null && labelModel.Ignore)
            {
                return null;
            }

            string rewriteName = null;
            string rewriteParent = null;

            rewriteName = (labelModel?.RewriteName).HasValue()
                              ? labelModel.RewriteName
                              : labelEntity.Name;

            rewriteParent = (labelModel?.RewriteParent).HasValue()
                                ? labelModel.RewriteParent.Trim().ToNullIfEmpty() // Whitespace that is not empty results in no parent...
                                : parentName;

            // Applies to the use of the given name anywhere, i.e. name or parent...
            var nameWildcard = await TryGetMediaLabelAsync(rewriteName, "*");

            var parentWildcard = rewriteParent.IsNullOrEmpty() || rewriteParent.EqualsOrdinalCi(rewriteName)
                                     ? null
                                     : await TryGetMediaLabelAsync(rewriteParent, "*");

            if (parentWildcard != null && parentWildcard.RewriteName.HasValue())
            { // Any uses of the name anywhere are replaced
                var wildcardName = parentWildcard.Name;

                if (rewriteName.EqualsOrdinalCi(wildcardName))
                {
                    rewriteName = parentWildcard.RewriteName;
                }

                if (rewriteParent.HasValue() && rewriteParent.EqualsOrdinalCi(wildcardName))
                {
                    rewriteParent = parentWildcard.RewriteName;
                }
            }

            if (nameWildcard != null && nameWildcard.RewriteName.HasValue())
            { // Any uses of the name anywhere are replaced
                var wildcardName = nameWildcard.Name;

                if (rewriteName.EqualsOrdinalCi(wildcardName))
                {
                    rewriteName = nameWildcard.RewriteName;
                }

                if (rewriteParent.HasValue() && rewriteParent.EqualsOrdinalCi(wildcardName))
                {
                    rewriteParent = nameWildcard.RewriteName;
                }
            }

            // Name is always set (might be itself)...
            labelEntity.Name = rewriteName;

            // Parent gets set if it is a value and does not match the name
            if (rewriteParent.HasValue() && !rewriteParent.EqualsOrdinalCi(labelEntity.Name))
            {
                if (labelEntity.Parents == null)
                {
                    labelEntity.Parents = new List<Parent>();
                }
                else if (labelEntity.Parents.Count > 0)
                {
                    labelEntity.Parents.Clear();
                }

                labelEntity.Parents.Add(new Parent
                                        {
                                            Name = rewriteParent
                                        });
            }
            else
            {
                labelEntity.Parents = null;
            }

            return labelEntity;
        }

        public async Task<bool> ProcessIgnoreAsync(Label label)
        {
            var labelParentName = label.Parents?.FirstOrDefault()?.Name;
            var fullyQualifiedName = GetLabelFullyQualifiedName(label.Name, labelParentName);

            var labelModel = await TryGetMediaLabelAsync(label.Name, labelParentName);

            var ignore = false;

            if (labelModel == null)
            {
                labelModel = CreateNewLabel(label.Name, labelParentName);

                await _dynamoDb.PutItemAsync(labelModel);

                SetModel(fullyQualifiedName, labelModel);
            }
            else if (labelModel.Ignore)
            {
                ignore = true;
            }
            else
            {
                // If the update succeeds, not ignored...
                var (labelIdHash, labelEdgeId) = GetLabelIdEdge(fullyQualifiedName);

                var updated = await _dynamoDb.UpdateItemAsync(_dynamoDb.UpdateExpression<DynMediaLabel>(labelIdHash, labelEdgeId)
                                                                       .Add(() => new DynMediaLabel
                                                                                  {
                                                                                      ProcessCount = 1
                                                                                  })
                                                                       .Condition(l => l.Ignore == false));

                ignore = !updated;

                FlushModel(fullyQualifiedName);
            }

            return ignore;
        }

        public async Task<DynMediaLabel> UpdateLabelAsync(string name, string parentName, bool? ignore, string rewriteName, string rewriteParent)
        {
            var fullyQualifiedName = GetLabelFullyQualifiedName(name, parentName);

            var label = await TryGetMediaLabelAsync(name, parentName) ?? CreateNewLabel(name, parentName);

            label.Ignore = ignore ?? label.Ignore;

            if (rewriteName != null)
            {
                label.RewriteName = rewriteName.HasValue()
                                        ? rewriteName
                                        : null;
            }

            if (rewriteParent != null)
            {
                label.RewriteParent = rewriteParent.HasValue()
                                          ? rewriteParent
                                          : null;
            }

            await _dynamoDb.PutItemAsync(label);

            SetModel(fullyQualifiedName, label);

            return label;
        }

        private string GetLabelFullyQualifiedName(string name, string parentName)
            => string.Concat(parentName ?? "",
                             parentName.HasValue()
                                 ? "|"
                                 : "",
                             name);

        private (long Id, string EdgeId) GetLabelIdEdge(string name, string parentName)
        {
            var fullyQualifiedName = GetLabelFullyQualifiedName(name, parentName);

            var (labelIdHash, labelEdgeId) = GetLabelIdEdge(fullyQualifiedName);

            return (labelIdHash, labelEdgeId);
        }

        private (long Id, string EdgeId) GetLabelIdEdge(string fullyQualifiedName)
        {
            var labelIdHash = fullyQualifiedName.ToLongHashCode();
            var labelEdgeId = DynMediaLabel.BuildEdgeId(fullyQualifiedName);

            return (labelIdHash, labelEdgeId);
        }

        private DynMediaLabel CreateNewLabel(string name, string parentName)
        {
            var (labelIdHash, labelEdgeId) = GetLabelIdEdge(name, parentName);

            return new DynMediaLabel
                   {
                       Id = labelIdHash,
                       EdgeId = labelEdgeId,
                       DynItemType = DynItemType.MediaLabel,
                       WorkspaceId = UserAuthInfo.PublicWorkspaceId,
                       OwnerId = UserAuthInfo.PublicOwnerId,
                       CreatedBy = UserAuthInfo.AdminUserId,
                       ModifiedBy = UserAuthInfo.AdminUserId,
                       ModifiedOnUtc = DateTimeHelper.UtcNowTs,
                       ProcessCount = 1
                   };
        }
    }
}
