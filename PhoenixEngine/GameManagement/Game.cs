using PhoenixEngine.Engine;
using PhoenixEngine.Unit;
using System.Collections.Generic;

namespace PhoenixEngine.GameManagement
{
    public enum P_Game
    { 
       Null=0,Skyrim = 1
    }

    #region Skyrim

    public class P_Skyrim
    {
        public static bool IsBookContent(BaseUnit Item, ref P_Game DetectGame)
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
            CheckChars.Add(new CheckChar("<", ">"));
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
        public List<UnitChunk> ChunkBook(BaseUnit Unit)
        {
            List<UnitChunk> UnitChunks = new List<UnitChunk>();
            if (!IsInit)
            {
                Init();
            }
            CheckChar LastSetChar = null;
            string TempText = "";
            string GetBookContent = Unit.Original;
            int Block = 0;
            for (int i = 0; i < GetBookContent.Length; i++)
            {
                string GetChar = GetBookContent.Substring(i, 1);
                TempText = TempText + GetChar;
                var Check = CheckCode(GetChar);
                if (Check != null && TempText.Length > 0)
                {
                    if (Check.Chars[0].Equals(GetChar))
                    {
                        string Preceding = TempText.Substring(0, TempText.Length - 1);
                        if (Preceding.Length > 0)
                        {
                            Block++;
                            UnitChunks.Add(new UnitChunk(Unit.Key, Unit.Key + "_" + Block, false, Preceding));
                        }
                        TempText = GetChar;
                        LastSetChar = Check;
                    }
                    else if (Check.Chars[1].Equals(GetChar) && LastSetChar != null)
                    {
                        Block++;
                        UnitChunks.Add(new UnitChunk(Unit.Key, Unit.Key + "_" + Block, true, TempText));
                        TempText = string.Empty;
                        LastSetChar = null;
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

    #endregion
}
