using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;

namespace D365.VirtualEntity.DynamicSqlDataProvider.Services
{
    public static class UserService
    {
        private static readonly Guid SysAdminRoleId = new Guid("627090FF-40A3-4053-8790-584EDC5BE201");

        public static bool UserIsSysAdmin(IOrganizationService service, Guid userId)
        {
            var query = new QueryExpression("role");
            query.Criteria.AddCondition("roletemplateid", ConditionOperator.Equal, SysAdminRoleId);
            var link = query.AddLink("systemuserroles", "roleid", "roleid");
            link.LinkCriteria.AddCondition("systemuserid", ConditionOperator.Equal, userId);

            return service.RetrieveMultiple(query).Entities.Count > 0;
        }

        /// <summary>
        /// excludes default org team
        /// </summary>
        /// <param name="teamIds"></param>
        /// <returns></returns>
        public static List<Guid> GetUsersInTeams(IOrganizationService service, List<Guid> teamIds)
        {
            // main query returing users
            QueryExpression userQuery = new QueryExpression("systemuser");
            // take all columns
            userQuery.ColumnSet = new ColumnSet("systemuserid");
            LinkEntity teamLink = new LinkEntity("systemuser", "teammembership", "systemuserid", "systemuserid", JoinOperator.Inner);
            teamLink.LinkCriteria.AddCondition(new ConditionExpression("teamid", ConditionOperator.In, teamIds.ToArray()));
            // add the intersect to the query
            userQuery.LinkEntities.Add(teamLink);
            //get the results
            EntityCollection retrievedUsers = service.RetrieveMultiple(userQuery);
            return retrievedUsers.Entities.Select(e => e.Id).ToList();
        }

        public static List<Guid> GetTeams(IOrganizationService service, Guid userId)
        {
            QueryExpression query = new QueryExpression("team");
            query.ColumnSet = new ColumnSet("teamid", "name");
            LinkEntity link = query.AddLink("teammembership", "teamid", "teamid");
            link.LinkCriteria.AddCondition(new ConditionExpression("systemuserid", ConditionOperator.Equal, userId));
            query.Criteria.AddCondition(new ConditionExpression("isdefault", ConditionOperator.NotEqual, true));

            try
            {
                var res = service.RetrieveMultiple(query);
                return res.Entities.Select(e => e.Id).ToList();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
