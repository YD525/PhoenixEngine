using System.Collections.Generic;
using System.Text.RegularExpressions;
using PhoenixEngine.EngineManagement.Unit;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;
using PhoenixEngine.TranslateManagement;
using static PhoenixEngine.EngineManagement.DataTransmission;
using static PhoenixEngine.TranslateManage.EngineCore;

namespace PhoenixEngine.EngineManagement
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
        public TranslationPreprocessor Preprocessor = null;

        public UnitSequence(bool CanSkip)
        { 
            this.CanSkip = CanSkip;
        }
    }
    public static class UnitGroupUnitSequence
    {
        /// <summary>
        /// Apply sequence to UnitGroup
        /// </summary>
        /// <param name="Item"></param>
        /// <param name="Sequences"></param>
        public static void UPDateSequences(this UnitGroup Item, Dictionary<string, UnitSequence> Sequences)
        {
            Dictionary<string, UnitSequence> CopySequences = new Dictionary<string, UnitSequence>(Sequences);
            foreach (var GetSeq in Sequences)
            {
                for (int i = 0; i < Item.Units.Count; i++)
                {
                    var GetUnit = Item.Units[i];
                    if (GetUnit.Key.Equals(GetSeq.Key))
                    {
                        if (GetSeq.Value.CanSkip)
                        {
                            Item.Units[i].Translated = GetSeq.Value.Data;
                        }
                    }
                }
            }
        }

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

                Sequences[GetUnit.Key].Step = 0;
                Sequences[GetUnit.Key].Preprocessor = TranslationPreprocessor.Clone(Preprocessor);

                if (Preprocessor.IsOnlySymbolsAndSpaces(Source))//Skip pure symbol content.
                {
                    Sequences[GetUnit.Key].Data = Source;
                    Sequences[GetUnit.Key].CanSkip = true;
                }
                else
                if (string.IsNullOrEmpty(Source))//SkipEmptyContent
                {
                    Sequences[GetUnit.Key].Data = Source;
                    Sequences[GetUnit.Key].CanSkip = true;
                }
                else
                if (Preprocessor.IsNumeric(Source))//Skip pure numbers
                {
                    Sequences[GetUnit.Key].Data = Source;
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

                    //Match DataBase
                    string GetMatchResult = "";
                    if (Preprocessor.ExactMatch(From, To, GetUnit.Key, GetUnit.Type, Content, ref GetMatchResult))
                    {
                        Sequences[GetUnit.Key].Data = GetMatchResult;
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

                    string GetCacheStr = CloudDBCache.FindCache(Phoenix.GetFileUniqueKey(), Item.Key, To);

                    if (GetCacheStr.Trim().Length > 0)
                    {
                        Call.ReceiveString = GetCacheStr;

                        Call.Log = "Cache From Database";

                        Call.Output();

                        Sequences[GetUnit.Key].CanSkipSleep = true;

                        //Update AI memory
                        if (Source.Length > 0 && Phoenix.Config.ContextEnable)
                        {
                            EngineNode.AIMemory.AddTranslation(From, To, Item.Original, GetCacheStr);
                        }

                        Sequences[GetUnit.Key].Data = GetCacheStr;
                        Sequences[GetUnit.Key].CanSkip = true;
                        return;
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
                                EngineNode.AIMemory.AddTranslation(From, To, Item.Original, MatchItem.Result);
                            }

                            Sequences[GetUnit.Key].Data = MatchItem.Result;
                            Sequences[GetUnit.Key].CanSkip = true;
                            return;
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

                string Source = Sequences[GetUnit.Key].Data;

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

                    Sequences[GetUnit.Key].HavePlaceholder = true;

                    if (!CanTrans)
                    {
                        Sequences[GetUnit.Key].Data = Preprocessor.RestoreFromPlaceholder(Source, To);
                        Sequences[GetUnit.Key].HavePlaceholder = false;
                    }
                }
                else
                {
                    CanTrans = true;
                }

                if (!CanTrans)
                {
                    Sequences[GetUnit.Key].CanSkip = true;
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

                if (Sequences[GetUnit.Key].HavePlaceholder)
                {
                    Sequences[GetUnit.Key].Data = Preprocessor.RestoreFromPlaceholder(Sequences[GetUnit.Key].Data, To);
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
            TranslationPreprocessor Preprocessor,
            Languages From, Languages To,
            ref Dictionary<string, UnitSequence> Sequences)
        {
            for (int i = 0; i < Item.Units.Count; i++)
            {
                var GetUnit = Item.Units[i];

                string Translated = Sequences[GetUnit.Key].Data;

                Sequences[GetUnit.Key].Step = 3;

                if (!Sequences[GetUnit.Key].CanSkip)
                {
                    try
                    {
                        if (Preprocessor.HasUnicodeEscape(Translated))
                        {
                            Translated = Regex.Unescape(Translated);
                        }

                    }
                    catch
                    {
                        Translated = Item.Units[i].Original;
                        Sequences[GetUnit.Key].Data = Translated;
                        return;
                    }

                    Preprocessor.OptimizeStrings(ref Translated);
                    Translated = Translated.Trim();

                    if (Sequences[GetUnit.Key].HasOuterQuotes)
                    {
                        Translated = "\"" + Translated + "\"";
                    }

                    Translated = Preprocessor.ReturnStr(Translated);
                    Sequences[GetUnit.Key].Data = Translated;
                }
            }
        }
    }
}
