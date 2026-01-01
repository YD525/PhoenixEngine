using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhoenixEngine.TranslateManagement
{
    public class ChunkHelper
    {
        public class UnitChunk
        {
            public string ParentKey = "";
            public string Key = "";
            public bool IsCode = false;
            public string Data = "";
            public int Size = 0;

            public UnitChunk(string ParentKey, string Key, bool IsCode, string Data)
            {
                this.ParentKey = ParentKey;
                this.Key = Key;
                this.IsCode = IsCode;
                this.Data = Data;
            }
        }


    }
}
