using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365.VirtualEntity.DynamicSqlDataProvider.Model
{
    public class QueryFilter
    {
        public List<QueryCondition> Conditions { get; set; }=new List<QueryCondition>();
        public QueryFilterOperator Operator { get; set; } = QueryFilterOperator.AND;
        public List<QueryFilter> Filters { get; set; } = new List<QueryFilter>();

        public void AddCondition(string attributeName, object value, QueryConditionOperator conditionOperator = QueryConditionOperator.Eq)
        {
            Conditions.Add(new QueryCondition()
            {
                AttributeName = attributeName,
                Operator = conditionOperator,
                Value = new List<Object>() { value }
            });
        }

        public void AddCondition(string attributeName, List<object> values, QueryConditionOperator conditionOperator = QueryConditionOperator.Eq)
        {
            Conditions.Add(new QueryCondition()
            {
                AttributeName = attributeName,
                Operator = conditionOperator,
                Value = values
            });
        }

    }
}
