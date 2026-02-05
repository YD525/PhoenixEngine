using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using PhoenixEngine.ConvertManager;
using PhoenixEngine.DelegateManagement;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.GameManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;

namespace PhoenixEngine.TranslateManagement
{
    public enum AggregationMode
    { 
        Null=0, Single = 1, Aggregation = 2
    }
    public class TranslationUnitGroup
    {
        public int Key = 0;

        public Languages From = Languages.Auto;
        public Languages To = Languages.Auto;
        public string AIParam = "";

        public int TotalLength;

        public List<TranslationUnit> Units = new List<TranslationUnit>();
        public AggregationMode Mode = AggregationMode.Null;

        public HashSet<string> AnchorTokens = new HashSet<string>();
        public HashSet<string> AllTokens = new HashSet<string>();
        public int LinkTo = 0;

        public bool IsUnrelated = false;
        public void Init(int Key,TranslationUnit First,AggregationMode SetMode,
            Languages SetFrom = Languages.Null, Languages SetTo = Languages.Null)
        {
            this.Mode = SetMode;

            if (SetMode == AggregationMode.Aggregation)
            {
                this.Key = Key;

                AnchorTokens = TranslationUnitExtend.ExtractTokens(First);
                AllTokens = new HashSet<string>(AnchorTokens);

                First.GroupRef = this;
                Units.Add(First);

                TotalLength += First.SourceText.Length;
            }
            else
            if (SetMode == AggregationMode.Single)
            {
                this.Key = 0;

                First.GroupRef = this;
                Units.Add(First);
            }

            if (SetFrom == Languages.Null)
            {
                SetFrom = Phoenix.From;
            }
            else
            if (SetTo == Languages.Null)
            {
                SetTo = Phoenix.To;
            }

            this.From = SetFrom;
            this.To = SetTo;
        }       
        public bool IsSimilarTo(HashSet<string> UnitTokens,int MatchCount)
        {
            return TranslationUnitExtend.TokenCoverageRatio(this.AnchorTokens, UnitTokens) >= MatchCount;
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

    public class TranslationUnitExtend
    {
        public static int TextLengthLimit = 2000;

        public class UnitUnion
        {
            public int GenKey = 0;

            public List<TranslationUnitGroup> Units = new List<TranslationUnitGroup>();
            public List<TranslationUnit> SameItems = new List<TranslationUnit>();
            public List<TranslationUnit> Books = new List<TranslationUnit>();

            public void AddLeader(TranslationUnit Item)
            {
                GenKey++;

                TranslationUnitGroup BatchUnit = new TranslationUnitGroup();
                BatchUnit.Init(GenKey, Item, AggregationMode.Aggregation);
                Units.Add(BatchUnit);
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

                foreach (var GetBatchUnit in this.Units)
                {
                    if (GetBatchUnit.IsSimilarTo(GenTokens, 1))
                    {
                        if (GetBatchUnit.TotalLength < TextLengthLimit)
                        {
                            GetBatchUnit.AddUnit(Item, GenTokens);
                        }
                        else
                        {
                            TranslationUnitGroup NextBatchUnit = new TranslationUnitGroup();

                            NextBatchUnit.Key = GenKey;

                            NextBatchUnit.AnchorTokens = new HashSet<string>(GetBatchUnit.AnchorTokens);
                            NextBatchUnit.AllTokens = new HashSet<string>(NextBatchUnit.AnchorTokens);

                            NextBatchUnit.AddUnit(Item, GenTokens);

                            NextBatchUnit.LinkTo = GetBatchUnit.Key;
                            Units.Add(NextBatchUnit);
                        }

                        return;
                    }
                }

                TranslationUnitGroup BatchUnit = new TranslationUnitGroup();
                BatchUnit.Init(GenKey,Item,AggregationMode.Aggregation);
                Units.Add(BatchUnit);
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

        public static UnitUnion BuildUnits(Dictionary<string, TranslationUnit> LeaderDict,List<TranslationUnit> Units)
        {
            UnitUnion UnitUnion = new UnitUnion();
            List<TranslationUnit> SameItems = new List<TranslationUnit>();

            HashSet<string> SeenTexts = new HashSet<string>();
            List<TranslationUnit> UniqueLeaders = new List<TranslationUnit>();

            foreach (var Leader in LeaderDict.Values)
            {
                if (!SeenTexts.Contains(Leader.SourceText))
                {
                    SeenTexts.Add(Leader.SourceText);
                    UniqueLeaders.Add(Leader);
                }
                else
                {
                    SameItems.Add(Leader);
                }
            }

            foreach (var Leader in UniqueLeaders)
            {
                UnitUnion.AddLeader(Leader);
            }

            foreach (var Unit in Units)
            {
                if (!SeenTexts.Contains(Unit.SourceText))
                {
                    SeenTexts.Add(Unit.SourceText);
                    UnitUnion.Add(Unit);
                }
                else
                {
                    SameItems.Add(Unit);
                }
            }

            Dictionary<TranslationUnitGroup, TranslationUnit> SingleUnits = new Dictionary<TranslationUnitGroup, TranslationUnit>();
            for (int i = 0; i < UnitUnion.Units.Count; i++)
            {
                if (UnitUnion.Units[i].Units.Count == 1)
                {
                    SingleUnits.Add(UnitUnion.Units[i], UnitUnion.Units[i].Units[0]);
                }
            }

            TranslationUnitGroup UnitGroup = new TranslationUnitGroup();
            UnitGroup.IsUnrelated = true;

            foreach (var Kvp in SingleUnits)
            {
                UnitUnion.Units.Remove(Kvp.Key);
                UnitGroup.AddUnit(Kvp.Value);

                if (UnitGroup.TotalLength > TranslationUnitExtend.TextLengthLimit)
                {
                    UnitUnion.Units.Add(UnitGroup);
                    UnitGroup = new TranslationUnitGroup();
                    UnitGroup.IsUnrelated = true;
                }
            }

            if (UnitGroup.Units.Count > 0)
            {
                UnitUnion.Units.Add(UnitGroup);
            }

            UnitUnion.SameItems.AddRange(SameItems);

            return UnitUnion;
        }
    }
   
}
