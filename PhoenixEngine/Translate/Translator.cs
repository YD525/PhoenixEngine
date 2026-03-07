using System;
using System.Collections.Generic;
using System.Linq;
using PhoenixEngine.ADO;
using PhoenixEngine.Engine;
using PhoenixEngine.EngineManagement.Engine;
using PhoenixEngine.GameManagement;
using PhoenixEngine.Language;
using PhoenixEngine.Sequence;
using PhoenixEngine.Unit;
using static PhoenixEngine.Engine.ChunkHelper;

namespace PhoenixEngine.Translate
{
    public enum PlatformType
    {
        Null = 0, ChatGpt = 1, DeepSeek = 2, Gemini = 3, DeepL = 5, GoogleApi = 7, Baichuan = 8, Cohere = 9, LMLocalAI = 10, PhoenixEngine = 11, CustomPlatform = 12
    }

    public class Translator
    {
        public Languages From = Languages.Null;
        public Languages To = Languages.Null;

        public string AIParam = null;
        public EngineCore Core = new EngineCore();
        private TranslatorCore BatchCore = null;
    
        public readonly object TransDataLocker = new object();

        private Dictionary<string, string> DataLink = new Dictionary<string, string>();
        public int MaxTry = 10;

        public Dictionary<string, string> GetLink()
        {
            return this.DataLink;
        }

        public Translator(Languages SetFrom,Languages SetTo,bool ClearCache)
        {
            if (BatchCore == null)
            {
                BatchCore = new TranslatorCore(this, ClearCache);
            }

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
            return new UnitGroup(Unit);
        }

        public void Init(List<BaseUnit>BaseUnits,AggregationMode Mode)
        {
            if (BatchCore != null)
            {
                if (!BatchCore.Init(BaseUnits, Mode))
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

        public void ReInit()
        {
            if (this.BatchCore != null)
            {
                this.BatchCore.Cancel();

                if (this.BatchCore.Content != null)
                {
                    this.BatchCore.Content.Clear();
                }
              
                this.BatchCore.ProcStage = 0;
            }
        }

        public List<BaseUnit> ChunkTranslationUnit(BaseUnit Unit,ref List<UnitChunk> Chunks)
        {
            Chunks = new SkyrimBookHelper().ChunkBook(Unit);

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
                       Unit.Score
                   ));
                }
            }

            return Units;
        }
        public UnitGroup Translate(BaseUnit Unit,bool CanSleep = true)
        {
            Game SetGameType = new Game();
            bool IsBook = false;

            if (SkyrimBookHelper.IsSkyrimBook(Unit, ref SetGameType))
            {
                IsBook = true;
            }

            UnitGroup SetGroup = ToUnitGroup(Unit);

            return Translate(new TransParam(SetGroup,IsBook,CanSleep));
        }
        public UnitGroup Translate(TransParam Params)
        {
            if (this.From == this.To)
            {
                return Params.Data;
            }

            UnitGroup SetUnitGroup = Params.Data;

            if (Params.Preprocessor == null)
            {
                Params.Preprocessor = TranslationPreprocessor.Instance;
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
                    if (SetLength < ProcContent.TextLengthLimit)
                    {
                        SetLength += Units[i].Original.Length;
                        NewUnitGroup.Units.Add(Units[i]);
                    }
                    else
                    {
                        UnitGroups.Add(NewUnitGroup);
                        NewUnitGroup = new UnitGroup();
                        SetLength = Units[i].Original.Length;
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
                    var ResultGroup = Core.CallOnce(this,
                       Params.Preprocessor, GetGroup, From, To, AIParam, Params.CanSleep, false,false);

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
                BaseUnit SingleUnit = new BaseUnit(GetFrist.FileUniqueKey, GetFrist.Key, GetFrist.Type, GetFrist.Original, MergeLine, 100);

                ReturnItem.Units.Add(SingleUnit);

                CloudDBCache.AddCache(SingleUnit.FileUniqueKey,SingleUnit.Key,(int)To,SingleUnit.Original,SingleUnit.Translated);

                return ReturnItem;
            }
            else
            {
                return Core.CallOnce(this,
                    Params.Preprocessor, SetUnitGroup, From, To, AIParam, Params.CanSleep, true,true);
            }
        }

        public void UnifiedSymbols()
        {
            try
            {
                for (int i = 0; i < DataLink.Count; i++)
                {
                    try
                    {
                        var GetHashKey = DataLink.ElementAt(i).Key;
                        if (DataLink[GetHashKey].Trim().Length > 0)
                        {
                            TranslationPreprocessor.UnifiedSymbols(this,GetHashKey, DataLink[GetHashKey].Trim());
                        }
                    }
                    catch (System.Exception ex)
                    {
                        System.Console.WriteLine($"Error in WriteAllMemoryData loop at index {i}: {ex.Message}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Error in WriteAllMemoryData: {ex.Message}");
            }
        }
    }


    public class TransParam
    {
        public bool CanSleep; //A thread can be suspended for a certain period of time.
        public bool IsBook;//Book type requires special handling.
        public TranslationPreprocessor Preprocessor = null;//Allows passing custom preprocessors.
        public Game GameType = Game.Null;//Specify the game type; currently, this feature is only for identification.
        public UnitGroup Data;//Data that needs to be translated.

        public TransParam(UnitGroup Data, bool IsBook, bool CanSleep, TranslationPreprocessor SetPreprocessor = null, Game GameType = Game.Null)
        {
            this.CanSleep = CanSleep;
            this.IsBook = IsBook;
            this.Data = Data;
            this.Preprocessor = SetPreprocessor;
            this.GameType = GameType;
        }
    }
}