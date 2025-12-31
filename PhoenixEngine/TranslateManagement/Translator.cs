
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.GameManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManagement;
using static PhoenixEngine.EngineManagement.DataTransmission;
using static PhoenixEngine.TranslateManage.TransCore;
using static PhoenixEngine.TranslateManagement.ChunkHelper;

namespace PhoenixEngine.TranslateManage
{
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
            TranslationPreprocessor.OptimizeStrings(ref Content);
            return Content;
        }

        public static bool ExactMatch(Languages From, Languages To, string Key, string Type, string Source, ref string Result)
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

                NPreTranslateCall.ReplaceTags.Add(new ReplaceTag(GetData.Rowid, GetData.Source, GetData.Result));

                NPreTranslateCall.Output();

                Result = GetData.Result;

                return true;
            }

            return false;
        }

        public static bool IsBook(TranslationUnit Item,ref Game DetectGame)
        {
            if (Item.Type == "BOOK" && Item.Key.EndsWith("DESC"))
            {
                return true;
            }

            return false;
        }

        public static List<TranslationUnit> ChunkTranslationUnit(Game GameType,TranslationUnit Unit,ref List<UnitChunk> Chunks)
        {
            if (GameType == Game.Skyrim)
            {
                Chunks = new SkyrimBookHelper().ChunkBook(Unit);
            }
            List<TranslationUnit> Units = new List<TranslationUnit>();
            foreach (UnitChunk Chunk in Chunks)
            {
                if (!Chunk.IsCode)
                {
                    Units.Add(
                    new TranslationUnit(
                       Unit.FileUniqueKey,
                       Chunk.Key,
                       Unit.Type,
                       Chunk.Data,
                       string.Empty,
                       Unit.AIParam,
                       Unit.From,
                       Unit.To,
                       Unit.Score
                   ));
                }
            }

            return Units;
        }

        public static string QuickTrans(TranslationUnit Item, ref bool CanSleep)
        {
            List<TranslationUnit> Units = new List<TranslationUnit>();
            List<UnitChunk> Chunks = new List<UnitChunk>();
            Game GameType = Game.Null;

            if (IsBook(Item,ref GameType))
            {
                Units.AddRange(ChunkTranslationUnit(GameType,Item,ref Chunks));
            }
            else
            {
                Units.Add(Item);
            }

            string MergeLine = "";

            foreach (var GetUnit in Units)
            {
                Regex Regex = new Regex(@"\{([A-Za-z0-9_ ]+)\}");

                if (Regex.IsMatch(Item.SourceText))
                {
                    Item.SourceText = Regex.Replace(Item.SourceText, @"$$$$$1$$$$");
                }

                //Skip fields that do not need translation

                string GetSourceStr = Item.SourceText;

                if (IsOnlySymbolsAndSpaces(GetSourceStr))
                {
                    return GetSourceStr;
                }

                if (string.IsNullOrEmpty(GetSourceStr))
                {
                    return GetSourceStr;
                }

                Languages SourceLanguage = Item.From;
                if (SourceLanguage == Item.To)
                {
                    return GetSourceStr;
                }

                if (TranslationPreprocessor.IsNumeric(GetSourceStr))
                {
                    return GetSourceStr;
                }

                //Optimize strings
                TranslationPreprocessor.OptimizeStrings(ref GetSourceStr);

                //Check OuterQuotes
                bool HasOuterQuotes = TranslationPreprocessor.HasOuterQuotes(GetSourceStr.Trim());

                //Remove OuterQuotes
                if (HasOuterQuotes)
                {
                    TranslationPreprocessor.StripOuterQuotes(ref GetSourceStr);
                }

                //Match DataBase
                string Content = GetSourceStr;
                string GetMatchResult = "";
                if (ExactMatch(Item.From, Item.To, Item.Key, Item.Type, Content, ref GetMatchResult))
                {
                    return GetMatchResult;
                }

                Item.SourceText = Content;
                Content = CurrentTransCore.TransAny(Item, ref CanSleep);

                try
                {
                    if (TranslationPreprocessor.HasUnicodeEscape(Content))
                    {
                        Content = Regex.Unescape(Content);
                    }
                }
                catch { Content = string.Empty; }

                TranslationPreprocessor.OptimizeStrings(ref Content);
                TranslationPreprocessor.StripOuterQuotes(ref Content);

                Content = Content.Trim();

                TranslationPreprocessor.OptimizeStrings(ref Content);

                if (HasOuterQuotes)
                {
                    Content = "\"" + Content + "\"";
                }

                Content = ReturnStr(Content);

                if (Chunks.Count > 0)
                {
                    for (int i = 0; i < Chunks.Count; i++)
                    {
                        if (Chunks[i].Equals(Item.Key))
                        {
                            int SetNextOffset = 0;

                            while (Chunks.Count > SetNextOffset)
                            {
                                SetNextOffset = (i + 1) + 1;

                                if (Chunks.Count > SetNextOffset)
                                {
                                    if (Chunks[SetNextOffset].IsCode)
                                    {
                                        MergeLine += Content;
                                        MergeLine += Chunks[SetNextOffset];
                                    }
                                    else
                                    {
                                        MergeLine += Content;
                                        break;
                                    }
                                }
                            }

                            break;
                        }
                    }
                }
                else
                {
                    MergeLine += Content;
                }   
            }

            return MergeLine;
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