using System.Net;
using System.Text;
using System.Text.Json;
using static YaGpt.Model;
using static YaGpt.Sql.Templates;

namespace YaGpt
{
    public static class Generic
    {
        public static bool IsOk(this HttpStatusCode stat)
        {
            var i = (int)stat;
            return i >= 200 && i <= 299;
        }

        public static T NVL<T>(this T a, params T[] args)
        {
            var lst = new List<T> { a };
            lst.AddRange(args);

            foreach (var t in lst)
                if (!t.IsNull())
                    return t;

            return default;
        }

        public static bool IsNull(this object e)
        {
            if (e == null)
                return true;
            if (e is string)
                return string.IsNullOrEmpty(e.ToString());
            return false;
        }

        public static string ToStr<T>(this T obj, JsonSerializerOptions options = null)
        {
            if (obj == null)
                return "null";
            if (obj is string)
                return obj as string;

            return JsonSerializer.Serialize(obj, options ?? SerializationHelper.jsonOptions);
        }

        public static T ToTypedObject<T>(this string str, JsonSerializerOptions options = null)
        {
            if (!string.IsNullOrEmpty(str))
                return JsonSerializer.Deserialize<T>(str, options ?? SerializationHelper.jsonOptions);
            return default;
        }


        public static object ToTypedObject(this string str, Type type, JsonSerializerOptions options = null)
        {
            if (!string.IsNullOrEmpty(str))
                return JsonSerializer.Deserialize(str, type, options ?? SerializationHelper.jsonOptions);
            return default;
        }

        public static List<string> SplitNoEmpty(this string value, params char[] args) =>
            value.Split(args, StringSplitOptions.RemoveEmptyEntries)?.Where(m=>!m.IsNull()).ToList();


        public static string FullMessage(this Exception e)
        {
            StringBuilder sb = new();
            do
                sb.AppendLine(e.Message);
            while ((e = e?.InnerException) != null);
            return sb.ToString();
        }

        public static T PostBody<T>(this HttpContext context, out string raw_data)
        {

            if (context?.Request?.Method != HttpMethod.Post.ToString())
            {
                raw_data = string.Empty;
                throw new Exception("it is no POST request");
            }
            using (var sr = new StreamReader(context.Request.BodyReader.AsStream()))
            {
                raw_data = sr.ReadToEnd();
                return raw_data.ToTypedObject<T>();
            }
        }

        public static async Task WriteResult(this HttpContext context, SQL.ITConnection sqlEntry, LogItem log, string spec_message = null)
        {
            context.Response.StatusCode = log.status;
            await context.Response.WriteAsync(new { status = log.status.ToString(), message = spec_message ?? log.response }.ToStr());
            log.Write(sqlEntry);
        }  
        
        public static async Task WriteResult(this HttpContext context, HttpStatusCode status, Exception e)
        {
            context.Response.StatusCode = (int)status;
            await context.Response.WriteAsync(new { status = status.ToString(), message = e?.Message }.ToStr());
        }
    }
}
