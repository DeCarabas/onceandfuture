using Npgsql;
using Serilog;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace OnceAndFuture.DAL
{
    public abstract class DalBase
    {
        readonly ILogger logger;

        protected DalBase(string table)
        {
            this.logger = Serilog.Log
                .ForContext(HoneycombSink.DatasetPropertyKey, "Database")
                .ForContext("Table", table);
        }

        protected async Task<NpgsqlConnection> OpenConnection()
        {
            string connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
            var connection = new NpgsqlConnection(connectionString);
            try
            {
                await connection.OpenAsync();
                return connection;
            }
            catch (Exception)
            {
                if (connection != null) { connection.Dispose(); }
                throw;
            }
        }

        protected async Task DoOperation(string operation, string key, Func<Task<string>> func)
        {
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                string details = await func();
                OperationStatus status = (details == null)
                    ? OperationStatus.OK
                    : OperationStatus.Error;
                LogOperation(operation, key, timer, status, details);
            }
            catch (Exception e)
            {
                LogOperation(operation, key, timer, OperationStatus.Exception, e.ToString());
                throw;
            }
        }

        void LogOperation(string operation, string key, Stopwatch timer, OperationStatus status, string details)
        {
            this.logger.Information(
                "{Operation} {Key}: {ElapsedMs}ms: {Status}: {Details}",
                operation, key.ToString(), timer.ElapsedMilliseconds, status.ToString(), details);
        }

        enum OperationStatus
        {
            OK,
            Error,
            Exception
        }
    }
}
