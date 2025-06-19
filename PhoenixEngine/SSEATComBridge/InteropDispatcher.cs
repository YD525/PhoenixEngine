using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PhoenixEngine.Engine;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;
using static PhoenixEngine.SSEATComBridge.InteropDispatcher;

namespace PhoenixEngine.SSEATComBridge
{
    /// <summary>
    /// For SSEAT
    /// </summary>
    [ComVisible(true)]
    [Guid("ABCD1234-5678-90AB-CDEF-1234567890AB")] 
    [ClassInterface(ClassInterfaceType.AutoDual)]  
    public class InteropDispatcher
    {
        public static void FormatData()
        {
            Translator.FormatData();
        }

        public static void ClearCache()
        {
            Translator.ClearCache();
        }

        public static string? GetTranslatorCache(string Key)
        {
            if (Translator.TransData.ContainsKey(Key))
            {
                return Translator.TransData[Key];
            }
            else
            {
                return null;
            }
        }

        public static string GetTransData(string Key)
        {
            var GetResult = GetTranslatorCache(Key);
            if (GetResult != null)
            {
                return GetResult;
            }
            else
            {
                Translator.TransData.Add(Key, string.Empty);
            }
            return string.Empty;
        }

        public static void SetTransData(string Key, string Value)
        {
            if (Translator.TransData.ContainsKey(Key))
            {
                Translator.TransData[Key] = Value;
            }
            else
            {
                Translator.TransData.Add(Key, Value);
            }
        }

        public static int ConvertLangToID(string Lang)
        { 
            Languages GetLang = (Languages)Enum.Parse(typeof(Languages), Lang);
            return (int)GetLang;
        }

        public static string translate(string Key,string Text,int Src,int Dst)
        {
            try 
            {
                if (Key.Trim().Length == 0 || Key == null)
                {
                    throw new Exception("Key is Null!");
                }

                bool CanSleep = true;
                return Translator.QuickTrans(EngineConfig.CurrentModName,string.Empty,Key,Text,(Languages)Src, (Languages)Dst,ref CanSleep);
            }
            catch (Exception e) 
            {
                throw;
            }
        }
        public static void CancelAll()
        {
            ForceStop = true;

            while (CurrentThreadCount > 0)
            {
                Thread.Sleep(10);
            }

            CurrentThreadCount = 0;
        }

        public class WaitList
        {
            public string Key = "";
            public string Result = "";

            public WaitList(string Key, string Result)
            { 
                this.Key = Key;
                this.Result = Result;
            }
        }

        public static object QueueLocker = new object();
        public static Queue<WaitList> WaitLists = new Queue<WaitList>();

        public static void Clear()
        {
            WaitLists.Clear();
        }

        public static bool ForceStop = false;

        public static int CurrentThreadCount = 0;
        public static void translate_async(string Key, string Text, int Src, int Dst)
        {
            ForceStop = false;

            try
            {
                new Thread(() =>
                {
                    while (CurrentThreadCount > EngineConfig.MaxThreadCount)
                    {
                        Thread.Sleep(EngineConfig.ThrottleDelayMs);

                        if (ForceStop)
                        {
                            CurrentThreadCount--;
                            return;
                        }
                    }

                    CurrentThreadCount++;

                    bool CanSleep = true;

                    if (ForceStop)
                    {
                        CurrentThreadCount--;
                        return;
                    }

                    var GetResult = Translator.QuickTrans(EngineConfig.CurrentModName, string.Empty, Key, Text, (Languages)Src, (Languages)Dst, ref CanSleep);

                    lock (QueueLocker)
                    {
                        WaitLists.Enqueue(new WaitList(Key, GetResult));
                    }

                    CurrentThreadCount--;

                }).Start();
            }
            catch (Exception e)
            {
                throw;
            }
        }

        public static string QueryThreadInFo()
        {
            ThreadUsageInfo NewInfo = new ThreadUsageInfo();
            NewInfo.MaxThreads = EngineConfig.MaxThreadCount;
            NewInfo.CurrentThreads = CurrentThreadCount;

            return JsonSerializer.Serialize(NewInfo);
        }

        public static string dequeue_translated()
        {
            if (WaitLists.Count > 0)
            {
                lock (QueueLocker)
                {
                   var GetResult = WaitLists.Dequeue();
                   return JsonSerializer.Serialize(GetResult);
                }
            }

            return string.Empty;
        }

        public static int get_translation_queue_length()
        {
            lock (QueueLocker)
            {
                return WaitLists.Count;
            }
        }
    }
}
