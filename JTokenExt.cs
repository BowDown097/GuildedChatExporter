using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GuildedChatExporter;

public static class JTokenExt
{
    // kind of cheating, idc
    public static bool TryParseJson(this string json, out JToken token)
    {
        using StringReader sr = new(json);
        using JsonTextReader jr = new(sr);
        jr.DateParseHandling = DateParseHandling.None;

        try
        {
            token = JToken.ReadFrom(jr);
            return true;
        }
        catch (JsonReaderException)
        {
            token = new JObject();
            return false;
        }
    }

    public static bool ValueEquals(this JToken? t, JToken? o) => (t as JValue)?.CompareTo(o as JValue) == 0;
    public static JToken ValueOr(this JToken? t, string key, JToken or) => t?[key] ?? or;
}