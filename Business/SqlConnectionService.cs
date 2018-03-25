using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;

namespace Derafsh.Business
{
    internal class SqlConnectionService
    {
        private SqlConnection _sqlConnection;
        private readonly string _connectionString;

        public SqlConnectionService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public SqlConnection Create()
        {
            _sqlConnection = new SqlConnection(_connectionString);
            _sqlConnection.Open();

            return _sqlConnection;
        }

        public void Dispose()
        {
            if (_sqlConnection == null)
            {
                return;
            }

            _sqlConnection.Dispose();
            _sqlConnection = null;
        }
    }
}
