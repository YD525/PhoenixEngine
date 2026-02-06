using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using PhoenixEngine.EngineManagement.Engine;
using PhoenixEngine.EngineManagement.Unit;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;
using PhoenixEngine.TranslateManagement;
using static PhoenixEngine.TranslateCore.LanguageHelper;

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
                bool SkipSign = false;

                if (Preprocessor.IsOnlySymbolsAndSpaces(Source))//Skip pure symbol content.
                {
                    Sequences[GetUnit.Key].Data = Source;
                    SkipSign = true;
                }
                else
                if (string.IsNullOrEmpty(Source))//SkipEmptyContent
                {
                    Sequences[GetUnit.Key].Data = Source;
                    SkipSign = true;
                }
                else
                if (Preprocessor.IsNumeric(Source))//Skip pure numbers
                {
                    Sequences[GetUnit.Key].Data = Source;
                    SkipSign = true;
                }

                if (!SkipSign)
                {
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
                        SkipSign = true;
                    }

                    Sequences[GetUnit.Key].CanSkip = SkipSign;
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
            Dictionary<string, UnitSequence> Sequences)
        {
            for (int i = 0; i < Item.Units.Count; i++)
            {
                var GetUnit = Item.Units[i];

                string Translated = Sequences[GetUnit.Key].Data;

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
