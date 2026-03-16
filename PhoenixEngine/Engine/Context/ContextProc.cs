using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PhoenixEngine.Language;
using PhoenixEngine.Unit;

namespace PhoenixEngine.Engine
{
    internal class ContextProc
    {
        #region Words Analysis

        public static int CountWords(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            int count = 0;
            bool inWord = false;

            foreach (char c in text)
            {
                if (c == ' ' || c == '\t')
                    inWord = false;
                else if (!inWord)
                {
                    count++;
                    inWord = true;
                }
            }

            return count;
        }

        public static int PickContextLeader(
            List<int> bucket,
            List<BaseUnit> items,
            Dictionary<int, HashSet<string>> tokensCache)
        {
            int best = -1;

            int bestWordCount = int.MaxValue;
            bool bestHasDigit = true;
            int bestTokenCount = -1;
            int bestLength = int.MaxValue;

            foreach (var i in bucket)
            {
                var text = items[i].Original;

                int wc = CountWords(text);
                bool hasDigit = HasDigit(text);
                int tc = tokensCache[i].Count;
                int len = text.Length;

                if (
                    wc < bestWordCount ||
                    (wc == bestWordCount && bestHasDigit && !hasDigit) ||
                    (wc == bestWordCount && hasDigit == bestHasDigit && tc > bestTokenCount) ||
                    (wc == bestWordCount && hasDigit == bestHasDigit && tc == bestTokenCount && len < bestLength)
                )
                {
                    best = i;
                    bestWordCount = wc;
                    bestHasDigit = hasDigit;
                    bestTokenCount = tc;
                    bestLength = len;
                }
            }

            return best;
        }

        public static string BuildPrefixKey(string text, int maxWords)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            int len = text.Length;
            int wordCount = 0;
            int i = 0;

            var sb = new StringBuilder(len);

            while (i < len && wordCount < maxWords)
            {
                while (i < len && (text[i] == ' ' || text[i] == '\t'))
                    i++;

                if (i >= len)
                    break;

                if (wordCount > 0)
                    sb.Append(' ');

                while (i < len && text[i] != ' ' && text[i] != '\t')
                {
                    sb.Append(char.ToLowerInvariant(text[i]));
                    i++;
                }

                wordCount++;
            }

            return sb.ToString();
        }

        private static bool HasDigit(string text)
        {
            foreach (char c in text)
                if (char.IsDigit(c))
                    return true;
            return false;
        }

        #endregion
    }
}
