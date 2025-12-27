using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PhoenixEngine.LanguageDetector
{
    public static class PersianHelper
    {
        // Common Persian function words / particles
        private static readonly string[] PersianKeywords = new[]
        {
            "و",       // and
            "در",      // in / at
            "به",      // to
            "از",      // from / of
            "این",     // this
            "آن",      // that
            "برای",    // for
            "که",      // that
            "با",      // with
            "یک",      // a / one
            "را",      // object marker
            "است",     // is / be
            "می",      // present tense marker
            "شد",      // past tense marker
            "بر",      // on / over
            "هم",      // also / too
            "تا"       // until
        };

        // Persian Unicode block: U+0600 – U+06FF
        private static readonly Regex PersianCharRegex = new Regex(
            "[\u0600-\u06FF]",
            RegexOptions.Compiled);

        /// <summary>
        /// Determines whether the input text is likely Persian
        /// </summary>
        /// <param name="Input">The text to check</param>
        /// <param name="KeywordThreshold">Minimum number of keyword matches required (default 1)</param>
        /// <param name="CharRatioThreshold">Minimum ratio of Persian characters (default 0.3)</param>
        /// <returns>True if the text is probably Persian, otherwise false</returns>
        public static bool IsProbablyPersian(
            string Input,
            int KeywordThreshold = 1,
            double CharRatioThreshold = 0.3)
        {
            if (string.IsNullOrWhiteSpace(Input))
                return false;

            int TotalLength = Input.Length;
            if (TotalLength == 0)
                return false;

            int PersianCharCount = PersianCharRegex.Matches(Input).Count;
            double PersianCharRatio = (double)PersianCharCount / TotalLength;

            int KeywordHits = PersianKeywords.Count(k => Input.IndexOf(k, StringComparison.Ordinal) >= 0);

            // Consider Persian if enough characters and at least some keywords
            return PersianCharRatio >= CharRatioThreshold &&
                   KeywordHits >= KeywordThreshold;
        }

        /// <summary>
        /// Calculates a score indicating the likelihood that the text is Persian
        /// </summary>
        /// <param name="Input">The text to score</param>
        /// <returns>A score representing the likelihood of Persian language</returns>
        public static double GetPersianScore(string Input)
        {
            if (string.IsNullOrWhiteSpace(Input))
                return 0;

            int TotalLength = Input.Length;
            if (TotalLength == 0)
                return 0;

            int PersianCharCount = PersianCharRegex.Matches(Input).Count;
            int KeywordHits = PersianKeywords.Count(k => Input.IndexOf(k, StringComparison.Ordinal) >= 0);

            // Persian chars are more reliable than keywords
            return (PersianCharCount * 1.5 + KeywordHits * 2.0) / TotalLength;
        }
    }
}
