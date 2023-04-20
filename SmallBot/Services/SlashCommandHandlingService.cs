using Discord.Net;
using Discord.WebSocket;
using Discord;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SmallBot.Models;
using System.Text;
using System.Web;

namespace SmallBot.Services
{
    public class SlashCommandHandlingService
    {



        private readonly DiscordSocketClient _discord;
        private readonly IServiceProvider _services;
        private readonly HttpClient _http;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger _logger;
        private readonly IOpenAiService _openAiService;


        public DiscordConfigSettings DiscordConfigOptions { get; }


        public SlashCommandHandlingService(IServiceProvider services,
            HttpClient http,
            IMemoryCache memoryCache,
            IOptions<DiscordConfigSettings> discordOptions,
            ILogger<OpenAiService> logger,
            IOpenAiService openAiService)
        {
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _services = services;
            _http = http;
            _memoryCache = memoryCache;
            _logger = logger;
            _openAiService = openAiService;
            DiscordConfigOptions = discordOptions.Value;

            _discord.SlashCommandExecuted += SlashCommandHandler;


        }

        public async Task InitializeAsync()
        {
            // Process when the client is ready, so we can register our commands.
            _discord.Ready += SlashCommandReadyRegister;

        }





        private async Task SlashCommandReadyRegister()
        {

            // Let's do our global command
            List<ApplicationCommandProperties> applicationCommandProperties = new();

            try
            {
                // Simple help slash command.
                SlashCommandBuilder globalHelpCommandHelp = new SlashCommandBuilder();
                globalHelpCommandHelp.WithName("help");
                globalHelpCommandHelp.WithDescription("Replies back to you with the commands the bot has.");
                applicationCommandProperties.Add(globalHelpCommandHelp.Build());

                // Simple help slash command.
                SlashCommandBuilder globalWhsiperCommandHelp = new SlashCommandBuilder();
                globalWhsiperCommandHelp.WithName("whisper");
                globalWhsiperCommandHelp.WithDescription("SmallBot replies back only to you with any questions you might have.");
                globalWhsiperCommandHelp.AddOption("question", ApplicationCommandOptionType.String, "The question or message you have for Small Bot that you want him to respond directly to you on.", isRequired: true);
                applicationCommandProperties.Add(globalWhsiperCommandHelp.Build());

                // Simple help slash command.
                SlashCommandBuilder globalCommandHelp = new SlashCommandBuilder();
                globalCommandHelp.WithName("who-is");
                globalCommandHelp.WithDescription("Get info on a small bets user.");
                globalCommandHelp.AddOption("user", ApplicationCommandOptionType.User, "The users whos info you want", isRequired: true);
                applicationCommandProperties.Add(globalCommandHelp.Build());


                SlashCommandBuilder globalCommandAddFamily = new SlashCommandBuilder();
                globalCommandAddFamily.WithName("see-bets");
                globalCommandAddFamily.WithDescription("Get the projects a small bets user is working on.");
                globalCommandAddFamily.AddOption("user", ApplicationCommandOptionType.User, "The users whos projects you want to see", isRequired: true);
                applicationCommandProperties.Add(globalCommandAddFamily.Build());


                // Simple help slash command.
                SlashCommandBuilder globalSearchCommand = new SlashCommandBuilder();
                globalSearchCommand.WithName("search");
                globalSearchCommand.WithDescription("SmallBot searches our community directory for profiles that match your query.");
                globalSearchCommand.AddOption("term", ApplicationCommandOptionType.String, "The search term you want SmallBot to try and search our directory for.", isRequired: true);
                applicationCommandProperties.Add(globalSearchCommand.Build());

                await _discord.BulkOverwriteGlobalApplicationCommandsAsync(applicationCommandProperties.ToArray());

                // Now that we have our builder, we can call the CreateApplicationCommandAsync method to make our slash command.
                //await guild.CreateApplicationCommandAsync(guildCommand.Build());

                //await _discord.CreateGlobalApplicationCommandAsync(globalCommand.Build());
                //await _discord.BulkOverwriteGlobalApplicationCommandsAsync(applicationCommandProperties.ToArray());


            }
            catch (ApplicationCommandException exception)
            {
                // If our command was invalid, we should catch an ApplicationCommandException. This exception contains the path of the error as well as the error message. You can serialize the Error field in the exception to get a visual of where your error is.
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);

                // You can send this error somewhere or just print it to the console, for this example we're just going to print it.
                Console.WriteLine(json);
            }
        }


        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            //await command.RespondAsync($"You executed {command.Data.Name}");

