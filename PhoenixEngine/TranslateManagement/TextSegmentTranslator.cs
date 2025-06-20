using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;

namespace PhoenixEngine.TranslateManagement
{
    // Copyright (C) 2025 YD525
    // Licensed under the GNU GPLv3
    // See LICENSE for details
    //https://github.com/YD525/PhoenixEngine
    public class Segment
    {
        public string Tag { get; set; } = "";
        public string RawContent { get; set; } = "";
        public string TextToTranslate { get; set; } = "";
    }
    public class TextSegmentTranslator
    {
        public string ModName = "";
        public string Key = "";
        public string Source = "";
        public List<string> TransLines = new List<string>();
        public int TransCount = 0;
        public int CurrentTransCount = 0;
        public string CurrentText = "";
        public bool IsEnd = false;


        public TextSegmentTranslator()
        {
            IsEnd = false;
        }

        private string StripHtmlTags(string input)
        {
            return Regex.Replace(input, "<.*?>", string.Empty);
        }
        public List<Segment> Load(string Input)
        {
            List<Segment> Segments = new List<Segment>();

            var Matches = Regex.Matches(Input,
                @"(\[[^\]]+\])\s*([\s\S]*?)(?=(\[[^\]]+\])|<[^<>]+>|$)" +
                @"|<([a-zA-Z0-9]+(?:\s[^<>]*?)?)>([\s\S]*?)</\4>" +
                @"|^([\s\S]+?)(?=(\[[^\]]+\])|<[^<>]+>|$)",
                RegexOptions.Compiled);

            foreach (Match Match in Matches)
            {
                string Tag = "";
                string Content = "";

                if (!string.IsNullOrEmpty(Match.Groups[1].Value))
                {
                    Tag = Match.Groups[1].Value.Trim();
                    Content = Match.Groups[2].Value.Trim();
                }
                else if (!string.IsNullOrEmpty(Match.Groups[4].Value))
                {
                    string tagFull = Match.Groups[4].Value.Trim();
                    string inner = Match.Groups[5].Value;
                    string tagName = tagFull.Split(' ')[0];
                    string openTag = $"<{tagFull}>";
                    string closeTag = $"</{tagName}>";

                    Tag = openTag;
                    Content = (openTag + inner + closeTag).Trim();
                }
                else if (!string.IsNullOrEmpty(Match.Groups[6].Value))
                {
                    Tag = "";
                    Content = Match.Groups[6].Value.Trim();
                }

                string TextOnly = StripHtmlTags(Content).Trim();

                Segments.Add(new Segment
                {
                    Tag = Tag,
                    RawContent = Content,
                    TextToTranslate = string.IsNullOrWhiteSpace(TextOnly) ? null : TextOnly
                });
            }

            return Segments;
        }

        public void ApplyAllLine(string Source)
        {
            this.CurrentText = Source;
        }
        List<Segment> Content = new List<Segment>();
        public void TransBook(Languages SourceLanguage, Languages TargetLanguage, string ModName,string Key, string Source, CancellationToken Token)
        {
            this.ModName = ModName;
            this.Key = Key;
            Content.Clear();
            this.Source = Source;
            Content = Load(Source);
            List<Segment> GetSegments = new List<Segment>();

            GetSegments.AddRange(Content);

            foreach (var Segment in GetSegments)
            {
                if (Segment.TextToTranslate != null)
                    foreach (var GetLine in Segment.TextToTranslate.Split(new char[2] { '\r', '\n' }))
                    {
                        if (GetLine.Trim().Length > 0)
                        {
                            TransCount++;
                        }
                    }
            }

            int LineID = 0;
            for (int i = 0; i < GetSegments.Count; i++)
            {
                if (GetSegments[i].TextToTranslate != null)
                    foreach (var GetSourceLine in GetSegments[i].TextToTranslate.Split(new char[2] { '\r', '\n' }))
                    {
                        if (GetSourceLine.Trim().Length > 0)
                        {
                        NextCall:
                            try
                            {
                                Token.ThrowIfCancellationRequested();
                            }
                            catch { return; }

                            bool CanSleep = false;
                            LineID++;
                            var GetTransLine = Translator.QuickTrans(ModName, "Book", Key + LineID.ToString(), GetSourceLine, SourceLanguage, TargetLanguage, ref CanSleep, true);

                            if (GetTransLine.Trim().Length > 0)
                            {
                                Source = ReplaceFirst(Source, GetSourceLine, GetTransLine);
                                CurrentTransCount++;
                                ApplyAllLine(Source);
                            }
                            else
                            {
                                goto NextCall;
                            }
                        }
                    }
            }

            IsEnd = true;
        }

        public static string ReplaceFirst(string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0) return text;
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }

    }
}
