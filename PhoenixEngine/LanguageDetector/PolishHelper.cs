using System.Text.RegularExpressions;

namespace PhoenixEngine.LanguageDetector
{
    public static class PolishHelper
    {
        // Matches common Polish function words: conjunctions, prepositions, pronouns, verbs, particles, etc.
        private static readonly Regex PolishKeywordRegex = new Regex(
      @"\b(że|jest|nie|tak|jak|co|czy|go|za|po|się|dla|ten|tego|być|był|była|było|jestem|jesteś|są|mamy|macie|oni|one|który|która|które|którego|którą|więc|bardzo|jeszcze|może|muszę|chcę)\b",
      RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Matches Polish-specific accented characters: ąćęłńóśźż and their uppercase variants
        private static readonly Regex PolishAccentCharRegex = new Regex("[ąćęłńóśźżĄĆĘŁŃÓŚŹŻ]", RegexOptions.Compiled);

        /// <summary>
        /// Determines whether a given text is likely to be Polish
        /// </summary>
        /// <param name="Input">Input text to check</param>
        /// <param name="KeywordHitsThreshold">Minimum number of matched keywords required (default 2)</param>
        /// <param name="AccentCharRatioThreshold">Minimum ratio of accented chars to total length (default 0.01)</param>
        /// <returns>True if text is probably Polish, otherwise false</returns>
        public static bool IsProbablyPolish(string Input, int KeywordHitsThreshold = 3, double AccentCharRatioThreshold = 0.02)
        {
            if (string.IsNullOrWhiteSpace(Input))
                return false;

            int TotalLength = Input.Length;
            int KeywordHits = PolishKeywordRegex.Matches(Input).Count;
            int AccentCharCount = PolishAccentCharRegex.Matches(Input).Count;
            double AccentCharRatio = (double)AccentCharCount / TotalLength;

            // Return true if keyword hits exceed threshold OR accent char ratio exceeds threshold
            return (KeywordHits >= KeywordHitsThreshold && AccentCharCount > 0)
            || AccentCharRatio > AccentCharRatioThreshold;
        }

        /// <summary>
        /// Calculates a feature score indicating likelihood of Polish language
        /// </summary>
        /// <param name="Input">Input text</param>
        /// <returns>Score value representing Polish language likelihood</returns>
        public static double GetPolishScore(string Input)
        {
            if (string.IsNullOrWhiteSpace(Input))
                return 0;

            int TotalLength = Input.Length;
            int KeywordHits = PolishKeywordRegex.Matches(Input).Count;
            int AccentCharCount = PolishAccentCharRegex.Matches(Input).Count;

            // Weighted score combining keywords and accented char count normalized by input length
            return (KeywordHits * 1.5 + AccentCharCount * 2.0) / TotalLength;
        }
    }
}
