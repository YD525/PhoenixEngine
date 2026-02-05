using System;
using System.Text.RegularExpressions;
using System.Xml;
using PhoenixEngine.TranslateManage;

namespace PhoenixEngine.TranslateManagement
{
    public class TranslationPreprocessorExtend
    {
        public static void UnifiedSymbols(Translator Translator, string GetKey, string TransData)
        {
            string NewStr = TransData;

            new TranslationPreprocessor().NormalizePunctuation(ref NewStr);

            if (Regex.Replace(NewStr, @"\s+", "").Length > 0)
            {
                Translator.TransData[GetKey] = NewStr;
            }
            else
            {
                Translator.TransData[GetKey] = string.Empty;
            }
        }
        public static string FormatStr(string Content)
        {
            new TranslationPreprocessor().OptimizeStrings(ref Content);
            return Content;
        }
        public string ReturnStr(string Str)
        {
            if (string.IsNullOrWhiteSpace(Str.Replace("　", "").Replace(" ", "")))
            {
                return string.Empty;
            }
            else
            {
                return Str;
            }
        }

        public bool IsProbablyString(string str)
        {
            if (string.IsNullOrEmpty(str))
                return false;

            int zeroCount = 0;
            foreach (char c in str)
            {
                if (c == '\0')
                    zeroCount++;
            }

            if (zeroCount > str.Length / 4)
                return false;

            int printable = 0;
            int scanned = 0;

            foreach (char c in str)
            {
                if (c == '\0') break;
                scanned++;

                if ((c >= 0x20 && c <= 0x7E) || c == '\n' || c == '\r' || c == '\t' || c >= 0x80)
                {
                    printable++;
                }
            }

            if (printable == 0)
                return false;

            if (printable * 2 < scanned)
                return false;

            bool IsHexChar(char c) =>
                char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

            int hexLike = 0;
            foreach (char c in str.Substring(0, scanned))
            {
                if (IsHexChar(c) || c == '-')
                    hexLike++;
            }

            if (hexLike == scanned)
                return false;

            return true;
        }
        public bool HasUnicodeEscape(string Text)
        {
            return Regex.IsMatch(Text, @"\\u[0-9a-fA-F]{4}");
        }

        /// <summary>
        /// Remove invisible characters, convert full-width characters to half-width characters, and remove certain special symbols.
        /// </summary>
        /// <returns></returns>
        public void OptimizeStrings(ref string Input)
        {
            NormalizePunctuation(ref Input);
            RemoveInvisibleCharacters(ref Input);
            //ConditionalSplitCamelCase(ref Input);
            ProcessEscapeCharacters(ref Input);
            ProcessEmptyEndLine(ref Input);
        }
        public bool IsNullOrEmpty(string Input)
        {
            if (Input == null)
            {
                return true;
            }
            if (Input.Trim().Length == 0)
            {
                return true;
            }

            return false;
        }
        public bool IsNumeric(string Input)
        {
            if (string.IsNullOrWhiteSpace(Input))
                return false;

            return double.TryParse(Input.Trim(), out _);
        }

        /// <summary>
        /// Removes common invisible Unicode characters from the input string.
        /// These include zero-width spaces, non-breaking spaces, and similar hidden characters
        /// that might interfere with text processing.
        /// </summary>
        /// <param name="Input">The string to be cleaned (passed by reference).</param>
        public void RemoveInvisibleCharacters(ref string Input)
        {
            if (string.IsNullOrEmpty(Input))
            {
                return;
            }
            //Remove common "invisible" characters
            var InvisibleChars = new[] { '\u200B', '\u200C', '\u200D', '\uFEFF', '\u00A0', '\u200b' };
            foreach (var Char in InvisibleChars)
            {
                Input = Input.Replace(Char.ToString(), "");
            }

            Input = Input.Replace(@"\t", "");
        }

        /// <summary>
        /// Trims trailing newline characters from the translated text.
        /// Removes either CRLF ("\r\n") or LF ("\n") at the end of the string.
        /// </summary>
        /// <param name="TransText">The translated text to process (passed by reference).</param>
        public void ProcessEmptyEndLine(ref string TransText)
        {
            TransText = Regex.Replace(TransText, @"((\r\n)|\n|\\n)+$", "");
        }

