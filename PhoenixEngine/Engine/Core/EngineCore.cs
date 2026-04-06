using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using PhoenixEngine.Events;
using PhoenixEngine.Language;
using PhoenixEngine.P_Delegate;
using PhoenixEngine.Platform;
using PhoenixEngine.Platform.LocalAI;
using PhoenixEngine.Request;
using PhoenixEngine.Sequence;
using PhoenixEngine.Translate;
using PhoenixEngine.Unit;

namespace PhoenixEngine.Engine
{
    public class EngineCore
    {
        public static EngineCore Instance = new EngineCore();
        private static readonly object _SortLock = new object();

        public static void SortByCallCountDescending()
        {
            lock (_SortLock)
            {
                EngineNodes.Sort((a, b) => b.CallCountDown.CompareTo(a.CallCountDown));
            }
        }

        public static List<EngineNode> EngineNodes = new List<EngineNode>();

        public static void Init()
        {
            ReloadEngine();
        }

        private static readonly object _EngineLock = new object();
        public static void RemoveEngine<T>()
        {
            lock (_EngineLock)
            {
                EngineNodes.RemoveAll(e => e is T);
            }
        }

        public static void ReloadEngine()
        {
            lock (_EngineLock)
            {
                EngineNodes.Clear();
                PlatformApiKeys KeyData = null;

                // ChatGPT support
                var ChatGptConfig = Phoenix.Config.GetPlatformData(ChatGptApi.Type);
                KeyData = Phoenix.KeyData.GetData(ChatGptApi.Type);
                if (ChatGptConfig.Enable && KeyData.HaveKey())
                {
                    ChatGptApi NChatGptApi = new ChatGptApi();
                    NChatGptApi.Init(0, Phoenix.AIMemory, Phoenix.Config,ProxyCenter.CurrentProxy);
                    EngineNodes.Add(new EngineNode(NChatGptApi, KeyData.GetKeyCount()));
                }

                // Gemini support
                var GeminiConfig = Phoenix.Config.GetPlatformData(GeminiApi.Type);
                KeyData = Phoenix.KeyData.GetData(GeminiApi.Type);
                if (GeminiConfig.Enable && KeyData.HaveKey())
                {
                    GeminiApi NGeminiApi = new GeminiApi();
                    NGeminiApi.Init(0, Phoenix.AIMemory, Phoenix.Config, ProxyCenter.CurrentProxy);
                    EngineNodes.Add(new EngineNode(NGeminiApi, KeyData.GetKeyCount()));
                }

                // DeepSeek support
                var DeepSeekConfig = Phoenix.Config.GetPlatformData(DeepSeekApi.Type);
                KeyData = Phoenix.KeyData.GetData(DeepSeekApi.Type);
                if (DeepSeekConfig.Enable && KeyData.HaveKey())
                {
                    DeepSeekApi NDeepSeekApi = new DeepSeekApi();
                    NDeepSeekApi.Init(0, Phoenix.AIMemory, Phoenix.Config, ProxyCenter.CurrentProxy);
                    EngineNodes.Add(new EngineNode(NDeepSeekApi, KeyData.GetKeyCount()));
                }

                //LocalAI(LM) support
                var LMLocalAIConfig = Phoenix.Config.GetPlatformData(LMStudio.Type);
                KeyData = null;
                if (LMLocalAIConfig.Enable)
                {
                    LMStudio NLMStudio = new LMStudio();
                    NLMStudio.Init(0, Phoenix.AIMemory, Phoenix.Config);
                    EngineNodes.Add(new EngineNode(NLMStudio, 1));
                }

                // DeepL support
                var DeepLConfig = Phoenix.Config.GetPlatformData(DeepLApi.Type);
                KeyData = Phoenix.KeyData.GetData(DeepLApi.Type);
                if (DeepLConfig.Enable && KeyData.HaveKey())
                {
                    DeepLApi NDeepLApi = new DeepLApi();
                    NDeepLApi.Init(0, Phoenix.Config, ProxyCenter.CurrentProxy);
                    EngineNodes.Add(new EngineNode(NDeepLApi, KeyData.GetKeyCount()));
                }

                //Custom support
                for (int i = 0; i < Phoenix.Config.PlatformConfigs.Count; i++)
                { 
                    int GetKey = Phoenix.Config.PlatformConfigs.ElementAt(i).Key;

                    var CustomInFo = Phoenix.Config.PlatformConfigs[GetKey].CustomInFo;
                    if (CustomInFo != null)
                    {
                        if (CustomInFo.CustomID > 0 && Phoenix.Config.PlatformConfigs[GetKey].Enable)
                        {
                            KeyData = Phoenix.KeyData.GetData(CustomInFo.CustomID);
                            switch (CustomInFo.Type)
                            {
                                case CustomPlatformType.LocalAI:
                                    {
                                        CustomLocalAIApi NCustomLocalAIApi = new CustomLocalAIApi();
                                        NCustomLocalAIApi.Init(CustomInFo.CustomID, Phoenix.AIMemory, Phoenix.Config);
                                        EngineNodes.Add(new EngineNode(NCustomLocalAIApi, 1));
                                    }
                                break;
                                case CustomPlatformType.CloudAI:
                                    {
                                        CustomAIApi NCustomAIApi = new CustomAIApi();
                                        NCustomAIApi.Init(CustomInFo.CustomID, Phoenix.AIMemory, Phoenix.Config, ProxyCenter.CurrentProxy);
                                        EngineNodes.Add(new EngineNode(NCustomAIApi, KeyData.GetKeyCount()));
                                    }
                                break;
                                case CustomPlatformType.Traditional:
                                    {
                                        CustomApi NCustomApi = new CustomApi();
                                        NCustomApi.Init(CustomInFo.CustomID, Phoenix.Config, ProxyCenter.CurrentProxy);
                                        EngineNodes.Add(new EngineNode(NCustomApi, KeyData.GetKeyCount()));
                                    }
                                break;
                            }
                        }
                    }
                }

                KeyData = null;
            }
        }

