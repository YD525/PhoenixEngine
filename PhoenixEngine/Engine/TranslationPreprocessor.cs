using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PhoenixEngine.ADO;
using PhoenixEngine.Translate;
using PhoenixEngine.TranslateCore;

namespace PhoenixEngine.Engine
{
    public class ReplaceTag
    {
        public int Rowid = 0;
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
        public ReplaceTag(string Key, string Value)
        {
            this.Key = Key;
            this.Value = Value;
        }
        public ReplaceTag(int Rowid, string Key, string Value)
        {
            this.Rowid = Rowid;
            this.Key = Key;
            this.Value = Value;
        }
    }

    public class TranslationPreprocessor : TranslationPreprocessorExtend
    {
        public static TranslationPreprocessor Instance = new TranslationPreprocessor();

        public bool HasPlaceholder = false;
        public string SourceStr = "";

        public List<ReplaceTag> ReplaceTags = new List<ReplaceTag>();

        public TranslationPreprocessor()
        {

        }

        public static TranslationPreprocessor Clone(TranslationPreprocessor Preprocessor)
        {
            TranslationPreprocessor NTranslationPreprocessor = new TranslationPreprocessor();
            NTranslationPreprocessor.HasPlaceholder = Preprocessor.HasPlaceholder;
            NTranslationPreprocessor.SourceStr = Preprocessor.SourceStr;
            NTranslationPreprocessor.ReplaceTags.AddRange(Preprocessor.ReplaceTags);
            return NTranslationPreprocessor;
        }

        public bool SecondaryQualityInspection(string Source, List<ReplaceTag> CustomWords)
        {
            if (string.IsNullOrEmpty(Source))
                return false;

            if (CustomWords == null || CustomWords.Count == 0)
                return true;

            HashSet<string> FoundIds = new HashSet<string>();

            string Pattern = @"[\[【\(（]\s*_\s*([Pp]?\d+)\s*[\]】\)）]";

            var Matches = Regex.Matches(Source, Pattern, RegexOptions.IgnoreCase);

            foreach (Match Match in Matches)
            {
                if (Match.Success)
                {
                    string FoundId = Match.Groups[1].Value.Trim().ToUpper();
                    FoundIds.Add(FoundId);
                }
            }

            HashSet<string> ExpectedIds = new HashSet<string>();

            foreach (var Word in CustomWords)
            {
                var IDMatch = Regex.Match(Word.Key, @"\[_([Pp]?\d+)\]");
                if (IDMatch.Success)
                {
                    string ID = IDMatch.Groups[1].Value.ToUpper();
                    ExpectedIds.Add(ID);
                }
            }

            return FoundIds.Count == ExpectedIds.Count && ExpectedIds.All(id => FoundIds.Contains(id));
        }

        private List<ReplaceTag> GenerateProtectedTags(string Source, bool IsAIPlatform)
        {
            var Tags = new List<ReplaceTag>();
            int Index = 0;

            foreach (var Pattern in Phoenix.Config.ProtectedPatterns)
            {
                var Matches = Regex.Matches(Source, Pattern);
                foreach (Match Match in Matches)
                {
                    if (!Match.Success)
                        continue;

                    string Value = Match.Value;

                    if (Tags.Any(T => T.Value == Value))
                        continue;

                    if (IsAIPlatform)
                    {
                        Tags.Add(new ReplaceTag(Value, Value));
                    }
                    else
                    {
                        string Placeholder = $"[_P{Index}]";
                        Tags.Add(new ReplaceTag(Placeholder, Value));
                        Index++;
                    }
                }
            }

            return Tags;
        }

