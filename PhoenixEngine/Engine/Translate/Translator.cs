using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PhoenixEngine.ADO;
using PhoenixEngine.Common;
using PhoenixEngine.Engine;
using PhoenixEngine.Engine.Core;
using PhoenixEngine.Game;
using PhoenixEngine.Language;
using PhoenixEngine.Memory;
using PhoenixEngine.Unit;
using static PhoenixEngine.Engine.P_BucketContainer;

namespace PhoenixEngine.Translate
{
    public enum PlatformType
    {
        Null = 0, ChatGpt = 1, DeepSeek = 2, Gemini = 3, DeepL = 5, GoogleApi = 7, LMLocalAI = 10, PhoenixEngine = 11, CustomPlatform = 12, HumanTranslation = 13
    }

    public class Translator
    {
        public string ID = "";

        public Languages From = Languages.Null;
        public Languages To = Languages.Null;

        public string AIParam = null;
        public EngineCore Core = new EngineCore();
        private TranslatorCore BatchCore = null;
        public TranslationPreprocessor Preprocessor = new TranslationPreprocessor();

        /// <summary>Gets or sets the provider used to translate domain requests.</summary>
        public ITranslationProvider TranslationProvider { get; set; }

        /// <summary>Gets or sets the provider-neutral translation store.</summary>
        public ITranslationStore TranslationStore { get; set; }

        /// <summary>Gets or sets the execution and cancellation policy.</summary>
        public ITranslationScheduler TranslationScheduler { get; set; }

        public readonly object TransDataLocker = new object();

        private P_Dict<string, P_String> DataLink = new P_Dict<string,P_String>();
        public int MaxTry = 10;

        public P_Dict<string, P_String> GetLink()
        {
            return this.DataLink;
        }

        public Translator(string ID,Languages SetFrom, Languages SetTo, bool ClearCache)
        {
            this.ID = ID;

            if (BatchCore == null)
            {
                BatchCore = new TranslatorCore(this, ClearCache);
            }

            TranslationProvider = new LegacyTranslationProvider(this);
            TranslationStore = new NullTranslationStore();
            TranslationScheduler = new SequentialTranslationScheduler();

            if (Phoenix.AIMemory.OptimizeToken(this))
            {
                if (SetFrom != Languages.Null)
                {
                    this.From = SetFrom;
                }
                if (SetTo != Languages.Null)
                {
                    this.To = SetTo;
                }
            }
        }

        public UnitGroup ToUnitGroup(BaseUnit Unit)
        {
            return new UnitGroup(this,Unit);
        }

        public void Init(List<BaseUnit> BaseUnits,int Addition,CheckLinks CheckLinksEvent)
        {
            if (this.BatchCore != null)
            {
                this.BatchCore.Close();

                if (this.BatchCore.Container != null)
                {
                    this.BatchCore.Container.Clear();
                }

                this.BatchCore.ProcStage = 0;
            }

            if (BatchCore != null)
            {
                if (!BatchCore.Init(BaseUnits,Addition, CheckLinksEvent))
                {
                    throw (new Exception("Translator->Attempting to initialize at the wrong stage."));
                }
            }
            else
            {
                throw (new Exception("Translator->Error: Null pointer."));
            }
        }

        public TranslatorCore GetBatchCore()
        {
            return this.BatchCore;
        }

        public void ClearCache()
        {
            DataLink.Clear();
        }

