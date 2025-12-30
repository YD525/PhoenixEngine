using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;

namespace PhoenixEngine.PlatformManagement
{
    public class AIPrompt
    {
        public static string GenerateTranslationPrompt(Languages From, Languages To, string TextToTranslate, string CategoryType, List<string> TerminologyReferences, List<ReplaceTag> CustomWords, string AdditionalInstructions)
        {
            if (CategoryType == "Papyrus" || CategoryType == "MCM")
            {
                CategoryType = string.Empty;
            }

            var Prompt = new System.Text.StringBuilder();

            Prompt.AppendLine($"\n<!-- Request ID: {DateTime.UtcNow.Ticks.GetHashCode().ToString().Replace("-","_")} -->");

            // Main Role and Instructions
            Prompt.AppendLine("You are a professional translation AI. Your task is to provide only the translated text, with no additional explanation, reasoning, or commentary.");

            if (From == Languages.Auto)
            {
                Prompt.AppendLine("Translate the following text to " + To.ToString() + ". The source language will be automatically detected.");
            }
            else
            {
                Prompt.AppendLine($"Translate the following text from {From.ToString()} to {To.ToString()}.");
            }

            // Direct instruction to exclude extra information
            Prompt.AppendLine("Respond ONLY with the translated content. Do not include any explanations, reasoning, or additional comments. The response must only contain the translation, and no other text.");
            Prompt.AppendLine("The category is a broad context type (e.g., related to NPC_,ARMO, etc.), but it is NOT a specific entity label.");

            // Optional Context Category
            if (!string.IsNullOrWhiteSpace(CategoryType))
            {
                Prompt.AppendLine("\n[Optional: Context Category]");
                Prompt.AppendLine($"Category: {CategoryType}");
            }

            // Custom Words section
            if (CustomWords != null && CustomWords.Count > 0)
            {
                Prompt.AppendLine("[Placeholder Rule]");
                foreach (var GetWord in CustomWords)
                {
                    Prompt.AppendLine($"{GetWord.Key} //meaning: {GetWord.Value}");
                }
            }

            // Terminology References section
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

            // Response Format section
            Prompt.AppendLine("\n[Response Format]");
            Prompt.AppendLine("If you cannot translate, do not return any content; return empty JSON instead: {\"translation\": \"\"}");
            Prompt.AppendLine("Respond strictly with: {\"translation\": \"....\"}");

            return Prompt.ToString();
        }
    }
}
