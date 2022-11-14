using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using System.Runtime.Serialization.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using D365.VirtualEntity.DynamicSqlDataProvider.Model;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Data.Extensions;
using System.Data.SqlClient;

namespace D365.VirtualEntity.DynamicSqlDataProvider
{
    public class VirtualEntityService
    {
        private IOrganizationService Service;
        private string EntityName;
        private EntityMetadata Metadata;
        private ConnectionSettings ConnectionSettings;
        private DynamicEntityMapper EntityMapper;
        private AttributeMetadata IdAttribute;
        private AttributeMetadata NameAttribute;
        List<AttributeMetadata> ExternalAttributes { get; set; }
        private ITracingService Tracer;
        private Entity Datasource;
        private readonly int CommandTimeout = 90;

        private readonly string EnableCreateCol = "dsqlvep_enablecreate";
        private readonly string EnableUpdateCol = "dsqlvep_enableupdate";
        private readonly string EnableDeleteCol = "dsqlvep_enabledelete";
        private readonly string RetrieveLinkCol = "dsqlvep_retrievelinkdata";
        private readonly string ConnectionStringCol = "dsqlvep_connectionstring";

        public VirtualEntityService(Entity datasource, IOrganizationService service, ITracingService tracer, string entityName)
        {
            Service = service;
            EntityName = entityName;
            Metadata = GetEntityMetadata(entityName);
            Tracer = tracer;
            Datasource = datasource;
            ConnectionSettings = GetConnectionSettings();//(Guid)Metadata.DataSourceId);
            EntityMapper = new DynamicEntityMapper(Metadata, Tracer);
            IdAttribute = Metadata.Attributes.Where(ema => ema.IsPrimaryId == true).First();
            NameAttribute = Metadata.Attributes.Where(ema => ema.IsPrimaryName == true).First();
            ExternalAttributes = Metadata.Attributes.Where(attr => !string.IsNullOrWhiteSpace(attr.ExternalName)).ToList();
            //CommandTimeout = 120;
        }

