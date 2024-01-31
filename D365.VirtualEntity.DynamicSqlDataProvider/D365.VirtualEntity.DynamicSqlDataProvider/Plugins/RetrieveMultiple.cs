using D365.VirtualEntity.DynamicSqlDataProvider.Services;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Extensions;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace D365.VirtualEntity.DynamicSqlDataProvider.Plugins
{
    public class RetrieveMultiple : IPlugin
    {
        private string _secureConfig = null;
        private string _unsecureConfig = null;

        public RetrieveMultiple(string unsecureConfig, string secureConfig)
        {
            _secureConfig = secureConfig;
            _unsecureConfig = unsecureConfig;
        }

        public void Execute(IServiceProvider serviceProvider)
        {

            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            var retriever = (IEntityDataSourceRetrieverService)serviceProvider.GetService(typeof(IEntityDataSourceRetrieverService));
            var datasource = retriever.RetrieveEntityDataSource();

            try
            {

                var veService = new VirtualEntityService(datasource, service, tracer, context);

                QueryExpression queryExpression = context.InputParameterOrDefault<QueryExpression>("Query");
                var visitor = new SearchVisitor();
                queryExpression.Accept(visitor);
                var results = veService.GetEntities(queryExpression);
                context.OutputParameters["BusinessEntityCollection"] = results;
            }
            catch (Exception e)
            {
                tracer.Trace($"{e.Message} {e.StackTrace}");
                if (e.InnerException != null)
                    tracer.Trace($"{e.InnerException.Message} {e.InnerException.StackTrace}");

                throw new InvalidPluginExecutionException(e.Message);
            }
        }
    }
}