        public static object SwitchLocker = new object();


        public static void CheckCanSkip(Dictionary<string, UnitSequence> Sequences,ref UnitGroup Item)
        {
            foreach (var GetSeq in new Dictionary<string,UnitSequence>(Sequences))
            {
                if (GetSeq.Value.CanSkip)
                {
                    foreach (var GetBaseUnit in Item.Units)
                    {
                        if (GetBaseUnit.Key.Equals(GetSeq.Key))
                        {
                            if (GetBaseUnit.ApplyStateChange(UnitTranslationState.Skipped).ControlSignal.Sign > 0)
                            {
                                Sequences[GetSeq.Key].CanSkip = false;
                                continue;
                            }
                        }
                    }
                }
            }
        }
        public static void CheckCanGeneratePlaceholder(Dictionary<string, UnitSequence> Sequences, ref UnitGroup Item)
        {
            foreach (var GetSeq in new Dictionary<string, UnitSequence>(Sequences))
            {
                foreach (var GetBaseUnit in Item.Units)
                {
                    if (GetBaseUnit.Key.Equals(GetSeq.Key))
                    {
                        var GetSignResult = GetBaseUnit.ApplyStateChange(UnitTranslationState.GeneratePlaceholder);
                        if (GetSignResult.ControlSignal.Sign > 0)
                        {
                            Item.ReSet(GetSignResult.ControlSignal.Index);
                            continue;
                        }
                    }
                }
            }
        }



