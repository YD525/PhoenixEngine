using System.Collections.Generic;
using System.Data.SQLite;
using PhoenixEngine.ADO;
using PhoenixEngine.Common;

namespace PhoenixEngine
{
    /// <summary>Provides bounded pagination over fixed product database tables.</summary>
    public class P_SQL_Pagination
    {
        /// <summary>Calculates pages for a legacy call that does not bind filter values.</summary>
        /// <param name="TableName">The fixed product table name.</param>
        /// <param name="Where">An empty or allow-listed parameter-free filter template.</param>
        /// <returns>The number of pages at the configured default page size.</returns>
        public static int GetPageCount(string TableName, string Where)
        {
            return GetPageCount(TableName, Where, new SQLiteParameter[0]);
        }

        /// <summary>Calculates the number of pages for an allow-listed table and filter template.</summary>
        /// <param name="TableName">The fixed product table name.</param>
        /// <param name="Where">An allow-listed filter template containing parameters, not values.</param>
        /// <param name="Parameters">The data values referenced by <paramref name="Where"/>.</param>
        /// <returns>The number of pages at the configured default page size.</returns>
        public static int GetPageCount(
            string TableName,
            string Where,
            params SQLiteParameter[] Parameters)
        {
            string QuotedTableName = SqliteSql.QuoteIdentifier(TableName);
            string Filter = SqliteSql.RequirePaginationFilter(Where);
            string SqlOrder = "SELECT COUNT(*) FROM " + QuotedTableName + " " + Filter + ";";
            int GetCount = P_Convert.ObjToInt(Phoenix.LocalDB.ExecuteScalar(SqlOrder, Parameters));
            int PageCount = GetCount / Phoenix.Config.DefPageSize;
            if (GetCount % Phoenix.Config.DefPageSize > 0)
            {
                PageCount++;
            }
            return PageCount;
        }

        /// <summary>Reads one page for a legacy call that does not bind filter values.</summary>
        /// <param name="TableName">The fixed product table name.</param>
        /// <param name="PageNo">The one-based page number.</param>
        /// <param name="Count">The maximum number of rows returned.</param>
        /// <param name="Where">An empty or allow-listed parameter-free filter template.</param>
        /// <returns>The rows in descending row-id order.</returns>
        public static List<Dictionary<string, object>> GetTablePageData(
            string TableName,
            int PageNo,
            int Count,
            string Where = "")
        {
            return GetTablePageData(TableName, PageNo, Count, Where, new SQLiteParameter[0]);
        }

        /// <summary>Reads one descending row-id page from an allow-listed table and filter template.</summary>
        /// <param name="TableName">The fixed product table name.</param>
        /// <param name="PageNo">The one-based page number.</param>
        /// <param name="Count">The maximum number of rows returned.</param>
        /// <param name="Where">An allow-listed filter template containing parameters, not values.</param>
        /// <param name="Parameters">The data values referenced by <paramref name="Where"/>.</param>
        /// <returns>The rows in descending row-id order.</returns>
        public static List<Dictionary<string, object>> GetTablePageData(
            string TableName,
            int PageNo,
            int Count,
            string Where,
            params SQLiteParameter[] Parameters)
        {
            string QuotedTableName = SqliteSql.QuoteIdentifier(TableName);
            string Filter = SqliteSql.RequirePaginationFilter(Where);
            string SqlOrder = "SELECT Rowid, * FROM " + QuotedTableName + " " + Filter +
                " ORDER BY Rowid DESC LIMIT @pageSize OFFSET @offset;";

            var BoundParameters = new List<SQLiteParameter>(Parameters ?? new SQLiteParameter[0])
            {
                SqliteSql.Parameter("@pageSize", Count),
                SqliteSql.Parameter("@offset", ((long)PageNo - 1L) * Count)
            };
            return Phoenix.LocalDB.ExecuteQuery(SqlOrder, BoundParameters.ToArray());
        }
    }

    /// <summary>Describes one page of database-backed results.</summary>
    /// <typeparam name="T">The page payload type.</typeparam>
    public class P_SQL_Page<T> where T : new()
    {
        public T CurrentPage = new T();
        public int PageNo = 0;
        public int MaxPage = 0;

        /// <summary>Creates a page result from its payload and paging metadata.</summary>
        /// <param name="Source">The page payload.</param>
        /// <param name="PageNo">The one-based current page number.</param>
        /// <param name="MaxPage">The total number of available pages.</param>
        public P_SQL_Page(T Source, int PageNo, int MaxPage)
        {
            this.CurrentPage = Source;
            this.PageNo = PageNo;
            this.MaxPage = MaxPage;
        }
    }
}