            // Let's add a switch statement for the command name so we can handle multiple commands in one event.
            switch (command.Data.Name)
            {
                case "help":
                    await HelpCommand(command);
                    break;
                case "whisper":
                    await WhisperCommand(command);
                    break;
                case "who-is":
                    await UserInfoCommand(command);
                    break;
                case "see-bets":
                    await UserProjects(command);
                    break;
                case "search":
                    await SearchCommand(command);
                    break;
            }
        }


        private async Task HelpCommand(SocketSlashCommand command)
        {
            // We need to extract the user parameter from the command. since we only have one option and it's required, we can just use the first option.


            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Here are my available commands:");
            stringBuilder.AppendLine($"!cat: Grabs a random cat picture from the internet.");
            stringBuilder.AppendLine($"!quote: Fetches a random quote from the internet.");
            stringBuilder.AppendLine($"!server-info: Displays very basic server-related information.");
            stringBuilder.AppendLine($"!ping: pong.");

            stringBuilder.AppendLine("There are also the more powerful slash commands such as: who-is ('/who-is user:') see-bets ('/see-bets user:') and {'/whisper question:') that respond directly to you with info on a user from our Member Directory.");


            var embedBuilder = new EmbedBuilder()
                .WithTitle("Help")
                .WithDescription(stringBuilder.ToString())
                .WithColor(Color.Blue);


            // Now, Let's respond with the embed.
            await command.RespondAsync(embed: embedBuilder.Build(), ephemeral: true);


        }

        private async Task WhisperCommand(SocketSlashCommand command)
        {
            // We need to extract the user parameter from the command. since we only have one option and it's required, we can just use the first option.
            var question = (string)command.Data.Options.First().Value;
            SocketGuild guild = _discord.GetGuild(command.GuildId ?? 0);


            var prompt = PromptGPT.GetDavinciPrompt(question);

            try
            {
                var response = await _openAiService.GetCompletionAsync(prompt, 150, 0.9);
                await command.RespondAsync($"{response}", ephemeral: true);

            }
            catch (Exception ex)
            {
                await command.RespondAsync("Forgive me but it appears something is broken on my side and I am unable to respond intelligently right now. \nTry again in a little bit please", ephemeral: true);

            }
        }


        private async Task UserInfoCommand(SocketSlashCommand command)
        {
            // We need to extract the user parameter from the command. since we only have one option and it's required, we can just use the first option.
            var guildUser = (SocketGuildUser)command.Data.Options.First().Value;

            // This is where you can pull in your own user info from your own member directory external to discord for additional context
            //var smallBetsUserInfo = await _cosmosDbService.GetUserInfoByDiscordIdAsync(guildUser.Username + "#" + guildUser.Discriminator);

            //Example Data - remove
            var userInfo = GetSampleUserInfoData();

            String userInfoFormmated = String.Empty;

            if (userInfo != null)
            {
                var bob = new StringBuilder();
                bob.AppendLine($"Name: {userInfo.FirstName} {userInfo.LastName}");

                //Running these checks here because of DB Values
                if (!String.IsNullOrEmpty(userInfo.TwitterUrl))
                    bob.AppendLine($"Twitter: https://twitter.com/{userInfo.TwitterUrl}");

                if (!String.IsNullOrEmpty(userInfo.LinkedInUrl))
                    bob.AppendLine($"LinkedIn: https://www.linkedin.com/in/{userInfo.LinkedInUrl}/");

                if (!String.IsNullOrEmpty(userInfo.Location))
                    bob.AppendLine($"Location: {userInfo.Location}");

                if (userInfo.TotalNumberOfProjects.HasValue)
                    bob.AppendLine($"# of Projects: {userInfo.TotalNumberOfProjects.Value}");

                userInfoFormmated = bob.ToString();
            }

            if (String.IsNullOrEmpty(userInfoFormmated))
                userInfoFormmated = "I found no info. Maybe they have not yet setup their Community Directory profile.";


            var embedBuiler = new EmbedBuilder()
                .WithAuthor(guildUser.ToString(), guildUser.GetAvatarUrl() ?? guildUser.GetDefaultAvatarUrl())
                .WithTitle($"Who is {guildUser.Username}? Here is what I found:")
                .WithDescription(userInfoFormmated)
                .WithCurrentTimestamp();

            if (userInfo != null)
            {
                embedBuiler.WithColor(Color.Green);
                //substitute your own community site, move to config or db
                embedBuiler.Url = @"https://home.smallbets.co/Home/DirectoryProfile?userId=" + userInfo.Id;
            }
            else
                embedBuiler.WithColor(Color.Red);

            // Now, Let's respond with the embed.
            await command.RespondAsync(embed: embedBuiler.Build(), ephemeral: true);

        }

        private async Task UserProjects(SocketSlashCommand command)
        {
            // We need to extract the user parameter from the command. since we only have one option and it's required, we can just use the first option.
            var guildUser = (SocketGuildUser)command.Data.Options.First().Value;

            //Connect to your own data source and pull projects
            //var smallBetsUserInfo = await _cosmosDbService.GetUserInfoByDiscordIdAsync(guildUser.Username + "#" + guildUser.Discriminator);
            //var allUserProjects = await _cosmosDbService.GetAllProjectInfoForUserAsync(smallBetsUserInfo.id);

            //Example Data - remove
            var userInfo = GetSampleUserInfoData();
            var allProjects = GetSampleProjectInfoData();

            String userInfoFormmated = String.Empty;

            if (userInfo != null)
            {
                var bob = new StringBuilder();
                if (userInfo.TotalNumberOfProjects.HasValue)
                    bob.AppendLine($"{userInfo.TotalNumberOfProjects.Value} projects.");
                else bob.AppendLine($"No projects listed on their Small Bets directory profile.");


                foreach (var project in allProjects)
                {
                    bob.AppendLine($"{project.Name}: {project.URL}");
                }



                userInfoFormmated = bob.ToString();
            }

            if (String.IsNullOrEmpty(userInfoFormmated))
                userInfoFormmated = "I found no info. Maybe they have not yet setup their Small Bets Directory profile.";


            var embedBuiler = new EmbedBuilder()
                .WithAuthor(guildUser.ToString(), guildUser.GetAvatarUrl() ?? guildUser.GetDefaultAvatarUrl())
                .WithTitle($"See Small Bets for {guildUser.Username}. Here is what I found:")
                .WithDescription(userInfoFormmated)
                .WithCurrentTimestamp();

            if (allProjects != null)
            {
                embedBuiler.WithColor(Color.Green);
                //substitute your own community site, move to config or db
                embedBuiler.Url = @"https://home.smallbets.co/Home/DirectoryProfile?userId=" + userInfo.Id;
            }
            else
                embedBuiler.WithColor(Color.Red);

            // Now, Let's respond with the embed.
            await command.RespondAsync(embed: embedBuiler.Build(), ephemeral: true);
        }




        private async Task SearchCommand(SocketSlashCommand command)
        {
            // We need to extract the user parameter from the command. since we only have one option and it's required, we can just use the first option.
            var term = (string)command.Data.Options.First().Value;
            SocketGuild guild = _discord.GetGuild(command.GuildId ?? 0);


            try
            {
                String stringifiedResult = String.Empty;
                //Add your own search engine
                //var results = await _azureSearchService.TermSearch<UserInfoSearchDoc>(term);

                var results = GetSampleUsers();

                if (results != null)
                {
                    var bob = new StringBuilder();

                    if (results.Count > 10)
                    {
                        results = results.Take(10).ToList();
                    }


                    foreach (var userInfo in results)
                    {
                        bob.AppendLine($"{userInfo.FirstName} {userInfo.LastName}: with {userInfo.TotalNumberOfProjects} projects.");

                        if (!String.IsNullOrEmpty(userInfo.LinkedInUrl))
                            bob.AppendLine($"Their LinkedIn: https://www.linkedin.com/in/{userInfo.LinkedInUrl}/");


                        if (!String.IsNullOrEmpty(userInfo.TwitterUrl))
                            bob.AppendLine($"Their Twitter: https://twitter.com/{userInfo.TwitterUrl}");

                        //append your own directory
                        bob.AppendLine(@"Directory Profile: https://home.smallbets.co/Home/DirectoryProfile?userId=" + userInfo.Id);
                        bob.AppendLine(@"");

                    }

                    stringifiedResult = bob.ToString();

                    if (String.IsNullOrEmpty(stringifiedResult))
                        stringifiedResult = "Try searching for something else.";


                    var embedBuiler = new EmbedBuilder()
                        .WithTitle($"I found {results.Count} results.")
                        .WithDescription(stringifiedResult)
                        .WithCurrentTimestamp();

                    if (stringifiedResult != null)
                    {
                        string termEncoded = HttpUtility.HtmlEncode(term);
                        embedBuiler.WithColor(Color.Green);
                        //append your own directory
                        embedBuiler.Url = @"https://home.smallbets.co/Home/Directory?SearchString=" + termEncoded;
                    }
                    else
                        embedBuiler.WithColor(Color.Red);

                    // Now, Let's respond with the embed.
                    await command.RespondAsync(embed: embedBuiler.Build(), ephemeral: true);

                }
                else
                {
                    var embedBuiler = new EmbedBuilder()
                        .WithTitle($"I found 0 results.")
                        .WithDescription("Try searching for something else.")
                        .WithCurrentTimestamp();

                    embedBuiler.WithColor(Color.Red);
                    await command.RespondAsync(embed: embedBuiler.Build(), ephemeral: true);

                }

            }
            catch (Exception ex)
            {
                await command.RespondAsync("Forgive me but it appears something is broken on my side and I am unable to respond intelligently right now. \nTry again in a little bit please", ephemeral: true);

            }
        }

        private UserInfo GetSampleUserInfoData()
        {
            return new UserInfo()
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = "Louie",
                LastName = "Bacaj",
                TwitterUrl = "LBacaj",
                LinkedInUrl = "louiebacaj",
                TotalNumberOfProjects = 2
            };
        }

        private List<UserInfo> GetSampleUsers()
        {
            var users = new List<UserInfo>();
            users.Add(new UserInfo()
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = "Louie",
                LastName = "Bacaj",
                TwitterUrl = "LBacaj",
                LinkedInUrl = "louiebacaj",
                TotalNumberOfProjects = 2
            });
            users.Add(new UserInfo()
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = "Daniel",
                LastName = "Vassallo",
                TwitterUrl = "dvassallo",
                LinkedInUrl = "dvassallo",
                TotalNumberOfProjects = 2
            });

            return users;
        }

        private List<ProjectInfo> GetSampleProjectInfoData()
        {
            var projects = new List<ProjectInfo>();
            projects.Add(new ProjectInfo()
            {
                Id = Guid.NewGuid().ToString(),
                Name = "The M&Ms Newsletter",
                URL = "https://newsletter.memesmotivations.com/",
                Description = "My personal newsletter"

            });
            projects.Add(new ProjectInfo()
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Small Bets",
                URL = "https://smallbets.co/",
                Description = "The Small Bets community"
            });

            return projects;
        }
    }

}

