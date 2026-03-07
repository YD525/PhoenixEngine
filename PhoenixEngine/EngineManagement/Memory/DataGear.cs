using System;
using System.Collections.Generic;

namespace PhoenixEngine.EngineManagement
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
    public class P_Dict<TKey, TValue>
    {
        private object DictLock = new object();
        private object CacheLock = new object();

        private Dictionary<TKey, int> DictData = new Dictionary<TKey, int>();
        private Dictionary<TValue, int> CacheDict = new Dictionary<TValue, int>();
        private List<TValue> CacheList = new List<TValue>();
        private int AddData(TValue Data)
        {
            lock (CacheLock)
            {
                if (CacheDict.TryGetValue(Data, out var Index))
                    return Index;

                Index = CacheList.Count;
                CacheList.Add(Data);
                CacheDict.Add(Data, Index);
                return Index;
            }
        }
        public TValue this[TKey Key]
        {
            get
            {
                int Index;
                lock (DictLock)
                {
                    if (!DictData.TryGetValue(Key, out Index))
                        return default;
                }

                lock (CacheLock)
                {
                    return CacheList[Index];
                }
            }
            set
            {
                int Index = AddData(value);

                lock (DictLock)
                {
                    DictData[Key] = Index;
                }
            }
        }
        public void CheckLinks(Action<TKey, TValue> LinkCheck)
        {
            if (LinkCheck == null) return;

            Dictionary<TKey, int> SnapshotDict;
            List<TValue> SnapshotCache;

            lock (DictLock)
                SnapshotDict = new Dictionary<TKey, int>(DictData);

            lock (CacheLock)
                SnapshotCache = new List<TValue>(CacheList);

            foreach (var KV in SnapshotDict)
            {
                TValue Value = SnapshotCache[KV.Value];
                LinkCheck.Invoke(KV.Key, Value);
            }
        }
        public void Add(TKey Key, TValue Value)
        {
            int Index = 0;

            lock (CacheLock)
                Index = AddData(Value);

            lock (DictLock)
                DictData.Add(Key, Index);
        }
        public void Clear()
        {
            lock (DictLock)
                DictData.Clear();
            lock (CacheLock)
            {
                CacheDict.Clear();
                CacheList.Clear();
            }
        }
    }
    public class LinkTest
    {
        public void Test()
        {
            P_Dict<string, string> SetLink = new P_Dict<string, string>();

            SetLink.Add("1", "AAA");
            SetLink.Add("2", "AAA");

            SetLink["1212"] = "AAA";

            var CCC = SetLink["13235"];

            //                    //Key
            //var Find = SetLink["XXXXXXXXXXXXXXXXXX"];
            //              //FileName ,   Key
            //Find = SetLink["XXXXXXXXXXXXXXXXXX",""];
        }
    }
}
