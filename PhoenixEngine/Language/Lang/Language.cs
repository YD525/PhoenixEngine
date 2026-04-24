using System;
using System.Collections.Generic;
using System.Linq;

namespace PhoenixEngine.Language
{
    public enum Languages
    {
        // Special
        Null = -1,
        Auto = 0,

        // Latin-based / common Western languages
        English = 1,
        German = 2,
        French = 3,
        CanadianFrench = 5,
        Spanish = 6,
        Italian = 7,
        Portuguese = 8,
        Brazilian = 9,
        Polish = 10,
        Turkish = 11,
        Vietnamese = 12,
        Indonesian = 13,

        // Slavic / Eastern European
        Russian = 20,
        Ukrainian = 21,
        Czech = 22,

        // South Asian / Middle Eastern (RTL / special word boundaries)
        Hindi = 30,
        Urdu = 31,
        Persian = 32,

        // East Asian (no explicit word delimiters)
        TraditionalChinese = 50,
        SimplifiedChinese = 51,
        Japanese = 52,
        Korean = 53,
        Thai = 55
    }
    public class LanguageDetector
    {
        public Dictionary<Languages, double> Array = new Dictionary<Languages, double>();

        public void Add(Languages Lang)
        {
            if (Array.ContainsKey(Lang))
            {
                Array[Lang] = Array[Lang] + 1;
            }
            else
            {
                Array.Add(Lang, 1);
            }
        }

        public void Add(Languages Lang, double Ratio)
        {
            if (Array.ContainsKey(Lang))
            {
                Array[Lang] = Array[Lang] + Ratio;
            }
            else
            {
                Array.Add(Lang, Ratio);
            }
        }


        public Languages GetMaxLang()
        {
            if (Array.Count > 0)
            {
                return Array
                  .OrderByDescending(kv => kv.Value)
                  .First().Key;
            }
            return Languages.English;
        }
    }
    public static class P_Language
    {
        private static readonly Dictionary<Languages, string> LanguageCodeMap = new Dictionary<Languages, string>()
        {
            [Languages.English] = "en",
            [Languages.SimplifiedChinese] = "zh-CN",
            [Languages.TraditionalChinese] = "zh-TW",
            [Languages.Japanese] = "ja",
            [Languages.German] = "de",
            [Languages.Korean] = "ko",
            [Languages.Turkish] = "tr",
            [Languages.Brazilian] = "pt-BR",
            [Languages.Portuguese] = "pt",
            [Languages.Russian] = "ru",
            [Languages.Ukrainian] = "uk",
            [Languages.Czech] = "cs",
            [Languages.Italian] = "it",
            [Languages.Spanish] = "es",
            [Languages.Hindi] = "hi",
            [Languages.Urdu] = "ur",
            [Languages.Indonesian] = "id",
            [Languages.French] = "fr",
            [Languages.CanadianFrench] = "fr-CA",
            [Languages.Vietnamese] = "vi",
            [Languages.Polish] = "pl",
            [Languages.Thai] = "th",
            [Languages.Persian] = "fa",
            [Languages.Auto] = "auto",
            [Languages.Null] = ""
        };

        private static readonly Dictionary<string, Languages> CodeToLanguageMap = new Dictionary<string, Languages>(StringComparer.OrdinalIgnoreCase);
        static P_Language()
        {
            foreach (var pair in LanguageCodeMap)
            {
                if (!string.IsNullOrWhiteSpace(pair.Value))
                {
                    CodeToLanguageMap[pair.Value] = pair.Key;
                }
            }
        }
        public static string ToLanguageCode(Languages lang)
        {
            return LanguageCodeMap.TryGetValue(lang, out var code) ? code : "";
        }
        public static Languages FromLanguageCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return Languages.Null;

