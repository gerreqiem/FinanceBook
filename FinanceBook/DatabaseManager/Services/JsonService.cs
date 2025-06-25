using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace DatabaseManager.Services
{
    public class JsonService
    {
        public async Task ExportToJsonAsync<T>(string filePath, T data)
        {
            try
            {
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    TypeNameHandling = TypeNameHandling.None,
                    ContractResolver = new DefaultContractResolver
                    {
                        NamingStrategy = new CamelCaseNamingStrategy()
                    }
                };
                string json = JsonConvert.SerializeObject(data, Formatting.Indented, settings);
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting to JSON: {ex.Message}");
                throw;
            }
        }

        public async Task<T> ImportFromJsonAsync<T>(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File not found", filePath);

                string json = await File.ReadAllTextAsync(filePath);

                // Добавляем настройки для корректной десериализации
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    Converters = { new DecimalConverter() } // Для обработки decimal
                };

                return JsonConvert.DeserializeObject<T>(json, settings);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing from JSON: {ex.Message}");
                throw;
            }
        }

        // Добавляем специальный конвертер для decimal
        public class DecimalConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(decimal) || objectType == typeof(decimal?);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                    return null;

                if (reader.TokenType == JsonToken.Integer || reader.TokenType == JsonToken.Float)
                    return Convert.ToDecimal(reader.Value);

                if (reader.TokenType == JsonToken.String)
                {
                    if (string.IsNullOrEmpty((string)reader.Value))
                        return null;
                    return decimal.Parse((string)reader.Value, CultureInfo.InvariantCulture);
                }

                throw new JsonSerializationException($"Unexpected token type: {reader.TokenType}");
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteValue(value);
            }
        }
    }
}