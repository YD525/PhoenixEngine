
using PhoenixEngine.EngineManagement;
using PhoenixEngine.LanguageDetector;
using PhoenixEngine.PlatformManagement.LocalAI;
using PhoenixEngine.TranslateManage;
using static System.Net.Mime.MediaTypeNames;
using static PhoenixEngine.TranslateManage.TransCore;

namespace PhoenixEngine.TranslateCore
{
    // Copyright (c) 2025 YD525
    // Licensed under the MIT License.
    // See LICENSE file in the project root for full license information.
    //https://github.com/YD525/PhoenixEngine

    public enum Languages
    {
        Null = -2, English = 0, SimplifiedChinese = 1, Japanese = 2, German = 5, Korean = 6, Turkish = 7, Brazilian = 8, Russian = 9, TraditionalChinese = 10, Italian = 11, Spanish = 12, Hindi = 13, Urdu = 15, Indonesian = 16, French = 17, Vietnamese = 20, Polish = 22, CanadianFrench = 23, Portuguese = 25, Ukrainian = 26, Auto = 99
    }
    public static class LanguageConverter
    {
        private static readonly Dictionary<Languages, string> LanguageCodeMap = new()
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
            [Languages.Italian] = "it",
            [Languages.Spanish] = "es",
            [Languages.Hindi] = "hi",
            [Languages.Urdu] = "ur",
            [Languages.Indonesian] = "id",
            [Languages.French] = "fr",
            [Languages.CanadianFrench] = "fr-CA",
            [Languages.Vietnamese] = "vi",
            [Languages.Polish] = "pl",
            [Languages.Auto] = "auto",
            [Languages.Null] = ""
        };

        private static readonly Dictionary<string, Languages> CodeToLanguageMap = new(StringComparer.OrdinalIgnoreCase);
        static LanguageConverter()
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
    }

    public class LanguageHelper
    {
        public static string ToLanguageCode(Languages Lang)
        {
            return LanguageConverter.ToLanguageCode(Lang);
        }

        public static Languages FromLanguageCode(string Code)
        {
            Languages Lang = LanguageConverter.FromLanguageCode(Code);
            return Lang;
        }

        /// <summary>
        /// Uses AI to guess which language the given text belongs to,
        /// and converts the AI’s result into a <see cref="Languages"/> enum value.
        /// </summary>
        public static Languages AIGuess(string Source)
        {
            LMStudio NewLMAI = new LMStudio();

            var Names = Enum.GetNames(typeof(Languages))
                       .Where(n => n != nameof(Languages.Null) && n != nameof(Languages.Auto))
                       .ToArray();
            var EnumNames = string.Join(", ", Names);

            var Prompt = $"Identify which language the following text belongs to.\nOnly return one of the enum names from this list, and nothing else:\n{EnumNames}\nText: \"" + Source + "\"";

            var ReturnJson = "";
            var GetResult = NewLMAI.CallAI(Prompt, ref ReturnJson);

            string GetStr = "";

            if (GetResult != null)
            {
                if (GetResult.choices != null)
                {
                    if (GetResult.choices.Length > 0)
                    {
                        GetStr = GetResult.choices[0].message.content.Trim();
                    }
                    if (GetStr.Trim().Length > 0)
                    {
                        GetStr = JsonGeter.GetValue(GetStr);
                    }
                }
            }

            if (Enum.TryParse(typeof(Languages), GetStr, ignoreCase: true, out var Result))
            {
                return (Languages)Result;
            }

            return Languages.Null;
        }

        public static void DetectLanguage(ref LanguageDetect OneDetect, string Str)
        {
            if (string.IsNullOrWhiteSpace(Str))
                return;

            if (EnglishHelper.IsProbablyEnglish(Str)) //100%
            {
                OneDetect.Add(Languages.English,0.01);
            }

            if (RussianHelper.ContainsRussian(Str)) //100%
            {
                OneDetect.Add(Languages.Russian,0.02); 
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
                if (TraditionalChineseHelper.ContainsTraditionalChinese(Str))
                {
                    OneDetect.Add(Languages.TraditionalChinese, 1);
                }

                if (SimplifiedChineseHelper.ContainsSimplifiedChinese(Str))  //100%
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

            if (TurkishHelper.IsProbablyTurkish(Str))
            {
                OneDetect.Add(Languages.Turkish, TurkishHelper.GetTurkishScore(Str));
            }

            if (HindiHelper.IsProbablyHindi(Str))
            {
                OneDetect.Add(Languages.Hindi,HindiHelper.GetHindiScore(Str));
            }

            if (UrduHelper.IsProbablyUrdu(Str))
            {
                OneDetect.Add(Languages.Urdu,UrduHelper.GetUrduScore(Str));
            }

            if (IndonesianHelper.IsProbablyIndonesian(Str))
            {
                OneDetect.Add(Languages.Indonesian,IndonesianHelper.GetIndonesianScore(Str));
            }

            if (VietnameseHelper.IsProbablyVietnamese(Str))
            {
                OneDetect.Add(Languages.Vietnamese,VietnameseHelper.GetVietnameseScore(Str));
            }

            if (OneDetect.Array.Count == 0)
            {
                OneDetect.Add(Languages.English);
            }
        }

        public static Languages DetectLanguageByLine(string String)
        {
            LanguageDetect OneDetect = new LanguageDetect();
            DetectLanguage(ref OneDetect, String);
            return OneDetect.GetMaxLang();
        }

        public class FileLanguageDetect
        {
            LanguageDetect LanguageDetectItem = new LanguageDetect();

            public void DetectLanguageByFile(string Line)
            {
                DetectLanguage(ref LanguageDetectItem, Line);
            }

            public Languages GetLang()
            {
                return LanguageDetectItem.GetMaxLang();
            }
        }
       

        public static Languages DetectLanguageByContent(string Text)
        {
            LanguageDetect OneDetect = new LanguageDetect();

            foreach (var GetLine in Text.Split(new char[2] { '\r', '\n' }))
            {
                if (GetLine.Trim().Length > 0)
                {
                    DetectLanguage(ref OneDetect, GetLine);
                }
            }

            return OneDetect.GetMaxLang();
        }

        public class LanguageDetect
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

            public void Add(Languages Lang,double Ratio)
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

    }
}
