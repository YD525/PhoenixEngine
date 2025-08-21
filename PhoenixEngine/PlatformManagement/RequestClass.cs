using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;

namespace PhoenixEngine.PlatformManagement
{
    public class RequestClass
    {
        public class CacheCall
        {
            public string SendString = "";
            public string ReceiveString = "";
            public string Log = "";

            public void Output()
            {

            }
        }
        public class PreTranslateCall
        {
            public string PlatformName = "";
            public string SendString = "";
            public string ReceiveString = "";
            public List<ReplaceTag> ReplaceTags = new List<ReplaceTag>();

            public bool FromAI = false;

            public void Output()
            {

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
                this.SendString = Send;
                this.ReceiveString = Recv;
            }

            public void Output()
            { 
            
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
               this.SendString = Send;
               this.ReceiveString = Recv;
            }

            public void Output()
            {

            }
        }
    }
}
