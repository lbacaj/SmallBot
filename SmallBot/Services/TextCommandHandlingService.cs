using Discord.Commands;
using Discord.WebSocket;
using Discord;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SmallBot.Models;
using System.Reflection;
using System.Text;
using IResult = Discord.Commands.IResult;

namespace SmallBot.Services
{
    public class TextCommandHandlingService
    {

        private readonly CommandService _commands;
        private readonly DiscordSocketClient _discord;
        private readonly IServiceProvider _services;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger _logger;
        private readonly IOpenAiService _openAiService;

        public DiscordConfigSettings DiscordConfigOptions { get; }


        public TextCommandHandlingService(IServiceProvider services,
            IMemoryCache memoryCache,
            IOptions<DiscordConfigSettings> discordOptions,
            ILogger<OpenAiService> logger,
            IOpenAiService openAiService)
        {
            //The general command service is auto detected from the binary
            _commands = services.GetRequiredService<CommandService>();
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _services = services;
            _memoryCache = memoryCache;
            _logger = logger;
            _openAiService = openAiService;

            DiscordConfigOptions = discordOptions.Value;

            // Hook CommandExecuted to handle post-command-execution logic.
            _commands.CommandExecuted += CommandExecutedAsync;
            // Hook MessageReceived so we can process each message to see
            // if it qualifies as a command.
            _discord.MessageReceived += MessageReceivedAsync;
            _discord.UserJoined += WelcomeNewUserAsync;

        }

        public async Task InitializeAsync()
        {
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

        }



        //Create the Slash Commands


