using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Derafsh.Models;
using Derafsh.Models.RequestModels;
using Derafsh.QueriesGenerator;
using Derafsh.ReflectionHelpers;

namespace Derafsh.Business
{
    public interface IDatabaseActions
    {
        /// <summary>
        /// گرفتن کوئری اینزرت به همراه تمام پارامترهایش
        /// </summary>
        Task<int> Insert<T>(object viewModel, CancellationToken cancellationToken,
            SqlTransaction transaction = null);

        /// <summary>
        /// پر کردن یک ویو مدل بر اساس رابطه هایش
        /// </summary>
        Task<IEnumerable<T>> Select<T>(QueryConditions queryConditions = null,
            FilterRequest filter = null, SqlTransaction transaction = null);

        Task<DataTable> Abstract<T>(string whereTerm = null,
            FilterRequest filterRequest = null, SqlTransaction transaction = null);

        /// <summary>
        /// ویرایش یک جدول با حذف منطقی سطر خودش و اضافه کردن سطرهای جدید دیگر
        /// </summary>
        Task<int> Update<T>( object viewModel,
            CancellationToken cancellationToken, SqlTransaction transaction = null);

        /// <summary>
        /// ویرایش یک جدول با حذف منطقی سطر خودش و اضافه کردن سطرهای جدید دیگر
        /// </summary>
        Task<int> UpdateByParentSoftDelete<T>(CancellationToken cancellationToken,
            object viewModel);

        /// <summary>
        /// حذف منطقی یک جدول
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="id"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        Task<int> SoftDelete<T>(CancellationToken cancellationToken, int id,
            SqlTransaction transaction = null);

        /// <summary>
        /// گرفتن تعداد سطرهای یک جدول
        /// </summary>
        Task<int> Count<T>(CancellationToken cancellationToken,
            string condition = "", SqlTransaction transaction = null);

        /// <summary>
        /// یافتن یک شیء با آی دی
        /// </summary>
        Task<T> Find<T>(int id, QueryConditions queryConditions = null,
            SqlTransaction transaction = null);

    }
    public class DatabaseActions : IDatabaseActions
    {
        private readonly SqlConnectionService _connectionService;
        public DatabaseActions(string connectionString)
        {
            _connectionService = new SqlConnectionService(connectionString);
        }
        public async Task<int> Insert<T>(object viewModel,
            CancellationToken cancellationToken, SqlTransaction transaction = null)
        {
            var type = typeof(T);
            var reflectionTable = await
                Task.Run(() => new TablesReflectionHelper()
                    .GetReflectionTable(typeof(T)), cancellationToken);
            return await Insert(viewModel, type, cancellationToken, reflectionTable, transaction);
        }

