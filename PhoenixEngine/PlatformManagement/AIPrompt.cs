using System;
using System.Collections.Generic;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;

namespace PhoenixEngine.PlatformManagement
{
    public class AIPrompt
    {
        public static string GenerateTranslationPrompt(Languages From, Languages To, string TextToTranslate,List<string> TerminologyReferences, List<ReplaceTag> CustomWords, string AdditionalInstructions)
        {
            var Prompt = new System.Text.StringBuilder();

            Prompt.AppendLine($"<!-- Request ID: {DateTime.UtcNow.Ticks.GetHashCode().ToString().Replace("-", "_")} -->");

            // Role & Core Rules
            Prompt.AppendLine(
            "You are a professional translation AI.\n" +
            "This is a structure-preserving HTML translation task.\n" +
            "Translate ONLY the inner text of each <li id='...'>...</li> element.\n" +
            "Do NOT modify, remove, rename, reorder, or regenerate any <li> tags or their id attributes.\n" +
            "The id attribute is a positional identifier used by the program and MUST be preserved verbatim.\n" +
            "Removing or altering any id attribute is considered INVALID output.\n" +
            "The original HTML structure MUST be preserved exactly."
            );

            // Language direction
            if (From == Languages.Auto)
            {
                Prompt.AppendLine(
                    $"Translate the following text to {LanguageHelper.ToLanguageCode(To)}. " +
                    "The source language will be automatically detected."
                );
            }
            else
            {
                Prompt.AppendLine(
                    $"Translate the following text from {LanguageHelper.ToLanguageCode(From)} " +
                    $"to {LanguageHelper.ToLanguageCode(To)}."
                );
            }

            // Output restriction
            Prompt.AppendLine(
            "Output ONLY the translated HTML.\n" +
            "Do NOT add explanations, comments, headers, or any extra text.\n" +
            "The response MUST consist solely of valid HTML with all <li> elements and id attributes preserved."
            );

            // Custom placeholders
            if (CustomWords != null && CustomWords.Count > 0)
            {
                Prompt.AppendLine("[Placeholder Rule]");
                Prompt.AppendLine(
                    "Placeholders represent protected content.\n" +
                    "They MUST be preserved exactly.\n" +
                    "You may only reorder placeholders if required for natural sentence flow."
                );

                foreach (var GetWord in CustomWords)
                {
                    Prompt.AppendLine($"{GetWord.Key} // meaning: {GetWord.Value}");
                }
            }

            // Terminology
            if (TerminologyReferences != null && TerminologyReferences.Count > 0)
            {
                Prompt.AppendLine("[Terminology References]");
                foreach (var Reference in TerminologyReferences)
                {
                    Prompt.AppendLine($"- {Reference}");
                }
            }

            // Text input
            Prompt.AppendLine("[Text to Translate]");
            Prompt.AppendLine("```html");
            Prompt.AppendLine(TextToTranslate);
            Prompt.AppendLine("```");

            return Prompt.ToString();
        }
    }
}