        public bool DeleteEntity(EntityReference entity)
        {

            if (!ConnectionSettings.IsDeleteEnabled)
            {
                throw new InvalidPluginExecutionException("Deletes are disabled for this virtual entity connection data source");
            }

            if (entity == null)
            {
                throw new InvalidPluginExecutionException("Entity to delete is null");
            }

            if (entity.Id == null)
            {
                throw new InvalidPluginExecutionException("Entity Id to delete is null");
            }


            bool success = false;
            string deleteString = $"DELETE FROM {Metadata.ExternalName} WHERE {IdAttribute.ExternalName}=@id";
            using (SqlConnection connection = new SqlConnection(ConnectionSettings.SqlConnectionString))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandTimeout = CommandTimeout;
                    command.CommandText = deleteString;
                    command.Parameters.AddWithValue("@id", entity.Id);
                    command.ExecuteNonQuery();
                    success = true;
                }
            }
            return success;
        }

        public Guid CreateEntity(Entity entity)
        {
            if (!ConnectionSettings.IsCreateEnabled)
            {
                throw new InvalidPluginExecutionException("Creates are disabled for this virtual entity connection data source");
            }

            if (entity == null)
            {
                throw new InvalidPluginExecutionException("Entity to create is null");
            }

            Guid id = Guid.NewGuid();

            var atts = entity.Attributes.Select(ea => ea.Key);
            var attributesToUpdate = ExternalAttributes.Where(ea => atts.Contains(ea.LogicalName));
            var mappedItem = EntityMapper.MapItem(entity);
            var rawValues = mappedItem.ToDictionary(); //entity.ToRaw();

            using (SqlConnection connection = new SqlConnection(ConnectionSettings.SqlConnectionString))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                //using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandTimeout = CommandTimeout;
                    List<string> colList = new List<string>();
                    List<string> paramNames = new List<string>();

                    string createString = "INSERT INTO " + Metadata.ExternalName + " ({0}) VALUES ({1})";

                    // assign an id if there isn't one in the object
                    // if there is, the for loop will handle the params otherwise we need to set it here
                    if (rawValues.ContainsKey(IdAttribute.LogicalName))
                    {
                        id = (Guid)rawValues[IdAttribute.LogicalName];
                    }
                    else
                    {
                        colList.Add(IdAttribute.ExternalName);
                        paramNames.Add("@id");
                        command.Parameters.AddWithValue("@id", id);
                    }


                    foreach (var att in attributesToUpdate)
                    {
                        if (rawValues.ContainsKey(att.LogicalName))
                        {
                            var pname = $"@{att.LogicalName}";
                            paramNames.Add(pname);
                            command.Parameters.AddWithValue(pname, rawValues[att.LogicalName] ?? DBNull.Value);
                            colList.Add($"{att.ExternalName}");
                        }
                    }
                    command.CommandText = string.Format(createString, string.Join(",", colList), string.Join(",", paramNames));
                    command.ExecuteNonQuery();
                }
            }
            return id;
        }

        public bool UpdateEntity(Entity entity)
        {
            if (!ConnectionSettings.IsUpdateEnabled)
            {
                throw new InvalidPluginExecutionException("Updates are disabled for this virtual entity connection data source");
            }

            var success = false;
            if (entity == null)
            {
                throw new InvalidPluginExecutionException("Entity to update is null");
            }

            if (entity.Id == null)
            {
                throw new InvalidPluginExecutionException("Entity to update has no Id");
            }

            var atts = entity.Attributes.Select(ea => ea.Key);
            var attributesToUpdate = ExternalAttributes.Where(ea => atts.Contains(ea.LogicalName));
            var mappedItem = EntityMapper.MapItem(entity);
            var rawValues = mappedItem.ToDictionary();

            using (SqlConnection connection = new SqlConnection(ConnectionSettings.SqlConnectionString))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandTimeout = CommandTimeout;
                    List<string> colList = new List<string>();
                    string updateString = "UPDATE " + Metadata.ExternalName + " SET {0} WHERE " + IdAttribute.ExternalName + "=@id";
                    command.Parameters.AddWithValue("@id", entity.Id);
                    foreach (var att in attributesToUpdate)
                    {
                        if (rawValues.ContainsKey(att.LogicalName))
                        {
                            command.Parameters.AddWithValue($"@{att.LogicalName}", rawValues[att.LogicalName] ?? DBNull.Value);
                            colList.Add($"{att.ExternalName}=@{att.LogicalName}");
                        }
                    }
                    command.CommandText = string.Format(updateString, string.Join(",", colList));
                    command.ExecuteNonQuery();
                }
            }
            success = true;
            return success;
        }

        public EntityCollection GetEntities(QueryExpression qe)
        {
            var qeSerialized = SerializeObject<QueryExpression>(qe);
            Tracer.Trace(qeSerialized);

            var queryBuilder = new QueryBuilder(Metadata, Tracer);
            queryBuilder.ParseQueryExpression(qe);

            EntityCollection results = new EntityCollection();
            results.EntityName = Metadata.LogicalName;
            // var hasMoreRecords = false;

            var queryResponse = RetrieveData(queryBuilder);
            // Update the mapped records with any linked entity data, if the connection allows it
            if (ConnectionSettings.CanRetrieveLinkData)
            {
                queryResponse.MappedEntities = PopulateLinkedEntityValues(queryResponse.MappedEntities, qe);
            }

            results.Entities.AddRange(queryResponse.MappedEntities.Select(m => m.ToEntity()));

            if (queryResponse.HasMoreRecords)
            {
                // total behaves oddly, if you don't have a lower limit it won't enable the paging button in D365
                // we don't want to do total count of the table in case it is very large, as that could cause some performance problems
                // so we'll se it to an arbitrary value
                var total = 5000;// (queryExpression.PageInfo.PageNumber * queryBuilder.PagingSize)+1;

                PagingInfo pi = new PagingInfo();
                pi.PageNumber = qe.PageInfo.PageNumber + 1;
                pi.Count = qe.PageInfo.Count;
                results = PaginationExtensions.WithNumericPagination(results, pi, total);
                results.TotalRecordCountLimitExceeded = true;
            }
            else
            {
                var total = ((qe.PageInfo.PageNumber - 1) * qe.PageInfo.Count) + results.Entities.Count;
                results = PaginationExtensions.WithNumericPagination(results, qe.PageInfo, total);
            }

            return results;

        }

        public Entity GetEntity(Guid id)
        {
            var queryBuilder = new QueryBuilder(Metadata, Tracer);

            var queryFilter = new QueryFilter();
            queryFilter.AddCondition(IdAttribute.ExternalName, id);
            queryBuilder.Filter = queryFilter;

            var queryResponse = RetrieveData(queryBuilder);

            if (queryResponse.MappedEntities.Count == 0)
            {
                throw new InvalidPluginExecutionException($"Entity with id {id} not found");
            }

            if (queryResponse.MappedEntities.Count > 1)
            {
                throw new InvalidPluginExecutionException($"Multiple results returned for id {id}");

            }

            return queryResponse.MappedEntities[0].ToEntity();
        }

        private QueryResponse RetrieveData(QueryBuilder queryBuilder)
        {
            var res = new QueryResponse();

            using (SqlConnection connection = new SqlConnection(ConnectionSettings.SqlConnectionString))
            {
                connection.Open();

                using (SqlCommand command = queryBuilder.BuildSqlCommand(false))
                //using (SqlCommand command = connection.CreateCommand())
                {
                    command.Connection = connection;
                    command.CommandTimeout = CommandTimeout;
                    using (SqlDataReader reader = command.ExecuteReader())
                    {

                        while (reader.Read())
                        {

                            var item = Enumerable.Range(0, reader.FieldCount).ToDictionary(reader.GetName, reader.GetValue); //Select(i => reader.GetName(i)).ToArray();

                            var mappedEntity = EntityMapper.MapItem(item);

                            if (res.MappedEntities.Count < queryBuilder.PagingSize)
                            {
                                res.MappedEntities.Add(mappedEntity);
                            }
                            else
                            {
                                // and it will skip the final item
                                res.HasMoreRecords = true;
                            }
                        }
                    }
                }
            }
            return res;
        }

        private List<MappedEntity> PopulateLinkedEntityValues(List<MappedEntity> data, QueryExpression qe)
        {
            var results = new List<Entity>();
            if (data.Count == 0)
            {
                return data;
            }

            foreach (var link in qe.LinkEntities)
            {
                var linkData = RetrieveLinkEntityValues(link, data);
                data = ApplyLinkEntityValues(data, linkData, link);
            }
            return data;
        }

        private List<Entity> RetrieveLinkEntityValues(LinkEntity link, List<MappedEntity> data)
        {
            var results = new List<Entity>();
            var linkedEntityMetadata = GetEntityMetadata(link.LinkToEntityName);
            var linkedEntityMapper = new DynamicEntityMapper(linkedEntityMetadata, Tracer);

            List<Guid> linkIds;
            var attr = this.Metadata.Attributes.Where(at => at.LogicalName == link.LinkFromAttributeName).FirstOrDefault();
            if (attr != null)
            {
                linkIds = data
                    .SelectMany(d => d.Attributes.Where(a => a.AttributeLogicalName == link.LinkFromAttributeName))
                    .Where(v => v.RawValue != null)
                    .Select(v => (Guid)v.RawValue)
                    .Distinct()
                    .ToList();

                if (linkIds.Count > 0)
                {
                    var linkedAttribute = linkedEntityMetadata.Attributes.FirstOrDefault(a => a.LogicalName == link.LinkToAttributeName);

                    if (linkedAttribute != null && link.JoinOperator == JoinOperator.Inner || link.JoinOperator == JoinOperator.LeftOuter)
                    {
                        Tracer.Trace("Running query against " + link.LinkToEntityName + " on " + link.LinkToAttributeName);

                        QueryExpression linkExpression = new QueryExpression(link.LinkToEntityName);
                        linkExpression.ColumnSet = new ColumnSet(link.Columns.Columns.ToArray());
                        linkExpression.Criteria.AddCondition(link.LinkToAttributeName, ConditionOperator.In, linkIds.Select(i => (object)i).ToArray());
                        linkExpression.PageInfo = new PagingInfo();
                        linkExpression.PageInfo.Count = 250;
                        linkExpression.PageInfo.PageNumber = 1;

                        while (true)
                        {
                            // It's possible this will have multiple pages results?
                            var res = Service.RetrieveMultiple(linkExpression);
                            results.AddRange(res.Entities);
                            if (res.MoreRecords)
                            {
                                linkExpression.PageInfo.PageNumber++;
                                // Set the paging cookie to the paging cookie returned from current results.
                                linkExpression.PageInfo.PagingCookie = res.PagingCookie;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }

                }
            }
            return results;
        }

        private List<MappedEntity> ApplyLinkEntityValues(List<MappedEntity> data, List<Entity> linkData, LinkEntity link)
        {
            foreach (var lv in linkData)
            {
                var mapTo = data.Where(d => d.Attributes.Any(a => a.AttributeLogicalName == link.LinkFromAttributeName && a.RawValue != null && (Guid)a.RawValue == lv.Id));
                foreach (var ent in mapTo)
                {
                    foreach (var col in link.Columns.Columns)
                    {
                        if (lv.Contains(col))
                        {
                            if (lv.FormattedValues.ContainsKey(col))
                            {
                                var fv = lv.FormattedValues[col];
                                ent.AddAttribute(new MappedEntityAttribute(link.EntityAlias + "." + col, string.Empty, lv[col], lv[col], true, fv));
                            }
                            else
                            {
                                ent.AddAttribute(new MappedEntityAttribute(link.EntityAlias + "." + col, string.Empty, lv[col], lv[col], true));
                            }

                            Tracer.Trace(String.Format("added value {0} to {1}", lv[col], link.EntityAlias + "." + col));
                        }
                    }
                }
            }
            return data;
        }

        private ConnectionSettings GetConnectionSettings()//Guid datasourceId)
        {
            ColumnSet cs = new ColumnSet(true);

            //var e = Datasource; 
            var iscreateEnabled = Datasource.Contains(EnableCreateCol) ? (bool)Datasource[EnableCreateCol] : false;
            var isUpdateEnabled = Datasource.Contains(EnableUpdateCol) ? (bool)Datasource[EnableUpdateCol] : false;
            var isDeleteEnabled = Datasource.Contains(EnableDeleteCol) ? (bool)Datasource[EnableDeleteCol] : false;
            var canRetrieveLinkData = Datasource.Contains(RetrieveLinkCol) ? (bool)Datasource[RetrieveLinkCol] : false;

            var cn = new ConnectionSettings()
            {
                SqlConnectionString = (string)Datasource[ConnectionStringCol],
                IsCreateEnabled = iscreateEnabled,
                IsUpdateEnabled = isUpdateEnabled,
                IsDeleteEnabled = isDeleteEnabled,
                CanRetrieveLinkData = canRetrieveLinkData,
            };

            return cn;
        }

        private EntityMetadata GetEntityMetadata(string entityName)
        {
            RetrieveEntityRequest req = new RetrieveEntityRequest();
            req.RetrieveAsIfPublished = true;
            req.LogicalName = entityName;
            req.EntityFilters = EntityFilters.Attributes | EntityFilters.Relationships;
            RetrieveEntityResponse res = Service.Execute(req) as RetrieveEntityResponse;
            return res.EntityMetadata;
        }

        /// <summary>
        /// This is only really needed to help us trace and debug the query expressions
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        private string SerializeObject<T>(object obj)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                serializer.WriteObject(stream, obj);
                stream.Position = 0;
                StreamReader reader = new StreamReader(stream);
                string requestBody = reader.ReadToEnd();
                return requestBody;
            }
        }

    }
}
