namespace Derafsh.Models.Abstract
{
    internal class AbstractQuery
    {
        public string SelectTerm { get; set; }
        public string JoinTerm { get; set; }
        public string WhereTerm { get; set; }
        public string OrderByTerm { get; set; }

        internal ReflectionTable Root { get; set; }
    }
}
