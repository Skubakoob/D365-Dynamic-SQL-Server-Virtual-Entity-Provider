using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365.VirtualEntity.DynamicSqlDataProvider.Model
{
    public class QueryCondition
    {
        public string AttributeName { get; set; } = String.Empty;
        // public List<object> Value { get; set; } = new List<object>();
        public List<object> Value { get; set; } = new List<object>();

        public QueryConditionOperator Operator { get; set; } = QueryConditionOperator.Eq;

        public QueryCondition()
        {

        }

        public QueryCondition(string attributeName, object value, QueryConditionOperator conditionOperator = QueryConditionOperator.Eq)
        {
            AttributeName = attributeName;
            Operator = conditionOperator;
            Value = new List<Object>() { value };
        }

        public QueryCondition(string attributeName, List<object> values, QueryConditionOperator conditionOperator = QueryConditionOperator.Eq)
        {

            AttributeName = attributeName;
            Operator = conditionOperator;
            Value = values;

        }
    }
}
