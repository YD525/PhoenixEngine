using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PhoenixEngine.Language;
using PhoenixEngine.Unit;

namespace PhoenixEngine.Translate
{
    /// <summary>
    /// Describes one domain translation request without exposing provider transport or persistence types.
    /// </summary>
    public sealed class TranslationRequest
    {
        /// <summary>
        /// Creates a request for an existing translation parameter set.
        /// </summary>
        /// <param name="parameters">The units and preprocessing options to translate.</param>
        /// <param name="from">The source language.</param>
        /// <param name="to">The target language.</param>
        /// <param name="aiParameter">The optional provider-neutral AI instruction.</param>
        public TranslationRequest(TransParam parameters, Languages from, Languages to, string aiParameter)
        {
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            From = from;
            To = to;
            AIParameter = aiParameter;
        }

        /// <summary>Gets the units and preprocessing options to translate.</summary>
        public TransParam Parameters { get; }

        /// <summary>Gets the source language.</summary>
        public Languages From { get; }

        /// <summary>Gets the target language.</summary>
        public Languages To { get; }

        /// <summary>Gets the optional provider-neutral AI instruction.</summary>
        public string AIParameter { get; }
    }

    /// <summary>
    /// Translates domain requests independently from scheduling and persistence policy.
    /// </summary>
    public interface ITranslationProvider
    {
        /// <summary>
        /// Translates one request and distinguishes cancellation through <see cref="OperationCanceledException"/>.
        /// </summary>
        /// <param name="request">The provider-neutral translation request.</param>
        /// <param name="cancellationToken">The token that cancels provider work.</param>
        /// <returns>A task containing the translated units.</returns>
        Task<UnitGroup> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Persists and retrieves translated domain results without exposing a storage implementation.
    /// </summary>
    public interface ITranslationStore
    {
        /// <summary>
        /// Attempts to retrieve a previously stored translation.
        /// </summary>
        /// <param name="request">The request used as the storage key.</param>
        /// <param name="result">Receives the stored result when available.</param>
        /// <returns><c>true</c> when a result was found; otherwise, <c>false</c>.</returns>
        bool TryGet(TranslationRequest request, out UnitGroup result);

        /// <summary>
        /// Stores a completed translation.
        /// </summary>
        /// <param name="request">The request used as the storage key.</param>
        /// <param name="result">The translated units to persist.</param>
        void Store(TranslationRequest request, UnitGroup result);
    }

    /// <summary>
    /// Applies execution and cancellation policy to translation requests.
    /// </summary>
    public interface ITranslationScheduler
    {
        /// <summary>
        /// Executes requests using the supplied provider and store.
        /// </summary>
        /// <param name="requests">The requests to execute in their required result order.</param>
        /// <param name="provider">The provider that performs missing translations.</param>
        /// <param name="store">The store used for cache lookup and persistence.</param>
        /// <param name="cancellationToken">The token that cancels scheduled work.</param>
        /// <returns>A task containing results in request order.</returns>
        Task<IReadOnlyList<UnitGroup>> ExecuteAsync(
            IReadOnlyList<TranslationRequest> requests,
            ITranslationProvider provider,
            ITranslationStore store,
            CancellationToken cancellationToken);
    }
}
