using System;
using System.Collections.Generic;
using System.Linq;
using Derafsh.Models;
using Derafsh.Models.Abstract;
using Derafsh.Models.RequestModels;
using Derafsh.Models.Select;
using Derafsh.ReflectionHelpers;

namespace Derafsh.QueriesGenerator
{
    internal class AbstractGenerator
    {
        internal string GetAbstractQuery(Type type,
            string whereTerm = null,
            FilterRequest filter = null)
        {
            var reflectionHelper = new TablesReflectionHelper();
            var node = reflectionHelper.GetReflectionTable(type);
            var result = new AbstractQuery()
            {
                SelectTerm = GetSelectTerm($"[{node.TableName}].[{node.PrimaryKey.Name}]",node.TableName, node.Cols),
                Root = node,
                WhereTerm = whereTerm
            };
            (result.WhereTerm, result.OrderByTerm) =
                GetOrderByTerm(result.WhereTerm, filter, node.TableName, node.Cols);
            node.UniqName = node.TableName;
            var que = new Queue<SelectStackItem>();
            if (node.JoinTables != null)
            {
                foreach (var joinTable in node.JoinTables
                    .Where(q=>q.SelectedForAbstract))
                {
                    if (!joinTable.IsOneToMany && !joinTable.IsInverse)
                        que.Enqueue(new SelectStackItem()
                        {
                            Table = joinTable,
                            ParentUniqName = node.UniqName                            
                        });
                }

                while (que.Any())
                {
                    var item = que.Dequeue();
                    node = item.Table.ReflectionTable;
                    // سلمت
                    result.SelectTerm = GetSelectTerm(result.SelectTerm,node.UniqName, 
                        node.Cols);
                    // جوین ها
                    result.JoinTerm += Environment.NewLine +
                        $" left join [{node.TableName}] as {node.UniqName} " +
                        $"on [{item.ParentUniqName}].[{item.Table.ForeignKey}] = [{node.UniqName}].{node.PrimaryKey.Name} ";

                    // فیلتر
                    if (filter!=null && string.IsNullOrEmpty(result.OrderByTerm))
                    {
                        (result.WhereTerm, result.OrderByTerm) =
                            GetOrderByTerm(result.WhereTerm, filter, node.UniqName, node.Cols);
                    }
                    
                    // زیر مجموعه
                    if (node.JoinTables != null)
                    {
                        foreach (var joinTable in node.JoinTables
                            .Where(q => q.SelectedForAbstract))
                        {
                            if (!joinTable.IsOneToMany && !joinTable.IsInverse)
                                que.Enqueue(new SelectStackItem()
                                {
                                    Table = joinTable,
                                    ParentUniqName = node.UniqName                                    
                                });
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(result.WhereTerm))
                result.WhereTerm = "Where " + result.WhereTerm;
            var nodeQuery = $@"select {result.SelectTerm} 
                               from [{result.Root.TableName}] 
                               {result.JoinTerm}
                               {result.WhereTerm}
                               {result.OrderByTerm}";
            
            nodeQuery += Environment.NewLine;

            return nodeQuery;
        }


        private (string,string) GetOrderByTerm(string prevWhereTerm,FilterRequest filter,
            string tableName, List<ReflectionTableCol> cols)
        {
            string whereTerm = prevWhereTerm;
            string orderByTerm = "";
            if (filter != null)
            {
                if (!string.IsNullOrEmpty(filter.SearchPhrase))
                {
                    var searchableCols = cols.Where(q => q.IsSearchable && q.SelectedForAbstract)
                        .ToList();
                    if (searchableCols.Any())
                    {
                        if (!string.IsNullOrEmpty(prevWhereTerm))
                            whereTerm += " And ";
                        whereTerm += "(";
                        whereTerm += string.Join(" Or ", searchableCols.Select(q =>
                            $" [{q.Name}] like N'%{filter.SearchPhrase}%' "
                        ));
                        whereTerm += ")";
                    }
                }

                if (cols.Any(q =>
                    String.Equals(q.Name, filter.Sort,
                        StringComparison.CurrentCultureIgnoreCase)))
                    orderByTerm +=
                        $@" ORDER BY [{tableName}].{filter.Sort} {filter.SortDirection}
                   OFFSET {filter.PageSize} * ({filter.PageNumber} - 1) ROWS
                   FETCH NEXT {filter.PageSize} ROWS ONLY OPTION (RECOMPILE);";
            }

            return (whereTerm, orderByTerm);
        }
        
        private string GetSelectTerm(string prevTerm,string tableName,List<ReflectionTableCol> cols)
        {
            var result = prevTerm;
            var term = string.Join(",", cols
                .Where(q => q.SelectedForAbstract)
                .Select(q => $"[{tableName}].[{q.Name}] as [{(string.IsNullOrEmpty(q.DisplayName)?q.Name:q.DisplayName)}]"));
            if (!string.IsNullOrEmpty(term))
            {
                result += ","+ term;
            }

            return result;
        }
    }
}
