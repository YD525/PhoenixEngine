using PhoenixEngine.Engine;
using PhoenixEngine.Events;
using PhoenixEngine.Language;
using PhoenixEngine.P_Delegate;
using PhoenixEngine.Platform.LocalAI;
using PhoenixEngine.Platform;
using PhoenixEngine.Request;
using PhoenixEngine.Sequence;
using PhoenixEngine.Translate;
using PhoenixEngine.Unit;
using PhoenixEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Linq;

public class EngineCore
{
    public static readonly EngineCore Instance = new EngineCore();

    private readonly object _Lock = new object();

    private readonly Dictionary<string, int> _FailureCounts = new Dictionary<string, int>();
    private readonly Dictionary<string, bool> _DisabledPlatforms = new Dictionary<string, bool>();

    /// <summary>
    /// Waits for a synchronous provider delay while allowing cancellation to interrupt the wait.
    /// </summary>
    /// <param name="cancellationToken">The token that interrupts the delay.</param>
    /// <param name="milliseconds">The delay in milliseconds; non-positive values return immediately.</param>
    internal static void Delay(CancellationToken cancellationToken, int milliseconds)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (milliseconds <= 0)
        {
            return;
        }
        if (cancellationToken.WaitHandle.WaitOne(milliseconds))
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    public List<EngineNode> EngineNodes = new List<EngineNode>();

    public void Init()
    {
        ReloadEngine();
    }

    public void RemoveEngine<T>()
    {
        lock (_Lock)
        {
            EngineNodes.RemoveAll(e => e is T);
        }
    }

    public void ResetEngineHealth()
    {
        lock (_Lock)
        {
            _FailureCounts.Clear();
            _DisabledPlatforms.Clear();

            foreach (var Node in EngineNodes)
            {
                Node.Disabled = false;
                Node.EffectiveWeight = Node.MaxCallCount;
                Node.CurrentWeight = 0;
            }
        }
    }

    private void SyncHealthFromMap_NoLock(EngineNode Node)
    {
        Node.Disabled = _DisabledPlatforms.TryGetValue(Node.Key, out var Dis) && Dis;
    }

    internal void RegisterFailure(EngineNode Node)
    {
        lock (_Lock)
        {
            _FailureCounts.TryGetValue(Node.Key, out int Count);
            Count++;
            _FailureCounts[Node.Key] = Count;

            if (Count >= Phoenix.Config.FailThreshold)
            {
                _DisabledPlatforms[Node.Key] = true;
                Node.Disabled = true;
            }

            if (Node.EffectiveWeight > 1) Node.EffectiveWeight--;
        }
    }

    internal void RegisterSuccess(EngineNode Node)
    {
        lock (_Lock)
        {
            _FailureCounts[Node.Key] = 0;
            if (Node.EffectiveWeight < Node.MaxCallCount) Node.EffectiveWeight++;
        }
    }

