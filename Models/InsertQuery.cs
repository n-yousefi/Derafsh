using System.Collections.Generic;
using System.Data.SqlClient;

namespace Derafsh.Models
{
    internal class InsertQuery
    {
        internal string Pre { get; set; }
        internal string Main { get; set; }
        internal string Post { get; set; }
        internal List<SqlParameter> Params { get; set; }
        internal string TempTableName { get; set; }
    }
}
