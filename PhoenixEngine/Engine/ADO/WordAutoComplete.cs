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

        public List<string> Query(string Prefix)
        {
            if (string.IsNullOrEmpty(Prefix) || CurrentLanguage == Languages.Null)
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
            Results.Sort((a, b) => b.Freq.CompareTo(a.Freq));

            return Results
                .Take(MaxResults)
                .Select(r => r.Word)
                .ToList();
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