    public void ReloadEngine()
    {
        lock (_Lock)
        {
            EngineNodes.Clear();
            PlatformApiKeys KeyData = null;

            var ChatGptConfig = Phoenix.Config.GetPlatformData(ChatGptApi.Type);
            KeyData = Phoenix.KeyData.GetData(ChatGptApi.Type);
            if (ChatGptConfig.Enable && KeyData.HaveKey())
            {
                ChatGptApi NChatGptApi = new ChatGptApi();
                NChatGptApi.Init(0, Phoenix.AIMemory, Phoenix.Config, ProxyCenter.CurrentProxy);
                AddNode_NoLock(new EngineNode(this, NChatGptApi, KeyData.GetKeyCount(), "ChatGpt"));
            }

            var GeminiConfig = Phoenix.Config.GetPlatformData(GeminiApi.Type);
            KeyData = Phoenix.KeyData.GetData(GeminiApi.Type);
            if (GeminiConfig.Enable && KeyData.HaveKey())
            {
                GeminiApi NGeminiApi = new GeminiApi();
                NGeminiApi.Init(0, Phoenix.AIMemory, Phoenix.Config, ProxyCenter.CurrentProxy);
                AddNode_NoLock(new EngineNode(this, NGeminiApi, KeyData.GetKeyCount(), "Gemini"));
            }

            var DeepSeekConfig = Phoenix.Config.GetPlatformData(DeepSeekApi.Type);
            KeyData = Phoenix.KeyData.GetData(DeepSeekApi.Type);
            if (DeepSeekConfig.Enable && KeyData.HaveKey())
            {
                DeepSeekApi NDeepSeekApi = new DeepSeekApi();
                NDeepSeekApi.Init(0, Phoenix.AIMemory, Phoenix.Config, ProxyCenter.CurrentProxy);
                AddNode_NoLock(new EngineNode(this, NDeepSeekApi, KeyData.GetKeyCount(), "DeepSeek"));
            }

            var LMLocalAIConfig = Phoenix.Config.GetPlatformData(LMStudio.Type);
            if (LMLocalAIConfig.Enable)
            {
                LMStudio NLMStudio = new LMStudio();
                NLMStudio.Init(0, Phoenix.AIMemory, Phoenix.Config);
                AddNode_NoLock(new EngineNode(this, NLMStudio, 1, "LMStudio"));
            }

            var DeepLConfig = Phoenix.Config.GetPlatformData(DeepLApi.Type);
            KeyData = Phoenix.KeyData.GetData(DeepLApi.Type);
            if (DeepLConfig.Enable && KeyData.HaveKey())
            {
                DeepLApi NDeepLApi = new DeepLApi();
                NDeepLApi.Init(0, Phoenix.Config, ProxyCenter.CurrentProxy);
                AddNode_NoLock(new EngineNode(this, NDeepLApi, KeyData.GetKeyCount(), "DeepL"));
            }

            for (int i = 0; i < Phoenix.Config.PlatformConfigs.Count; i++)
            {
                int GetKey = Phoenix.Config.PlatformConfigs.ElementAt(i).Key;
                var CustomInFo = Phoenix.Config.PlatformConfigs[GetKey].CustomInFo;
                if (CustomInFo == null) continue;
                if (CustomInFo.CustomID <= 0 || !Phoenix.Config.PlatformConfigs[GetKey].Enable) continue;

                KeyData = Phoenix.KeyData.GetData(CustomInFo.CustomID);
                string CustomKey = "Custom_" + CustomInFo.CustomID;

                switch (CustomInFo.Type)
                {
                    case CustomPlatformType.LocalAI:
                        {
                            CustomLocalAIApi NCustomLocalAIApi = new CustomLocalAIApi();
                            NCustomLocalAIApi.Init(CustomInFo.CustomID, Phoenix.AIMemory, Phoenix.Config);
                            AddNode_NoLock(new EngineNode(this, NCustomLocalAIApi, 1, CustomKey));
                        }
                        break;
                    case CustomPlatformType.CloudAI:
                        {
                            CustomAIApi NCustomAIApi = new CustomAIApi();
                            NCustomAIApi.Init(CustomInFo.CustomID, Phoenix.AIMemory, Phoenix.Config, ProxyCenter.CurrentProxy);
                            AddNode_NoLock(new EngineNode(this, NCustomAIApi, KeyData.GetKeyCount(), CustomKey));
                        }
                        break;
                    case CustomPlatformType.Traditional:
                        {
                            CustomApi NCustomApi = new CustomApi();
                            NCustomApi.Init(CustomInFo.CustomID, Phoenix.Config, ProxyCenter.CurrentProxy);
                            AddNode_NoLock(new EngineNode(this, NCustomApi, KeyData.GetKeyCount(), CustomKey));
                        }
                        break;
                }
            }

            var HumanConfig = Phoenix.Config.GetPlatformData(HumanTranslationApi.Type);
            if (HumanConfig.Enable)
            {
                HumanTranslationApi NHumanTranslationApi = new HumanTranslationApi();
                NHumanTranslationApi.Init(0, Phoenix.AIMemory, Phoenix.Config);
                AddNode_NoLock(new EngineNode(this, NHumanTranslationApi, 1, "Human"));
            }

            KeyData = null;
        }
    }

    private void AddNode_NoLock(EngineNode Node)
    {
        SyncHealthFromMap_NoLock(Node);
        EngineNodes.Add(Node);
    }

