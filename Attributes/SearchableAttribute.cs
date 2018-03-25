using System;

namespace Derafsh.Attributes
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false)]
    public class SearchableAttribute : System.Attribute
    {
        public SearchableAttribute()
        {
            
        }
    }
}
