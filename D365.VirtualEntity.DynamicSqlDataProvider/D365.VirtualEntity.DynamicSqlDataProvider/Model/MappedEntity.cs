using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365.VirtualEntity.DynamicSqlDataProvider.Model
{
    public class MappedEntity
    {
        public string EntityLogicalName { get; }
        public List<MappedEntityAttribute> Attributes = new List<MappedEntityAttribute>();

        public MappedEntity(string entityLogicalName)
        {
            EntityLogicalName = entityLogicalName;
        }

        public void AddAttribute(MappedEntityAttribute attribute)
        {
            // test to see if it already exists?
            Attributes.Add(attribute);
        }

        public Dictionary<string, object> ToDictionary()
        {
            var item = new Dictionary<string, object>();
            foreach (var attribute in Attributes)
            {
                item[attribute.AttributeLogicalName] = attribute.RawValue;
            }
            return item;
        }

        public Entity ToEntity()
        {
            var entity = new Entity(EntityLogicalName);
            foreach (var attribute in Attributes)
            {
                if (attribute.CrmValue != null)
                {
                    entity[attribute.AttributeLogicalName] = attribute.CrmValue;
                    if (!string.IsNullOrWhiteSpace(attribute.FormattedValue))
                    {
                        entity.FormattedValues[attribute.AttributeLogicalName] = attribute.FormattedValue;
                    }
                }
            }
            return entity;
        }
    }


}
