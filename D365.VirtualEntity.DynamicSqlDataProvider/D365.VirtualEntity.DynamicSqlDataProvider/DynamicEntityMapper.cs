using D365.VirtualEntity.DynamicSqlDataProvider.Model;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;

namespace D365.VirtualEntity.DynamicSqlDataProvider
{
    public class DynamicEntityMapper
    {
        public EntityMetadata EntityMetadata;
        private List<AttributeMetadata> ExternalAttributes;
        private List<OneToManyRelationshipMetadata> OneToManyRelationships;
        private ITracingService Tracer;

        public DynamicEntityMapper(EntityMetadata entityMetadata, ITracingService tracer)
        {
            EntityMetadata = entityMetadata;
            ExternalAttributes = EntityMetadata.Attributes.Where(attr => !string.IsNullOrWhiteSpace(attr.ExternalName)).ToList();
            OneToManyRelationships = EntityMetadata.ManyToOneRelationships.ToList();
            
            Tracer = tracer;
        }

        public MappedEntity MapItem(Dictionary<string,object> item)
        {
            var entity = new MappedEntity(EntityMetadata.LogicalName);

            ExternalAttributes.ForEach(attribute =>
            {
                if (item.ContainsKey(attribute.ExternalName))
                {
                    var propValue = (object)item[attribute.ExternalName];
                    MappedEntityAttribute attr = MapEntityAttribute(attribute, propValue);
                    entity.AddAttribute(attr);
                }
            });
            return entity;
        }

        public MappedEntity MapItem(Entity entity)
        {
            var mappedItem = new MappedEntity(EntityMetadata.LogicalName);

            ExternalAttributes.ForEach(attributeMeta =>
            {
                var key = attributeMeta.LogicalName;
                if (entity.Attributes.ContainsKey(key))
                {
                    var crmVal = entity.Attributes[key];
                    object rawVal = null;

                    if (crmVal != null)
                    {
                        var type = crmVal.GetType();

                        if (type == typeof(EntityReference))
                        {
                            rawVal = ((EntityReference)crmVal).Id;
                        }
                        else if (type == typeof(OptionSetValue))
                        {
                            rawVal = ((OptionSetValue)crmVal).Value;
                        }
                        else
                        {
                            rawVal = crmVal;
                        }
                    }

                    if (entity.FormattedValues.ContainsKey(key))
                    {
                        mappedItem.AddAttribute(new MappedEntityAttribute(key,attributeMeta.ExternalName, rawVal, crmVal, false, entity.FormattedValues[key]));
                    }
                    else
                    {
                        mappedItem.AddAttribute(new MappedEntityAttribute(key, attributeMeta.ExternalName, rawVal, crmVal, false));
                    }
                }
            });

            return mappedItem;

        }

        private MappedEntityAttribute MapEntityAttribute(AttributeMetadata attribute, object propValue)
        {
            // Ensure consistent null
            var rawValue = propValue==null || propValue.GetType() == typeof(System.DBNull) ? null : propValue;
            object crmValue = null;

            if (propValue != null && propValue.GetType() != typeof(System.DBNull))
            {

                switch (attribute.AttributeType)
                {
                    // this will be the primary key
                    case AttributeTypeCode.Uniqueidentifier:
                        crmValue = new Guid(propValue.ToString());
                        break;
                    case AttributeTypeCode.String:
                    case AttributeTypeCode.Memo:
                        crmValue = (string)propValue;
                        break;
                    case AttributeTypeCode.BigInt:
                    case AttributeTypeCode.Integer:
                        crmValue = (int)propValue;
                        break;
                    case AttributeTypeCode.Decimal:
                        crmValue = (Decimal)propValue;
                        break;
                    case AttributeTypeCode.Double:
                        crmValue = (Double)propValue;
                        break;
                    case AttributeTypeCode.Boolean:
                        crmValue = (Boolean)propValue;
                        break;
                    case AttributeTypeCode.DateTime:
                        crmValue = (DateTime)propValue;
                        break;
                    case AttributeTypeCode.Picklist:
                        crmValue = new OptionSetValue((int)propValue);
                        break;
                    case AttributeTypeCode.Money:
                        Money moneyVal = new Money((decimal)propValue);
                        crmValue = moneyVal;
                        break;
                    case AttributeTypeCode.Lookup:
                        var rel = OneToManyRelationships.Where(x => x.ReferencingAttribute == attribute.LogicalName).FirstOrDefault();
                        if (rel != null)
                        {
                            crmValue = new EntityReference(rel.ReferencedEntity, new Guid(propValue.ToString()));
                        }
                        break;
                    case AttributeTypeCode.Virtual:
                        crmValue=(string)propValue;
                        break;
                    //case AttributeTypeCode.Owner:
                    //    We can't have owner on virtual, so it shouldn't matter
                    //    break;
                    //case AttributeTypeCode.Customer:
                        // not sure how to manage these
                        //break;
                    default:
                        // it wont have owner, 
                        Tracer.Trace("Unsupported type mapping {0} for attribute {1}", attribute.AttributeType.ToString(), attribute.LogicalName);
                        break;
                };

            }
            return new MappedEntityAttribute(attribute.LogicalName, attribute.ExternalName, rawValue, crmValue);
        }
    }
}
