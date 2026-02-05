using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PhoenixEngine.LanguageDetector
{

    public static class ThaiHelper
    {
        private static readonly string[] ThaiKeywords = new[]
        {
        "และ",   // and
        "ของ",   // of
        "เป็น",   // is / be
        "มี",     // have
        "ไม่",    // not
        "ได้",    // can / got
        "ว่า",    // that
        "ใน",     // in
        "ที่",    // that / which
        "กับ",    // with
        "ก็",     // also
        "จะ",     // will
        "มา",     // come
        "ไป",     // go
        "คน",     // person
        "อะไร",   // what
        "นี่",    // this
        "นั้น"    // that
    };

        private static readonly Regex ThaiCharRegex = new Regex(
            "[\u0E00-\u0E7F]",
            RegexOptions.Compiled);

        /// <summary>
        /// Determines whether the input text is likely Thai
        /// </summary>
        /// <param name="Input">The text to check</param>
        /// <param name="KeywordThreshold">Minimum number of keyword matches required (default 1)</param>
        /// <param name="CharRatioThreshold">Minimum ratio of Thai characters (default 0.3)</param>
        /// <returns>True if the text is probably Thai, otherwise false</returns>
        public static bool IsProbablyThai(
            string Input,
            int KeywordThreshold = 1,
            double CharRatioThreshold = 0.3)
        {
            if (string.IsNullOrWhiteSpace(Input))
                return false;

            int TotalLength = Input.Length;
            if (TotalLength == 0)
                return false;

            int ThaiCharCount = ThaiCharRegex.Matches(Input).Count;
            double ThaiCharRatio = (double)ThaiCharCount / TotalLength;

            int KeywordHits = ThaiKeywords.Count(k => Input.IndexOf(k, StringComparison.Ordinal) >= 0);

            // Core rule:
            // - Enough Thai characters
            // - And at least some Thai keywords
            return ThaiCharRatio >= CharRatioThreshold &&
                   KeywordHits >= KeywordThreshold;
        }

        /// <summary>
        /// Calculates a score indicating the likelihood that the text is Thai
        /// </summary>
        /// <param name="Input">The text to score</param>
        /// <returns>A score representing the likelihood of Thai language</returns>
        public static double GetThaiScore(string Input)
        {
            if (string.IsNullOrWhiteSpace(Input))
                return 0;

            int TotalLength = Input.Length;
            if (TotalLength == 0)
                return 0;

            int ThaiCharCount = ThaiCharRegex.Matches(Input).Count;
            int KeywordHits = ThaiKeywords.Count(k => Input.IndexOf(k, StringComparison.Ordinal) >= 0);

            // Thai chars are much more reliable than keywords
            return (ThaiCharCount * 1.5 + KeywordHits * 2.0) / TotalLength;
        }
    }
}
