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
        public static string GenerateTranslationPrompt(Languages From, Languages To, string TextToTranslate, string CategoryType, List<string> TerminologyReferences, List<string> CustomWords, string AdditionalInstructions)
        {
            if (CategoryType == "Papyrus" || CategoryType == "MCM")
            {
                CategoryType = string.Empty;
            }

            var Prompt = new System.Text.StringBuilder();

            // Main Role and Instructions
            Prompt.AppendLine("You are a professional translation AI. Your task is to provide only the translated text, with no additional explanation, reasoning, or commentary.");

            if (From == Languages.Auto)
            {
                Prompt.AppendLine("Translate the following text to " + LanguageHelper.ToLanguageCode(To) + ". The source language will be automatically detected.");
            }
            else
            {
                Prompt.AppendLine($"Translate the following text from {LanguageHelper.ToLanguageCode(From)} to {LanguageHelper.ToLanguageCode(To)}.");
            }

            // Direct instruction to exclude extra information
            Prompt.AppendLine("Respond ONLY with the translated content. Do not include any explanations, reasoning, or additional comments. The response must only contain the translation, and no other text.");
            Prompt.AppendLine("The category is a broad context type (e.g., related to NPCs, weapons, etc.), but it is NOT a specific entity label.");

            //Check if there are paired $$Word$$ placeholders present.
            if (!string.IsNullOrWhiteSpace(TextToTranslate))
            {
                var Regex = new System.Text.RegularExpressions.Regex(@"\$\$(.+?)\$\$");
                if (Regex.IsMatch(TextToTranslate))
                {
                    Prompt.AppendLine();
                    Prompt.AppendLine("[Important Placeholder Rule]");
                    Prompt.AppendLine("The text contains placeholders using the format $$...$$.");
                    Prompt.AppendLine();
                    Prompt.AppendLine("You must strictly obey the following rules:");
                    Prompt.AppendLine();
                    Prompt.AppendLine("1. A placeholder always begins with two dollar signs ($$) and ends with two dollar signs ($$).");
                    Prompt.AppendLine("   Example: $$Name$$ , $$ItemId$$ , $$ Some Text $$");
                    Prompt.AppendLine();
                    Prompt.AppendLine("2. You MUST NOT translate, modify, rewrite, lowercase, uppercase, or alter ANYTHING inside $$...$$.");
                    Prompt.AppendLine("   Keep the inside EXACTLY as the original, including letters, symbols, numbers, and spaces.");
                    Prompt.AppendLine();
                    Prompt.AppendLine("3. You MUST NOT remove or merge dollar signs.");
                    Prompt.AppendLine("   \"$$\" must stay exactly \"$$\".");
                    Prompt.AppendLine();
                    Prompt.AppendLine("4. Every placeholder that exists in the input MUST appear exactly once in the output.");
                    Prompt.AppendLine("   Do NOT delete, omit, or duplicate placeholders.");
                    Prompt.AppendLine();
                    Prompt.AppendLine("5. You may change the position of the placeholders ONLY when needed for grammar,");
                    Prompt.AppendLine("   but the total number of placeholders MUST remain unchanged.");
                    Prompt.AppendLine();
                    Prompt.AppendLine("6. Placeholders are NOT equations, NOT math, NOT Markdown.");
                    Prompt.AppendLine("   Treat $$...$$ as plain text tokens, not formatting.");
                }
            }

            // Optional Context Category
            if (!string.IsNullOrWhiteSpace(CategoryType))
            {
                Prompt.AppendLine("\n[Optional: Context Category]");
                Prompt.AppendLine($"Category: {CategoryType}");
            }

            // Custom Words section
            if (CustomWords != null && CustomWords.Count > 0)
            {
                Prompt.AppendLine("For the words listed under [Custom Words], use the exact provided translation.");
                Prompt.AppendLine("\n[Custom Words]");
                foreach (var Word in CustomWords)
                {
                    Prompt.AppendLine($"- {Word}");
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
            Prompt.AppendLine("Respond strictly with: {\"translation\": \"....\"}");
            
            return Prompt.ToString();
        }
    }
}
