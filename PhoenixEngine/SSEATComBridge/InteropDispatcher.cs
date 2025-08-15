using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using PhoenixEngine.DataBaseManagement;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;
using PhoenixEngine.TranslateManagement;
using static PhoenixEngine.SSEATComBridge.BridgeHelper;

namespace PhoenixEngine.SSEATComBridge
{

    /// <summary>
    /// For SSEAT V2
    /// </summary>
    [ComVisible(true)]
    public class InteropDispatcher
    {
        #region Com
        //init();
        //config_language(from_language_code("en"),from_language_code("de"));
        //clear();

        //enqueue_translation_unit...
        //start_translation(2);

        /// <summary>
        /// Initializes the engine.
        ///
        /// This method must be called first before using any other functionality.
        /// It sets up necessary engine components and marks the engine as initialized.
        /// </summary>
        public static void init()
        {
            Engine.Init();
            IsInit = true;
        }

        private class VersionItem
        { 
            public string Version { get; set; }

            public VersionItem(string Version)
            {
                this.Version = Version;
            }
        }

        public static string get_version()
        {
            return JsonSerializer.Serialize(new VersionItem(DeFine.Version));
        }

        public static void set_modname(string modname)
        {
            Engine.ChangeModName(modname);
        }

        /// <summary>
        /// Configures the source and target languages for translation.
        ///
        /// Parameters:
        /// Src - Integer code representing the source language (can be Auto for automatic detection).
        /// Dst - Integer code representing the target language (must NOT be Auto).
        ///
        /// This method initializes or reinitializes the BulkTranslator with the specified languages.
        /// If BulkTranslator already exists, it will be closed and recreated.
        ///
        /// Note: The target language must be explicitly set and cannot be Auto.
        /// </summary>
        public static bool config_language(string Json)
        {
            ConfigLanguageJson? GetItem = null;

            try
            {
                GetItem = JsonSerializer.Deserialize<ConfigLanguageJson>(Json);
            }
            catch { }

            if (GetItem != null)
            {
               return Engine.ConfigLanguage((Languages)GetItem.Src, (Languages)GetItem.Dst);
            }

            return false;
        }

        /// <summary>
        /// Clears the current translation queue and resets the translator state.
        ///
        /// This method reinitializes the BulkTranslator, effectively removing all
        /// translation units that were previously enqueued via enqueue_translation_unit.
        /// </summary>
        public static void clear()
        {
            Engine.End();
        }

        /// <summary>
        /// Gets the current number of active translation threads.
        ///
        /// Returns:
        /// The count of threads currently working on translation tasks,
        /// or -1 if the translator is not initialized.
        /// </summary>
        public static int get_current_thread_count()
        {
            return Engine.GetThreadCount();
        }

        /// <summary>
        /// Converts a language code (e.g., "en", "ja", "zh") into its corresponding integer enum value.
        ///
        /// Special Case:
        /// If the input is "auto" (case-insensitive), it returns the enum value for automatic language detection.
        /// Only the **source language** supports "auto". The **target language** must always be explicitly specified.
        ///
        /// Parameters:
        /// Lang - A string representing the language code.
        ///
        /// Returns:
        /// Integer value representing the corresponding language enum.
        public static int from_language_code(string Json)
        {
            FromLanguageCodeJson? GetItem = null;

            try
            {
                GetItem = JsonSerializer.Deserialize<FromLanguageCodeJson>(Json);
            }
            catch { }

            if (GetItem != null)
            {
                if (GetItem.Lang.ToLower() == "auto")
                {
                    return (int)Languages.Auto;
                }

                return (int)LanguageHelper.FromLanguageCode(GetItem.Lang);
            }

            return (int)Languages.Null;
        }

