using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Threading;

namespace PhoenixEngine.ADO
{
    public class P_SQLite : IDisposable
    {

        /// <summary>Enable SQL logging</summary>
        public bool EnableSqlOutput { get; set; } = false;

        /// <summary>Number of retries if SQLite operation fails (e.g., Busy)</summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>Delay between retries in milliseconds</summary>
        public int RetryDelay { get; set; } = 200;

        /// <summary>Controls connection pooling for isolated tests while production keeps pooling enabled.</summary>
        internal bool PoolingEnabled { get; set; } = true;


        private string _SQLPath;
        private string _ConnStr;

        // Dedicated write connection — serialized via _WriteLock
        private SQLiteConnection _WriteConn;
        private readonly object _WriteLock = new object();

        // Reader-writer lock: multiple concurrent reads, exclusive writes
        // Read ops open their own short-lived connections (WAL allows true parallel reads)
        private readonly ReaderWriterLockSlim _RWLock = new ReaderWriterLockSlim();

        private bool _Disposed = false;


        /// <summary>
        /// Open database connection, return directly if already open
        /// </summary>
        public string OpenSQL(string DBPath)
        {
            _SQLPath = DBPath;

            // Pooling=true: read connections are short-lived, let the pool reuse them
            _ConnStr = $"Data Source={_SQLPath};Pooling={PoolingEnabled};Journal Mode=WAL;Synchronous=NORMAL;BusyTimeout=30000";

            lock (_WriteLock)
            {
                if (_WriteConn != null && _WriteConn.State == ConnectionState.Open)
                    return "true";

                _WriteConn?.Dispose();
                _WriteConn = new SQLiteConnection(_ConnStr);
                _WriteConn.Open();
                ApplyPragmas(_WriteConn); // Apply once on write connection
            }

            return "true";
        }

        /// <summary>
        /// Apply high-performance PRAGMAs, call once after connection is open
        /// </summary>
        private void ApplyPragmas(SQLiteConnection Conn)
        {
            using (var CMD = Conn.CreateCommand())
            {
                // WAL mode: allows concurrent reads while writing
                CMD.CommandText = "PRAGMA journal_mode=WAL;";
                CMD.ExecuteNonQuery();

                // NORMAL sync: balance between performance and safety
                CMD.CommandText = "PRAGMA synchronous=NORMAL;";
                CMD.ExecuteNonQuery();

                // Cache size (pages), larger cache improves read performance
                CMD.CommandText = "PRAGMA cache_size=10000;";
                CMD.ExecuteNonQuery();

                // Store temp tables in memory, reduce disk IO
                CMD.CommandText = "PRAGMA temp_store=MEMORY;";
                CMD.ExecuteNonQuery();

                // Enable foreign key constraints (disabled by default)
                CMD.CommandText = "PRAGMA foreign_keys=ON;";
                CMD.ExecuteNonQuery();
            }

            LogSQL("[SQLITE] PRAGMAS APPLIED: WAL / NORMAL / CACHE=10000 / TEMP=MEMORY / FK=ON");
        }

        /// <summary>
        /// Open a short-lived read connection (WAL allows multiple concurrent readers)
        /// </summary>
        private SQLiteConnection OpenReadConn()
        {
            if (string.IsNullOrEmpty(_ConnStr))
                throw new InvalidOperationException("DATABASE NOT OPENED. CALL OPENSQL() FIRST.");

            var Conn = new SQLiteConnection(_ConnStr);
            Conn.Open();
            return Conn;
        }

        /// <summary>
        /// Execute action with retry, show error dialog if all retries fail
        /// </summary>
        private T ExecuteWithRetry<T>(string SQL, Func<T> Action, T Fallback = default)
        {
            int Attempt = 0;
            Exception LastEx = null;

            while (Attempt <= RetryCount)
            {
                try
                {
                    return Action();
                }
                catch (SQLiteException Ex) when (
                    Ex.ResultCode == SQLiteErrorCode.Busy ||
                    Ex.ResultCode == SQLiteErrorCode.Locked)
                {
                    // Database busy / locked, wait and retry
                    LastEx = Ex;
                    Attempt++;
                    LogSQL($"[RETRY {Attempt}/{RetryCount}] BUSY/LOCKED: {Ex.Message}");
                    if (Attempt <= RetryCount)
                        Thread.Sleep(RetryDelay);
                }
                catch (Exception Ex)
                {
                    // Other errors (syntax, constraint, etc.) no retry, show dialog immediately
                    LastEx = Ex;
                    Attempt = RetryCount + 1; // Break out of loop
                }
            }

            // All retries exhausted, notify user
            ShowErrorDialog(SQL, LastEx);
            return Fallback;
        }

        public static Action<string> OnError = null;

        /// <summary>
        /// Show error dialog, thread-safe for UI invoke
        /// </summary>
        private static void ShowErrorDialog(string SQL, Exception Ex)
        {
            string Msg = $"DATABASE OPERATION FAILED, ALL RETRIES EXHAUSTED.\n\n"
                       + $"ERROR TYPE: {Ex?.GetType().Name}\n"
                       + $"ERROR MSG:  {Ex?.Message}\n\n"
                       + $"SQL: {(SQL?.Length > 200 ? SQL.Substring(0, 200) + "…" : SQL)}";

            if (OnError != null)
            {
                OnError.Invoke(Msg);
            }

            Console.WriteLine(Msg);
        }


