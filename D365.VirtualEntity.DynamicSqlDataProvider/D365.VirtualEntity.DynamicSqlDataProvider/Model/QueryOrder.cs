using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365.VirtualEntity.DynamicSqlDataProvider.Model
{
    public class QueryOrder
    {
        public string AttributeName;
        public QueryOrderType OrderType;

        public QueryOrder(string attributeName, QueryOrderType orderType)
        {
            AttributeName = attributeName;
            OrderType = orderType;
        }
    }
}
