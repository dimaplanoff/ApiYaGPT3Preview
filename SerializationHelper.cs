using System.Text.Encodings.Web;
using System.Text.Json;

namespace YaGpt
{
    public static class SerializationHelper
    {
        public static readonly JsonSerializerOptions jsonOptions = CreateSerializerOptions();
        private static JsonSerializerOptions CreateSerializerOptions()
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            options.WriteIndented = true;
            options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

            return options;
        }
    }
}
