using System;

namespace Derafsh.Attributes
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false)]
    public class ForeignKeyAttribute : System.Attribute
    {
        public ForeignKeyAttribute(string tableName)
        {
            TableName = tableName;
        }

        public string TableName { get; set; }
    }
}
