using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManagement;

namespace PhoenixEngine.TranslateManage
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
        public ReplaceTag(int Rowid,string Key, string Value)
        {
            this.Rowid = Rowid;
            this.Key = Key;
            this.Value = Value;
        }
    }

    public class TranslationPreprocessor : TranslationPreprocessorExtend
    {
        public bool HasPlaceholder = false;
        public string SourceStr = "";

        public List<ReplaceTag> ReplaceTags = new List<ReplaceTag>();

        public TranslationPreprocessor()
        {
        
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
                        string Placeholder = $"__P({Index})__";
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

            var ProtectedTags = GenerateProtectedTags(SourceStr,false);
            for (int i=0;i< ProtectedTags.Count;i++)
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
                string Placeholder = $"__({i})__";
                string Source = Word.Source;

                if (UseWordBoundary)
                {
                    string Pattern = Regex.Escape(Source);
                    if (Regex.IsMatch(SourceStr, Pattern, RegexOptions.IgnoreCase))
                    {
                        SourceStr = Regex.Replace(SourceStr, Pattern, Placeholder, RegexOptions.IgnoreCase);
                        ReplaceTags.Add(new ReplaceTag(Tags[i].Rowid,Placeholder, Word.Result));
                        HasPlaceholder = true;
                    }
                }
                else
                {
                    if (SourceStr.Contains(Source))
                    {
                        SourceStr = SourceStr.Replace(Source, Placeholder);
                        ReplaceTags.Add(new ReplaceTag(Placeholder, Word.Result));
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

            bool HasSpace = IsSpaceLanguage(Lang);

            StringBuilder Result = new StringBuilder(Str.Length);
            int I = 0;

            while (I < Str.Length)
            {
                if (Str[I] == '_' && I + 1 < Str.Length && Str[I + 1] == '_')
                {
                    int PrefixLength = 0;

                    if (I + 2 < Str.Length && Str[I + 2] == '(')
                    {
                        PrefixLength = 3;
                    }
                    else if (I + 3 < Str.Length && Str[I + 2] == 'P' && Str[I + 3] == '(')
                    {
                        PrefixLength = 4;
                    }

                    if (PrefixLength > 0)
                    {
                        int Start = I;
                        int J = I + PrefixLength;

                        while (J < Str.Length && char.IsDigit(Str[J]))
                            J++;

                        if (J + 2 < Str.Length &&
                            Str[J] == ')' &&
                            Str[J + 1] == '_' &&
                            Str[J + 2] == '_')
                        {
                            int TokenLength = J - Start + 3;
                            string Token = Str.Substring(Start, TokenLength);

                            string MatchToken = HasSpace
                                ? Token
                                : Regex.Replace(Token, @"\s+", "");

                            string MatchedKey = FindBestMatchingPlaceholder(MatchToken);

                            if (MatchedKey != null)
                            {
                                var Tag = ReplaceTags.FirstOrDefault(t => t.Key == MatchedKey);
                                if (Tag != null)
                                {
                                    Result.Append(Tag.Value);
                                    I += TokenLength;
                                    continue;
                                }
                            }
                        }
                    }
                }

                Result.Append(Str[I]);
                I++;
            }

            return Result.ToString();
        }

        private bool IsSpaceLanguage(Languages lang)
        {
            return lang == Languages.English ||
                   lang == Languages.German ||
                   lang == Languages.Italian ||
                   lang == Languages.Spanish;
        }
        private string FindBestMatchingPlaceholder(string Input)
        {
            foreach (var Tag in ReplaceTags)
            {
                string Key = Tag.Key;
                if (Input.Contains(Key) || Normalize(Input) == Normalize(Key))
                    return Key;
            }
            return null;
        }

        private string Normalize(string Input)
        {
            return ToHalfWidth(Input).Replace("_", "").Replace("(", "").Replace(")", "").ToUpperInvariant();
        }

        private string ToHalfWidth(string Input)
        {
            StringBuilder Sb = new StringBuilder();
            foreach (char C in Input)
            {
                if (C >= 0xFF01 && C <= 0xFF5E)
                    Sb.Append((char)(C - 0xFEE0));
                else if (C == 0x3000)
                    Sb.Append(' ');
                else
                    Sb.Append(C);
            }
            return Sb.ToString();
        }
    }
}
