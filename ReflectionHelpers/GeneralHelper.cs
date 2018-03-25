using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using Derafsh.Attributes;

namespace Derafsh.ReflectionHelpers
{
    internal class GeneralHelper
    {
        internal static string GetTableName(Type t)
        {
            return t.GetTypeInfo().GetCustomAttribute<TableAttribute>().Name;
        }
    }
}
