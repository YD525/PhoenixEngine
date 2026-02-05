using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading;
using PhoenixEngine.ConvertManager;
using PhoenixEngine.DelegateManagement;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.GameManagement;
using PhoenixEngine.TranslateManage;
using static PhoenixEngine.TranslateManage.EngineCore;
using static PhoenixEngine.TranslateManagement.TranslationUnitExtend;

namespace PhoenixEngine.TranslateManagement
{
    public enum AggregationMode
    {
        Null = 0, Single = 1, Aggregation = 2
    }
    public class NeedConfirm
    {
        public int Index = 0;
        public string Result = "";
        public NeedConfirm(int Index, string Result)
        {
            this.Index = Index;
            this.Result = Result;
        }
    }
    public class ConfirmPasser
    {
        private List<TranslationUnit> ArrayRef = new List<TranslationUnit>();
        public List<TranslationUnit> Units = new List<TranslationUnit>();
        public List<NeedConfirm> NeedConfirms = new List<NeedConfirm>();
        public ConfirmPasser(List<TranslationUnit> Units)
        {
            ArrayRef = Units;
            this.Units.AddRange(Units);
        }
        public bool TryPass(ref List<TranslationUnit> NotPassUnits,ref List<TranslationUnit> PassUnits)
        {
            for (int i = 0; i < Units.Count; i++)
            {
                for (int ir = 0; ir < NeedConfirms.Count; ir++)
                {
                    if (i == NeedConfirms[ir].Index)
                    {
                        this.Units[i].Translated = NeedConfirms[ir].Result;
                    }
                }
            }

            NotPassUnits = new List<TranslationUnit>();

            foreach (var GetUnit in Units)
            {
                if (GetUnit.Original.Length > 0)
                {
                    if (GetUnit.Translated.Length == 0)
                    {
                        NotPassUnits.Add(GetUnit);
                    }
                    else
                    {
                        PassUnits.Add(GetUnit);
                    }
                }
            }

            if (NotPassUnits.Count > 0)
            {
                return false;
            }
            return true;
        }

        public void Apply(List<TranslationUnit> PassUnits)
        {
            for (int i = 0; i < ArrayRef.Count; i++)
            {
                ArrayRef[i].Translated = PassUnits[i].Translated;
            }
        }
    }
    public class TranslationTrd
    {
        public bool Processing = false;
        public bool IsTranslated = false;
        public int WorkEnd = 0;
        public Thread CurrentTrd;
        public CancellationTokenSource TransThreadToken;
    }
    public class TranslationUnitGroup : TranslationTrd
    {
        public int Key = 0;
        public int TotalLength;

        public List<TranslationUnit> Units = new List<TranslationUnit>();
        public AggregationMode Mode = AggregationMode.Null;

        public HashSet<string> AnchorTokens = new HashSet<string>();
        public HashSet<string> AllTokens = new HashSet<string>();
        public int LinkTo = 0;

        public bool IsUnrelated = false;

        public UnitUnion UnitUnionRef = null;
        public string Original = "";

        public void Preprocessing(int State)
        {
            if (State == 0)
            {
                for (int i = 0; i < Units.Count; i++)
                { 
                
                }
            }
            else
            { 
            
            }
        }

        public TranslationUnitGroup(UnitUnion UnitUnion)
        { 
            this.UnitUnionRef = UnitUnion;
        }

        public TranslationUnitGroup(UnitUnion UnitUnion, TranslationUnit SingleUnit)
        {
            this.UnitUnionRef = UnitUnion;
            Init(0,SingleUnit,AggregationMode.Single);
        }

        public TranslationUnit GetFrist()
        {
            if (Units.Count > 0)
            {
                return Units[0];
            }
            return null;
        }

        public void Init(int Key,TranslationUnit First,AggregationMode SetMode)
        {
            this.Mode = SetMode;

            if (SetMode == AggregationMode.Aggregation)
            {
                this.Key = Key;

                AnchorTokens = TranslationUnitExtend.ExtractTokens(First);
                AllTokens = new HashSet<string>(AnchorTokens);

                First.GroupRef = this;
                Units.Add(First);

                TotalLength += First.Original.Length;
            }
            else
            if (SetMode == AggregationMode.Single)
            {
                this.Key = 0;

                First.GroupRef = this;
                Units.Add(First);
            }
        }       
        public bool IsSimilarTo(HashSet<string> UnitTokens,int MatchCount)
        {
            return TranslationUnitExtend.TokenCoverageRatio(this.AnchorTokens, UnitTokens) >= MatchCount;
        }
        public void AddUnit(TranslationUnit Unit)
        {
            Units.Add(Unit);
            TotalLength += Unit.Original.Length;
        }
        public void AddUnit(TranslationUnit Unit, HashSet<string> UnitTokens)
        {
            Units.Add(Unit);
            TotalLength += Unit.Original.Length;
            AllTokens.UnionWith(UnitTokens);
        }
        public string GenContent()
        {
            return TranslationUnitGroup.GenContent(this.Units);
        }
        public static string GenContent(List<TranslationUnit> Array)
        {
            string Html = "";
            for (int i = 0; i < Array.Count; i++)
            {
                Html += string.Format("<li id='{0}'>{1}</li>\n", i + 100, Array[i].Original);
            }
            return Html;
        }
      
