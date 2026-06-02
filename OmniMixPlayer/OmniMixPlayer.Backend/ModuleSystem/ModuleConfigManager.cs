using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using OmniMixPlayer.SDK.Interfaces;

namespace OmniMixPlayer.Backend.ModuleSystem
{
    public class ModuleConfigManager : IModuleConfigManager
    {
        private readonly string _configPath;
        private Dictionary<string, JsonElement> _values;

        public string ModuleId { get; }

        public ModuleConfigManager(string moduleId, string configDirectory)
        {
            ModuleId = moduleId ?? throw new ArgumentNullException(nameof(moduleId));
            _configPath = Path.Combine(configDirectory, $"{moduleId}.json");
            _values = LoadConfig();
        }

        private Dictionary<string, JsonElement> LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    using var document = JsonDocument.Parse(json);
                    var result = new Dictionary<string, JsonElement>();
                    if (document.RootElement.ValueKind != JsonValueKind.Object)
                        return result;

                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        result[property.Name] = property.Value.Clone();
                    }
                    return result;
                }
            }
            catch { }
            return new Dictionary<string, JsonElement>();
        }

        public T GetValue<T>(string key, T defaultValue = default)
        {
            if (_values.TryGetValue(key, out var element))
            {
                try
                {
                    var targetType = typeof(T);
                    if (targetType == typeof(string))
                        return (T)(object)(element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString());
                    if (targetType == typeof(bool) && (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False))
                        return (T)(object)element.GetBoolean();
                    if (targetType == typeof(int) && element.TryGetInt32(out var intValue))
                        return (T)(object)intValue;
                    if (targetType == typeof(long) && element.TryGetInt64(out var longValue))
                        return (T)(object)longValue;
                    if (targetType == typeof(double) && element.TryGetDouble(out var doubleValue))
                        return (T)(object)doubleValue;
                    if (targetType == typeof(float) && element.TryGetSingle(out var floatValue))
                        return (T)(object)floatValue;
                    if (targetType == typeof(JsonElement))
                        return (T)(object)element.Clone();
                }
                catch { }
            }
            return defaultValue;
        }

        public void SetValue<T>(string key, T value)
        {
            _values[key] = ToJsonElement(value);
        }

        public string GetString(string key, string defaultValue = "")
        {
            if (_values.TryGetValue(key, out var element) && element.ValueKind == JsonValueKind.String)
                return element.GetString() ?? defaultValue;
            return defaultValue;
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            if (_values.TryGetValue(key, out var element) && element.ValueKind == JsonValueKind.Number)
            {
                if (element.TryGetInt32(out var val)) return val;
            }
            return defaultValue;
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            if (_values.TryGetValue(key, out var element))
            {
                if (element.ValueKind == JsonValueKind.True) return true;
                if (element.ValueKind == JsonValueKind.False) return false;
            }
            return defaultValue;
        }

        public bool HasKey(string key) => _values.ContainsKey(key);

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using var stream = File.Create(_configPath);
                using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
                writer.WriteStartObject();
                foreach (var pair in _values)
                {
                    writer.WritePropertyName(pair.Key);
                    pair.Value.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            catch { }
        }

        private static JsonElement ToJsonElement<T>(T value)
        {
            if (value is null)
                return ParseElement("null");
            if (value is JsonElement element)
                return element.Clone();
            if (value is bool boolValue)
                return ParseElement(boolValue ? "true" : "false");
            if (value is string stringValue)
                return ParseElement($"\"{JsonEncodedText.Encode(stringValue)}\"");
            if (value is int intValue)
                return ParseElement(intValue.ToString(CultureInfo.InvariantCulture));
            if (value is long longValue)
                return ParseElement(longValue.ToString(CultureInfo.InvariantCulture));
            if (value is double doubleValue)
                return ParseElement(doubleValue.ToString("R", CultureInfo.InvariantCulture));
            if (value is float floatValue)
                return ParseElement(floatValue.ToString("R", CultureInfo.InvariantCulture));
            if (value is decimal decimalValue)
                return ParseElement(decimalValue.ToString(CultureInfo.InvariantCulture));

            return ParseElement($"\"{JsonEncodedText.Encode(value.ToString() ?? string.Empty)}\"");
        }

        private static JsonElement ParseElement(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }
}
