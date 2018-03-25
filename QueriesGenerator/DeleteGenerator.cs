using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Derafsh.Models;
using Derafsh.ReflectionHelpers;

namespace Derafsh.QueriesGenerator
{
    internal class DeleteGenerator
    {
        internal InsertQuery GetLogicalDeleteQuery(ReflectionTable reflectionTable, object viewModel, Type type)
        {
            var key = PropertyReflectionHelper.GetPrimaryKeyNameAndValue(viewModel);
            var query = new InsertQuery
            {
                Main = $"Update [{reflectionTable.TableName}] " +
                       $"Set IsDeleted = 1  Where {key.Name}= @key",
                Params = new List<SqlParameter>()
                {
                    new SqlParameter("key", key.Value)
                }
            };
            return query;
        }


        internal InsertQuery GetLogicalDeleteQuery(Type type, object keyValue)
        {
            var keyName = PropertyReflectionHelper.GetPrimaryKeyName(type);
            var tableName = GeneralHelper.GetTableName(type);
            var query = new InsertQuery
            {
                Main = $"Update [{tableName}] Set IsDeleted = 0  Where {keyName}= @key",
                Params = new List<SqlParameter>()
                {
                    new SqlParameter("key", keyValue)
                }
            };
            return query;
        }
    }
}
