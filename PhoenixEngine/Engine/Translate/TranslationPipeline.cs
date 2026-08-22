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
                throw new InvalidOperationException(
                    "The translation scheduler returned no result for a single request.");
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
                    Task<UnitGroup> providerTask = provider.TranslateAsync(request, cancellationToken);
                    result = await TaskCancellation.AwaitAsync(
                        providerTask,
                        cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    store.Store(request, result);
                }
                results.Add(result);
            }
            return results;
        }
    }

    /// <summary>
    /// Stops awaiting non-cooperative provider tasks when the caller cancels.
    /// </summary>
    internal static class TaskCancellation
    {
        /// <summary>
        /// Awaits an operation until it completes or the caller cancels without blocking a thread.
        /// </summary>
        /// <typeparam name="T">The operation result type.</typeparam>
        /// <param name="operation">The provider operation to observe.</param>
        /// <param name="cancellationToken">The token that stops awaiting the operation.</param>
        /// <returns>A task containing the provider result.</returns>
        internal static async Task<T> AwaitAsync<T>(Task<T> operation, CancellationToken cancellationToken)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }
            if (operation.IsCompleted || !cancellationToken.CanBeCanceled)
            {
                return await operation.ConfigureAwait(false);
            }

            var cancellationSource = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancellationSource.TrySetResult(true)))
            {
                Task completed = await Task.WhenAny(operation, cancellationSource.Task).ConfigureAwait(false);
                if (completed != operation)
                {
                    ObserveFailure(operation);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            return await operation.ConfigureAwait(false);
        }

        /// <summary>
        /// Observes a detached compatibility task so its failure remains available without becoming unobserved.
        /// </summary>
        /// <param name="operation">The task whose eventual failure is observed.</param>
        internal static void ObserveFailure(Task operation)
        {
            operation.ContinueWith(
                completed =>
                {
                    _ = completed.Exception;
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
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
            return Task.Run(
                () => _translator.TranslateCore(request.Parameters, cancellationToken),
                cancellationToken);
        }
    }
}
