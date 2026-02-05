using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using PhoenixEngine.TranslateManage;
using PhoenixEngine.TranslateManagement;
using static PhoenixEngine.GameManagement.SkyrimBookHelper;
using static PhoenixEngine.TranslateManagement.ChunkHelper;

namespace PhoenixEngine.GameManagement
{
    public class SkyrimBookHelper
    {
        public static bool IsSkyrimBook(TranslationUnit Item, ref Game DetectGame)
        {
            if (Item.Type == "BOOK" && Item.Key.EndsWith("DESC"))
            {
                return true;
            }

            return false;
        }

        public class CheckChar
        {
            public List<string> Chars = new List<string>();
            public CheckChar(string Start, string End)
            { 
                this.Chars.Add(Start);
                this.Chars.Add(End);
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

        public CheckChar CheckCode(string Char)
        {
            for (int i = 0; i < this.CheckChars.Count; i++)
            {
                if (
                    this.CheckChars[i].Chars[0].Equals(Char) ||
                    this.CheckChars[i].Chars[1].Equals(Char)
                   )
                {
                    return this.CheckChars[i];
                }
            }
            return null;
        }
        public List<UnitChunk> ChunkBook(TranslationUnit Unit)
        {
            //Okay, I just need to take care of this.
            //My real concern is that if the user isn't using local AI, but rather cloud-based AI, SSELex, due to its context-aware generation, might waste a lot of tokens.

            List<UnitChunk> UnitChunks = new List<UnitChunk>();

            if (!IsInit)
            {
                Init();
            }

            CheckChar LastSetChar = null;

            string TempText = "";

            string GetBookContent = Unit.SourceText;

            int Block = 0;

            for (int i = 0; i < GetBookContent.Length; i++)
            {
                string GetChar = GetBookContent.Substring(i,1);

                TempText = TempText + GetChar;

                var Check = CheckCode(GetChar);

                if (Check != null && TempText.Length > 0)
                {
                    if (Check.Chars[0].Equals(GetChar))
                    {
                        LastSetChar = Check;
                    }
                    else
                    if (Check.Chars[1].Equals(GetChar) && LastSetChar != null)
                    {
                        if (LastSetChar.Chars.Contains(GetChar))
                        {
                            Block++;
                            UnitChunks.Add(new UnitChunk(Unit.Key, Unit.Key + "_" + Block, true, TempText));
                            TempText = string.Empty;
                            LastSetChar = null;
                        }
                    }
                }

                if ((GetChar.Equals("\r") || GetChar.Equals("\n")) && TempText.Length > 0)
                {
                    if (i + 1 < GetBookContent.Length && GetBookContent[i + 1] == '\n' && GetChar.Equals("\r"))
                    {
                        i++;
                    }

                    Block++;
                    UnitChunks.Add(new UnitChunk(Unit.Key, Unit.Key + "_" + Block, false, TempText));
                    TempText = string.Empty;
                }
            }

            if (TempText.Length > 0)
            {
                Block++;
                UnitChunks.Add(new UnitChunk(Unit.Key, Unit.Key + "_" + Block, false, TempText));
                TempText = string.Empty;
            }

            return UnitChunks;
        }
    }
}
