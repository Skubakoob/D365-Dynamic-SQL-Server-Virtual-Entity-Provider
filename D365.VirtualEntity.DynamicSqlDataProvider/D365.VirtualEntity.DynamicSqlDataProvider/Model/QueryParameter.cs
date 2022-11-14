using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365.VirtualEntity.DynamicSqlDataProvider.Model
{
    public class QueryParameter
    {
        public string name { get; set; }

        public object value { get; set; } // object?
        public QueryParameter()
        {

        }
        public QueryParameter(string name, object value)
        {
            this.name = name;
            this.value = value;
        }
    }
}
