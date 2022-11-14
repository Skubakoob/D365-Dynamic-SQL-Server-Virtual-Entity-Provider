using Microsoft.Xrm.Sdk;
using System;

namespace D365.VirtualEntity.DynamicSqlDataProvider.Plugins
{
    public class Retrieve : IPlugin
    {
        private string _secureConfig = null;
        private string _unsecureConfig = null;

        public Retrieve(string unsecureConfig, string secureConfig)
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
                EntityReference target = (EntityReference)context.InputParameters["Target"];
                var id = target.Id;
                var veService = new VirtualEntityService(datasource, service, tracer, context.PrimaryEntityName);
                var entity = veService.GetEntity(id);
                context.OutputParameters["BusinessEntity"] = entity;
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
