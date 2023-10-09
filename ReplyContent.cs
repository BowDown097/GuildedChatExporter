using Newtonsoft.Json.Linq;

namespace GuildedChatExporter;

public class ReplyContent
{
    public string Content { get; } = string.Empty;
    public JArray Data { get; } = new();

    public ReplyContent(JToken msg)
    {
        foreach (JToken docNode in msg["content"]?["document"]?["nodes"]!)
        {
            if (docNode["data"]?["src"] != null)
                Data.Add(docNode["data"]!);

            foreach (JToken node in docNode["nodes"]!)
            {
                foreach (JToken leaf in node.ValueOr("leaves", new JArray()))
                    Content += leaf["text"]?.ToString();

                foreach (JToken childNode in node.ValueOr("nodes", new JArray()))
                    foreach (JToken childLeaf in childNode.ValueOr("leaves", new JArray()))
                        Content += childLeaf["text"]?.ToString();
            }
        }
    }
}