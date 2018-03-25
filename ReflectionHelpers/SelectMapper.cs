using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using Derafsh.Business;
using Derafsh.Models;
using Derafsh.Models.Select;

namespace Derafsh.ReflectionHelpers
{
    internal class SelectMapper
    {
        internal IEnumerable<T> GetSelectObject<T>(SelectQuery query)
        {
            var type = typeof(T);
            var result = query.Root.ViewModelDic.Values.Cast<T>();
            try
            {
                var stack = new Stack<ReflectionJoinTable>();
                if (query.Root.JoinTables != null)
                {
                    AssignPropertyViewModels(query.Root.JoinTables, query.Root.ViewModelDic);
                    foreach (var joinTable in query.Root.JoinTables)
                    {
                        stack.Push(joinTable);
                    }
                }

                while (stack.Any())
                {
                    var item = stack.Pop();
                    if (item.ReflectionTable.JoinTables != null)
                    {
                        AssignPropertyViewModels(item.ReflectionTable.JoinTables,
                            item.ReflectionTable.ViewModelDic);
                        foreach (var joinTable in item.ReflectionTable.JoinTables)
                        {
                            stack.Push(joinTable);
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
            return result;
        }

        private void AssignPropertyViewModels(List<ReflectionJoinTable> joinTables,
            IDictionary viewModelDic)
        {
            foreach (var joinTable in joinTables)
            {
                foreach (DictionaryEntry row in viewModelDic)
                {
                    if (joinTable.IsInverse)
                    {
                        var id = row.Key;
                        var value = joinTable.ReflectionTable.ViewModelDic[id];
                        PropertyReflectionHelper.SetPropertyValue(row.Value, value,
                            joinTable.Property);
                    }
                    else
                    {
                        var isOntoMany = row.Value is IEnumerable;
                        if (!isOntoMany)
                        {
                            var id = PropertyReflectionHelper.GetPropValue(row.Value, joinTable.ForeignKey);
                            if (id != null)
                            {
                                var value = joinTable.ReflectionTable.ViewModelDic[id];
                                PropertyReflectionHelper.SetPropertyValue(row.Value, value,
                                    joinTable.Property);
                            }
                        }
                        else
                        {
                            var obj = (IList)row.Value;
                            foreach (var instance in obj)
                            {
                                var id = PropertyReflectionHelper.GetPropValue(instance, joinTable.ForeignKey);
                                if (id != null)
                                {
                                    var value = joinTable.ReflectionTable.ViewModelDic[id];
                                    PropertyReflectionHelper.SetPropertyValue(instance, value,
                                        joinTable.Property);
                                }
                            }

                        }
                    }


                }
            }
        }


        internal void ReflectionTableViewModelsMapping(SqlConnectionService connectionService, SelectQuery query, Type type, SqlTransaction transaction = null)
        {
            var dataTables = MultipleSelectQuery(connectionService, query.Query, transaction);
            var viewModel = GetDataTableReflectedList(type, dataTables);
            query.Root.ViewModelDic = new Dictionary<object, object>();
            foreach (var row in viewModel)
            {
                var key = PropertyReflectionHelper.GetPrimaryKeyNameAndValue(row);
                query.Root.ViewModelDic.Add(key.Value, row);
            }

            var stack = new Stack<SelectStackItem>();
            if (query.Root.JoinTables != null)
                foreach (var joinTable in query.Root.JoinTables)
                {
                    stack.Push(new SelectStackItem()
                    {
                        Table = joinTable
                    });
                }

            while (stack.Any())
            {
                var item = stack.Pop();
                Type childType = item.Table.Property.PropertyType;
                if (item.Table.IsOneToMany)
                {
                    childType = childType.GetGenericArguments()[0];
                }

                viewModel = GetDataTableReflectedList(childType, dataTables);
                item.Table.ReflectionTable.ViewModelDic = new Dictionary<object, object>();
                var dic = item.Table.ReflectionTable.ViewModelDic;
                foreach (var row in viewModel)
                {
                    if (!item.Table.IsInverse)
                    {
                        var key = PropertyReflectionHelper.GetPrimaryKeyNameAndValue(row);
                        dic.Add(key.Value, row);
                    }
                    else
                    {
                        var id = PropertyReflectionHelper.GetPropValue(row, item.Table.ForeignKey);
                        if (!item.Table.IsOneToMany)
                        {
                            if (!dic.Contains(id))
                                dic.Add(id, row);
                        }
                        else
                        {
                            if (!dic.Contains(id))
                            {
                                var list =
                                    (IList)Activator.CreateInstance(
                                        item.Table.Property.PropertyType);
                                list.Add(row);
                                dic.Add(id, list);
                            }
                            else
                            {
                                var list = (IList)dic[id];
                                list.Add(row);
                            }
                        }
                    }
                }

                if (item.Table.ReflectionTable.JoinTables != null)
                    foreach (var joinTable in item.Table.ReflectionTable.JoinTables)
                    {
                        stack.Push(new SelectStackItem()
                        {
                            Table = joinTable
                        });
                    }
            }
        }

        private IList GetDataTableReflectedList(Type type, Queue<DataTable> dataTables)
        {
            var dataTable = dataTables.Dequeue();
            var methods = typeof(DataTableReflectionHelper).GetMethods();
            var readMethod = methods.Single(m => m.Name == "ConvertToList");
            MethodInfo generic = readMethod.MakeGenericMethod(type);
            var viewModel = generic.Invoke(null, new object[] { dataTable });
            return (IList)viewModel;
        }

        private Queue<DataTable> MultipleSelectQuery(SqlConnectionService connectionService,
            string query,
            SqlTransaction transaction)
        {
            Queue<DataTable> result;
            using (var dataSet = new DataSet())
            {
                using (var connection = connectionService.Create())
                {
                    using (var sqlCommand = connection.CreateCommand())
                    {
                        sqlCommand.CommandText = query;
                        sqlCommand.Transaction = transaction;
                        using (var adapter = new SqlDataAdapter(sqlCommand))
                        {
                            adapter.Fill(dataSet);
                        }
                    }
                }
                result = new Queue<DataTable>();
                foreach (DataTable table in dataSet.Tables)
                {
                    result.Enqueue(table);
                }
            }
            return result;
        }
    }
}
