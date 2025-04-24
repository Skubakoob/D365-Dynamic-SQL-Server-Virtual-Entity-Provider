using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365.VirtualEntity.DynamicSqlDataProvider.Extensions
{
    public static class EntityExtensions
    {
        public static T FromEntity<T>(this T proxy, Entity original) where T : Entity
        {

            //foreach(var attr in entity.Attributes)
            //{
            //    proxy[attr.Key] = attr.Value;
            //}

            //return proxy;

            if (proxy.LogicalName != original.LogicalName) { throw new Exception("Please make sure that the entity logical name matches that of the proxy class you are creating."); }

            proxy.LogicalName = original.LogicalName;
            proxy.RelatedEntities.Clear();
            proxy.FormattedValues.Clear();
            proxy.Attributes = original.Attributes;
            proxy.RelatedEntities.AddRange(original.RelatedEntities);
            proxy.FormattedValues.AddRange(original.FormattedValues);
            proxy.ExtensionData = original.ExtensionData;
            proxy.EntityState = original.EntityState;
            proxy.Id = original.Id;

            return proxy;
        }

    }
}
