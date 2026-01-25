using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.EngineManagement;
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
            if (this.Enable)
            {
                this.CallCount++;
                return Key;
            }
            else
            {
                return string.Empty;
            }
  
        }
        public void CallError()
        {
            if (this.ErrorCount < KeyManage.MaxErrorCount)
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
        private object ArrayQueryLock = new object();
        public PlatformType Type = new PlatformType();
        private List<ApiKey> ApiKeys = new List<ApiKey>();
        public void AddKeys(List<string> Keys)
        {
            ApiKeys.Clear();
            foreach (var Key in Keys)
            {
                this.ApiKeys.Add(new ApiKey(Key));
            }
        }
        private void Sort()
        {
            ApiKeys.Sort(new ApiKeyComparer());
        }
        public string GetFirstKey()
        {
            lock (ArrayQueryLock)
            {
                if (ApiKeys.Count > 0)
                {
                    Sort();
                    return ApiKeys[0].GetKey();
                }

                return string.Empty;
            }
        }
        public bool HaveKey()
        {
            if (this.ApiKeys.Count > 0)
            {
                return true;
            }

            return false;
        }
    }
    public class KeyManage
    {
        private Dictionary<int, PlatformApiKeys> KeysData = new Dictionary<int, PlatformApiKeys>();
        public static int MaxErrorCount = 10;
        public void Init()
        {
            for (int i = 0; i < EngineConfig.Config.PlatformConfigs.Count; i++)
            { 
                int GetKey = EngineConfig.Config.PlatformConfigs.ElementAt(i).Key;
                var GetConfig = EngineConfig.Config.PlatformConfigs[GetKey];

                PlatformApiKeys NPlatformApiKeys = new PlatformApiKeys();
                NPlatformApiKeys.Type = GetConfig.Platform;
                NPlatformApiKeys.AddKeys(GetConfig.ApiKeys);

                KeysData.Add(GetKey, NPlatformApiKeys);
            }
        }

        public PlatformApiKeys GetData(PlatformType Type)
        {
            return KeysData[(int)Type];
        }

        public PlatformApiKeys GetData(int CustomID)
        {
            return KeysData[CustomID];
        }
    }
}
