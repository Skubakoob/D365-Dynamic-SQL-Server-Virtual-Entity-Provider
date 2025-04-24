using D365.VirtualEntity.DynamicSqlDataProvider.Model;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace D365.VirtualEntity.DynamicSqlDataProvider.Helpers
{
    public static class QueryParserHelper
    {
        public static QueryBuilder ToQueryBuilder(this QueryExpression qe, EntityMetadata metadata, ITracingService tracingService)
        {
            QueryBuilder queryBuilder = new QueryBuilder(metadata, tracingService);
            queryBuilder.PageNumber = qe.PageInfo.PageNumber;
            queryBuilder.PagingSize = qe.PageInfo.Count;
            queryBuilder.Columns = ParseQueryExpressionColumns(qe, queryBuilder.ExternalAttributes);
            queryBuilder.LinkColumns = ParseQueryExpressionLinkColumns(qe, queryBuilder.ExternalAttributes);
            queryBuilder.Filter = ParseQueryExpressionFilter(qe.Criteria, queryBuilder.ExternalAttributes);
            queryBuilder.Ordering = ParseQueryExpressionOrdering(qe, queryBuilder.ExternalAttributes);
            return queryBuilder;
        }
        public static List<string> ParseQueryExpressionColumns(QueryExpression qe, List<AttributeMetadata> externalAttributes)
        {
            var columns = new List<string>();
            if (qe.ColumnSet.Columns.Count > 0)
            {
                qe.ColumnSet.Columns.ToList().ForEach(c =>
                {
                    var attr = externalAttributes.Where(a => a.LogicalName == c).FirstOrDefault();
                    if (attr != null && !string.IsNullOrWhiteSpace(attr.ExternalName))
                    {
                        //  AddColumn(attr.ExternalName.ToCamelCase());
                        if (columns.IndexOf(attr.ExternalName) == -1)
                        {
                            columns.Add(attr.ExternalName);
                            if (attr.AttributeType == AttributeTypeCode.Customer)
                            {
                                columns.Add(attr.ExternalName + "_entitytype");
                            }
                        }
                    }
                });
            }
            return columns;
        }
        public static List<string> ParseQueryExpressionLinkColumns(QueryExpression qe, List<AttributeMetadata> externalAttributes)
        {
            List<string> linkColumns = new List<string>();
            var cols = qe.LinkEntities.Select(le => le.LinkFromAttributeName).ToList();
            foreach (var col in cols)
            {
                var attr = externalAttributes.Where(a => a.LogicalName == col).FirstOrDefault();
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
        public static QueryCondition ParseQueryExpressionCondition(ConditionExpression cond, List<AttributeMetadata> externalAttributes)
        {
            QueryCondition queryCondition = null;
            var attr = externalAttributes.Where(a => a.LogicalName == cond.AttributeName).FirstOrDefault();

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
        public static QueryConditionOperator MapConditionOperator(ConditionOperator conditionOperator)
        {
            var queryConditionOperator = QueryConditionOperator.Eq;

            if (conditionOperator == ConditionOperator.NotEqual)
            {
                queryConditionOperator = QueryConditionOperator.Ne;
            }
            else if (conditionOperator == ConditionOperator.NotIn)
            {
                queryConditionOperator = QueryConditionOperator.NotIn;
            }
            else if (conditionOperator == ConditionOperator.Equal)
            {
                queryConditionOperator = QueryConditionOperator.Eq;
            }
            else if (conditionOperator == ConditionOperator.In)
            {
                queryConditionOperator = QueryConditionOperator.In;
            }
            else if (conditionOperator == ConditionOperator.GreaterEqual)
            {
                queryConditionOperator = QueryConditionOperator.GreaterThanOrEqual;
            }
            else if (conditionOperator == ConditionOperator.GreaterThan)
            {
                queryConditionOperator = QueryConditionOperator.GreaterThan;
            }
            else if (conditionOperator == ConditionOperator.LessEqual)
            {
                queryConditionOperator = QueryConditionOperator.LessThanOrEqual;
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
        public static List<QueryOrder> ParseQueryExpressionOrdering(QueryExpression qe, List<AttributeMetadata> externalAttributes)
        {
            var ordering = new List<QueryOrder>();
            foreach (var o in qe.Orders)
            {
                var orderType = o.OrderType == OrderType.Ascending ? QueryOrderType.Ascending : QueryOrderType.Descending;
                var attribute = externalAttributes.First(x => x.LogicalName == o.AttributeName); //  metadata.Attributes.First(x => x.LogicalName == o.AttributeName);
                ordering.Add(new QueryOrder(attribute.ExternalName, orderType));
            }
            return ordering;
        }
        public static List<object> ParseQueryValuesForAttribute(AttributeMetadata metadata, DataCollection<object> values)
        {
            List<object> parsedValues = new List<object>();

            foreach (var v in values)
            {
                var type = v.GetType();
                bool isList = type != null
                   && type.IsGenericType
                   && type.GetGenericTypeDefinition() == typeof(List<>);

                if (isList)
                {
                    foreach (var subval in (dynamic)v)
                    {
                        parsedValues.Add((object)subval);
                    }

                }
                else
                {
                    parsedValues.Add(v);
                }
            }
            return parsedValues;
        }
        public static QueryFilter ParseQueryExpressionFilter(FilterExpression fe, List<AttributeMetadata> externalAttributes)
        {
            var qf = new QueryFilter();
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

            foreach (var condition in fe.Conditions)
            {
                var queryCondition = ParseQueryExpressionCondition(condition, externalAttributes);
                qf.Conditions.Add(queryCondition);
            }

            foreach (var subFilter in fe.Filters)
            {
                var subQueryFilter = ParseQueryExpressionFilter(subFilter, externalAttributes);
                qf.Filters.Add(subQueryFilter);
            }

            return qf;
        }
    }
}
