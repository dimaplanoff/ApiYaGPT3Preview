using System.Net;

namespace YaGpt.Sql
{
	public static class Templates
	{
        public class LogItem
        {
            public LogItem()
            {
                request_date = DateTime.Now;
            }
            public DateTime? request_date { get; set; }
            public string request { get; set; }
            public string response { get; set; }
            public string uri { get; set; }
            public string sid { get; set; }
            public int status { get; set; }

            public void Write(SQL.ITConnection sqlEntry)
            {
                SQL.ExecuteCommand(sqlEntry, "GPT_PKG.WRITE",
                    "p_request_date", request_date,
                    "p_request", request,
                    "p_response", response,
                    "p_uri", uri,
                    "p_sid", sid,
                    "p_status", status
                    );
            }
        }
    }
}
