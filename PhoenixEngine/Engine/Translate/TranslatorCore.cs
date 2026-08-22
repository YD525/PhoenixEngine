using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PhoenixEngine.Engine;
using PhoenixEngine.Events;
using PhoenixEngine.PThread;
using PhoenixEngine.Unit;
using static PhoenixEngine.Engine.P_BucketContainer;

namespace PhoenixEngine.Translate
{
    /// <summary>
    /// Describes the observable lifecycle of one batch translation run.
    /// </summary>
    public enum TranslationRunState
    {
        /// <summary>The run has been initialized but has not started.</summary>
        Created = 0,

        /// <summary>The run is scheduling or executing translation work.</summary>
        Running = 1,

        /// <summary>The run is waiting at cooperative pause checkpoints.</summary>
        Paused = 2,

        /// <summary>Cancellation has been requested and outstanding work is stopping.</summary>
        Cancelling = 3,

        /// <summary>The run completed normally or finished cooperative cancellation.</summary>
        Completed = 4,

        /// <summary>The run stopped because a provider or execution component failed.</summary>
        Failed = 5
    }

    /// <summary>
    /// Coordinates batch translation through task-based workers and cooperative lifecycle controls.
    /// </summary>
    public class TranslatorCore
    {
        private static readonly TimeSpan DefaultCancellationTimeout = TimeSpan.FromSeconds(5);
        private readonly object _runLock = new object();
        private AsyncPauseGate _pauseGate = new AsyncPauseGate();
        private CancellationTokenSource _cancelSource;
        private Task _runTask = Task.CompletedTask;
        private Exception _lastError;
        private TranslationRunState _state = TranslationRunState.Created;
        private int _activeWorkerCount;
        private int _runGeneration;

        public P_BucketContainer Container = null;
        public ConcurrentQueue<BaseUnit> TranslatedQueue = new ConcurrentQueue<BaseUnit>();
        public volatile int AutoThreadLimit = 0;
        public volatile bool SkipWordAnalysis = false;
        public Translator TranslatorRef = null;

        /// <summary>
        /// Retains the legacy field for binary compatibility; task-based runs do not populate it.
        /// </summary>
        public P_ThreadPool<UnitGroup> TrdPool = null;

        public readonly object CacheSetGetLock = new object();
        public Dictionary<string, string> DequeueCache = new Dictionary<string, string>();
        public volatile int ProcStage = 0;
        public volatile bool IsStopped = false;
        public volatile bool IsWorking = false;
        public volatile int BaseTranslatedCount = 0;
        public volatile int TranslatedCount = 0;

        /// <summary>
        /// Retains the legacy field for binary compatibility; task-based runs do not create a main thread.
        /// </summary>
        public Thread TransMainTrd = null;

        /// <summary>
        /// Creates a batch translation coordinator for a translator.
        /// </summary>
        /// <param name="SetTranslator">The translator that executes each bucket.</param>
        /// <param name="ClearCache">Whether to clear the translator link cache before initialization.</param>
        /// <exception cref="ArgumentNullException"><paramref name="SetTranslator"/> is <c>null</c>.</exception>
        public TranslatorCore(Translator SetTranslator, bool ClearCache = false)
        {
            TranslatorRef = SetTranslator ?? throw new ArgumentNullException(nameof(SetTranslator));
            if (ClearCache)
            {
                TranslatorRef.ClearCache();
            }
        }

        /// <summary>
        /// Gets the current lifecycle state.
        /// </summary>
        public TranslationRunState State
        {
            get
            {
                lock (_runLock)
                {
                    return _state;
                }
            }
        }

        /// <summary>
        /// Gets the provider or execution failure that ended the latest run.
        /// </summary>
        public Exception LastError
        {
            get
            {
                lock (_runLock)
                {
                    return _lastError;
                }
            }
        }

        /// <summary>
        /// Gets the task representing the current or latest run.
        /// </summary>
        public Task RunTask
        {
            get
            {
                lock (_runLock)
                {
                    return _runTask;
                }
            }
        }

        /// <summary>
        /// Gets the number of translation buckets currently executing.
        /// </summary>
        /// <returns>The active task count.</returns>
        public int GetWorkingThreadCount()
        {
            return Volatile.Read(ref _activeWorkerCount);
        }

        /// <summary>
        /// Gets the number of units in the initialized batch.
        /// </summary>
        /// <returns>The unit count, or zero when no batch is initialized.</returns>
        public int GetCount()
        {
            return Container?.GetCount() ?? 0;
        }

