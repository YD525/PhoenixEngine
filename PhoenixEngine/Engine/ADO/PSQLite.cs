using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Threading;

namespace PhoenixEngine.ADO
{
    public class P_SQLite
    {
        private string _SQLPath = null;
        private SQLiteConnection _SharedConn;
        private readonly object _ConnLocker = new object();

        /// <summary>
        /// Enable SQL logging
        /// </summary>
        public bool EnableSqlOutput { get; set; } = false;

        /// <summary>
        /// Number of retries if SQLite operation fails (e.g., Busy)
        /// </summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// Delay between retries in milliseconds
        /// </summary>
        public int RetryDelay { get; set; } = 200;

        public string OpenSQL(string DBPath)
        {
            _SQLPath = DBPath;
            string ConnStr = $"Data Source={_SQLPath};Pooling=true;Journal Mode=WAL;Synchronous=OFF;BusyTimeout=30000";

            lock (_ConnLocker)
            {
                if (_SharedConn != null)
                {
                    if (_SharedConn.State == ConnectionState.Open) return "true";
                    _SharedConn.Close();
                    _SharedConn.Dispose();
                }

                _SharedConn = new SQLiteConnection(ConnStr);
                _SharedConn.Open();
            }

            EnableSQLiteCache(_SharedConn);

            return "true";
        }

        /// <summary>
        /// Enable SQLite high-performance cache and WAL mode
        /// Call this after opening the connection with OpenSql()
        /// </summary>
        /// <param name="conn">An already opened SQLiteConnection</param>
        public void EnableSQLiteCache(SQLiteConnection Connect)
        {
            if (Connect == null || Connect.State != ConnectionState.Open)
                throw new InvalidOperationException("Connection must be open before enabling cache.");

            using (var CMD = Connect.CreateCommand())
            {
                // Enable WAL (Write-Ahead Logging) mode for better concurrent read/write performance
                CMD.CommandText = "PRAGMA journal_mode=WAL;";
                CMD.ExecuteNonQuery();

                // Set synchronous to NORMAL to improve write performance
                // Note: this reduces safety on power failure but speeds up writes
                CMD.CommandText = "PRAGMA synchronous=NORMAL;";
                CMD.ExecuteNonQuery();

                // Set cache size (number of pages), larger cache improves read performance
                CMD.CommandText = "PRAGMA cache_size=10000;";
                CMD.ExecuteNonQuery();

                // Store temporary tables in memory to reduce disk IO
                CMD.CommandText = "PRAGMA temp_store=MEMORY;";
                CMD.ExecuteNonQuery();

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
                if (_SharedConn == null) throw new InvalidOperationException("Database not opened. Call OpenSql() first.");

                if (_SharedConn.State != ConnectionState.Open)
                {
                    lock (_ConnLocker)
                    {
                        if (_SharedConn.State != ConnectionState.Open)
                            _SharedConn.Open();
                    }
                }

                return _SharedConn;
            }
        }

        private void LogSql(string SQL)
        {
            if (EnableSqlOutput)
            {
                System.Diagnostics.Debug.WriteLine("[SQLite] " + SQL);
            }
        }

        private T ExecuteWithRetry<T>(Func<T> Action)
        {
            int Attempt = 0;
            while (true)
            {
                try
                {
                    return Action();
                }
                catch (SQLiteException Ex)
                {
                    Attempt++;
                    if (Attempt > RetryCount)
                        throw;

                    // Optional: only retry for busy/locked errors
                    if (Ex.ResultCode == SQLiteErrorCode.Busy || Ex.ResultCode == SQLiteErrorCode.Locked)
                    {
                        Thread.Sleep(RetryDelay);
                        continue;
                    }
                    throw;
                }
            }
        }

        public List<Dictionary<string, object>> ExecuteQuery(string SQL)
        {
            LogSql(SQL);
            return ExecuteWithRetry(() =>
            {
                lock (_ConnLocker)
                {
                    List<Dictionary<string, object>> Rows = new List<Dictionary<string, object>>();
                    using (var CMD = new SQLiteCommand(SQL, SharedConn))
                    using (var Reader = CMD.ExecuteReader())
                    {
                        while (Reader.Read())
                        {
                            Dictionary<string, object> Row = new Dictionary<string, object>(Reader.FieldCount);
                            for (int i = 0; i < Reader.FieldCount; i++)
                            {
                                string ColName = Reader.GetName(i);

                                if (string.Equals(ColName, "rowid", StringComparison.OrdinalIgnoreCase))
                                    ColName = "Rowid";

                                Row[ColName] = Reader.IsDBNull(i) ? null : Reader.GetValue(i);
                            }
                            Rows.Add(Row);
                        }
                    }
                    return Rows;
                }
            });
        }

        public int ExecuteNonQuery(string CommandText, params SQLiteParameter[] Parameters)
        {
            LogSql(CommandText);
            return ExecuteWithRetry(() =>
            {
                lock (_ConnLocker)
                {
                    using (var CMD = SharedConn.CreateCommand())
                    {
                        CMD.CommandText = CommandText;
                        if (Parameters != null && Parameters.Length > 0)
                            CMD.Parameters.AddRange(Parameters);
                        return CMD.ExecuteNonQuery();
                    }
                }
            });
        }

        public object ExecuteScalar(string SQL, params SQLiteParameter[] Parameters)
        {
            LogSql(SQL);
            return ExecuteWithRetry(() =>
            {
                lock (_ConnLocker)
                {
                    using (var CMD = SharedConn.CreateCommand())
                    {
                        CMD.CommandText = SQL;
                        CMD.CommandTimeout = 0;
                        if (Parameters != null && Parameters.Length > 0)
                            CMD.Parameters.AddRange(Parameters);
                        return CMD.ExecuteScalar();
                    }
                }
            });
        }

        public void Close()
        {
            lock (_ConnLocker)
            {
                if (_SharedConn != null)
                {
                    _SharedConn.Close();
                    _SharedConn.Dispose();
                    _SharedConn = null;
                }
            }
        }
    }

}
