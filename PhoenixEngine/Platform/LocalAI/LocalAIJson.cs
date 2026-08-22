using System.Collections.Generic;

namespace PhoenixEngine.Platform.LocalAI
{
    /// <summary>Represents the bounded model list returned by an OpenAI-compatible local provider.</summary>
    internal sealed class OpenAIModelListResponse
    {
        /// <summary>Contains the provider's advertised models.</summary>
        public OpenAIModel[] data { get; set; }
    }

    /// <summary>Identifies one model returned by an OpenAI-compatible local provider.</summary>
    internal sealed class OpenAIModel
    {
        /// <summary>Contains the provider-specific model identifier.</summary>
        public string id { get; set; }
    }

    public class OpenAIItem
    {
        public string model { get; set; }
        public bool store { get; set; }
        public List<OpenAIMessage> messages { get; set; } = new List<OpenAIMessage>();

        public OpenAIItem(string model)
        {
            this.model = model;
        }
    }

    public class OpenAIMessage
    {
        public string role { get; set; }
        public string content { get; set; }

        public OpenAIMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }

    public class OpenAIResponse
    {
        public string id { get; set; }
        public string _object { get; set; }
        public int created { get; set; }
        public string model { get; set; }
        public OpenAIChoice[] choices { get; set; }
        public OpenAIUsage usage { get; set; }
        public OpenAIStats stats { get; set; }
        public string system_fingerprint { get; set; }
    }

    public class OpenAIUsage
    {
        public int prompt_tokens { get; set; }
        public int completion_tokens { get; set; }
        public int total_tokens { get; set; }
    }

    public class OpenAIStats
    {
    }

    public class OpenAIChoice
    {
        public int index { get; set; }
        public object logprobs { get; set; }
        public string finish_reason { get; set; }
        public OpenAIRMessage message { get; set; }
    }

    public class OpenAIRMessage
    {
        public string role { get; set; }
        public string content { get; set; }
    }
}
