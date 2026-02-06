using System.Collections.Generic;
using System.Linq;
using System.Web.UI.WebControls;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.EngineManagement.Engine;
using PhoenixEngine.EngineManagement.Unit;
using PhoenixEngine.GameManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManagement;
using static PhoenixEngine.TranslateManagement.ChunkHelper;

namespace PhoenixEngine.TranslateManage
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

        private TranslationPreprocessor PreprocessorInstance = new TranslationPreprocessor();
        public EngineCore Core = new EngineCore();
        public TranslatorCore BatchCore = null;
    

        public readonly object TransDataLocker = new object();

        public Dictionary<string, string> TranslatedLink = new Dictionary<string, string>();
        public int MaxTry = 10;
        public Translator(Languages SetFrom,Languages SetTo)
        {
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

        public void ClearCache()
        {
            TranslatedLink.Clear();
        }

        public void ClearAICache()
        {
            Phoenix.AIMemory.Clear();
        }

        public List<BaseUnit> ChunkTranslationUnit(Game GameType, BaseUnit Unit,ref List<UnitChunk> Chunks)
        {
            if (GameType == Game.Skyrim)
            {
                Chunks = new SkyrimBookHelper().ChunkBook(Unit);
            }
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

        public UnitGroup Translate(TransParam Params,bool IsBook)
        {
            if (this.From == this.To)
            {
                return Params.Data;
            }

            UnitGroup SetUnitGroup = Params.Data;

            if (Params.Preprocessor == null)
            {
                Params.Preprocessor = this.PreprocessorInstance;
            }

            if (IsBook)
            {
                List<UnitChunk> Chunks = new List<UnitChunk>();
                List<BaseUnit> Units = new List<BaseUnit>();
                Game GameType = Game.Null;

                bool Book = false;

                var GetFrist = SetUnitGroup.GetFrist();

                if (SkyrimBookHelper.IsSkyrimBook(GetFrist, ref GameType))
                {
                    GameType = Game.Skyrim;
                    Book = true;
                    Units.AddRange(ChunkTranslationUnit(GameType, GetFrist, ref Chunks));
                }

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

                UnitGroup NewUnitGroup = new UnitGroup(SetUnitGroup.ParentRef);

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
                        NewUnitGroup = new UnitGroup(SetUnitGroup.ParentRef);
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

                foreach (var GetUnitGroup in UnitGroups)
                {
                    var ResultGroup = Core.CallOnce(this,
                       PreprocessorInstance, SetUnitGroup, From, To, AIParam, Params.CanSleep, false);

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
                UnitGroup ReturnItem = new UnitGroup(SetUnitGroup.ParentRef);
                ReturnItem.Units.Add(new BaseUnit(GetFrist.FileUniqueKey, GetFrist.Key, GetFrist.Type, GetFrist.Original, MergeLine, 100));

                return ReturnItem;
            }
            else
            {
                return Core.CallOnce(this,
                    PreprocessorInstance, SetUnitGroup, From, To, AIParam, Params.CanSleep, true);
            }
        }

        public void UnifiedSymbols()
        {
            try
            {
                for (int i = 0; i < TranslatedLink.Count; i++)
                {
                    try
                    {
                        var GetHashKey = TranslatedLink.ElementAt(i).Key;
                        if (TranslatedLink[GetHashKey].Trim().Length > 0)
                        {
                            TranslationPreprocessor.UnifiedSymbols(this,GetHashKey, TranslatedLink[GetHashKey].Trim());
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