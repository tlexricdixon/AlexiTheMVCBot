using AlexiTheMVCBot.Code;
using AlexiTheMVCBot.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace AlexiTheMVCBot.Controllers
{
    public class ChatController : Controller
    {
        private readonly IChatProxy _chatService;

        public ChatController(IChatProxy chatService)
        {
            _chatService = chatService;
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
            if (!string.IsNullOrEmpty(botName))
            {
                HttpContext.Session.SetString("ChatbotName", botName);
            }

            var response = await _chatService.SendChatMessage(userInput);
            var messages = response
                .Select(x => new ChatMessage
                {
                    Role = x.Role == "assistant" ? "Alexi" : x.Role,
                    Content = x.Content
                })
                .ToList();

            return View(messages);
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
