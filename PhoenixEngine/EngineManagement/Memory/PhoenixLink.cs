using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhoenixEngine.EngineManagement.Memory
{
    public class PhoenixLink<Key, Value> where Value : new()
    {
        private object QueryLock = new object();
        private Dictionary<Key, Value> DictData = new Dictionary<Key, Value>();
        public Value this[Key Key]
        {
            get
            {
                lock (QueryLock)
                {
                    if (DictData.ContainsKey(Key))
                    {
                        return DictData[Key];
                    }

                    return new Value();
                }
            }
            set
            {
                lock (QueryLock)
                {
                    if (DictData.ContainsKey(Key))
                    {
                        DictData[Key] = value;
                    }
                    else
                    {
                        DictData.Add(Key, value);
                    }
                }

            }
        }

    }

    public class LinkTest
    {
        public void Test()
        {
            PhoenixLink<string, LinkTest> SetLink = new PhoenixLink<string, LinkTest>();
            SetLink[""] = new LinkTest();
        }
    }
}
