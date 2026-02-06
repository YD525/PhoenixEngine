using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhoenixEngine.EngineManagement.Thread
{
    public enum WorkState
    { 
       Null=0,WaitToCreated = 1,Working = 2,WorkEnd = 3
    }
    public class PhoenixThreadPool<T> where T : class
    {
        public List<PhoenixThread<T>> Threads = new List<PhoenixThread<T>>();

        public int GetWorkCount()
        { 
        
        }
    }
    public class PhoenixThread<T> where T : class
    {
        public WorkState State = WorkState.Null;
        public Action<T> Job;
    }
}