    public static void CheckCanSkip(string TranslatorID, Dictionary<string, UnitSequence> Sequences, ref UnitGroup Item)
    {
        foreach (var GetSeq in new Dictionary<string, UnitSequence>(Sequences))
        {
            if (GetSeq.Value.CanSkip)
            {
                foreach (var GetBaseUnit in Item.Units)
                {
                    if (GetBaseUnit.Key.Equals(GetSeq.Key))
                    {
                        if (GetBaseUnit.ApplyStateChange(TranslatorID, UnitTranslationState.Skipped).ControlSignal.Sign > 0)
                        {
                            Sequences[GetSeq.Key].CanSkip = false;
                            continue;
                        }
                    }
                }
            }
        }
    }

    private EngineNode PickNext()
    {
        lock (_Lock)
        {
            EngineNode Best = null;
            int Total = 0;

            foreach (var Node in EngineNodes)
            {
                if (Node.Disabled) continue;

                Node.CurrentWeight += Node.EffectiveWeight;
                Total += Node.EffectiveWeight;

                if (Best == null || Node.CurrentWeight > Best.CurrentWeight)
                {
                    Best = Node;
                }
            }

            if (Best != null)
            {
                Best.CurrentWeight -= Total;
            }

            return Best;
        }
    }

    public UnitGroup CallOnce(CancellationToken CancelToken, Translator TranslatorRef, TranslationPreprocessor Preprocessor, UnitGroup Item,
        Languages From, Languages To, string AIParam, bool CanSleep, bool UseAIMemory, bool CanUpdate)
    {
        if (!Item.ApplyStateChange(TranslatorRef.ID, UnitTranslationState.Preparing).CanDo(-1))
        {
            return Item;
        }

        Item.StartPreProcess(Preprocessor, From, To, out Dictionary<string, UnitSequence> Sequences);

        CheckCanSkip(TranslatorRef.ID, Sequences, ref Item);

        Item.CenterPreProcess(TranslatorRef, From, To, ref Sequences);

        CheckCanSkip(TranslatorRef.ID, Sequences, ref Item);

        EngineNode CurrentEngine = null;
        int IdleSpins = 0;

        while (CurrentEngine == null)
        {
            CancelToken.ThrowIfCancellationRequested();

            CurrentEngine = PickNext();

            if (CurrentEngine != null)
            {
                int MaxTry = 10;

                NextCall:

                string GetTrans = "";

                if (!Item.ApplyStateChange(TranslatorRef.ID, UnitTranslationState.Translating).CanDo(-1))
                {
                    return Item;
                }

                string SetType = "";

                GetTrans = CurrentEngine.Call(TranslatorRef.ID, CancelToken, TranslatorRef, ref Item, ref Sequences, From, To,
                true, Phoenix.Config.ContextLimit, AIParam, ref SetType);

                CancelToken.ThrowIfCancellationRequested();

                try
                {
                    if (Preprocessor.HasUnicodeEscape(GetTrans))
                    {
                        GetTrans = Regex.Unescape(GetTrans);
                    }
                }
                catch
                {
                    goto NextCall;
                }

                int Hits = 0;
                foreach (var GetSeq in new Dictionary<string, UnitSequence>(Sequences))
                {
                    if (GetSeq.Value.CanSkipSleep)
                    {
                        Hits++;
                    }
                }

                if (CanSleep && ((Hits == Sequences.Count) == false))
                {
                    CurrentEngine.BeginSleep(CancelToken);
                }

                ConfirmPasser Passer = Item.AnalysisContent(GetTrans);

                List<BaseUnit> NotPassUnits = new List<BaseUnit>();
                List<BaseUnit> PassUnits = new List<BaseUnit>();

                bool IsDeepL = SetType.Equals("DeepL");

                bool Passed = Passer.TryPass(ref NotPassUnits, ref PassUnits, IsDeepL);

                if (!Passed)
                {
                    Delay(CancelToken, Phoenix.Config.ThrottleDelayMs);

                    if (PassUnits.Count == 0)
                    {
                        Delay(CancelToken, 1000);
                    }

                    if (MaxTry > 0)
                    {
                        MaxTry--;
                        goto NextCall;
                    }
                }

                Passer.Apply(PassUnits);

                for (int i = 0; i < PassUnits.Count; i++)
                {
                    var PassUnit = PassUnits[i];
                    Sequences[PassUnit.Key].CanSkip = true;
                    Sequences[PassUnit.Key].Step = 6;
                    Sequences[PassUnit.Key].Data = PassUnit.Translated;
                }

                Item.EndPreProcess(From, To, ref Sequences);
                Item.EndGeneratePlaceholder(From, To, ref Sequences);

                if (UseAIMemory)
                {
                    Item.UpdateAIMemory(TranslatorRef, Sequences);
                }

                if (CanUpdate)
                {
                    Item.UpdateCloudData(TranslatorRef, Sequences);
                }

                if (!Item.ApplyStateChange(TranslatorRef.ID, UnitTranslationState.Completed).CanDo(-1))
                {
                    return Item;
                }

                return Item;
            }

            IdleSpins++;
            if (IdleSpins > 500)
            {
                return null;
            }

            Delay(CancelToken, 1);
            ReloadEngine();

            if (EngineNodes.Count == 0)
            {
                return null;
            }
        }

        return null;
    }

