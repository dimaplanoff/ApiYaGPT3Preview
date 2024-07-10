using Oracle.ManagedDataAccess.Client;
using System.Net;
using System.Text.RegularExpressions;



namespace YaGpt
{
    public class MiddlewareHandler
    {
        private RequestDelegate next;
        public MiddlewareHandler(RequestDelegate next)
        {
            this.next = next;
        }


        public async Task InvokeAsync(HttpContext context, SQL.ITConnection sqlEntry, HttpHelper.IWebHandlerClient httpClient, Sql.Templates.LogItem log)        
        {
            string spec_message = null;
            try
            {
                if (!context.Request.Path.HasValue)
                {
                    log.status = (int)HttpStatusCode.Forbidden;
                    await context.WriteResult(sqlEntry, log);
                    return;
                }

                var request_path = context.Request.Path.Value.ToLower();
                var segments = request_path.SplitNoEmpty('/');
                if (Regex.IsMatch(request_path, @"/\d{1,10}$") || Regex.IsMatch(request_path, @"/[a-zA-Z]{1,2}$"))
                {
                    var id = segments.Last();
                    segments.Remove(id);                 
                    context.Items["id"] = id;
                }

                switch (segments.Last())
                {
                    case "get-info-from-ai":
                        string uri = "https://llm.api....completion";
                        log.uri = uri;
                        var user_request = context.PostBody<Model.UserRequest>(out string raw_request);

                        if(string.IsNullOrEmpty(user_request.text))
                            throw new ErrorsHelper.HttpSimpleException("empty text", HttpStatusCode.BadRequest);
                        
                        if(string.IsNullOrEmpty(user_request.id_session))
                            throw new ErrorsHelper.HttpSimpleException("empty sid", HttpStatusCode.BadRequest);

                        log.request = raw_request;
                        log.sid = user_request.id_session;

                        var is_accepted = ((decimal)SQL.ExecuteCommand(sqlEntry, $"GPT_PKG.IS_ACCEPT('{user_request.id_session}')",
                            new OracleParameter { ParameterName = "result", OracleDbType = OracleDbType.Decimal, Direction = System.Data.ParameterDirection.ReturnValue })
                            ) == 1 ? true : false;
                        if (!is_accepted)
                            throw new ErrorsHelper.HttpSimpleException("daily limit", HttpStatusCode.TooManyRequests);


                        List<Model.Message> history = new();

                        var sql_history = SQL.ExecuteCommand(sqlEntry, $"GPT_PKG.GET_HISTORY('{user_request.id_session}')",
                           new OracleParameter { ParameterName = "result", OracleDbType = OracleDbType.Clob, Direction = System.Data.ParameterDirection.ReturnValue })
                           .ToString().ToTypedObject<Model.HistoryItem[]>();

                        
                        if (sql_history != null)
                            foreach (var history_item in sql_history)
                            {
                                history.Add(new Model.Message("user", history_item.request.ToTypedObject<Model.UserRequest>().text));
                                history.Add(new Model.Message("assistant", history_item.response.ToTypedObject<Model.GenResponse>().result.alternatives[0].message.text));                                
                            }

                        var gen_result = await httpClient.GetResultAsync<Model.GenResponse>(
                            uri,
                            new Model.GenRequest("user", user_request.text, user_request.temperature, user_request.max_tokens, history),
                            HttpMethod.Post,
                            true
                            );


                        if(gen_result.exception != null)                            
                            throw gen_result.exception;

                        log.response = gen_result.raw_data;
                        var answer = gen_result.value.result.alternatives.LastOrDefault();
                        spec_message = answer.message.text;
                        log.status = (int)HttpStatusCode.OK;
                        
                        break;

                    default:
                        log.status = (int)HttpStatusCode.MethodNotAllowed;
                        break;
                }

            }
            catch (ErrorsHelper.HttpSimpleException e)
            {
                log.status = (int)e.Status;
                log.response = log.response ?? e.Message;                
            }
            catch (Exception e)
            {
                log.status = (int)HttpStatusCode.InternalServerError;
                log.response = log.response ?? e.Message;
            }

            await context.WriteResult(sqlEntry, log, spec_message);
        }
    }
}
