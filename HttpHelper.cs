using System.Net;
using System.Net.Http.Headers;

namespace YaGpt
{
    public class HttpHelper
    {
        public interface IWebHandlerClient
        {
            Task<HttpResult<T>> GetResultAsync<T>(string url, object query, HttpMethod method, bool no_throw);
            void SetHeader(string key, string value);
        }  
        
        public class HttpResult<T> //where T : class, new()
        {            
            public string raw_data { get; private set; }
            public T _value { get; private set; }
            public T value => _value ?? (T)(object)raw_data;
            public HttpStatusCode status { get; private set; }
            public ErrorsHelper.HttpSimpleException exception { get; private set; }

            public void SetValue(string raw_data)
            {                
                if (value == null)
                {
                    try
                    {
                        this.raw_data = raw_data;

                        if (!new Type[] { typeof(string), typeof(object) }.Contains(typeof(T)))
                            _value = raw_data.ToTypedObject<T>();                       
                    }
                    catch (Exception e)
                    {
                        SetError(e);
                        throw;
                    }
                }
            }

            public void SetValue(byte[] raw_data)
            {
                this.raw_data = Convert.ToBase64String(raw_data);
                if (_value == null && raw_data != null)
                    _value = (T)(object)raw_data;
            }

            public void SetStatus(HttpStatusCode status)
            {
                this.status = status;
            }

            public void SetError(Exception exception)
            {
                this.exception = new(exception.FullMessage(), status);
            }
        }

        public class YaClient : HttpClient, IWebHandlerClient
        {
            public YaClient()
            {
                SetHeader("Authorization", Const.Config.ya_auth);
                SetHeader("x-folder-id", Const.Config.ya_folder);
            }

            public async Task<HttpResult<T>> GetResultAsync<T>(string url, object query, HttpMethod method = null, bool no_throw = false) //where T : class, new()
            {
                var urls = url.Split("://");
                url = urls[0].Trim() + "://" + urls[1].Replace("//", "/").Trim();
                string serializedQuery = null;
                var result = new HttpResult<T>();
                try
                {
                    using (var tokenSource = new CancellationTokenSource())
                    {
                        tokenSource.CancelAfter(TimeSpan.FromSeconds(30));
                        using (HttpRequestMessage request = new(method ?? (query is null ? HttpMethod.Get : HttpMethod.Post), url))
                        {
                            Console.WriteLine($"request to {url} [{request.Method}]");
                            if (request.Method != HttpMethod.Get)
                            {
                                serializedQuery = query is string q ? q : query.ToStr();
                                Console.WriteLine($"request body: {serializedQuery}");
                                StringContent content = new StringContent(serializedQuery);
                                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                                request.Content = content;
                            }


                            using (var response = await SendAsync(request))
                            {
                                result.SetStatus(response.StatusCode);

                                do
                                {
                                    if (response.IsSuccessStatusCode)
                                    {
                                        if (response.StatusCode == HttpStatusCode.NoContent)
                                            break;

                                        if (typeof(T) == typeof(byte[]))
                                        {
                                            var bytes = await response.Content.ReadAsByteArrayAsync();
                                            result.SetValue(bytes);
                                            break;
                                        }

                                        result.SetValue(await response.Content.ReadAsStringAsync());
                                    }
                                    else
                                    {
                                        result.SetValue(await response.Content.ReadAsStringAsync());
                                        throw new Exception(response.StatusCode.ToString());
                                    }

                                } while (false);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    result.SetError(e);
                }

                if (!no_throw && result.exception != null)
                    throw result.exception;

                return result;

            }

            public void SetHeader(string key, string value)
            {
                if (DefaultRequestHeaders.Contains(key))
                    DefaultRequestHeaders.Remove(key);
                DefaultRequestHeaders.Add(key, value);
            }
        }
    }
}
