using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365.VirtualEntity.DynamicSqlDataProvider.Model
{
    public class MappedEntityAttribute
    {
        public string AttributeLogicalName { get; set; }
        public string ExternalName { get; set; }
        public object RawValue { get; set; }
        public object CrmValue { get; set; }
        public bool IsAliased { get; set; } = false;
        public string FormattedValue { get; set; } = string.Empty;

        public MappedEntityAttribute(string attributeLogicalName, string externalName, object rawValue, object crmValue, bool isAliased = false, string formattedValue = "")
        {
            AttributeLogicalName = attributeLogicalName;
            ExternalName = externalName;
            RawValue = rawValue;
            CrmValue = crmValue;
            IsAliased = isAliased;
            FormattedValue = formattedValue;
        }

    }
}
