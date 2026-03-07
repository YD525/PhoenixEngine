using System;
using System.Collections.Generic;
using System.Linq;
using PhoenixEngine.Engine;
using PhoenixEngine.EngineManagement.Engine;
using PhoenixEngine.Language;
using PhoenixEngine.Unit;
using static PhoenixEngine.Language.LanguageHelper;

namespace PhoenixEngine.Sequence
{
    public class UnionArray
    {
        public int AutoLeaderTrd = 0;
        public Languages DetectSourceLang = Languages.Null;

        public Dictionary<string, BaseUnit> Leaders = new Dictionary<string, BaseUnit>();
        public List<BaseUnit> Units = new List<BaseUnit>();

        public void Load(List<BaseUnit> BaseUnits, Languages From,ref double MarkLeadersPercent)
        {
            MarkLeadersAndSort(BaseUnits, DetectSource(From),ref MarkLeadersPercent);
        }

        public Languages DetectSource(Languages From)
        {
            if (From != Languages.Auto)
            {
                this.DetectSourceLang = From;
            }
            else
            {
                FileLanguageDetect LangDetecter = new FileLanguageDetect();

                for (int i = 0; i < this.Units.Count; i++)
                {
                    LangDetecter.DetectLanguageByFile(this.Units[i].Original);
                }

                this.DetectSourceLang = LangDetecter.GetLang();

                LangDetecter = null;
            }

            return this.DetectSourceLang;
        }

        public void MarkLeadersAndSort(List<BaseUnit> SetBaseUnits, Languages Lang,ref double MarkLeadersPercent)
        {
            MarkLeadersPercent = 0;

            int N = SetBaseUnits.Count;
            if (N == 0)
                return;

            Leaders.Clear();
            Units.Clear();

            int MaxCharsForLeaderSelection = Phoenix.Config.ContextLimit;

            var FilteredItems = new List<int>();

            for (int i = 0; i < N; i++)
            {
                var Item = SetBaseUnits[i];
                Item.TempSim = 0;

                if (!string.IsNullOrEmpty(Item.Original) &&
                    Item.Original.Length > MaxCharsForLeaderSelection)
                {
                    Units.Add(Item);
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
                var Token = TextTokenizer.BuildTokenSignature(Lang, SetBaseUnits[Item].Original);
                TokensCache[Item] = Token.Take(10).ToHashSet();
            }

            var PrefixBuckets = new Dictionary<string, List<int>>();

            foreach (var Item in FilteredItems)
            {
                var Prefix = ContextProc.BuildPrefixKey(SetBaseUnits[Item].Original, 3);

                if (!PrefixBuckets.TryGetValue(Prefix, out var List))
                {
                    List = new List<int>();
                    PrefixBuckets[Prefix] = List;
                }

                List.Add(Item);
            }

            int ProcessedCount = Units.Count;
            int TotalToProcess = N;
            int UpdateInterval = Math.Max(1, TotalToProcess / 100);

            foreach (var Bucket in PrefixBuckets.Values)
            {
                if (Bucket.Count == 0)
                    continue;

                if (Bucket.Count == 1)
                {
                    Units.Add(SetBaseUnits[Bucket[0]]);
                    ProcessedCount++;
                    continue;
                }

                int LeaderIndex = ContextProc.PickContextLeader(Bucket, SetBaseUnits, TokensCache);
                var LeaderItem = SetBaseUnits[LeaderIndex];

                LeaderItem.TempSim = Bucket.Count - 1;

                if (!string.IsNullOrEmpty(LeaderItem.Key))
                {
                    LeaderItem.Leader = true;
                    Leaders[LeaderItem.Key] = LeaderItem;
                }
                else
                {
                    Units.Add(LeaderItem);
                }

                ProcessedCount++;

                foreach (var Item in Bucket)
                {
                    if (Item == LeaderIndex)
                        continue;

                    Units.Add(SetBaseUnits[Item]);
                    ProcessedCount++;
                }

                if (ProcessedCount % UpdateInterval == 0)
                {
                    MarkLeadersPercent = Math.Round(Math.Min(ProcessedCount, TotalToProcess) * 100.0 / TotalToProcess, 2);
                }
            }

            var SecondStageMap = new Dictionary<string, int>();
            var RemoveLeaders = new List<string>();

            foreach (var KV in Leaders)
            {
                var Item = KV.Value;
                var Key2 = ContextProc.BuildPrefixKey(Item.Original, 2);

                if (SecondStageMap.ContainsKey(Key2))
                {
                    Units.Add(Item);
                    RemoveLeaders.Add(KV.Key);
                }
                else
                {
                    SecondStageMap[Key2] = 1;
                }
            }

            foreach (var K in RemoveLeaders)
            {
                Leaders.Remove(K);
            }

            if (Leaders.Count < 1500)
            {
                AutoLeaderTrd = ContextProc.SortLeadersAndCalculateThreads(DetectSourceLang, Phoenix.Config.MaxThreadCount, ref Leaders);
            }
            else
            {
                AutoLeaderTrd = 3;
            }

            MarkLeadersPercent = 100;
        }

        public void Add(BaseUnit Unit)
        {
            Units.Add(Unit);
        }

        public int GetCount()
        {
            return this.Leaders.Count + this.Units.Count;
        }

        public void Clear()
        {
            Leaders.Clear();
            Units.Clear();
        }
    }
}
