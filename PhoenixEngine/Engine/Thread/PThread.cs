using System;
using System.Collections.Generic;
using System.Threading;

namespace PhoenixEngine.PThread
{
    public enum WorkState
    {
        Null = 0, WaitToCreated = 1, Working = 2, WorkEnd = 3
    }

    public static class IdGenerator
    {
        private static readonly int _MachineId;
        private static int _Sequence = 0;
        private static long _LastTimestamp = -1;
        private static readonly object _Lock = new object();
        static IdGenerator()
        {
            _MachineId = (System.Diagnostics.Process.GetCurrentProcess().Id & 0x3FF);
        }
        public static long CreateId(DateTime CurrentTime)
        {
            lock (_Lock)
            {
                long Timestamp = ((DateTimeOffset)CurrentTime).ToUnixTimeMilliseconds();

                if (Timestamp < _LastTimestamp)
                    Timestamp = _LastTimestamp + 1;

                if (Timestamp == _LastTimestamp)
                {
                    _Sequence = (_Sequence + 1) & 0x1FFF;
                    if (_Sequence == 0)
                    {
                        while (Timestamp <= _LastTimestamp)
                            Timestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds();
                    }
                }
                else
                {
                    _Sequence = 0;
                    _LastTimestamp = Timestamp;
                }

                long ID = ((long)Timestamp << 23)
                | (((long)_MachineId & 0x3FF) << 13)
                | ((long)_Sequence & 0x1FFF);

                return ID;
            }
        }
    }

    public class P_ThreadPool<T> where T : class
    {
        private List<Do_Thread<T>> Threads = new List<Do_Thread<T>>();
        public int ConcurrencyLimit = 0;
        public object SyncLock = new object();

        private bool CanPut = true;

        private readonly Timer _CleanTimer;
        private volatile int _LastCleanTime = 0;
        public P_ThreadPool()
        {
            _CleanTimer = new Timer(_ =>
            {
                int Now = Environment.TickCount;
                if (Now - _LastCleanTime < 2000)
                    return;

                _LastCleanTime = Now;

                if (Monitor.TryEnter(SyncLock))
                {
                    try
                    {
                        SyncPool();
                    }
                    finally
                    {
                        Monitor.Exit(SyncLock);
                    }
                }
            }, null, 1000, 1000);
        }
        ~P_ThreadPool()
        {
            try
            {
                if (_CleanTimer != null)
                {
                    _CleanTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    _CleanTimer.Dispose();
                }

                lock (SyncLock)
                {
                    CanPut = false;

                    foreach (var t in Threads)
                        t.Cancel();

                    Threads.Clear();
                }
            }
            catch { }
        }

        public int GetWorkingThreadCount()
        {
            lock (SyncLock)
            {
                int WorkCount = 0;

                for (int i = 0; i < Threads.Count; i++)
                {
                    if (Threads[i].IsWorking())
                        WorkCount++;
                }

                return WorkCount;
            }
        }

        public int GetCount()
        {
            lock (SyncLock)
            {
                return Threads.Count;
            }
        }

        private volatile int _Cleaning = 0;
        public void SyncPool()
        {
            if (Interlocked.Exchange(ref _Cleaning, 1) == 1)
                return;

            try
            {
                lock (SyncLock)
                {
                    if (Threads.Count == 0)
                        return;

                    Threads.RemoveAll(t => t.WorkEnd);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _Cleaning, 0);
            }
        }

        public void DeleteTrdByID(long ID)
        {
            lock (SyncLock)
            {
                for (int i = 0; i < Threads.Count; i++)
                {
                    if (Threads[i].ID == ID)
                    {
                        Threads.RemoveAt(i);
                        return;
                    }
                }
            }
        }

        public bool Put(T Param,Do_Thread<T> ThreadRef, bool Run = true)
        {
            SyncPool();

            if (!CanPut) return false;

            lock (SyncLock)
            {
                _LastCleanTime = Environment.TickCount;

                if (ConcurrencyLimit > 0 &&
                    Threads.Count >= ConcurrencyLimit)
                    return false;

                Threads.Add(ThreadRef);
            }

            if (Run)
                ThreadRef.Do(Param);

            return true;
        }

        public void CloseAll()
        {
            lock (SyncLock)
            {
                foreach (var T in Threads)
                {
                    T.Cancel();
                }

                Threads.Clear();
            }
        }

        public void SuspendAll(bool Check)
        {
            lock (SyncLock)
            {
                for (int i = 0; i < Threads.Count; i++)
                {
                    Threads[i].Suspend(Check);
                }

                CanPut = !Check;
            }
        }
    }

