using System.Collections.Generic;
using System.Threading;
using PhoenixEngine.Memory;
using PhoenixEngine.Sequence;
using PhoenixEngine.Translate;

namespace PhoenixEngine.Unit
{
    public static class UnitGroupExtend
    {
        /// <summary>
        /// Associating all objects with TranslatedLink is used to bridge the gap with the target program.
        /// </summary>
        /// <param name=""></param>
        public static void UpdateLink(this UnitGroup Item,Translator TranslatorRef)
        {
            lock (TranslatorRef.TransDataLocker)
            {
                for (int i = 0; i < Item.Units.Count; i++)
                {
                    BaseUnit GetUnit = Item.Units[i];

                    var Link = TranslatorRef.GetLink();

                    Link[GetUnit.Key] = new P_String(GetUnit.Translated,0);
                }
            }
        }

        /// <summary>
        /// Update all objects to the cloud translation list in the local database.
        /// </summary>
        /// <param name="Item"></param>
        /// <param name="TranslatorRef"></param>
        /// <param name="UnitGroups"></param>
        public static void UpdateCloudData(this UnitGroup Item,Translator TranslatorRef,Dictionary<string, UnitSequence> Sequences)
        {
            lock (TranslatorRef.TransDataLocker)
            {
                for (int i = 0; i < Item.Units.Count; i++)
                {
                    var GetUnit = Item.Units[i];

                    NextTry:
                    try
                    {
                        if (Sequences[GetUnit.Key].CanUpdateDB)
                        {
                            string Source = GetUnit.GetRealOriginal();
                           
                            TranslatorRef.SetCloudData(GetUnit.FileUniqueKey,GetUnit.Key, Source, GetUnit.Translated);
                        }
                    }
                    catch
                    {
                        Thread.Sleep(100);
                        goto NextTry;
                    }
                }
            }
        }

        public static void UpdateAIMemory(this UnitGroup Item, Translator TranslatorRef, Dictionary<string, UnitSequence> Sequences)
        {
            lock (TranslatorRef.TransDataLocker)
            {
                Item.IsMemoryCreated = true;

                for (int i = 0; i < Item.Units.Count; i++)
                {
                    var GetUnit = Item.Units[i];

                    if (GetUnit.Translated.Trim().Length > 0)
                    { 
                        Phoenix.AIMemory.AddTranslation(TranslatorRef.From, TranslatorRef.To, GetUnit.GetRealOriginal(), GetUnit.Translated);
                    }
                }
            }
        }

        public static int GetCount(this UnitGroup Item)
        {
            return Item.Units.Count;
        }

        public static void ReSet(this UnitGroup Item)
        {
            for (int i = 0; i < Item.Units.Count; i++)
            {
                Item.Units[i].ReSet();
            }
        }

        public static void ReSet(this UnitGroup Item,int Index)
        {
            Item.Units[Index].ReSet();
        }
    }
}
