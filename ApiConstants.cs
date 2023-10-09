namespace GuildedChatExporter;

public static class ApiConstants
{
    public const string DefaultAvatar = "https://img.guildedcdn.com/asset/DefaultUserAvatars/profile_3.png";
    public const string DefaultAvatarName = "profile_3.png";
    
    public const string ChannelsEndpoint = "https://www.guilded.gg/api/teams/{0}/channels?excludeBadgedContent=true";
    public const string LoginEndpoint = "https://www.guilded.gg/api/login";
    public const string MembersEndpoint = "https://www.guilded.gg/api/teams/{0}/members";
    public const string MessagesEndpoint = "https://www.guilded.gg/api/channels/{0}/messages?limit=50&maxReactionUsers=8";
    public const string MessagesEndpointWDate = "https://www.guilded.gg/api/channels/{0}/messages?beforeDate={1}&limit=50&maxReactionUsers=8";
}