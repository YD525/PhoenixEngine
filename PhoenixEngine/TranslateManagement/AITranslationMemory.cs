using System.Runtime.InteropServices.ObjectiveC;
using System.Text;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManagement;

namespace PhoenixEngine.TranslateManage
{
    // Copyright (c) 2025 YD525
    // Licensed under the MIT License.
    // See LICENSE file in the project root for full license information.
    //https://github.com/YD525/PhoenixEngine
    public static class LanguageExtensions
    {
        public static bool IsSpaceDelimitedLanguage(this Languages Lang)
        {
            return Lang == Languages.English ||
            Lang == Languages.German ||
            Lang == Languages.Turkish ||
            Lang == Languages.Brazilian ||
            Lang == Languages.Russian ||
            Lang == Languages.Italian ||
            Lang == Languages.Spanish ||
            Lang == Languages.Indonesian ||
            Lang == Languages.Hindi ||
            Lang == Languages.Urdu ||
            Lang == Languages.French ||
            Lang == Languages.Vietnamese ||
            Lang == Languages.Polish;
        }

        public static bool IsNoSpaceLanguage(this Languages Lang)
        {
            return Lang == Languages.SimplifiedChinese ||
            Lang == Languages.TraditionalChinese ||
            Lang == Languages.Japanese ||
            Lang == Languages.Korean;
        }
    }

    public class AITranslationMemory
    {
        private readonly Dictionary<string, string> _TranslationDictionary = new Dictionary<string, string>();
        private readonly Dictionary<string, HashSet<string>> _WordIndex = new Dictionary<string, HashSet<string>>();

        public void Clear()
        {
            _TranslationDictionary.Clear();
            _WordIndex.Clear();
        }

        private object AddTranslationLocker = new object();
        /// <summary>
        /// Add translation and create index (filter out long text)
        /// </summary>
        public void AddTranslation(Languages SourceLang, string Original, string Translated)
        {
            lock (AddTranslationLocker)
            {
                if (!_TranslationDictionary.ContainsKey(Original))
                {
                    _TranslationDictionary[Original] = Translated;

                    string[] Tokens = Tokenize(SourceLang, Original);
                    foreach (string Word in Tokens)
                    {
                        string Key = Word.ToLower();
                        if (!_WordIndex.ContainsKey(Key))
                            _WordIndex[Key] = new HashSet<string>();

                        _WordIndex[Key].Add(Original);
                    }
                }
            }
        }

        /// <summary>
        /// Find the most relevant translations
        /// </summary>
        public List<string> FindRelevantTranslations(Languages SourceLang, string Query,int ContextLength)
        {
            lock (AddTranslationLocker)
            {
                string[] Words = Tokenize(SourceLang, Query);
                Dictionary<string, int> RelevanceMap = new Dictionary<string, int>();
                HashSet<string> CandidateSentences = new HashSet<string>();

                foreach (string Word in Words)
                {
                    string Key = Word.ToLower();
                    if (_WordIndex.ContainsKey(Key))
                    {
                        foreach (var Sentence in _WordIndex[Key])
                        {
                            CandidateSentences.Add(Sentence);
                        }
                    }
                }

                foreach (var Sentence in CandidateSentences)
                {
                    int MatchCount = 0;
                    foreach (string Word in Words)
                    {
                        string Key = Word.ToLower();
                        if (_WordIndex.TryGetValue(Key, out var SentencesForWord))
                        {
                            if (SentencesForWord.Contains(Sentence))
                                MatchCount++;
                        }
                    }
                    if (MatchCount > 0)
                    {
                        RelevanceMap[Sentence] = MatchCount;
                    }
                }

                List<string> GetContexts = RelevanceMap.OrderByDescending(kvp => kvp.Value)
                    .Select(kvp => $"{kvp.Key} -> {_TranslationDictionary[kvp.Key]}")
                    .ToList();

                TrimListByCharCount(ref GetContexts, ContextLength);

                return GetContexts;
            }
        }

        /// <summary>
        /// Tokenizer: supports English splitting + simplified N-gram for no-space languages
        /// </summary>
        private string[] Tokenize(Languages Lang, string Text)
        {
            return TextTokenizer.Tokenize(Lang, Text);
        }

        public void TrimListByCharCount(ref List<string> ListToTrim, int MaxChars)
        {
            if (ListToTrim == null || ListToTrim.Count == 0 || MaxChars <= 0)
            {
                return;
            }

            int CurrentLength = 0;
            var TrimmedList = new List<string>();

            foreach (var item in ListToTrim)
            {
                if (CurrentLength + item.Length > MaxChars)
                {
                    break;
                }

                TrimmedList.Add(item);
                CurrentLength += item.Length;
            }

            ListToTrim = TrimmedList;
        }
    }
}