        /// <summary>
        /// Initializes a batch and places its lifecycle in the created state.
        /// </summary>
        /// <param name="BaseUnits">The source units to bucket.</param>
        /// <param name="Addition">The translated-count offset supplied by the consuming application.</param>
        /// <param name="CheckLinksEvent">The optional callback used to establish linked units.</param>
        /// <returns><c>true</c> when initialization succeeds; otherwise, <c>false</c>.</returns>
        public bool Init(List<BaseUnit> BaseUnits, int Addition, CheckLinks CheckLinksEvent)
        {
            if (BaseUnits == null)
            {
                throw new ArgumentNullException(nameof(BaseUnits));
            }

            lock (_runLock)
            {
                if (ProcStage != 0)
                {
                    return false;
                }
            }

            Close();
            TranslatorRef.SyncTranslatedCount(Addition);
            Container = new P_BucketContainer(TranslatorRef, BaseUnits)
            {
                CheckLinksEvent = CheckLinksEvent
            };
            ProcStage = 1;
            Container.Build();
            ProcStage = 2;

            if (Phoenix.Config.MaxThreadCount <= 0)
            {
                Phoenix.Config.MaxThreadCount = 1;
            }

            lock (_runLock)
            {
                _state = TranslationRunState.Created;
                _lastError = null;
            }
            return true;
        }

        /// <summary>
        /// Starts the initialized batch through the compatibility facade.
        /// </summary>
        public void Start()
        {
            TaskCancellation.ObserveFailure(StartAsync());
        }

        /// <summary>
        /// Starts the initialized batch and returns its observable completion task.
        /// </summary>
        /// <returns>A task that completes, cancels cooperatively, or exposes the provider failure.</returns>
        /// <exception cref="InvalidOperationException">
        /// No batch is initialized or the previous run requires reinitialization.
        /// </exception>
        public Task StartAsync()
        {
            lock (_runLock)
            {
                if (_state == TranslationRunState.Running ||
                    _state == TranslationRunState.Paused ||
                    _state == TranslationRunState.Cancelling)
                {
                    return _runTask;
                }
                if (Container == null || ProcStage != 2)
                {
                    throw new InvalidOperationException(
                        "The translation batch must be initialized before it can start.");
                }

                _cancelSource = new CancellationTokenSource();
                _pauseGate = new AsyncPauseGate();
                _lastError = null;
                _state = TranslationRunState.Running;
                IsStopped = false;
                IsWorking = true;
                BaseTranslatedCount = TranslatorRef.CalcTranslatedCount(0);
                int generation = ++_runGeneration;
                P_BucketContainer runContainer = Container;
                CancellationTokenSource runCancellation = _cancelSource;
                AsyncPauseGate runPauseGate = _pauseGate;
                _runTask = Task.Run(
                    () => RunAsync(runContainer, generation, runCancellation, runPauseGate),
                    CancellationToken.None);
                return _runTask;
            }
        }

        /// <summary>
        /// Resumes scheduling and result publication after a cooperative pause.
        /// </summary>
        public void Keep()
        {
            Resume();
        }

        /// <summary>
        /// Pauses scheduling and result publication at the next cooperative checkpoint.
        /// </summary>
        public void Stop()
        {
            Pause();
        }

        /// <summary>
        /// Pauses the active run without suspending or aborting a thread.
        /// </summary>
        public void Pause()
        {
            lock (_runLock)
            {
                if (_state != TranslationRunState.Running)
                {
                    return;
                }
                _pauseGate.Pause();
                IsStopped = true;
                _state = TranslationRunState.Paused;
            }
        }

        /// <summary>
        /// Resumes a cooperatively paused run.
        /// </summary>
        public void Resume()
        {
            lock (_runLock)
            {
                if (_state != TranslationRunState.Paused)
                {
                    return;
                }
                IsStopped = false;
                _state = TranslationRunState.Running;
                _pauseGate.Resume();
            }
        }

        /// <summary>
        /// Requests cooperative cancellation and waits for the default bounded interval.
        /// </summary>
        /// <returns>A task containing <c>true</c> when the run stops within the interval.</returns>
        public Task<bool> CancelAsync()
        {
            return CancelAsync(DefaultCancellationTimeout);
        }

        /// <summary>
        /// Requests cooperative cancellation and waits for a bounded interval.
        /// </summary>
        /// <param name="timeout">The maximum interval to wait for outstanding work.</param>
        /// <returns>A task containing <c>true</c> when the run stops within the interval.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
        public Task<bool> CancelAsync(TimeSpan timeout)
        {
            if (timeout < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }

            Task runTask;
            CancellationTokenSource cancellation;
            lock (_runLock)
            {
                if (_state == TranslationRunState.Created ||
                    _state == TranslationRunState.Completed ||
                    _state == TranslationRunState.Failed)
                {
                    return Task.FromResult(true);
                }

                _state = TranslationRunState.Cancelling;
                IsStopped = false;
                _pauseGate.Resume();
                cancellation = _cancelSource;
                runTask = _runTask;
            }

            try
            {
                cancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The run completed and disposed its token between the state snapshot and cancellation request.
            }
            return WaitForCancellationAsync(runTask, timeout);
        }

