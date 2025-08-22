using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.DelegateManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;

namespace PhoenixEngine.EngineManagement
{
    public class DataTransmission
    {
        public enum CallType
        { 
            Null = 0, CacheCall = 1, PreTranslateCall = 2, PlatformCall = 3, AICall = 5
        }
        public static void Recv(CallType Type, object Any)
        {
            Recv((int)Type, Any);
        }
        public static void Recv(int Type, object Any)
        {
            if (DelegateHelper.SetDataCall != null)
            {
                DelegateHelper.SetDataCall(Type, Any);
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
                Recv(CallType.CacheCall,this);
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
                Recv(CallType.PreTranslateCall, this);
            }
        }
        public class PlatformCall
        {
            public PlatformType Platform = PlatformType.Null;
            public Languages From = Languages.Null;
            public Languages To = Languages.Null;
            public string SendString = "";
            public string ReceiveString = "";
            public bool Success = false;

            public PlatformCall()
            {
               
            }

            public PlatformCall(PlatformType Platform,Languages From,Languages To,string Send, string Recv)
            {
                this.Platform = Platform;
                this.From = From;
                this.To = To;
                SendString = Send;
                ReceiveString = Recv;
            }

            public void Output()
            {
                Recv(CallType.PlatformCall, this);
            }
        }
        public class AICall
        {
            public PlatformType Platform = PlatformType.Null;
            public string SendString = "";
            public string ReceiveString = "";
            public bool Success = false;

            public AICall()
            { 
            }

            public AICall(PlatformType Platform, string Send, string Recv)
            { 
               this.Platform = Platform;
               SendString = Send;
               ReceiveString = Recv;
            }

            public void Output()
            {
                Recv(CallType.AICall, this);
            }
        }
    }
}
