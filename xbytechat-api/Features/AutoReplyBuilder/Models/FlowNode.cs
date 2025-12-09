namespace xbytechat.api.Features.AutoReplyBuilder.Models
{
    public class FlowNode
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public Position Position { get; set; }
        public Dictionary<string, object> Data { get; set; }  // This should capture config
    }
}