        public async Task MessageReceivedAsync(SocketMessage rawMessage)
        {
            // Ignore system messages, or messages from other bots
            if (!(rawMessage is SocketUserMessage message))
                return;
            if (message.Source != MessageSource.User)
                return;

            if (message.Author.Id == _discord.CurrentUser.Id || message.Author.IsBot)
            {
                return; // Ignore messages from the bot itself or other bots
            }


            // This value holds the offset where the prefix ends
            var argPos = 0;

            //Handle DM conversations
            if (message.Channel is IDMChannel)
            {
                var context = new SocketCommandContext(_discord, message);
                var conversation = await GetConversationAsync(context, message.Content);

                try
                {
                    if (conversation != null)
                    {
                        conversation.Add(new Message() { role = "user", content = message.Content });
                        var response = await _openAiService.GetChatCompletionAsync(conversation, 4096, 0.9);

                        if (response != null)
                        {
                            //await GetCompletionAsync(prompt, 150, 0.9);
                            await message.Channel.SendMessageAsync($"{response.content}", messageReference: new MessageReference(context.Message.Id));

                            //Set its memory
                            conversation.Add(response);
                            _memoryCache.Set(context.User.Username + "#" + context.User.Discriminator, conversation);
                        }
                        else throw new Exception("an OpenAi issue");

                    }
                    else
                        throw new Exception("a Cache issue");

                }
                catch (Exception ex)
                {
                    await message.Channel.SendMessageAsync($"Hey {context.User.Username}, sorry about this but I seem to be having some issues but my internal commands should still work inside the SmallBets Discord.", messageReference: new MessageReference(context.Message.Id));
                }

            }
            //Intercept a generic word in the chat and respond with helpful info, for us its recordings
            else if ((message.Content.Contains("recording", StringComparison.OrdinalIgnoreCase) ||
                     message.Content.Contains("recordings", StringComparison.OrdinalIgnoreCase)) && message.Content.Contains("?", StringComparison.OrdinalIgnoreCase))
            {
                var context = new SocketCommandContext(_discord, message);
                //find your own channels if you want to link
                var recordingsChannel = context.Guild.TextChannels.FirstOrDefault(x => x.Name == "📼recordings");

                //Pull this data from your own data source
                //var latestRecording = await _cosmosDbService.GetTheLatestRecordingAsync();
                await context.Channel.SendMessageAsync($"Hey {context.User.Username}, I noticed you mentioned recording(s). " +
                    $"\nOur recordings are usually posted in our {recordingsChannel.Mention} channel within 24 hours of the event and Cohort recordings are sent by email. " +
                    $"\nAnd they are also posted on our home site: https://home.smallbets.co/home/recordings " +
                    $"\nThe latest recording up there on our Home site is: Enough GPT to be Dangerous.", messageReference: new MessageReference(context.Message.Id));
            }
            //intercept a fun and unique personality to the bot, uses different prompts and calls
            else if (message.Content.Contains("taleb mode", StringComparison.OrdinalIgnoreCase))
            {
                var context = new SocketCommandContext(_discord, message);

                try
                {
                    string channelName = (context.Channel as ITextChannel).Name;

                    var channelMessages = new List<Message>();
                    if (!String.IsNullOrEmpty(channelName))
                    {
                        channelMessages = await GetChannelConversationAsync(channelName);

                    }

                    var talebConvo = await GetTalebModeConversationAsync(context, message.Content);


                    if (talebConvo != null)
                    {
                        talebConvo.Add(new Message() { role = "user", content = message.Content });


                        var allMessages = new List<Message>(talebConvo.Count +
                                                            channelMessages.Count);
                        allMessages.AddRange(talebConvo);
                        allMessages.AddRange(channelMessages);

                        var response = await _openAiService.GetChatCompletionAsync(allMessages, 4096, 0.9);

                        if (response != null)
                        {
                            await message.Channel.SendMessageAsync($"{response.content}", messageReference: new MessageReference(context.Message.Id));

                            //Set its memory
                            talebConvo.Add(response);
                            _memoryCache.Set(context.User.Username + "#" + context.User.Discriminator + "#taleb", talebConvo);
                        }
                        else throw new Exception("an OpenAi issue");

                    }
                    else
                        throw new Exception("a Cache issue");

                }
                catch (Exception ex)
                {
                    await message.Channel.SendMessageAsync($"Hey {context.User.Username}, I seem to be having some issues but my internal commands should still work. \nChecking my working memory I see {ex.Message}", messageReference: new MessageReference(context.Message.Id));
                }
            }
            else if (message.Reference != null) // Check if the message is a reply
            {
                var referencedMessage = await message.Channel.GetMessageAsync(message.Reference.MessageId.Value);
                if (referencedMessage != null && referencedMessage.Author.Id == _discord.CurrentUser.Id) // Check if the bot is being replied to
                {
                    var context = new SocketCommandContext(_discord, message);
                    try
                    {
                        string channelName = (context.Channel as ITextChannel).Name;

                        var channelMessages = new List<Message>();
                        if (!String.IsNullOrEmpty(channelName))
                        {
                            channelMessages = await GetChannelConversationAsync(channelName);

                        }

                        var conversation = await GetConversationAsync(context, message.Content);

                        if (conversation != null)
                        {
                            conversation.Add(new Message() { role = "user", content = message.Content });


                            var allMessages = new List<Message>(conversation.Count + channelMessages.Count);
                            allMessages.AddRange(conversation);
                            allMessages.AddRange(channelMessages);

                            var response = await _openAiService.GetChatCompletionAsync(allMessages, 4096, 0.9);

                            if (response != null)
                            {
                                //await GetCompletionAsync(prompt, 150, 0.9);
                                await message.Channel.SendMessageAsync($"{response.content}", messageReference: new MessageReference(context.Message.Id));

                                //Set its memory
                                conversation.Add(response);
                                _memoryCache.Set(context.User.Username + "#" + context.User.Discriminator, conversation);
                            }
                            else throw new Exception("an OpenAi issue");

                        }
                        else
                            throw new Exception("a Cache issue");

                    }
                    catch (Exception ex)
                    {
                        await message.Channel.SendMessageAsync($"Hey {context.User.Username}, I seem to be having some issues but my internal commands should still work. \nChecking my working memory I see {ex.Message}", messageReference: new MessageReference(context.Message.Id));
                    }
                }
            }
            //This makes calls to the registered GeneralCommandHandlingService
            else if (message.HasCharPrefix('!', ref argPos)) // You can remove this block if you don't want to use the '!' prefix
            {
                var context = new SocketCommandContext(_discord, message);
                // Perform the execution of the command. In this method,
                // the command service will perform precondition and parsing check
                // then execute the command if one is matched.
                await _commands.ExecuteAsync(context, argPos, _services);
                // Note that normally a result will be returned by this format, but here
                // we will handle the result in CommandExecutedAsync,
            }
            //Substitute your own bots unique identifier here to respond to direct pings to the bot
            else if (message.HasMentionPrefix(_discord.CurrentUser, ref argPos) || message.Content.Contains("<@1084897384045223936>", StringComparison.OrdinalIgnoreCase))
            {
                var context = new SocketCommandContext(_discord, message);

                // Extract the message content without the bot's mention
                string messageContent = message.Content.Substring(argPos).Trim();

                //if it's only the ping, dont make the trip to OpenAi
                if (messageContent == "<@1084897384045223936>")
                    messageContent = String.Empty;

                //get rid of self reference
                if (messageContent.Contains("<@1084897384045223936>"))
                    messageContent.Replace("<@1084897384045223936>", "");

                // Check if the message content is empty or not
                if (!string.IsNullOrEmpty(messageContent))
                {

                    string channelName = (context.Channel as ITextChannel).Name;

                    var channelMessages = new List<Message>();
                    if (!String.IsNullOrEmpty(channelName))
                    {
                        channelMessages = await GetChannelConversationAsync(channelName);

                    }

                    var conversation = await GetConversationAsync(context, message.Content);


                    try
                    {
                        if (conversation != null)
                        {
                            conversation.Add(new Message() { role = "user", content = messageContent });


                            var allMessages = new List<Message>(conversation.Count + channelMessages.Count);
                            allMessages.AddRange(conversation);
                            allMessages.AddRange(channelMessages);

                            var response = await _openAiService.GetChatCompletionAsync(allMessages, 4096, 0.9);

                            if (response != null)
                            {
                                await message.Channel.SendMessageAsync($"{response.content}", messageReference: new MessageReference(context.Message.Id));

                                //Set its memory
                                conversation.Add(response);
                                _memoryCache.Set(context.User.Username + "#" + context.User.Discriminator, conversation);
                            }
                            else throw new Exception("an OpenAi issue");

                        }
                        else
                            throw new Exception("a Cache issue");

                    }
                    catch (Exception ex)
                    {
                        await message.Channel.SendMessageAsync($"Hey {context.User.Username}, I seem to be having some issues but my internal commands should still work. \nChecking my working memory I see {ex.Message}", messageReference: new MessageReference(context.Message.Id));
                    }
                }
                else
                {
                    // Execute your custom logic here or call a command
                    await context.Channel.SendMessageAsync($"Hello {context.User.Username}! You need me?", messageReference: new MessageReference(context.Message.Id));
                }
            }
            else if (message.Content.Contains("smallbot", StringComparison.OrdinalIgnoreCase))
            {

                var context = new SocketCommandContext(_discord, message);
                string channelName = (context.Channel as ITextChannel).Name;

                var channelMessages = new List<Message>();
                if (!String.IsNullOrEmpty(channelName))
                {
                    channelMessages = await GetChannelConversationAsync(channelName);

                }

                var conversation = await GetConversationAsync(context, message.Content);

                try
                {
                    if (conversation != null)
                    {
                        conversation.Add(new Message() { role = "user", content = message.Content });

                        var allMessages = new List<Message>(conversation.Count + channelMessages.Count);
                        allMessages.AddRange(conversation);
                        allMessages.AddRange(channelMessages);

                        var response = await _openAiService.GetChatCompletionAsync(allMessages, 4096, 0.9);

                        if (response != null)
                        {
                            //await GetCompletionAsync(prompt, 150, 0.9);
                            await message.Channel.SendMessageAsync($"{response.content}", messageReference: new MessageReference(context.Message.Id));

                            //Set its memory
                            conversation.Add(response);
                            _memoryCache.Set(context.User.Username + "#" + context.User.Discriminator, conversation);
                        }
                        else throw new Exception("an OpenAi issue");

                    }
                    else
                        throw new Exception("a Cache issue");
                }
                catch (Exception ex)
                {

                    await message.Channel.SendMessageAsync($"Hey {context.User.Username}, I seem to be having some issues but my internal commands should still work. \nChecking my working memory I see {ex.Message}", messageReference: new MessageReference(context.Message.Id));

                }

            }
            else
            {
                var context = new SocketCommandContext(_discord, message);


                // Check if the message is in a thread
                if (context.Channel is IThreadChannel threadChannel)
                {
                    // Get the thread name
                    string threadName = threadChannel.Name;

                    if (!String.IsNullOrEmpty(threadName))
                    {
                        var conversation = await SetChannelConversationAsync(context, threadName, message.Content);

                    }
                }
                //Finally we cache the last few messages from the channel in memory, these are useful for context to ChatGPT
                else
                {
                    // Get the channel name
                    string channelName = (context.Channel as ITextChannel).Name;

                    // Do something with the channel name
                    if (!String.IsNullOrEmpty(channelName))
                    {
                        var conversation = await SetChannelConversationAsync(context, channelName, message.Content);

                    }
                }
            }
        }




