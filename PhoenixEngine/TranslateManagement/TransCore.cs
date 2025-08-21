
using PhoenixEngine.ConvertManager;
using PhoenixEngine.DelegateManagement;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.PlatformManagement;
using PhoenixEngine.PlatformManagement.LocalAI;
using PhoenixEngine.TranslateCore;
using static PhoenixEngine.EngineManagement.DataTransmission;

namespace PhoenixEngine.TranslateManage
{
    // Copyright (C) 2025 YD525
    // Licensed under the GNU GPLv3
    // See LICENSE for details
    //https://github.com/YD525/PhoenixEngine
    public class TransCore
    {
        private static readonly object _SortLock = new object();

        public static void SortByCallCountDescending()
        {
            lock (_SortLock)
            {
                EngineSelects.Sort((a, b) => b.CallCountDown.CompareTo(a.CallCountDown));
            }
        }

        public static List<EngineSelect> EngineSelects = new List<EngineSelect>();

        public static void Init()
        {
            ReloadEngine();
        }

        private static readonly object _EngineLock = new object();
        public static void RemoveEngine<T>()
        {
            lock (_EngineLock)
            {
                EngineSelects.RemoveAll(e => e is T);
            }
        }

        public static void ReloadEngine()
        {
            lock (_EngineLock)
            {
                EngineSelects.Clear();

                // Google support
                if (EngineConfig.GoogleYunApiEnable &&
                    !string.IsNullOrWhiteSpace(EngineConfig.GoogleApiKey))
                {
                    EngineSelects.Add(new EngineSelect(new GoogleTransApi(), 1));
                }

                // ChatGPT support
                if (EngineConfig.ChatGptApiEnable &&
                    !string.IsNullOrWhiteSpace(EngineConfig.ChatGptKey))
                {
                    EngineSelects.Add(new EngineSelect(new ChatGptApi(), 6));
                }

                // Gemini support
                if (EngineConfig.GeminiApiEnable &&
                    !string.IsNullOrWhiteSpace(EngineConfig.GeminiKey))
                {
                    EngineSelects.Add(new EngineSelect(new GeminiApi(), 6));
                }

                // DeepSeek support
                if (EngineConfig.DeepSeekApiEnable &&
                    !string.IsNullOrWhiteSpace(EngineConfig.DeepSeekKey))
                {
                    EngineSelects.Add(new EngineSelect(new DeepSeekApi(), 6));
                }

                // Cohere support
                if (EngineConfig.CohereApiEnable &&
                    !string.IsNullOrWhiteSpace(EngineConfig.CohereKey))
                {
                    EngineSelects.Add(new EngineSelect(new CohereApi(), 6));
                }

                // Baichuan support
                if (EngineConfig.BaichuanApiEnable &&
                    !string.IsNullOrWhiteSpace(EngineConfig.BaichuanKey))
                {
                    EngineSelects.Add(new EngineSelect(new BaichuanApi(), 6));
                }

                //LocalAI(LM) support
                if (EngineConfig.LMLocalAIEnable)
                {
                    EngineSelects.Add(new EngineSelect(new LMStudio(), 1, 0));
                }

                // DeepL support
                if (EngineConfig.DeepLApiEnable &&
                    !string.IsNullOrWhiteSpace(EngineConfig.DeepLKey))
                {
                    EngineSelects.Add(new EngineSelect(new DeepLApi(), 6));
                }
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
        public string TransAny(TranslationUnit Item,ref bool CanSleep,ref bool CanAddCache,bool IsBook)
        {
            CacheCall Call = new CacheCall();

            if (string.IsNullOrEmpty(Item.SourceText))
            {
                return Item.SourceText;
            }

            if (Item.From.Equals(Item.To))
            {
                return Item.SourceText;
            }

            Call.SendString = Item.SourceText;
            string GetCacheStr = CloudDBCache.FindCache(Engine.GetModName(), Item.Key, Item.To);

            if (GetCacheStr.Trim().Length > 0)
            {
                Call.ReceiveString = GetCacheStr;

                Call.Log = "Cache From Database";

                Call.Output();

                CanSleep = false;
                return GetCacheStr;
            }

            EngineSelect? CurrentEngine = null;

            while (CurrentEngine == null)
            {
                lock (SwitchLocker)
                {
                    try
                    {
                        for (int i = 0; i < TransCore.EngineSelects.Count; i++)
                        {
                            if (TransCore.EngineSelects[i].CallCountDown > 0)
                            {
                                TransCore.EngineSelects[i].CallCountDown--;

                                CurrentEngine = TransCore.EngineSelects[i];

                                SortByCallCountDescending();

                                break;
                            }
                        }
                    }
                    catch { }
                }

                if (CurrentEngine != null)
                {
                    string GetTrans = "";
                    if (!IsBook)
                    {
                        GetTrans = CurrentEngine.Call(Item, true, EngineConfig.ContextLimit, string.Empty, ref CanAddCache);
                    }
                    else
                    {
                        GetTrans = CurrentEngine.Call(Item, false, 1, Item.AIParam, ref CanAddCache);
                    }

                    if (CanSleep)
                    {
                        CurrentEngine.BeginSleep();
                    }

                    return GetTrans;
                }

                ReloadEngine();
            }
            return string.Empty;
        }

        public class EngineSelect
        {
            public static AITranslationMemory AIMemory = new AITranslationMemory();

            public object TransEngine = new object();
            public int CallCountDown = 0;
            public int MaxCallCount = 0;

            public int SleepBySec = 0;

            public EngineSelect(object Engine, int MaxCallCount)
            {
                this.TransEngine = Engine;
                this.MaxCallCount = MaxCallCount;
                this.CallCountDown = this.MaxCallCount;

                this.SleepBySec = 1;
            }

            public EngineSelect(object Engine, int MaxCallCount, int SleepBySec)
            {
                this.TransEngine = Engine;
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

            public string Call(TranslationUnit Item,bool UseAIMemory, int AIMemoryCountLimit, string AIParam, ref bool CanAddCache)
            {
                TranslationPreprocessor NTranslationPreprocessor = new TranslationPreprocessor();

                string GetSource = Item.SourceText;
                string TransText = string.Empty;
                PlatformType CurrentPlatform = PlatformType.Null;

                if (GetSource.Length > 0)
                {
                    if (this.TransEngine is GoogleTransApi || this.TransEngine is DeepLApi)
                    {
                        bool CanTrans = false;

                        if (EngineConfig.PreTranslateEnable)
                        {
                            PreTranslateCall NPreTranslateCall = new PreTranslateCall();
                            NPreTranslateCall.Platform = PlatformType.PhoenixEngine;
                            NPreTranslateCall.FromAI = false;

                            string GetDefSource = GetSource;

                            NPreTranslateCall.SendString = GetDefSource;

                            GetSource = NTranslationPreprocessor.GeneratePlaceholderText(Engine.GetModName(),Item.From,Item.To, GetDefSource, Item.Type, out CanTrans);

                            NPreTranslateCall.ReceiveString = GetSource;

                            NPreTranslateCall.ReplaceTags = NTranslationPreprocessor.ReplaceTags;

                            NPreTranslateCall.Output();
                        }
                        else
                        {
                            CanTrans = true;
                        }

                        if (CanTrans)
                        {
                            if (this.TransEngine is GoogleTransApi)
                            {
                                if (EngineConfig.GoogleYunApiEnable)
                                {
                                    PlatformCall Call = new PlatformCall();

                                    var GetData = ConvertHelper.ObjToStr(((GoogleTransApi)this.TransEngine).Translate(GetSource, Item.From, Item.To,ref Call));

                                    TransText = GetData;

                                    Call.Output();

                                    CurrentPlatform = PlatformType.GoogleApi;

                                    CanAddCache = false;

                                    if (GetData.Trim().Length == 0)
                                    {
                                        this.CallCountDown = 0;
                                    }
                                }
                                else
                                {
                                    this.CallCountDown = 0;
                                    CanAddCache = false;
                                }
                            }
                            else
                            if (this.TransEngine is DeepLApi)
                            {
                                if (EngineConfig.DeepLApiEnable)
                                {
                                    PlatformCall Call = new PlatformCall();

                                    var GetData = ((DeepLApi)this.TransEngine).QuickTrans(GetSource, Item.From, Item.To,ref Call).Trim();

                                    if (GetData.Trim().Length > 0 && UseAIMemory)
                                    {
                                        AIMemory.AddTranslation(Item.From, GetSource, GetData);
                                    }

                                    TransText = GetData;

                                    Call.Output();

                                    CurrentPlatform = PlatformType.DeepL;

                                    if (GetData.Trim().Length == 0)
                                    {
                                        this.CallCountDown = 0;
                                        if (CanAddCache)
                                        {
                                            CanAddCache = false;
                                        }
                                    }
                                }
                                else
                                {
                                    this.CallCountDown = 0;
                                    CanAddCache = false;
                                }
                            }
                        }
                        else
                        {
                            TransText = NTranslationPreprocessor.RestoreFromPlaceholder(GetSource, Item.To);

                            this.CallCountDown++;
                        }
                    }
                    else
                    if (this.TransEngine is CohereApi || this.TransEngine is ChatGptApi || this.TransEngine is GeminiApi || this.TransEngine is DeepSeekApi || this.TransEngine is BaichuanApi || this.TransEngine is LMStudio)
                    {
                        bool CanTrans = false;

                        List<string> CustomWords = new List<string>();

                        if (EngineConfig.PreTranslateEnable)
                        {
                            PreTranslateCall NPreTranslateCall = new PreTranslateCall();
                            NPreTranslateCall.Platform = PlatformType.PhoenixEngine;
                            NPreTranslateCall.FromAI = true;

                            NPreTranslateCall.SendString = GetSource;

                            CustomWords = NTranslationPreprocessor.GeneratePlaceholderTextByAI(Engine.GetModName(), Item.From, Item.To, GetSource, Item.Type, out CanTrans);

                            NPreTranslateCall.ReplaceTags = NTranslationPreprocessor.ReplaceTags;

                            NPreTranslateCall.Output();
                        }
                        else
                        {
                            CanTrans = true;
                        }

                        if (CanTrans)
                        {
                            if (this.TransEngine is LMStudio)
                            {
                                if (EngineConfig.LMLocalAIEnable)
                                {
                                    AICall Call = new AICall();
                                    var GetData = ((LMStudio)this.TransEngine).QuickTrans(CustomWords, GetSource, Item.From, Item.To, UseAIMemory, AIMemoryCountLimit, AIParam,ref Call).Trim();

                                    if (GetData.Trim().Length > 0 && UseAIMemory)
                                    {
                                        AIMemory.AddTranslation(Item.From, GetSource, GetData);
                                    }
                                    TransText = GetData;

                                    CurrentPlatform = PlatformType.LMLocalAI;

                                    if (GetData.Trim().Length == 0)
                                    {
                                        this.CallCountDown = 0;
                                        if (CanAddCache)
                                        {
                                            CanAddCache = false;
                                        }
                                    }
                                    Call.Output();
                                }
                                else
                                {
                                    this.CallCountDown = 0;
                                    CanAddCache = false;
                                }
                            }
                            else
                            if (this.TransEngine is CohereApi)
                            {
                                if (EngineConfig.CohereApiEnable)
                                {
                                    AICall Call = new AICall();

                                    var GetData = ((CohereApi)this.TransEngine).QuickTrans(CustomWords, GetSource, Item.From, Item.To, UseAIMemory, AIMemoryCountLimit, AIParam,ref Call).Trim();

                                    if (GetData.Trim().Length > 0 && UseAIMemory)
                                    {
                                        AIMemory.AddTranslation(Item.From, GetSource, GetData);
                                    }
                                    TransText = GetData;

                                    CurrentPlatform = PlatformType.Cohere;

                                    if (GetData.Trim().Length == 0)
                                    {
                                        this.CallCountDown = 0;
                                        if (CanAddCache)
                                        {
                                            CanAddCache = false;
                                        }
                                    }

                                    Call.Output();
                                }
                                else
                                {
                                    this.CallCountDown = 0;
                                    CanAddCache = false;
                                }
                            }
                            else
                            if (this.TransEngine is ChatGptApi)
                            {
                                if (EngineConfig.ChatGptApiEnable)
                                {
                                    AICall Call = new AICall();

                                    var GetData = ((ChatGptApi)this.TransEngine).QuickTrans(CustomWords, GetSource, Item.From, Item.To, UseAIMemory, AIMemoryCountLimit, AIParam,ref Call).Trim();

                                    if (GetData.Trim().Length > 0 && UseAIMemory)
                                    {
                                        AIMemory.AddTranslation(Item.From, GetSource, GetData);
                                    }
                                    TransText = GetData;

                                    CurrentPlatform = PlatformType.ChatGpt;

                                    if (GetData.Trim().Length == 0)
                                    {
                                        this.CallCountDown = 0;
                                        if (CanAddCache)
                                        {
                                            CanAddCache = false;
                                        }
                                    }

                                    Call.Output();
                                }
                                else
                                {
                                    this.CallCountDown = 0;
                                    CanAddCache = false;
                                }
                            }
                            else
                            if (this.TransEngine is GeminiApi)
                            {
                                if (EngineConfig.GeminiApiEnable)
                                {
                                    AICall Call = new AICall();

                                    var GetData = ((GeminiApi)this.TransEngine).QuickTrans(CustomWords, GetSource, Item.From, Item.To, UseAIMemory, AIMemoryCountLimit, AIParam,ref Call).Trim();

                                    if (GetData.Trim().Length > 0 && UseAIMemory)
                                    {
                                        AIMemory.AddTranslation(Item.From, GetSource, GetData);
                                    }
                                    TransText = GetData;

                                    CurrentPlatform = PlatformType.Gemini;

                                    if (GetData.Trim().Length == 0)
                                    {
                                        this.CallCountDown = 0;
                                        if (CanAddCache)
                                        {
                                            CanAddCache = false;
                                        }
                                    }

                                    Call.Output();
                                }
                                else
                                {
                                    this.CallCountDown = 0;
                                    CanAddCache = false;
                                }
                            }
                            else
                            if (this.TransEngine is DeepSeekApi)
                            {
                                if (EngineConfig.DeepSeekApiEnable)
                                {
                                    AICall Call = new AICall();

                                    var GetData = ((DeepSeekApi)this.TransEngine).QuickTrans(CustomWords, GetSource, Item.From, Item.To, UseAIMemory, AIMemoryCountLimit, AIParam, ref Call).Trim();

                                    if (GetData.Trim().Length > 0 && UseAIMemory)
                                    {
                                        AIMemory.AddTranslation(Item.From, GetSource, GetData);
                                    }
                                    TransText = GetData;

                                    CurrentPlatform = PlatformType.DeepSeek;

                                    if (GetData.Trim().Length == 0)
                                    {
                                        this.CallCountDown = 0;
                                        if (CanAddCache)
                                        {
                                            CanAddCache = false;
                                        }
                                    }

                                    Call.Output();
                                }
                                else
                                {
                                    this.CallCountDown = 0;
                                    CanAddCache = false;
                                }
                            }
                            else
                            if (this.TransEngine is BaichuanApi)
                            {
                                if (EngineConfig.BaichuanApiEnable)
                                {
                                    AICall Call = new AICall();

                                    var GetData = ((BaichuanApi)this.TransEngine).QuickTrans(CustomWords, GetSource, Item.From, Item.To, UseAIMemory, AIMemoryCountLimit, AIParam,ref Call).Trim();

                                    if (GetData.Trim().Length > 0 && UseAIMemory)
                                    {
                                        AIMemory.AddTranslation(Item.From, GetSource, GetData);
                                    }
                                    TransText = GetData;

                                    CurrentPlatform = PlatformType.Baichuan;

                                    if (GetData.Trim().Length == 0)
                                    {
                                        this.CallCountDown = 0;
                                        if (CanAddCache)
                                        {
                                            CanAddCache = false;
                                        }
                                    }

                                    Call.Output();
                                }
                                else
                                {
                                    this.CallCountDown = 0;
                                    CanAddCache = false;
                                }
                            }
                        }
                        else
                        {
                            TransText = GetSource;

                            for (int i = 0; i < NTranslationPreprocessor.ReplaceTags.Count; i++)
                            {
                                TransText = TransText.Replace(NTranslationPreprocessor.ReplaceTags[i].Key, NTranslationPreprocessor.ReplaceTags[i].Value);
                            }

                            this.CallCountDown++;
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
