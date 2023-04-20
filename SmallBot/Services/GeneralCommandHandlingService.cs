using Discord.Commands;
using Discord.WebSocket;
using Discord;
using Newtonsoft.Json.Linq;
using System.Text;

namespace SmallBot.Services
{
    public class GeneralCommandHandlingService : ModuleBase<SocketCommandContext>
    {

        private readonly HttpClient _http;

        public GeneralCommandHandlingService(HttpClient http)
        {
            _http = http;
        }


        [Command("help")]
        [Summary("Lists all available commands.")]
        public async Task HelpAsync()
        {

            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Here are the available commands:");
            stringBuilder.AppendLine($"!cat: Grabs a random cat picture from the internet.");
            stringBuilder.AppendLine($"!quote: Fetches a random quote from the internet.");
            stringBuilder.AppendLine($"!server-info: Displays very basic server-related information.");
            stringBuilder.AppendLine($"!ping: pong.");

            stringBuilder.AppendLine("There are also the more powerful slash commands such as: who-is ('/who-is user:') see-bets ('/see-bets user:') and {'/whisper question:') that respond directly to you with info on a user from our Member Directory.");


            var embed = new EmbedBuilder()
                .WithTitle("Help")
                .WithDescription(stringBuilder.ToString())
                .WithColor(Color.Blue)
                .Build();

            await Context.Channel.SendMessageAsync(embed: embed);
        }

        [Command("cat")]
        [Summary("Grabs a random cat picture from the internet.")]
        public async Task CatAsync()
        {
            try
            {
                // Get a stream containing an image of a cat
                //var stream = await PictureService.GetCatPictureAsync();
                var resp = await _http.GetAsync("https://cataas.com/cat");

                var stream = await resp.Content.ReadAsStreamAsync();

                // Streams must be seeked to their beginning before being uploaded!
                if (stream.Length != 150)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    await Context.Channel.SendFileAsync(stream, "cat.png");
                }
                else
                {
                    //Fallback cats in case the cat api is down - gotta always have cats
                    string imagesFolderPath = "wwwroot/images/cats"; // Set the path to the folder containing the images
                    var random = new Random();

                    var imageFiles = Directory.GetFiles(imagesFolderPath, "*.jpg"); // Get all .jpg files in the folder
                    int randomIndex = random.Next(imageFiles.Length); // Pick a random index
                    string randomImagePath = imageFiles[randomIndex]; // Get the random image file path

                    using var filestream = new FileStream(randomImagePath, FileMode.Open);

                    await Context.Channel.SendFileAsync(filestream, "cat.png", text: @"The public cataas.com api is down. But my programmers were smart enough to give me backup cats.");
                }
            }
            catch
            {

            }
        }

        /* Reddit blocks you pretty fast, a new meme API is required
        [Command("meme")]
        [Summary("Fetches a random meme from r/memes.")]
        public async Task MemeAsync()
        {
            string url = "https://www.reddit.com/r/memes/top/.json?sort=top&t=day&limit=100";
            HttpResponseMessage response = await _http.GetAsync(url);
            string json = await response.Content.ReadAsStringAsync();
            JObject parsedJson = JObject.Parse(json);
            var posts = parsedJson["data"]["children"].ToArray();
            var randomPost = posts[new Random().Next(posts.Length)];

            string memeTitle = (string)randomPost["data"]["title"];
            string memeImageUrl = (string)randomPost["data"]["url"];

            var embed = new EmbedBuilder()
                .WithTitle(memeTitle)
                .WithImageUrl(memeImageUrl)
                .WithColor(Color.Blue)
                .WithFooter("From r/memes")
                .Build();

            await Context.Channel.SendMessageAsync(embed: embed);
        }*/


        [Command("quote")]
        [Summary("Fetches a random quote.")]
        public async Task RandomQuoteAsync()
        {
            string url = "https://zenquotes.io/api/random";
            HttpResponseMessage response = await _http.GetAsync(url);
            string json = await response.Content.ReadAsStringAsync();
            JArray parsedJson = JArray.Parse(json);
            JObject quoteObj = (JObject)parsedJson[0];

            string quote = (string)quoteObj["q"];
            string author = (string)quoteObj["a"];

            var embed = new EmbedBuilder()
                .WithTitle("Random Quote")
                .WithDescription($"\"{quote}\"\n- {author}")
                .WithColor(Color.Blue)
                .Build();

            await Context.Channel.SendMessageAsync(embed: embed);
        }

        [Command("server-info")]
        [Summary("Displays server-related information.")]
        public async Task ServerInfoAsync()
        {
            var guild = Context.Guild;
            int memberCount = guild.MemberCount;
            //int onlineUsers = guild.Users.Count(user => user.Status != UserStatus.Offline);
            DateTimeOffset serverCreationDate = guild.CreatedAt;

            var embed = new EmbedBuilder()
                .WithTitle($"{guild.Name} Information")
                .WithColor(Color.Blue)
                .AddField("Member Count", memberCount.ToString(), true)
                .AddField("Server Creation Date", serverCreationDate.ToString("dd MMMM yyyy"), true)
                .WithThumbnailUrl(guild.IconUrl)
                .Build();

            await Context.Channel.SendMessageAsync(embed: embed);
        }

        // Get info on a user, or the user who invoked the command if one is not specified
        [Command("userinfo")]
        [Summary("Displays basic user info from the discord server.")]
        public async Task UserInfoAsync(IUser user = null)
        {
            user ??= (SocketGuildUser)Context.User;

            DateTimeOffset accountCreationDate = user.CreatedAt;
            int messageCount = 0;

            foreach (var textChannel in Context.Guild.TextChannels)
            {
                var messages = await textChannel.GetMessagesAsync().FlattenAsync();
                messageCount += messages.Count(msg => msg.Author.Id == user.Id);
            }

            var embed = new EmbedBuilder()
                .WithTitle($"{user.Username} Information")
                .WithColor(Color.Blue)
                .AddField("Account Creation Date", accountCreationDate.ToString("dd MMMM yyyy"), true)
                .AddField("Message Count", messageCount.ToString(), true)
                .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .Build();

            await Context.Channel.SendMessageAsync(embed: embed);
        }


        [Command("ping")]
        [Alias("pong", "hello")]
        [Summary("Pong.")]
        public Task PingAsync()
             => ReplyAsync("pong!");

        /* Additional example commands
        // [Remainder] takes the rest of the command's arguments as one argument, rather than splitting every space
        [Command("echo")]
        [Summary("echo")]
        public Task EchoAsync([Remainder] string text)
            // Insert a ZWSP before the text to prevent triggering other bots!
            => ReplyAsync('\u200B' + text);


        // 'params' will parse space-separated elements into a list
        [Command("list")]
        public Task ListAsync(params string[] objects)
            => ReplyAsync("You listed: " + string.Join("; ", objects));

        // Setting a custom ErrorMessage property will help clarify the precondition error
        [Command("guild_only")]
        [RequireContext(ContextType.Guild, ErrorMessage = "Sorry, this command must be ran from within a server, not a DM!")]
        public Task GuildOnlyCommand()
            => ReplyAsync("Nothing to see here!");

        */
    }
}
