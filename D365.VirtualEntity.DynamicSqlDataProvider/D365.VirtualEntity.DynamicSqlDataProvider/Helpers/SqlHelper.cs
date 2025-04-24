using D365.VirtualEntity.DynamicSqlDataProvider.ProxyClasses;
//using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net.Http;
using System.Text.Json;

namespace D365.VirtualEntity.DynamicSqlDataProvider.Helpers
{
    public static class SqlHelper
    {
        private class AccessToken
        {
            public string access_token { get; set; }
        }


        public static string GetAadToken(string tenantId, string clientId, string clientSecret, ITracingService tracer)
        {
            string token = string.Empty;
                
            using (HttpClient client = new HttpClient())
            {
                var body = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("resource", "https://database.windows.net"),
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                };

                client.DefaultRequestHeaders.ConnectionClose = true;
                var httpRes = client.PostAsync($"https://login.microsoftonline.com/{tenantId}/oauth2/token", new FormUrlEncodedContent(body)).Result;
                string responseText = httpRes.Content.ReadAsStringAsync().Result; //Make sure it is synchonrous
                var tokenval=JsonSerializer.Deserialize<AccessToken>(responseText);                
                token = tokenval.access_token;
            }
            return token;
        }

        public static SqlConnection GetSqlConnection(ITracingService tracer, DSqlVeP_DynamicSqlVirtualEntityDataSource datasource)
        {
            tracer.Trace($"Configuring SQL Connection Type: {datasource.DSqlVeP_ConnectionType.ToString()}");

            var connection = new SqlConnection();
            try
            {
                // introduce a type
                if (datasource.DSqlVeP_ConnectionType == DSqlVeP_DynamicSqlVirtualEntityDataSource_DSqlVeP_ConnectionType.AadConnection)
                {
                    
                    SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder();
                    csb.DataSource = datasource.DSqlVeP_ServerName;
                    csb.InitialCatalog = datasource.DSqlVeP_DataBasename;
                    var accessToken = GetAadToken(datasource.DSqlVeP_AzureAdTenantId, datasource.DSqlVeP_AzureAdClientId, datasource.DSqlVeP_AzureAdClientSecret, tracer);
                    connection.ConnectionString = csb.ConnectionString;
                    connection.AccessToken = accessToken;
                }
                else if (datasource.DSqlVeP_ConnectionType == DSqlVeP_DynamicSqlVirtualEntityDataSource_DSqlVeP_ConnectionType.SqlConnection && !string.IsNullOrWhiteSpace(datasource.DSqlVeP_ConnectionString))
                {
                    connection.ConnectionString = datasource.DSqlVeP_ConnectionString;
                }
                else
                {
                    throw new Exception("Invalid connection type or parameters missing");
                }
            }

            catch (Exception ex)
            {
                tracer.Trace("Error setting the SQL connection:");
                tracer.Trace(ex.ToString());
                throw;
            }

            return connection;


        }
    }
}
