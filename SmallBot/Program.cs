using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using SmallBot.Models;
using SmallBot.Services;

namespace SmallBot
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorPages();




            builder.Services.AddTransient<IOpenAiService, OpenAiService>();
            builder.Services.AddMemoryCache();

            builder.Services.AddSingleton(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers
            });
            builder.Services.AddSingleton<DiscordSocketClient>();
            builder.Services.AddSingleton<CommandService>();
            builder.Services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));
            builder.Services.AddSingleton<TextCommandHandlingService>();
            builder.Services.AddSingleton<SlashCommandHandlingService>();

            builder.Services.AddSingleton<HttpClient>();

            builder.Services.Configure<DiscordConfigSettings>(
                builder.Configuration.GetSection("DiscordConfigSettings"));

            builder.Services.Configure<OpenAiConfigSetting>(
                builder.Configuration.GetSection("OpenAiConfigSetting"));

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapRazorPages();

            // Uncomment to configure the bot not to run locally if you dont want to
            //if (!app.Environment.IsDevelopment())
            //{
                //The general command service is auto detected from the binary
                DiscordSocketClient _client = app.Services.GetRequiredService<DiscordSocketClient>();
                var configValue = builder.Configuration.GetValue<string>("DiscordConfigSettings:BotToken");

                await _client.LoginAsync(TokenType.Bot, configValue, true);
                await _client.StartAsync();

                await app.Services.GetRequiredService<TextCommandHandlingService>().InitializeAsync();
                await app.Services.GetRequiredService<SlashCommandHandlingService>().InitializeAsync();
            //}

            app.Run();
        }
    }
}