        /// <summary>
        /// Removes and returns the next completed unit without blocking.
        /// </summary>
        /// <param name="IsEnd">Receives whether the run is terminal and no more results remain.</param>
        /// <returns>The next translated unit, or <c>null</c> when none is currently available.</returns>
        public BaseUnit DequeueTranslated(out bool IsEnd)
        {
            if (TranslatedQueue.TryDequeue(out BaseUnit item))
            {
                Interlocked.Increment(ref TranslatedCount);
                lock (CacheSetGetLock)
                {
                    DequeueCache[item.GetRealOriginal()] = item.Translated;
                }
                IsEnd = false;
                return item;
            }

            TranslationRunState state = State;
            IsEnd = (state == TranslationRunState.Completed || state == TranslationRunState.Failed) &&
                GetWorkingThreadCount() == 0;
            return null;
        }

        /// <summary>
        /// Cancels the current run and resets this coordinator for reinitialization.
        /// </summary>
        public void Close()
        {
            CancellationTokenSource cancellation;
            lock (_runLock)
            {
                ++_runGeneration;
                cancellation = _cancelSource;
                _cancelSource = null;
                _pauseGate.Resume();
                _pauseGate = new AsyncPauseGate();
                _state = TranslationRunState.Created;
                _lastError = null;
                ProcStage = 0;
                IsStopped = false;
                IsWorking = false;
                TrdPool = null;
                TransMainTrd = null;
            }

            try
            {
                cancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The run completed and disposed its token while the compatibility close path was resetting state.
            }
            lock (CacheSetGetLock)
            {
                DequeueCache.Clear();
            }
            while (TranslatedQueue.TryDequeue(out _))
            {
            }
            Container?.Clear();
            Interlocked.Exchange(ref _activeWorkerCount, 0);
            Interlocked.Exchange(ref TranslatedCount, 0);
            BaseTranslatedCount = 0;
        }

        private async Task RunAsync(
            P_BucketContainer runContainer,
            int generation,
            CancellationTokenSource cancellation,
            AsyncPauseGate pauseGate)
        {
            Exception failure = null;
            CancellationToken cancellationToken = cancellation.Token;
            try
            {
                if (!TrySetStage(generation, 3))
                {
                    return;
                }
                cancellationToken.ThrowIfCancellationRequested();
                TranslatorRef.Core.ResetEngineHealth();
                await ProcessStageAsync(
                    runContainer.Units,
                    false,
                    generation,
                    pauseGate,
                    cancellationToken).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();
                if (!TrySetStage(generation, 5))
                {
                    return;
                }
                await ProcessStageAsync(
                    runContainer.Books,
                    true,
                    generation,
                    pauseGate,
                    cancellationToken).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();
                TrySetStage(generation, 6);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                failure = exception;
                throw;
            }
            finally
            {
                lock (_runLock)
                {
                    if (_runGeneration == generation)
                    {
                        lock (CacheSetGetLock)
                        {
                            DequeueCache.Clear();
                        }
                        _lastError = failure;
                        _state = failure == null
                            ? TranslationRunState.Completed
                            : TranslationRunState.Failed;
                        IsStopped = false;
                        IsWorking = false;
                        ProcStage = 10;
                        _cancelSource = null;
                        Interlocked.Exchange(ref _activeWorkerCount, 0);
                    }
                }
                cancellation.Dispose();
            }
        }

        private async Task ProcessStageAsync(
            IReadOnlyList<UnitGroup> groups,
            bool isBook,
            int generation,
            AsyncPauseGate pauseGate,
            CancellationToken cancellationToken)
        {
            if (groups.Count == 0)
            {
                return;
            }

            var pendingGroups = new ConcurrentQueue<UnitGroup>(groups);
            int workerCount = Math.Min(Math.Max(1, Phoenix.Config.MaxThreadCount), groups.Count);
            var workers = new List<Task>(workerCount);
            for (int index = 0; index < workerCount; index++)
            {
                workers.Add(ProcessQueueAsync(
                    pendingGroups,
                    isBook,
                    generation,
                    pauseGate,
                    cancellationToken));
            }
            await Task.WhenAll(workers).ConfigureAwait(false);
        }

