using System;
using System.Collections.Generic;
using System.Linq;
using Derafsh.Models;
using Derafsh.Models.RequestModels;
using Derafsh.Models.Select;
using Derafsh.ReflectionHelpers;

namespace Derafsh.QueriesGenerator
{
    internal class SelectGenerator
    {
        internal SelectQuery GetSelectViewModel(Type type,
            QueryConditions tableConditions = null,
            FilterRequest filter =null)
        {
            var reflectionHelper = new TablesReflectionHelper();
            var root = reflectionHelper.GetReflectionTable(type);
            var query = CreateFullSelectQuery(root,tableConditions,filter);
            return query;
        }


        /// <summary>
        ///  تابع ایجاد کوئری سلکت به صورت بازگشتی
        /// </summary>
        private SelectQuery CreateFullSelectQuery(ReflectionTable node,
            QueryConditions tableConditions =null, 
            FilterRequest filter=null)
        {
            var result = new SelectQuery()
            {
                Query = "",
                Root = node
            };            
            var nodeQuery = $"select * into #{node.UniqName} from [{node.TableName}] ";
            bool hasWhere = false;
            if (tableConditions != null)
            {
                nodeQuery += "where " + tableConditions.GetConditions(node.TableName, node.Cols.Select(q => q.Name));
                hasWhere = true;
            }

            if (filter != null)
            {
                if (!string.IsNullOrEmpty(filter.SearchPhrase))
                {
                    var searchableCols=node.Cols.Where(q => q.IsSearchable)
                        .ToList();
                    if(searchableCols.Any())
                    {
                        if (!hasWhere)
                            nodeQuery += " Where ";
                        else nodeQuery += " And ";
                        nodeQuery += "(";
                        nodeQuery += String.Join(" Or ", searchableCols.Select(q =>
                            $" {q.Name} like N'%{filter.SearchPhrase}%' "
                        ));
                        nodeQuery += ")";
                    }
                }
                nodeQuery += Environment.NewLine;
                nodeQuery +=
                    $@" ORDER BY [{node.TableName}].{filter.Sort} {filter.SortDirection}
                   OFFSET {filter.PageSize} * ({filter.PageNumber} - 1) ROWS
                   FETCH NEXT {filter.PageSize} ROWS ONLY OPTION (RECOMPILE);";
            }
            nodeQuery += Environment.NewLine;
            nodeQuery += $" select * from #{node.UniqName}";
            result.Query += nodeQuery;
            var stack = new Stack<SelectStackItem>();
            if (node.JoinTables != null)
            {
                foreach (var joinTable in node.JoinTables)
                {
                    stack.Push(new SelectStackItem()
                    {
                        Table = joinTable,
                        ParentUniqName = node.UniqName,  
                        ParentPrimaryKeyName = node.PrimaryKey.Name
                    });
                }

                while (stack.Any())
                {
                    var item = stack.Pop();
                    node = item.Table.ReflectionTable;
                    nodeQuery = Environment.NewLine;
                    nodeQuery += $" select * into #{node.UniqName} from [{node.TableName}] ";
                    if (!item.Table.IsInverse)
                    {
                        nodeQuery += $" where [{item.ParentPrimaryKeyName}] in (select [{item.Table.ForeignKey}] from #{item.ParentUniqName})";
                    }
                    else
                    {
                        nodeQuery += $" where [{item.Table.ForeignKey}] in (select [{item.ParentPrimaryKeyName}] from #{item.ParentUniqName})";
                    }

                    var conditions = tableConditions?.GetConditions(node.TableName,
                        node.Cols.Select(q => q.Name));
                    if (!String.IsNullOrEmpty(conditions))
                        nodeQuery += " and " + conditions;

                    nodeQuery += Environment.NewLine;
                    nodeQuery += $" select * from #{node.UniqName}";
                    result.Query += nodeQuery;
                    if (node.JoinTables != null)
                    {
                        foreach (var joinTable in node.JoinTables)
                        {
                            stack.Push(new SelectStackItem()
                            {
                                Table = joinTable,
                                ParentUniqName = node.UniqName,   
                                ParentPrimaryKeyName = node.PrimaryKey.Name
                            });
                        }
                    }
                }
            }

            return result;
        }
        
        /// <summary>
        /// ایجاد کوئری تعداد سطر جدول
        /// </summary>
        public InsertQuery GetCountSelect(Type type, string condition)
        {
            
            var whereCondition = "";
            if (!string.IsNullOrWhiteSpace(condition))
                whereCondition = " Where " + condition;
            var query = new InsertQuery
            {
                Main = $@"Select Count(*) from [{GeneralHelper.GetTableName(type)}]
                          {whereCondition}"
            };
            return query;
        }
    }
}
