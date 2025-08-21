
using System.Text.RegularExpressions;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;
using static PhoenixEngine.TranslateManage.TransCore;

namespace PhoenixEngine.TranslateManage
{
    // Copyright (C) 2025 YD525
    // Licensed under the GNU GPLv3
    // See LICENSE for details
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

        public static string QuickTrans(TranslationUnit Item, ref bool CanSleep,bool IsBook = false)
        {
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

            bool HasOuterQuotes = TranslationPreprocessor.HasOuterQuotes(GetSourceStr.Trim());

            TranslationPreprocessor.ConditionalSplitCamelCase(ref Content);
            TranslationPreprocessor.RemoveInvisibleCharacters(ref Content);

            Languages SourceLanguage = Item.From;

            if (SourceLanguage == Languages.Auto)
            {
                SourceLanguage = LanguageHelper.DetectLanguageByLine(Content);
            }  

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
        public static bool ClearCloudCache(string ModName)
        {
            string SqlOrder = "Delete From CloudTranslation Where ModName = '" + ModName + "'";
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