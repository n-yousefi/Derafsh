using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;

namespace Derafsh.ReflectionHelpers
{
    internal class DataTableReflectionHelper
    {
        public static List<T> ConvertToList<T>(DataTable dt)
        {
            List<T> data = new List<T>();
            foreach (DataRow row in dt.Rows)
            {
                T item = GetItem<T>(row);
                data.Add(item);
            }
            return data;
        }
        internal static T GetItem<T>(DataRow dr)
        {
            Type temp = typeof(T);
            T obj = Activator.CreateInstance<T>();

            foreach (DataColumn column in dr.Table.Columns)
            {
                foreach (PropertyInfo pro in temp.GetProperties())
                {
                    if (pro.Name == column.ColumnName)
                    {
                        var val = dr[column.ColumnName] != DBNull.Value ? dr[column.ColumnName] : null;
                        pro.SetValue(obj, val, null);                        
                    }
                    else
                        continue;
                }
            }
            return obj;
        }
    }
}