    public class Do_Thread<T>
    where T : class
    {
        private readonly Action<T, CancellationToken, ManualResetEventSlim> _DoAction;
        private readonly Action _CancelAction;

        private readonly object _StateLock = new object();

        private CancellationTokenSource _DoCts = null;
        private Thread _DoThread = null;

        private bool _ForceStarting = false;
        private long _ActiveEpoch = 0;

        public string Name { get; set; }

        public bool WorkEnd { get; private set; }

        private volatile bool Doing;
        private volatile bool Canceling;

        public volatile bool IsSuspended = false;

        public long ID = 0;

        public bool IsWorking()
        {
            return !WorkEnd && Doing;
        }

        public Action<int, string> LogHandler { get; set; }

        public Do_Thread(
            Action<T, CancellationToken, ManualResetEventSlim> DoAction,
            Action CancelAction,
            string Name = null)
        {
            this._DoAction = DoAction;
            this._CancelAction = CancelAction;
            this.Name = Name;
            this.ID = IdGenerator.CreateId(DateTime.Now);
        }

        private readonly ManualResetEventSlim _PauseEvent = new ManualResetEventSlim(true);
        public void Suspend(bool Check)
        {
            IsSuspended = Check;

            if (Check)
            {
                _PauseEvent.Reset();   
            }
            else
            {
                _PauseEvent.Set(); 
            }
        }
        public bool Do(T Param,bool ForceStart = false)
        {
            bool NeedNormalStart = false;

            lock (_StateLock)
            {
                if (Doing || Canceling)
                {
                    if (!ForceStart)
                        return false;

                    if (_ForceStarting)
                        return false;

                    _ForceStarting = true;
                }
                else
                {
                    NeedNormalStart = true;
                }
            }

            if (NeedNormalStart)
                StartDoThread(Param);
            else
                StartForceThread(Param);

            return true;
        }

        public void Cancel()
        {
            Suspend(false);

            CancellationTokenSource LocalCts = null;

            lock (_StateLock)
            {
                if (!Doing || Canceling)
                    return;

                Canceling = true;
                IsSuccess = false;
                WorkEnd = false;
                LocalCts = _DoCts;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    LocalCts?.Cancel();
                    _CancelAction?.Invoke();
                }
                catch (Exception Ex)
                {
                    Log(-1, "Cancel exception: " + Ex.Message);
                }
            });
        }

        public volatile bool IsSuccess;
        private void StartDoThread(T Param)
        {
            long LocalEpoch;

            lock (_StateLock)
            {
                _ActiveEpoch++;
                LocalEpoch = _ActiveEpoch;

                Doing = true;
                Canceling = false;
                WorkEnd = false;

                _DoCts = new CancellationTokenSource();
            }

            CancellationToken Token = _DoCts.Token;

            _DoThread = new Thread(() =>
            {
                CancellationTokenSource DisposeCts = null;

                try
                {
                    _DoAction?.Invoke(Param,Token,_PauseEvent);

                    lock (_StateLock)
                    {
                        if (_ActiveEpoch == LocalEpoch)
                        {
                            IsSuccess = true;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Log(1, "Task canceled");
                    if (_ActiveEpoch == LocalEpoch)
                       IsSuccess = false;
                }
                catch (Exception Ex)
                {
                    Log(-1, "Task execution error: " + Ex.Message);
                    if (_ActiveEpoch == LocalEpoch)
                        IsSuccess = false;
                }
                finally
                {
                    lock (_StateLock)
                    {
                        if (_ActiveEpoch == LocalEpoch)
                        {
                            Doing = false;
                            Canceling = false;
                            WorkEnd = true;
                            DisposeCts = _DoCts;
                            _DoCts = null;
                        }
                    }

                    DisposeCts?.Dispose();
                }
            })
            {
                IsBackground = true,
                Name = Name ?? $"DoItem_{LocalEpoch}"
            };

            _DoThread.Start();
        }

        private void StartForceThread(T Param)
        {
            Thread ForceThread = new Thread(() =>
            {
                try
                {
                    Log(1, "Force start requested");

                    Cancel();

                    int WaitCount = 24;
                    bool Safe = false;

                    while (WaitCount-- > 0)
                    {
                        bool DoingLocal;
                        bool CancelLocal;
                        bool AliveLocal;

                        lock (_StateLock)
                        {
                            DoingLocal = Doing;
                            CancelLocal = Canceling;
                            AliveLocal = _DoThread != null && _DoThread.IsAlive;
                        }

                        if (!DoingLocal && !CancelLocal && !AliveLocal)
                        {
                            Safe = true;
                            break;
                        }

                        Thread.Sleep(500);
                    }

                    if (Safe)
                    {
                        lock (_StateLock)
                        {
                            if (!_ForceStarting)
                                return;
                        }

                        StartDoThread(Param);
                    }
                    else
                    {
                        Log(-1, "Force start failed: task not stopped");
                    }
                }
                catch (Exception Ex)
                {
                    Log(-1, "Force thread error: " + Ex.Message);
                }
                finally
                {
                    lock (_StateLock)
                    {
                        _ForceStarting = false;
                    }
                }
            })
            {
                IsBackground = true,
                Name = "DoItem_ForceScheduler"
            };

            ForceThread.Start();
        }

        private void Log(int level, string message)
        {
            LogHandler?.Invoke(level, message);
        }
    }
}
