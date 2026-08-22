using Microsoft.VisualStudio.TestTools.UnitTesting;
using PhoenixEngine.Engine;
using PhoenixEngine.Events;
using PhoenixEngine.Language;
using PhoenixEngine.Memory;
using PhoenixEngine.Sequence;
using PhoenixEngine.Translate;
using PhoenixEngine.Unit;
using System.Collections.Generic;
using System.Linq;

namespace PhoenixEngine.Tests
{
    /// <summary>Verifies deterministic engine grouping, preprocessing, state, and memory behavior.</summary>
    [TestClass]
    [DoNotParallelize]
    public sealed class EngineBehaviorTests
    {
        private EngineConfigJson _previousConfig;
        private AITranslationMemory _previousMemory;
        private EngineEvents.OnUnitStateChanged _previousStateCallback;

        /// <summary>Isolates process-wide engine collaborators before each test.</summary>
        [TestInitialize]
        public void Initialize()
        {
            _previousConfig = Phoenix.Config;
            _previousMemory = Phoenix.AIMemory;
            _previousStateCallback = EngineEvents.SetBaseUnitStateChangedCallback;

            Phoenix.Config = new EngineConfigJson
            {
                PlatformConfigs = new Dictionary<int, PlatformConfig>()
            };
            Phoenix.AIMemory = new AITranslationMemory();
            EngineEvents.SetBaseUnitStateChangedCallback = null;
        }

        /// <summary>Restores process-wide engine collaborators after each test.</summary>
        [TestCleanup]
        public void Cleanup()
        {
            EngineEvents.SetBaseUnitStateChangedCallback = _previousStateCallback;
            Phoenix.AIMemory = _previousMemory;
            Phoenix.Config = _previousConfig;
        }

        /// <summary>Verifies stable bucket formation without mixing unrelated content into linked groups.</summary>
        [TestMethod]
        public void FormsBucketsDeterministicallyAndPreservesLinks()
        {
            Phoenix.Config.BucketLengthLimit = 500;
            Phoenix.Config.ContextLimit = 200;
            Phoenix.Config.StrictLinkBucketPurity = true;

            string[] first = BuildBucketSignatures();
            string[] second = BuildBucketSignatures();

            CollectionAssert.AreEqual(first, second, "Repeated builds must produce identical groups.");
            CollectionAssert.Contains(first, "L:dialogue-a,dialogue-b");
            CollectionAssert.Contains(first, "S:unrelated-c");
        }

        /// <summary>Verifies skip decisions and quote normalization during initial preprocessing.</summary>
        [TestMethod]
        public void PreprocessesSymbolsNumbersAndQuotedTextConsistently()
        {
            var group = new UnitGroup();
            group.Units.Add(CreateUnit("symbols", "!!!"));
            group.Units.Add(CreateUnit("number", "12345"));
            group.Units.Add(CreateUnit("quoted", "\"Hello world\""));

            Dictionary<string, UnitSequence> sequences;
            group.StartPreProcess(
                new TranslationPreprocessor(),
                Languages.English,
                Languages.German,
                out sequences);

            Assert.IsTrue(sequences["symbols"].CanSkip);
            Assert.AreEqual("!!!", group.Units[0].Translated);
            Assert.IsTrue(sequences["number"].CanSkip);
            Assert.AreEqual("12345", group.Units[1].Translated);
            Assert.IsFalse(sequences["quoted"].CanSkip);
            Assert.IsTrue(sequences["quoted"].HasOuterQuotes);
            Assert.AreEqual("Hello world", sequences["quoted"].Data);
            Assert.AreEqual(1, sequences["quoted"].Step);
        }

