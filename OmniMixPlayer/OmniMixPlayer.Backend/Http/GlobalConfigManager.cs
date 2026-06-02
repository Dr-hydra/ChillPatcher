using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace OmniMixPlayer.Backend.Http
{
    public class GlobalConfigManager
    {
        private readonly string _configPath;
        private Dictionary<string, JsonElement> _config;

        /// <summary>
        /// Raised after Save() completes, so callers (e.g. Program) can react
        /// to config changes without restarting the process.
        /// </summary>
        public Action OnConfigSaved;

        public GlobalConfigManager(string configDir)
        {
            _configPath = Path.Combine(configDir, "global_config.json");
            _config = new Dictionary<string, JsonElement>();
            Load();
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var doc = JsonDocument.Parse(json);
                    _config = new Dictionary<string, JsonElement>();
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        _config[prop.Name] = prop.Value.Clone();
                    }
                }
            }
            catch { }
        }

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
                foreach (var kv in _config)
                {
                    writer.WritePropertyName(kv.Key);
                    kv.Value.WriteTo(writer);
                }
                writer.WriteEndObject();
                OnConfigSaved?.Invoke();
            }
            catch { }
        }

        private object ConvertElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String: return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out var i)) return i;
                    if (element.TryGetDouble(out var d)) return d;
                    return element.GetRawText();
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                default: return element.GetRawText();
            }
        }

        public T GetValue<T>(string key, T defaultValue = default)
        {
            if (_config.TryGetValue(key, out var elem))
            {
                try
                {
                    var targetType = typeof(T);
                    if (targetType == typeof(string))
                        return (T)(object)(elem.ValueKind == JsonValueKind.String ? elem.GetString() : elem.ToString());
                    if (targetType == typeof(bool) && (elem.ValueKind == JsonValueKind.True || elem.ValueKind == JsonValueKind.False))
                        return (T)(object)elem.GetBoolean();
                    if (targetType == typeof(int) && elem.TryGetInt32(out var intValue))
                        return (T)(object)intValue;
                    if (targetType == typeof(long) && elem.TryGetInt64(out var longValue))
                        return (T)(object)longValue;
                    if (targetType == typeof(double) && elem.TryGetDouble(out var doubleValue))
                        return (T)(object)doubleValue;
                    if (targetType == typeof(float) && elem.TryGetSingle(out var floatValue))
                        return (T)(object)floatValue;
                    if (targetType == typeof(JsonElement))
                        return (T)(object)elem.Clone();
                }
                catch { }
            }
            return defaultValue;
        }

        public void SetValue<T>(string key, T value)
        {
            _config[key] = ToJsonElement(value);
        }

        public bool HasKey(string key) => _config.ContainsKey(key);

        public Dictionary<string, object> GetAll()
        {
            var result = new Dictionary<string, object>();
            foreach (var kv in _config)
            {
                result[kv.Key] = ConvertElement(kv.Value);
            }
            return result;
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
