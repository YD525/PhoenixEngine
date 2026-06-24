using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PhoenixEngine.ADO
{
    public static class SQLSafeCodec
    {
        public static string EncodeSQLValues(string Sql)
        {
            if (string.IsNullOrEmpty(Sql)) return Sql;

            string ProcessedSql = Sql;
            int SearchPos = 0;

            var HandledRanges = new List<(int Start, int End)>();

            while (true)
            {
                int FindLike = ProcessedSql.IndexOf("LIKE", SearchPos, StringComparison.OrdinalIgnoreCase);
                int FindGlob = ProcessedSql.IndexOf("GLOB", SearchPos, StringComparison.OrdinalIgnoreCase);

                int FoundIdx = -1;
                bool IsGlobMode = false;

                if (FindLike >= 0 && (FindGlob < 0 || FindLike < FindGlob))
                { FoundIdx = FindLike; IsGlobMode = false; }
                else if (FindGlob >= 0)
                { FoundIdx = FindGlob; IsGlobMode = true; }

                if (FoundIdx == -1) break;

                while (FoundIdx >= 0)
                {
                    const int KeywordLen = 4;

                    bool LeftOk = FoundIdx == 0
                                  || !char.IsLetterOrDigit(ProcessedSql[FoundIdx - 1]);

                    char RightChar = (FoundIdx + KeywordLen < ProcessedSql.Length)
                                     ? ProcessedSql[FoundIdx + KeywordLen]
                                     : '\0';
                    bool RightOk = RightChar == '\0'
                                   || (!char.IsLetterOrDigit(RightChar) && RightChar != '=');

                    if (LeftOk && RightOk) break;

                    SearchPos = FoundIdx + 1;
                    FindLike = ProcessedSql.IndexOf("LIKE", SearchPos, StringComparison.OrdinalIgnoreCase);
                    FindGlob = ProcessedSql.IndexOf("GLOB", SearchPos, StringComparison.OrdinalIgnoreCase);

                    FoundIdx = -1;
                    IsGlobMode = false;

                    if (FindLike >= 0 && (FindGlob < 0 || FindLike < FindGlob))
                    { FoundIdx = FindLike; IsGlobMode = false; }
                    else if (FindGlob >= 0)
                    { FoundIdx = FindGlob; IsGlobMode = true; }
                }

                if (FoundIdx == -1) break;

                int QuoteStart = -1;
                char QuoteChar = '\0';

                for (int I = FoundIdx + 4; I < ProcessedSql.Length; I++)
                {
                    char C = ProcessedSql[I];
                    if (C == '\'' || C == '\"') { QuoteStart = I; QuoteChar = C; break; }
                    if (C == ';' || C == ')') break;
                }

                if (QuoteStart >= 0)
                {
                    int QuoteEnd = FindClosingQuote(ProcessedSql, QuoteStart);
                    if (QuoteEnd > QuoteStart)
                    {
                        string OriginalContent = ProcessedSql.Substring(QuoteStart + 1, QuoteEnd - QuoteStart - 1);
                        char SplitChar = IsGlobMode ? '*' : '%';
                        string[] Parts = OriginalContent.Split(SplitChar);

                        for (int J = 0; J < Parts.Length; J++)
                        {
                            if (string.IsNullOrEmpty(Parts[J])) continue;

                            if (IsGlobMode)
                            {
                                string GlobPattern = @"(\?|\[.+?\])";
                                string[] SubParts = Regex.Split(Parts[J], GlobPattern);
                                for (int K = 0; K < SubParts.Length; K++)
                                {
                                    if (string.IsNullOrEmpty(SubParts[K])) continue;
                                    if (!Regex.IsMatch(SubParts[K], "^" + GlobPattern + "$"))
                                    {
                                        SubParts[K] = SubParts[K].Replace("''", "'").Replace("\"\"", "\"");
                                        SubParts[K] = SQLSafeCodec.Encode(SubParts[K]);
                                    }
                                }
                                Parts[J] = string.Concat(SubParts);
                            }
                            else
                            {
                                string LikePattern = @"(_)";
                                string[] SubParts = Regex.Split(Parts[J], LikePattern);
                                for (int K = 0; K < SubParts.Length; K++)
                                {
                                    if (string.IsNullOrEmpty(SubParts[K])) continue;
                                    if (SubParts[K] != "_")
                                    {
                                        SubParts[K] = SubParts[K].Replace("''", "'").Replace("\"\"", "\"");
                                        SubParts[K] = SQLSafeCodec.Encode(SubParts[K]);
                                    }
                                }
                                Parts[J] = string.Concat(SubParts);
                            }
                        }

                        string EncodedContent = string.Join(SplitChar.ToString(), Parts);
                        string Replacement = $"{QuoteChar}{EncodedContent}{QuoteChar}";
                        string Before = ProcessedSql.Substring(0, QuoteStart);
                        string After = ProcessedSql.Substring(QuoteEnd + 1);

                        ProcessedSql = Before + Replacement + After;

                        int NewEnd = QuoteStart + Replacement.Length - 1;
                        HandledRanges.Add((QuoteStart, NewEnd));
                        SearchPos = NewEnd + 1;
                    }
                    else SearchPos = FoundIdx + 4;
                }
                else SearchPos = FoundIdx + 4;

                if (SearchPos >= ProcessedSql.Length) break;
            }

            string SqlStringLiteral = @"(['""])(?:(?!\1).|\1\1)*?\1";

            return Regex.Replace(ProcessedSql, SqlStringLiteral, M =>
            {
                if (HandledRanges.Any(R => M.Index >= R.Start && M.Index <= R.End))
                    return M.Value;

                string FullValue = M.Value;
                char Q = FullValue[0];
                string Content = FullValue.Substring(1, FullValue.Length - 2);

                Content = Content.Replace("''", "'").Replace("\"\"", "\"");
                return $"{Q}{SQLSafeCodec.Encode(Content)}{Q}";
            }, RegexOptions.Singleline);
        }

        private static int FindClosingQuote(string Sql, int OpenPos)
        {
            char Q = Sql[OpenPos];
            int I = OpenPos + 1;
            while (I < Sql.Length)
            {
                if (Sql[I] == Q)
                {
                    if (I + 1 < Sql.Length && Sql[I + 1] == Q)
                        I += 2;
                    else
                        return I;
                }
                else I++;
            }
            return -1;
        }

        private static readonly char[] DangerChars = new char[]
        {
        '\'', '\"', ';', '-', '#', '/', '\\', '%', '_', '=', '<', '>', '!',
        '|', '&', '(', ')', '[', ']', '\r', '\n', '\0'
        };

        private static readonly Dictionary<char, char> EncodeMap;
        private static readonly Dictionary<char, char> DecodeMap;
        private static readonly HashSet<char> EncodedSet;

        static SQLSafeCodec()
        {
            EncodeMap = new Dictionary<char, char>(DangerChars.Length);
            DecodeMap = new Dictionary<char, char>(DangerChars.Length);
            EncodedSet = new HashSet<char>();

            int baseCode = 0xE000;
            for (int i = 0; i < DangerChars.Length; i++)
            {
                char source = DangerChars[i];
                char mapped = (char)(baseCode + i);
                EncodeMap[source] = mapped;
                DecodeMap[mapped] = source;
                EncodedSet.Add(mapped);
            }
        }

        public static bool IsEncoded(string input)
        {
            if (string.IsNullOrEmpty(input)) return false;
            foreach (var c in input)
            {
                if (EncodedSet.Contains(c)) return true;
            }
            return false;
        }

        public static string Encode(string input)
        {
            if (input == null) return null;
            if (input.Length == 0) return string.Empty;

            if (IsEncoded(input)) return input;

            var sb = new StringBuilder(input.Length);
            foreach (var c in input)
            {
                if (EncodeMap.TryGetValue(c, out var m))
                    sb.Append(m);
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

        public static string Decode(string input)
        {
            if (input == null) return null;
            if (input.Length == 0) return string.Empty;

            var sb = new StringBuilder(input.Length);
            foreach (var c in input)
            {
                if (DecodeMap.TryGetValue(c, out var o))
                    sb.Append(o);
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

        public static string EncodeForSqlLiteral(string input)
        {
            var s = Encode(input) ?? string.Empty;
            return $"'{s}'";
        }
    }
}
