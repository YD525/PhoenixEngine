using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PhoenixEngine.Engine;
using PhoenixEngine.Language;
using PhoenixEngine.Translate;
using PhoenixEngine.Unit;

namespace PhoenixEngine.Tests
{
    /// <summary>
    /// Verifies provider, store, and scheduler boundaries without HTTP or SQLite implementations.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public sealed class TranslationBoundaryTests
    {
        /// <summary>
        /// Verifies that the public translator facade delegates through in-memory components.
        /// </summary>
        [TestMethod]
        public async Task TranslatorFacadeUsesInjectedProviderStoreAndSchedulerAsync()
        {
            EngineConfigJson previousConfig = Phoenix.Config;
            Phoenix.Config = new EngineConfigJson
            {
                PlatformConfigs = new Dictionary<int, PlatformConfig>()
            };
            var provider = new InMemoryProvider();
            var store = new InMemoryStore();
            var translator = new Translator(
                "boundary-test",
                Languages.English,
                Languages.German,
                false)
            {
                TranslationProvider = provider,
                TranslationStore = store,
                TranslationScheduler = new SequentialTranslationScheduler()
            };

            try
            {
                TransParam parameters = CreateParameters("first", "Hello");
                UnitGroup first = await translator.TranslateAsync(parameters, CancellationToken.None);
                UnitGroup cached = await translator.TranslateAsync(parameters, CancellationToken.None);

                Assert.AreEqual("translated:Hello", first.Units[0].Translated);
                Assert.AreSame(first, cached);
                Assert.AreEqual(1, provider.CallCount);
                Assert.AreEqual(1, store.StoreCount);
            }
            finally
            {
                translator.Close();
                Phoenix.Config = previousConfig;
            }
        }

        /// <summary>
        /// Verifies that domain contracts do not expose HTTP or SQLite types.
        /// </summary>
        [TestMethod]
        public void TranslationContractsContainOnlyDomainAndFrameworkTypes()
        {
            Type[] contracts =
            {
                typeof(TranslationRequest),
                typeof(ITranslationProvider),
                typeof(ITranslationStore),
                typeof(ITranslationScheduler)
            };
            IEnumerable<Type> exposedTypes = contracts.SelectMany(type =>
                type.GetMethods().SelectMany(method =>
                    method.GetParameters().Select(parameter => parameter.ParameterType)
                        .Concat(new[] { method.ReturnType }))
                .Concat(type.GetProperties().Select(property => property.PropertyType)));

            Assert.IsFalse(exposedTypes.Any(type =>
                (type.FullName ?? string.Empty).StartsWith("System.Data.SQLite", StringComparison.Ordinal) ||
                (type.FullName ?? string.Empty).StartsWith("System.Net.Http", StringComparison.Ordinal)));
        }

        private static TransParam CreateParameters(string key, string original)
        {
            var group = new UnitGroup();
            group.Units.Add(new BaseUnit(1, key, "DIALOGUE", original, string.Empty, string.Empty, 100));
            return new TransParam(group, false, false);
        }

        private sealed class InMemoryProvider : ITranslationProvider
        {
            internal int CallCount { get; private set; }

            public Task<UnitGroup> TranslateAsync(
                TranslationRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                UnitGroup result = request.Parameters.Data;
                foreach (BaseUnit unit in result.Units)
                    unit.Translated = "translated:" + unit.Original;
                return Task.FromResult(result);
            }
        }

        private sealed class InMemoryStore : ITranslationStore
        {
            private UnitGroup _result;
            internal int StoreCount { get; private set; }

            public bool TryGet(TranslationRequest request, out UnitGroup result)
            {
                result = _result;
                return result != null;
            }

            public void Store(TranslationRequest request, UnitGroup result)
            {
                _result = result;
                StoreCount++;
            }
        }
    }
}
