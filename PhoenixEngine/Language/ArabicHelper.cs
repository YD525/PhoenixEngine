using System.Text.RegularExpressions;

namespace PhoenixEngine.Language
{
    internal static class ArabicHelper
    {
        // Matches Arabic Unicode block characters
        private static readonly Regex ArabicCharRegex = new Regex(
            @"[\u0600-\u06FF]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        // Matches common Arabic words, pronouns, particles, and connectors
        private static readonly Regex ArabicKeywordRegex = new Regex(
            @"\b(أنا|أنت|أنتِ|هو|هي|نحن|أنتم|هم|هذا|هذه|ذلك|الذي|التي|كان|كانت|يكون|يمكن|لدي|عند|هناك|هنا|كيف|متى|أين|لماذا|ماذا|من|كل|بعض|مع|بدون|في|على|إلى|عن|منذ|حتى|بين|بعد|قبل|أو|و|لكن|إذا|لأن|ثم|أيضًا|فقط|جدا|الآن|اليوم|غدًا|نعم|لا|ما|تم|قد|هل|كما|إن|أن)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        /// <summary>
        /// Determines whether a given text is likely to be Arabic
        /// </summary>
        /// <param name="Input">Input text to check</param>
        /// <param name="CharThreshold">Threshold for ratio of Arabic characters</param>
        /// <param name="KeywordHitsThreshold">Minimum number of matched keywords</param>
        public static bool IsProbablyArabic(string Input, double CharThreshold = 0.10, int KeywordHitsThreshold = 2)
        {
            if (string.IsNullOrWhiteSpace(Input))
                return false;

            int TotalLength = Input.Length;

            // Count of Arabic characters matched
            int ArabicCharCount = ArabicCharRegex.Matches(Input).Count;
            double ArabicCharRatio = (double)ArabicCharCount / TotalLength;

            // Count of matched keywords
            int KeywordHits = ArabicKeywordRegex.Matches(Input).Count;

            // Return true if either Arabic character ratio or keyword hits exceed threshold
            return ArabicCharRatio > CharThreshold || KeywordHits >= KeywordHitsThreshold;
        }

        /// <summary>
        /// Calculates a feature score indicating likelihood of Arabic language
        /// </summary>
        /// <param name="Input">Input text</param>
        /// <returns>Score value representing Arabic language likelihood</returns>
        public static double GetArabicScore(string Input)
        {
            if (string.IsNullOrWhiteSpace(Input))
                return 0;

            int TotalLength = Input.Length;
            int ArabicCharCount = ArabicCharRegex.Matches(Input).Count;
            int KeywordHits = ArabicKeywordRegex.Matches(Input).Count;

            // Simple weighted score formula combining Arabic characters and keywords
            return (ArabicCharCount * 1.5 + KeywordHits * 2.0) / TotalLength;
        }
    }
}
