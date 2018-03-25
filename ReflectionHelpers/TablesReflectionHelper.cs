using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Derafsh.Attributes;
using Derafsh.Models;

namespace Derafsh.ReflectionHelpers
{
    internal class TablesReflectionHelper
    {
        private List<string> _usedNames;
        private string GetUniqName(string prefix)
        {
            prefix = prefix.ToUpper();
            if(_usedNames==null)
                _usedNames=new List<string>();
            int i = 1;
            while (true)
            {
                if (!_usedNames.Contains(prefix + i))
                {
                    _usedNames.Add(prefix + i);
                    return prefix + i;
                }
                i++;
            }
        }

        /// <summary>
        /// گرفتن ساختار لیست پیوندی
        /// </summary>
        /// <returns>خروجی به صورت لیست پیوندی</returns>
        internal ReflectionTable GetReflectionTable(Type type)
        {
            var tableName = type.GetTypeInfo().GetCustomAttribute<TableAttribute>().Name;
            var result = new ReflectionTable()
            {
                TableName = tableName,
                PrimaryKey = PropertyReflectionHelper.GetPrimaryKeyNameAndType(type),
                UniqName = GetUniqName(tableName)                
            };

            // گرفتن پراپرتی های ویو مدل برای ساختن جدول
            var properties = type.GetProperties();
            foreach (var prop in properties)
            {
                bool notMappedProp = Attribute.IsDefined(prop, typeof(NotMappedAttribute));
                
                // اگر پراپرتی نمایشی باشد درنظر نگیر
                if (!prop.CanWrite || notMappedProp) continue;
                // اگر از نوع جوین نباشد نامش را بر میداریم 
                var joinProp = prop.GetCustomAttribute<JoinAttribute>();
                var invertAttr = prop.GetCustomAttribute<InverseJoinAttribute>();
                var selectedForAbstract = Attribute.IsDefined(prop, typeof(AbstractAttribute));
                if (joinProp==null && invertAttr == null) // اگر صفت عادی باشد
                {
                    if (result.Cols == null)
                        result.Cols = new List<ReflectionTableCol>();
                    result.Cols.Add(new ReflectionTableCol()
                    {
                        Name=prop.Name,
                        IsSearchable = Attribute.IsDefined(prop, typeof(SearchableAttribute)),
                        SelectedForAbstract = selectedForAbstract,
                        DisplayName = prop.GetCustomAttribute<DisplayAttribute>()?.Name
                    });
                }
                else // اگر جوین یا معکوس جوین باشد
                {
                    if (result.JoinTables == null)
                        result.JoinTables = new List<ReflectionJoinTable>();

                    if (invertAttr != null)
                    {
                        // چک میکنیم که آیا یک لیست است
                        var isOntoMany = typeof(IEnumerable).IsAssignableFrom(prop.PropertyType);
                        // اگر لیست بود نوع داخل لیست را می فرستیم 
                        var propType = isOntoMany
                            ? prop.PropertyType.GetGenericArguments()[0]
                            : prop.PropertyType;
                        var childResult = GetReflectionTable(propType);                        
                        var joinTable = new ReflectionJoinTable
                        {
                            ForeignKey = invertAttr.ForeignKey,
                            ReflectionTable = childResult,
                            IsInverse = true,                            
                            IsOneToMany = isOntoMany,
                            Property = prop,
                            SelectedForAbstract = selectedForAbstract
                        };

                        result.JoinTables.Add(joinTable);
                    }
                    else // برای جوین ها
                    {                        
                        var childResult = GetReflectionTable(prop.PropertyType);                        
                        var joinTable = new ReflectionJoinTable()
                        {
                            ForeignKey = joinProp.ForeignKey,
                            ReflectionTable = childResult,                           
                            Property = prop,
                            SelectedForAbstract = selectedForAbstract
                        };
                        result.JoinTables.Add(joinTable);
                    }
                }
            }

            return result;
        }

    }
}
