using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Derafsh.Models;
using Derafsh.ReflectionHelpers;

namespace Derafsh.QueriesGenerator
{
    internal class InsertGenerator
    {
        internal int LastParamId { get; set; }
        internal RawQuery GetInsertQueryAndParameters(
            ReflectionTable reflectionTable,
            object viewModel, Type type)
        {
            LastParamId = 1;
            var query = CreateFullInserQuery(reflectionTable, viewModel);
            return new RawQuery()
            {
                Query = query.Pre + query.Main + query.Post,
                Params = query.Params
            };
        }


        /// <summary>
        ///  تابع ایجاد کوئری اینزرت به صورت بازگشتی
        /// </summary>
        private InsertQuery CreateFullInserQuery(ReflectionTable node,
            Object viewModel,
            Tuple<string, string> parentTempTable = null,
            bool isReverce = false)
        {
            var result = new InsertQuery()
            {
                Params = new List<SqlParameter>()
            };
            // جدول های موقت موردنیاز ما
            var joinPropTables = new Dictionary<string, string>();
            // اگر وابسته به پدر باشیم جدول موقت پدر 
            //را به جدول های موقت مورد نیازمان اضافه می کنیم
            if (parentTempTable != null)
                joinPropTables.Add(parentTempTable.Item1, parentTempTable.Item2);
            if (node.JoinTables != null)
            {
                // بچه هایی که من به آن ها نیاز دارم
                foreach (var joinTable in
                    node.JoinTables.Where(q => !q.IsInverse && !q.IsOneToMany))
                {
                    var propObj = joinTable.Property.GetValue(viewModel, null);
                    if (propObj != null)
                    {
                        var childResult = CreateFullInserQuery(joinTable.ReflectionTable,
                            propObj, isReverce: true);
                        // نام جدول موقت این بچه
                        joinPropTables.Add(joinTable.ForeignKey, childResult.TempTableName);
                        // تجمیع بجه هایی که من به آن ها نیاز دارم
                        result.Pre += childResult.Pre;
                        result.Main += childResult.Main;
                        result.Post = childResult.Post + result.Post;
                        result.Params.AddRange(childResult.Params);
                    }
                }
            }
            // بچه هایی که به من نیاز دارند
            var childrenNeesMe = node.JoinTables?.Where(q => q.IsInverse || q.IsOneToMany)
                .ToList();
            bool oneChilNeedsMe = childrenNeesMe != null && childrenNeesMe.Any();
            var lastParamId = LastParamId;
            // ایجاد کوئری من
            var me = CreateSingleInsertQuery(node, viewModel, joinPropTables,
                    oneChilNeedsMe || isReverce, ref lastParamId);
            LastParamId = lastParamId;
            if (oneChilNeedsMe)
            {
                // بچه هایی که به من نیاز دارند
                foreach (var joinTable in childrenNeesMe)
                {
                    var myTempProTable = new Tuple<string, string>
                    (
                        joinTable.ForeignKey,
                        me.TempTableName
                    );
                    var propObj = joinTable.Property.GetValue(viewModel, null);
                    if (propObj != null)
                    {
                        var childResult = new InsertQuery()
                        {
                            Params = new List<SqlParameter>()
                        };
                        if (joinTable.IsOneToMany)
                        {
                            var list = (IList)propObj;
                            foreach (var obj in list)
                            {
                                var cr = CreateFullInserQuery(joinTable.ReflectionTable,
                                    obj, myTempProTable);
                                childResult.Main = cr.Pre + cr.Main + cr.Post;
                                childResult.Params.AddRange(cr.Params);
                            }
                        }
                        else
                        {
                            childResult = CreateFullInserQuery(joinTable.ReflectionTable,
                                propObj, myTempProTable);
                        }

                        // تجمیع من با بچه هایی که به من نیاز دارند 
                        me.Main += childResult.Pre + childResult.Main + childResult.Post;
                        result.Params.AddRange(childResult.Params);

                    }
                }
            }
            // تجمیع خودم
            if (isReverce)
            {
                result.Pre += me.Pre;
                result.Main += me.Main;
                result.Post = me.Post + result.Post;
            }
            else
            {
                result.Main += me.Pre + me.Main + me.Post;
            }

            result.Params.AddRange(me.Params);

            result.TempTableName = me.TempTableName;
            return result;
        }

        internal InsertQuery CreateSingleInsertQuery(ReflectionTable reflectionTable,
            Object viewModel,
            Dictionary<string, string> joinPropTable,
            bool storeIdInTempTable,
            ref int lastParamId)
        {
            var cols = reflectionTable.Cols
                .Where(q => q.Name != reflectionTable.PrimaryKey.Name)
                .ToArray();
            var result = new InsertQuery()
            {
                Params = new List<SqlParameter>()
            };
            var outPut = "";
            var varSet = "";
            if (storeIdInTempTable)
            {
                result.TempTableName = reflectionTable.TableName + lastParamId;
                result.Pre = $@"declare @{result.TempTableName}Scalar {reflectionTable.PrimaryKey.Type.ToString()}
                 create table #{result.TempTableName} ([{reflectionTable.PrimaryKey.Name}] {reflectionTable.PrimaryKey.Type.ToString()}){Environment.NewLine}";

                outPut += $@"output inserted.[{reflectionTable.PrimaryKey.Name}] into #{result.TempTableName}{Environment.NewLine}";
                varSet = $@"SELECT @{result.TempTableName}Scalar = [{reflectionTable.PrimaryKey.Name}] from #{result.TempTableName}
                            drop table #{result.TempTableName}{Environment.NewLine}";
            }

            string query = $@"insert into [{reflectionTable.TableName}] ";
            string queryVars = string.Join(",", cols.Select(q=>q.Name));
            string queryValues = "";
            foreach (var col in cols)
            {
                if (joinPropTable.ContainsKey(col.Name))
                {
                    queryValues += "@" + joinPropTable[col.Name] + "Scalar,";
                }
                else
                {
                    lastParamId++;
                    var parname = "Param" + lastParamId;
                    queryValues += "@" + parname + ",";
                    var paraval = PropertyReflectionHelper.GetPropValue(viewModel, col.Name);
                    var sqlParam = new SqlParameter(parname,paraval);
                    if (paraval == null)
                        sqlParam.SqlValue = DBNull.Value;
                    result.Params.Add(sqlParam);

                }
            }
            queryValues = queryValues.Substring(0, queryValues.Length - 1);
            query += $@"({queryVars})
                        {outPut}
                        values ({queryValues})
                        {varSet}{Environment.NewLine}";

            result.Main = query;
            return result;
        }
    }
}
