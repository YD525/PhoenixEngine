using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.TranslateManage;

namespace PhoenixEngine.PlatformManagement
{
    public class ApiKey
    {
        private string Key;
        public bool Enable = true;

        public int ErrorCount;
        public int CallCount;
        public ApiKey(string ApiKey)
        { 
            this.Key = ApiKey;
            this.Enable = true;
        }
        public string GetKey()
        {
            this.CallCount++;
            return Key;
        }
        public void CallError()
        {
            if (this.ErrorCount < KeyManager.MaxErrorCount)
            {
                this.ErrorCount++;
            }
            else
            {
                this.Enable = false;
            }
        }
    }
    public class ApiKeyComparer : IComparer<ApiKey>
    {
        public int Compare(ApiKey X, ApiKey Y)
        {
            if (X == null || Y == null) return 0;

            int errorCompare = X.ErrorCount.CompareTo(Y.ErrorCount);
            if (errorCompare != 0)
                return errorCompare;

            return X.CallCount.CompareTo(Y.CallCount);
        }
    }
    public class PlatformApiKeys
    {
        public List<ApiKey> ApiKeys = new List<ApiKey>();

        private void Sort()
        {
            ApiKeys.Sort(new ApiKeyComparer());
        }
        public string GetFristKey()
        {
            if (ApiKeys.Count > 0)
            {
                Sort();
                return ApiKeys[0].GetKey();
            }
            return string.Empty;
        }
    }
    public class KeyManager
    {
        public static int MaxErrorCount = 10;
        public void Init()
        { 
        
        }

        public KeyManager(PlatformType Type)
        { 
        
        }
    }
}
