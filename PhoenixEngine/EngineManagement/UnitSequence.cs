using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.TranslateManagement;

namespace PhoenixEngine.EngineManagement
{
    
    public class UnitSequence
    {
        public class UnionArray
        {
            public Dictionary<string, TranslationUnit> Leaders = new Dictionary<string, TranslationUnit>();
            public List<TranslationUnit> Units = new List<TranslationUnit>();

            public UnionArray(List<TranslationUnit> Units)
            { 
            
            
            }
        }

    }
}
