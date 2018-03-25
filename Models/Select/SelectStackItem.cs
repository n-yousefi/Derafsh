namespace Derafsh.Models.Select
{
    internal class SelectStackItem
    {
        public ReflectionJoinTable Table { get; set; }
        public string ParentUniqName { get; set; }        
        public string ParentPrimaryKeyName { get; set; }
    }
}
