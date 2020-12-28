using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;

namespace BigBang.Cosmos.EventStore.Serialization
{
    /// <summary>
    /// Custom cosmos JSON serializer implementation for System.Text.Json.
    /// </summary>
    public class TextJsonCosmosSerializer : CosmosSerializer
    {
        private readonly JsonSerializerOptions jsonSerializerOptions;

        public TextJsonCosmosSerializer()
            : this(null)
        {
        }

        public TextJsonCosmosSerializer(IEventTypeNameMapper? mapper)
        {
            jsonSerializerOptions = new JsonSerializerOptions
            {
                IgnoreNullValues = true,
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            if (mapper != null)
            {
                jsonSerializerOptions.Converters.Add(new EventConverter(mapper));
            }

            jsonSerializerOptions.Converters.Add(new TimeSpanConverter());
            jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        }

        [return:MaybeNull]
        public override T FromStream<T>(Stream stream)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using (stream)
            {
                if (stream.CanSeek && stream.Length == 0)
                {
                    return default;
                }

                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    return (T)(object)stream;
                }

                // Response data from cosmos always comes as a memory stream.
                // Note: This might change in v4, but so far it doesn't look like it.
                if (stream is MemoryStream memoryStream && memoryStream.TryGetBuffer(out ArraySegment<byte> buffer))
                {
                    return JsonSerializer.Deserialize<T>(buffer, jsonSerializerOptions);
                }

                return default;
            }
        }

        public override Stream ToStream<T>(T input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            var streamPayload = new MemoryStream();

            using var utf8JsonWriter = new Utf8JsonWriter(
                streamPayload,
                new JsonWriterOptions
                {
                    Indented = jsonSerializerOptions.WriteIndented,
                });

            JsonSerializer.Serialize(utf8JsonWriter, input, jsonSerializerOptions);
            streamPayload.Position = 0;

            return streamPayload;
        }
    }
}