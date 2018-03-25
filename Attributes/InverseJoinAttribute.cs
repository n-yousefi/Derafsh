using System;

namespace Derafsh.Attributes
{
    [AttributeUsage(AttributeTargets.Property,Inherited = false)]
    public class InverseJoinAttribute : Attribute
    {
        public InverseJoinAttribute(string foreignKey)
        {
            ForeignKey = foreignKey;
        }

        public string ForeignKey { get; set; }
    }
}
