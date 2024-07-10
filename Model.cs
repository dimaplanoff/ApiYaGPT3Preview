using Microsoft.AspNetCore.Routing.Constraints;

namespace YaGpt
{
    public class Model
    {
        public class HistoryItem
        {
            public string request { get; init; }
            public string response { get; init; }
        }

        public class AuthData
        {
            public string token { get; init; }
            public DateTime? expired { get; init; }

            public AuthData()
            {
            }

            public AuthData(string token, DateTime? expired)
            {
                this.token = token;
                this.expired = expired;
            }
        }

        public class UserRequest
        {
            public string id_session { get; init; }
            public string text { get; init; }
            public double temperature { get; init; } = 0.9;
            public int max_tokens { get; init; } = 1000;
        }   
        
        
        public class UserResponse
        {
            public string text { get; private set; }
            public string error { get; private set; }

            public UserResponse(string text, bool is_success = true)
            {
                if (is_success)
                    this.text = text;
                else
                    this.error = text;
            }
        }

        public class Message
        {
            public Message()
            { 
            }
            public Message(string role, string text)
            {
                this.role = role;
                this.text = text;
            }
            public string role { get; init; }
            public string text { get; init; }
        }

        public class GenRequest
        {



            public GenRequest(string role, string text, double temperature, int maxTokens, IEnumerable<Message> history = null)
            {
                completionOptions = new(temperature, maxTokens);
                modelUri = $"gpt://{Const.Config.ya_folder}/yandexgpt-lite";
                messages = new () ;
                if(history != null)
                    messages.AddRange(history);
                messages.Add(new Message(role, text)); 
            }

            public class CompletionOptions
            {
                public CompletionOptions(double temperature, int maxTokens)
                {
                    this.temperature = temperature;
                    this.maxTokens = maxTokens.ToString();
                }
                public bool stream = false;
                public double temperature { get; private set; }
                public string maxTokens { get; private set; }

            }



            public string modelUri { get; private set; }
            public CompletionOptions completionOptions { get; private set; }
            public List<Message> messages { get; private set; }

        }

        public class GenResponse
        {
            public class Error
            {
                public int grpcCode { get; init; }
                public int httpCode { get; init; }
                public string message { get; init; }
                public string httpStatus { get; init; }
                public string[] details { get; init; }
            }

            public class Result
            {
                public class Alternative
                {
                    public Message message { get; init; }
                    public string status { get; init; }

                }
                public class Usage
                {
                    public string inputTextTokens { get; init; }
                    public string completionTokens { get; init; }
                    public string totalTokens { get; init; }
                }

                public Alternative[] alternatives { get; init; }
                public Usage usage { get; init; }
                public string modelVersion { get; init; }

            }

            public Result result { get; init; }
            public Error error { get; init; }
        }
    }
}
