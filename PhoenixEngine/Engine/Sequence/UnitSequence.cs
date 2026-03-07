using System.Collections.Generic;
using PhoenixEngine.ADO;
using PhoenixEngine.Engine;
using PhoenixEngine.Translate;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.Unit;

namespace PhoenixEngine.Sequence
{
    public enum AggregationMode
    {
        Null = 0, Single = 1, Aggregation = 2
    }

    public class UnitSequence
    {
        public string Data = "";
        public bool CanSkip = false;//This indicates that the specified object can be skipped.
        public bool HasOuterQuotes = false;
        public bool CanSkipSleep = false;
        public bool HavePlaceholder = false;
        public int Step = 0;
        public bool CanUPDateDB = true;
        public TranslationPreprocessor Preprocessor = null;

        public UnitSequence(bool CanSkip)
        { 
            this.CanSkip = CanSkip;
        }
    }
    public static class UnitGroupUnitSequence
    {

        /// <summary>
        /// Strings at the beginning of the preprocessing translation stage
        /// </summary>
        /// <param name="Item"></param>
        /// <param name="Preprocessor"></param>
        public static void StartPreProcess(this UnitGroup Item,
            TranslationPreprocessor Preprocessor,
            Languages From,Languages To,
            ref Dictionary<string,UnitSequence> Sequences)
        {
            Sequences = new Dictionary<string, UnitSequence>();

            for (int i = 0; i < Item.Units.Count; i++)
            { 
                var GetUnit = Item.Units[i];
                string Source = string.Copy(GetUnit.Original);

                Sequences.Add(GetUnit.Key,new UnitSequence(false));
                Sequences[GetUnit.Key].Step = 0;
                Sequences[GetUnit.Key].Preprocessor = TranslationPreprocessor.Clone(Preprocessor);

                if (Preprocessor.IsOnlySymbolsAndSpaces(Source))//Skip pure symbol content.
                {
                    Sequences[GetUnit.Key].Data = Source;
                    GetUnit.Translated = Sequences[GetUnit.Key].Data;
                    Sequences[GetUnit.Key].CanSkip = true;
                }
                else
                if (string.IsNullOrEmpty(Source))//SkipEmptyContent
                {
                    Sequences[GetUnit.Key].Data = Source;
                    GetUnit.Translated = Sequences[GetUnit.Key].Data;
                    Sequences[GetUnit.Key].CanSkip = true;
                }
                else
                if (Preprocessor.IsNumeric(Source))//Skip pure numbers
                {
                    Sequences[GetUnit.Key].Data = Source;
                    GetUnit.Translated = Sequences[GetUnit.Key].Data;
                    Sequences[GetUnit.Key].CanSkip = true;
                }

                if (!Sequences[GetUnit.Key].CanSkip)
                {
                    Sequences[GetUnit.Key].Step = 1;

                    string Content = Source;
                    Preprocessor.OptimizeStrings(ref Content);
                    Sequences[GetUnit.Key].HasOuterQuotes = Preprocessor.HasOuterQuotes(Content.Trim());

                    //Remove OuterQuotes
                    if (Sequences[GetUnit.Key].HasOuterQuotes)
                    {
                        Preprocessor.StripOuterQuotes(ref Content);
                    }

                    Sequences[GetUnit.Key].Data = Content;
                    GetUnit.Original = Sequences[GetUnit.Key].Data;

                    //Match DataBase
                    string GetMatchResult = "";
                    if (Preprocessor.ExactMatch(From, To, GetUnit.Key, GetUnit.Type, Content, ref GetMatchResult))
                    {
                        Sequences[GetUnit.Key].Data = GetMatchResult;
                        GetUnit.Translated = Sequences[GetUnit.Key].Data;
                        Sequences[GetUnit.Key].CanSkip = true;
                    }
                    else
                    {
                        Sequences[GetUnit.Key].Data = Content;
                    }
                }

            }
        }
        /// <summary>
        /// The second stage involves matching the database.
        /// </summary>
        /// <param name="Item"></param>
        /// <param name="Preprocessor"></param>
        /// <param name="From"></param>
        /// <param name="To"></param>
        /// <param name="Sequences"></param>
        /// <param name="CanSkipSleep"></param>
        public static void CenterPreProcess(this UnitGroup Item,
          Languages From, Languages To,
          ref Dictionary<string, UnitSequence> Sequences)
        {
            for (int i = 0; i < Item.Units.Count; i++)
            {
                var GetUnit = Item.Units[i];

                string Source = Sequences[GetUnit.Key].Data;
                var Preprocessor = Sequences[GetUnit.Key].Preprocessor;

                Sequences[GetUnit.Key].Step = 2;

                if (!Sequences[GetUnit.Key].CanSkip)
                {
                    CacheCall Call = new CacheCall();
                    Call.SendString = Source;

                    string GetCacheStr = CloudDBCache.FindCache(Phoenix.GetFileUniqueKey(), GetUnit.Key, To);

                    if (GetCacheStr.Trim().Length > 0)
                    {
                        Call.ReceiveString = GetCacheStr;

                        Call.Log = "Cache From Database";

                        Call.Output();

                        Sequences[GetUnit.Key].CanSkipSleep = true;

                        //Update AI memory
                        if (Source.Length > 0 && Phoenix.Config.ContextEnable)
                        {
                            Phoenix.AIMemory.AddTranslation(From, To, GetUnit.Original, GetCacheStr);
                        }

                        Sequences[GetUnit.Key].Data = GetCacheStr;
                        GetUnit.Translated = Sequences[GetUnit.Key].Data;
                        Sequences[GetUnit.Key].CanSkip = true;
                        continue;
                    }

                    if (Phoenix.Config.EnableGlobalSearch)
                    {
                        var MatchItem = CloudDBCache.Match((int)To, Source);
                        if (MatchItem != null)
                        {
                            Call.ReceiveString = GetCacheStr;
                            try
                            {
                                Call.Log = "Data available for translation was retrieved from the database. File:" + UniqueKeyHelper.RowidToOriginalKey(MatchItem.FileUniqueKey);
                            }
                            catch { }

                            Call.Output();

                            Sequences[GetUnit.Key].CanSkipSleep = true;

                            if (Source.Length > 0 && Phoenix.Config.ContextEnable)
                            {
                                Phoenix.AIMemory.AddTranslation(From, To, GetUnit.Original, MatchItem.Result);
                            }

                            Sequences[GetUnit.Key].Data = MatchItem.Result;
                            GetUnit.Translated = Sequences[GetUnit.Key].Data;
                            Sequences[GetUnit.Key].CanSkip = true;
                            continue;
                        }
                    }
                }
            }
        }

