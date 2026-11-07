using Standard.AI.OpenAI.Clients.OpenAIs;
using Standard.AI.OpenAI.Models.Configurations;
using Standard.AI.OpenAI.Models.Services.Foundations.ChatCompletions;

namespace AlexiTheMVCBot.Code
{
    public class OpenAiProxy
    {
        readonly OpenAIClient client;
        readonly List<ChatCompletionMessage> _messages;
        public OpenAiProxy(string apiKey)
        {
            var openAiConfig = new OpenAIConfigurations
            {
                ApiKey = apiKey,
            };
            client = new OpenAIClient(openAiConfig);
            _messages = [];
        }
        void StackMessages(params ChatCompletionMessage[] messages)
        {
            _messages.AddRange(messages);
        }
        static ChatCompletionMessage[] ToCompletionMessage(ChatCompletionChoice[] choices) => choices.Select(x => x.Message).ToArray();
        public Task<ChatCompletionMessage[]> SendChatMessage(string prompt)
        {
            var chatMsg = new ChatCompletionMessage()
            {
                Content = prompt,
                Role = "user"
            };
            return SendChatMessage(chatMsg);
        }
        //Where business happens
        async Task<ChatCompletionMessage[]> SendChatMessage(
          ChatCompletionMessage message)
        {
            //we should send all the messages
            //so we can give Open AI context of conversation
            StackMessages(message);

            var chatCompletion = new ChatCompletion
            {
                Request = new ChatCompletionRequest
                {
                    Model = "gpt-4o-mini",
                    Messages = _messages.ToArray(),
                    Temperature = 0.2,
                    MaxTokens = 800
                }
            };

            var result = await client
              .ChatCompletions
              .SendChatCompletionAsync(chatCompletion);

            var choices = result.Response.Choices;

            var messages = ToCompletionMessage(choices);

            //stack the response as well - everything is context to Open AI
            StackMessages(messages);

            return messages;
        }
    }
}
