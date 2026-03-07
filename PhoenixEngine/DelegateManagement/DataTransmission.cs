using System.Collections.Generic;
using PhoenixEngine.Engine;
using PhoenixEngine.Events;
using PhoenixEngine.Translate;
using PhoenixEngine.TranslateCore;

namespace PhoenixEngine
{
    public enum CallType
    {
        Null = 0, CacheCall = 1, PreTranslateCall = 2, PlatformCall = 3, AICall = 5
    }

    public class DataTransmission
    {
        public static void Recv(CallType Type, object Any)
        {
            Recv((int)Type, Any);
        }
        public static void Recv(int Type, object Any)
        {
            if (EngineEvents.SetDataCall != null)
            {
                EngineEvents.SetDataCall(Type, Any);
            }
        }
    }

    public class PreTranslateCall
    {
        public string Key = "";
        public PlatformType Platform = PlatformType.Null;
        public string SendString = "";
        public string ReceiveString = "";
        public List<ReplaceTag> ReplaceTags = new List<ReplaceTag>();

        public bool FromAI = false;

        public PreTranslateCall()
        {
        }

        public void Output()
        {
            DataTransmission.Recv(CallType.PreTranslateCall, this);
        }
    }

    public class CacheCall
    {
        public string SendString = "";
        public string ReceiveString = "";
        public string Log = "";

        public CacheCall()
        {

        }

        public void Output()
        {
            DataTransmission.Recv(CallType.CacheCall, this);
        }
    }

    public class PlatformCall
    {
        public PlatformType Platform = PlatformType.Null;
        public int CustomID = 0;
        public Languages From = Languages.Null;
        public Languages To = Languages.Null;
        public string SendString = "";
        public string ReceiveString = "";
        public bool Success = false;

        public PlatformCall() { }
        public PlatformCall(PlatformType Platform, Languages From, Languages To, string Send, string Recv, int CustomID)
        {
            this.Platform = Platform;
            this.CustomID = CustomID;
            this.From = From;
            this.To = To;
            this.SendString = Send;
            this.ReceiveString = Recv;
        }

        public void Output()
        {
            DataTransmission.Recv(CallType.PlatformCall, this);
        }
    }
    public class AICall
    {
        public PlatformType Platform = PlatformType.Null;
        public int CustomID = 0;
        public string SendString = "";
        public string ReceiveString = "";
        public bool Success = false;

        public AICall() { }
        public AICall(PlatformType Platform, string Send, string Recv, int CustomID)
        {
            this.Platform = Platform;
            this.CustomID = CustomID;
            SendString = Send;
            ReceiveString = Recv;
        }

        public void Output()
        {
            DataTransmission.Recv(CallType.AICall, this);
        }
    }
}
