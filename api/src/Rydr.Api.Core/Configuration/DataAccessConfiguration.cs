using Funq;
using Rydr.Api.Core.DataAccess;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using ServiceStack;

namespace Rydr.Api.Core.Configuration
{
    public class DataAccessConfiguration : IAppHostConfigurer
    {
        public void Apply(ServiceStackHost appHost, Container container)
        {
            // Default marshaller
            container.RegisterAutoWiredAs<CachedStorageMarshaller, IStorageMarshaller>()
                     .ReusedWithin(ReuseScope.Hierarchy);

            // Named RDBMS services
            container.Register<IRdbmsDataService>(ConnectionStringAppNames.Rydr,
                                                  c => new GuardedRdbmsDataService(new OrmLiteDataService(c.Resolve<IRydrSqlConnectionFactory>(),
                                                                                                          c.Resolve<IStorageMarshaller>(),
                                                                                                          ConnectionStringAppNames.Rydr)))
                     .ReusedWithin(ReuseScope.Hierarchy);

            // Default RDBMS service
            container.Register(c => c.ResolveNamed<IRdbmsDataService>(ConnectionStringAppNames.Rydr))
                     .ReusedWithin(ReuseScope.Hierarchy);

            // Just a shortcut registration for main Rydr data service (just an IRdbmsDataService wrapped with another interface for easier IoC)
            container.Register(c => (IRydrDataService)c.ResolveNamed<IRdbmsDataService>(ConnectionStringAppNames.Rydr))
                     .ReusedWithin(ReuseScope.Hierarchy);
        }
    }
}