    public class EngineNode
    {
        private readonly EngineCore Owner;

        public object ApiRef;
        public string Key;
        public int MaxCallCount;
        public int EffectiveWeight;
        public int CurrentWeight;
        public bool Disabled;
        public int SleepBySec;

        public EngineNode(EngineCore Owner, object Api, int MaxCallCount, string Key) : this(Owner, Api, MaxCallCount, Key, 1) { }

        public EngineNode(EngineCore Owner, object Api, int MaxCallCount, string Key, int SleepBySec)
        {
            this.Owner = Owner;
            this.ApiRef = Api;
            this.Key = Key;
            this.MaxCallCount = MaxCallCount;
            this.EffectiveWeight = MaxCallCount;
            this.CurrentWeight = 0;
            this.SleepBySec = SleepBySec;
        }

        /// <summary>
        /// Applies the provider throttle delay for callers without cancellation support.
        /// </summary>
        public void BeginSleep()
        {
            BeginSleep(CancellationToken.None);
        }

        /// <summary>
        /// Applies the provider throttle delay while honoring cooperative cancellation.
        /// </summary>
        /// <param name="cancellationToken">The token that cancels the delay.</param>
        public void BeginSleep(CancellationToken cancellationToken)
        {
            for (int i = 0; i < SleepBySec; i++)
            {
                Delay(cancellationToken, 1000);
            }
        }

        public static bool IsLocked(object Obj)
        {
            bool LockTaken = false;
            Monitor.TryEnter(Obj, 0, ref LockTaken);
            if (LockTaken) Monitor.Exit(Obj);
            return !LockTaken;
        }