        public static void StartGeneratePlaceholder(this UnitGroup Item,
            Languages From, Languages To,
            ref Dictionary<string, UnitSequence> Sequences)
        {
            for (int i = 0; i < Item.Units.Count; i++)
            {
                var GetUnit = Item.Units[i];
                var Preprocessor = Sequences[GetUnit.Key].Preprocessor;

                List<ReplaceTag> CustomWords = new List<ReplaceTag>();

                bool CanTrans = false;

                string Source = string.Copy(Sequences[GetUnit.Key].Data);

                if (Phoenix.Config.PreTranslateEnable)
                {
                    PreTranslateCall NPreTranslateCall = new PreTranslateCall();
                    NPreTranslateCall.Platform = PlatformType.PhoenixEngine;
                    NPreTranslateCall.FromAI = false;
                    NPreTranslateCall.Key = Item.Key;

                    NPreTranslateCall.SendString = Source;

                    Source = Preprocessor.GeneratePlaceholderText(Phoenix.LastLoadFileName, From, To, Source, GetUnit.Type, out CanTrans);

                    CustomWords.Clear();
                    foreach (var GetWord in Preprocessor.ReplaceTags)
                    {
                        CustomWords.Add(GetWord);
                    }

                    NPreTranslateCall.ReceiveString = Source;

                    NPreTranslateCall.ReplaceTags = Preprocessor.ReplaceTags;

                    NPreTranslateCall.Output();

                    Sequences[GetUnit.Key].Data = Source;

                    GetUnit.Original = Sequences[GetUnit.Key].Data;

                    Sequences[GetUnit.Key].HavePlaceholder = true;

                    if (!CanTrans)
                    {
                        Sequences[GetUnit.Key].Data = Preprocessor.RestoreFromPlaceholder(Source, To);
                        Sequences[GetUnit.Key].HavePlaceholder = false;
                        Sequences[GetUnit.Key].CanUPDateDB = false;

                        GetUnit.Translated = Sequences[GetUnit.Key].Data;
                        Sequences[GetUnit.Key].CanSkip = true;
                    }
                }
                else
                {
                    Sequences[GetUnit.Key].CanSkip = false;
                    CanTrans = true;
                }
            }
        }


        public static void EndGeneratePlaceholder(this UnitGroup Item,
            Languages From, Languages To,
            ref Dictionary<string, UnitSequence> Sequences)
        {
            for (int i = 0; i < Item.Units.Count; i++)
            {
                var GetUnit = Item.Units[i];
                var Preprocessor = Sequences[GetUnit.Key].Preprocessor;
                Sequences[GetUnit.Key].Step = 8;

                if (Sequences[GetUnit.Key].HavePlaceholder)
                {
                    GetUnit.Translated = Preprocessor.RestoreFromPlaceholder(Sequences[GetUnit.Key].Data, To);
                    Sequences[GetUnit.Key].Data = GetUnit.Translated;
                    Sequences[GetUnit.Key].HavePlaceholder = false;
                }
            }
        }
        /// <summary>
        /// The string is processed again after translation.
        /// </summary>
        /// <param name="Item"></param>
        /// <param name="Preprocessor"></param>
        /// <param name="From"></param>
        /// <param name="To"></param>
        /// <param name="Sequences"></param>
        public static void EndPreProcess(this UnitGroup Item,
            Languages From, Languages To,
            ref Dictionary<string, UnitSequence> Sequences)
        {
            for (int i = 0; i < Item.Units.Count; i++)
            {
                var GetUnit = Item.Units[i];

                string Translated = GetUnit.Translated;

                Sequences[GetUnit.Key].Step = 7;
                var Preprocessor = Sequences[GetUnit.Key].Preprocessor;

                Preprocessor.OptimizeStrings(ref Translated);
                Translated = Translated.Trim();

                if (Sequences[GetUnit.Key].HasOuterQuotes)
                {
                    Translated = "\"" + Translated + "\"";
                }

                Translated = Preprocessor.ReturnStr(Translated);
                Sequences[GetUnit.Key].Data = Translated;

                GetUnit.Translated = Translated;
            }
        }
    }
}