        private async Task<int> Insert(object viewModel, Type type,
            CancellationToken cancellationToken, ReflectionTable reflectionTable,
            SqlTransaction transaction = null)
        {
            var insertGenerator = new InsertGenerator();
            var query = insertGenerator.GetInsertQueryAndParameters(reflectionTable,
                viewModel, type);
            int affectedRowsCount;
            using (var conection = _connectionService.Create())
            {
                using (SqlCommand cmd = conection.CreateCommand())
                {
                    cmd.CommandText = query.Query;
                    cmd.Transaction = transaction;
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.AddRange(query.Params.ToArray());
                    affectedRowsCount = await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            return affectedRowsCount;
        }

        public async Task<IEnumerable<T>> Select<T>(QueryConditions queryConditions = null,
            FilterRequest filter = null, SqlTransaction transaction = null)
        {
            IEnumerable<T> result = null;
            var type = typeof(T);
            var queryGenerator = new SelectGenerator();
            var query = queryGenerator.GetSelectViewModel(type, queryConditions, filter);
            var mapper = new SelectMapper();
            await Task.Run(() =>
            {
                mapper.ReflectionTableViewModelsMapping(_connectionService,
                    query, type, transaction);
                result = mapper.GetSelectObject<T>(query);
            });
            return result;
        }

        public async Task<DataTable> Abstract<T>(string whereTerm = null, FilterRequest filter = null,
            SqlTransaction transaction = null)
        {
            var result = new DataTable();
            var type = typeof(T);
            await Task.Run(() =>
            {
                var query = new AbstractGenerator().GetAbstractQuery(type, whereTerm, filter);
                using (var connection = _connectionService.Create())
                {
                    using (SqlDataAdapter adp = new SqlDataAdapter(query, connection))
                    {
                        adp.Fill(result);
                    }
                }
            });
            return result;
        }

        public async Task<int> Update<T>(object viewModel,
            CancellationToken cancellationToken, SqlTransaction transaction = null)
        {
            var reflectionTable = await
                Task.Run(() => new TablesReflectionHelper()
                    .GetReflectionTable(typeof(T)), cancellationToken);
            var query = new UpdateGenerator()
                .GetUpdateQuery(reflectionTable, viewModel);
            int affectedRowsCount = 0;
            using (var connection = _connectionService.Create())
            {
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = query.Query;
                    cmd.Transaction = transaction;
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.AddRange(query.Params.ToArray());
                    affectedRowsCount = await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            return affectedRowsCount;
        }

        public async Task<int> UpdateByParentSoftDelete<T>(CancellationToken cancellationToken,
            object viewModel)
        {
            Type type = typeof(T);
            int result = 0;
            using (var scope = new TransactionScope())
            {
                var reflectionTable = await
                    Task.Run(() => new TablesReflectionHelper()
                        .GetReflectionTable(typeof(T)), cancellationToken);
                int deleteResult = 0;
                using (var connection = _connectionService.Create())
                {
                    deleteResult = await SoftDelete(cancellationToken,
                         viewModel, type, reflectionTable);
                }
                using (var connection = _connectionService.Create())
                {
                    if (deleteResult > 0)
                    {
                        result = await Insert(viewModel, type, cancellationToken, reflectionTable);
                    }
                }


                scope.Complete();
            }
            return result;
        }

        public async Task<int> SoftDelete<T>(CancellationToken cancellationToken,
            int id, SqlTransaction transaction = null)
        {
            var type = typeof(T);
            var query = new DeleteGenerator().GetLogicalDeleteQuery(type, id);
            int affectedRowsCount = 0;
            using (var connection = _connectionService.Create())
            {
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = query.Main;
                    cmd.Transaction = transaction;
                    affectedRowsCount = await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            return affectedRowsCount;
        }

        private async Task<int> SoftDelete(CancellationToken cancellationToken,
            object viewModel, Type type, ReflectionTable reflectionTable, SqlTransaction transaction = null)
        {
            var query = new DeleteGenerator()
                .GetLogicalDeleteQuery(reflectionTable, viewModel, type);
            int affectedRowsCount = 0;
            using (var connection = _connectionService.Create())
            {
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = query.Main;
                    cmd.Transaction = transaction;
                    cmd.Parameters.AddRange(query.Params.ToArray());
                    affectedRowsCount = await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            return affectedRowsCount;
        }

        public async Task<int> Count<T>(CancellationToken cancellationToken,
            string condition = "", SqlTransaction transaction = null)
        {
            var type = typeof(T);
            var selectGenerator = new SelectGenerator();
            var query = selectGenerator.GetCountSelect(type, condition);
            int result;
            using (var connection = _connectionService.Create())
            {
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = query.Main;
                    cmd.Transaction = transaction;
                    result = (Int32)await cmd.ExecuteScalarAsync(cancellationToken);
                }
            }

            return result;
        }

        public async Task<T> Find<T>(int id, QueryConditions queryConditions = null,
            SqlTransaction transaction = null)
        {
            var tableName = GeneralHelper.GetTableName(typeof(T));
            if (queryConditions == null)
                queryConditions = new QueryConditions();
            queryConditions.AddCondition(tableName, $"Id = {id}");
            using (var connection = _connectionService.Create())
            {
                var good = await Select<T>(queryConditions,
                    transaction: transaction);
                var goods = good.ToList();
                return goods[0];
            }
        }
    }
}
