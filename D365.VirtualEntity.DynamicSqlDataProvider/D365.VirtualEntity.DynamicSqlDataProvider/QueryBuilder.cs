using D365.VirtualEntity.DynamicSqlDataProvider.Model;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace D365.VirtualEntity.DynamicSqlDataProvider
{
    public class QueryBuilder
    {
        public List<string> Columns { get; set; }=new List<string>();
        public List<string> LinkColumns { get; set; } = new List<string>();
        public QueryFilter Filter { get; set; }
        public List<QueryOrder> Ordering { get; set; } = new List<QueryOrder>();
        public int PagingSize { get; set; } = 50;
        private int PageNumber { get; set; } = 1;
        List<AttributeMetadata> ExternalAttributes { get; set; }
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

        private List<string> ParseQueryExpressionColumns(QueryExpression qe)
        {          
            var columns= new List<string>();
            if (qe.ColumnSet.Columns.Count > 0)
            {
                qe.ColumnSet.Columns.ToList().ForEach(c =>
                {
                    var attr = ExternalAttributes.Where(a => a.LogicalName == c).FirstOrDefault();
                    if (attr != null && !string.IsNullOrWhiteSpace(attr.ExternalName))
                    {
                        //  AddColumn(attr.ExternalName.ToCamelCase());
                        if (columns.IndexOf(attr.ExternalName) == -1)
                        {
                            columns.Add(attr.ExternalName);
                        }
                    }
                });
            }
            return columns;
        }
        public void ParseQueryExpression(QueryExpression qe)
        {
            PageNumber = qe.PageInfo.PageNumber;
            PagingSize = qe.PageInfo.Count;

            Columns = ParseQueryExpressionColumns(qe);
            LinkColumns = ParseQueryExpressionLinkColumns(qe);
            Filter = ParseQueryExpressionFilter(qe.Criteria);
            Ordering = ParseQueryExpressionOrdering(qe);
        }
        
        private List<string> ParseQueryExpressionLinkColumns(QueryExpression qe)
        {
            List<string> linkColumns = new List<string>();
            var cols=qe.LinkEntities.Select(le=>le.LinkFromAttributeName).ToList();
            foreach (var col in cols)
            {
                var attr = ExternalAttributes.Where(a => a.LogicalName == col).FirstOrDefault();
                if (attr != null && !string.IsNullOrWhiteSpace(attr.ExternalName))
                {
                    //  AddColumn(attr.ExternalName.ToCamelCase());
                    if (linkColumns.IndexOf(attr.ExternalName) == -1)
                    {
                        linkColumns.Add(attr.ExternalName);
                    }
                }
            }
            return linkColumns;
        }

        private QueryFilter ParseQueryExpressionFilter(FilterExpression fe)
        {
            var qf=new QueryFilter();
            //var isQuickFindFilter = fe.IsQuickFindFilter;

            switch (fe.FilterOperator)
            {
                case LogicalOperator.And:
                    qf.Operator = QueryFilterOperator.AND;
                    break;
                case LogicalOperator.Or:
                    qf.Operator = QueryFilterOperator.OR;
                    break;
            }

            foreach(var condition in fe.Conditions)
            {
                var queryCondition = ParseQueryExpressionCondition(condition);
                qf.Conditions.Add(queryCondition);
            }

            foreach (var subFilter in fe.Filters)
            {
                var subQueryFilter = ParseQueryExpressionFilter(subFilter);
                qf.Filters.Add(subQueryFilter);
            }

            return qf;
        }
        
        private QueryConditionOperator MapConditionOperator(ConditionOperator conditionOperator)
        {
            var queryConditionOperator = QueryConditionOperator.Eq;

            // this needs more effort to makeit fully functioning
            if (conditionOperator == ConditionOperator.NotEqual || conditionOperator == ConditionOperator.NotIn)
            {
                queryConditionOperator = QueryConditionOperator.Ne;
            }
            else if (conditionOperator == ConditionOperator.Equal || conditionOperator == ConditionOperator.In)
            {
                queryConditionOperator = QueryConditionOperator.Eq;
            }
            else if (conditionOperator == ConditionOperator.GreaterThan)
            {
                queryConditionOperator = QueryConditionOperator.GreaterThan;
            }
            else if (conditionOperator == ConditionOperator.LessThan)
            {
                queryConditionOperator = QueryConditionOperator.LessThan;
            }
            else if (conditionOperator == ConditionOperator.Like)
            {
                queryConditionOperator = QueryConditionOperator.Like;
            }
            else if (conditionOperator == ConditionOperator.BeginsWith)
            {
                queryConditionOperator = QueryConditionOperator.BeginsWith;
            }
            else if (conditionOperator == ConditionOperator.NotNull)
            {
                queryConditionOperator = QueryConditionOperator.NotNull;
            }
            else if (conditionOperator == ConditionOperator.Null)
            {
                queryConditionOperator = QueryConditionOperator.Null;
            }
            else
            {
                throw new NotImplementedException($"Condition operator {conditionOperator.ToString()} not yet implemented");
            }

            return queryConditionOperator;
        }

        private QueryCondition ParseQueryExpressionCondition(ConditionExpression cond)
        {
            QueryCondition queryCondition = null;
            var attr = ExternalAttributes.Where(a => a.LogicalName == cond.AttributeName).FirstOrDefault();
            
            if (attr != null)
            {
                var parsedValues = ParseQueryValuesForAttribute(attr, cond.Values);

                var queryConditionOperator = MapConditionOperator(cond.Operator);

                if (parsedValues.Count == 1)
                {
                    queryCondition = new QueryCondition(attr.ExternalName, parsedValues[0], queryConditionOperator);
                }
                else
                {
                    queryCondition = new QueryCondition(attr.ExternalName, parsedValues, queryConditionOperator);
                }
            }

            return queryCondition;
        }

        private List<QueryOrder> ParseQueryExpressionOrdering(QueryExpression qe)
        {
            var ordering = new List<QueryOrder>();
            foreach (var o in qe.Orders)
            {
                var attribute = Metadata.Attributes.First(x => x.LogicalName == o.AttributeName);
                var orderType = o.OrderType == OrderType.Ascending ? QueryOrderType.Ascending : QueryOrderType.Descending;
                ordering.Add(new QueryOrder(attribute.ExternalName, orderType));
                //ordering.Add(new QueryOrder(o.AttributeName, orderType));
            }
            return ordering;
        }

        private static List<object> ParseQueryValuesForAttribute(AttributeMetadata metadata, DataCollection<object> values)
        {
            List<object> parsedValues = new List<object>();

            foreach (var v in values)
            {
                parsedValues.Add(v);
            }
            return parsedValues;
        }

        private string BuildSqlCondition(QueryCondition condition,SqlCommand command, List<int> level, int cndIx)
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
                    conditionOperator = isMultiCondition ? "IN" : "=";
                    conditionValueFilter = string.Format(isMultiCondition ? "({0})" : "{0}", conditionValue);
                    break;
                case QueryConditionOperator.Ne:
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
            List<string> filterConditions=new List<string>();

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
            var subFilters=new List<string>();
            filter.Filters.ForEach(subFilter =>
            {
                var currentLevel=new List<int>(level);
                currentLevel.Add(levelIx);
                levelIx++;

                var subFilterQryString = " ("+ BuildSqlFilter(command, subFilter, currentLevel)+")";
                subFilters.Add(subFilterQryString);
            });

            var filterString=string.Join(" " + filter.Operator.ToString() + " ", filterConditions);
            filterString += String.Join(" ", subFilters);

            // nb above doesn't protext against nested filters that don't have any conditions

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
                AddColumn(IdAttribute.ExternalName);//.ToCamelCase()); 
                AddColumn(NameAttribute.ExternalName);//.ToCamelCase());

                selectColumns = string.Join(",", Columns.Union(LinkColumns).Distinct().Select(col => "a." + col));
            }

            qry.Add(String.Format("SELECT {0} FROM {1} as a where 1=1", selectColumns, Metadata.ExternalName));

            var filters = BuildSqlFilter(command, Filter,new List<int>() { 0 });

            if (!string.IsNullOrWhiteSpace(filters))
            {
                qry.Add("AND ("+filters+")");
            }

            var pageNumber = PageNumber - 1;
            if (pageNumber < 0)
            {
                pageNumber = 0;
            }

            if (Ordering.Count == 0)
            {                
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

            if (isPaged) {
                //// add 1 to paging size - if we get a full set we have more data
                qry.Add(string.Format("OFFSET {0} ROWS FETCH NEXT {1} ROWS ONLY", pageNumber * PagingSize, (PagingSize + 1)));
            }

            command.CommandText= String.Join(" ", qry);
            Tracer.Trace(command.CommandText);
            return command;
        }
    }
}
