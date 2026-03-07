using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PhoenixEngine.Language;

namespace PhoenixEngine.Engine
{
    public static class LanguageExtensions
    {
        public static bool IsSpaceDelimitedLanguage(this Languages Lang)
        {
            return Lang == Languages.German ||
            Lang == Languages.English ||
            Lang == Languages.Turkish ||
            Lang == Languages.Brazilian ||
            Lang == Languages.Russian ||
            Lang == Languages.Italian ||
            Lang == Languages.Spanish ||
            Lang == Languages.Indonesian ||
            Lang == Languages.Hindi ||
            Lang == Languages.Urdu ||
            Lang == Languages.French ||
            Lang == Languages.Vietnamese ||
            Lang == Languages.Polish ||
            Lang == Languages.Persian;
        }
        public static bool IsNoSpaceLanguage(this Languages Lang)
        {
            return Lang == Languages.Japanese ||
            Lang == Languages.Korean ||
            Lang == Languages.TraditionalChinese ||
            Lang == Languages.Thai ||
            Lang == Languages.SimplifiedChinese;
        }
    }

    public class TextTokenizer
    {

        private static readonly HashSet<string> EnglishStopWords =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a","an","the",
            "and","or","but","if","then","else","while","when","because","since","unless",

            "on","in","at","to","for","with","by","of","from","into","onto","upon",
            "about","around","through","across","between","among","against",
            "over","under","above","below","before","after","during","within","without",

            "i","me","my","mine","myself",
            "you","your","yours","yourself","yourselves",
            "he","him","his","himself",
            "she","her","hers","herself",
            "it","its","itself",
            "we","us","our","ours","ourselves",
            "they","them","their","theirs","themselves",
            "this","that","these","those","which","who","whom","whose","what",

            "is","are","was","were","be","been","being",
            "has","have","had","having",
            "do","does","did","doing",

            "can","could","may","might","must",
            "will","would","shall","should",

            "not","no","nor","never","none","nothing","nobody",

            "very","too","so","just","only","even","still","also","already","yet",
            "again","ever","always","often","sometimes","usually","rarely",

            "all","any","some","none","each","every","either","neither",
            "much","many","few","less","more","most","several",

            "now","today","tomorrow","yesterday",
            "current","previous","next","new","old",

            "as","than","like","via","per"
        };

        public static HashSet<string> BuildTokenSignature(Languages Lang,string Text,int MinTokenLength = 2)
        {
            if (string.IsNullOrWhiteSpace(Text))
                return new HashSet<string>();

            var tokens = TextTokenizer.Tokenize(Lang, Text);

            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var t in tokens)
            {
                if (t.Length < MinTokenLength)
                    continue;

                result.Add(t.ToLowerInvariant());
            }

            return result;
        }

        public const int MaxGram = 3 + 3;

        private static string[] Tokenize(Languages Lang, string Text)
        {
            if (Lang == Languages.Auto)
            {
                Lang = P_Language.DetectLanguageByLine(Text);
            }

            Text = Text.Replace('_', ' ').Replace('-', ' ');

            if (Lang.IsSpaceDelimitedLanguage())
            {
                Text = Regex.Replace(Text, "(?<!^)([A-Z])", " $1");
                var tokens = Text.Split(new[] { ' ', '.', ',', '?', '!', ';', ':', '(', ')', '[', ']', '{', '}', '"', '\'' },
                    StringSplitOptions.RemoveEmptyEntries);

                return tokens
                    .Where(t => t.Length > 1)
                    .Where(t => !(Lang == Languages.English && EnglishStopWords.Contains(t)))
                    .ToArray();
            }

            if (!Lang.IsNoSpaceLanguage())
            {
                return Text.Split(new[] { ' ', '.', ',', '?', '!', ';', ':', '(', ')', '[', ']', '{', '}', '"', '\'' },
                    StringSplitOptions.RemoveEmptyEntries)
                    .Where(t => t.Length > 1)
                    .ToArray();
            }

            List<(string Token, int Index)> TokensWithIndex = new List<(string Token, int Index)>();
            for (int I = 0; I < Text.Length; I++)
            {
                TokensWithIndex.Add((" " + Text[I] + " ", I));
            }

            List<string> Result = new List<string>();

            string[] SingleTokens = TokensWithIndex.Select(T => T.Token).ToArray();
            int[] Indices = TokensWithIndex.Select(T => T.Index).ToArray();

            for (int I = 0; I < SingleTokens.Length; I++)
            {
                for (int Len = 1; Len <= MaxGram && I + Len <= SingleTokens.Length; Len++)
                {
                    bool IsContinuous = true;
                    bool UsedOffset = false;

                    for (int J = I; J < I + Len - 1; J++)
                    {
                        int Diff = Indices[J + 1] - Indices[J];

                        if (Diff == 1)
                        {
                            continue;
                        }
                        else if (Diff == 2 && !UsedOffset)
                        {
                            UsedOffset = true;
                            continue;
                        }
                        else
                        {
                            IsContinuous = false;
                            break;
                        }
                    }

                    if (!IsContinuous)
                        continue;

                    var TokenSb = new System.Text.StringBuilder();
                    for (int K = I; K < I + Len; K++)
                    {
                        TokenSb.Append(SingleTokens[K]);
                    }

                    string Token = TokenSb.ToString().Replace(" ", "");

                    if (!string.IsNullOrWhiteSpace(Token) && Token.Length > 1)
                    {
                        Result.Add(Token);
                    }
                }
            }

            return Result.ToArray();
        }
    }
}
