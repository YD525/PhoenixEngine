using System;
using System.Collections.Generic;
using PhoenixEngine.TranslateManage;
using static PhoenixEngine.TranslateManagement.ChunkHelper;

namespace PhoenixEngine.GameManagement
{
    public class SkyrimBookHelper
    {
        public class CheckChar
        {
            public string Char = "";
            public bool IsStartOrEnd = false;
            public CheckChar(string Char, bool IsStartOrEnd)
            { 
                this.Char = Char;
                this.IsStartOrEnd = IsStartOrEnd;
            }
        }

        public List<CheckChar> CheckChars = new List<CheckChar>();

        public bool IsInit = false;
        public void Init()
        {
            IsInit = true;
        }
        public List<UnitChunk> ChunkBook(TranslationUnit Unit)
        {
            if (!IsInit)
            {
                Init();
            }
            //Okay, I just need to take care of this.
            //My real concern is that if the user isn't using local AI, but rather cloud-based AI, SSELex, due to its context-aware generation, might waste a lot of tokens.
            return new List<UnitChunk>();
        }
    }
}
