using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.TranslateCore;

namespace PhoenixEngine.PlatformManagement
{
    public class AIPrompt
    {
        public static string GenerateTranslationPrompt(Languages From,Languages To,string TextToTranslate,string CategoryType,List<string> TerminologyReferences, List<string> CustomWords,string AdditionalInstructions)
        {
            var Prompt = new System.Text.StringBuilder();

            // Main Role and Instructions
            Prompt.AppendLine("You are a professional translation AI.");
            Prompt.AppendLine($"Translate the following text from {LanguageHelper.ToLanguageCode(From)} to {LanguageHelper.ToLanguageCode(To)}.");
            Prompt.AppendLine("Respond ONLY with the translated content. Do not include any explanations or comments.");
            Prompt.AppendLine("The category is a broad context type (e.g., related to NPCs, weapons, etc.), not a specific entity label.");

            // Optional Context Category
            if (!string.IsNullOrWhiteSpace(CategoryType))
            {
                Prompt.AppendLine("\n[Optional: Context Category]");
                Prompt.AppendLine($"Category: {CategoryType}");
            }

            // New: Custom Words
            if (CustomWords != null && CustomWords.Count > 0)
            {
                Prompt.AppendLine("For the words listed under [Custom Words], use the exact provided translation.");
                Prompt.AppendLine("\n[Custom Words]");
                foreach (var Word in CustomWords)
                {
                    Prompt.AppendLine($"- {Word}");
                }
            }

            // Optional Terminology References
            if (TerminologyReferences != null && TerminologyReferences.Count > 0)
            {
                Prompt.AppendLine("\n[Terminology References]");
                foreach (var Reference in TerminologyReferences)
                {
                    Prompt.AppendLine($"- {Reference}");
                }
            }

            // Main Text to Translate
            Prompt.AppendLine("\n[Text to Translate]");
            Prompt.AppendLine("\"\"\"");
            Prompt.AppendLine(TextToTranslate);
            Prompt.AppendLine("\"\"\"");

            // Additional Instructions (Custom Parameter)
            if (!string.IsNullOrWhiteSpace(AdditionalInstructions))
            {
                Prompt.AppendLine($"\n{AdditionalInstructions}");
            }

            // Response Format
            Prompt.AppendLine("\n[Response Format]");
            Prompt.AppendLine("Respond strictly with: {\"translation\": \"....\"}");
            Prompt.AppendLine("The value must contain only translated text.");

            return Prompt.ToString();
        }
    }
}
