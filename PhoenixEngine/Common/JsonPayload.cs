using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace PhoenixEngine.Common
{
    /// <summary>
    /// Parses untrusted JSON with product-wide size, depth, and type-materialization limits.
    /// </summary>
    internal static class JsonPayload
    {
        internal const int MaximumDepth = 64;
        internal const int MaximumDocumentBytes = 8 * 1024 * 1024;
        internal const int MaximumDocumentCharacters = MaximumDocumentBytes;

        /// <summary>Deserializes and validates one bounded JSON document.</summary>
        /// <typeparam name="T">The expected document type.</typeparam>
        /// <param name="json">The untrusted JSON document.</param>
        /// <param name="validator">The structural validation applied after deserialization.</param>
        /// <param name="value">Receives the validated value, or the default value on failure.</param>
        /// <returns><c>true</c> when the document is bounded, valid JSON, and structurally valid.</returns>
        internal static bool TryDeserialize<T>(string json, Func<T, bool> validator, out T value)
        {
            value = default(T);
            if (string.IsNullOrWhiteSpace(json) ||
                json.Length > MaximumDocumentCharacters ||
                validator == null)
            {
                return false;
            }

            try
            {
                using (StringReader stringReader = new StringReader(json))
                using (JsonTextReader jsonReader = new JsonTextReader(stringReader)
                {
                    DateParseHandling = DateParseHandling.None,
                    MaxDepth = MaximumDepth
                })
                {
                    JsonSerializer serializer = new JsonSerializer
                    {
                        CheckAdditionalContent = true,
                        MaxDepth = MaximumDepth,
                        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                        MissingMemberHandling = MissingMemberHandling.Ignore,
                        TypeNameHandling = TypeNameHandling.None
                    };
                    T parsed = serializer.Deserialize<T>(jsonReader);
                    if (!validator(parsed))
                        return false;

                    value = parsed;
                    return true;
                }
            }
            catch (JsonException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>Parses one bounded JSON token tree without enabling runtime type metadata.</summary>
        /// <param name="json">The untrusted JSON document.</param>
        /// <param name="value">Receives the parsed token tree, or <c>null</c> on failure.</param>
        /// <returns><c>true</c> when the document is bounded and valid.</returns>
        internal static bool TryParseToken(string json, out JToken value)
        {
            return TryDeserialize(json, token => token != null, out value);
        }

        /// <summary>Validates one bounded JSON document without exposing its token tree.</summary>
        /// <param name="json">The untrusted JSON document.</param>
        /// <returns><c>true</c> when the document is bounded and valid.</returns>
        internal static bool IsValidDocument(string json)
        {
            JToken value;
            return TryParseToken(json, out value);
        }
    }
}
