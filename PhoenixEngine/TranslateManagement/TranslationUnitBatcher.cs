using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;

namespace PhoenixEngine.TranslateManagement
{
    public class BatchTranslationUnit
    {
        public int TotalLength;

        public List<TranslationUnit> Units = new List<TranslationUnit>();

        public HashSet<string> AnchorTokens = new HashSet<string>();
        public HashSet<string> AllTokens = new HashSet<string>();

        public void Init(TranslationUnit First)
        {
            AnchorTokens = TranslationUnitBatcher.ExtractTokens(First);
            AllTokens = new HashSet<string>(AnchorTokens);

            Units.Add(First);
            TotalLength += First.SourceText.Length;
        }

        public bool IsSimilarTo(HashSet<string> UnitTokens, float Threshold)
        {
            return TranslationUnitBatcher.TokenCoverageRatio(this.AnchorTokens, UnitTokens) >= Threshold;
        }

        public void AddUnit(TranslationUnit Unit, HashSet<string> UnitTokens)
        {
            Units.Add(Unit);
            TotalLength += Unit.SourceText.Length;
            AllTokens.UnionWith(UnitTokens);
        }
    }

    public class TranslationUnitBatcher
    {
        public static int TextLengthLimit = 1000;

        public class UnitBatcher
        {
            public List<BatchTranslationUnit> BatchTranslationUnits = new List<BatchTranslationUnit>();

            public void Add(TranslationUnit Item)
            {
                var GenTokens = ExtractTokens(Item);

                foreach (var GetBatchUnit in this.BatchTranslationUnits)
                {
                    if (GetBatchUnit.IsSimilarTo(GenTokens, 0.35f))
                    {
                        GetBatchUnit.AddUnit(Item, GenTokens);
                        return;
                    }
                }

                BatchTranslationUnit BatchUnit = new BatchTranslationUnit();
                BatchUnit.Init(Item);
                BatchTranslationUnits.Add(BatchUnit);
            }
        }

        public static float TokenCoverageRatio(HashSet<string> A, HashSet<string> B)
        {
            if (A == null || B == null || A.Count == 0 || B.Count == 0)
                return 0f;

            int Intersection = 0;
            foreach (var t in A)
            {
                if (B.Contains(t))
                    Intersection++;
            }

            float CoverageA = (float)Intersection / A.Count;
            float CoverageB = (float)Intersection / B.Count;

            return Math.Max(CoverageA, CoverageB);
        }

        public static HashSet<string> ExtractTokens(TranslationUnit Unit)
        {
            return TextTokenizer.Tokenize(Unit.From, Unit.SourceText).Select(t => t.ToLowerInvariant()).ToHashSet();
        }

        public static List<BatchTranslationUnit> MergeUnits(List<TranslationUnit> Leaders, List<TranslationUnit> Units)
        {
            UnitBatcher NUnitBatcher = new UnitBatcher();

            Leaders.AddRange(Units);
            Units = Leaders;

            for (int i = 0; i < Units.Count; i++)
            {
                NUnitBatcher.Add(Units[i]);
            }

            return NUnitBatcher.BatchTranslationUnits;
        }
    }
   
}
