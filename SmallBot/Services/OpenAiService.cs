using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SmallBot.Models;
using System.Net.Http.Headers;
using System.Text;

namespace SmallBot.Services
{
    public class OpenAiService : IOpenAiService
    {
        private readonly HttpClient _http;
        private readonly ILogger _logger;
        public OpenAiConfigSetting OpenAiConfigOptions { get; }


        public OpenAiService(IOptions<OpenAiConfigSetting> openAiOptions, ILogger<OpenAiService> logger, HttpClient http)
        {
            OpenAiConfigOptions = openAiOptions.Value;
            _logger = logger;
            _http = http;
        }

        public async Task<string> GetCompletionAsync(string prompt, int maxTokens = 150, double temperature = 0.9)
        {
            string apiUrl = OpenAiConfigOptions.CompletionsEndpoint;
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", OpenAiConfigOptions.ApiKey);

            var requestData = new
            {
                model = "text-davinci-003",
                prompt = prompt,
                temperature = temperature,
                max_tokens = maxTokens,
                top_p = 1,
                frequency_penalty = 0.0,
                presence_penalty = 0.6,
                stop = new string[] { "Human:", "AI:" }
            };

            var jsonRequest = JsonConvert.SerializeObject(requestData);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _http.PostAsync(apiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                dynamic result = JsonConvert.DeserializeObject(jsonResponse);
                return result.choices[0].text.ToString();
            }
            else
            {
                _logger.LogError($"OpenApiService Failed to GetCompletionAsync() reason: {response.ReasonPhrase}");
                return $"Unfortunetly something appears to be wrong, the error reason I am seeing: {response.ReasonPhrase}";
            }
        }

        //This thread does a great job explaining the variables of open ai
        //https://twitter.com/realchasecurtis/status/1643312487305977867?s=46&t=8EFbTQ0DVPggrJAtbfhJdw
        //The tokenizer
        //https://platform.openai.com/tokenizer
        public async Task<Message> GetChatCompletionAsync(List<Message> messageList, int maxTokens = 4096, double temperature = 0.9)
        {
            string apiUrl = "https://api.openai.com/v1/chat/completions";
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", OpenAiConfigOptions.ApiKey);


            var modelRequest = new
            {
                //model = "gpt-3.5-turbo",
                model = "gpt-4",
                temperature = temperature,
                //max_tokens = maxTokens,
                top_p = 1,
                frequency_penalty = 0.0,
                presence_penalty = 0.6,
                messages = messageList.ToArray()
            };

            var jsonRequest = JsonConvert.SerializeObject(modelRequest);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _http.PostAsync(apiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();

                var chatAPIResponse = JsonConvert.DeserializeObject<ChatAPIResponse>(jsonResponse);

                //var textResponse = chatAPIResponse.Choices.FirstOrDefault().Message.Content;

                //dynamic result = JsonConvert.DeserializeObject(jsonResponse);
                return chatAPIResponse.Choices.FirstOrDefault().Message;
            }
            else
            {
                _logger.LogError($"OpenApiService Failed to GetCompletionAsync() reason: {response.ReasonPhrase}");
                return null;
            }
        }
    }
}
