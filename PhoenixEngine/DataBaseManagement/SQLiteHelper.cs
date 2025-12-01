using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace PhoenixEngine.DataBaseManagement
{
    public class SQLiteHelper
    {
        private string _sqlPath = null;
        private SQLiteConnection _sharedConn;
        private readonly object _connLocker = new object();

        /// <summary>
        /// Enable SQL logging
        /// </summary>
        public bool EnableSqlOutput { get; set; } = true;

        /// <summary>
        /// Number of retries if SQLite operation fails (e.g., Busy)
        /// </summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// Delay between retries in milliseconds
        /// </summary>
        public int RetryDelay { get; set; } = 200;

        public string OpenSql(string dbPath)
        {
            _sqlPath = dbPath;
            string connStr = $"Data Source={_sqlPath};Pooling=true;Journal Mode=WAL;Synchronous=OFF;BusyTimeout=30000";

            lock (_connLocker)
            {
                if (_sharedConn != null)
                {
                    if (_sharedConn.State == ConnectionState.Open) return "true";
                    _sharedConn.Close();
                    _sharedConn.Dispose();
                }

                _sharedConn = new SQLiteConnection(connStr);
                _sharedConn.Open();
            }

            EnableSQLiteCache(_sharedConn);

            return "true";
        }

        /// <summary>
        /// Enable SQLite high-performance cache and WAL mode
        /// Call this after opening the connection with OpenSql()
        /// </summary>
        /// <param name="conn">An already opened SQLiteConnection</param>
        public void EnableSQLiteCache(SQLiteConnection conn)
        {
            if (conn == null || conn.State != ConnectionState.Open)
                throw new InvalidOperationException("Connection must be open before enabling cache.");

            using (var cmd = conn.CreateCommand())
            {
                // Enable WAL (Write-Ahead Logging) mode for better concurrent read/write performance
                cmd.CommandText = "PRAGMA journal_mode=WAL;";
                cmd.ExecuteNonQuery();

                // Set synchronous to NORMAL to improve write performance
                // Note: this reduces safety on power failure but speeds up writes
                cmd.CommandText = "PRAGMA synchronous=NORMAL;";
                cmd.ExecuteNonQuery();

                // Set cache size (number of pages), larger cache improves read performance
                cmd.CommandText = "PRAGMA cache_size=10000;";
                cmd.ExecuteNonQuery();

                // Store temporary tables in memory to reduce disk IO
                cmd.CommandText = "PRAGMA temp_store=MEMORY;";
                cmd.ExecuteNonQuery();

                // Optional: attach an in-memory database as cache (read-only scenarios)
                // cmd.CommandText = "ATTACH DATABASE ':memory:' AS memdb;";
                // cmd.ExecuteNonQuery();
            }

            Console.WriteLine("[SQLite] Cache and WAL enabled.");
        }

        private SQLiteConnection SharedConn
        {
            get
            {
                if (_sharedConn == null) throw new InvalidOperationException("Database not opened. Call OpenSql() first.");

                if (_sharedConn.State != ConnectionState.Open)
                {
                    lock (_connLocker)
                    {
                        if (_sharedConn.State != ConnectionState.Open)
                            _sharedConn.Open();
                    }
                }

                return _sharedConn;
            }
        }

        private void LogSql(string sql)
        {
            if (EnableSqlOutput)
            {
                System.Diagnostics.Debug.WriteLine("[SQLite] " + sql);
            }
        }

        private T ExecuteWithRetry<T>(Func<T> action)
        {
            int attempt = 0;
            while (true)
            {
                try
                {
                    return action();
                }
                catch (SQLiteException ex)
                {
                    attempt++;
                    if (attempt > RetryCount)
                        throw;

                    // Optional: only retry for busy/locked errors
                    if (ex.ResultCode == SQLiteErrorCode.Busy || ex.ResultCode == SQLiteErrorCode.Locked)
                    {
                        Thread.Sleep(RetryDelay);
                        continue;
                    }
                    throw;
                }
            }
        }

        public List<Dictionary<string, object>> ExecuteQuery(string sql)
        {
            LogSql(sql);
            return ExecuteWithRetry(() =>
            {
                lock (_connLocker)
                {
                    List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
                    using (var cmd = new SQLiteCommand(sql, SharedConn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Dictionary<string, object> row = new Dictionary<string, object>(reader.FieldCount);
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                            }
                            rows.Add(row);
                        }
                    }
                    return rows;
                }
            });
        }

        public int ExecuteNonQuery(string commandText, params SQLiteParameter[] parameters)
        {
            LogSql(commandText);
            return ExecuteWithRetry(() =>
            {
                lock (_connLocker)
                {
                    using (var cmd = SharedConn.CreateCommand())
                    {
                        cmd.CommandText = commandText;
                        if (parameters != null && parameters.Length > 0)
                            cmd.Parameters.AddRange(parameters);
                        return cmd.ExecuteNonQuery();
                    }
                }
            });
        }

        public object ExecuteScalar(string sql, params SQLiteParameter[] parameters)
        {
            LogSql(sql);
            return ExecuteWithRetry(() =>
            {
                lock (_connLocker)
                {
                    using (var cmd = SharedConn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.CommandTimeout = 0;
                        if (parameters != null && parameters.Length > 0)
                            cmd.Parameters.AddRange(parameters);
                        return cmd.ExecuteScalar();
                    }
                }
            });
        }

        public void Close()
        {
            lock (_connLocker)
            {
                if (_sharedConn != null)
                {
                    _sharedConn.Close();
                    _sharedConn.Dispose();
                    _sharedConn = null;
                }
            }
        }
    }

}