        public void ClearAICache()
        {
            Phoenix.AIMemory.Clear();
        }
        public List<BaseUnit> ChunkTranslationUnit(BaseUnit Unit, ref List<UnitChunk> Chunks)
        {
            Chunks = new P_Skyrim().ChunkBook(Unit);

            List<BaseUnit> Units = new List<BaseUnit>();
            foreach (UnitChunk Chunk in Chunks)
            {
                if (!Chunk.IsCode)
                {
                    Units.Add(
                    new BaseUnit(
                       Unit.FileUniqueKey,
                       Chunk.Key,
                       Unit.Type,
                       Chunk.Data,
                       string.Empty,
                       Unit.Emotion,
                       Unit.Score
                   ));
                }
            }

            return Units;
        }
        public UnitGroup Translate(BaseUnit Unit, CancellationToken CancelToken, bool CanSleep = true)
        {
            if (!BatchCore.IsWorking)
            {
                this.Core.ResetEngineHealth();
            }
            P_Game SetGameType = new P_Game();
            bool IsBook = false;

            if (P_Skyrim.IsBookContent(Unit, ref SetGameType))
            {
                IsBook = true;
            }

            UnitGroup SetGroup = ToUnitGroup(Unit);

            return Translate(new TransParam(SetGroup, IsBook, CanSleep), CancelToken);
        }
        /// <summary>
        /// Translates a parameter set through the configured provider, store, and scheduler.
        /// </summary>
        /// <param name="Params">The units and preprocessing options to translate.</param>
        /// <param name="CancelToken">The token that cancels the translation.</param>
        /// <returns>The translated unit group.</returns>
        public UnitGroup Translate(TransParam Params, CancellationToken CancelToken)
        {
            return TranslateAsync(Params, CancelToken).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Translates a parameter set asynchronously through the configured boundaries.
        /// </summary>
        /// <param name="parameters">The units and preprocessing options to translate.</param>
        /// <param name="cancellationToken">The token that cancels the translation.</param>
        /// <returns>A task containing the translated unit group.</returns>
        public Task<UnitGroup> TranslateAsync(TransParam parameters, CancellationToken cancellationToken)
        {
            var request = new TranslationRequest(parameters, From, To, AIParam);
            var pipeline = new TranslationPipeline(
                TranslationProvider,
                TranslationStore,
                TranslationScheduler);
            return pipeline.TranslateAsync(request, cancellationToken);
        }

        internal UnitGroup TranslateCore(TransParam Params, CancellationToken CancelToken)
        {
            if (this.From == this.To)
            {
                return Params.Data;
            }

            UnitGroup SetUnitGroup = Params.Data;

            if (Params.Preprocessor == null)
            {
                Params.Preprocessor = this.Preprocessor;
            }

            if (Params.IsBook)
            {
                List<UnitChunk> Chunks = new List<UnitChunk>();
                List<BaseUnit> Units = new List<BaseUnit>();

                var GetFrist = SetUnitGroup.GetFrist();

                Units.AddRange(ChunkTranslationUnit(GetFrist, ref Chunks));

                string MergeLine = "";

                if (Chunks.Count > 0)
                {
                    //It is necessary to prevent the preceding lines of code from being lost.
                    foreach (var GetChunk in Chunks)
                    {
                        if (GetChunk.IsCode)
                        {
                            MergeLine += GetChunk.Data;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                List<UnitGroup> UnitGroups = new List<UnitGroup>();
                int SetLength = 0;

                UnitGroup NewUnitGroup = new UnitGroup();

                for (int i = 0; i < Units.Count; i++)
                {
                    int UnitTokenLen = P_Bucket_Core.CalcTextTokenEstimate(Units[i].Original);

                    if (SetLength + UnitTokenLen < Phoenix.Config.BucketLengthLimit)
                    {
                        SetLength += UnitTokenLen;
                        NewUnitGroup.Units.Add(Units[i]);
                    }
                    else
                    {
                        if (NewUnitGroup.Units.Count > 0)
                        UnitGroups.Add(NewUnitGroup);
                        NewUnitGroup = new UnitGroup();
                        SetLength = UnitTokenLen;
                        NewUnitGroup.Units.Add(Units[i]);
                    }
                }
                if (NewUnitGroup.Units.Count > 0)
                {
                    UnitGroups.Add(NewUnitGroup);
                    NewUnitGroup = null;
                }

                List<BaseUnit> BaseUnits = new List<BaseUnit>();

                foreach (var GetGroup in UnitGroups)
                {
                    var ResultGroup = Core.CallOnce(CancelToken, this,
                       Params.Preprocessor, GetGroup, From, To, AIParam, Params.CanSleep, false, false);

                    BaseUnits.AddRange(ResultGroup.Units);
                }

                if (Chunks.Count > 0)
                {
                    for (int i = 0; i < Chunks.Count; i++)
                    {
                        foreach (var GetUnit in BaseUnits)
                        {
                            if (Chunks[i].Key.Equals(GetUnit.Key))
                            {
                                MergeLine += GetUnit.Translated + "\n";

                                for (int j = i + 1; j < Chunks.Count; j++)
                                {
                                    if (Chunks[j].IsCode)
                                    {
                                        MergeLine += Chunks[j].Data;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }

                                break;
                            }
                        }
                    }
                }
                else
                {
                    foreach (var GetUnit in BaseUnits)
                    {
                        MergeLine += GetUnit.Translated;
                    }
                }
                UnitGroup ReturnItem = new UnitGroup();
                BaseUnit SingleUnit = new BaseUnit(GetFrist.FileUniqueKey, GetFrist.Key, GetFrist.Type, GetFrist.Original, MergeLine,GetFrist.Emotion, 100);

                ReturnItem.Units.Add(SingleUnit);

                CloudDBCache.AddCache(SingleUnit.FileUniqueKey, SingleUnit.Key, (int)To, SingleUnit.Original, SingleUnit.Translated);

                return ReturnItem;
            }
            else
            {
                return Core.CallOnce(CancelToken, this,
                    Params.Preprocessor, SetUnitGroup, From, To, AIParam, Params.CanSleep, true, true);
            }
        }

        public void UnifiedSymbols()
        {
            try
            {
                DataLink.CheckLinks(new Action<string,P_String,bool>((Key,Value,Unique) =>
                {
                    var SetValue = Value.String.Trim();
                    try
                    {
                        if (SetValue.Length > 0)
                        {
                            TranslationPreprocessor.UnifiedSymbols(this, Key, SetValue);
                        }
                    }
                    catch { }
                }));
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Error in WriteAllMemoryData: {ex.Message}");
            }
        }

        public void AddAIMemory(string Original, string Translated)
        {
            Phoenix.AIMemory.AddTranslation(this.From, this.To, Original, Translated);
        }
        public int CalcTranslatedCount(int Addition)
        {
            if (LastLoadFileName.Length == 0) return 0;
            const string SqlOrder = @"SELECT COUNT(*) AS TotalCount
FROM (
    SELECT Key
    FROM LocalTranslation
    WHERE FileUniqueKey = @fileUniqueKey AND [To] = @to
    
    UNION  
    SELECT Key
    FROM CloudTranslation
    WHERE FileUniqueKey = @fileUniqueKey AND [To] = @to
) AS Combined;";

            int GetCount = P_Convert.ObjToInt(Phoenix.LocalDB.ExecuteScalar(
                SqlOrder,
                SqliteSql.Parameter("@fileUniqueKey", FileUniqueKey),
                SqliteSql.Parameter("@to", (int)this.To)));

            return GetCount + Addition;
        }

        private int FileUniqueKey = 0;
        public string LastLoadFileName = "";
        public void LoadFile(string FilePath, bool CanSkipFuzzyMatching = false)
        {
            UniqueKeyItem NewKey = new UniqueKeyItem();
            FileUniqueKey = UniqueKeyHelper.AddItemByReturn(ref NewKey, FilePath, CanSkipFuzzyMatching);
            LastLoadFileName = NewKey.FileName;
        }

        public void Close()
        {
            GetBatchCore()?.Close();
            GetLink()?.Clear();
            FileUniqueKey = 0;
        }
        public int GetFileUniqueKey()
        {
            return FileUniqueKey;
        }
    }


    public class TransParam
    {
        public bool CanSleep; //A thread can be suspended for a certain period of time.
        public bool IsBook;//Book type requires special handling.
        public TranslationPreprocessor Preprocessor = null;//Allows passing custom preprocessors.
        public P_Game GameType = P_Game.Null;//Specify the game type; currently, this feature is only for identification.
        public UnitGroup Data;//Data that needs to be translated.

        public TransParam(UnitGroup Data, bool IsBook, bool CanSleep, TranslationPreprocessor SetPreprocessor = null, P_Game GameType = P_Game.Null)
        {
            this.CanSleep = CanSleep;
            this.IsBook = IsBook;
            this.Data = Data;
            this.Preprocessor = SetPreprocessor;
            this.GameType = GameType;
        }
    }
}
