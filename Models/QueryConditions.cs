using System.Collections.Generic;
using System.Linq;

namespace Derafsh.Models
{
    public class QueryConditions
    {
        private readonly Dictionary<string, string> _publicConditionsColNameCondition;
        private readonly Dictionary<string, string> _spaceficConditionsTableNameCondition;
        public QueryConditions()
        {
            _publicConditionsColNameCondition = new Dictionary<string, string>();
            _spaceficConditionsTableNameCondition = new Dictionary<string, string>();
        }

        public string GetConditions(string tableName, IEnumerable<string> cols)
        {
            cols = cols.Select(q => q.ToLower());
            string condition = "";
            if (_spaceficConditionsTableNameCondition.ContainsKey(tableName))
                condition = _spaceficConditionsTableNameCondition[tableName];
            if (_publicConditionsColNameCondition.Count > 0)
            {
                if (!string.IsNullOrWhiteSpace(condition))
                {
                    condition += " and ";
                }
                condition += string.Join(" and ", _publicConditionsColNameCondition
                    .Where(q => cols.Contains(q.Key.ToLower())).Select(q => q.Value));
            }
            return condition;
        }

        public void AddCondition(string tableName, string condition)
        {
            _spaceficConditionsTableNameCondition.Add(tableName,condition);
        }
        public void AddPublicCondition(string colName, string condition)
        {
            _publicConditionsColNameCondition.Add(colName, condition);
        }
    }
}
