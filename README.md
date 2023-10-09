# GuildedChatExporter
**GuildedChatExporter** exports message history from Guilded servers to easily digestable HTML files. Inspired by [its Discord equivalent](https://github.com/Tyrrrz/DiscordChatExporter).

### What is supported
- Channels with text content in servers
- Emojis
- Media

### What is not supported
Including, but I'm very sure not limited to:
- DMs
- Polls
- Reactions

### Fair warning
This code is pretty shoddy. There may be a crash or things may not export properly. That being said, it should perform pretty well at least, granted there is a 1 second pause between each group of messages exported to avoid rate limiting, account bans, and so on. Keep in mind that you still do risk these things happening by using this tool. To help calm the mind a little, I tested this with ~100 pages of messages with no problems.
