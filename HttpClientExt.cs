using Newtonsoft.Json.Linq;

namespace GuildedChatExporter;

public static class HttpClientExt
{
    public static async Task GetFileAsync(this HttpClient client, string uri, string path)
    {
        await using Stream s = await client.GetStreamAsync(uri);
        await using FileStream fs = File.Open(path, FileMode.Open, FileAccess.Write);
        await s.CopyToAsync(fs);
    }
    
    public static async Task<JToken> GetJsonAsync(this HttpClient client, string uri)
    {
        string message = await client.GetStringAsync(uri);
        return message.TryParseJson(out JToken token) ? token : new JObject();
    }
}