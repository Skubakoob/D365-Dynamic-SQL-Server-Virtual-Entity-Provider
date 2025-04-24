using D365.VirtualEntity.DynamicSqlDataProvider.Model;
using D365.VirtualEntity.DynamicSqlDataProvider.ProxyClasses;
using D365.VirtualEntity.DynamicSqlDataProvider.Services;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365.VirtualEntity.DynamicSqlDataProvider.Helpers
{
    public static class QuerySecurityHelper
    {

        /// <summary>
        /// Replaces the current query builder filter with the security filter
        /// </summary>
        /// <param name="qb"></param>
        public static QueryBuilder ApplySecurityFilter(this QueryBuilder qb, DSqlVeP_DynamicSqlVirtualEntityDataSource datasource, IOrganizationService service, Guid userId)
        {
            var secFilter = BuildSecurityFilter(datasource, service, userId);
            if (secFilter.Conditions.Count > 0 || secFilter.Filters.Count > 0)
            {
                secFilter.Operator = QueryFilterOperator.AND;
                secFilter.Filters.Add(qb.Filter);
                qb.Filter = secFilter;
            }
            return qb;
        }
        private static QueryFilter BuildSecurityFilter(DSqlVeP_DynamicSqlVirtualEntityDataSource datasource, IOrganizationService service, Guid userId)
        {
            var filter = new QueryFilter();
            var userIsAdmin = UserService.UserIsSysAdmin(service, userId);
            if (!string.IsNullOrWhiteSpace(datasource.DSqlVeP_OwnerSecurityAttributeName) && !userIsAdmin)
            {
                var userTeams = UserService.GetTeams(service, userId);
                var validUserIds = UserService.GetUsersInTeams(service, userTeams);
                List<Object> validOwners = new List<object>();
                validOwners.AddRange(userTeams.Select(x => (object)x));
                validOwners.AddRange(validUserIds.Select(x => (object)x));
                validOwners.Add(userId);
                filter.AddCondition(datasource.DSqlVeP_OwnerSecurityAttributeName, validOwners, QueryConditionOperator.In);
            }
            return filter;
        }
    }
}
