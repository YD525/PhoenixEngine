using System;
using System.Collections.Generic;
using PhoenixEngine.TranslateManage;
using static PhoenixEngine.TranslateManagement.ChunkHelper;

namespace PhoenixEngine.GameManagement
{
    public class SkyrimBookHelper
    {
        public List<UnitChunk> ChunkBook(TranslationUnit Unit)
        {
            //Okay, I just need to take care of this.
            //My real concern is that if the user isn't using local AI, but rather cloud-based AI, SSELex, due to its context-aware generation, might waste a lot of tokens.
            return new List<UnitChunk>();
        }
    }
}
