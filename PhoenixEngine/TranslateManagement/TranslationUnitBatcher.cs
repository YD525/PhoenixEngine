using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;

namespace PhoenixEngine.TranslateManagement
{
    public class TranslationUnitBatcher
    {
        public static int TextLengthLimit = 1000;
        public class BatchTranslationUnit
        {
            public Languages From;
            public Languages To;

            public int TextLength = 0;
            public string[] ContextTokens;

            public List<TranslationUnit> TranslationUnits = new List<TranslationUnit>();
        }

        public static List<BatchTranslationUnit> MergeUnits(List<TranslationUnit> Units)
        {
            return new List<BatchTranslationUnit>();
        }
    }
   
}