        public string GeneratePlaceholderText(string FileName, Languages From, Languages To, string SourceStr, string Type, out bool NeedFurtherTranslate)
        {
            ReplaceTags.Clear();
            HasPlaceholder = false;

            bool UseWordBoundary = LanguageExtensions.IsSpaceDelimitedLanguage(From);

            var ProtectedTags = GenerateProtectedTags(SourceStr, false);
            for (int i = 0; i < ProtectedTags.Count; i++)
            {
                if (SourceStr.Contains(ProtectedTags[i].Value))
                {
                    var Source = ProtectedTags[i].Value;
                    var Placeholder = ProtectedTags[i].Key;

                    if (ProtectedTags[i].Value == string.Empty)
                    {
                        continue;
                    }

                    if (UseWordBoundary)
                    {
                        string Pattern = Regex.Escape(Source);
                        if (Regex.IsMatch(SourceStr, Pattern, RegexOptions.IgnoreCase))
                        {
                            SourceStr = SourceStr.Replace(Source, Placeholder);
                            ReplaceTags.Add(ProtectedTags[i]);
                            HasPlaceholder = true;
                        }
                    }
                    else
                    {
                        if (SourceStr.Contains(Source))
                        {
                            SourceStr = SourceStr.Replace(Source, Placeholder);
                            ReplaceTags.Add(ProtectedTags[i]);
                            HasPlaceholder = true;
                        }
                    }
                }
            }

            var Tags = AdvancedDictionary.Query(FileName, Type, From, To, SourceStr, UseWordBoundary);

            for (int i = 0; i < Tags.Count; i++)
            {
                var Word = Tags[i];
                string Placeholder = $"[_{i}]";
                string Source = Word.Source;

                if (UseWordBoundary)
                {
                    string Pattern = Regex.Escape(Source);
                    if (Regex.IsMatch(SourceStr, Pattern, RegexOptions.IgnoreCase))
                    {
                        SourceStr = Regex.Replace(SourceStr, Pattern, Placeholder, RegexOptions.IgnoreCase);
                        ReplaceTags.Add(new ReplaceTag(Tags[i].Rowid, Placeholder, Word.Result));
                        HasPlaceholder = true;
                    }
                }
                else
                {
                    if (SourceStr.Contains(Source))
                    {
                        SourceStr = SourceStr.Replace(Source, Placeholder);
                        ReplaceTags.Add(new ReplaceTag(Tags[i].Rowid, Placeholder, Word.Result));
                        HasPlaceholder = true;
                    }
                }
            }

            string Residual = SourceStr;

            foreach (var tag in ReplaceTags)
            {
                Residual = Residual.Replace(tag.Key, "");
            }

            Residual = Regex.Replace(Residual, @"[\s\u3000]", "");

            NeedFurtherTranslate = !string.IsNullOrWhiteSpace(Residual.Trim());

            this.SourceStr = SourceStr;
            return SourceStr;
        }

        public string RestoreFromPlaceholder(string Str, Languages Lang)
        {
            if (string.IsNullOrEmpty(Str) || ReplaceTags.Count == 0)
                return Str;

            Dictionary<string, string> IDToValueMap = new Dictionary<string, string>();

            foreach (var Tag in ReplaceTags)
            {
                var Match = Regex.Match(Tag.Key, @"\[_([Pp]?\d+)\]");
                if (Match.Success)
                {
                    string ID = Match.Groups[1].Value.ToUpper();
                    IDToValueMap[ID] = Tag.Value;
                }
            }

            string Pattern = @"[\[【\(（]\s*_\s*([Pp]?\d+)\s*[\]】\)）]";

            string Result = Regex.Replace(Str, Pattern, Match =>
            {
                string ID = Match.Groups[1].Value.Trim().ToUpper();

                if (IDToValueMap.TryGetValue(ID, out string Value))
                {
                    return Value;
                }

                return Match.Value;
            }, RegexOptions.IgnoreCase);

            return Result;
        }

        private bool IsSpaceLanguage(Languages lang)
        {
            return lang == Languages.English ||
                   lang == Languages.German ||
                   lang == Languages.Italian ||
                   lang == Languages.Spanish ||
                   lang == Languages.French ||
                   lang == Languages.Portuguese;
        }

        public bool ExactMatch(Languages From, Languages To, string Key, string Type, string Source, ref string Result)
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
    }
}