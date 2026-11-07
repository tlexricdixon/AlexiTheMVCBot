using AlexiTheMVCBot.ChatGpt;
using AlexiTheMVCBot.Models;
using Microsoft.AspNetCore.Mvc;
using Standard.AI.OpenAI.Models.Services.Foundations.ChatCompletions;
using System.Diagnostics;

namespace AlexiTheMVCBot.Controllers
{
    public class ChatController : Controller
    {
        private readonly ILogger<ChatController> _logger;
        private readonly OpenAIServices _openAIService;

        public ChatController(OpenAIServices openAIService)
        {
            _openAIService = openAIService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            ViewBag.ChatbotName = HttpContext.Session.GetString("ChatbotName") ?? "Alexi";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(string userInput, string botName)
        {
            // Set the bot name in session
            if (!string.IsNullOrEmpty(botName))
            {
                HttpContext.Session.SetString("ChatbotName", botName);
            }
            var response = await _openAIService.SendChatMessage(userInput);
            var _message = new ChatCompletionMessage();
            var _messages = new List<ChatCompletionMessage>();
            foreach (var x in response)
            {
                if (x.Role == "assistant")
                    _message.Role = "Alexi";
                else _message.Role = x.Role;
                _message.Content = x.Content;
                _messages.Add(_message);
            }
            return View(_messages);
        }
        public IActionResult CoinbaseChart()
        {

            return PartialView();
        }
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
