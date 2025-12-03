## 🧩 Third-Party Frameworks

This project uses the following third-party frameworks and packages:

- **Cohere**  
  Required package to call the CohereAI API for natural language processing tasks.

- **System.Data.SQLite**  
  Dependency package for SQLite database operations.

---

## 🔥 PhoenixEngine

**PhoenixEngine** is a high-performance, multi-threaded language translation engine.  
It combines **AI-powered translation** with **context generation**, and features **text segmentation** and **priority-based ordering** to deliver more natural and context-aware results.  
In addition, it provides **heuristic analysis for Papyrus scripts**, generating a **comprehensive scoring report** to help evaluate script structure, consistency, and potential translation risks.

---

## ⭐ API Usage Example (How to Call the Engine)

Quickly call PhoenixAPI

```csharp
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;

  public class PhoenixAPI
  {
      public void Init()
      {
          Engine.Init();
          DelegateHelper.SetTranslationUnitCallBack += TranslationUnitStateChanged;

          string SetCachePath = GetFullPath(@"\Cache");
          if (!Directory.Exists(SetCachePath))
          {
              Directory.CreateDirectory(SetCachePath);
          }

          EngineConfig.LMLocalAIEnable = true;
          EngineConfig.ContextEnable = true;

          EngineConfig.ContextLimit = 150;
          EngineConfig.PreTranslateEnable = true;

          EngineConfig.Save();

          Engine.From = Languages.English;
          Engine.To = Languages.English;
      }
    
      public TranslationUnit? Dequeue(ref bool IsEnd)
      {
          return Engine.DequeueTranslated(ref IsEnd);
      }
     
      public int Enqueue(string FileName, string Key, string Type, string Original, string AIParam)
      {
          TranslationUnit Unit = new TranslationUnit(
          CrcHelper.ComputeCRC32Int(FileName),
          Key,
          Type,
          Original,
          "",
          "",
          Engine.From,
          Engine.To,
          100
          );

          int GetEnqueueCount = Engine.AddTranslationUnit(Unit);

          return GetEnqueueCount;
      }

      public void Start()
      {
          Engine.Start();
      }

      public void End()
      {
          Engine.End();
      }

      public void SetThread(int ThreadCount)
      {
          EngineConfig.MaxThreadCount = ThreadCount;
          EngineConfig.AutoSetThreadLimit = false;

          EngineConfig.Save();
      }
      public int GetWorkingThreadCount()
      {
          return Engine.GetThreadCount();
      }
      public int SetLang(string From, string To)
      {
          try
          {
              Engine.From = LanguageHelper.FromLanguageCode(From);
              Engine.To = LanguageHelper.FromLanguageCode(To);

              return 1;

          }
          catch
          {
              return -1;
          }
       }
       
        /// <summary>
        /// This is used to receive any entry state change event.
        /// </summary>
        /// <param name="Item">Translation Unit</param>
        /// <param name="State">
        /// 0 = is picked up by the thread.
        /// 1 = Initiating translation.
        /// 2 = Obtain translation results
        /// </param>
        /// <returns></returns>
       public static bool TranslationUnitStateChanged(TranslationUnit Item,int State)
       {
           //If false is returned in stage 2, the final translation will not be stored in the database.
           return true;
       }
  }

```
---
## 🌙 SSEATTransCore — Lightweight Version Made Exclusively for SSEAT

The Lite version of the translation core is available here:  
**https://github.com/YD525/SSEATTransCore**

This version is built on **.NET Framework**, so **end users do not need to install any runtime**.  
Compared to the full version, it **removes several AI platform integrations**, resulting in a **smaller and more lightweight** build.

This Lite edition is jointly maintained by the authors of **SSEAT** and **SSELex**.


## 💬 Community & Contribution

Join our Discord community: [https://discord.gg/GRu7WtgqsB](https://discord.gg/GRu7WtgqsB)  

Feel free to drop by and chat — always happy to talk code (or just vent boredom).