        /// <summary>Verifies accepted mutations and rejected units remain consistent in one state transition.</summary>
        [TestMethod]
        public void AppliesPartialStateTransitionsWithoutCorruptingRejectedUnits()
        {
            var accepted = CreateUnit("accepted", "Original A");
            var rejected = CreateUnit("rejected", "Original B");
            var group = new UnitGroup();
            group.Units.Add(accepted);
            group.Units.Add(rejected);
            var observedStates = new List<UnitTranslationState>();

            EngineEvents.SetBaseUnitStateChangedCallback = (translatorId, item, state) =>
            {
                Assert.AreEqual("test-translator", translatorId);
                observedStates.Add(state);
                if (item.Key == "accepted")
                {
                    item.Original = "Normalized A";
                    item.Translated = "Translated A";
                    return new UnitContext<BaseUnit>
                    {
                        Data = item,
                        ControlSignal = new Signal { Sign = 1, Index = 3 }
                    };
                }

                return new UnitContext<BaseUnit>
                {
                    Data = item,
                    ControlSignal = new Signal { Sign = -1, Index = 7 }
                };
            };

            GroupContext context = group.ApplyStateChange(
                "test-translator",
                UnitTranslationState.Preparing);

            Assert.AreEqual("Normalized A", accepted.Original);
            Assert.AreEqual("Translated A", accepted.Translated);
            Assert.AreEqual("Original B", rejected.Original);
            Assert.AreEqual(string.Empty, rejected.Translated);
            Assert.AreEqual(2, observedStates.Count);
            Assert.IsTrue(observedStates.All(state => state == UnitTranslationState.Preparing));
            int rejectedIndex = -1;
            Assert.IsFalse(context.CanDo(-1, ref rejectedIndex));
            Assert.AreEqual(7, rejectedIndex);
        }

        /// <summary>
        /// Verifies translation-memory add, update, query, conditional removal, and deletion behavior.
        /// </summary>
        [TestMethod]
        public void MaintainsTranslationMemoryIndexesAcrossUpdatesAndDeletes()
        {
            var memory = new AITranslationMemory();
            memory.AddTranslation(
                Languages.English,
                Languages.German,
                "The dragon guards the cave",
                "Der Drache bewacht die Höhle");

            List<string> initial = memory.FindRelevantTranslationsPublic(
                Languages.English,
                Languages.German,
                "dragon cave",
                500);
            CollectionAssert.AreEqual(
                new[] { "The dragon guards the cave -> Der Drache bewacht die Höhle" },
                initial);

            memory.AddTranslation(
                Languages.English,
                Languages.German,
                "The dragon guards the cave",
                "Ein Drache bewacht die Höhle");
            Assert.IsFalse(memory.RemoveTranslation(
                Languages.English,
                Languages.German,
                "The dragon guards the cave",
                "obsolete"));

            List<string> updated = memory.FindRelevantTranslationsPublic(
                Languages.English,
                Languages.German,
                "dragon",
                500);
            CollectionAssert.AreEqual(
                new[] { "The dragon guards the cave -> Ein Drache bewacht die Höhle" },
                updated);

            Assert.IsTrue(memory.DeleteTranslation(
                Languages.English,
                Languages.German,
                "The dragon guards the cave"));
            Assert.AreEqual(
                0,
                memory.FindRelevantTranslationsPublic(
                    Languages.English,
                    Languages.German,
                    "dragon",
                    500).Count);
        }

        private static string[] BuildBucketSignatures()
        {
            var translator = new Translator(
                "bucket-test",
                Languages.English,
                Languages.German,
                false);
            try
            {
                var units = new List<BaseUnit>
                {
                    CreateUnit("dialogue-a", "The dragon is near the old gate."),
                    CreateUnit("dialogue-b", "The dragon waits beside the old gate."),
                    CreateUnit("unrelated-c", "Fresh apples are available today.")
                };
                var container = new P_BucketContainer(translator, units);
                container.CheckLinksEvent = (all, current) => current.Key == "dialogue-a"
                    ? new List<BaseUnit> { units[0], units[1] }
                    : null;

                container.Build();

                Assert.AreEqual(3, container.GetCount());
                Assert.AreEqual(100D, container.MarkHeadsPercent);
                Assert.IsTrue(container.Units.All(group => group.Units.Count > 0));
                return container.Units
                    .Select(group =>
                        (group.IsLink ? "L:" : "S:") +
                        string.Join(",", group.Units.Select(unit => unit.Key).OrderBy(key => key)))
                    .OrderBy(signature => signature)
                    .ToArray();
            }
            finally
            {
                translator.Close();
            }
        }

        private static BaseUnit CreateUnit(string key, string original)
        {
            return new BaseUnit(1, key, "DIALOGUE", original, string.Empty, string.Empty, 100);
        }
    }
}
