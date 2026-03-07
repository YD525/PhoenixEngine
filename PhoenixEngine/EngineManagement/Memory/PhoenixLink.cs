using System;
using System.Collections.Generic;

namespace PhoenixEngine.EngineManagement.Memory
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
    public class P_DictLink<T> where T : new()
    {
        private object QueryLock = new object();
                         //Leader Units
        private Dictionary<int, T> DictData = new Dictionary<int, T>();
        private Dictionary<string,int> DictKeys = new Dictionary<string,int>();

        private int Seed = 0;
        private int ConvertKey(string Key)
        {
            lock (QueryLock)
            {
                if (DictKeys.ContainsKey(Key))
                {
                    return DictKeys[Key];
                }
                else
                {
                    int ConvertKey = Seed++;
                    DictKeys.Add(Key, ConvertKey);
                    return ConvertKey;
                }
            }
        }
        public T this[string Key]
        {
            get
            {
                lock (QueryLock)
                {
                    var IntKey = ConvertKey(Key);

                    if (DictData.TryGetValue(IntKey, out var Value))
                    {
                        return Value;
                    }

                    return new T();
                }
            }
            set
            {
                lock (QueryLock)
                {
                    var IntKey = ConvertKey(Key);
                    DictData[IntKey] = value;
                }
            }
        }
        public T this[string Key1, string Key2]
        {
            get
            {
                var MergeKey = Key1 + "_" + Key2;

                lock (QueryLock)
                {
                    var IntKey = ConvertKey(MergeKey);

                    if (DictData.TryGetValue(IntKey, out var Value))
                    {
                        return Value;
                    }

                    return new T();
                }
            }
            set
            {
                var MergeKey = Key1 + "_" + Key2;

                lock (QueryLock)
                {
                    var IntKey = ConvertKey(MergeKey);
                    DictData[IntKey] = value;
                }

            }
        }

        public Action<string, T> LinkCheck = null;
        public void CheckLinks()
        {
            if (LinkCheck != null)
            {
                foreach (var GetItem in new Dictionary<int, T>(DictData))
                {
                    string RealKey = "";

                    foreach (var GetKey in new Dictionary<string, int>(DictKeys))
                    {
                        if (GetKey.Value.Equals(GetItem.Key))
                        {
                            RealKey = GetKey.Key;
                            break;
                        }
                    }

                    LinkCheck.Invoke(RealKey,GetItem.Value);
                }
                
            }
        }
        public void Clear()
        {
            lock (QueryLock)
            {
                this.DictData.Clear();
                this.DictKeys.Clear();
                Seed = 0;
            }
        }
    }
    public class LinkTest
    {
        public void Test()
        {
            P_DictLink<LinkTest> SetLink = new P_DictLink<LinkTest>();
                                //Key
            var Find = SetLink["XXXXXXXXXXXXXXXXXX"];
                          //FileName ,   Key
            Find = SetLink["XXXXXXXXXXXXXXXXXX",""];
        }
    }
}
