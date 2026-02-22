using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.EngineManagement;

namespace PhoenixEngine.LanguageManagement
{
    public class ChineseVariantMap
    {
        public static void Init()
        {
            string CheckTableSql = "SELECT name FROM sqlite_master WHERE type='table' AND name='ChineseVariantMap';";
            var Result = Phoenix.LocalDB.ExecuteScalar(CheckTableSql);

            if (Result == null || Result == DBNull.Value)
            {
                CreateNewTable();
            }
        }

        private static void CreateNewTable()
        {
            string SqlOrder = @"
            CREATE TABLE [ChineseVariantMap](
            [Simplified] TEXT, 
            [Traditional] TEXT, 
            [MatchType] INT
            );";

            Phoenix.LocalDB.ExecuteNonQuery(SqlOrder);
        }
    }
}