        public async Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            // command is unspecified when there was a search failure (command not found); we don't care about these errors
            if (!command.IsSpecified)
                return;

            // the command was successful, we don't care about this result, unless we want to log that a command succeeded.
            if (result.IsSuccess)
                return;

            // the command failed, let's notify the user that something happened.
            await context.Channel.SendMessageAsync($"error: {result}");
        }

        public async Task<List<Message>> GetConversationAsync(SocketCommandContext context, string prompt)
        {
            List<Message> conversation;

            //try to get the conversation, if not make one
            if (!_memoryCache.TryGetValue(context.User.Username + "#" + context.User.Discriminator, out conversation))
            {
                if (conversation == null)
                {
                    conversation = new List<Message>();

                    conversation.Add(new Message { role = "system", content = PromptGPT.SystemPrompt });
                    conversation.Add(new Message { role = "user", content = PromptGPT.seedHumanPrompt });
                    conversation.Add(new Message { role = "user", content = PromptGPT.secondSeedHumanPrompt });
                    var formattedUserinfo = await InfoOnUser(context.User.Username + "#" + context.User.Discriminator);
                    conversation.Add(new Message { role = "user", content = formattedUserinfo });

                    //messageList.Add(new { role = "assistant", content = firstAiResponse });
                    conversation.Add(new Message { role = "user", content = PromptGPT.thirdSeedHumanPrompt + context.User.Username + " here is what they said:" + prompt });

                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromDays(1));

                    _memoryCache.Set(context.User.Username + "#" + context.User.Discriminator, conversation, cacheEntryOptions);
                }
            }

