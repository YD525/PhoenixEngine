
using System.Text.RegularExpressions;
using PhoenixEngine.Engine;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManagement;
using static PhoenixEngine.TranslateManage.TransCore;

namespace PhoenixEngine.TranslateManage
{
    // Copyright (C) 2025 YD525
    // Licensed under the GNU GPLv3
    // See LICENSE for details
    //https://github.com/YD525/PhoenixEngine

    public enum PlatformType
    {
        Null = 0, ChatGpt = 1, DeepSeek = 2, Gemini = 3, DeepL = 5, GoogleApi = 7, Baichuan = 8, Cohere = 9, LMLocalAI = 10
    }
    public class Translator
    {
        public delegate void TranslateMsg(string EngineName, string Text, string Result);

        public static TranslateMsg SendTranslateMsg;

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

        public static string QuickTrans(string ModName,string Type, string Key,string Content,Languages From, Languages To, ref bool CanSleep, bool IsBook = false)
        {
            string GetSourceStr = Content;

            if (IsOnlySymbolsAndSpaces(GetSourceStr))
            {
                return GetSourceStr;
            }

            if (GetSourceStr.Trim().Length == 0)
            {
                return GetSourceStr;
            }

            bool HasOuterQuotes = TranslationPreprocessor.HasOuterQuotes(GetSourceStr.Trim());

            TranslationPreprocessor.ConditionalSplitCamelCase(ref Content);
            TranslationPreprocessor.RemoveInvisibleCharacters(ref Content);

            Languages SourceLanguage = From;
            if (SourceLanguage == Languages.Auto)
            {
                SourceLanguage = LanguageHelper.DetectLanguageByLine(Content);
            }  
            if (SourceLanguage == To)
            {
                return GetSourceStr;
            }

            if (TranslationPreprocessor.IsNumeric(Content))
            {
                return GetSourceStr;
            }

            bool CanAddCache = true;
            Content = CurrentTransCore.TransAny(Type, Key,SourceLanguage, To, Content, IsBook, ref CanAddCache, ref CanSleep);

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

            if (CanAddCache && Content.Trim().Length > 0)
            {
                CloudDBCache.AddCache(ModName, Key, (int)To, Content);
            }

            return Content;
        }
        public static bool ClearCloudCache(string ModName)
        {
            string SqlOrder = "Delete From CloudTranslation Where ModName = '" + ModName + "'";
            int State = EngineConfig.LocalDB.ExecuteNonQuery(SqlOrder);
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
                            SetData(GetHashKey, Translator.TransData[GetHashKey].Trim());
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

        public static void SetData(string GetKey, string TransData)
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