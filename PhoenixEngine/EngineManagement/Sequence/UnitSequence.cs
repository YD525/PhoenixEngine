using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.TranslateManagement;

namespace PhoenixEngine.EngineManagement
{
    public enum AggregationMode
    {
        Null = 0, Single = 1, Aggregation = 2
    }

    public class UnitSequence
    {
        public class UnionArray
        {
            public Dictionary<string, BaseUnit> Leaders = new Dictionary<string, BaseUnit>();
            public List<BaseUnit> Units = new List<BaseUnit>();

            public void Load(List<BaseUnit> BaseUnits)
            { 
            
            }
        }

    }
}