        public ConfirmPasser AnalysisContent(string Content)
        {
            ConfirmPasser WaitConfirm = new ConfirmPasser(this.Units);
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
                                WaitConfirm.NeedConfirms.Add(new NeedConfirm(NormalID,Result));
                            }
                        }
                    }
                }
            }

            return WaitConfirm;
        }

        public bool CanTrans(int State)
        {
            if (DelegateHelper.SetTranslationUnitCallBack != null)
            {
                return DelegateHelper.SetTranslationUnitCallBack(this, State);
            }

            return true;
        }

        public void StartWork()
        {
            if (!CanTrans(0))
            {
                this.WorkEnd = 2;
                return;
            }

            WorkEnd = 1;
            this.Processing = true;
            CurrentTrd = new Thread(() =>
            {
                Translator Translator = this.UnitUnionRef.GetTranslator();
                TransThreadToken = new CancellationTokenSource();
                var Token = TransThreadToken.Token;
                try
                {
                    NextGet:

                    Token.ThrowIfCancellationRequested();

                    if (this.Original.Trim().Length > 0)
                    {
                        bool CanSleep = true;

                        if (!CanTrans(1))
                        {
                            this.Processing = false;
                            this.WorkEnd = 2;
                            CurrentTrd = null;

                            return;
                        }

                        var GetResult = Translator.Translate(new TranslationPreprocessor(), this, CanSleep);
                        if (GetResult.Trim().Length > 0)
                        {
                            this.Trans = GetResult.Trim();

                            if (!CanTrans(2))
                            {
                                EngineNode.AIMemory.RemoveTranslation(Phoenix.From, Phoenix.To, TranslationPreprocessor.FormatStr(this.SourceText), TransText);

                                this.Trans = string.Empty;
                                this.Processing = false;
                                this.WorkEnd = 0;

                                CurrentTrd = null;
                                return;
                            }

                            this.IsTranslated = true;

                            Source.AddTranslated(this);

                            WorkEnd = 2;

                            Token.ThrowIfCancellationRequested();
                        }
                        else
                        {
                            if (Translator.MaxTry > 0)
                            {
                                Thread.Sleep(500);
                                Translator.MaxTry--;

                                goto NextGet;
                            }
                            else
                            {
                                WorkEnd = 2;
                            }
                        }
                    }
                    else
                    {
                        WorkEnd = 2;
                    }
                }
                catch (OperationCanceledException)
                {
                    try
                    {
                        this.Processing = false;
                        this.CurrentTrd = null;
                    }
                    catch { }
                }
                this.Processing = false;
                this.CurrentTrd = null;
            });
            CurrentTrd.Start();
        }

        public void CancelWorkThread()
        {
            WorkEnd = 2;
            TransThreadToken?.Cancel();
        }
    }
    public class TranslationUnitExtend
    {
        public static int TextLengthLimit = 2000;

        public class UnitUnion
        {
            public int GenKey = 0;

            private Translator TranslatorRef = null;

            public List<TranslationUnitGroup> Units = new List<TranslationUnitGroup>();
            public List<TranslationUnitGroup> SameItems = new List<TranslationUnitGroup>();
            public List<TranslationUnitGroup> Books = new List<TranslationUnitGroup>();

            public UnitUnion(Translator Translator)
            { 
                this.TranslatorRef = Translator;
            }
            public Translator GetTranslator()
            {
                return this.TranslatorRef;
            }
            public void AddLeader(TranslationUnit Item)
            {
                GenKey++;

                TranslationUnitGroup BatchUnit = new TranslationUnitGroup(this);
                BatchUnit.Init(GenKey, Item, AggregationMode.Aggregation);
                Units.Add(BatchUnit);
            }
            public void Add(TranslationUnit Item)
            {
                GenKey++;

                Game CheckGameType = Game.Null;
                if (SkyrimBookHelper.IsSkyrimBook(Item,ref CheckGameType))
                {
                    Books.Add(new TranslationUnitGroup(this,Item));
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
                            TranslationUnitGroup NextBatchUnit = new TranslationUnitGroup(this);

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

                TranslationUnitGroup BatchUnit = new TranslationUnitGroup(this);
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
            return TextTokenizer.BuildTokenSignature(Phoenix.From,Unit.Original,0);
        }

        public static UnitUnion BuildUnits(Translator Translator, Dictionary<string, TranslationUnit> LeaderDict,List<TranslationUnit> Units)
        {
            UnitUnion UnitUnion = new UnitUnion(Translator);
            List<TranslationUnitGroup> SameItems = new List<TranslationUnitGroup>();

            HashSet<string> SeenTexts = new HashSet<string>();
            List<TranslationUnit> UniqueLeaders = new List<TranslationUnit>();

            foreach (var Leader in LeaderDict.Values)
            {
                if (!SeenTexts.Contains(Leader.Original))
                {
                    SeenTexts.Add(Leader.Original);
                    UniqueLeaders.Add(Leader);
                }
                else
                {
                    SameItems.Add(new TranslationUnitGroup(UnitUnion, Leader));
                }
            }

            foreach (var Leader in UniqueLeaders)
            {
                UnitUnion.AddLeader(Leader);
            }

            foreach (var Unit in Units)
            {
                if (!SeenTexts.Contains(Unit.Original))
                {
                    SeenTexts.Add(Unit.Original);
                    UnitUnion.Add(Unit);
                }
                else
                {
                    SameItems.Add(new TranslationUnitGroup(UnitUnion, Unit));
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

            TranslationUnitGroup UnitGroup = new TranslationUnitGroup(UnitUnion);
            UnitGroup.IsUnrelated = true;

            foreach (var Kvp in SingleUnits)
            {
                UnitUnion.Units.Remove(Kvp.Key);
                UnitGroup.AddUnit(Kvp.Value);

                if (UnitGroup.TotalLength > TranslationUnitExtend.TextLengthLimit)
                {
                    UnitUnion.Units.Add(UnitGroup);
                    UnitGroup = new TranslationUnitGroup(UnitUnion);
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
