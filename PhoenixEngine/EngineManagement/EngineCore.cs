using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.EngineManagement.Unit;
using PhoenixEngine.PlatformManagement;
using PhoenixEngine.PlatformManagement.LocalAI;
using PhoenixEngine.RequestManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManagement;
using static PhoenixEngine.EngineManagement.DataTransmission;

namespace PhoenixEngine.TranslateManage
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

        /// <summary>
        /// Multithreaded translation entry
        /// </summary>
        /// <param name="Source"></param>
        /// <param name="Target"></param>
        /// <param name="SourceStr"></param>
        /// <returns></returns>
        public UnitGroup CallOnce(Translator TranslatorRef, TranslationPreprocessor Preprocessor,UnitGroup Item,
        Languages From, Languages To,string AIParam, bool CanSleep, bool UseAIMemory)
        {
            Dictionary<string, UnitSequence> Sequences = null;

            Item.StartPreProcess(Preprocessor,From,To, ref Sequences);
            Item.UPDateSequences(Sequences);

            Item.CenterPreProcess(From, To, ref Sequences);
            Item.UPDateSequences(Sequences);

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
                    NextCall:

                    string GetTrans = "";

                    GetTrans = CurrentEngine.Call(ref Item,ref Sequences,From,To,
                    true, Phoenix.Config.ContextLimit,
                    AIParam);

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

                    for (int i = 0; i < PassUnits.Count; i++)
                    {
                        var PassUnit = PassUnits[i];
                        Sequences[PassUnit.Key].CanSkip = true;
                        Sequences[PassUnit.Key].Step = 6;
                        Sequences[PassUnit.Key].Data = PassUnit.Translated;
                    }

                    Item.UPDateSequences(Sequences);

                    if (!Passer.TryPass(ref NotPassUnits, ref PassUnits))
                    {
                        goto NextCall;
                    }

                    Item.EndPreProcess(From, To, ref Sequences);

                    Item.UPDateCloudData(TranslatorRef,Sequences);

                    Item.EndGeneratePlaceholder(From, To, ref Sequences);

                    if (UseAIMemory)
                    {
                        Item.UPDateAIMemory(TranslatorRef, Sequences);
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

            public bool SecondaryQualityInspection(string Source, List<ReplaceTag> CustomWords)
            {
                if (string.IsNullOrEmpty(Source))
                    return false;

                if (CustomWords == null || CustomWords.Count == 0)
                return true;

                HashSet<string> FoundKeys = new HashSet<string>();

                int Index = 0;
                while (Index < Source.Length)
                {
                    // Detect "__"
                    if (Source[Index] == '_' &&
                        Index + 1 < Source.Length &&
                        Source[Index + 1] == '_')
                    {
                        int PrefixLength = 0;

                        // __(Number)__
                        if (Index + 2 < Source.Length && Source[Index + 2] == '(')
                        {
                            PrefixLength = 3;
                        }
                        // __P(Number)__
                        else if (Index + 3 < Source.Length &&
                                 Source[Index + 2] == 'P' &&
                                 Source[Index + 3] == '(')
                        {
                            PrefixLength = 4;
                        }

                        if (PrefixLength > 0)
                        {
                            int Start = Index;
                            int Cursor = Index + PrefixLength;

                            while (Cursor < Source.Length && char.IsDigit(Source[Cursor]))
                            {
                                Cursor++;
                            }

                            if (Cursor + 2 < Source.Length &&
                                Source[Cursor] == ')' &&
                                Source[Cursor + 1] == '_' &&
                                Source[Cursor + 2] == '_')
                            {
                                int TokenLength = Cursor - Start + 3;
                                string Token = Source.Substring(Start, TokenLength);

                                string NormalizedToken = Regex.Replace(Token, @"[\s\u3000]", "");

                                if (CustomWords.Any(T => T.Key == NormalizedToken))
                                {
                                    FoundKeys.Add(NormalizedToken);
                                }

                                Index += TokenLength;
                                continue;
                            }
                        }
                    }

                    Index++;
                }

                return FoundKeys.Count == CustomWords.Count;
            }
            public string Call(ref UnitGroup Source,ref Dictionary<string, UnitSequence> Sequences,
               Languages From,Languages To,bool UseAIMemory,int AIMemoryQueryCount,string AIParam)
            {
                Source.StartGeneratePlaceholder(From, To, ref Sequences);
                Source.UPDateSequences(Sequences);

                string GetSource = Source.GenContent();

                if (GetSource.Length == 0)
                {
                    return string.Empty;
                }

                if (From == Languages.Auto)
                {
                    From = LanguageHelper.DetectLanguageByLine(GetSource);
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
                                    Passed = SecondaryQualityInspection(GetData, CustomWords);

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
                                    Passed = SecondaryQualityInspection(GetData, CustomWords);

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
                                LMStudio SetApi = ((LMStudio)this.ApiRef);
                                AICall Call = new AICall();

                                string GetData = null;
                                bool Passed = false;
                                int MaxTry = MaxTranslationAttempts;

                                do
                                {
                                    GetData = SetApi.QuickTrans(CustomWords, Source, From, To, UseAIMemory, AIMemoryQueryCount, AIParam, ref Call).Trim();
                                    Passed = SecondaryQualityInspection(GetData, CustomWords);

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

                                    GetData = SetApi.QuickTrans(CurrentApiKey, CustomWords, Source, From, To, UseAIMemory, AIMemoryQueryCount, AIParam, ref Call).Trim();
                                    Passed = SecondaryQualityInspection(GetData, CustomWords);

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

                                    GetData = SetApi.QuickTrans(CurrentApiKey, CustomWords, Source, From, To, UseAIMemory, AIMemoryQueryCount, AIParam, ref Call).Trim();
                                    Passed = SecondaryQualityInspection(GetData, CustomWords);

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

                                    GetData = SetApi.QuickTrans(CurrentApiKey, CustomWords, Source, From, To, UseAIMemory, AIMemoryQueryCount, AIParam, ref Call).Trim();
                                    Passed = SecondaryQualityInspection(GetData, CustomWords);

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

                                    GetData = SetApi.QuickTrans(CurrentApiKey,CustomWords,Source, From, To, UseAIMemory, AIMemoryQueryCount, AIParam, ref Call).Trim();
                                    Passed = SecondaryQualityInspection(GetData,CustomWords);

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
                                    GetData = SetApi.QuickTrans(CustomWords,Source, From, To, UseAIMemory, AIMemoryQueryCount, AIParam, ref Call).Trim();
                                    Passed = SecondaryQualityInspection(GetData, CustomWords);

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
