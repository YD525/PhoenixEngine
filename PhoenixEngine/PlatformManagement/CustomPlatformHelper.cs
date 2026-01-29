using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace PhoenixEngine.PlatformManagement
{
    public class CustomPlatformHelper
    {
    }

    public class CustomCore
    {
        private string _Url = "";
        private string _Header = "";
        private string _PayLoad = "";
        public bool IsPost { get; set; }

        public void SetUrl(string Url)
        {
            this._Url = HttpUtility.UrlDecode(Url);
        }

        public QueryRuleItem QueryRule = new QueryRuleItem();
        public string GenUrl(List<ReplaceTag> Tags)
        {
            string NewUrl = string.Copy(_Url);

            foreach (var GetTag in Tags)
            {
                NewUrl.Replace(GetTag.Tag,GetTag.Value);
            }

            return NewUrl;
        }
        public List<CustomKeyValue> GetUrlKeyValues()
        {
            List<CustomKeyValue> CustomKeyValues = new List<CustomKeyValue>();
            if (_Url.Contains("?"))
            {
                string GetRightStr = _Url.Substring(_Url.IndexOf("?") + "?".Length);
                foreach (var GetParam in GetRightStr.Split('&'))
                {
                    if (GetParam.Trim().Length > 0 && GetParam.Contains("="))
                    {
                        string GetKey = GetParam.Split('=')[0];
                        string GetValue = GetParam.Split('=')[1];
                        CustomKeyValues.Add(new CustomKeyValue(GetKey,GetValue));
                    }
                }
            }
            return CustomKeyValues;
        }
        public void SetHeader(string Header)
        { 
            this._Header = Header;
        }
        public WebHeaderCollection GenHeader(List<ReplaceTag> Tags)
        {
            WebHeaderCollection Header = new WebHeaderCollection();
            foreach (var GetLine in _Header.Split(new char[2] { '\r', '\n' }))
            {
                if (GetLine.Trim().Length > 0 && GetLine.Contains(":"))
                {
                    string GetKey = GetLine.Split(':')[0];

                    string GetValue = GetLine.Split(':')[1];

                    foreach (var GetTag in Tags)
                    {
                        if (GetTag.Key.Equals(GetKey))
                        {
                            GetValue = GetTag.Value;
                            break;
                        }
                    }
                    
                    Header.Add(GetKey, GetValue);
                }
            }
            return Header;
        }
        public List<CustomKeyValue> GetHeaderKeyValues()
        {
            List<CustomKeyValue> CustomKeyValues = new List<CustomKeyValue>();

            foreach (var GetLine in _Header.Split(new char[2] { '\r', '\n' }))
            {
                if (GetLine.Trim().Length > 0 && GetLine.Contains(":"))
                {
                    string GetKey = GetLine.Split(':')[0];
                    string GetValue = GetLine.Split(':')[1];
                    CustomKeyValues.Add(new CustomKeyValue(GetKey, GetValue));
                }
            }

            return CustomKeyValues;
        }
       
        public string MakePayLoad(string PayLoad, List<ReplaceTag> Tags)
        { 
        
        }
    }
    
    public class QueryRuleItem
    {
        public string FieldName { get; set; }
        public bool ByJson = true;

        public string LeftStr = "";
        public string RightStr = "";
        public string SplitStr = "";
    }

    public class CustomKeyValue
    {
        public string Key = "";
        public string Value = "";

        public CustomKeyValue(string Key, string Value)
        {
            this.Key = Key;
            this.Value = Value;
        }
    }

    public class ReplaceTag
    {
        public string Tag = "";
        public string Key = "";
        public string Value = "";

        public ReplaceTag(string Key, string Value)
        {
            this.Key = Key;
            this.Value = Value;
            this.Tag = "{" + Key + "}";
        }
    }

    public enum CustomPlatformType
    { 
        Null = 0, LocalAI = 1, CloudAI = 2 ,Traditional = 3
    }
}
