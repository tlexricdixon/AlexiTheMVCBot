using AlexiTheMVCBot.ChatGpt;
using AlexiTheMVCBot.Code;
using AlexiTheMVCBot.Components;

namespace AlexiTheMVCBot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services
                .AddRazorComponents()
                .AddInteractiveServerComponents();
            builder.Services.AddSingleton<IChatProxy, OllamaChatService>();

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error", createScopeForErrors: true);
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseAntiforgery();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}
