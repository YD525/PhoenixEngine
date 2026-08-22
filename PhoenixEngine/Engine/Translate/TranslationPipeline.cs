using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PhoenixEngine.Unit;

namespace PhoenixEngine.Translate
{
    /// <summary>
    /// Coordinates provider, persistence, and execution policy for the public translator facade.
    /// </summary>
    internal sealed class TranslationPipeline
    {
        private readonly ITranslationProvider _provider;
        private readonly ITranslationStore _store;
        private readonly ITranslationScheduler _scheduler;

        internal TranslationPipeline(
            ITranslationProvider provider,
            ITranslationStore store,
            ITranslationScheduler scheduler)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        }

        internal async Task<UnitGroup> TranslateAsync(
            TranslationRequest request,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<UnitGroup> results = await _scheduler.ExecuteAsync(
                new[] { request },
                _provider,
                _store,
                cancellationToken).ConfigureAwait(false);
            if (results.Count != 1 || results[0] == null)
                throw new InvalidOperationException("The translation scheduler returned no result for a single request.");
            return results[0];
        }
    }

    /// <summary>
    /// Executes translation requests sequentially while honoring cache and cancellation boundaries.
    /// </summary>
    public sealed class SequentialTranslationScheduler : ITranslationScheduler
    {
        /// <inheritdoc />
        public async Task<IReadOnlyList<UnitGroup>> ExecuteAsync(
            IReadOnlyList<TranslationRequest> requests,
            ITranslationProvider provider,
            ITranslationStore store,
            CancellationToken cancellationToken)
        {
            if (requests == null)
                throw new ArgumentNullException(nameof(requests));
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            var results = new List<UnitGroup>(requests.Count);
            foreach (TranslationRequest request in requests)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!store.TryGet(request, out UnitGroup result))
                {
                    result = await provider.TranslateAsync(request, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    store.Store(request, result);
                }
                results.Add(result);
            }
            return results;
        }
    }

    internal sealed class NullTranslationStore : ITranslationStore
    {
        public bool TryGet(TranslationRequest request, out UnitGroup result)
        {
            result = null;
            return false;
        }

        public void Store(TranslationRequest request, UnitGroup result)
        {
        }
    }

    internal sealed class LegacyTranslationProvider : ITranslationProvider
    {
        private readonly Translator _translator;

        internal LegacyTranslationProvider(Translator translator)
        {
            _translator = translator ?? throw new ArgumentNullException(nameof(translator));
        }

        public Task<UnitGroup> TranslateAsync(
            TranslationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_translator.TranslateCore(request.Parameters, cancellationToken));
        }
    }
}
