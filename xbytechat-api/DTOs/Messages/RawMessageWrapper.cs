// DTOs/Messages/RawMessageWrapper.cs
using Newtonsoft.Json.Linq;

public class RawMessageWrapper
{
    public string MessageType { get; set; } = string.Empty;
    public JObject Payload { get; set; } = new JObject();
}
