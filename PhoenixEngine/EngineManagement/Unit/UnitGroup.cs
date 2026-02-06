using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using PhoenixEngine.ConvertManager;
using PhoenixEngine.DelegateManagement;
using PhoenixEngine.EngineManagement.Engine;
using PhoenixEngine.TranslateManage;
using PhoenixEngine.TranslateManagement;

namespace PhoenixEngine.EngineManagement.Unit
{
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
        private List<BaseUnit> ParentRef = new List<BaseUnit>();
        public List<BaseUnit> Units = new List<BaseUnit>();
        public List<NeedConfirm> NeedConfirms = new List<NeedConfirm>();
        public ConfirmPasser(List<BaseUnit> Units)
        {
            ParentRef = Units;
            this.Units.AddRange(Units);
        }
        public bool TryPass(ref List<BaseUnit> NotPassUnits, ref List<BaseUnit> PassUnits)
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

            NotPassUnits = new List<BaseUnit>();

            for(int i = 0; i < Units.Count; i++)
            {
                var GetUnit = Units[i];
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

        public void Apply(List<BaseUnit> PassUnits)
        {
            for (int i = 0; i < ParentRef.Count; i++)
            {
                ParentRef[i].Translated = PassUnits[i].Translated;
            }
        }
    }

    public class UnitGroup
    {
        public string Key = "";
        public int TotalLength;

        public List<BaseUnit> Units = new List<BaseUnit>();
        public AggregationMode Mode = AggregationMode.Null;

        public HashSet<string> AnchorTokens = new HashSet<string>();
        public HashSet<string> AllTokens = new HashSet<string>();
        public string LinkTo = "";

        public bool IsUnrelated = false;

        public ProcContent ParentRef = null;

        public void BatchProc(int State)
        {
            if (State == 0)
            {
                for (int i = 0; i < Units.Count; i++)
                {
                    var GetUnit = Units[i];
                }
            }
            else
            {
                for (int i = 0; i < Units.Count; i++)
                {
                    var GetUnit = Units[i];
                }
            }
        }

        public UnitGroup(ProcContent ProcContent)
        {
            this.ParentRef = ProcContent;
        }

        public UnitGroup(ProcContent ProcContent, BaseUnit SingleUnit)
        {
            this.ParentRef = ProcContent;
            Init(0, SingleUnit, AggregationMode.Single);
        }

        public BaseUnit GetFrist()
        {
            if (Units.Count > 0)
            {
                return Units[0];
            }
            return null;
        }

        public void Init(int Key, BaseUnit First, AggregationMode SetMode)
        {
            this.Mode = SetMode;

            if (SetMode == AggregationMode.Aggregation)
            {
                this.Key = Key.ToString();

                AnchorTokens = First.ExtractTokens();
                AllTokens = new HashSet<string>(AnchorTokens);

                First.ParentRef = this;
                Units.Add(First);

                TotalLength += First.Original.Length;
            }
            else
            if (SetMode == AggregationMode.Single)
            {
                this.Key = First.Key;

                First.ParentRef = this;
                Units.Add(First);
            }
        }
        public bool IsSimilarTo(HashSet<string> UnitTokens, int MatchCount)
        {
            return TokenCoverageRatio(this.AnchorTokens, UnitTokens) >= MatchCount;
        }
        public void AddUnit(BaseUnit Unit)
        {
            Units.Add(Unit);
            TotalLength += Unit.Original.Length;
        }
        public void AddUnit(BaseUnit Unit, HashSet<string> UnitTokens)
        {
            Units.Add(Unit);
            TotalLength += Unit.Original.Length;
            AllTokens.UnionWith(UnitTokens);
        }
        public string GenContent()
        {
            return UnitGroup.GenContent(this.Units);
        }
        public static string GenContent(List<BaseUnit> Array)
        {
            if (Array.Count == 1)
            {
                if (Array[0].Translated.Length == 0)
                {
                    return string.Format("<li id='{0}'>{1}</li>\n", 0 + 100, Array[0].Original);
                }
            }

            string Html = "";
            for (int i = 0; i < Array.Count; i++)
            {
                if (Array[i].Translated.Length == 0)
                {
                    Html += string.Format("<li id='{0}'>{1}</li>\n", i + 100, Array[i].Original);
                }
            }
            return Html;
        }

        public ConfirmPasser AnalysisContent(string Content)
        {
            ConfirmPasser WaitConfirm = new ConfirmPasser(this.Units);

            string Pattern = @"<\s*li\s+id\s*=\s*'([^']*)'\s*>(.*?)</\s*li\s*>";

            var Matches = Regex.Matches(
                Content,
                Pattern,
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            );

            foreach (Match match in Matches)
            {
                int ID = ConvertHelper.ObjToInt(match.Groups[1].Value.Trim());
                string Result = match.Groups[2].Value.Trim();

                if (ID >= 100)
                {
                    int NormalID = ID - 100;
                    if (NormalID >= 0)
                    {
                        WaitConfirm.NeedConfirms.Add(
                            new NeedConfirm(NormalID, Result)
                        );
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
            //if (!CanTrans(0))
            //{
            //    this.WorkEnd = 2;
            //    return;
            //}

            //WorkEnd = 1;
            //this.Processing = true;
            //CurrentTrd = new Thread(() =>
            //{
            //    Translator Translator = this.ParentRef.GetTranslator();
            //    TransThreadToken = new CancellationTokenSource();
            //    var Token = TransThreadToken.Token;
            //    try
            //    {
            //    NextGet:

            //        Token.ThrowIfCancellationRequested();

            //        if (this.Original.Trim().Length > 0)
            //        {
            //            bool CanSleep = true;

            //            if (!CanTrans(1))
            //            {
            //                this.Processing = false;
            //                this.WorkEnd = 2;
            //                CurrentTrd = null;

            //                return;
            //            }

            //            var GetResult = Translator.Translate(new TranslationPreprocessor(), this, CanSleep);
            //            if (GetResult.Trim().Length > 0)
            //            {
            //                this.Trans = GetResult.Trim();

            //                if (!CanTrans(2))
            //                {
            //                    EngineNode.AIMemory.RemoveTranslation(Phoenix.From, Phoenix.To, TranslationPreprocessor.FormatStr(this.SourceText), TransText);

            //                    this.Trans = string.Empty;
            //                    this.Processing = false;
            //                    this.WorkEnd = 0;

            //                    CurrentTrd = null;
            //                    return;
            //                }

            //                this.IsTranslated = true;

            //                Source.AddTranslated(this);

            //                WorkEnd = 2;

            //                Token.ThrowIfCancellationRequested();
            //            }
            //            else
            //            {
            //                if (Translator.MaxTry > 0)
            //                {
            //                    Thread.Sleep(500);
            //                    Translator.MaxTry--;

            //                    goto NextGet;
            //                }
            //                else
            //                {
            //                    WorkEnd = 2;
            //                }
            //            }
            //        }
            //        else
            //        {
            //            WorkEnd = 2;
            //        }
            //    }
            //    catch (OperationCanceledException)
            //    {
            //        try
            //        {
            //            this.Processing = false;
            //            this.CurrentTrd = null;
            //        }
            //        catch { }
            //    }
            //    this.Processing = false;
            //    this.CurrentTrd = null;
            //});
            //CurrentTrd.Start();
        }

        private static int TokenCoverageRatio(HashSet<string> A, HashSet<string> B)
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

        public int GetCount()
        {
            return Units.Count;
        }
    }
}
