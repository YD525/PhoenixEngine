using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PhoenixEngine.ADO;
using PhoenixEngine.Language;

namespace PhoenixEngine.Engine.ADO
{
    internal class TrieNode
    {
        public Dictionary<char, TrieNode> Children { get; } = new Dictionary<char, TrieNode>();
        public double? Frequency { get; set; } = null;
    }

    public class WordAutoComplete : IDisposable
    {
        private readonly TrieNode _Root = new TrieNode();
        private readonly P_SQLite _DB = new P_SQLite(); 

        public Languages CurrentLanguage { get; private set; } = Languages.Null;

        public static string DatabaseDirectory { get; set; } = "";

        public int MaxResults { get; set; } = 10;


        public static string GetDatabaseName(Languages Language)
        {
            return (P_Language.ToLanguageCode(Language).Replace("-","_")) + ".db";
        }

        public bool ExistsDatabase(Languages Language)
        {
            string Path = GetDatabasePath(Language);
            return File.Exists(Path);
        }

        public bool LoadWords(Languages Language)
        {
            if (Language == Languages.Null || Language == Languages.Auto)
                return false;

            string Path = GetDatabasePath(Language);

            if (!File.Exists(Path))
                return false;

            try
            {
                _Root.Children.Clear();
                _Root.Frequency = null;

                _DB.OpenSQL(Path);
                LoadAndBuildTrie();
                _DB.Close();

                CurrentLanguage = Language;
                return true;
            }
            catch
            {
                CurrentLanguage = Languages.Null;
                return false;
            }
        }

        private string GetDatabasePath(Languages Language)
        {
            return Path.Combine(WordAutoComplete.DatabaseDirectory, GetDatabaseName(Language));
        }

        private void LoadAndBuildTrie()
        {
            var Rows = _DB.ExecuteQuery("SELECT Word, Freq FROM Words;");
            foreach (var Row in Rows)
            {
                if (Row["Word"] is string Word && !string.IsNullOrEmpty(Word)
                    && Row["Freq"] is double Freq)
                {
                    Insert(Word.ToLowerInvariant(), Freq);
                }
            }
        }

        private void Insert(string Word, double Freq)
        {
            var Node = _Root;
            foreach (char Char in Word)
            {
                if (!Node.Children.TryGetValue(Char, out var Next))
                {
                    Next = new TrieNode();
                    Node.Children[Char] = Next;
                }
                Node = Next;
            }
            Node.Frequency = Freq;
        }

        private void CountNodes(TrieNode Node, ref int Count)
        {
            if (Node.Frequency.HasValue)
                Count++;
            foreach (var KV in Node.Children)
                CountNodes(KV.Value, ref Count);
        }

        public int CountWords()
        {
            int Count = 0;
            CountNodes(_Root, ref Count);
            return Count;
        }
        public List<string> Query(string UserText)
        {
            if (string.IsNullOrEmpty(UserText) || CurrentLanguage == Languages.Null)
                return new List<string>();

            string Prefix = string.Empty;
            char[] Separators = new char[] { ' ', '\t', '\r', '\n' };

            if (UserText.Length > 0 && Separators.Contains(UserText[UserText.Length - 1]))
            {
                return new List<string>();
            }

            if (CurrentLanguage.IsSpaceDelimitedLanguage())
            {
                string[] Parts = UserText.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

                if (Parts.Length == 0) return new List<string>();

                Prefix = Parts[Parts.Length - 1];
            }
            else if (CurrentLanguage.IsNoSpaceLanguage())
            {
                Prefix = ExtractNoSpacePrefix(UserText, 6);
            }
            else
            {
                string[] Parts = UserText.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

                if (Parts.Length == 0) return new List<string>();

                Prefix = Parts[Parts.Length - 1];
            }

            if (string.IsNullOrEmpty(Prefix))
                return new List<string>();

            Prefix = Prefix.ToLowerInvariant();
            var Node = _Root;
            foreach (char Char in Prefix)
            {
                if (!Node.Children.TryGetValue(Char, out var Next))
                    return new List<string>();
                Node = Next;
            }

            var Results = new List<(string Word, double Freq)>();
            CollectWords(Node, Prefix, Results);

            Results.Sort((A, B) => B.Freq.CompareTo(A.Freq));

            return Results
                .Take(MaxResults)
                .Select(R => R.Word)
                .ToList();
        }

        private string ExtractNoSpacePrefix(string UserText, int MaxChars)
        {
            if (string.IsNullOrEmpty(UserText)) return string.Empty;

            int End = UserText.Length - 1;

            if (char.IsWhiteSpace(UserText[End]))
            {
                return string.Empty;
            }

            int Start = End + 1; 
            int Count = 0;

            while (Start > 0 && Count < MaxChars)
            {
                char Prev = UserText[Start - 1];

                if (char.IsWhiteSpace(Prev) || char.IsPunctuation(Prev) ||
                    char.IsSymbol(Prev) || IsCJKPunctuation(Prev))
                {
                    break;
                }

                Start--;
                Count++;
            }

            if (Start > End) return string.Empty;

            return UserText.Substring(Start, End - Start + 1);
        }

        private bool IsCJKPunctuation(char C)
        {
            return C == '，' || C == '。' || C == '！' || C == '？' ||
                   C == '；' || C == '：' || C == '"' || C == '"' ||
                   C == '、' || C == '「' || C == '」' || C == '【' || C == '】' ||
                   C == '《' || C == '》' || C == '・' || C == '。' ||
                   C == '　'; 
        }

        private void CollectWords(TrieNode Node, string Current, List<(string, double)> Results)
        {
            if (Results.Count >= MaxResults * 4)
                return;
            if (Node.Frequency.HasValue)
                Results.Add((Current, Node.Frequency.Value));
            foreach (var KV in Node.Children)
                CollectWords(KV.Value, Current + KV.Key, Results);
        }

        public void Dispose()
        {
            _DB.Dispose();
        }

        public static Dictionary<Languages, WordAutoComplete> WordCompleters = new Dictionary<Languages, WordAutoComplete>();

        public static void Init()
        {
            foreach (var GetFile in Directory.GetFiles(WordAutoComplete.DatabaseDirectory, "*.db"))
            {
                FileInfo GetInfo = new FileInfo(GetFile);
                var GetName = Path.GetFileNameWithoutExtension(GetInfo.Name).Replace("_", "-");
                Languages GetLang = P_Language.FromLanguageCode(GetName);

                if (!WordCompleters.ContainsKey(GetLang))
                {
                    WordCompleters.Add(GetLang,new WordAutoComplete());
                    WordCompleters[GetLang].LoadWords(GetLang);
                }
            }
        }
    }
}