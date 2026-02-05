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
        Null = 0, ChatGpt = 1, DeepSeek = 2, Gemini = 3, DeepL = 5, GoogleApi = 7, Baichuan = 8, Cohere = 9, LMLocalAI = 10, PhoenixEngine = 11, CustomPlatform = 12
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

        public static bool IsSkyrimBook(TranslationUnit Item,ref Game DetectGame)
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

        public static string QuickTrans(TranslationPreprocessor Preprocessor, TranslationUnit Item, ref bool CanSleep)
        {
            List<TranslationUnit> Units = new List<TranslationUnit>();
            List<UnitChunk> Chunks = new List<UnitChunk>();
            Game GameType = Game.Null;

            bool Book = false;

            if (IsSkyrimBook(Item,ref GameType))
            {
                GameType = Game.Skyrim;
                Book = true;
                Units.AddRange(ChunkTranslationUnit(GameType,Item,ref Chunks));
            }
            else
            {
                Units.Add(Item);
            }

            string MergeLine = "";

            if (Chunks.Count > 0)
            {
                //It is necessary to prevent the preceding lines of code from being lost.
                foreach (var GetChunk in Chunks)
                {
                    if (GetChunk.IsCode)
                    {
                        MergeLine += GetChunk.Data;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            foreach (var GetUnit in Units)
            {
                bool CanSkip = false;
                //Skip fields that do not need translation

                Languages SourceLanguage = GetUnit.From;
                string GetSourceStr = GetUnit.SourceText;

                if (Preprocessor.IsOnlySymbolsAndSpaces(GetSourceStr))
                {
                    CanSkip = true;
                }
                else
                if (string.IsNullOrEmpty(GetSourceStr))
                {
                    CanSkip = true;
                }
                else
                if (SourceLanguage == GetUnit.To)
                {
                    CanSkip = true;
                }
                else
                if (Preprocessor.IsNumeric(GetSourceStr))
                {
                    CanSkip = true;
                }

                string Content = GetSourceStr;

                if (!CanSkip)
                {
                    //Optimize strings
                    Preprocessor.OptimizeStrings(ref Content);

                    //Check OuterQuotes
                    bool HasOuterQuotes = Preprocessor.HasOuterQuotes(Content.Trim());

                    //Remove OuterQuotes
                    if (HasOuterQuotes)
                    {
                        Preprocessor.StripOuterQuotes(ref Content);
                    }

                    //Match DataBase
                    string GetMatchResult = "";
                    if (ExactMatch(GetUnit.From, GetUnit.To, GetUnit.Key, GetUnit.Type, Content, ref GetMatchResult))
                    {
                        Content = GetMatchResult;
                        CanSkip = true;
                    }

                    if (!CanSkip)
                    {
                        GetUnit.SourceText = Content;

                        Content = CurrentTransCore.TransAny(Preprocessor,GetUnit, ref CanSleep, Book);

                        try
                        {
                            if (Preprocessor.HasUnicodeEscape(Content))
                            {
                                Content = Regex.Unescape(Content);
                            }
                        }
                        catch { Content = string.Empty; }

                        Preprocessor.OptimizeStrings(ref Content);
                        Preprocessor.StripOuterQuotes(ref Content);

                        Content = Content.Trim();

                        Preprocessor.OptimizeStrings(ref Content);

                        if (HasOuterQuotes)
                        {
                            Content = "\"" + Content + "\"";
                        }

                        Content = Preprocessor.ReturnStr(Content);
                    }
                }

                if (Chunks.Count > 0)
                {
                    for (int i = 0; i < Chunks.Count; i++)
                    {
                        if (Chunks[i].Key.Equals(GetUnit.Key))
                        {
                            MergeLine += Content + "\n";

                            for (int j = i + 1; j < Chunks.Count; j++)
                            {
                                if (Chunks[j].IsCode)
                                {
                                    MergeLine += Chunks[j].Data;
                                }
                                else
                                {
                                    break;
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

            new TranslationPreprocessor().NormalizePunctuation(ref NewStr);

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