        /// <summary>
        /// Normalizes Chinese punctuation marks to their standard English equivalents.
        /// This ensures consistency in translated output, especially when targeting English text.
        /// </summary>
        /// <param name="Str">The string to normalize (passed by reference).</param>
        public void NormalizePunctuation(ref string Str)
        {
            Str = Str.Replace("（", "(");
            Str = Str.Replace("）", ")");
            Str = Str.Replace("【", "[");
            Str = Str.Replace("】", "]");
            Str = Str.Replace("《", "<");
            Str = Str.Replace("》", ">");
            Str = Str.Replace("｛", "{");
            Str = Str.Replace("｝", "}");
            Str = Str.Replace("［", "[");
            Str = Str.Replace("］", "]");
            Str = Str.Replace("‘", "'");
            Str = Str.Replace("’", "'");
            Str = Str.Replace("“", "\"");
            Str = Str.Replace("”", "\"");
            Str = Str.Replace("＂", "\""); 
            Str = Str.Replace("。", ".");
            Str = Str.Replace("，", ",");
            Str = Str.Replace("：", ":");
            Str = Str.Replace("；", ";");
            Str = Str.Replace("？", "?");
            Str = Str.Replace("！", "!");
            Str = Str.Replace("、", ",");
            Str = Str.Replace("·", ".");
            Str = Str.Replace("——", "--");
            Str = Str.Replace("—", "-");
            Str = Str.Replace("…", "...");
            Str = Str.Replace("　", " ");
        }

        public void StripOuterQuotes(ref string Input)
        {
            if (Input.Trim().Length == 0)
            {
                return;
            }
            int Start = 0;
            while (Start < Input.Length && (Input[Start] == '\\' || Input[Start] == '/' || Input[Start] == '"' || Input[Start] == '“' || Input[Start] == '”'))
            {
                Start++;
            }

            int End = Input.Length - 1;
            while (End >= Start && (Input[End] == '"' || Input[End] == '“' || Input[End] == '”'))
            {
                End--;
            }

            Input = Input.Substring(Start, End - Start + 1);
        }

        public bool HasOuterQuotes(string Input)
        {
            if (string.IsNullOrEmpty(Input) || Input.Length < 2)
                return false;

            char First = Input[0];
            char Last = Input[Input.Length - 1];

            return (IsQuote(First) && IsQuote(Last));
        }

        public bool IsOnlySymbolsAndSpaces(string Input)
        {
            return Regex.IsMatch(Input, @"^[\p{P}\p{S}\s]+$");
        }

        static bool IsQuote(char c)
        {
            return c == '"' || c == '“' || c == '”';
        }

        //public static void ConditionalSplitCamelCase(ref string Input)
        //{
        //    if (string.IsNullOrWhiteSpace(Input))
        //        return;

        //    if (!Input.Contains(" "))
        //    {
        //        Input = Regex.Replace(Input, @"([a-z])([A-Z])", "$1 $2");
        //        Input = Regex.Replace(Input, @"([a-zA-Z])([0-9])", "$1 $2");
        //        Input = Regex.Replace(Input, @"\s+", " ");

        //        Input = Input.Trim();
        //    }

        //    return;
        //}

        public static void ProcessEscapeCharacters(ref string Input)
        {
            Input = Regex.Replace(Input, @"\\n", "\n");
            Input = Regex.Replace(Input, @"\\t", "\t");
            Input = Regex.Replace(Input, @"\\r", "\r");
            Input = Regex.Replace(Input, @"\\b", "\b");
            Input = Regex.Replace(Input, @"\\f", "\f");
            Input = Regex.Replace(Input, @"\\""", "\"");
            Input = Regex.Replace(Input, @"\\'", "'");
            Input = Regex.Replace(Input, @"\\\\", "\\");       
        }

        public static bool IsValidTranslation(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            if (text.Contains("\uFFFD") || text.Contains("�"))
            {
                return false;
            }

            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsSymbol(c) || char.IsWhiteSpace(c))
                    continue;

                if (c == '_' || c == '(' || c == ')') continue;

                return false;
            }

            if (text.Contains(@"\u")) return false;

            return true;
        }

    }
}
