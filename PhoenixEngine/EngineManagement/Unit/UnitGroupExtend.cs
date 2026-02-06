using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.TranslateManage;
using PhoenixEngine.TranslateManagement;

namespace PhoenixEngine.EngineManagement.Unit
{
    public static class UnitGroupExtend
    {
        /// <summary>
        /// Associating all objects with TranslatedLink is used to bridge the gap with the target program.
        /// </summary>
        /// <param name=""></param>
        public static void UPDateLink(this UnitGroup Item,Translator TranslatorRef)
        {
            lock (TranslatorRef.TransDataLocker)
            {
                for (int i = 0; i < Item.Units.Count; i++)
                {
                    BaseUnit GetUnit = Item.Units[i];

                    TranslatorRef.TranslatedLink[GetUnit.Key] = GetUnit.Translated;
                }
            }
        }
    }
}
