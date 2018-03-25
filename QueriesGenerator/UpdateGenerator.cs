using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Derafsh.Models;
using Derafsh.ReflectionHelpers;

namespace Derafsh.QueriesGenerator
{
    internal class UpdateGenerator
    {
        internal int LastParamId { get; set; } = 1;
        internal RawQuery GetUpdateQuery(ReflectionTable node,object viewModel)
        {            
            var result = CreateSingleUpdateQuery(node, viewModel);                            
            if (result !=null && node.JoinTables != null)
            {
                // بچه هایی که من به آن ها نیاز دارم
                foreach (var joinTable in node.JoinTables)
                {
                    var propObj = joinTable.Property.GetValue(viewModel, null);
                    if (propObj != null)
                    {
                        var childResult = GetUpdateQuery(joinTable.ReflectionTable,
                            propObj);
                        if (childResult == null)
                        {
                            result = null;
                            break;                            
                        }
                        // تجمیع بجه هایی که من به آن ها نیاز دارم
                        result.Query += Environment.NewLine + childResult.Query;
                        result.Params.AddRange(childResult.Params);
                    }
                }
            }            
            return result;
        }

        internal RawQuery CreateSingleUpdateQuery(ReflectionTable node,
            Object viewModel)
        {
            var result = new RawQuery()
            {
                Params = new List<SqlParameter>()
            };
            var cols = node.Cols.Where(
                    q => !string.Equals(q.Name, "Id",StringComparison.CurrentCultureIgnoreCase))
                .Select(q => q.Name).ToList();
            string query = $@" update [{node.TableName}] Set ";
            foreach (var col in cols)
            {
                var parname = "Param" + LastParamId++;
                query += $" {col}=@{parname},";
                var paraval = PropertyReflectionHelper
                    .GetPropValue(viewModel, col);
                var sqlParam = new SqlParameter(parname, paraval);
                if (paraval == null)
                    sqlParam.SqlValue = DBNull.Value;
                result.Params.Add(sqlParam);
            }

            query = query.Substring(0, query.Length - 1);
            query += Environment.NewLine;

            var id = PropertyReflectionHelper
                .GetPropValue(viewModel, "Id");
            var idParname = "Id" + LastParamId++;
            query += "Where id= @" + idParname + ";";
            var idParam = new SqlParameter(idParname, id);
            if (id == null || (id is int && (int) id == 0))
                result = null;
            else
            {
                result.Params.Add(idParam);
                result.Query = query;
            }

            return result;
        }
    }
}
