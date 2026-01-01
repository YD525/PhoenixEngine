using System;
using System.Collections.Generic;
using PhoenixEngine.TranslateManage;
using static PhoenixEngine.GameManagement.SkyrimBookHelper;
using static PhoenixEngine.TranslateManagement.ChunkHelper;

namespace PhoenixEngine.GameManagement
{
    public class SkyrimBookHelper
    {
        public class CheckChar
        {
            public string StartChar = "";
            public string EndChar = "";
            public CheckChar(string Start, string End)
            { 
                this.StartChar = Start;
                this.EndChar = End;
            }
        }

        public List<CheckChar> CheckChars = new List<CheckChar>();

        public bool IsInit = false;
        public void Init()
        {
            CheckChars.Add(new CheckChar("<",">"));
            CheckChars.Add(new CheckChar("[", "]"));

            IsInit = true;
        }
        public List<UnitChunk> ChunkBook(TranslationUnit Unit)
        {
            //Okay, I just need to take care of this.
            //My real concern is that if the user isn't using local AI, but rather cloud-based AI, SSELex, due to its context-aware generation, might waste a lot of tokens.

            if (!IsInit)
            {
                Init();
            }

            string GetBookContent = Unit.SourceText;

            for (int i = 0; i < GetBookContent.Length; i++)
            {
                string GetChar = GetBookContent.Substring(i,1);
            }
           
            return new List<UnitChunk>();
        }
    }
}
