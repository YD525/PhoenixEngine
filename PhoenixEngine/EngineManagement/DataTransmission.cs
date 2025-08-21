using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            public string PlatformName = "";
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
            public string PlatformName = "";
            public Languages From = Languages.Null;
            public Languages To = Languages.Null;
            public string SendString = "";
            public string ReceiveString = "";
            public bool Success = false;

            public PlatformCall()
            {
               
            }

            public PlatformCall(string PlatformName,Languages From,Languages To,string Send, string Recv)
            {
                this.PlatformName = PlatformName;
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
            public string PlatformName = "";
            public string SendString = "";
            public string ReceiveString = "";
            public bool Success = false;

            public AICall()
            { 
            }

            public AICall(string PlatformName,string Send, string Recv)
            { 
               this.PlatformName = PlatformName;
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
