using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365.VirtualEntity.DynamicSqlDataProvider.Model
{
    public enum QueryConditionOperator
    {
        // this will map to query expression - should we just use the query experssion list?
        Eq, // if multiple values, treat as in
        Ne, // not equal
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
        Like,
        BeginsWith,
        NotNull,
        Null
        
        // IsNull etc.
    }
}
