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
    }
}
