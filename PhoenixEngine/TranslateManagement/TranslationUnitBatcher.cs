using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using PhoenixEngine.ConvertManager;
using PhoenixEngine.GameManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;

namespace PhoenixEngine.TranslateManagement
{
    public class BatchTranslationUnit
    {
        public int Key = 0;

        public int TotalLength;

        public List<TranslationUnit> Units = new List<TranslationUnit>();

        public HashSet<string> AnchorTokens = new HashSet<string>();
        public HashSet<string> AllTokens = new HashSet<string>();

        public int LinkTo = 0;

        public bool IsUnrelated = false;

        public void Init(int Key,TranslationUnit First)
        {
            this.Key = Key;

            AnchorTokens = TranslationUnitBatcher.ExtractTokens(First);
            AllTokens = new HashSet<string>(AnchorTokens);

            Units.Add(First);
            TotalLength += First.SourceText.Length;
        }
        
        public bool IsSimilarTo(HashSet<string> UnitTokens,int MatchCount)
        {
            return TranslationUnitBatcher.TokenCoverageRatio(this.AnchorTokens, UnitTokens) >= MatchCount;
        }
        public void AddUnit(TranslationUnit Unit)
        {
            Units.Add(Unit);
            TotalLength += Unit.SourceText.Length;
        }
        public void AddUnit(TranslationUnit Unit, HashSet<string> UnitTokens)
        {
            Units.Add(Unit);
            TotalLength += Unit.SourceText.Length;
            AllTokens.UnionWith(UnitTokens);
        }

        public string GenContent()
        {
            return GenContent(this.Units);
        }

        public string GenContent(List<TranslationUnit> Array)
        {
            string Html = "";
            for (int i = 0; i < Array.Count; i++)
            {
                Html += string.Format("<li id='{0}'>{1}</li>\n", i + 100, Array[i].SourceText);
            }
            return Html;
        }

        public void ApplyContent(string Content, ref List<int> SuccessIDs)
        {
            Content = Content.Replace(">", ">\n");

            foreach (var Line in Content.Split(new char[2] { '\r', '\n' }))
            {
                if (Line.Trim().Length > 0)
                {
                    string Pattern = @"<\s*li\s+id\s*=\s*'([^']*)'\s*>(.*?)</\s*li\s*>";

                    Match Match = Regex.Match(Line, Pattern, RegexOptions.IgnoreCase);

                    if (Match.Success)
                    {
                        int ID = ConvertHelper.ObjToInt(Match.Groups[1].Value.Trim());
                        string Result = Match.Groups[2].Value;

                        if (ID >= 100)
                        {
                            int NormalID = (ID - 100);
                            if (NormalID >= 0)
                            {
                                SuccessIDs.Add(NormalID);
                                this.Units[NormalID].TransText = Result;
                                this.Units[NormalID].Translated = true;
                            }
                        }
                    }
                }
            }
        }
    }

    public class TranslationUnitBatcher
    {
        public static int TextLengthLimit = 2000;

        public class UnitBatcher
        {
            public int GenKey = 0;

            public List<BatchTranslationUnit> BatchTranslationUnits = new List<BatchTranslationUnit>();
            public List<TranslationUnit> Books = new List<TranslationUnit>();

            public void AddLeader(TranslationUnit Item)
            {
                GenKey++;

                BatchTranslationUnit BatchUnit = new BatchTranslationUnit();
                BatchUnit.Init(GenKey, Item);
                BatchTranslationUnits.Add(BatchUnit);
            }
            public void Add(TranslationUnit Item)
            {
                GenKey++;

                Game CheckGameType = Game.Null;
                if (Translator.IsSkyrimBook(Item,ref CheckGameType))
                {
                    Books.Add(Item);
                    return;
                }

                var GenTokens = ExtractTokens(Item);

                foreach (var GetBatchUnit in this.BatchTranslationUnits)
                {
                    if (GetBatchUnit.IsSimilarTo(GenTokens, 1))
                    {
                        if (GetBatchUnit.TotalLength < TextLengthLimit)
                        {
                            GetBatchUnit.AddUnit(Item, GenTokens);
                        }
                        else
                        {
                            BatchTranslationUnit NextBatchUnit = new BatchTranslationUnit();

                            NextBatchUnit.Key = GenKey;

                            NextBatchUnit.AnchorTokens = new HashSet<string>(GetBatchUnit.AnchorTokens);
                            NextBatchUnit.AllTokens = new HashSet<string>(NextBatchUnit.AnchorTokens);

                            NextBatchUnit.AddUnit(Item, GenTokens);

                            NextBatchUnit.LinkTo = GetBatchUnit.Key;
                            BatchTranslationUnits.Add(NextBatchUnit);
                        }

                        return;
                    }
                }

                BatchTranslationUnit BatchUnit = new BatchTranslationUnit();
                BatchUnit.Init(GenKey,Item);
                BatchTranslationUnits.Add(BatchUnit);
            }
        }

        public static int TokenCoverageRatio(HashSet<string> A, HashSet<string> B)
        {
            if (A == null || B == null || A.Count == 0 || B.Count == 0)
            {
                return 0;
            }

            int Intersection = 0;
            foreach (var T in A)
            {
                if (B.Contains(T))
                {
                    Intersection++;
                }
            }

            return Intersection;
        }

        public static HashSet<string> ExtractTokens(TranslationUnit Unit)
        {
            return TextTokenizer.BuildTokenSignature(Unit.From,Unit.SourceText,0);
        }

        public static List<BatchTranslationUnit> MergeUnits(Dictionary<string,TranslationUnit> LeaderDict, List<TranslationUnit> Units)
        {
            UnitBatcher NUnitBatcher = new UnitBatcher();

            List<TranslationUnit> Leaders = new List<TranslationUnit>();

            for (int i = 0;i < LeaderDict.Count;i++)
            {
                var GetKey = LeaderDict.ElementAt(i).Key;

                NUnitBatcher.AddLeader(LeaderDict[GetKey]);
            }

            for (int i = 0; i < Units.Count; i++)
            {
                NUnitBatcher.Add(Units[i]);
            }

            Dictionary<BatchTranslationUnit, TranslationUnit> SingleUnits = new Dictionary<BatchTranslationUnit, TranslationUnit>();

            for (int i = 0; i < NUnitBatcher.BatchTranslationUnits.Count; i++)
            {
                if (NUnitBatcher.BatchTranslationUnits[i].Units.Count == 1)
                {
                    SingleUnits.Add(NUnitBatcher.BatchTranslationUnits[i], NUnitBatcher.BatchTranslationUnits[i].Units[0]);
                }
            }


            BatchTranslationUnit BatchTransUnit = new BatchTranslationUnit();
            BatchTransUnit.IsUnrelated = true;


            foreach (var Kvp in SingleUnits)
            {
                NUnitBatcher.BatchTranslationUnits.Remove(Kvp.Key);
                BatchTransUnit.AddUnit(Kvp.Value);

                if (BatchTransUnit.TotalLength > TranslationUnitBatcher.TextLengthLimit)
                {
                    NUnitBatcher.BatchTranslationUnits.Add(BatchTransUnit);
                    BatchTransUnit = new BatchTranslationUnit();
                    BatchTransUnit.IsUnrelated = true;
                }
            }

            if (BatchTransUnit.Units.Count > 0)
            {
                NUnitBatcher.BatchTranslationUnits.Add(BatchTransUnit);
            }


            return NUnitBatcher.BatchTranslationUnits;
        }
    }
   
}
