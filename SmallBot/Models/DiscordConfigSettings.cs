namespace SmallBot.Models
{
    public class DiscordConfigSettings
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string BotToken { get; set; }

        public string GuildId { get; set; }

        public string TestGuildId { get; set; }

        public string RecordingsChannel { get; set; }

    }
}
