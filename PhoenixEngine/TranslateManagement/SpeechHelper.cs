
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PhoenixEngine.TranslateCore;

namespace PhoenixEngine.TranslateManagement
{
    public static class SpeechHelper
    {
        private static readonly object VoiceLock = new object();
        private static dynamic VoiceInstance = null;

        private static readonly Dictionary<Languages, string[]> VoiceHints = new Dictionary<Languages, string[]>()
    {
        { Languages.English, new[] { "English", "David", "Zira", "George" } },
        { Languages.SimplifiedChinese, new[] { "Chinese", "Huihui", "Zh-cn" } },
        { Languages.TraditionalChinese, new[] { "Chinese (Traditional)", "Zh-hk", "Zh-tw" } },
        { Languages.Japanese, new[] { "Japanese", "Haruka", "Ja-jp" } },
        { Languages.German, new[] { "German", "De-de" } },
        { Languages.Korean, new[] { "Korean", "Heami", "Ko-kr" } },
        { Languages.Turkish, new[] { "Turkish", "Tr-tr" } },
        { Languages.Brazilian, new[] { "Portuguese", "Pt-br" } },
        { Languages.Russian, new[] { "Russian", "Ru-ru" } },
        { Languages.Italian, new[] { "Italian", "It-it" } },
        { Languages.Spanish, new[] { "Spanish", "Es-es" } },
        { Languages.Hindi, new[] { "Hindi", "Hi-in" } },
        { Languages.Urdu, new[] { "Urdu", "Ur-pk" } },
        { Languages.Indonesian, new[] { "Indonesian", "Id-id" } }
    };

        public static void TryPlaySound(string Text,bool CanCreatTrd = false)
        {
            Action PlaySoundAction = new Action(() => {
                try
                {
                    Languages Lang = LanguageHelper.DetectLanguageByLine(Text);
                    lock (VoiceLock)
                    {
                        if (VoiceInstance == null)
                        {
                            Type VoiceType = Type.GetTypeFromProgID("SAPI.SpVoice");
                            VoiceInstance = Activator.CreateInstance(VoiceType);
                            VoiceInstance.Volume = 100;
                            VoiceInstance.Rate = 0;
                        }

                        dynamic Voices = VoiceInstance.GetVoices();
                        dynamic BestMatch = null;

                        if (VoiceHints.TryGetValue(Lang, out var Hints))
                        {
                            foreach (dynamic Token in Voices)
                            {
                                string Desc = Token.GetDescription().ToString();
                                string LangAttr = Token.GetAttribute("Language")?.ToString() ?? "";

                                foreach (var Hint in Hints)
                                {
                                    if (Desc.IndexOf(Hint, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    LangAttr.IndexOf(Hint, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        BestMatch = Token;
                                        break;
                                    }
                                }

                                if (BestMatch != null)
                                    break;
                            }
                        }

                        if (BestMatch != null)
                            VoiceInstance.Voice = BestMatch;

                        VoiceInstance.Speak("", 2); // Purge before speak
                        VoiceInstance.Speak(Text, 1); // Async speak
                    }
                }
                catch
                {
                }
            });

            if (!CanCreatTrd)
            {
                PlaySoundAction.Invoke();
            }
            else
            {
                new Thread(() => {
                    PlaySoundAction.Invoke();
                }).Start();
            }
        }
    }
}