        private void LogSQL(string SQL)
        {
            if (EnableSqlOutput)
                System.Diagnostics.Debug.WriteLine("[SQLITE] " + SQL);
        }
        public List<Dictionary<string, object>> P_ExecuteQuery(string SQL)
        {
            return ExecuteQuery(SQL);
        }

        /// <summary>
        /// Execute query, return row list.
        /// Uses a short-lived read connection — multiple threads can read in parallel under WAL.
        /// </summary>
        public List<Dictionary<string, object>> ExecuteQuery(string SQL, params SQLiteParameter[] Parameters)
        {
            LogSQL(SQL);

            return ExecuteWithRetry(SQL, () =>
            {
                // Shared read lock: multiple readers allowed simultaneously
                _RWLock.EnterReadLock();
                try
                {
                    List<Dictionary<string, object>> Rows = new List<Dictionary<string, object>>();

                    // Each read gets its own connection — no contention between readers
                    using (var Conn = OpenReadConn())
                    using (var CMD = new SQLiteCommand(SQL, Conn))
                    {
                        if (Parameters?.Length > 0)
                            CMD.Parameters.AddRange(Parameters);

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
                    }

                    return Rows;
                }
                finally
                {
                    _RWLock.ExitReadLock();
                }
            }, Fallback: new List<Dictionary<string, object>>());
        }

        /// <summary>
        /// Execute non-query (INSERT / UPDATE / DELETE), return affected row count, -1 on failure.
        /// Acquires exclusive write lock — blocks until all active readers finish.
        /// </summary>
        public int ExecuteNonQuery(string CommandText, params SQLiteParameter[] Parameters)
        {
            LogSQL(CommandText);

            return ExecuteWithRetry(CommandText, () =>
            {
                // Exclusive write lock: waits for all readers to finish, blocks new readers
                _RWLock.EnterWriteLock();
                try
                {
                    lock (_WriteLock)
                    {
                        using (var CMD = _WriteConn.CreateCommand())
                        {
                            CMD.CommandText = CommandText;
                            if (Parameters?.Length > 0)
                                CMD.Parameters.AddRange(Parameters);
                            return CMD.ExecuteNonQuery();
                        }
                    }
                }
                finally
                {
                    _RWLock.ExitWriteLock();
                }
            }, Fallback: -1);
        }

        /// <summary>
        /// Execute scalar query, return first column of first row, null on failure.
        /// Uses a short-lived read connection — concurrent with other reads.
        /// </summary>
        public object ExecuteScalar(string SQL, params SQLiteParameter[] Parameters)
        {
            LogSQL(SQL);

            return ExecuteWithRetry(SQL, () =>
            {
                _RWLock.EnterReadLock();
                try
                {
                    using (var Conn = OpenReadConn())
                    using (var CMD = new SQLiteCommand(SQL, Conn))
                    {
                        CMD.CommandTimeout = 0;
                        if (Parameters?.Length > 0)
                            CMD.Parameters.AddRange(Parameters);
                        return CMD.ExecuteScalar();
                    }
                }
                finally
                {
                    _RWLock.ExitReadLock();
                }
            }, Fallback: null);
        }

        /// <summary>Executes parameterized commands atomically on the dedicated write connection.</summary>
        /// <param name="Commands">The fixed command texts and their data parameters.</param>
        /// <returns><c>true</c> after a successful commit; otherwise, <c>false</c> after rollback.</returns>
        /// <remarks>
        /// This instance owns and disposes the transaction and every command. The caller owns the enumerable
        /// and must provide a separate parameter instance for each command. The exclusive write lock is held
        /// for the complete transaction lifetime.
        /// </remarks>
        public bool ExecuteTransaction(IEnumerable<(string SQL, SQLiteParameter[] Params)> Commands)
        {
            _RWLock.EnterWriteLock();
            try
            {
                lock (_WriteLock)
                {
                    SQLiteTransaction Transaction = null;
                    try
                    {
                        Transaction = _WriteConn.BeginTransaction();
                        foreach (var Command in Commands)
                        {
                            LogSQL(Command.SQL);
                            using (var CMD = _WriteConn.CreateCommand())
                            {
                                CMD.Transaction = Transaction;
                                CMD.CommandText = Command.SQL;
                                if (Command.Params?.Length > 0)
                                    CMD.Parameters.AddRange(Command.Params);
                                CMD.ExecuteNonQuery();
                            }
                        }

                        Transaction.Commit();
                        return true;
                    }
                    catch (Exception Ex)
                    {
                        if (Transaction != null)
                        {
                            try
                            {
                                Transaction.Rollback();
                            }
                            catch (SQLiteException RollbackException)
                            {
                                LogSQL("[ROLLBACK FAILED] " + RollbackException.Message);
                            }
                        }

                        ShowErrorDialog("EXECUTETRANSACTION", Ex);
                        return false;
                    }
                    finally
                    {
                        Transaction?.Dispose();
                    }
                }
            }
            finally
            {
                _RWLock.ExitWriteLock();
            }
        }

        public void Close()
        {
            lock (_WriteLock)
            {
                if (_WriteConn != null)
                {
                    _WriteConn.Close();
                    _WriteConn.Dispose();
                    _WriteConn = null;
                }
            }
        }

        public void Dispose()
        {
            if (!_Disposed)
            {
                Close();
                _RWLock.Dispose();
                _Disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        public static void CreateDataBase(string Path)
        {
            SQLiteConnection.CreateFile(Path);
        }
    }
}
