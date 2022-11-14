using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace D365.VirtualEntity.DynamicSqlDataProvider.Model
{
    public class ConnectionSettings
    {
        public string SqlConnectionString { get; set; }
        public bool IsUpdateEnabled { get; set; }
        public bool IsCreateEnabled { get; set; }
        public bool IsDeleteEnabled { get; set; }
        public bool CanRetrieveLinkData { get; set; } = true;
    }
}
