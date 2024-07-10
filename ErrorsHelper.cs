using System.Net;

namespace YaGpt
{
    public static class ErrorsHelper
    {
        public class HttpSimpleException : Exception
        {
            public HttpStatusCode Status { get; private set; }
            public HttpSimpleException(string text, HttpStatusCode status)
                : base(text)
            {
                Status = status;
            }

            public HttpSimpleException(HttpStatusCode status)
                : base(status.ToString())
            {
                Status = status;
            }
        }

        public class SqlException : Exception
        {

            public SqlException(string text, Exception e = null)
                : base($"{e.Message}\n{text}", e?.InnerException)
            {
            }

        }

        public static Exception DefaultException => new Exception("Empty exception");
    }
}
