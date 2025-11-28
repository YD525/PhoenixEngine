
using System.Text.RegularExpressions;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManagement;
using static PhoenixEngine.EngineManagement.DataTransmission;
using static PhoenixEngine.TranslateManage.TransCore;

namespace PhoenixEngine.TranslateManage
{
    // Copyright (c) 2025 YD525
    // Licensed under the MIT License.
    // See LICENSE file in the project root for full license information.
    //https://github.com/YD525/PhoenixEngine

    public enum PlatformType
    {
        Null = 0, ChatGpt = 1, DeepSeek = 2, Gemini = 3, DeepL = 5, GoogleApi = 7, Baichuan = 8, Cohere = 9, LMLocalAI = 10, PhoenixEngine = 11
    }
    public class Translator
    {
        public static readonly object TransDataLocker = new object();

        public static Dictionary<string, string> TransData = new Dictionary<string, string>();

        public static void ClearCache()
        {
            TransData.Clear();
        }

        public static void ClearAICache()
        {
            EngineSelect.AIMemory.Clear();
        }

        public static TransCore CurrentTransCore = new TransCore();

        public static string ReturnStr(string Str)
        {
            if (string.IsNullOrWhiteSpace(Str.Replace("　", "")))
            {
                return string.Empty;
            }
            else
            {
                return Str;
            }
        }

        public static bool IsOnlySymbolsAndSpaces(string Input)
        {
            return Regex.IsMatch(Input, @"^[\p{P}\p{S}\s]+$");
        }

        public static string FormatStr(string Content)
        {
            Content = Content.Replace("{", "%%").Replace("}", "%%");

            string GetSourceStr = Content;

            bool HasOuterQuotes = TranslationPreprocessor.HasOuterQuotes(GetSourceStr.Trim());

            TranslationPreprocessor.ConditionalSplitCamelCase(ref Content);
            TranslationPreprocessor.RemoveInvisibleCharacters(ref Content);

            if (TranslationPreprocessor.IsNumeric(Content))
            {
                return GetSourceStr;
            }

            TranslationPreprocessor.NormalizePunctuation(ref Content);
            TranslationPreprocessor.ProcessEmptyEndLine(ref Content);
            TranslationPreprocessor.RemoveInvisibleCharacters(ref Content);

            TranslationPreprocessor.StripOuterQuotes(ref Content);

            Content = Content.Trim();

            if (HasOuterQuotes)
            {
                Content = "\"" + HasOuterQuotes + "\"";
            }

            Content = ReturnStr(Content);

            TranslationPreprocessor.ProcessEscapeCharacters(ref Content);

            return Content;
        }

        public static bool ExactMatch(Languages From,Languages To,string Key,string Type,string Source,ref string Result)
        {
            var GetData = AdvancedDictionary.ExactMatch(From, To, Type, Source);
            if (GetData != null)
            {
                PreTranslateCall NPreTranslateCall = new PreTranslateCall();
                NPreTranslateCall.Platform = PlatformType.PhoenixEngine;
                NPreTranslateCall.FromAI = false;
                NPreTranslateCall.Key = Key;

                string GetDefSource = Source;

                NPreTranslateCall.SendString = GetDefSource;

                NPreTranslateCall.ReceiveString = Source;

                NPreTranslateCall.ReplaceTags.Add(new ReplaceTag(GetData.Rowid,GetData.Source,GetData.Result));

                NPreTranslateCall.Output();

                Result = GetData.Result;

                return true;
            }

            return false;
        }

     

        public static string QuickTrans(TranslationUnit Item, ref bool CanSleep,bool IsBook = false)
        {
            Regex Regex = new Regex(@"\{([A-Za-z0-9_]+)\}");

            if (Regex.IsMatch(Item.SourceText))
            {
                Item.SourceText = Regex.Replace(Item.SourceText, @"$$$1$$");
            }

            string GetSourceStr = Item.SourceText;
            string Content = Item.SourceText;

            if (IsOnlySymbolsAndSpaces(GetSourceStr))
            {
                return GetSourceStr;
            }

            if (string.IsNullOrEmpty(GetSourceStr))
            {
                return GetSourceStr;
            }

            string GetMatchResult = "";

            if (ExactMatch(Item.From,Item.To,Item.Key, Item.Type, GetSourceStr, ref GetMatchResult))
            {
                return GetMatchResult;
            }

            bool HasOuterQuotes = TranslationPreprocessor.HasOuterQuotes(GetSourceStr.Trim());

            TranslationPreprocessor.ConditionalSplitCamelCase(ref Content);
            TranslationPreprocessor.RemoveInvisibleCharacters(ref Content);

            Languages SourceLanguage = Item.From;

            if (SourceLanguage == Item.To)
            {
                return GetSourceStr;
            }

            if (TranslationPreprocessor.IsNumeric(Content))
            {
                return GetSourceStr;
            }

            Item.From = SourceLanguage;

            Item.SourceText = Content;

            Content = CurrentTransCore.TransAny(Item,ref CanSleep, IsBook);

            TranslationPreprocessor.NormalizePunctuation(ref Content);
            TranslationPreprocessor.ProcessEmptyEndLine(ref Content);
            TranslationPreprocessor.RemoveInvisibleCharacters(ref Content);

            TranslationPreprocessor.StripOuterQuotes(ref Content);

            Content = Content.Trim();

            if (HasOuterQuotes)
            {
                Content = "\"" + HasOuterQuotes + "\"";
            }

            Content = ReturnStr(Content);

            TranslationPreprocessor.ProcessEscapeCharacters(ref Content);

            return Content;
        }
        public static bool ClearCloudCache(int FileUniqueKey)
        {
            string SqlOrder = "Delete From CloudTranslation Where FileUniqueKey = " + FileUniqueKey + "";
            int State = Engine.LocalDB.ExecuteNonQuery(SqlOrder);
            if (State != 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static void FormatData()
        {
            try
            {
                for (int i = 0; i < Translator.TransData.Count; i++)
                {
                    try
                    {
                        var GetHashKey = Translator.TransData.ElementAt(i).Key;
                        if (Translator.TransData[GetHashKey].Trim().Length > 0)
                        {
                            FormatData(GetHashKey, Translator.TransData[GetHashKey].Trim());
                        }
                    }
                    catch (System.Exception ex)
                    {
                        System.Console.WriteLine($"Error in WriteAllMemoryData loop at index {i}: {ex.Message}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Error in WriteAllMemoryData: {ex.Message}");
            }
        }

        public static void FormatData(string GetKey, string TransData)
        {
            string NewStr = TransData;

            TranslationPreprocessor.NormalizePunctuation(ref NewStr);

            if (Regex.Replace(NewStr, @"\s+", "").Length > 0)
            {
                Translator.TransData[GetKey] = NewStr;
            }
            else
            {
                Translator.TransData[GetKey] = string.Empty;
            }
        }

    }
}