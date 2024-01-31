﻿using D365.VirtualEntity.DynamicSqlDataProvider.ProxyClasses;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static D365.VirtualEntity.DynamicSqlDataProvider.ProxyClasses.DSqlVeP_DynamicSqlVirtualEntityDataSource;

namespace D365.VirtualEntity.DynamicSqlDataProvider.Helpers
{
    public static class SqlHelper
    {
        public static string GetAadToken(string tenantId, string clientId, string clientSecret)
        {
            string token = string.Empty;
            ClientCredential clientCredentials = new ClientCredential(clientId, clientSecret);
            AuthenticationContext authenticationContext = new AuthenticationContext($"https://login.microsoftonline.com/{tenantId}", validateAuthority: false);
            AuthenticationResult authenticationResult = authenticationContext.AcquireTokenAsync("https://database.windows.net", clientCredentials).GetAwaiter().GetResult();
            return authenticationResult.AccessToken;
        }

        public static SqlConnection GetSqlConnection(ITracingService tracer, DSqlVeP_DynamicSqlVirtualEntityDataSource datasource)
        {
            tracer.Trace($"Configuring SQL Connection Type: {datasource.dsqlvep_connectiontype.ToString()}");

            var connection = new SqlConnection();
            try
            {
                // introduce a type
                if (datasource.dsqlvep_connectiontype == DSqlVeP_DynamicSqlVirtualEntityDataSource_DSqlVeP_ConnectionType.AADConnection)
                {
                    
                    SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder();
                    csb.DataSource = datasource.dsqlvep_servername;
                    csb.InitialCatalog = datasource.dsqlvep_databasename;
                    var accessToken = GetAadToken(datasource.dsqlvep_azureadtenantid, datasource.dsqlvep_azureadclientid, datasource.dsqlvep_azureadclientsecret);
                    connection.ConnectionString = csb.ConnectionString;
                    connection.AccessToken = accessToken;
                }
                else if (datasource.dsqlvep_connectiontype == DSqlVeP_DynamicSqlVirtualEntityDataSource_DSqlVeP_ConnectionType.SQLConnection && !string.IsNullOrWhiteSpace(datasource.dsqlvep_connectionstring))
                {
                    connection.ConnectionString = datasource.dsqlvep_connectionstring;
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