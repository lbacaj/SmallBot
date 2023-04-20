namespace SmallBot.Models
{
    public class ChatAPIResponse
    {
        public string Id { get; set; }
        public string Object { get; set; }
        public long Created { get; set; }
        public ChoiceChat[] Choices { get; set; }
        public Usage Usage { get; set; }
    }

    public class ChoiceChat
    {
        public int Index { get; set; }
        public Message Message { get; set; }
        public string Finish_reason { get; set; }
    }

    public class Message
    {
        public string role { get; set; }
        public string content { get; set; }
    }

    public class Usage
    {
        public int Prompt_tokens { get; set; }
        public int Completion_tokens { get; set; }
        public int Total_tokens { get; set; }
    }
}