        private async Task ProcessQueueAsync(
            ConcurrentQueue<UnitGroup> pendingGroups,
            bool isBook,
            int generation,
            AsyncPauseGate pauseGate,
            CancellationToken cancellationToken)
        {
            while (pendingGroups.TryDequeue(out UnitGroup group))
            {
                await pauseGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                await ProcessGroupAsync(
                    group,
                    isBook,
                    generation,
                    pauseGate,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task ProcessGroupAsync(
            UnitGroup group,
            bool isBook,
            int generation,
            AsyncPauseGate pauseGate,
            CancellationToken cancellationToken)
        {
            if (!TryBeginWorker(generation))
            {
                return;
            }
            try
            {
                await pauseGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (group == null ||
                    !group.ApplyStateChange(TranslatorRef.ID, UnitTranslationState.Created).CanDo(-1))
                {
                    return;
                }

                UnitGroup translated = await TranslatorRef.TranslateAsync(
                    new TransParam(group, isBook, true),
                    cancellationToken).ConfigureAwait(false);
                await pauseGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (translated != null)
                {
                    TryAddTranslated(generation, cancellationToken, TranslatorRef.ID, translated);
                }
            }
            finally
            {
                EndWorker(generation);
            }
        }

        private bool TryBeginWorker(int generation)
        {
            lock (_runLock)
            {
                if (_runGeneration != generation)
                {
                    return false;
                }
                Interlocked.Increment(ref _activeWorkerCount);
                return true;
            }
        }

        private bool TrySetStage(int generation, int stage)
        {
            lock (_runLock)
            {
                if (_runGeneration != generation)
                {
                    return false;
                }
                ProcStage = stage;
                return true;
            }
        }

        private void EndWorker(int generation)
        {
            lock (_runLock)
            {
                if (_runGeneration == generation)
                {
                    Interlocked.Decrement(ref _activeWorkerCount);
                }
            }
        }

        private void TryAddTranslated(
            int generation,
            CancellationToken cancellationToken,
            string translatorId,
            UnitGroup item)
        {
            lock (_runLock)
            {
                if (_runGeneration != generation ||
                    _state == TranslationRunState.Cancelling ||
                    cancellationToken.IsCancellationRequested ||
                    !item.ApplyStateChange(translatorId, UnitTranslationState.Queued).CanDo(-1))
                {
                    return;
                }
                foreach (BaseUnit unit in item.Units)
                {
                    TranslatedQueue.Enqueue(unit);
                }
            }
        }

        private static async Task<bool> WaitForCancellationAsync(Task runTask, TimeSpan timeout)
        {
            if (runTask.IsCompleted)
            {
                await runTask.ConfigureAwait(false);
                return true;
            }
            Task completed = await Task.WhenAny(runTask, Task.Delay(timeout)).ConfigureAwait(false);
            if (completed != runTask)
            {
                return false;
            }
            await runTask.ConfigureAwait(false);
            return true;
        }
    }

    /// <summary>
    /// Provides a reusable asynchronous checkpoint for cooperative pause and resume.
    /// </summary>
    internal sealed class AsyncPauseGate
    {
        private readonly object _sync = new object();
        private TaskCompletionSource<bool> _resumeSource = CreateCompletedSource();

        /// <summary>
        /// Closes the gate for future checkpoints.
        /// </summary>
        internal void Pause()
        {
            lock (_sync)
            {
                if (_resumeSource.Task.IsCompleted)
                {
                    _resumeSource = CreateSource();
                }
            }
        }

        /// <summary>
        /// Opens the gate and releases current checkpoints.
        /// </summary>
        internal void Resume()
        {
            lock (_sync)
            {
                _resumeSource.TrySetResult(true);
            }
        }

        /// <summary>
        /// Waits for the gate to open or for cancellation without blocking a thread.
        /// </summary>
        /// <param name="cancellationToken">The token that cancels the checkpoint.</param>
        /// <returns>A task representing the checkpoint.</returns>
        internal Task WaitAsync(CancellationToken cancellationToken)
        {
            Task resumeTask;
            lock (_sync)
            {
                resumeTask = _resumeSource.Task;
            }
            if (resumeTask.IsCompleted || !cancellationToken.CanBeCanceled)
            {
                return resumeTask;
            }
            return WaitWithCancellationAsync(resumeTask, cancellationToken);
        }

        private static async Task WaitWithCancellationAsync(
            Task resumeTask,
            CancellationToken cancellationToken)
        {
            var cancellationSource = CreateSource();
            using (cancellationToken.Register(() => cancellationSource.TrySetCanceled()))
            {
                Task completed = await Task.WhenAny(resumeTask, cancellationSource.Task).ConfigureAwait(false);
                await completed.ConfigureAwait(false);
            }
        }

        private static TaskCompletionSource<bool> CreateCompletedSource()
        {
            TaskCompletionSource<bool> source = CreateSource();
            source.SetResult(true);
            return source;
        }

        private static TaskCompletionSource<bool> CreateSource()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
