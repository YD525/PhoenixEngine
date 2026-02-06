using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using Newtonsoft.Json.Linq;
using PhoenixEngine.TranslateCore;

namespace PhoenixEngine.PlatformManagement
{
    public class CustomPlatformHelper
    {
        public static string EnCodeValue(string Content,ReqEncodeType EncodeType)
        {
            switch (EncodeType)
            {
                case ReqEncodeType.UrlEncode:
                    return System.Web.HttpUtility.UrlEncode(Content);
                case ReqEncodeType.HtmlEncode:
                    return System.Net.WebUtility.HtmlEncode(Content);
                case ReqEncodeType.UnicodeEscape:
                    return ReqEncodeHelper.EncodeUnicode(Content);
                case ReqEncodeType.Base64:
                    return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Content));
                default:
                    return Content;
            }
        }
        public static string DeCodeValue(string Content, ReqEncodeType DecodeType)
        {
            string NewContent = "";
            switch (DecodeType)
            {
                case ReqEncodeType.UrlEncode:
                    {
                        NewContent = System.Web.HttpUtility.UrlDecode(Content);
                    }
                    break;
                case ReqEncodeType.HtmlEncode:
                    {
                        NewContent = System.Net.WebUtility.HtmlDecode(Content);
                    }
                    break;
                case ReqEncodeType.UnicodeEscape:
                    {
                        NewContent = ReqEncodeHelper.DecodeUnicode(Content);
                    }
                    break;
                case ReqEncodeType.Base64:
                    {
                        NewContent = ReqEncodeHelper.DecodeBase64(Content);
                    }
                    break;
                default:
                    { 
                    
                    }
                break;
            }
            return NewContent;
        }

        public static List<ReqCustomKeyValue> GetJsonValues(string Json)
        {
            var Result = new List<ReqCustomKeyValue>();

            try
            {
                var Token = JToken.Parse(Json);
                ParseJsonElement(Token, "", Result);
            }
            catch
            {
               
            }

            return Result;
        }
        public static List<ReqCustomKeyValue> GetPayLoadKeyValues(PayLoad PayLoad)
        {
            if (PayLoad == null) return new List<ReqCustomKeyValue>();
            if (PayLoad.Content == null) return new List<ReqCustomKeyValue>();

            string Payload = PayLoad.Content;

            var Result = new List<ReqCustomKeyValue>();

            if (string.IsNullOrEmpty(Payload))
            {
                return Result;
            }

            Payload = Payload.Trim();

            if ((Payload.StartsWith("{") && Payload.EndsWith("}")) ||
                (Payload.StartsWith("[") && Payload.EndsWith("]")))
            {
                try
                {
                    var Token = JToken.Parse(Payload);
                    ParseJsonElement(Token, "", Result);
                }
                catch
                {
                    ParseForm(Payload, Result);
                }
            }
            else
            {
                ParseForm(Payload, Result);
            }

            return Result;
        }
        private static void ParseJsonElement(JToken Token, string ParentKey, List<ReqCustomKeyValue> Result)
        {
            if (Token == null)
                return;

            switch (Token.Type)
            {
                case JTokenType.Object:
                    foreach (var Prop in Token.Children<JProperty>())
                    {
                        string NewKey = string.IsNullOrEmpty(ParentKey) ? Prop.Name : ParentKey + "." + Prop.Name;

                        ParseJsonElement(Prop.Value, NewKey, Result);
                    }
                    break;

                case JTokenType.Array:
                    int Index = 0;
                    foreach (var Item in Token.Children())
                    {
                        string NewKey = ParentKey + "[" + Index + "]";

                        ParseJsonElement(Item, NewKey, Result);
                        Index++;
                    }
                    break;

                default:
                    Result.Add(new ReqCustomKeyValue(ParentKey, Token.ToString()));
                    break;
            }
        }
        private static void ParseForm(string Payload, List<ReqCustomKeyValue> Result)
        {
            var Params = Payload.Split('&');
            foreach (var Param in Params)
            {
                if (Param.Contains("="))
                {
                    Result.Add(new ReqCustomKeyValue(Param.Split('=')[0], Param.Split('=')[1]));
                }
            }
        }
    }

    public class CustomReqCore
    {
        private string _Url = "";
        private string _Header = "";
        private PayLoad _PayLoad = null;
        public bool IsPost { get; set; }

        private string ApiKey = "";
        private string Prompt = "";
        private string Model = "";

        public static string ApiKeySign = "{API_KEY}";
        public static string PromptSign = "{AI_Prompt}";
        public static string SourceSign = "{SourceStr}";
        public static string ModelSign = "{AI_Model}";
        public static string FromSign = "{P_From}";
        public static string ToSign = "{P_To}";

        public string From = "";
        public string To = "";
        public string Source = "";

        public string GetTagValue(ReqReplaceTag Tag)
        {
            string Value = Tag.GetValue();
            if (Value.Equals(ApiKeySign))
            {
                return ApiKey;
            }
            else
            if (Value.Equals(PromptSign))
            {
                return Prompt;
            }
            else
            if (Value.Equals(SourceSign))
            {
                return Source;
            }
            else
            if (Value.Equals(ModelSign))
            {
                return Model;
            }
            else
            if (Value.Equals(FromSign))
            {
                return From;
            }
            else
            if (Value.Equals(ToSign))
            {
                return To;
            }
            else
            {
                return Value;
            }
        }

        public void SetSource(string Source)
        { 
            this.Source = Source;
        }
        public void SetFrom(Languages From)
        {
            this.From = LanguageHelper.ToLanguageCode(From);
        }
        public void SetTo(Languages To)
        {
            this.To = LanguageHelper.ToLanguageCode(To);
        }

        public void SetApiKey(string ApiKey)
        { 
           this.ApiKey = ApiKey;
        }

        public void SetPrompt(string Prompt)
        {
            this.Prompt = Prompt;
        }

        public void SetModel(string Model)
        { 
            this.Model = Model;
        }

        public void SetUrl(string Url)
        {
            this._Url = HttpUtility.UrlDecode(Url);
        }

        public ReqQueryRuleItem QueryRule = new ReqQueryRuleItem();
        public string GenUrl(List<ReqReplaceTag> Tags)
        {
            if (!_Url.Contains("?"))
                return _Url;

            string UrlBase = _Url.Split('?')[0];

            var Params = GetUrlKeyValues();

            for (int i = 0; i < Params.Count; i++)
            {
                var Param = Params[i];
                var Tag = Tags.FirstOrDefault(T => T.Key == Param.Key);
                if (Tag != null)
                {
                    Params[i].Value = CustomPlatformHelper.EnCodeValue(GetTagValue(Tag), Tag.EncodeType);
                }
            }

            string NewQuery = string.Join("&", Params.Select(KV => KV.Key + "=" + KV.Value));

            return UrlBase + "?" + NewQuery;
        }
        public List<ReqCustomKeyValue> GetUrlKeyValues()
        {
            List<ReqCustomKeyValue> CustomKeyValues = new List<ReqCustomKeyValue>();
            if (_Url.Contains("?"))
            {
                string GetRightStr = _Url.Substring(_Url.IndexOf("?") + "?".Length);
                foreach (var GetParam in GetRightStr.Split('&'))
                {
                    if (GetParam.Trim().Length > 0 && GetParam.Contains("="))
                    {
                        string GetKey = GetParam.Split('=')[0];
                        string GetValue = GetParam.Split('=')[1];
                        CustomKeyValues.Add(new ReqCustomKeyValue(GetKey,GetValue));
                    }
                }
            }
            return CustomKeyValues;
        }
        public void SetHeader(string Header)
        { 
            this._Header = Header;
        }
        public string UserAgent { get; private set; } = "";
        public string ContentType { get; private set; } = "";
        public string Accept { get; private set; } = "";

        public WebHeaderCollection GenHeader(List<ReqReplaceTag> Tags)
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
                            GetValue = CustomPlatformHelper.EnCodeValue(GetTagValue(GetTag), GetTag.EncodeType);

                            foreach (var GetParam in GetHeaderKeyValues())
                            {
                                if (GetParam.Key.Equals(GetTag.Key))
                                {
                                    if (GetParam.Value.StartsWith("Bearer "))
                                    {
                                        if (!GetValue.StartsWith("Bearer "))
                                        {
                                            GetValue = "Bearer " + GetValue;
                                        }
                                        break;
                                    }
                                }
                            }
                            
                            break;
                        }
                    }

                    if (GetKey.ToLower().Equals("UserAgent".ToLower()))
                    {
                        UserAgent = GetValue;
                    }
                    else
                    if (GetKey.ToLower().Equals("ContentType".ToLower()) || GetKey.ToLower().Equals("Content-Type".ToLower()))
                    {
                        ContentType = GetValue;
                    }
                    else
                    if (GetKey.ToLower().Equals("Accept".ToLower()))
                    {
                        Accept = GetValue;
                    }
                    else
                    {
                        Header.Add(GetKey, GetValue);
                    }
                }
            }
            return Header;
        }
        public List<ReqCustomKeyValue> GetHeaderKeyValues()
        {
            List<ReqCustomKeyValue> CustomKeyValues = new List<ReqCustomKeyValue>();

            foreach (var GetLine in _Header.Split(new char[2] { '\r', '\n' }))
            {
                if (GetLine.Trim().Length > 0 && GetLine.Contains(":"))
                {
                    string GetKey = GetLine.Split(':')[0];
                    string GetValue = GetLine.Split(':')[1];
                    CustomKeyValues.Add(new ReqCustomKeyValue(GetKey, GetValue));
                }
            }

            return CustomKeyValues;
        }

        public void SetPayLoad(string PayLoad, ReqEncodeType Encoding = ReqEncodeType.Null)
        {
            _PayLoad = new PayLoad(PayLoad);
            _PayLoad.EncodeType = Encoding;
        }
        public List<ReqCustomKeyValue> GetPayLoadKeyValues()
        {
            return CustomPlatformHelper.GetPayLoadKeyValues(_PayLoad);
        }
   
        public string GenPayLoad(List<ReqReplaceTag> Tags)
        {
            PayLoad NewPayLoad = new PayLoad(_PayLoad.Content);
            NewPayLoad.EncodeType = _PayLoad.EncodeType;

            string PayLoad = NewPayLoad.Content;

            if (string.IsNullOrEmpty(PayLoad))
                return PayLoad;

            PayLoad = PayLoad.Trim();

            bool IsJson = (PayLoad.StartsWith("{") && PayLoad.EndsWith("}")) ||
                          (PayLoad.StartsWith("[") && PayLoad.EndsWith("]"));

            if (IsJson)
            {
                try
                {
                    var Token = JToken.Parse(PayLoad);
                    ReplaceJsonTokens(Token, Tags);
                    NewPayLoad.Content = Token.ToString(Newtonsoft.Json.Formatting.None);
                }
                catch
                {
                    NewPayLoad.Content = GenFormPayLoad(PayLoad, Tags);
                }
            }
            else
            {
                NewPayLoad.Content = GenFormPayLoad(PayLoad, Tags);
            }

            return CustomPlatformHelper.EnCodeValue(NewPayLoad.Content,NewPayLoad.EncodeType);
        }
        private JToken BuildJsonValue(ReqReplaceTag tag, JToken originalToken)
        {
            string raw = GetTagValue(tag);

            if (originalToken == null || originalToken.Type == JTokenType.Null)
                return JValue.CreateNull();

            switch (originalToken.Type)
            {
                case JTokenType.Boolean:
                    return new JValue(bool.Parse(raw));

                case JTokenType.Integer:
                    return new JValue(long.Parse(raw));

                case JTokenType.Float:
                    return new JValue(double.Parse(raw));

                case JTokenType.String:
                    return new JValue(
                        CustomPlatformHelper.EnCodeValue(raw, tag.EncodeType)
                    );

                default:
                    return new JValue(raw);
            }
        }
        private void ReplaceJsonTokens(JToken Token, List<ReqReplaceTag> Tags, string ParentKey = "")
        {
            if (Token == null) return;

            switch (Token.Type)
            {
                case JTokenType.Object:
                    foreach (var Prop in Token.Children<JProperty>())
                    {
                        string FullKey = string.IsNullOrEmpty(ParentKey) ? Prop.Name : ParentKey + "." + Prop.Name;

                        var Tag = Tags.FirstOrDefault(T => T.Key == FullKey);
                        if (Tag != null)
                            Prop.Value = BuildJsonValue(Tag,Prop.Value);

                        ReplaceJsonTokens(Prop.Value, Tags, FullKey);
                    }
                    break;

                case JTokenType.Array:
                    int Index = 0;
                    foreach (var Item in Token.Children())
                    {
                        string ArrayKey = ParentKey + "[" + Index + "]";

                        var Tag = Tags.FirstOrDefault(t => t.Key == ArrayKey);
                        if (Tag != null && Item.Type != JTokenType.Object && Item.Type != JTokenType.Array)
                        {
                            Item.Replace(BuildJsonValue(Tag,Item));
                        }

                        ReplaceJsonTokens(Item, Tags, ArrayKey);
                        Index++;
                    }
                    break;

                default:
                    break; 
            }
        }
        private string GenFormPayLoad(string Payload, List<ReqReplaceTag> Tags)
        {
            var Params = Payload.Split('&');
            for (int i = 0; i < Params.Length; i++)
            {
                if (!Params[i].Contains("=")) continue;

                var KV = Params[i].Split(new[] { '=' }, 2);
                string Key = KV[0];
                string Value = KV.Length > 1 ? KV[1] : "";

                var Tag = Tags.FirstOrDefault(t => t.Key == Key || t.Key == char.ToUpper(Key[0]) + Key.Substring(1));
                if (Tag != null)
                    Value = CustomPlatformHelper.EnCodeValue(GetTagValue(Tag), Tag.EncodeType);

                Key = char.ToUpper(Key[0]) + Key.Substring(1);

                Params[i] = Key + "=" + Value;
            }

            return string.Join("&", Params);
        }

        public void SetQueryRule(ReqQueryRuleItem QueryRule)
        { 
            this.QueryRule = QueryRule;
        }
    }
    
    public class ReqQueryRuleItem
    {
        public string FieldName { get; set; }
        public bool ByJson = false;

        public string LeftStr = "";
        public string RightStr = "";
        public string SplitStr = "";
    }

    public class ReqCustomKeyValue
    {
        public string Key = "";
        public string Value = "";

        public ReqCustomKeyValue(string Key, string Value)
        {
            this.Key = Key.Trim();
            this.Value = Value.Trim();
        }
    }

    public enum ReqEncodeType
    { 
        Null = 0, UrlEncode = 1, HtmlEncode = 2, UnicodeEscape = 3, Base64 = 5
    }

    internal class ReqEncodeHelper
    {
        public static string EncodeUnicode(string Input)
        {
            if (string.IsNullOrEmpty(Input))
            {
                return Input;
            }

            var NStringBuilder = new StringBuilder();

            foreach (char C in Input)
            {
                if (C <= 127)
                {
                    NStringBuilder.Append(C);
                }
                else
                {
                    NStringBuilder.AppendFormat("\\u{0:X4}", (int)C);
                }
            }

            return NStringBuilder.ToString();
        }

        public static string DecodeUnicode(string Input)
        {
            if (string.IsNullOrEmpty(Input))
            {
                return Input;
            }

            var NStringBuilder = new StringBuilder(Input.Length);

            for (int i = 0; i < Input.Length; i++)
            {
                char C = Input[i];

                if (C == '\\' && i + 5 < Input.Length && Input[i + 1] == 'u')
                {
                    string Hex = Input.Substring(i + 2, 4);

                    if (int.TryParse(Hex, NumberStyles.HexNumber, null, out int code))
                    {
                        NStringBuilder.Append((char)code);
                        i += 5; 
                        continue;
                    }
                }

                NStringBuilder.Append(C);
            }

            return NStringBuilder.ToString();
        }

        public static string DecodeBase64(string Content)
        {
            if (string.IsNullOrEmpty(Content))
                return Content;

            byte[] Bytes = Convert.FromBase64String(Content);
            return Encoding.UTF8.GetString(Bytes);
        }
    }

    public class PayLoad
    {
        public string Content = "";

        public ReqEncodeType EncodeType = ReqEncodeType.Null;
        public PayLoad(string Content)
        { 
          this.Content = Content;
        }
    }

    public class ReqReplaceTag
    {
        public string Key { get; set; } = "";
        public ReqEncodeType EncodeType { get; set; } = ReqEncodeType.Null;
        public string Value { get; set; } = "";

        public string GetValue()
        {
            return this.Value;
        }

        public void SetValue(string Value, ReqEncodeType Type)
        { 
            this.Value = Value;
            this.EncodeType = Type;
        }

        public ReqReplaceTag(string Key, string Value)
        {
            this.Key = Key;
            this.Value = Value;
        }
    }

    public enum CustomPlatformType
    { 
        Null = 0, LocalAI = 1, CloudAI = 2 ,Traditional = 3
    }
}
