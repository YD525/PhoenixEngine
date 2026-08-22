using System;
using System.Collections.Generic;
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
    /// Verifies cooperative batch lifecycle behavior with in-memory translation providers.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public sealed class TranslationTaskTests
    {
        private EngineConfigJson _previousConfig;

        /// <summary>
        /// Isolates process-wide concurrency configuration before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _previousConfig = Phoenix.Config;
            Phoenix.Config = new EngineConfigJson
            {
                MaxThreadCount = 2,
                PlatformConfigs = new Dictionary<int, PlatformConfig>()
            };
        }

        /// <summary>
        /// Restores process-wide concurrency configuration after each test.
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            Phoenix.Config = _previousConfig;
        }

        /// <summary>
        /// Verifies that pause and resume stop publication and scheduling at cooperative checkpoints.
        /// </summary>
        [TestMethod]
        public async Task PausesAndResumesWithoutBlockingWorkerThreadsAsync()
        {
            Phoenix.Config.MaxThreadCount = 1;
            var provider = new PausingProvider();
            TranslatorCore core = CreateCore(provider, CreateGroup("first"), CreateGroup("second"));

            Task run = core.StartAsync();
            await AwaitWithinAsync(provider.FirstStarted.Task);
            core.Stop();
            Assert.AreEqual(TranslationRunState.Paused, core.State);

            provider.ReleaseFirst.SetResult(true);
            await AwaitWithinAsync(provider.FirstCompleted.Task);
            await Task.Delay(100);

            Assert.AreEqual(1, provider.CallCount);
            Assert.AreEqual(0, core.TranslatedQueue.Count);
            core.Keep();
            Assert.AreEqual(TranslationRunState.Running, core.State);

            await AwaitWithinAsync(run);
            Assert.AreEqual(TranslationRunState.Completed, core.State);
            Assert.AreEqual(2, provider.CallCount);
            Assert.AreEqual(2, core.TranslatedQueue.Count);
        }

        /// <summary>
        /// Verifies that cancellation completes within a bound without publishing partial results.
        /// </summary>
        [TestMethod]
        public async Task CancelsWithinBoundAndLeavesQueuesConsistentAsync()
        {
            var provider = new CancellingProvider();
            TranslatorCore core = CreateCore(provider, CreateGroup("cancelled"));

            Task run = core.StartAsync();
            await AwaitWithinAsync(provider.Started.Task);
            bool completed = await core.CancelAsync(TimeSpan.FromSeconds(1));
            await AwaitWithinAsync(run);

            Assert.IsTrue(completed);
            Assert.AreEqual(TranslationRunState.Completed, core.State);
            Assert.IsNull(core.LastError);
            Assert.AreEqual(0, core.GetWorkingThreadCount());
            Assert.AreEqual(0, core.TranslatedQueue.Count);
            Assert.AreEqual(0, core.DequeueCache.Count);

            provider.Release.SetResult(true);
            await AwaitWithinAsync(provider.Completed.Task);
            await Task.Delay(50);
            Assert.AreEqual(0, core.TranslatedQueue.Count);
        }

        /// <summary>
        /// Verifies that provider failures remain observable and distinct from cancellation.
        /// </summary>
        [TestMethod]
        public async Task ExposesProviderFailureSeparatelyFromCancellationAsync()
        {
            var failure = new InvalidOperationException("provider failed");
            TranslatorCore core = CreateCore(new FailingProvider(failure), CreateGroup("failed"));

            InvalidOperationException observed = null;
            try
            {
                await core.StartAsync();
            }
            catch (InvalidOperationException exception)
            {
                observed = exception;
            }

            Assert.AreSame(failure, observed);
            Assert.AreEqual(TranslationRunState.Failed, core.State);
            Assert.AreSame(failure, core.LastError);
            Assert.AreEqual(0, core.TranslatedQueue.Count);
        }

        /// <summary>
        /// Verifies that independent batch runs can execute concurrently without shared thread state.
        /// </summary>
        [TestMethod]
        public async Task ExecutesIndependentRunsInParallelAsync()
        {
            var provider = new ParallelProvider();
            TranslatorCore first = CreateCore(provider, CreateGroup("parallel-first"));
            TranslatorCore second = CreateCore(provider, CreateGroup("parallel-second"));

            Task firstRun = first.StartAsync();
            Task secondRun = second.StartAsync();
            await AwaitWithinAsync(provider.BothStarted.Task);
            Assert.AreEqual(2, provider.MaximumConcurrency);

            provider.Release.SetResult(true);
            await AwaitWithinAsync(Task.WhenAll(firstRun, secondRun));

            Assert.AreEqual(TranslationRunState.Completed, first.State);
            Assert.AreEqual(TranslationRunState.Completed, second.State);
            Assert.AreEqual(1, first.TranslatedQueue.Count);
            Assert.AreEqual(1, second.TranslatedQueue.Count);
        }

        private static TranslatorCore CreateCore(ITranslationProvider provider, params UnitGroup[] groups)
        {
            var translator = new Translator("task-test", Languages.English, Languages.German, false)
            {
                TranslationProvider = provider,
                TranslationScheduler = new SequentialTranslationScheduler()
            };
            TranslatorCore core = translator.GetBatchCore();
            core.Container = new P_BucketContainer(translator, new List<BaseUnit>());
            core.Container.Units.AddRange(groups);
            core.ProcStage = 2;
            return core;
        }

        private static UnitGroup CreateGroup(string key)
        {
            var group = new UnitGroup();
            group.Units.Add(new BaseUnit(1, key, "DIALOGUE", key, string.Empty, string.Empty, 100));
            return group;
        }

        private static async Task AwaitWithinAsync(Task task)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(3)));
            if (completed != task)
            {
                Assert.Fail("The asynchronous test operation did not complete within three seconds.");
            }
            await task;
        }

        private sealed class PausingProvider : ITranslationProvider
        {
            private int _callCount;

            internal PausingProvider()
            {
                FirstStarted = CreateSignal();
                FirstCompleted = CreateSignal();
                ReleaseFirst = CreateSignal();
            }

            internal int CallCount => Volatile.Read(ref _callCount);
            internal TaskCompletionSource<bool> FirstStarted { get; }
            internal TaskCompletionSource<bool> FirstCompleted { get; }
            internal TaskCompletionSource<bool> ReleaseFirst { get; }

            public async Task<UnitGroup> TranslateAsync(
                TranslationRequest request,
                CancellationToken cancellationToken)
            {
                int call = Interlocked.Increment(ref _callCount);
                if (call == 1)
                {
                    FirstStarted.TrySetResult(true);
                    await WaitForSignalAsync(ReleaseFirst.Task, cancellationToken);
                    FirstCompleted.TrySetResult(true);
                }
                SetTranslated(request.Parameters.Data);
                return request.Parameters.Data;
            }
        }

        private sealed class CancellingProvider : ITranslationProvider
        {
            internal CancellingProvider()
            {
                Started = CreateSignal();
                Completed = CreateSignal();
                Release = CreateSignal();
            }

            internal TaskCompletionSource<bool> Started { get; }
            internal TaskCompletionSource<bool> Completed { get; }
            internal TaskCompletionSource<bool> Release { get; }

            public async Task<UnitGroup> TranslateAsync(
                TranslationRequest request,
                CancellationToken cancellationToken)
            {
                Started.TrySetResult(true);
                await Release.Task;
                Completed.TrySetResult(true);
                return request.Parameters.Data;
            }
        }

        private sealed class FailingProvider : ITranslationProvider
        {
            private readonly Exception _failure;

            internal FailingProvider(Exception failure)
            {
                _failure = failure;
            }

            public Task<UnitGroup> TranslateAsync(
                TranslationRequest request,
                CancellationToken cancellationToken)
            {
                return Task.FromException<UnitGroup>(_failure);
            }
        }

        private sealed class ParallelProvider : ITranslationProvider
        {
            private int _active;
            private int _maximumConcurrency;

            internal ParallelProvider()
            {
                BothStarted = CreateSignal();
                Release = CreateSignal();
            }

            internal int MaximumConcurrency => Volatile.Read(ref _maximumConcurrency);
            internal TaskCompletionSource<bool> BothStarted { get; }
            internal TaskCompletionSource<bool> Release { get; }

            public async Task<UnitGroup> TranslateAsync(
                TranslationRequest request,
                CancellationToken cancellationToken)
            {
                int active = Interlocked.Increment(ref _active);
                UpdateMaximum(active);
                if (active == 2)
                {
                    BothStarted.TrySetResult(true);
                }
                try
                {
                    await WaitForSignalAsync(Release.Task, cancellationToken);
                    SetTranslated(request.Parameters.Data);
                    return request.Parameters.Data;
                }
                finally
                {
                    Interlocked.Decrement(ref _active);
                }
            }

            private void UpdateMaximum(int value)
            {
                int current;
                do
                {
                    current = Volatile.Read(ref _maximumConcurrency);
                    if (value <= current)
                    {
                        return;
                    }
                }
                while (Interlocked.CompareExchange(ref _maximumConcurrency, value, current) != current);
            }
        }

        private static TaskCompletionSource<bool> CreateSignal()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static async Task WaitForSignalAsync(Task signal, CancellationToken cancellationToken)
        {
            var cancellationSignal = CreateSignal();
            using (cancellationToken.Register(() => cancellationSignal.TrySetCanceled()))
            {
                Task completed = await Task.WhenAny(signal, cancellationSignal.Task);
                await completed;
            }
        }

        private static void SetTranslated(UnitGroup group)
        {
            foreach (BaseUnit unit in group.Units)
            {
                unit.Translated = "translated:" + unit.Original;
            }
        }
    }
}
