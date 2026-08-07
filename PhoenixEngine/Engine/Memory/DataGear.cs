using System;
using System.Collections.Generic;

namespace PhoenixEngine.Memory
{
    public class P_Link<T> where T : new()
    {
        private T Value;
        private P_Link<T> Head = null;
        public P_Link<T> Next = null;
        public P_Link<T> Prev = null;
        private P_Link<T> Tail = null;

        public P_Link<T> GetTail()
        {
            return Tail ?? this;
        }
        public P_Link<T> GetHead()
        {
            return Head ?? this;
        }
        public void Remove()
        {
            if (Prev == null && Next == null && Head == null && Tail == null)
                return;

            if (Prev != null)
                Prev.Next = Next;

            if (Next != null)
                Next.Prev = Prev;

            if (Head != null && this == Head.Tail)
                Head.Tail = Prev;

            if (Head == null && Next != null)
            {
                Next.Head = null;
                Next.Tail = Tail;

                var Node = Next.Next;
                while (Node != null)
                {
                    Node.Head = Next;
                    Node = Node.Next;
                }
            }

            Prev = null;
            Next = null;
            Head = null;
            Tail = null;
        }

        public bool HaveValue()
        {
            if (Tail != null)
            {
                return true;
            }
            return false;
        }
        public P_Link<T> SetValue(T Value)
        {
            if (this.Value == null)
            {
                this.Value = Value;
                Tail = this;
                Head = null;

                return this;
            }
            else
            {
                var Head = GetHead();

                var NewNode = new P_Link<T>
                {
                    Value = Value,
                    Prev = Head.Tail,
                    Head = Head
                };

                Head.Tail.Next = NewNode;
                Head.Tail = NewNode;

                return NewNode;
            }
        }
        public T GetValueByIndex(int Index)
        {
            var Node = GetHead();
            int i = 0;
            while (Node != null)
            {
                if (i == Index)
                    return Node.Value;
                Node = Node.Next;
                i++;
            }
            return new T();
        }

        public T GetValueFromTail(int IndexFromTail)
        {
            var Node = GetHead().Tail;
            int i = 0;
            while (Node != null)
            {
                if (i == IndexFromTail)
                {
                    return Node.Value;
                }
                Node = Node.Prev;
                i++;
            }
            return new T();
        }
        public int Count()
        {
            int Count = 0;
            var Node = GetHead();
            while (Node != null)
            {
                Count++;
                Node = Node.Next;
            }
            return Count;
        }
        public void ForEachForward(Action<P_Link<T>> Action)
        {
            var Node = GetHead();
            while (Node != null)
            {
                if (Node != null)
                    Action(Node);

                Node = Node.Next;
            }
        }
        public void ForEachBackward(Action<P_Link<T>> Action)
        {
            var Head = GetHead();
            var Node = Head.Tail;

            while (Node != null)
            {
                Action(Node);
                Node = Node.Prev;
            }
        }
        public List<P_Link<T>> GetNodesBefore()
        {
            var Result = new List<P_Link<T>>();
            var Node = Prev;
            while (Node != null)
            {
                Result.Insert(0, Node);
                Node = Node.Prev;
            }
            return Result;
        }
        public List<P_Link<T>> GetNodesAfter()
        {
            var Result = new List<P_Link<T>>();
            var Node = Next;
            while (Node != null)
            {
                Result.Add(Node);
                Node = Node.Next;
            }
            return Result;
        }
    }
    public class P_String : IEquatable<P_String>
    {
        public string String;
        public uint Type = 0;

        public P_String(string String, uint Type)
        {
            this.String = String;
            this.Type = Type;
        }

        public bool Equals(P_String Other)
        {
            if (Other is null) return false;
            return Type == Other.Type && string.Equals(String, Other.String, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as P_String);

        public override int GetHashCode()
        {
            unchecked
            {
                int Hash = 17;
                Hash = Hash * 31 + (String != null ? StringComparer.Ordinal.GetHashCode(String) : 0);
                Hash = Hash * 31 + Type.GetHashCode();
                return Hash;
            }
        }
    } 
    public class P_Dict<TKey, TValue>
    {
        private object GlobalLock = new object();
        private Dictionary<TKey, int> DictData = new Dictionary<TKey, int>();
        private Dictionary<TValue, int> CacheDict = new Dictionary<TValue, int>();
        private List<TValue> CacheList = new List<TValue>();
        private List<int> CacheRefCount = new List<int>();
        private int DeadCount = 0;

        // Reserved callback for future use. Triggered when a value changes to record history operations.
        public Action<TKey, TValue, TValue> OnValueChanged;

