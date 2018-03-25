using System.Collections.Generic;
using System.Data.SqlClient;

namespace Derafsh.Models
{
    internal class RawQuery
    {
        internal string Query { get; set; }
        internal List<SqlParameter> Params { get; set; }
    }
}
