using PhoenixEngine.Engine;
using PhoenixEngine.Language;
using System.Collections.Generic;
using System;
using System.Linq;

public class AIPrompt
{
    public static string GenerateTranslationPrompt(Languages From, Languages To, string TextToTranslate, List<string> TerminologyReferences, List<ReplaceTag> CustomWords, string AdditionalInstructions)
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
                $"Translate the following text to {P_Language.ToLanguageCode(To)}. " +
                "The source language will be automatically detected."
            );
        }
        else
        {
            Prompt.AppendLine(
                $"Translate the following text from {P_Language.ToLanguageCode(From)} " +
                $"to {P_Language.ToLanguageCode(To)}."
            );
        }

        Prompt.AppendLine(
        "Output ONLY the translated HTML.\n"
        );

        bool HasEmotion = TextToTranslate.Contains("data-emotion");

        if (HasEmotion)
        {
            Prompt.AppendLine(
            "[Emotion Context]\n" +
            "The 'data-emotion' attribute represents the NPC's emotional state when speaking. Use this as a tone reference for translation.\n"
            );
        }

        var ForcedTags = CustomWords?.Where(t => !t.IsHint).ToList() ?? new List<ReplaceTag>();
        var HintTags = CustomWords?.Where(t => t.IsHint).ToList() ?? new List<ReplaceTag>();

        if (HintTags.Count > 0)
        {
            var Seen = new HashSet<string>();
            HintTags = HintTags
                .Where(t => Seen.Add($"{t.Key}|{t.Value}"))
                .ToList();
        }

        if (ForcedTags.Count > 0)
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
            foreach (var GetWord in ForcedTags)
            {
                Prompt.AppendLine($"{GetWord.Key} // meaning: {GetWord.Value}");
            }
            Prompt.AppendLine();
        }

        if (HintTags.Count > 0)
        {
            Prompt.AppendLine("[User Defined Terms - For Reference Only]");
            Prompt.AppendLine("The following are user-provided term suggestions. You may use them if appropriate, but they are not mandatory.");

            foreach (var GetWord in HintTags)
            {
                Prompt.AppendLine($"- {GetWord.Key} → {GetWord.Value}");
            }
            Prompt.AppendLine();
        }

        if (TerminologyReferences != null && TerminologyReferences.Count > 0)
        {
            var Seen = new HashSet<string>();
            TerminologyReferences = TerminologyReferences
                .Where(refs => Seen.Add(refs))
                .ToList();

            Prompt.AppendLine("[Possible References]");
            Prompt.AppendLine("These are system-retrieved terms that may or may not be related to the current text. Review and decide for yourself - use them only if they actually fit the context, ignore if they seem irrelevant.");

            foreach (var Reference in TerminologyReferences)
            {
                Prompt.AppendLine($"- {Reference}");
            }
            Prompt.AppendLine();
        }

        Prompt.AppendLine("[Html to Translate]");
        Prompt.AppendLine(TextToTranslate);

        return Prompt.ToString();
    }

    public static string GenerateTranslationPromptSingle(Languages From, Languages To, string TextToTranslate, List<string> TerminologyReferences, List<ReplaceTag> CustomWords, string AdditionalInstructions)
    {
        var Prompt = new System.Text.StringBuilder();
        Prompt.AppendLine($"<!-- Request ID: {DateTime.UtcNow.Ticks.GetHashCode().ToString().Replace("-", "_")} -->");

        Prompt.AppendLine(
        "You are a professional translation AI.\n" +
        "Translate ONLY the given text. Output the translation and nothing else.\n"
        );

        if (From == Languages.Auto)
        {
            Prompt.AppendLine(
                $"Translate the following text to {P_Language.ToLanguageCode(To)}. " +
                "The source language will be automatically detected."
            );
        }
        else
        {
            Prompt.AppendLine(
                $"Translate the following text from {P_Language.ToLanguageCode(From)} " +
                $"to {P_Language.ToLanguageCode(To)}."
            );
        }

        Prompt.AppendLine(
        "Output ONLY the translated plain text. Do not add any explanation, notes, or formatting.\n"
        );

        bool HasEmotion = TextToTranslate.Contains("data-emotion");

        if (HasEmotion)
        {
            Prompt.AppendLine(
            "[Emotion Context]\n" +
            "The 'data-emotion' attribute represents the NPC's emotional state when speaking. Use this as a tone reference for translation.\n"
            );
        }

        var ForcedTags = CustomWords?.Where(t => !t.IsHint).ToList() ?? new List<ReplaceTag>();
        var HintTags = CustomWords?.Where(t => t.IsHint).ToList() ?? new List<ReplaceTag>();

        if (HintTags.Count > 0)
        {
            var Seen = new HashSet<string>();
            HintTags = HintTags
                .Where(t => Seen.Add($"{t.Key}|{t.Value}"))
                .ToList();
        }

        if (ForcedTags.Count > 0)
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
            foreach (var GetWord in ForcedTags)
            {
                Prompt.AppendLine($"{GetWord.Key} // meaning: {GetWord.Value}");
            }
            Prompt.AppendLine();
        }

        if (HintTags.Count > 0)
        {
            Prompt.AppendLine("[User Defined Terms - For Reference Only]");
            Prompt.AppendLine("The following are user-provided term suggestions. You may use them if appropriate, but they are not mandatory.");

            foreach (var GetWord in HintTags)
            {
                Prompt.AppendLine($"- {GetWord.Key} → {GetWord.Value}");
            }
            Prompt.AppendLine();
        }

        if (TerminologyReferences != null && TerminologyReferences.Count > 0)
        {
            var Seen = new HashSet<string>();
            TerminologyReferences = TerminologyReferences
                .Where(refs => Seen.Add(refs))
                .ToList();

            Prompt.AppendLine("[Possible References]");
            Prompt.AppendLine("These are system-retrieved terms that may or may not be related to the current text. Review and decide for yourself - use them only if they actually fit the context, ignore if they seem irrelevant.");

            foreach (var Reference in TerminologyReferences)
            {
                Prompt.AppendLine($"- {Reference}");
            }
            Prompt.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(AdditionalInstructions))
        {
            Prompt.AppendLine("[Additional Instructions]");
            Prompt.AppendLine(AdditionalInstructions);
        }

        Prompt.AppendLine("[Text to Translate]");
        Prompt.AppendLine(TextToTranslate);

        return Prompt.ToString();
    }
}