using System;
using System.Collections.Generic;
using System.Linq;
using Funq;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Services.Auth;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Caching;

namespace Rydr.Api.Core.Configuration
{
    public class AuthorizationConfiguration : IAppHostConfigurer
    {
        public void Apply(ServiceStackHost appHost, Container container)
        {
            container.Register<IAuthorizeService>(c => new DynAuthorizeService(c.Resolve<IPocoDynamo>(),
                                                                               c.Resolve<ICacheClient>()))
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.Register<IAuthorizationService>(c => new CompositeAuthorizationService(new IAuthorizer[]
                                                                                             {
                                                                                                 new TypeValidationAuthorizer(GetTypedValidations(c)),
                                                                                                 new RequestUserAuthInfoMatchAuthorizer(),
                                                                                                 new RequestStateAccessIntentMatchAuthorizer(c.Resolve<IRequestStateManager>()),
                                                                                                 new PublisherAccountAuthorizer(),
                                                                                                 new WorkspaceUserAuthorizer(c.Resolve<IWorkspaceService>()),
                                                                                                 new WorkspaceUserPublisherAccountAuthorizer(c.Resolve<IWorkspaceService>()),
                                                                                                 new DynAuthorizeService(c.Resolve<IPocoDynamo>(),
                                                                                                                         c.Resolve<ILocalDistributedCacheClient>())
                                                                                             },
                                                                                             c.Resolve<IRequestStateManager>()))
                     .ReusedWithin(ReuseScope.Hierarchy);
        }

        private Dictionary<Type, IAuthorizer> GetTypedValidations(Container container)
            => BuildTypeValidationServices(container).ToDictionary(t => t.Item1, t => t.Item2);

        private IEnumerable<(Type, IAuthorizer)> BuildTypeValidationServices(Container container)
        {
            var dynItemTypeValidators = container.TryResolveNamed<Dictionary<Type, IAuthorizer>>("DynItemTypeValidationAuthorizations");

            if (!dynItemTypeValidators.IsNullOrEmpty())
            {
                foreach (var dynItemValidator in dynItemTypeValidators)
                {
                    yield return (dynItemValidator.Key, dynItemValidator.Value);
                }
            }

            //            yield return (typeof(DynDialog), new DynItemTypeValidationAuthorizationService<DynDialog>(DynItemType.Dialog.AsEnumerable(), container.Resolve<IRequestStateManager>(),
            //                                                                                                      new Action<DynDialog, IHasUserAuthorizationInfo>[]
            //                                                                                                      {   // For dialogs, current contact must have created or be a member of the dialog
            //                                                                                                          (d, s) => Guard.AgainstUnauthorized(d.CreatedBy != s.ContactId &&
            //                                                                                                                                              !d.Members.Contains(new RecordTypeId(RecordType.Contact, s.ContactId)))
            //                                                                                                      }));
        }
    }
}
