using System.Text.RegularExpressions;

namespace PhoenixEngine.Language
{
    internal static class EnglishHelper
    {
        /// <summary>
        /// Determines whether the input string is likely to be English text,
        /// containing only English letters, digits, whitespace, and punctuation.
        /// Excludes accented characters to improve accuracy.
        /// </summary>
        /// <param name="Input">Input text to check</param>
        /// <returns>True if input is likely English; otherwise false.</returns>
        public static bool IsProbablyEnglish(string Input)
        {
            if (string.IsNullOrWhiteSpace(Input))
                return false;

            return !Regex.IsMatch(Input, @"\p{M}") && Regex.IsMatch(Input, @"^[\p{L}\p{N}\s\p{P}]+$");
        }
    }
}
