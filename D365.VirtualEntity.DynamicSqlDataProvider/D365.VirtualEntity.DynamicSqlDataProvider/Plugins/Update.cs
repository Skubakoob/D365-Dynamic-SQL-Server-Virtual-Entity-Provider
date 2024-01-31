using D365.VirtualEntity.DynamicSqlDataProvider.Services;
using Microsoft.Xrm.Sdk;
using System;

namespace D365.VirtualEntity.DynamicSqlDataProvider.Plugins
{
    public class Update : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            var retriever = (IEntityDataSourceRetrieverService)serviceProvider.GetService(typeof(IEntityDataSourceRetrieverService));
            var datasource = retriever.RetrieveEntityDataSource();

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                try
                {
                    Entity entity = (Entity)context.InputParameters["Target"];
                    var veService = new VirtualEntityService(datasource, service, tracer, context);
                    veService.UpdateEntity(entity);
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
}
