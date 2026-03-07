using System;
using System.Collections.Generic;
using PhoenixEngine.Engine;
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
            "Translate ONLY the inner text of each <li data-unit-id='*'>...</li> element.\n" +
            "\n" +
            "The 'data-unit-id' is used to associate and update data. Do not modify or delete the primary key, as this will cause the update to fail or result in incorrect content.\n" +
            "The `<li data-unit-id='*'>...</li>` tags must correspond exactly to the original text; translate as many tags as there are in the original text.\n"
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
            "Output ONLY the translated HTML.\n"
            );

            // Custom placeholders
            if (CustomWords != null && CustomWords.Count > 0)
            {
                Prompt.AppendLine("[Placeholder Rule]");
                Prompt.AppendLine(
                    "Placeholders in the format [_N] or [_PN] represent protected content that must NOT be translated.\n" +
                    "Rules:\n" +
                    "1. DO NOT translate the placeholder itself - keep [_0], [_1], [_P0] etc. exactly as-is\n" +
                    "2. DO NOT translate the meaning shown after '//'\n" +
                    "3. You may ONLY reorder placeholders if required for natural sentence flow\n" +
                    "4. Preserve the exact format: brackets, underscore, and number must remain unchanged\n" +
                    "\n" +
                    "Examples:\n" +
                    "✓ Correct: \"Click [_0] to continue\" → \"Cliquez sur [_0] pour continuer\"\n" +
                    "✗ Wrong: \"Click [_0] to continue\" → \"Cliquez sur [bouton] pour continuer\"\n"
                );

                Prompt.AppendLine("Protected placeholders:");
                foreach (var GetWord in CustomWords)
                {
                    Prompt.AppendLine($"{GetWord.Key} // meaning: {GetWord.Value}");
                }
                Prompt.AppendLine();
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
            Prompt.AppendLine("[Html to Translate]");
            Prompt.AppendLine(TextToTranslate);

            return Prompt.ToString();
        }
    }
}