        public int CompactThreshold = 1000;

        public int Count
        {
            get { lock (GlobalLock) return DictData.Count; }
        }

        private int AddData(TValue Data)
        {
            if (CacheDict.TryGetValue(Data, out var Index))
                return Index;
            Index = CacheList.Count;
            CacheList.Add(Data);
            CacheDict.Add(Data, Index);
            CacheRefCount.Add(0);
            return Index;
        }

        public bool IsUnique(TKey Key)
        {
            lock (GlobalLock)
            {
                if (!DictData.TryGetValue(Key, out var Index))
                    return false;
                return CacheRefCount[Index] == 1;
            }
        }

        public TValue this[TKey Key]
        {
            get
            {
                lock (GlobalLock)
                {
                    if (!DictData.TryGetValue(Key, out var Index))
                        return default;
                    return CacheList[Index];
                }
            }
            set
            {
                lock (GlobalLock)
                {
                    if (DictData.TryGetValue(Key, out var OldIndex))
                    {
                        TValue OldValue = CacheList[OldIndex];
                        if (EqualityComparer<TValue>.Default.Equals(OldValue, value))
                            return;

                        if (value is P_String && OldValue is P_String)
                        {
                            OnValueChanged?.Invoke(Key, OldValue, value);
                        }

                        if (CacheRefCount[OldIndex] == 1 && !CacheDict.ContainsKey(value))
                        {
                            CacheDict.Remove(OldValue);
                            CacheList[OldIndex] = value;
                            CacheDict[value] = OldIndex;
                            return;
                        }
                        CacheRefCount[OldIndex]--;
                        if (CacheRefCount[OldIndex] <= 0)
                            RegisterDead();
                    }
                    int NewIndex = AddData(value);
                    DictData[Key] = NewIndex;
                    CacheRefCount[NewIndex]++;
                }
            }
        }

        public void CheckLinks(Action<TKey, TValue, bool> LinkCheck)
        {
            if (LinkCheck == null) return;
            Dictionary<TKey, int> SnapshotDict;
            List<TValue> SnapshotCache;
            List<int> SnapshotRef;
            lock (GlobalLock)
            {
                SnapshotDict = new Dictionary<TKey, int>(DictData);
                SnapshotCache = new List<TValue>(CacheList);
                SnapshotRef = new List<int>(CacheRefCount);
            }
            foreach (var KV in SnapshotDict)
            {
                TValue Value = SnapshotCache[KV.Value];
                bool Unique = SnapshotRef[KV.Value] == 1;
                LinkCheck.Invoke(KV.Key, Value, Unique);
            }
        }

        public void Add(TKey Key, TValue Value)
        {
            lock (GlobalLock)
            {
                int Index = AddData(Value);
                DictData.Add(Key, Index);
                CacheRefCount[Index]++;
            }
        }

        public void Clear()
        {
            lock (GlobalLock)
            {
                DictData.Clear();
                CacheDict.Clear();
                CacheList.Clear();
                CacheRefCount.Clear();
                DeadCount = 0;
            }
        }

        public void Remove(TKey Key)
        {
            lock (GlobalLock)
            {
                if (!DictData.TryGetValue(Key, out var Index)) return;
                DictData.Remove(Key);
                CacheRefCount[Index]--;
                if (CacheRefCount[Index] <= 0)
                    RegisterDead();
            }
        }

        private void RegisterDead()
        {
            DeadCount++;
            if (CompactThreshold > 0 && DeadCount >= CompactThreshold)
            {
                DeadCount = 0;
                CompactInternal();
            }
        }

        public void Compact()
        {
            lock (GlobalLock)
            {
                CompactInternal();
            }
        }
        private void CompactInternal()
        {
            var NewCacheList = new List<TValue>();
            var NewCacheDict = new Dictionary<TValue, int>();
            var NewCacheRefCount = new List<int>();
            var IndexRemap = new Dictionary<int, int>();

            for (int I = 0; I < CacheList.Count; I++)
            {
                if (CacheRefCount[I] <= 0) continue;

                int NewIndex = NewCacheList.Count;
                NewCacheList.Add(CacheList[I]);
                NewCacheDict[CacheList[I]] = NewIndex;
                NewCacheRefCount.Add(CacheRefCount[I]);
                IndexRemap[I] = NewIndex;
            }

            var Keys = new List<TKey>(DictData.Keys);
            foreach (var Key in Keys)
                DictData[Key] = IndexRemap[DictData[Key]];

            CacheList = NewCacheList;
            CacheDict = NewCacheDict;
            CacheRefCount = NewCacheRefCount;
            DeadCount = 0;
        }
    }
}
