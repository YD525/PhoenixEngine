using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PhoenixEngine.PlatformManagement
{
    public class CustomPlatformHelper
    {
    }

    public class CustomCore
    {
        public string Url = "";
        public string PayLoad { get; set; }
        public bool IsPost { get; set; }

        public QueryRuleItem QueryRule = new QueryRuleItem();
        public string MakeUrl(string Url, List<ReplaceTag> Tags)
        { 
            
        }
        public WebHeaderCollection MakeHeader(string HeaderStr, List<ReplaceTag> Tags)
        { 
        
        }
        public string MakePayLoad(string PayLoad, List<ReplaceTag> Tags)
        { 
        
        }

        public string QueryReturn(string ReturnStr, QueryRuleItem QueryRule)
        { 
        
        }
    }
    
    public class QueryRuleItem
    {
        public string FieldName { get; set; }
        public bool ByJson = true;
        public bool IgnoreCase = true;

        public string LeftStr = "";
        public string RightStr = "";
    }

    public class ReplaceTag
    {
        public string Tag = "";
        public string Value = "";
    }

    public enum CustomPlatformType
    { 
        Null = 0, LocalAI = 1, CloudAI = 2 ,Traditional = 3
    }
}