        /// <summary>
        /// Multithreaded translation entry
        /// </summary>
        /// <param name="Source"></param>
        /// <param name="Target"></param>
        /// <param name="SourceStr"></param>
        /// <returns></returns>
        public UnitGroup CallOnce(Translator TranslatorRef, TranslationPreprocessor Preprocessor,UnitGroup Item,
        Languages From, Languages To,string AIParam, bool CanSleep, bool UseAIMemory,bool CanUPDate)
        {
            if (!Item.ApplyStateChange(UnitTranslationState.Preparing).CanDo(-1))
            {
                return Item;
            }

            Dictionary<string, UnitSequence> Sequences = null;

            Item.StartPreProcess(Preprocessor,From,To, ref Sequences);

            CheckCanSkip(Sequences,ref Item);

            Item.CenterPreProcess(TranslatorRef, From, To, ref Sequences);

            CheckCanSkip(Sequences, ref Item);

            EngineNode CurrentEngine = null;

            while (CurrentEngine == null)
            {
                lock (SwitchLocker)
                {
                    try
                    {
                        for (int i = 0; i < EngineNodes.Count; i++)
                        {
                            if (EngineNodes[i].CallCountDown > 0)
                            {
                                EngineNodes[i].CallCountDown--;

                                CurrentEngine = EngineNodes[i];

                                SortByCallCountDescending();

                                break;
                            }
                        }
                    }
                    catch { }
                }

                if (CurrentEngine != null)
                {
                    int MaxTry = 10;

                    NextCall:

                    string GetTrans = "";

                    if (!Item.ApplyStateChange(UnitTranslationState.Translating).CanDo(-1))
                    {
                        return Item;
                    }

                    string SetType = "";

                    GetTrans = CurrentEngine.Call(TranslatorRef,ref Item,ref Sequences,From,To,
                    true, Phoenix.Config.ContextLimit,
                    AIParam,ref SetType);

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
                        CurrentEngine.BeginSleep();
                    }

                    ConfirmPasser Passer = Item.AnalysisContent(GetTrans);

                    List<BaseUnit> NotPassUnits = new List<BaseUnit>();
                    List<BaseUnit> PassUnits = new List<BaseUnit>();

                    bool IsDeepL = false;

                    if (SetType.Equals("DeepL"))
                    {
                        IsDeepL = true;
                    }

                    bool Passed = Passer.TryPass(ref NotPassUnits, ref PassUnits, IsDeepL);

                    for (int i = 0; i < PassUnits.Count; i++)
                    {
                        var PassUnit = PassUnits[i];
                        Sequences[PassUnit.Key].CanSkip = true;
                        Sequences[PassUnit.Key].Step = 6;
                        Sequences[PassUnit.Key].Data = PassUnit.Translated;
                    }

                    if (!Passed)
                    {
                        //I hadn't anticipated that DeepL would consistently omit HTML content—and, as it happened, this specific section lacked any throttling and executed a direct `goto`.

                        Thread.Sleep(Phoenix.Config.ThrottleDelayMs);

                        //Continue to impose penalties.
                        if (PassUnits.Count == 0)
                        {
                            Thread.Sleep(1000);
                        }

                        //Preventing Infinite Loops
                        if (MaxTry > 0)
                        {
                            MaxTry--;

                            goto NextCall;
                        }
                    }

                    foreach (var Get in Item.Units)
                    {
                        if (Get.Original.Equals("Moorside Inn"))
                        { 
                        
                        }
                    }

                    Item.EndPreProcess(From, To, ref Sequences);

                    Item.EndGeneratePlaceholder(From, To, ref Sequences);

                    if (UseAIMemory)
                    {
                        Item.UPDateAIMemory(TranslatorRef, Sequences);
                    }

                    if (CanUPDate)
                    {
                        Item.UPDateCloudData(TranslatorRef, Sequences);
                    }

                    if (!Item.ApplyStateChange(UnitTranslationState.Completed).CanDo(-1))
                    {
                        return Item;
                    }

                    return Item;
                }

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
            public object ApiRef = new object();
            public int CallCountDown = 0;
            public int MaxCallCount = 0;

            public int SleepBySec = 0;

            public EngineNode(object Api, int MaxCallCount)
            {
                this.ApiRef = Api;
                this.MaxCallCount = MaxCallCount;
                this.CallCountDown = this.MaxCallCount;

                this.SleepBySec = 1;
            }

            public EngineNode(object Api, int MaxCallCount, int SleepBySec)
            {
                this.ApiRef = Api;
                this.MaxCallCount = MaxCallCount;
                this.CallCountDown = this.MaxCallCount;

                this.SleepBySec = SleepBySec;
            }

            public void BeginSleep()
            {
                for (int i = 0; i < SleepBySec; i++)
                {
                    Thread.Sleep(1000);
                }
            }

            public static bool IsLocked(object Obj)
            {
                bool LockTaken = false;
                Monitor.TryEnter(Obj, 0, ref LockTaken);
                if (LockTaken) Monitor.Exit(Obj);
                return !LockTaken;
            }
            public string Call(Translator TranslatorRef, ref UnitGroup Source,ref Dictionary<string, UnitSequence> Sequences,
               Languages From,Languages To,bool UseAIMemory,int AIMemoryQueryLimit,string AIParam,ref string SetType)
            {
                Source.StartGeneratePlaceholder(TranslatorRef, From, To, ref Sequences);

                CheckCanGeneratePlaceholder(Sequences, ref Source);
                CheckCanSkip(Sequences, ref Source);

                string GetSource = Source.GenContent();

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

                if (GetSource.Length > 0)
                {
                    List<ReplaceTag> CustomWords = new List<ReplaceTag>();

                    foreach (var GetSeq in new Dictionary<string, UnitSequence>(Sequences))
                    {
                        CustomWords.AddRange(GetSeq.Value.Preprocessor.ReplaceTags);
                    }

                    if (this.ApiRef is DeepLApi || this.ApiRef is CustomApi)
                    {

                        if (this.ApiRef is DeepLApi)
                        {
                            if (Phoenix.Config.GetPlatformData(DeepLApi.Type).Enable)
                            {
                                var Type = DeepLApi.Type;
                                DeepLApi SetApi = (DeepLApi)this.ApiRef;
                                PlatformCall Call = new PlatformCall();

                                string GetData = null;
                                bool Passed = false;
                                int MaxTry = MaxTranslationAttempts;
                                string CurrentApiKey = "";

                                //Detecting the quality of AI-translated content
                                do
                                {
                                    CurrentApiKey = Phoenix.KeyData.GetData(Type).GetFirstKey();

                                    GetData = SetApi.QuickTrans(CurrentApiKey, Source, From, To, ref Call).Trim();
                                    Passed = TranslationPreprocessor.Instance.SecondaryQualityInspection(GetData, CustomWords);

                                    if (!Passed && MaxTry > 0)
                                    {
                                        Thread.Sleep(Phoenix.Config.ReTryWaitTime);
                                        MaxTry--;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                } while (!Passed);

                                if (GetData.Length == 0)
                                {
                                    Phoenix.KeyData.GetData(Type).ReportError(CurrentApiKey);
                                }

                                TransText = GetData;

                                Call.Output();

                                SetType = "DeepL";

                                if (GetData.Trim().Length == 0)
                                {
                                    this.CallCountDown = 0;
                                }
                            }
                            else
                            {
                                this.CallCountDown = 0;
                            }
                        }
                        else
                            if (this.ApiRef is CustomApi)
                        {
                            CustomApi SetApi = (CustomApi)this.ApiRef;

                            if (Phoenix.Config.GetPlatformData(SetApi.CustomID).Enable)
                            {
                                var Type = SetApi.CustomID;
                                PlatformCall Call = new PlatformCall();

                                string GetData = null;
                                bool Passed = false;
                                int MaxTry = MaxTranslationAttempts;
                                string CurrentApiKey = "";

                                //Detecting the quality of AI-translated content
                                do
                                {
                                    CurrentApiKey = Phoenix.KeyData.GetData(Type).GetFirstKey();

                                    GetData = SetApi.QuickTrans(CurrentApiKey, Source, From, To, ref Call).Trim();
                                    Passed = TranslationPreprocessor.Instance.SecondaryQualityInspection(GetData, CustomWords);

                                    if (!Passed && MaxTry > 0)
                                    {
                                        Thread.Sleep(Phoenix.Config.ReTryWaitTime);
                                        MaxTry--;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                } while (!Passed);

                                if (GetData.Length == 0)
                                {
                                    Phoenix.KeyData.GetData(Type).ReportError(CurrentApiKey);
                                }

                                TransText = GetData;

                                Call.Output();

                                SetType = SetApi.CustomID.ToString();

                                if (GetData.Trim().Length == 0)
                                {
                                    this.CallCountDown = 0;
                                }
                            }
                            else
                            {
                                this.CallCountDown = 0;
                            }
                        }
                    }
                    else
                    if (this.ApiRef is ChatGptApi || this.ApiRef is GeminiApi || this.ApiRef is DeepSeekApi || this.ApiRef is LMStudio || this.ApiRef is CustomAIApi || this.ApiRef is CustomLocalAIApi)
                    {
                        if (this.ApiRef is LMStudio)
                        {
                            if (Phoenix.Config.GetPlatformData(LMStudio.Type).Enable)
                            {
                                if (IsLocked(LMStudio.SingleLock))
                                {
                                    this.CallCountDown = 0;

                                    while (IsLocked(LMStudio.SingleLock))
                                    {
                                        Thread.Sleep(200);
                                    }
                                }
                               

                                LMStudio SetApi = ((LMStudio)this.ApiRef);
                                AICall Call = new AICall();

                                string GetData = null;
                                bool Passed = false;
                                int MaxTry = MaxTranslationAttempts;

                                do
                                {
                                    GetData = SetApi.QuickTrans(CustomWords, Source, From, To, UseAIMemory, AIMemoryQueryLimit, AIParam, ref Call).Trim();
                                    Passed = TranslationPreprocessor.Instance.SecondaryQualityInspection(GetData, CustomWords);

                                    if (!Passed && MaxTry > 0)
                                    {
                                        Thread.Sleep(Phoenix.Config.ReTryWaitTime);
                                        MaxTry--;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                } while (!Passed);

                                TransText = GetData;

                                if (GetData.Trim().Length == 0)
                                {
                                    this.CallCountDown = 0;
                                }
                                Call.Output();

                                SetType = "LMStudio";
                            }
                            else
                            {
                                this.CallCountDown = 0;
                            }
                        }
                        else
                          if (this.ApiRef is ChatGptApi)
                        {
                            if (Phoenix.Config.GetPlatformData(ChatGptApi.Type).Enable)
                            {
                                var Type = ChatGptApi.Type;
                                ChatGptApi SetApi = ((ChatGptApi)this.ApiRef);
                                AICall Call = new AICall();

                                string GetData = null;
                                bool Passed = false;
                                int MaxTry = MaxTranslationAttempts;
                                string CurrentApiKey = "";

                                do
                                {
                                    CurrentApiKey = Phoenix.KeyData.GetData(Type).GetFirstKey();

                                    GetData = SetApi.QuickTrans(CurrentApiKey, CustomWords, Source, From, To, UseAIMemory, AIMemoryQueryLimit, AIParam, ref Call).Trim();
                                    Passed = TranslationPreprocessor.Instance.SecondaryQualityInspection(GetData, CustomWords);

                                    if (!Passed && MaxTry > 0)
                                    {
                                        Thread.Sleep(Phoenix.Config.ReTryWaitTime);
                                        MaxTry--;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                } while (!Passed);

                                if (GetData.Length == 0)
                                {
                                    Phoenix.KeyData.GetData(Type).ReportError(CurrentApiKey);
                                }

                                TransText = GetData;

                                if (GetData.Trim().Length == 0)
                                {
                                    this.CallCountDown = 0;
                                }

                                Call.Output();

                                SetType = "ChatGpt";
                            }
                            else
                            {
                                this.CallCountDown = 0;
                            }
                        }
                        else
                          if (this.ApiRef is GeminiApi)
                        {
                            if (Phoenix.Config.GetPlatformData(GeminiApi.Type).Enable)
                            {
                                var Type = GeminiApi.Type;
                                GeminiApi SetApi = ((GeminiApi)this.ApiRef);

                                AICall Call = new AICall();

                                string GetData = null;
                                bool Passed = false;
                                int MaxTry = MaxTranslationAttempts;
                                string CurrentApiKey = "";

                                do
                                {
                                    CurrentApiKey = Phoenix.KeyData.GetData(Type).GetFirstKey();

                                    GetData = SetApi.QuickTrans(CurrentApiKey, CustomWords, Source, From, To, UseAIMemory, AIMemoryQueryLimit, AIParam, ref Call).Trim();
                                    Passed = TranslationPreprocessor.Instance.SecondaryQualityInspection(GetData, CustomWords);

                                    if (!Passed && MaxTry > 0)
                                    {
                                        Thread.Sleep(Phoenix.Config.ReTryWaitTime);
                                        MaxTry--;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                } while (!Passed);

                                if (GetData.Length == 0)
                                {
                                    Phoenix.KeyData.GetData(Type).ReportError(CurrentApiKey);
                                }

                                TransText = GetData;

                                if (GetData.Trim().Length == 0)
                                {
                                    this.CallCountDown = 0;
                                }

                                Call.Output();

                                SetType = "Gemini";
                            }
                            else
                            {
                                this.CallCountDown = 0;
                            }
                        }
                        else
                          if (this.ApiRef is DeepSeekApi)
                        {
                            if (Phoenix.Config.GetPlatformData(DeepSeekApi.Type).Enable)
                            {
                                var Type = DeepSeekApi.Type;
                                DeepSeekApi SetApi = ((DeepSeekApi)this.ApiRef);
                                AICall Call = new AICall();

                                string GetData = null;
                                bool Passed = false;
                                int MaxTry = MaxTranslationAttempts;
                                string CurrentApiKey = "";

                                //Detecting the quality of AI-translated content
                                do
                                {
                                    CurrentApiKey = Phoenix.KeyData.GetData(Type).GetFirstKey();

                                    GetData = SetApi.QuickTrans(CurrentApiKey, CustomWords, Source, From, To, UseAIMemory, AIMemoryQueryLimit, AIParam, ref Call).Trim();
                                    Passed = TranslationPreprocessor.Instance.SecondaryQualityInspection(GetData, CustomWords);

                                    if (!Passed && MaxTry > 0)
                                    {
                                        Thread.Sleep(Phoenix.Config.ReTryWaitTime);
                                        MaxTry--;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                } while (!Passed);

                                if (GetData.Length == 0)
                                {
                                    Phoenix.KeyData.GetData(Type).ReportError(CurrentApiKey);
                                }

                                TransText = GetData;

                                if (GetData.Trim().Length == 0)
                                {
                                    this.CallCountDown = 0;
                                }

                                Call.Output();

                                SetType = "DeepSeek";
                            }
                            else
                            {
                                this.CallCountDown = 0;
                            }
                        }
                        else
                          if (this.ApiRef is CustomAIApi)
                        {
                            CustomAIApi SetApi = ((CustomAIApi)this.ApiRef);

                            if (Phoenix.Config.GetPlatformData(SetApi.CustomID).Enable)
                            {
                                var Type = SetApi.CustomID;
                                AICall Call = new AICall();

                                string GetData = null;
                                bool Passed = false;
                                int MaxTry = MaxTranslationAttempts;
                                string CurrentApiKey = "";

                                //Detecting the quality of AI-translated content
                                do
                                {
                                    CurrentApiKey = Phoenix.KeyData.GetData(Type).GetFirstKey();

                                    GetData = SetApi.QuickTrans(CurrentApiKey,CustomWords,Source, From, To, UseAIMemory, AIMemoryQueryLimit, AIParam, ref Call).Trim();
                                    Passed = TranslationPreprocessor.Instance.SecondaryQualityInspection(GetData,CustomWords);

                                    if (!Passed && MaxTry > 0)
                                    {
                                        Thread.Sleep(Phoenix.Config.ReTryWaitTime);
                                        MaxTry--;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                } while (!Passed);

                                if (GetData.Length == 0)
                                {
                                    Phoenix.KeyData.GetData(Type).ReportError(CurrentApiKey);
                                }

                                TransText = GetData;

                                if (GetData.Trim().Length == 0)
                                {
                                    this.CallCountDown = 0;
                                }

                                Call.Output();

                                SetType = SetApi.CustomID.ToString();
                            }
                            else
                            {
                                this.CallCountDown = 0;
                            }
                        }
                        else
                          if (this.ApiRef is CustomLocalAIApi)
                        {
                            CustomLocalAIApi SetApi = ((CustomLocalAIApi)this.ApiRef);

                            if (Phoenix.Config.GetPlatformData(SetApi.CustomID).Enable)
                            {
                                var Type = SetApi.CustomID;
                                AICall Call = new AICall();

                                string GetData = null;
                                bool Passed = false;
                                int MaxTry = MaxTranslationAttempts;

                                //Detecting the quality of AI-translated content
                                do
                                {
                                    GetData = SetApi.QuickTrans(CustomWords,Source, From, To, UseAIMemory, AIMemoryQueryLimit, AIParam, ref Call).Trim();
                                    Passed = TranslationPreprocessor.Instance.SecondaryQualityInspection(GetData, CustomWords);

                                    if (!Passed && MaxTry > 0)
                                    {
                                        Thread.Sleep(Phoenix.Config.ReTryWaitTime);
                                        MaxTry--;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                } while (!Passed);

                                TransText = GetData;

                                if (GetData.Trim().Length == 0)
                                {
                                    this.CallCountDown = 0;
                                }

                                Call.Output();

                                SetType = SetApi.CustomID.ToString();
                            }
                            else
                            {
                                this.CallCountDown = 0;
                            }
                        }
                    }

                    TransText = TransText.Trim();

                    return TransText;
                }

                return string.Empty;
            }
        }
    }
}
