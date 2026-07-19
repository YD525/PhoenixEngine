using System.Text.RegularExpressions;

namespace PhoenixEngine.Language
{
    internal static class CzechHelper
    {
        // Matches Czech special characters: háček (č,š,ž,ř,ě,ň,ď,ť) and čárka (á,é,í,ó,ú,ů,ý)
        private static readonly Regex CzechCharRegex = new Regex(
            "[áéíóúůýčšžřěňďť]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        // Matches common Czech function words (articles don't exist in Czech, so focus on
        // pronouns, prepositions, conjunctions, common verbs, and particles)
        private static readonly Regex CzechKeywordRegex = new Regex(
        @"\b(já|ty|on|ona|ono|vy|oni|ony|jsem|jsi|jsou|byl|byla|bylo|byli|být|mám|máš|má|máme|máte|mají|mít|co|kdo|jak|kde|kdy|proč|toto|tento|tato|jeden|jedna|jedno|ale|nebo|že|když|protože|pokud|jako|také|jen|už|ještě|více|méně|než|tak|zde|tam|tady|pak|dnes|teď|již|velmi|se|si|ho|mu|jej|ji|nás|vás|nich|ve|na|z|ze|od|pro|při|po|před|za|nad|pod|mezi|bez|přes|ke|ku)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
        );


        /// <summary>
        /// Determines whether a given text is likely to be Czech
        /// </summary>
        /// <param name="Input">Input text to check</param>
        /// <param name="CharThreshold">Threshold for ratio of special characters</param>
        /// <param name="KeywordHitsThreshold">Minimum number of matched keywords</param>
        public static bool IsProbablyCzech(string Input, double CharThreshold = 0.02, int KeywordHitsThreshold = 2)
        {
            if (string.IsNullOrWhiteSpace(Input))
                return false;

            int TotalLength = Input.Length;

            // Count of special Czech characters matched
            int SpecialCharCount = CzechCharRegex.Matches(Input).Count;
            double SpecialCharRatio = (double)SpecialCharCount / TotalLength;

            // Count of matched keywords
            int KeywordHits = CzechKeywordRegex.Matches(Input).Count;

            // Return true if either special character ratio or keyword hits exceed threshold
            return SpecialCharRatio > CharThreshold || KeywordHits >= KeywordHitsThreshold;
        }

        /// <summary>
        /// Calculates a feature score indicating likelihood of Czech language
        /// </summary>
        /// <param name="Input">Input text</param>
        /// <returns>Score value representing Czech language likelihood</returns>
        public static double GetCzechScore(string Input)
        {
            if (string.IsNullOrWhiteSpace(Input))
                return 0;

            int TotalLength = Input.Length;
            int SpecialCharCount = CzechCharRegex.Matches(Input).Count;
            int KeywordHits = CzechKeywordRegex.Matches(Input).Count;

            // Simple weighted score formula combining special characters and keywords
            return (SpecialCharCount * 1.5 + KeywordHits * 2.0) / TotalLength;
        }
    }
}
