using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PhoenixEngine.Engine;
using PhoenixEngine.Language;
using PhoenixEngine.Unit;

namespace PhoenixEngine.EngineManagement.Engine
{
    public static class BaseUnitExtend
    {
        public static HashSet<string> ExtractTokens(this BaseUnit Unit)
        {
            return TextTokenizer.BuildTokenSignature(Phoenix.From, Unit.Original, 0);
        }
    }

    internal class ContextProc
    {
        #region Words Analysis

        public static int SortLeadersAndCalculateThreads(Languages Lang, int NeedTrd, ref Dictionary<string, BaseUnit> Leaders)
        {
            var SortedPairs = new List<KeyValuePair<string, BaseUnit>>();
            var TokenMap = new Dictionary<BaseUnit, HashSet<string>>();

            foreach (var kv in Leaders)
            {
                var Unit = kv.Value;
                SortedPairs.Add(kv);

                TokenMap[Unit] = TextTokenizer.BuildTokenSignature(Lang, Unit.Original);
            }

            // Sort leaders by priority (Tokens count -> TempSim -> Length)
            SortedPairs.Sort((A, B) =>
            {
                var UA = A.Value;
                var UB = B.Value;

                int C = TokenMap[UB].Count.CompareTo(TokenMap[UA].Count);
                if (C != 0) return C;

                C = UB.TempSim.CompareTo(UA.TempSim);
                if (C != 0) return C;

                return UB.Original.Length.CompareTo(UA.Original.Length);
            });

            // --- Step 1: Build conflict matrix ---
            int N = SortedPairs.Count;
            bool[,] ConflictMatrix = new bool[N, N];

            for (int i = 0; i < N; i++)
            {
                var Ti = TokenMap[SortedPairs[i].Value];
                for (int j = i + 1; j < N; j++)
                {
                    var Tj = TokenMap[SortedPairs[j].Value];
                    int overlap = Ti.Count(t => Tj.Contains(t));
                    if (overlap >= 2) // Strong conflict threshold
                        ConflictMatrix[i, j] = ConflictMatrix[j, i] = true;
                }
            }

            // --- Step 2: Initialize threads ---
            var Threads = new List<List<KeyValuePair<string, BaseUnit>>>();
            int ThreadCount = Math.Min(NeedTrd, Math.Max(1, (int)Math.Sqrt(N))); //Empirical formula for initial thread count
            for (int i = 0; i < ThreadCount; i++)
                Threads.Add(new List<KeyValuePair<string, BaseUnit>>());

            // --- Step 3: Greedy assignment with global minimal conflict ---
            for (int idx = 0; idx < N; idx++)
            {
                var LeaderPair = SortedPairs[idx];

                int BestThread = 0;
                int MinConflictCount = int.MaxValue;

                for (int t = 0; t < Threads.Count; t++)
                {
                    int ConflictCount = 0;
                    foreach (var Existing in Threads[t])
                    {
                        int existingIdx = SortedPairs.IndexOf(Existing);
                        if (ConflictMatrix[idx, existingIdx])
                            ConflictCount++;
                    }

                    if (ConflictCount < MinConflictCount)
                    {
                        MinConflictCount = ConflictCount;
                        BestThread = t;
                    }
                }

                Threads[BestThread].Add(LeaderPair);
            }

            // --- Step 4: Optional: redistribute to fill <= NeedTrd threads ---
            while (Threads.Count > NeedTrd)
            {
                int mergeA = 0, mergeB = 1;
                int minOverlap = int.MaxValue;

                for (int i = 0; i < Threads.Count; i++)
                {
                    for (int j = i + 1; j < Threads.Count; j++)
                    {
                        int overlap = 0;
                        foreach (var a in Threads[i])
                            foreach (var b in Threads[j])
                                if (ConflictMatrix[SortedPairs.IndexOf(a), SortedPairs.IndexOf(b)])
                                    overlap++;
                        if (overlap < minOverlap)
                        {
                            minOverlap = overlap;
                            mergeA = i;
                            mergeB = j;
                        }
                    }
                }

                Threads[mergeA].AddRange(Threads[mergeB]);
                Threads.RemoveAt(mergeB);
            }

            // --- Step 5: Rebuild Leaders dictionary in sorted order ---
            var NewLeaders = new Dictionary<string, BaseUnit>(Leaders.Count);
            foreach (var thread in Threads)
                foreach (var kv in thread)
                    NewLeaders[kv.Key] = kv.Value;

            Leaders = NewLeaders;

            // Return approximate minimal thread count, with slight tolerated conflicts
            return Threads.Count;
        }

        public static int CountWords(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            int count = 0;
            bool inWord = false;

            foreach (char c in text)
            {
                if (c == ' ' || c == '\t')
                    inWord = false;
                else if (!inWord)
                {
                    count++;
                    inWord = true;
                }
            }

            return count;
        }

        public static int PickContextLeader(
            List<int> bucket,
            List<BaseUnit> items,
            Dictionary<int, HashSet<string>> tokensCache)
        {
            int best = -1;

            int bestWordCount = int.MaxValue;
            bool bestHasDigit = true;
            int bestTokenCount = -1;
            int bestLength = int.MaxValue;

            foreach (var i in bucket)
            {
                var text = items[i].Original;

                int wc = CountWords(text);
                bool hasDigit = HasDigit(text);
                int tc = tokensCache[i].Count;
                int len = text.Length;

                if (
                    wc < bestWordCount ||
                    (wc == bestWordCount && bestHasDigit && !hasDigit) ||
                    (wc == bestWordCount && hasDigit == bestHasDigit && tc > bestTokenCount) ||
                    (wc == bestWordCount && hasDigit == bestHasDigit && tc == bestTokenCount && len < bestLength)
                )
                {
                    best = i;
                    bestWordCount = wc;
                    bestHasDigit = hasDigit;
                    bestTokenCount = tc;
                    bestLength = len;
                }
            }

            return best;
        }

        public static string BuildPrefixKey(string text, int maxWords)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            int len = text.Length;
            int wordCount = 0;
            int i = 0;

            var sb = new StringBuilder(len);

            while (i < len && wordCount < maxWords)
            {
                while (i < len && (text[i] == ' ' || text[i] == '\t'))
                    i++;

                if (i >= len)
                    break;

                if (wordCount > 0)
                    sb.Append(' ');

                while (i < len && text[i] != ' ' && text[i] != '\t')
                {
                    sb.Append(char.ToLowerInvariant(text[i]));
                    i++;
                }

                wordCount++;
            }

            return sb.ToString();
        }

        private static bool HasDigit(string text)
        {
            foreach (char c in text)
                if (char.IsDigit(c))
                    return true;
            return false;
        }

        #endregion
    }
}
