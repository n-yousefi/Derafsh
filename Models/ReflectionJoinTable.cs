using System.Reflection;

namespace Derafsh.Models
{
    internal class ReflectionJoinTable
    {
        public string ForeignKey { get; set; }
        internal virtual ReflectionTable ReflectionTable { get; set; }
        internal PropertyInfo Property { get; set; }
        internal bool IsOneToMany { get; set; }
        internal bool IsInverse { get; set; }
        internal bool SelectedForAbstract { get; set; }
    }
}
