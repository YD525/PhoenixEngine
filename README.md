# 🔥 PhoenixEngine

**PhoenixEngine** is a high-performance, multi-threaded language translation engine.  
It combines AI-powered translation with **context generation**, text segmentation, and **priority-based ordering** to deliver natural and context-aware results.  
It also implements **Placeholder Logic**, allowing users to define custom dictionaries and placeholders for specific words, names, or terms, ensuring consistent translation of key terms across multiple contexts.

---

## ⭐ Aggregation-based Translation

Unlike traditional translation engines that rely on simple batching or brute-force concurrency, **Lexicon AI Translator** introduces an **aggregation-based translation model** at the engine level.

Before any AI request is issued, the engine analyzes the **structure and semantic relationships** of the source content.  
Text units that are contextually related, structurally similar, or semantically repetitive are grouped into a single **UnitGroup** and translated as one coherent semantic unit.

As a result:

- Multiple independent translation tasks are merged into a **single AI request**.
- Shared context is fully utilized.
- Redundant token usage is significantly reduced.

Even when explicit “context translation” is disabled by the user, **contextual awareness still exists implicitly**, because related content has already been aggregated and submitted together by the engine.

---

## ⚡ Fine-grained Unit Control

Each **text unit (`BaseUnit`)** is individually tracked and can be controlled via **signals and states**:

- **Created** – unit has been created but not yet processed.
- **Preparing** – unit is being prepared for translation.
- **Translating** – unit is currently being translated.
- **Queued** – unit has been submitted to the output queue.
- **Skipped** – unit is intentionally skipped.
- **Completed** – translation has finished successfully.
- **Failed** – translation failed.

This design allows:

- Precise handling of special cases.
- Skipped translations without affecting unrelated units.
- Real-time user overrides.
- Seamless integration with aggregation logic.

---

## 🚀 Performance & Scalability

With aggregation-based translation, **translation performance no longer scales linearly with the number of text lines**.  
Instead, it scales with **semantic complexity**, making it especially effective for:

- Large-scale scripts.
- Game localization.
- Content with high repetition.

In short: **aggregation-based translation improves performance by eliminating redundant AI work, not by forcing the AI to work faster**.

## Building from source

PhoenixEngine requires Visual Studio 2022 and the .NET Framework 4.8.1 Developer Pack. `PhoenixEngine.sln` is
the canonical build entry point; it contains the supported `PhoenixEngine\PhoenixEngine.csproj` product project
and its test project.

Restore the locked PackageReference dependencies, run analyzers, and build the x64 Release configuration:

```powershell
.\scripts\Test-RepositoryLayout.ps1
.\scripts\Invoke-Build.ps1 -Configuration Release -Platform x64
.\scripts\Test-PackageAdvisories.ps1
.\scripts\Run-Tests.ps1 -Configuration Release -Platform x64
```

Use `Invoke-Build.ps1 -UpdateLockFiles` only when intentionally changing package versions, then review and commit
both generated `packages.lock.json` files. Generated build outputs are not tracked.

Push a version tag matching `v*` to create `PhoenixEngine-win-x64.zip` and its SHA256 checksum as GitHub Release
assets. The archive contains the complete x64 Release output required by consuming applications.

---

## ✅ Summary

PhoenixEngine combines **semantic aggregation**, **fine-grained unit control**, and **placeholder logic** to deliver a translation engine that is:

- Fast
- Context-aware
- Highly customizable
- Scalable to large and repetitive datasets