            return CodeToLanguageMap.TryGetValue(code, out var lang) ? lang : Languages.Null;
        }
        public static void DetectLanguage(ref LanguageDetector OneDetect, string Str)
        {
            if (string.IsNullOrWhiteSpace(Str))
                return;

            if (EnglishHelper.IsProbablyEnglish(Str)) //100%
            {
                OneDetect.Add(Languages.English, 0.01);
            }

            if (RussianHelper.ContainsRussian(Str)) //100%
            {
                OneDetect.Add(Languages.Russian, 0.02);
            }

            if (UkrainianHelper.IsProbablyUkrainian(Str))
            {
                OneDetect.Add(Languages.Ukrainian, UkrainianHelper.GetUkrainianScore(Str));
            }

            if (JapaneseHelper.IsProbablyJapanese(Str)) //90%
            {
                OneDetect.Add(Languages.Japanese, JapaneseHelper.GetJapaneseScore(Str));
            }
            else
            {
                var GetResult = ChineseVariantMap.CheckLangType(Str);

                if (GetResult == ZHType.Traditional)
                {
                    OneDetect.Add(Languages.TraditionalChinese, 0.02);
                }
                else
                if (GetResult == ZHType.Simplified)
                {
                    OneDetect.Add(Languages.SimplifiedChinese, 0.02);
                }
            }

            if (KoreanHelper.IsProbablyKorean(Str)) //100%
            {
                OneDetect.Add(Languages.Korean, KoreanHelper.GetKoreanScore(Str));
            }

            if (FrenchHelper.IsProbablyFrench(Str)) //85%
            {
                if (CanadianFrenchHelper.IsProbablyCanadianFrench(Str))
                {
                    OneDetect.Add(Languages.CanadianFrench);
                }
                else
                {
                    OneDetect.Add(Languages.French, FrenchHelper.GetFrenchScore(Str));
                }
            }

            if (PortugueseHelper.IsProbablyPortuguese(Str)) //85%
            {
                OneDetect.Add(Languages.Portuguese, PortugueseHelper.GetPortugueseScore(Str));

                if (BrazilianPortugueseHelper.IsProbablyBrazilianPortuguese(Str))
                {
                    OneDetect.Add(Languages.Brazilian, BrazilianPortugueseHelper.GetBrazilianPortugueseScore(Str));
                }
            }

            if (GermanHelper.IsProbablyGerman(Str)) //85%
            {
                OneDetect.Add(Languages.German, GermanHelper.GetGermanScore(Str));
            }

            if (ItalianHelper.IsProbablyItalian(Str))
            {
                OneDetect.Add(Languages.Italian, ItalianHelper.GetItalianScore(Str));
            }

            if (SpanishHelper.IsProbablySpanish(Str))
            {
                OneDetect.Add(Languages.Spanish, SpanishHelper.GetSpanishScore(Str));
            }

            if (PolishHelper.IsProbablyPolish(Str))
            {
                OneDetect.Add(Languages.Polish, PolishHelper.GetPolishScore(Str));
            }
            else
            if (CzechHelper.IsProbablyCzech(Str))
            {
                OneDetect.Add(Languages.Czech, CzechHelper.GetCzechScore(Str));
            }

            if (TurkishHelper.IsProbablyTurkish(Str))
            {
                OneDetect.Add(Languages.Turkish, TurkishHelper.GetTurkishScore(Str));
            }

            if (HindiHelper.IsProbablyHindi(Str))
            {
                OneDetect.Add(Languages.Hindi, HindiHelper.GetHindiScore(Str));
            }
            else
            if (UrduHelper.IsProbablyUrdu(Str))
            {
                OneDetect.Add(Languages.Urdu, UrduHelper.GetUrduScore(Str));
            }

            if (IndonesianHelper.IsProbablyIndonesian(Str))
            {
                OneDetect.Add(Languages.Indonesian, IndonesianHelper.GetIndonesianScore(Str));
            }

            if (VietnameseHelper.IsProbablyVietnamese(Str))
            {
                OneDetect.Add(Languages.Vietnamese, VietnameseHelper.GetVietnameseScore(Str));
            }

            if (ThaiHelper.IsProbablyThai(Str))
            {
                OneDetect.Add(Languages.Thai, ThaiHelper.GetThaiScore(Str));
            }

            if (PersianHelper.IsProbablyPersian(Str))
            {
                OneDetect.Add(Languages.Persian, PersianHelper.GetPersianScore(Str));
            }

            if (OneDetect.Array.Count == 0)
            {
                OneDetect.Add(Languages.English);
            }
        }
        public static Languages DetectLanguageByLine(string String)
        {
            LanguageDetector OneDetect = new LanguageDetector();

            DetectLanguage(ref OneDetect, String);
            return OneDetect.GetMaxLang();
        }
        public static Languages DetectLanguageByContent(string Text)
        {
            LanguageDetector OneDetect = new LanguageDetector();

            foreach (var GetLine in Text.Split(new char[2] { '\r', '\n' }))
            {
                if (GetLine.Trim().Length > 0)
                {
                    DetectLanguage(ref OneDetect, GetLine);
                }
            }

            return OneDetect.GetMaxLang();
        }
    }
}