        public string Call(string TranslatorID, CancellationToken CancelToken, Translator TranslatorRef, ref UnitGroup Source, ref Dictionary<string, UnitSequence> Sequences,
           Languages From, Languages To, bool UseAIMemory, int AIMemoryQueryLimit, string AIParam, ref string SetType)
        {
            bool ForceReplace = true;

            if (this.ApiRef is ChatGptApi || this.ApiRef is GeminiApi || this.ApiRef is DeepSeekApi || this.ApiRef is LMStudio || this.ApiRef is CustomAIApi || this.ApiRef is CustomLocalAIApi || this.ApiRef is HumanTranslationApi)
            {
                ForceReplace = false;
            }

            Source.StartGeneratePlaceholder(TranslatorRef, From, To, ref Sequences, ForceReplace);

            CheckCanSkip(TranslatorID, Sequences, ref Source);

            bool CanTrans = false;
            string GetSource = Source.GenContent(ref CanTrans);
            if (!CanTrans)
            {
                return "<empty>";
            }

            if (GetSource.Length == 0)
            {
                return string.Empty;
            }

            if (From == Languages.Auto)
            {
                From = P_Language.DetectLanguageByLine(GetSource);
            }

            int MaxTranslationAttempts = Phoenix.Config.MaxTranslationAttempts;
            string TransText = string.Empty;

            List<ReplaceTag> CustomWords = new List<ReplaceTag>();
            foreach (var GetSeq in new Dictionary<string, UnitSequence>(Sequences))
            {
                if (GetSeq.Value.HasPlaceholder)
                    CustomWords.AddRange(GetSeq.Value.Preprocessor.ReplaceTags);
            }

            if (this.ApiRef is DeepLApi SetDeepL)
            {
                if (Phoenix.Config.GetPlatformData(DeepLApi.Type).Enable)
                {
                    PlatformCall Call = new PlatformCall();
                    string GetData = null;
                    bool Passed = false;
                    int MaxTry = MaxTranslationAttempts;
                    string CurrentApiKey = "";

                    do
                    {
                        CancelToken.ThrowIfCancellationRequested();
                        CurrentApiKey = Phoenix.KeyData.GetData(DeepLApi.Type).GetFirstKey();
                        GetData = SetDeepL.QuickTrans(CurrentApiKey, Source, From, To, ref Call).Trim();
                        Passed = TranslatorRef.Preprocessor.SecondaryQualityInspection(GetData, CustomWords);

                        if (!Passed && MaxTry > 0)
                        {
                            Delay(CancelToken, Phoenix.Config.ReTryWaitTime);
                            MaxTry--;
                        }
                        else
                        {
                            break;
                        }
                    } while (!Passed);

                    if (GetData.Length == 0) Phoenix.KeyData.GetData(DeepLApi.Type).ReportError(CurrentApiKey);

                    TransText = GetData;
                    Call.Output();
                    SetType = "DeepL";

                    if (GetData.Trim().Length == 0) Owner.RegisterFailure(this); else Owner.RegisterSuccess(this);
                }
            }
            else if (this.ApiRef is CustomApi SetCustomApi)
            {
                if (Phoenix.Config.GetPlatformData(SetCustomApi.CustomID).Enable)
                {
                    var Type = SetCustomApi.CustomID;
                    PlatformCall Call = new PlatformCall();
                    string GetData = null;
                    bool Passed = false;
                    int MaxTry = MaxTranslationAttempts;
                    string CurrentApiKey = "";

                    do
                    {
                        CancelToken.ThrowIfCancellationRequested();
                        CurrentApiKey = Phoenix.KeyData.GetData(Type).GetFirstKey();
                        GetData = SetCustomApi.QuickTrans(CurrentApiKey, Source, From, To, ref Call).Trim();
                        Passed = TranslatorRef.Preprocessor.SecondaryQualityInspection(GetData, CustomWords);

                        if (!Passed && MaxTry > 0)
                        {
                            Delay(CancelToken, Phoenix.Config.ReTryWaitTime);
                            MaxTry--;
                        }
                        else
                        {
                            break;
                        }
                    } while (!Passed);

                    if (GetData.Length == 0) Phoenix.KeyData.GetData(Type).ReportError(CurrentApiKey);

                    TransText = GetData;
                    Call.Output();
                    SetType = Type.ToString();

                    if (GetData.Trim().Length == 0) Owner.RegisterFailure(this); else Owner.RegisterSuccess(this);
                }
            }
            else if (this.ApiRef is LMStudio SetLM)
            {
                if (Phoenix.Config.GetPlatformData(LMStudio.Type).Enable)
                {
                    if (IsLocked(LMStudio.SingleLock))
                    {
                        while (IsLocked(LMStudio.SingleLock))
                        {
                            CancelToken.ThrowIfCancellationRequested();
                            Delay(CancelToken, 200);
                        }
                    }

                    AICall Call = new AICall();
                    string GetData = null;
                    bool Passed = false;
                    int MaxTry = MaxTranslationAttempts;

                    do
                    {
                        CancelToken.ThrowIfCancellationRequested();
                        GetData = SetLM.QuickTrans(CustomWords, Source, From, To, UseAIMemory, AIMemoryQueryLimit, AIParam, ref Call).Trim();
                        Passed = TranslatorRef.Preprocessor.SecondaryQualityInspection(GetData, CustomWords);

                        if (!Passed && MaxTry > 0)
                        {
                            Delay(CancelToken, Phoenix.Config.ReTryWaitTime);
                            MaxTry--;
                        }
                        else
                        {
                            break;
                        }
                    } while (!Passed);

                    TransText = GetData;
                    Call.Output();
                    SetType = "LMStudio";

                    if (GetData.Trim().Length == 0) Owner.RegisterFailure(this); else Owner.RegisterSuccess(this);
                }
            }
            else if (this.ApiRef is ChatGptApi SetChatGpt)
            {
                if (Phoenix.Config.GetPlatformData(ChatGptApi.Type).Enable)
                {
                    var Type = ChatGptApi.Type;
                    AICall Call = new AICall();
                    string GetData = null;
                    bool Passed = false;
                    int MaxTry = MaxTranslationAttempts;
                    string CurrentApiKey = "";

                    do
                    {
                        CancelToken.ThrowIfCancellationRequested();
                        CurrentApiKey = Phoenix.KeyData.GetData(Type).GetFirstKey();
                        GetData = SetChatGpt.QuickTrans(CurrentApiKey, CustomWords, Source, From, To, UseAIMemory, AIMemoryQueryLimit, AIParam, ref Call).Trim();
                        Passed = TranslatorRef.Preprocessor.SecondaryQualityInspection(GetData, CustomWords);

                        if (!Passed && MaxTry > 0)
                        {
                            Delay(CancelToken, Phoenix.Config.ReTryWaitTime);
                            MaxTry--;
                        }
                        else
                        {
                            break;
                        }
                    } while (!Passed);

                    if (GetData.Length == 0) Phoenix.KeyData.GetData(Type).ReportError(CurrentApiKey);

                    TransText = GetData;
                    Call.Output();
                    SetType = "ChatGpt";

                    if (GetData.Trim().Length == 0) Owner.RegisterFailure(this); else Owner.RegisterSuccess(this);
                }
            }
            else if (this.ApiRef is GeminiApi SetGemini)
            {
                if (Phoenix.Config.GetPlatformData(GeminiApi.Type).Enable)
                {
                    var Type = GeminiApi.Type;
                    AICall Call = new AICall();
                    string GetData = null;
                    bool Passed = false;
                    int MaxTry = MaxTranslationAttempts;
                    string CurrentApiKey = "";

                    do
                    {
                        CancelToken.ThrowIfCancellationRequested();
                        CurrentApiKey = Phoenix.KeyData.GetData(Type).GetFirstKey();
                        GetData = SetGemini.QuickTrans(CurrentApiKey, CustomWords, Source, From, To, UseAIMemory, AIMemoryQueryLimit, AIParam, ref Call).Trim();
                        Passed = TranslatorRef.Preprocessor.SecondaryQualityInspection(GetData, CustomWords);

                        if (!Passed && MaxTry > 0)
                        {
                            Delay(CancelToken, Phoenix.Config.ReTryWaitTime);
                            MaxTry--;
                        }
                        else
                        {
                            break;
                        }
                    } while (!Passed);

                    if (GetData.Length == 0) Phoenix.KeyData.GetData(Type).ReportError(CurrentApiKey);

                    TransText = GetData;
                    Call.Output();
                    SetType = "Gemini";

                    if (GetData.Trim().Length == 0) Owner.RegisterFailure(this); else Owner.RegisterSuccess(this);
                }
            }
            else if (this.ApiRef is DeepSeekApi SetDeepSeek)
            {
                if (Phoenix.Config.GetPlatformData(DeepSeekApi.Type).Enable)
                {
                    var Type = DeepSeekApi.Type;
                    AICall Call = new AICall();
                    string GetData = null;
                    bool Passed = false;
                    int MaxTry = MaxTranslationAttempts;
                    string CurrentApiKey = "";

                    do
                    {
                        CancelToken.ThrowIfCancellationRequested();
                        CurrentApiKey = Phoenix.KeyData.GetData(Type).GetFirstKey();
                        GetData = SetDeepSeek.QuickTrans(CurrentApiKey, CustomWords, Source, From, To, UseAIMemory, AIMemoryQueryLimit, AIParam, ref Call).Trim();
                        Passed = TranslatorRef.Preprocessor.SecondaryQualityInspection(GetData, CustomWords);

                        if (!Passed && MaxTry > 0)
                        {
                            Delay(CancelToken, Phoenix.Config.ReTryWaitTime);
                            MaxTry--;
                        }
                        else
                        {
                            break;
                        }
                    } while (!Passed);

                    if (GetData.Length == 0) Phoenix.KeyData.GetData(Type).ReportError(CurrentApiKey);

                    TransText = GetData;
                    Call.Output();
                    SetType = "DeepSeek";

                    if (GetData.Trim().Length == 0) Owner.RegisterFailure(this); else Owner.RegisterSuccess(this);
                }
            }
            else if (this.ApiRef is CustomAIApi SetCustomAI)
            {
                if (Phoenix.Config.GetPlatformData(SetCustomAI.CustomID).Enable)
                {
                    var Type = SetCustomAI.CustomID;
                    AICall Call = new AICall();
                    string GetData = null;
                    bool Passed = false;
                    int MaxTry = MaxTranslationAttempts;
                    string CurrentApiKey = "";

                    do
                    {
                        CancelToken.ThrowIfCancellationRequested();
                        CurrentApiKey = Phoenix.KeyData.GetData(Type).GetFirstKey();
                        GetData = SetCustomAI.QuickTrans(CurrentApiKey, CustomWords, Source, From, To, UseAIMemory, AIMemoryQueryLimit, AIParam, ref Call).Trim();
                        Passed = TranslatorRef.Preprocessor.SecondaryQualityInspection(GetData, CustomWords);

                        if (!Passed && MaxTry > 0)
                        {
                            Delay(CancelToken, Phoenix.Config.ReTryWaitTime);
                            MaxTry--;
                        }
                        else
                        {
                            break;
                        }
                    } while (!Passed);

                    if (GetData.Length == 0) Phoenix.KeyData.GetData(Type).ReportError(CurrentApiKey);

                    TransText = GetData;
                    Call.Output();
                    SetType = Type.ToString();

                    if (GetData.Trim().Length == 0) Owner.RegisterFailure(this); else Owner.RegisterSuccess(this);
                }
            }
            else if (this.ApiRef is CustomLocalAIApi SetCustomLocal)
            {
                if (Phoenix.Config.GetPlatformData(SetCustomLocal.CustomID).Enable)
                {
                    var Type = SetCustomLocal.CustomID;
                    AICall Call = new AICall();
                    string GetData = null;
                    bool Passed = false;
                    int MaxTry = MaxTranslationAttempts;

                    do
                    {
                        CancelToken.ThrowIfCancellationRequested();
                        GetData = SetCustomLocal.QuickTrans(CustomWords, Source, From, To, UseAIMemory, AIMemoryQueryLimit, AIParam, ref Call).Trim();
                        Passed = TranslatorRef.Preprocessor.SecondaryQualityInspection(GetData, CustomWords);

                        if (!Passed && MaxTry > 0)
                        {
                            Delay(CancelToken, Phoenix.Config.ReTryWaitTime);
                            MaxTry--;
                        }
                        else
                        {
                            break;
                        }
                    } while (!Passed);

                    TransText = GetData;
                    Call.Output();
                    SetType = Type.ToString();

                    if (GetData.Trim().Length == 0) Owner.RegisterFailure(this); else Owner.RegisterSuccess(this);
                }
            }
            else if (this.ApiRef is HumanTranslationApi SetHuman)
            {
                if (Phoenix.Config.GetPlatformData(HumanTranslationApi.Type).Enable)
                {
                    var Type = SetHuman.CustomID;
                    AICall Call = new AICall();
                    string GetData = null;
                    bool Passed = false;
                    int MaxTry = MaxTranslationAttempts;

                    do
                    {
                        CancelToken.ThrowIfCancellationRequested();
                        GetData = SetHuman.CallHuman(CustomWords, Source, From, To, UseAIMemory, AIMemoryQueryLimit, AIParam, ref Call).Trim();
                        Passed = TranslatorRef.Preprocessor.SecondaryQualityInspection(GetData, CustomWords);

                        if (!Passed && MaxTry > 0)
                        {
                            Delay(CancelToken, Phoenix.Config.ReTryWaitTime);
                            MaxTry--;
                        }
                        else
                        {
                            break;
                        }
                    } while (!Passed);

                    TransText = GetData;
                    Call.Output();
                    SetType = Type.ToString();

                    if (GetData.Trim().Length == 0) Owner.RegisterFailure(this); else Owner.RegisterSuccess(this);
                }
            }

            return TransText.Trim();
        }
    }
}
