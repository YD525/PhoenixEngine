
## 🔥 PhoenixEngine

**PhoenixEngine** is a high-performance, multi-threaded language translation engine.  
It combines **AI-powered translation** with **context generation**, and features **text segmentation** and **priority-based ordering** to deliver more natural and context-aware results.  
It also implements **Placeholder Logic**, allowing users to define custom dictionaries and placeholders for specific words, names, or terms, ensuring **consistent translation of key terms across multiple contexts**.  
In addition, it provides **heuristic analysis for Papyrus scripts**, generating a **comprehensive scoring report** to help evaluate script structure, consistency, and potential translation risks.

---

## ⭐ Aggregation-based Translation

Lexicon AI Translator does not rely on simple request batching or brute-force concurrency to improve translation speed.
Instead, it introduces an aggregation-based translation model at the engine level.

Before any AI request is issued, the engine analyzes the structure and semantic relationships of the source content.
Text units that are contextually related, structurally similar, or semantically repetitive are grouped into a single UnitGroup and translated as one coherent semantic unit.

As a result, multiple independent translation tasks are merged into a single AI request, allowing shared context to be fully utilized while significantly reducing redundant token usage.

Even when explicit “context translation” is disabled by the user, contextual awareness still exists implicitly.
This is because related content has already been aggregated and submitted together by the engine itself.

With this approach, translation performance no longer scales linearly with the number of text lines.
Instead, it scales with semantic complexity, making it especially effective for large-scale scripts, game localization, and content with high repetition.

In short:
Aggregation-based Translation improves performance by eliminating redundant AI work, not by forcing the AI to work faster.


## 💬 Community & Contribution

Join our Discord community: [https://discord.gg/GRu7WtgqsB](https://discord.gg/GRu7WtgqsB)  

Feel free to drop by and chat — always happy to talk code (or just vent boredom).