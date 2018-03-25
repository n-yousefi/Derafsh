using System.Collections;
using System.Collections.Generic;
using Derafsh.Models.General;

namespace Derafsh.Models
{
    internal class ReflectionTable
    {
        internal string TableName { get; set; }
        internal string UniqName { get; set; }
        public KeyType PrimaryKey { get; set; }
        internal List<ReflectionTableCol> Cols { get; set; }
        internal IDictionary ViewModelDic { get; set; }
        internal virtual List<ReflectionJoinTable> JoinTables { get; set; }        
    }

    internal class ReflectionTableCol
    {
        internal string Name { get; set; }
        internal bool IsSearchable { get; set; }
        internal bool SelectedForAbstract { get; set; }
        internal string DisplayName { get; set; }
    }
}