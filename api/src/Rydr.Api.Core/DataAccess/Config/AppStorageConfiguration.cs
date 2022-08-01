using System;
using System.Collections.Generic;
using Rydr.Api.Core.Interfaces.DataAccess;

namespace Rydr.Api.Core.DataAccess.Config
{
    public class AppStorageConfiguration
    {
        public BaseDataAccessConfiguration Configuration { get; set; }
        public string ConnectionStringAppName { get; set; }
        public Func<string, List<Type>, bool, IDbCreateConfiguration> DbCreateConfigurationFactory { get; set; }

        public bool IsOrmLiteIntegrated => Configuration.GetDialectProvider() != null;
    }
}
