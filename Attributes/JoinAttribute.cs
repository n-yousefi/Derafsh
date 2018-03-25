using System;

namespace Derafsh.Attributes
{
    [AttributeUsage(AttributeTargets.Property,Inherited = false)]
    public class JoinAttribute:Attribute
    {
        public JoinAttribute(string foreignKey)
        {
            ForeignKey = foreignKey;
        }

        public string ForeignKey { get; set; }
    }
}
