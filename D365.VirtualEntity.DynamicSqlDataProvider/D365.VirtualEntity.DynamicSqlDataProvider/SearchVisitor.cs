using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365.VirtualEntity.DynamicSqlDataProvider
{
    public class SearchVisitor : QueryExpressionVisitorBase
    {
        public string SearchKeyWords { get; private set; }

        public override QueryExpression Visit(QueryExpression query)
        {
            // Very simple visitor that extracts search keywords
            if (query.Criteria.Conditions.Count == 0)
            {
                return null;
            }

            foreach (ConditionExpression condition in query.Criteria.Conditions)
            {
                if (condition.Operator == ConditionOperator.Like && condition.Values.Count > 0)
                {
                    string exprVal = (string)condition.Values[0];

                    if (exprVal.Length > 2)
                    {
                        this.SearchKeyWords += " " + exprVal.Substring(1, exprVal.Length - 2);
                    }
                }
            }
            return query;
        }
    }
}
