using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365.VirtualEntity.DynamicSqlDataProvider.Model
{
    public class QueryResponse
    {
        public List<MappedEntity> MappedEntities = new List<MappedEntity>();
        public bool HasMoreRecords = false;
    }
}
