using System;
using System.Data;

namespace Derafsh.Attributes
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false)]
    public class PrimaryKeyAttribute : System.Attribute
    {
        public PrimaryKeyAttribute(SqlDbType type = SqlDbType.Int)
        {
            SqlType = type;
        }
        public SqlDbType SqlType { get; }
    }
}
