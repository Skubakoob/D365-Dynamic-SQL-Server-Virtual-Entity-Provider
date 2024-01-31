using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using System.Web.Services.Description;

namespace D365.VirtualEntity.DynamicSqlDataProvider.Model
{
    public class QueryBuilder
    {
        public List<string> Columns { get; set; } = new List<string>();
        public List<string> LinkColumns { get; set; } = new List<string>();
        public QueryFilter Filter { get; set; }
        public List<QueryOrder> Ordering { get; set; } = new List<QueryOrder>();
        public int PagingSize { get; set; } = 50;
        public int PageNumber { get; set; } = 1;
        public List<AttributeMetadata> ExternalAttributes { get; }
        private AttributeMetadata IdAttribute { get; set; }
        private AttributeMetadata NameAttribute { get; set; }
        private EntityMetadata Metadata { get; set; }
        private ITracingService Tracer { get; set; }
        public QueryBuilder(EntityMetadata metadata, ITracingService tracer)
        {
            Metadata = metadata;
            Tracer = tracer;
            ExternalAttributes = Metadata.Attributes.Where(attr => !string.IsNullOrWhiteSpace(attr.ExternalName)).ToList();
            IdAttribute = Metadata.Attributes.Where(ema => ema.IsPrimaryId == true).First();
            NameAttribute = Metadata.Attributes.Where(ema => ema.IsPrimaryName == true).First();
        }
        public void AddColumn(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                return;
            }

