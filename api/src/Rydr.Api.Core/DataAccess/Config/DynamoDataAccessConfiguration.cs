using ServiceStack.OrmLite;

namespace Rydr.Api.Core.DataAccess.Config
{
    public class DynamoDataAccessConfiguration : BaseDataAccessConfiguration
    {
        public DynamoDataAccessConfiguration(string appName) : base(appName, DataConfigurationPrefix.Dynamo) { }

        public override IOrmLiteDialectProvider GetDialectProvider() => null;
    }
}