            return conversation;
        }

        public async Task<List<Message>> SetChannelConversationAsync(SocketCommandContext context, string channelName, string message)
        {
            List<Message> conversation;

            //try to get the conversation, if not make one
            if (!_memoryCache.TryGetValue(channelName, out conversation))
            {
                if (conversation == null)
                {
                    conversation = new List<Message>();

                    //messageList.Add(new { role = "assistant", content = firstAiResponse });
                    conversation.Add(new Message { role = "system", content = "In addition, I am providing this for context so you can be more helpful, but these messages were not necessarily directed at you, the AI Assistant SmallBot. And for this conversation you are in the discord channel: " + channelName });
                    conversation.Add(new Message { role = "system", content = "This message directed to the channel was by discord user: " + context.User.Username + ". They said: " + message });

                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromDays(1));

                    _memoryCache.Set(channelName, conversation, cacheEntryOptions);
                }
            }
            else
            {
                //messageList.Add(new { role = "assistant", content = firstAiResponse });
                conversation.Add(new Message { role = "system", content = "This message directed to the channel was by discord user: " + context.User.Username + ". They said to the channel: " + message });

                if (conversation.Count > 10)
                {
                    conversation.RemoveAt(1);
                }

                //expiery is tricky
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromDays(1));

                _memoryCache.Set(channelName, conversation, cacheEntryOptions);
            }
            //conversation.Add(new Message { role = "system", content = "Those were all the recent channel messages; you can choose to use them if they help you in assisting the user or you can ignore them." });


            return conversation;
        }

        public async Task<List<Message>> GetChannelConversationAsync(string channelName)
        {
            List<Message> conversation;

            //try to get the conversation, if not make one
            if (!_memoryCache.TryGetValue(channelName, out conversation))
            {
                if (conversation == null)
                    conversation = new List<Message>();

                //conversation.Add(new Message { role = "system", content = "Those were all the recent channel messages; you can choose to use them if they help you in assisting the user or you can ignore them." });
                return conversation;
            }

            if (conversation == null)
                conversation = new List<Message>();
            //conversation.Add(new Message { role = "system", content = "Those were all the recent channel messages; you can choose to use them if they help you in assisting the user or you can ignore them." });
            return conversation;
        }



        public async Task<List<Message>> GetTalebModeConversationAsync(SocketCommandContext context, string prompt)
        {
            List<Message> conversation;

            //try to get the conversation, if not make one
            if (!_memoryCache.TryGetValue(context.User.Username + "#" + context.User.Discriminator + "#taleb", out conversation))
            {
                if (conversation == null)
                {
                    conversation = new List<Message>();

                    conversation.Add(new Message { role = "system", content = PromptGPT.talebSystemPrompt });
                    conversation.Add(new Message { role = "user", content = PromptGPT.seedTalebPrompt });
                    conversation.Add(new Message { role = "user", content = PromptGPT.secondSeedTalebPrompt });

                    var formattedUserinfo = await InfoOnUser(context.User.Username + "#" + context.User.Discriminator);
                    conversation.Add(new Message { role = "user", content = formattedUserinfo });

                    //messageList.Add(new { role = "assistant", content = firstAiResponse });
                    conversation.Add(new Message { role = "user", content = PromptGPT.thirdSeedTalebPrompt + context.User.Username + " here is what they said:" + prompt });

                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromDays(1));

                    _memoryCache.Set(context.User.Username + "#" + context.User.Discriminator + "#taleb", conversation, cacheEntryOptions);
                }
            }

            return conversation;
        }

        public async Task<string> InfoOnUser(string userNamme)
        {
            try
            {
                //To do wire the bot to your own data, this context is very useful for ChatGPT
                //var smallBetsUserInfo = await _cosmosDbService.GetUserInfoByDiscordIdAsync(userNamme);
                var smallBetsUserInfo = GetSampleUserInfoData();

                String userInfoFormmated = String.Empty;

                if (smallBetsUserInfo != null)
                {

                    var bob = new StringBuilder();
                    bob.AppendLine("This info is meant to help you better assist me, here is what the Small Bets Community Directory has on me:");

                    bob.AppendLine($"Name: {smallBetsUserInfo.FirstName} {smallBetsUserInfo.LastName}");

                    if (!String.IsNullOrEmpty(smallBetsUserInfo.TwitterUrl))
                        bob.AppendLine($"Twitter: {smallBetsUserInfo.TwitterUrl}");

                    if (!String.IsNullOrEmpty(smallBetsUserInfo.LinkedInUrl))
                        bob.AppendLine($"LinkedIn: {smallBetsUserInfo.LinkedInUrl}");

                    if (!String.IsNullOrEmpty(smallBetsUserInfo.Location))
                        bob.AppendLine($"Location: {smallBetsUserInfo.Location}");

                    if (smallBetsUserInfo.TotalNumberOfProjects.HasValue)
                        bob.AppendLine($"Number of Projects I have: {smallBetsUserInfo.TotalNumberOfProjects.Value}");


                    if (smallBetsUserInfo.JoinDate.HasValue)
                        bob.AppendLine($"Date I joined Small Bets: {smallBetsUserInfo.JoinDate.Value.ToString("yyyy-MM-dd")}");


                    userInfoFormmated = bob.ToString();
                }

                if (String.IsNullOrEmpty(userInfoFormmated))
                    userInfoFormmated = "There is no info about me in the Small Bets Directory at home.smallbets.co and if you get a chance you should remind me to either setup my profile or connect my discord so you can assist me better.";


                return userInfoFormmated;
            }
            catch
            {
                return String.Empty;
            }
        }

        private async Task WelcomeNewUserAsync(SocketGuildUser user)
        {
            var guildUser = user;

            //TO do wire the bot to your own community directory
            //var smallBetsUserInfo = await _cosmosDbService.GetUserInfoByDiscordIdAsync(guildUser.Username + "#" + guildUser.Discriminator);

            var smallBetsUserInfo = GetSampleUserInfoData();

            String userInfoFormmated = String.Empty;

            var bob = new StringBuilder();
            bob.AppendLine($"Welcome to the Small Bets Community, {user.Mention}! We're glad to have you here.");
            bob.AppendLine($"");
            bob.AppendLine($"My name is SmallBot and I am an AI powered bot custom built just to help you and other members of the Small Bets community.");
            bob.AppendLine($"");


            if (smallBetsUserInfo != null)
            {
                bob.AppendLine($"It seems you have already set up your community profile on our website and connected your discord! Very awesome. This will allow community members to see who you are, see your projects, and help you better.");


            }
            else
            {
                bob.AppendLine($"I was unable to find your Small Bets Directory profile. No worries if you haven't set it up yet.");
                bob.AppendLine($"");
                bob.AppendLine(@"You can set up your Direcotry Profile anytime on our website, over at: https://home.smallbets.co/Home/Directory");
                bob.AppendLine(@"Once set up, that profile is connected directly to our community discord. This allows community members to see who you are, see your projects, and maybe help you better.");
                bob.AppendLine($"");
                bob.AppendLine(@"If you did set up your profile, then maybe I was unable to pull it up because your discord is not yet connected. You can connect your discord handle with one click right from your profile on our website https://home.smallbets.co/Home/Directory");

            }

            bob.AppendLine($"");
            bob.AppendLine(@"By the way, I also wanted to let you know that inside our community discord; I have a few powerful commands that you can invoke, such as /help and /who-is user and several others.");
            bob.AppendLine($"");
            bob.AppendLine($"Anyway, welcome again. It's great to have you here.");


            userInfoFormmated = bob.ToString();


            var embedBuiler = new EmbedBuilder()
                .WithTitle($"Welcome to Small Bets")
                .WithDescription(userInfoFormmated)
                .WithCurrentTimestamp();

            embedBuiler.WithColor(Color.Blue);


            // Alternatively, send a direct message to the new user
            var dmChannel = await user.CreateDMChannelAsync();
            await dmChannel.SendMessageAsync(embed: embedBuiler.Build());

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
                JoinDate = DateTime.Now.AddDays(-2),
                TotalNumberOfProjects = 2
            };
        }

    }



}
