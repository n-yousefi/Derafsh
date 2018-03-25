using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Derafsh.Models.General
{
    internal class KeyValue
    {
        public string Name { get; set; }
        public object Value { get; set; }
    }

    internal class KeyType
    {
        public string Name { get; set; }
        public SqlDbType Type { get; set; }
    }
}
