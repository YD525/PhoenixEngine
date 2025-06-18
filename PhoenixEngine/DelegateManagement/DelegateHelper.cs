using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhoenixEngine.DelegateManagement
{
    public class DelegateHelper
    {
        public static LogCall SetLog = null;
        public delegate bool LogCall(string Log,int LogViewType);

        public static CacheFunction AddCache = null;
        public delegate bool CacheFunction(string SourceStr, int From, int To, string Content);

        public static QueryCacheFunction FindCache = null;
        public delegate string QueryCacheFunction(string SourceStr, int From, int To);
    }
}