        /// <summary>
        /// Adds a new translation unit to the translation queue from a JSON string.
        /// 
        /// This method deserializes the JSON input into a TranslationUnitJson object
        /// and appends it to the list of units to translate.
        ///
        /// Prerequisite:
        /// The BulkTranslator must be initialized first using from_language_code().
        ///
        /// Returns:
        /// true  - if the item was successfully parsed and added to the queue
        /// false - if parsing failed or the input was invalid
        /// </summary>
        public static bool enqueue_translation_unit(string Json)
        {
            TranslationUnitJson? GetItem = null;

            try
            {
                GetItem = JsonSerializer.Deserialize<TranslationUnitJson>(Json);
            }
            catch { }

            if (GetItem != null)
            {
                Engine.AddTranslationUnit(new TranslationUnit(GetItem.ModName, GetItem.Key, GetItem.Type, GetItem.SourceText, GetItem.TransText));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Dequeues one translated result from the translated queue and returns it as a JSON string.
        ///
        /// ReturnState explanation:
        /// -1 => Language configuration is missing (BulkTranslator is null; you must call config_language first)
        ///  0 => There are still translated items left in the queue
        ///  1 => All translated items have been dequeued (this was the last one)
        /// </summary>
        public static string dequeue_translated()
        {
            TranslatedResultJson NTranslatedResult = new TranslatedResultJson();
            int ReturnState = -1;

            bool State = false;
            var GetItem = Engine.DequeueTranslated(ref State);

            if (GetItem != null)
            {
                NTranslatedResult.Item = new TranslationUnitJson();

                NTranslatedResult.Item.ModName = GetItem.ModName;
                NTranslatedResult.Item.Score = GetItem.Score;

                NTranslatedResult.Item.Key = GetItem.Key;
                NTranslatedResult.Item.Type = GetItem.Type;

                NTranslatedResult.Item.SourceText = GetItem.SourceText;
                NTranslatedResult.Item.TransText = GetItem.TransText;

                NTranslatedResult.Item.IsDuplicateSource = GetItem.IsDuplicateSource;
                NTranslatedResult.Item.Leader = GetItem.Leader;
                NTranslatedResult.Item.Translated = GetItem.Translated;
            }

            if (State)
            {
                ReturnState = 1;
                clear();
            }
            else
            {
                ReturnState = 0;
            }

            NTranslatedResult.State = ReturnState;

            return JsonSerializer.Serialize(NTranslatedResult);
        }

        /// <summary>
        /// Starts the translation process with a specified maximum number of threads.
        ///
        /// This method sets the thread limit manually (disabling auto adjustment),
        /// updates the engine configuration, and begins processing the translation queue.
        ///
        /// Parameters:
        /// ThreadLimit - Maximum number of concurrent translation threads.
        ///
        /// Returns:
        /// true  - if the translation process was successfully started
        /// false - if BulkTranslator was not initialized
        /// </summary>
        public static bool start_translation(string Json,bool ClearCache)
        {
            StartTranslationJson? GetItem = null;

            try
            {
                GetItem = JsonSerializer.Deserialize<StartTranslationJson>(Json);
            }
            catch { }

            if (GetItem != null)
            {
                EngineConfig.AutoSetThreadLimit = false;
                EngineConfig.MaxThreadCount = GetItem.ThreadLimit;

                Engine.Start(ClearCache);

                return true;
            }
            return false;
        }

        /// <summary>
        /// Temporarily stops the translation process.
        ///
        /// This method calls Close() on the BulkTranslator to cancel ongoing translation threads,
        /// but does not clear the translation queue or results.  
        /// You can resume translation later by calling start_translation() again.
        ///
        /// Returns:
        /// true  - if the translator was successfully paused
        /// false - if the translator was not initialized
        /// </summary>
        public static void end_translation()
        {
            Engine.End();
        }

        /// <summary>
        /// Retrieves the current engine configuration as a JSON string.
        ///
        /// This method ensures that the configuration is up to date by calling SyncCurrentConfig().
        /// It can only be used after the engine has been initialized (IsInit == true).
        ///
        /// Throws:
        /// Exception - if the engine has not been initialized.
        ///
        /// Returns:
        /// A JSON string representing the current engine configuration.
        /// </summary>
        public static string get_config()
        {
            if (!IsInit)
            {
                throw new Exception("Initialization required.");
            }

            SyncCurrentConfig();
            return JsonSerializer.Serialize(CurrentConfig);
        }

        /// <summary>
        /// Applies a JSON configuration to the translation engine.
        ///
        /// This method deserializes a JSON string into a ConfigJson object and updates the internal engine settings accordingly.
        /// It must be called only after initialization (IsInit == true).
        ///
        /// Parameters:
        /// Json - A JSON string that matches the structure of ConfigJson.
        ///
        /// Returns:
        /// true  - if the configuration was successfully parsed and applied
        /// false - if parsing failed (default configuration will be restored)
        ///
        /// Throws:
        /// Exception - if the engine has not been initialized
        /// </summary>
        public static bool set_config(string Json)
        {
            if (!IsInit)
            {
                throw new Exception("Initialization required.");
            }

            try
            {
                CurrentConfig = JsonSerializer.Deserialize<ConfigJson>(Json);
            }
            catch
            {
                CurrentConfig = null;
                return false;
            }

            if (CurrentConfig == null)
            {
                CurrentConfig = new ConfigJson();
                return false;
            }

            // RequestConfig
            EngineConfig.ProxyIP = CurrentConfig.ProxyIP;
            EngineConfig.GlobalRequestTimeOut = CurrentConfig.GlobalRequestTimeOut;

            EngineConfig.PreTranslateEnable = CurrentConfig.PreTranslateEnable;

            // Platform Enable State
            EngineConfig.ChatGptApiEnable = CurrentConfig.ChatGptApiEnable;
            EngineConfig.GeminiApiEnable = CurrentConfig.GeminiApiEnable;
            EngineConfig.CohereApiEnable = CurrentConfig.CohereApiEnable;
            EngineConfig.DeepSeekApiEnable = CurrentConfig.DeepSeekApiEnable;
            EngineConfig.BaichuanApiEnable = CurrentConfig.BaichuanApiEnable;
            EngineConfig.GoogleYunApiEnable = CurrentConfig.GoogleYunApiEnable;
            EngineConfig.LMLocalAIEnable = CurrentConfig.LMLocalAIEnable;
            EngineConfig.DeepLApiEnable = CurrentConfig.DeepLApiEnable;

            // API Key / Model
            EngineConfig.GoogleApiKey = CurrentConfig.GoogleApiKey;
            EngineConfig.ChatGptKey = CurrentConfig.ChatGptKey;
            EngineConfig.ChatGptModel = CurrentConfig.ChatGptModel;
            EngineConfig.GeminiKey = CurrentConfig.GeminiKey;
            EngineConfig.GeminiModel = CurrentConfig.GeminiModel;
            EngineConfig.DeepSeekKey = CurrentConfig.DeepSeekKey;
            EngineConfig.DeepSeekModel = CurrentConfig.DeepSeekModel;
            EngineConfig.BaichuanKey = CurrentConfig.BaichuanKey;
            EngineConfig.BaichuanModel = CurrentConfig.BaichuanModel;
            EngineConfig.CohereKey = CurrentConfig.CohereKey;
            EngineConfig.DeepLKey = CurrentConfig.DeepLKey;
            EngineConfig.IsFreeDeepL = CurrentConfig.IsFreeDeepL;

            // LM Studio
            EngineConfig.LMHost = CurrentConfig.LMHost;
            EngineConfig.LMPort = CurrentConfig.LMPort;
            EngineConfig.LMQueryParam = CurrentConfig.LMQueryParam;
            EngineConfig.LMModel = CurrentConfig.LMModel;

            // Engine Setting
            EngineConfig.ThrottleRatio = CurrentConfig.ThrottleRatio;
            EngineConfig.ThrottleDelayMs = CurrentConfig.ThrottleDelayMs;
            EngineConfig.MaxThreadCount = CurrentConfig.MaxThreadCount;
            EngineConfig.AutoSetThreadLimit = CurrentConfig.AutoSetThreadLimit;
            EngineConfig.ContextEnable = CurrentConfig.ContextEnable;
            EngineConfig.ContextLimit = CurrentConfig.ContextLimit;
            EngineConfig.UserCustomAIPrompt = CurrentConfig.UserCustomAIPrompt;

            EngineConfig.Save();

            return true;
        }

        /// <summary>
        /// Adds a keyword entry to the translation engine's advanced dictionary.
        ///
        /// The keyword data is provided as a JSON string which is deserialized into an
        /// AdvancedDictionaryJson object. If deserialization is successful, the data
        /// is converted to an AdvancedDictionaryItem and added to the advanced dictionary.
        ///
        /// Parameters:
        /// Json - A JSON string representing the keyword entry to add.
        /// </summary>
        public static void add_keyword(string Json)
        {
            AdvancedDictionaryJson? Item = null;
            try
            {
                Item = JsonSerializer.Deserialize<AdvancedDictionaryJson>(Json);
            }
            catch { }

            if (Item != null)
            {
                AdvancedDictionaryItem NAdvancedDictionaryItem = new AdvancedDictionaryItem();

                NAdvancedDictionaryItem.TargetModName = Item.TargetModName;
                NAdvancedDictionaryItem.Type = Item.Type;
                NAdvancedDictionaryItem.From = Item.From;
                NAdvancedDictionaryItem.To = Item.To;
                NAdvancedDictionaryItem.Source = Item.Source;
                NAdvancedDictionaryItem.Result = Item.Result;
                NAdvancedDictionaryItem.ExactMatch = Item.ExactMatch;
                NAdvancedDictionaryItem.IgnoreCase = Item.IgnoreCase;
                NAdvancedDictionaryItem.Regex = Item.Regex;

                AdvancedDictionary.AddItem(NAdvancedDictionaryItem);
            }
            
        }
       
        /// <summary>
        /// Removes a keyword from the advanced dictionary by its row ID.
        ///
        /// The method deserializes the input JSON to obtain the row ID of the keyword to remove.
        /// It then deletes the keyword entry with the specified row ID from the dictionary.
        ///
        /// Parameters:
        /// Json - A JSON string containing the "rowid" of the keyword to delete.
        ///
        /// Returns:
        /// true if the keyword was successfully deleted; otherwise, false.
        /// </summary>
        public static bool remove_keyword(string Json) 
        {
            RemoveKeyWordJson? Item = null;
            try
            {
                Item = JsonSerializer.Deserialize<RemoveKeyWordJson>(Json);
            }
            catch { }
            if (Item != null)
            {
                return AdvancedDictionary.DeleteByRowid(Item.rowid);
            }
            return false;
        }

        /// <summary>
        /// Queries a paginated list of keywords from the advanced dictionary.
        /// </summary>
        public static string query_keywords(string Json)
        {
            QueryKeyWordJson? Item = null;
            try
            {
                Item = JsonSerializer.Deserialize<QueryKeyWordJson>(Json);
            }
            catch
            { }
            if (Item != null)
            {
               return JsonSerializer.Serialize(AdvancedDictionary.QueryByPage(Item.From, Item.To, Item.PageNo));
            }

            return JsonSerializer.Serialize(new PageItem<List<AdvancedDictionaryItem>>(new List<AdvancedDictionaryItem>(),-1,-1));
        }

        /// <summary>
        /// Starts a background thread to translate a book's text content.
        /// </summary>
        /// <param name="Json">
        /// A JSON string containing the following fields:
        /// - ModName: The name of the mod (used to identify the source of the book)
        /// - Key: A unique key for the book entry
        /// - Source: The full text content of the book that needs to be translated
        /// </param>
        /// <returns>
        /// Returns the managed thread ID of the created translation thread.
        /// If JSON deserialization fails or an exception occurs, returns -1.
        /// </returns>
        public static int translate_book_text(string Json)
        {
            int ThreadID = -1;
            BookTransItemJson? Item = null;
            try
            {
                Item = JsonSerializer.Deserialize<BookTransItemJson>(Json);
            }
            catch
            { }

            if (Item != null)
            {
                TextSegmentTranslator NTextSegmentTranslator = new TextSegmentTranslator();
                CancellationToken CreatEndCall = new CancellationToken();
                Thread CreatTrd = new Thread(() => 
                { 
                    NTextSegmentTranslator.TransBook(Item.Key,Item.Source, CreatEndCall);
                });

                BookTransingItem NBookTransingItem = new BookTransingItem();
                NBookTransingItem.Translator = NTextSegmentTranslator;
                NBookTransingItem.CurrentThread = CreatTrd;
                NBookTransingItem.EndCall = CreatEndCall;

                BridgeHelper.BookTransTrds.Add(NBookTransingItem);

                CreatTrd.Start();
                ThreadID = CreatTrd.ManagedThreadId;
            }

            return ThreadID;
        }

        /// <summary>
        /// Attempts to cancel a running book translation task based on the given thread ID.
        /// </summary>
        /// <param name="Json">
        /// A JSON string containing the following field:
        /// - ThreadID: The managed thread ID of the translation task to be cancelled.
        /// </param>
        /// <returns>
        /// Returns true if a matching translation thread was found and cancellation was triggered; otherwise, returns false.
        /// </returns>
        public static bool end_book_translation(string Json)
        {
            EndBookTranslationJson? Item = null;
            try
            {
                Item = JsonSerializer.Deserialize<EndBookTranslationJson>(Json);
            }
            catch
            { }

            if (Item != null)
            {
                for (int i = 0; i < BookTransTrds.Count; i++)
                {
                    if (BookTransTrds[i] != null && BookTransTrds[i].CurrentThread != null)
                    {
                        if (BookTransTrds[i].CurrentThread.ManagedThreadId.Equals(Item.ThreadID))
                        {
                            BookTransTrds[i].EndCall.ThrowIfCancellationRequested();

                            BookTransTrds.RemoveAt(i);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Dequeues the first completed book translation result from the active translation tasks.
        /// </summary>
        /// <returns>
        /// A JSON string representing the first completed book translation with the following fields:
        /// - ModName: The mod name
        /// - Key: The unique key identifying the book entry
        /// - Text: The translated text content
        /// - IsEnd: Whether the translation task is finished for this item
        /// - State: The state of the queue after dequeue operation:
        ///     0 = There are still more translation results waiting to be dequeued
        ///     1 = All translation results have been dequeued (queue is empty)
        ///    -1 = No completed translation results available currently
        /// </returns>
        public static string dequeue_book_translated()
        {
            BookTransListJson GetFristBook = new BookTransListJson();
            List<int> WaitDeleteIDs = new List<int>();

            for (int i = 0; i < BridgeHelper.BookTransTrds.Count; i++)
            {
                if (BridgeHelper.BookTransTrds[i].Translator.IsEnd)
                {
                    GetFristBook.ModName = BridgeHelper.BookTransTrds[i].Translator.ModName;
                    GetFristBook.Key = BridgeHelper.BookTransTrds[i].Translator.Key;
                    GetFristBook.Text = BridgeHelper.BookTransTrds[i].Translator.CurrentText;
                    GetFristBook.IsEnd = BridgeHelper.BookTransTrds[i].Translator.IsEnd;
                    BridgeHelper.BookTransTrds.RemoveAt(i);
                    break;
                }
            }

            if (GetFristBook.Key.Trim().Length == 0 && BridgeHelper.BookTransTrds.Count == 0)
            {
                GetFristBook.State = -1;
            }
            else
            if (BridgeHelper.BookTransTrds.Count > 0)
            {
                GetFristBook.State = 0;
            }
            else
            {
                GetFristBook.State = 1;
            }

            return JsonSerializer.Serialize(GetFristBook);
        }

        #endregion
    }
}
