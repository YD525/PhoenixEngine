
using PhoenixEngine.ConvertManager;
using PhoenixEngine.DelegateManagement;
using PhoenixEngine.Engine;
using PhoenixEngine.PlatformManagement;
using PhoenixEngine.PlatformManagement.LocalAI;
using PhoenixEngine.TranslateCore;

namespace PhoenixEngine.TranslateManage
{
    // Copyright (C) 2025 YD525
    // Licensed under the GNU GPLv3
    // See LICENSE for details
    //https://github.com/YD525/YDSkyrimToolR/
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
                if (EngineConfig.LMLocalAIEngineEnable)
                {
                    EngineSelects.Add(new EngineSelect(new LMStudio(), 1,0));
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


        public static string AIParam = "Important: When translating, strictly keep any text inside angle brackets (< >) or square brackets ([ ]) unchanged. Do not modify, translate, or remove them.\n\n";
        /// <summary>
        /// Multithreaded translation entry
        /// </summary>
        /// <param name="Source"></param>
        /// <param name="Target"></param>
        /// <param name="SourceStr"></param>
        /// <returns></returns>
        public string TransAny(string Type, string Key, Languages Source, Languages Target, string SourceStr, bool IsBook, ref bool CanAddCache, ref bool CanSleep)
        {
            if (SourceStr == "")
            {
                return "";
            }
            if (Source.Equals(Target))
            {
                return SourceStr;
            }

            string GetCacheStr = CloudDBCache.FindCache(Key);

            if (GetCacheStr.Trim().Length > 0)
            {
                Translator.SendTranslateMsg("Cache From Database", SourceStr, GetCacheStr);
                CanSleep = false;
                return GetCacheStr;
            }

            EngineSelect? CurrentEngine = null;

            while (CurrentEngine == null)
            {
                lock (SwitchLocker)
                {
                    for (int i = 0; i < TransCore.EngineSelects.Count; i++)
                    {
                        if (TransCore.EngineSelects[i].CallCountDown > 0)
                        {
                            TransCore.EngineSelects[i].CallCountDown--;

                            CurrentEngine = TransCore.EngineSelects[i];

                            SortByCallCountDescending();
                        }
                    }
                }

                if (CurrentEngine != null)
                {
                    string GetTrans = "";
                    if (!IsBook)
                    {
                        GetTrans = CurrentEngine.Call(Type, Source, Target, SourceStr, true, EngineConfig.ContextLimit, string.Empty, ref CanAddCache);
                    }
                    else
                    {
                        GetTrans = CurrentEngine.Call(Type, Source, Target, SourceStr, false, 1, AIParam, ref CanAddCache);
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

            public object Engine = new object();
            public int CallCountDown = 0;
            public int MaxCallCount = 0;

            public int SleepBySec = 0;

            public EngineSelect(object Engine, int MaxCallCount)
            {
                this.Engine = Engine;
                this.MaxCallCount = MaxCallCount;
                this.CallCountDown = this.MaxCallCount;

                this.SleepBySec = 1;
            }

            public EngineSelect(object Engine, int MaxCallCount, int SleepBySec)
            {
                this.Engine = Engine;
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

            public string Call(string Type, Languages From, Languages To, string SourceStr, bool UseAIMemory, int AIMemoryCountLimit, string Param, ref bool CanAddCache)
            {
                TranslationPreprocessor NTranslationPreprocessor = new TranslationPreprocessor();

                string GetSource = SourceStr.Trim();
                string TransText = string.Empty;
                PlatformType CurrentPlatform = PlatformType.Null;

                if (GetSource.Length > 0)
                {
                    if (this.Engine is GoogleTransApi || this.Engine is DeepLApi)
                    {
                        bool CanTrans = false;

                        if (EngineConfig.DivCacheEngineEnable)
                        {
                            string GetDefSource = GetSource;
                            GetSource = NTranslationPreprocessor.GeneratePlaceholderText(From, To, GetSource, Type, out CanTrans);

                            if (DelegateHelper.SetOutputCall != null)
                            {
                                DelegateHelper.SetOutputCall(GetSource);
                            }

                            if (DelegateHelper.SetKeyWordsCall != null)
                            {
                                DelegateHelper.SetKeyWordsCall(NTranslationPreprocessor.ReplaceTags);
                            }

                            Translator.SendTranslateMsg("Local Engine(SSELex)", GetDefSource, GetSource);
                        }
                        else
                        { 
                           CanTrans = true;
                        }

                        if (CanTrans)
                        {
                            if (this.Engine is GoogleTransApi)
                            {
                                if (EngineConfig.GoogleYunApiEnable)
                                {
                                    var GetData = ConvertHelper.ObjToStr(((GoogleTransApi)this.Engine).Translate(GetSource, From, To));
                                    TransText = GetData;
                                    Translator.SendTranslateMsg("Cloud Translation(GoogleApi)", GetSource, TransText);
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
                            if (this.Engine is DeepLApi)
                            {
                                if (EngineConfig.DeepLApiEnable)
                                {
                                    var GetData = ((DeepLApi)this.Engine).QuickTrans(GetSource, From, To).Trim();

                                    if (GetData.Trim().Length > 0 && UseAIMemory)
                                    {
                                        AIMemory.AddTranslation(From, GetSource, GetData);
                                    }
                                    TransText = GetData;
                                    Translator.SendTranslateMsg("Cloud Translation(DeepL)", GetSource, TransText);
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
                            TransText = NTranslationPreprocessor.RestoreFromPlaceholder(GetSource, To);

                            this.CallCountDown++;
                        }
                    }
                    else
                    if (this.Engine is CohereApi || this.Engine is ChatGptApi || this.Engine is GeminiApi || this.Engine is DeepSeekApi || this.Engine is BaichuanApi || this.Engine is LMStudio)
                    {
                        bool CanTrans = false;

                        List<string> CustomWords = new List<string>();

                        if (EngineConfig.DivCacheEngineEnable)
                        {
                            CustomWords = NTranslationPreprocessor.GeneratePlaceholderTextByAI(From, To, GetSource, Type, out CanTrans);

                            string GenParam = "";
                            for (int i = 0; i < CustomWords.Count; i++)
                            {
                                GenParam += CustomWords[i] + "\n";
                            }

                            if (DelegateHelper.SetOutputCall != null)
                            {
                                DelegateHelper.SetOutputCall(GenParam);
                            }

                            if (DelegateHelper.SetKeyWordsCall != null)
                            {
                                DelegateHelper.SetKeyWordsCall(NTranslationPreprocessor.ReplaceTags);
                            }

                            Translator.SendTranslateMsg("Local Engine(SSELex)", GetSource, GenParam);
                        }
                        else
                        {
                            CanTrans = true;
                        }

                        if (CanTrans)
                        {
                            if (this.Engine is LMStudio)
                            {
                                if (EngineConfig.LMLocalAIEngineEnable)
                                {
                                    var GetData = ((LMStudio)this.Engine).QuickTrans(CustomWords, GetSource, From, To, UseAIMemory, AIMemoryCountLimit, Param).Trim();

                                    if (GetData.Trim().Length > 0 && UseAIMemory)
                                    {
                                        AIMemory.AddTranslation(From, GetSource, GetData);
                                    }
                                    TransText = GetData;
                                    Translator.SendTranslateMsg("LocalAI(LM)", GetSource, TransText);
                                    CurrentPlatform = PlatformType.LMLocalAI;

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
                            else
                            if (this.Engine is CohereApi)
                            {
                                if (EngineConfig.CohereApiEnable)
                                {
                                    var GetData = ((CohereApi)this.Engine).QuickTrans(CustomWords, GetSource, From, To, UseAIMemory, AIMemoryCountLimit, Param).Trim();

                                    if (GetData.Trim().Length > 0 && UseAIMemory)
                                    {
                                        AIMemory.AddTranslation(From, GetSource, GetData);
                                    }
                                    TransText = GetData;
                                    Translator.SendTranslateMsg("AI(CohereApi)", GetSource, TransText);
                                    CurrentPlatform = PlatformType.Cohere;

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
                            else
                            if (this.Engine is ChatGptApi)
                            {
                                if (EngineConfig.ChatGptApiEnable)
                                {
                                    var GetData = ((ChatGptApi)this.Engine).QuickTrans(CustomWords, GetSource, From, To, UseAIMemory, AIMemoryCountLimit, Param).Trim();

                                    if (GetData.Trim().Length > 0 && UseAIMemory)
                                    {
                                        AIMemory.AddTranslation(From, GetSource, GetData);
                                    }
                                    TransText = GetData;
                                    Translator.SendTranslateMsg("AI(ChatGptApi)", GetSource, TransText);
                                    CurrentPlatform = PlatformType.ChatGpt;

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
                            else
                            if (this.Engine is GeminiApi)
                            {
                                if (EngineConfig.GeminiApiEnable)
                                {
                                    var GetData = ((GeminiApi)this.Engine).QuickTrans(CustomWords, GetSource, From, To, UseAIMemory, AIMemoryCountLimit, Param).Trim();

                                    if (GetData.Trim().Length > 0 && UseAIMemory)
                                    {
                                        AIMemory.AddTranslation(From, GetSource, GetData);
                                    }
                                    TransText = GetData;
                                    Translator.SendTranslateMsg("AI(GeminiApi)", GetSource, TransText);
                                    CurrentPlatform = PlatformType.Gemini;

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
                            else
                            if (this.Engine is DeepSeekApi)
                            {
                                if (EngineConfig.DeepSeekApiEnable)
                                {
                                    var GetData = ((DeepSeekApi)this.Engine).QuickTrans(CustomWords, GetSource, From, To, UseAIMemory, AIMemoryCountLimit, Param).Trim();

                                    if (GetData.Trim().Length > 0 && UseAIMemory)
                                    {
                                        AIMemory.AddTranslation(From, GetSource, GetData);
                                    }
                                    TransText = GetData;
                                    Translator.SendTranslateMsg("AI(DeepSeek)", GetSource, TransText);
                                    CurrentPlatform = PlatformType.DeepSeek;

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
                            else
                            if (this.Engine is BaichuanApi)
                            {
                                if (EngineConfig.BaichuanApiEnable)
                                {
                                    var GetData = ((BaichuanApi)this.Engine).QuickTrans(CustomWords, GetSource, From, To, UseAIMemory, AIMemoryCountLimit, Param).Trim();

                                    if (GetData.Trim().Length > 0 && UseAIMemory)
                                    {
                                        AIMemory.AddTranslation(From, GetSource, GetData);
                                    }
                                    TransText = GetData;
                                    Translator.SendTranslateMsg("AI(Baichuan)", GetSource, TransText);
                                    CurrentPlatform = PlatformType.Baichuan;

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
                            TransText = GetSource;

                            for (int i = 0; i < NTranslationPreprocessor.ReplaceTags.Count; i++)
                            {
                                TransText = TransText.Replace(NTranslationPreprocessor.ReplaceTags[i].Key, NTranslationPreprocessor.ReplaceTags[i].Value);
                            }

                            this.CallCountDown++;
                        }
                    }

                    TransText = TransText.Trim();

                    if (TransText.Length > 0)
                    {
                        if (DelegateHelper.SetUsageCall != null)
                        {
                            DelegateHelper.SetUsageCall(CurrentPlatform, GetSource.Length);
                        }
                    }

                    return TransText;
                }

                return string.Empty;
            }
        }
    }
}
