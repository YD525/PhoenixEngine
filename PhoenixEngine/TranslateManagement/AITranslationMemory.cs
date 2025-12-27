using System;
using System.Collections.Generic;
using System.Linq;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManagement;

namespace PhoenixEngine.TranslateManage
{
    public static class LanguageExtensions
    {
        public static bool IsSpaceDelimitedLanguage(this Languages Lang)
        {
            return Lang == Languages.German ||
            Lang == Languages.English ||
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
            Lang == Languages.Polish||
            Lang == Languages.Persian;
        }

        public static bool IsNoSpaceLanguage(this Languages Lang)
        {
            return Lang == Languages.Japanese ||
            Lang == Languages.Korean ||
            Lang == Languages.TraditionalChinese ||
            Lang == Languages.Thai||
            Lang == Languages.SimplifiedChinese;
        }
    }

    public class AITranslationMemory
    {
        // TranslationMemory[TargetLang][Original] = Translated
        private readonly Dictionary<Languages, Dictionary<string, string>> _TranslationMemory
            = new Dictionary<Languages, Dictionary<string, string>>();

        // WordIndex[TargetLang][token] = set of originals
        private readonly Dictionary<Languages, Dictionary<string, HashSet<string>>> _WordIndex
            = new Dictionary<Languages, Dictionary<string, HashSet<string>>>();

        private readonly object Locker = new object();

        public void Clear()
        {
            lock (Locker)
            {
                _TranslationMemory.Clear();
                _WordIndex.Clear();
            }
        }

        /// <summary>
        /// Remove translation only if stored value equals the provided translated.
        /// Index is cleaned accordingly.
        /// </summary>
        public bool RemoveTranslation(Languages SourceLang, Languages TargetLang,
                                      string Original, string Translated)
        {
            // detect languages
            if (SourceLang == Languages.Auto)
                SourceLang = LanguageHelper.DetectLanguageByLine(Original);

            if (TargetLang == Languages.Auto)
                TargetLang = LanguageHelper.DetectLanguageByLine(Translated);

            lock (Locker)
            {
                if (!_TranslationMemory.ContainsKey(TargetLang))
                    return false;

                var dict = _TranslationMemory[TargetLang];

                // not found
                if (!dict.ContainsKey(Original))
                    return false;

                // must match exactly
                string stored = dict[Original];
                if (!string.Equals(stored, Translated, StringComparison.Ordinal))
                    return false;

                // --- remove from main dict ---
                dict.Remove(Original);

                // --- update word index ---
                if (_WordIndex.ContainsKey(TargetLang))
                {
                    var index = _WordIndex[TargetLang];

                    // tokenize original using source language
                    string[] tokens = Tokenize(SourceLang, Original);

                    foreach (string w in tokens)
                    {
                        string key = w.ToLower();

                        if (index.TryGetValue(key, out var set))
                        {
                            set.Remove(Original);

                            if (set.Count == 0)
                                index.Remove(key);
                        }
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Add translation: tokenize using source language, 
        /// but store index under target language bucket.
        /// </summary>
        public void AddTranslation(Languages SourceLang, Languages TargetLang,
                                   string Original, string Translated)
        {
            // Auto detect source
            if (SourceLang == Languages.Auto)
                SourceLang = LanguageHelper.DetectLanguageByLine(Original);

            // Auto detect target
            if (TargetLang == Languages.Auto)
                TargetLang = LanguageHelper.DetectLanguageByLine(Translated);

            lock (Locker)
            {
                // Create target dictionaries if missing
                if (!_TranslationMemory.ContainsKey(TargetLang))
                    _TranslationMemory[TargetLang] = new Dictionary<string, string>();

                if (!_WordIndex.ContainsKey(TargetLang))
                    _WordIndex[TargetLang] = new Dictionary<string, HashSet<string>>();

                var dict = _TranslationMemory[TargetLang];
                var index = _WordIndex[TargetLang];

                // Do not overwrite existing
                if (!dict.ContainsKey(Original))
                {
                    dict[Original] = Translated;

                    // TOKENIZE USING SOURCE LANGUAGE
                    string[] tokens = Tokenize(SourceLang, Original);

                    foreach (string word in tokens)
                    {
                        string key = word.ToLower();

                        if (!index.ContainsKey(key))
                            index[key] = new HashSet<string>();

                        index[key].Add(Original);
                    }
                }
            }
        }

        /// <summary>
        /// Find relevant translations using target language memory.
        /// Query tokenization uses source language.
        /// </summary>
        public List<string> FindRelevantTranslations(Languages SourceLang,
                                                     Languages TargetLang,
                                                     string Query,
                                                     int ContextLength)
        {
            if (SourceLang == Languages.Auto)
                SourceLang = LanguageHelper.DetectLanguageByLine(Query);

            if (TargetLang == Languages.Auto)
                throw new InvalidOperationException("TargetLang cannot be Auto when finding context.");

            lock (Locker)
            {
                if (!_TranslationMemory.ContainsKey(TargetLang))
                    return new List<string>();

                if (!_WordIndex.ContainsKey(TargetLang))
                    return new List<string>();

                var dict = _TranslationMemory[TargetLang];
                var index = _WordIndex[TargetLang];

                // TOKENIZE QUERY USING SOURCE LANGUAGE
                string[] words = Tokenize(SourceLang, Query);

                HashSet<string> CandidateSentences = new HashSet<string>();
                Dictionary<string, int> RelevanceMap = new Dictionary<string, int>();

                // get candidate entries
                foreach (string word in words)
                {
                    string key = word.ToLower();
                    if (index.ContainsKey(key))
                    {
                        foreach (var sentence in index[key])
                            CandidateSentences.Add(sentence);
                    }
                }

                // score candidate relevance
                foreach (var sentence in CandidateSentences)
                {
                    int count = 0;

                    foreach (string word in words)
                    {
                        string key = word.ToLower();
                        if (index.TryGetValue(key, out var set))
                        {
                            if (set.Contains(sentence))
                                count++;
                        }
                    }

                    if (count > 0)
                        RelevanceMap[sentence] = count;
                }

                var result = RelevanceMap
                    .OrderByDescending(kvp => kvp.Value)
                    .Select(kvp => $"{kvp.Key} -> {dict[kvp.Key]}")
                    .ToList();

                TrimListByCharCount(ref result, ContextLength);
                return result;
            }
        }

        /// <summary>
        /// Tokenizer wrapper
        /// </summary>
        private string[] Tokenize(Languages Lang, string Text)
        {
            if (Lang == Languages.Auto)
                Lang = LanguageHelper.DetectLanguageByLine(Text);

            return TextTokenizer.Tokenize(Lang, Text);
        }

        public void TrimListByCharCount(ref List<string> ListToTrim, int MaxChars)
        {
            if (ListToTrim == null || ListToTrim.Count == 0 || MaxChars <= 0)
                return;

            int current = 0;
            var trimmed = new List<string>();

            foreach (var item in ListToTrim)
            {
                if (current + item.Length > MaxChars)
                    break;

                trimmed.Add(item);
                current += item.Length;
            }

            ListToTrim = trimmed;
        }
    }

}
