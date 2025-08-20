using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PhoenixEngine.DelegateManagement;
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
                @"(\[[^\]]+\])" +                             
                @"|(<[a-zA-Z0-9]+(?:\s[^<>]*?)?>.*?</[a-zA-Z0-9]+>)" +  
                @"|(<[a-zA-Z0-9]+(?:\s[^<>]*/?)>)" +          
                @"|([^<\[\r\n][^<\[]*)",                      
                RegexOptions.Singleline | RegexOptions.Compiled);

            foreach (Match m in Matches)
            {
                string Tag = "";
                string Content = "";

                if (m.Groups[1].Success) // [pagebreak]
                {
                    Tag = m.Groups[1].Value;
                    Content = "";
                }
                else if (m.Groups[2].Success) // 成对标签
                {
                    string fullTag = m.Groups[2].Value;
                    string tagName = Regex.Match(fullTag, @"<([a-zA-Z0-9]+)").Groups[1].Value;
                    Tag = $"<{tagName}>";
                    Content = fullTag;
                }
                else if (m.Groups[3].Success) // 自闭合标签
                {
                    Tag = m.Groups[3].Value;
                    Content = Tag;
                }
                else if (m.Groups[4].Success) // 普通文本
                {
                    Tag = "";
                    Content = m.Groups[4].Value.Trim();
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

            if (DelegateHelper.SetBookTranslateCallback != null)
            {
                DelegateHelper.SetBookTranslateCallback(this.Key, this.CurrentText);
            }
        }

        private CancellationTokenSource CancelToken = new CancellationTokenSource();

        List<Segment> Content = new List<Segment>();
        public void TransBook(string Key, string Source)
        {
            CancelToken = new CancellationTokenSource();
            var Token = CancelToken.Token;

            this.ModName = Engine.GetModName();

            Languages SourceLanguage = Engine.From;
            Languages TargetLanguage = Engine.To;

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
                            bool CanAddCache = true;

                            LineID++;
                            var GetTransLine = Translator.QuickTrans(this.ModName, "Book", Key + LineID.ToString(), GetSourceLine, SourceLanguage, TargetLanguage, ref CanSleep,ref CanAddCache, true);

                            if (GetTransLine.Trim().Length > 0)
                            {
                                Source = ReplaceFirst(Source, GetSourceLine, GetTransLine);
                                CurrentTransCount++;

                                try
                                {
                                    Token.ThrowIfCancellationRequested();
                                }
                                catch { return; }

                                ApplyAllLine(Source);
                            }
                            else
                            {
                                try
                                {
                                    Token.ThrowIfCancellationRequested();
                                }
                                catch { return; }

                                goto NextCall;
                            }
                        }
                    }
            }

            IsEnd = true;
        }

        public void Cancel()
        {
            CancelToken.Cancel();
        }
        public static string ReplaceFirst(string Text, string Search, string Replace)
        {
            int Position = Text.IndexOf(Search);
            if (Position < 0) return Text;
            return Text.Substring(0, Position) + Replace + Text.Substring(Position + Search.Length);
        }

    }
}
