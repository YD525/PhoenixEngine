using System;
using System.Collections.Generic;
using PhoenixEngine.Engine;
using PhoenixEngine.Language;
using PhoenixEngine.Memory;
using PhoenixEngine.P_Delegate;
using PhoenixEngine.Translate;
using PhoenixEngine.Unit;

namespace PhoenixEngine.Platform
{
    public class HumanTranslationApi
    {
        public static PlatformType Type = PlatformType.HumanTranslation;

        public EngineConfigJson ConfigRef { get; set; } = null;
        public AITranslationMemory AIMemoryRef { get; set; } = null;

        public int CustomID { get; set; } = 0;

        public void Init(int CustomID, AITranslationMemory AIMemory, EngineConfigJson Config)
        {
            this.CustomID = CustomID;
            this.AIMemoryRef = AIMemory;
            this.ConfigRef = Config;
        }

        public static AwaitHumanTranslationHandler WaitHumanInput = null;
        public delegate string AwaitHumanTranslationHandler(string SendStr);
        public string CallHuman(List<ReplaceTag> CustomWords, UnitGroup Source, Languages FromLang, Languages ToLang, bool UseAIMemory, int AIMemoryCountLimit, string AIParam, ref AICall Call)
        {
            if (WaitHumanInput == null)
            {
                throw new Exception("Null Func Ptr!");
            }

            List<string> Related = new List<string>();

            if (ConfigRef.ContextEnable && UseAIMemory)
            {
                Related = Source.QueryAIMemory(FromLang, ToLang, AIMemoryCountLimit);
            }

            if (ConfigRef.UserCustomAIPrompt.Trim().Length > 0)
            {
                AIParam = AIParam + "\n" + ConfigRef.UserCustomAIPrompt;
            }

            bool CanTrans = false;
            string TransSource = Source.GenContent(ref CanTrans,Source.IsLink);
            if (!CanTrans)
            {
                return "<empty>";
            }

            Call.Platform = PlatformType.HumanTranslation;

            var GetTransSource = AIPrompt.GenerateTranslationPrompt(FromLang, ToLang, TransSource, Related, CustomWords, AIParam);

            string Send = GetTransSource;

            Call.SendString = Send;

            string Recv = HumanTranslationApi.WaitHumanInput.Invoke(Send);

            Call.ReceiveString = Recv;

            return Recv;
        }
    }
}
