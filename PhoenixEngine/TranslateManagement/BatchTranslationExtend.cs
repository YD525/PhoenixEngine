using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;

namespace PhoenixEngine.TranslateManagement
{
    public class BatchTranslationExtend
    {
        public Dictionary<string, TranslationUnit> UnitsLeaderToTranslate = new Dictionary<string, TranslationUnit>();
        public List<TranslationUnit> UnitsToTranslate = new List<TranslationUnit>();
        public Languages DetectSourceLang = Languages.Null;


        #region Words Analysis

        public double MarkLeadersPercent = 0;
        public int AutoLeaderTrd = 0;
        public void MarkLeadersAndSort(List<TranslationUnit> SetItems, Languages Lang)
        {
            MarkLeadersPercent = 0;

            int N = SetItems.Count;
            if (N == 0)
                return;

            UnitsLeaderToTranslate.Clear();
            UnitsToTranslate.Clear();

            int MaxCharsForLeaderSelection = Phoenix.Config.ContextLimit;

            var FilteredItems = new List<int>();

            for (int i = 0; i < N; i++)
            {
                var Item = SetItems[i];
                Item.TempSim = 0;

                if (!string.IsNullOrEmpty(Item.SourceText) &&
                    Item.SourceText.Length > MaxCharsForLeaderSelection)
                {
                    UnitsToTranslate.Add(Item);
                }
                else
                {
                    FilteredItems.Add(i);
                }
            }

            if (FilteredItems.Count == 0)
            {
                MarkLeadersPercent = 100;
                return;
            }

            var TokensCache = new Dictionary<int, HashSet<string>>(FilteredItems.Count);

            foreach (var Item in FilteredItems)
            {
                var Token = TextTokenizer.BuildTokenSignature(Lang, SetItems[Item].SourceText);
                TokensCache[Item] = Token.Take(10).ToHashSet();
            }

            var PrefixBuckets = new Dictionary<string, List<int>>();

            foreach (var Item in FilteredItems)
            {
                var Prefix = BuildPrefixKey(SetItems[Item].SourceText, 3);

                if (!PrefixBuckets.TryGetValue(Prefix, out var List))
                {
                    List = new List<int>();
                    PrefixBuckets[Prefix] = List;
                }

                List.Add(Item);
            }

            int ProcessedCount = UnitsToTranslate.Count;
            int TotalToProcess = N;
            int UpdateInterval = Math.Max(1, TotalToProcess / 100);

            foreach (var Bucket in PrefixBuckets.Values)
            {
                if (Bucket.Count == 0)
                    continue;

                if (Bucket.Count == 1)
                {
                    UnitsToTranslate.Add(SetItems[Bucket[0]]);
                    ProcessedCount++;
                    continue;
                }

                int LeaderIndex = PickContextLeader(Bucket, SetItems, TokensCache);
                var LeaderItem = SetItems[LeaderIndex];

                LeaderItem.TempSim = Bucket.Count - 1;

                if (!string.IsNullOrEmpty(LeaderItem.Key))
                {
                    LeaderItem.Leader = true;
                    UnitsLeaderToTranslate[LeaderItem.Key] = LeaderItem;
                }
                else
                {
                    UnitsToTranslate.Add(LeaderItem);
                }

                ProcessedCount++;

                foreach (var Item in Bucket)
                {
                    if (Item == LeaderIndex)
                        continue;

                    UnitsToTranslate.Add(SetItems[Item]);
                    ProcessedCount++;
                }

                if (ProcessedCount % UpdateInterval == 0)
                {
                    MarkLeadersPercent = Math.Round(Math.Min(ProcessedCount, TotalToProcess) * 100.0 / TotalToProcess, 2);
                }
            }

            var SecondStageMap = new Dictionary<string, int>();
            var RemoveLeaders = new List<string>();

            foreach (var KV in UnitsLeaderToTranslate)
            {
                var Item = KV.Value;
                var Key2 = BuildPrefixKey(Item.SourceText, 2);

                if (SecondStageMap.ContainsKey(Key2))
                {
                    UnitsToTranslate.Add(Item);
                    RemoveLeaders.Add(KV.Key);
                }
                else
                {
                    SecondStageMap[Key2] = 1;
                }
            }

            foreach (var K in RemoveLeaders)
            {
                UnitsLeaderToTranslate.Remove(K);
            }

            if (UnitsLeaderToTranslate.Count < 1500)
            {
                AutoLeaderTrd = SortLeadersAndCalculateThreads(DetectSourceLang, Phoenix.Config.MaxThreadCount, ref UnitsLeaderToTranslate);
            }
            else
            {
                AutoLeaderTrd = 3;
            }

            MarkLeadersPercent = 100;
        }

        private int SortLeadersAndCalculateThreads(Languages Lang, int NeedTrd, ref Dictionary<string, TranslationUnit> Leaders)
        {
            var SortedPairs = new List<KeyValuePair<string, TranslationUnit>>();
            var TokenMap = new Dictionary<TranslationUnit, HashSet<string>>();

            foreach (var kv in Leaders)
            {
                var Unit = kv.Value;
                SortedPairs.Add(kv);

                TokenMap[Unit] = TextTokenizer.BuildTokenSignature(Lang, Unit.SourceText);
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

                return UB.SourceText.Length.CompareTo(UA.SourceText.Length);
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
            var Threads = new List<List<KeyValuePair<string, TranslationUnit>>>();
            int ThreadCount = Math.Min(NeedTrd, Math.Max(1, (int)Math.Sqrt(N))); //Empirical formula for initial thread count
            for (int i = 0; i < ThreadCount; i++)
                Threads.Add(new List<KeyValuePair<string, TranslationUnit>>());

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
            var NewLeaders = new Dictionary<string, TranslationUnit>(Leaders.Count);
            foreach (var thread in Threads)
                foreach (var kv in thread)
                    NewLeaders[kv.Key] = kv.Value;

            Leaders = NewLeaders;

            // Return approximate minimal thread count, with slight tolerated conflicts
            return Threads.Count;
        }

        private static int CountWords(string text)
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

        private static int PickContextLeader(
            List<int> bucket,
            List<TranslationUnit> items,
            Dictionary<int, HashSet<string>> tokensCache)
        {
            int best = -1;

            int bestWordCount = int.MaxValue;
            bool bestHasDigit = true;
            int bestTokenCount = -1;
            int bestLength = int.MaxValue;

            foreach (var i in bucket)
            {
                var text = items[i].SourceText;

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

        private static string BuildPrefixKey(string text, int maxWords)
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