            if (Columns.IndexOf(columnName) == -1)
            {
                Columns.Add(columnName);
            }
        }
        private string BuildSqlCondition(QueryCondition condition, SqlCommand command, List<int> level, int cndIx)
        {
            var paramNames = new List<string>();

            for (var ix = 0; ix < condition.Value.Count; ix++)
            {
                var val = condition.Value[ix];
                string paramName = string.Format("@{0}_{1}_{2}_{3}", string.Join("_", level), condition.AttributeName, ix, cndIx);
                paramNames.Add(paramName);
                command.Parameters.Add(new SqlParameter(paramName, val));
            }
            var conditionStr = string.Empty;
            var conditionOperator = string.Empty;
            var conditionValueFilter = string.Empty;
            var conditionValue = string.Join(",", paramNames);
            var isMultiCondition = paramNames.Count > 1;

            switch (condition.Operator)
            {
                case QueryConditionOperator.Eq:
                case QueryConditionOperator.In:
                    conditionOperator = isMultiCondition ? "IN" : "=";
                    conditionValueFilter = string.Format(isMultiCondition ? "({0})" : "{0}", conditionValue);
                    break;
                case QueryConditionOperator.Ne:
                case QueryConditionOperator.NotIn:
                    conditionOperator = isMultiCondition ? "NOT IN" : "!=";
                    conditionValueFilter = string.Format(isMultiCondition ? "({0})" : "{0}", conditionValue);
                    break;
                case QueryConditionOperator.GreaterThan:
                    conditionOperator = ">";
                    conditionValueFilter = string.Format("{0}", conditionValue);
                    break;
                case QueryConditionOperator.GreaterThanOrEqual:
                    conditionOperator = ">=";
                    conditionValueFilter = string.Format("{0}", conditionValue);
                    break;
                case QueryConditionOperator.LessThan:
                    conditionOperator = "<";
                    conditionValueFilter = string.Format("{0}", conditionValue);
                    break;
                case QueryConditionOperator.LessThanOrEqual:
                    conditionOperator = "<=";
                    conditionValueFilter = string.Format("{0}", conditionValue);
                    break;
                case QueryConditionOperator.Like:
                    conditionOperator = "LIKE";
                    conditionValueFilter = string.Format("'%'+{0}+'%'", conditionValue);
                    break;
                case QueryConditionOperator.BeginsWith:
                    conditionOperator = "LIKE";
                    conditionValueFilter = string.Format("{0}'%'", conditionValue);
                    break;
                case QueryConditionOperator.NotNull:
                    conditionOperator = "IS NOT NULL"; // or "IS"
                    conditionValueFilter = string.Empty;
                    break;
                case QueryConditionOperator.Null:
                    conditionOperator = "IS NULL";
                    conditionValueFilter = string.Empty;
                    break;
                default:
                    throw new NotImplementedException($"Query condition operator mapping for {condition.Operator.ToString()} is not yet implemented");

            }
            conditionStr = string.Format("a.{0} {1} {2}", condition.AttributeName, conditionOperator, conditionValueFilter);
            // nb the condition value filter is the value parameter name, not the actual value
            // filterConditions.Add(string.Format("a.{0} {1} {2}", condition.AttributeName, conditionOperator, conditionValueFilter));

            return conditionStr;
        }
        private string BuildSqlFilter(SqlCommand command, QueryFilter filter, List<int> level)
        {
            List<string> filterConditions = new List<string>();

            var cndIx = -1;
            filter.Conditions.ForEach(condition =>
            {
                cndIx++;
                var conditionStr = BuildSqlCondition(condition, command, level, cndIx);
                if (!string.IsNullOrWhiteSpace(conditionStr))
                {
                    filterConditions.Add(conditionStr);
                }
            });

            var levelIx = 0;
            var subFilters = new List<string>();
            filter.Filters.ForEach(subFilter =>
            {
                var currentLevel = new List<int>(level);
                currentLevel.Add(levelIx);
                levelIx++;
                if (subFilter.Filters.Count > 0 || subFilter.Conditions.Count > 0)
                {
                    var subFilterQryString = BuildSqlFilter(command, subFilter, currentLevel);
                    subFilters.Add(subFilterQryString);
                }
            });

            var filterString = string.Join(" " + filter.Operator.ToString() + " ", filterConditions);

            Tracer.Trace(filterString);

            if (subFilters.Count > 0)
            {
                var subFilterString = string.Empty;
                if (filter.Conditions.Count > 0)
                {
                    subFilterString = $" {filter.Operator.ToString()} ({String.Join(" ", subFilters)})";
                }
                else
                {
                    subFilterString = $"{String.Join(" ", subFilters)}";
                }
                filterString += " " + subFilterString;

            }
            return filterString;
        }
        public SqlCommand BuildSqlCommand(bool isPaged)
        {
            SqlCommand command = new SqlCommand();
            List<string> qry = new List<string>();

            string selectColumns = string.Empty;

            if (Columns.Count == 0)
            {
                selectColumns = "a.*";
            }
            else
            {
                // ensure Id and primary name column are included
                AddColumn(IdAttribute.ExternalName);
                AddColumn(NameAttribute.ExternalName);

                selectColumns = string.Join(",", Columns.Union(LinkColumns).Distinct().Select(col => "a." + col));
            }

            qry.Add(String.Format("SELECT {0} FROM {1} as a", selectColumns, Metadata.ExternalName)); // removed where 1=1

            var filters = BuildSqlFilter(command, Filter, new List<int>() { 0 });

            if (!string.IsNullOrWhiteSpace(filters))
            {
                //qry.Add("AND (" + filters + ")");
                qry.Add($"WHERE {filters}");
            }

            var pageNumber = PageNumber - 1;
            if (pageNumber < 0)
            {
                pageNumber = 0;
            }

            if (Ordering.Count == 0)
            {
                Tracer.Trace($"Primary attribute order: {NameAttribute.ExternalName}");
                Ordering.Add(new QueryOrder(NameAttribute.ExternalName, QueryOrderType.Ascending));
            }

            if (Ordering.Count > 0)
            {
                List<string> orderbyStr = new List<string>();
                qry.Add("ORDER BY");
                foreach (var o in Ordering)
                {
                    orderbyStr.Add(String.Format("{0} {1}", o.AttributeName, (o.OrderType == QueryOrderType.Ascending ? "ASC" : "DESC")));
                }
                qry.Add(String.Join(",", orderbyStr));
            }

            if (isPaged)
            {
                //// add 1 to paging size - if we get a full set we have more data
                qry.Add(string.Format("OFFSET {0} ROWS FETCH NEXT {1} ROWS ONLY", pageNumber * PagingSize, (PagingSize + 1)));
            }

            command.CommandText = String.Join(" ", qry);
            Tracer.Trace(command.CommandText);
            return command;
        }
    }
}
