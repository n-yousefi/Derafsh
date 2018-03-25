using System;
using System.Linq;
using System.Reflection;
using Derafsh.Attributes;
using Derafsh.Models;
using Derafsh.Models.General;

namespace Derafsh.ReflectionHelpers
{
    internal static class PropertyReflectionHelper
    {
        internal static void SetPropertyValue(object parent, string propertyName, object propertyvalue)
        {
            PropertyInfo property = parent.GetType().GetProperty(propertyName);
            SetPropertyValue(parent, propertyvalue, property);
        }

        internal static void SetPropertyValue(object parent, object propertyvalue, PropertyInfo property)
        {
            Type t = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            object safeValue = (propertyvalue == null) ? null : Convert.ChangeType(propertyvalue, t);
            property.SetValue(parent, safeValue, null);
        }

        /// <summary>
        /// گرفتن مقدار یک پراپرتی
        /// </summary>
        internal static object GetPropValue(object src, string propName)
        {
            return src.GetType().GetProperty(propName).GetValue(src, null);
        }

        /// <summary>
        /// گرفتن مقدار کلید اصلی
        /// </summary>
        internal static KeyValue GetPrimaryKeyNameAndValue(Object src)
        {
            var primaryKey = src.GetType().GetProperties().First(
                prop => Attribute.IsDefined(prop, typeof(PrimaryKeyAttribute))
            );
            return new KeyValue()
            {
                Name = primaryKey.Name,
                Value = primaryKey.GetValue(src, null)
            };
        }


        internal static string GetPrimaryKeyName(Type type)
        {
            return type.GetProperties().First(
                prop => Attribute.IsDefined(prop, typeof(PrimaryKeyAttribute))
            ).Name;
        }

        internal static KeyType GetPrimaryKeyNameAndType(Type type)
        {
            var primaryKey = type.GetProperties().First(
                prop => Attribute.IsDefined(prop, typeof(PrimaryKeyAttribute))
            );
            var sqlType = primaryKey.GetCustomAttribute<PrimaryKeyAttribute>().SqlType;
            return new KeyType()
            {
                Name = primaryKey.Name,
                Type = sqlType
            };
        }
    }
}