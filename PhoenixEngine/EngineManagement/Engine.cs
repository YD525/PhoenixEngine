using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.DataBaseManagement;

namespace PhoenixEngine.EngineManagement
{
    public class Engine
    {
        /// <summary>
        /// Instance of the local SQLite database helper.
        /// Represents the pointer/reference to the current local database.
        /// </summary>
        public static SQLiteHelper LocalDB = new SQLiteHelper();

        public static void Init()
        { 
        
        }

        public static void Vacuum()
        {
            LocalDB.ExecuteNonQuery("vacuum");
        }
    }
}
