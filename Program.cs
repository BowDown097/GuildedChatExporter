using System.Net;
using System.Net.Http.Json;
using System.Web;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using ByteSizeLib;
using Newtonsoft.Json.Linq;
using Sharprompt;

namespace GuildedChatExporter;

public static class Program
{
    private static readonly CookieContainer Cookies = new();
    private static readonly HttpClientHandler ClientHandler = new() { CookieContainer = Cookies };
    private static readonly HttpClient Client = new(ClientHandler);

    private static void DownloadFile(List<Task> downloadTasks, string url, string path)
    {
        if (File.Exists(path))
            return;
        File.Create(path).Dispose();
        downloadTasks.Add(Client.GetFileAsync(url, path));
    }

    private static void SetUpAuthorAvatar(JToken author, List<Task> downloadTasks, string folder,
        IDocument document, IElement div, string @class)
    {
        if (author["profilePicture"] != null)
        {
            string pfpUrl = author["profilePicture"]!.ToString();
            string filename = UrlFilename(pfpUrl);
            DownloadFile(downloadTasks, pfpUrl, Path.Join(folder, filename));

            var avatar = document.CreateElement<IHtmlImageElement>(div, @class);
            avatar.Source = Path.Join("_files", filename);
            avatar.SetAttribute("loading", "lazy");
        }
        else
        {
            DownloadFile(downloadTasks, ApiConstants.DefaultAvatar, Path.Join(folder, ApiConstants.DefaultAvatarName));
            var avatar = document.CreateElement<IHtmlImageElement>(div, @class);
            avatar.Source = Path.Join("_files", ApiConstants.DefaultAvatarName);
        }
    }

    private static string UrlFilename(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? Path.GetFileName(uri.LocalPath) : string.Empty;

    public static async Task Main()
    {
        // get email and password
        string email = Prompt.Input<string>("Enter your login e-mail");
        string password = Prompt.Password("Enter your login password");
        Console.WriteLine();

        // log in
        object loginDetails = new { email = email.Trim(), getMe = true, password = password.Trim() };
        HttpResponseMessage loginMessage = await Client.PostAsJsonAsync(ApiConstants.LoginEndpoint, loginDetails);
        string loginResp = await loginMessage.Content.ReadAsStringAsync();

        if (!loginResp.TryParseJson(out JToken loginObj) || !loginMessage.IsSuccessStatusCode)
        {
            Console.WriteLine($"Login request failed: {loginObj["code"]} - {loginObj["message"]}");
            return;
        }
        
        Console.WriteLine($"Logged in as {loginObj["user"]?["name"]}\n");
        
        // select server
        if (loginObj["teams"] is not JArray servers || servers.Count == 0)
        {
            Console.WriteLine("Not in any servers.");
            return;
        }

        for (int i = 0; i < servers.Count; i++)
            Console.WriteLine($"{i + 1} - {servers[i]["name"]}");
        int serverNum = Prompt.Input<int>("Select a server number");
        JToken server = servers[serverNum - 1];
        Console.WriteLine();
        
        // get server members
        JToken membersObj = await Client.GetJsonAsync(string.Format(ApiConstants.MembersEndpoint, server["id"]));
        if (membersObj["members"] is not JArray members || members.Count == 0)
        {
            Console.WriteLine("No members found.");
            return;
        }
        
        // get server channels
        JToken channelsObj = await Client.GetJsonAsync(string.Format(ApiConstants.ChannelsEndpoint, server["id"]));
        if (channelsObj["channels"] is not JArray channels || channels.Count == 0)
        {
            Console.WriteLine("No channels found.");
            return;
        }
        
        // select channel
        for (int i = 0; i < channels.Count; i++)
            Console.WriteLine($"{i + 1} - {channels[i]["name"]}");
        int channelNum = Prompt.Input<int>("Select a channel number");
        JToken channel = channels[channelNum - 1];
        Console.WriteLine();
        
        // set up channel folders
        string channelFolder = Path.Join("out", server["id"]!.ToString(), channel["id"]!.ToString());
        string channelFilesFolder = Path.Join(channelFolder, "_files");
        Directory.CreateDirectory(channelFilesFolder);
        File.Copy("styles.css", Path.Join(channelFilesFolder, "styles.css"), true);
        
        string? beforeDate = null;
        List<Task> downloadTasks = new List<Task>();
        bool hasMoreMsgs = true;
        int page = 1;

        while (hasMoreMsgs)
        {
            downloadTasks.RemoveAll(t => t.IsCompleted);

            // construct url
            string msgsUrl = string.IsNullOrEmpty(beforeDate)
                ? string.Format(ApiConstants.MessagesEndpoint, channel["id"])
                : string.Format(ApiConstants.MessagesEndpointWDate, channel["id"], HttpUtility.UrlEncode(beforeDate));

            // get messages
            JToken msgsObj = await Client.GetJsonAsync(msgsUrl);
            if (msgsObj["messages"] is not JArray msgs)
            {
                Console.WriteLine($"Failed to get messages for beforeDate {beforeDate}");
                Thread.Sleep(1000);
                continue;
            }
            
            // create document
            IBrowsingContext context = BrowsingContext.New(Configuration.Default);
            IDocument doc = await context.OpenNewAsync();

            // stylesheet
            var link = doc.CreateElement<IHtmlLinkElement>(doc.Head);
            link.Href = "_files/styles.css";
            link.Relation = "stylesheet";
            
            // header and chat log
            doc.CreateElement("h2", doc.Body, "channelname", channel["name"]!.ToString());
            var chatLog = doc.CreateElement<IHtmlDivElement>(doc.Body, "chatlog");

            foreach (JToken msg in msgs)
            {
                // replies
                if (msg["repliesToIds"] is JArray repliesToIds)
                {
                    var repliesDiv = doc.CreateElement<IHtmlDivElement>(chatLog, "replies");
                    foreach (JToken reply in repliesToIds)
                    {
                        var replyDiv = doc.CreateElement<IHtmlDivElement>(repliesDiv, "reply");

                        // find message
                        JToken? replyMsg = msgs.FirstOrDefault(t => reply.ValueEquals(t["id"]));
                        if (replyMsg == null)
                        {
                            doc.CreateElement<IHtmlSpanElement>(replyDiv, "reply-content", $"Could not find reply ({reply})");
                            continue;
                        }

                        // author
                        JToken? replyAuthor = members.FirstOrDefault(t => t["id"].ValueEquals(replyMsg["createdBy"]));
                        if (replyAuthor != null)
                        {
                            SetUpAuthorAvatar(replyAuthor, downloadTasks, channelFilesFolder, doc, replyDiv, "reply-author-avatar");
                            doc.CreateElement<IHtmlSpanElement>(replyDiv, "reply-author-name", replyAuthor["name"]?.ToString());
                        }

                        // get and add content
                        ReplyContent content = new(replyMsg);
                        if (!string.IsNullOrEmpty(content.Content))
                            doc.CreateElement<IHtmlSpanElement>(replyDiv, "reply-content", content.Content);
                        if (content.Data.Count != 0)
                            doc.CreateElement<IHtmlSpanElement>(replyDiv, "reply-media", "[media]");
                    }
                }

                // message divs
                var msgDiv = doc.CreateElement<IHtmlDivElement>(chatLog, "message");
                var authorContainer = doc.CreateElement<IHtmlDivElement>(msgDiv, "author-container");
                var msgContainer = doc.CreateElement<IHtmlDivElement>(msgDiv, "message-container");

                // author
                JToken? author = members.FirstOrDefault(t => t["id"].ValueEquals(msg["createdBy"]));
                if (author != null)
                {
                    SetUpAuthorAvatar(author, downloadTasks, channelFilesFolder, doc, authorContainer, "author-avatar");
                    doc.CreateElement<IHtmlSpanElement>(msgContainer, "author-name", author["name"]?.ToString());
                }

                // timestamp and content
                doc.CreateElement<IHtmlSpanElement>(msgContainer, "message-timestamp", $"{msg["createdAt"]} • ID: {msg["id"]}");
                var msgContent = doc.CreateElement<IHtmlDivElement>(msgContainer, "message-content");

                foreach (JToken docNode in msg["content"]?["document"]?["nodes"]!)
                {
                    // message text
                    var msgText = doc.CreateElement<IHtmlSpanElement>(msgContent);
                    foreach (JToken node in docNode["nodes"]!)
                    {
                        foreach (JToken leaf in node.ValueOr("leaves", new JArray()))
                            msgText.InnerHtml += leaf["text"]?.ToString();

                        switch (node["type"]?.ToString())
                        {
                            case "link":
                            {
                                string dataHref = node["data"]!["href"]!.ToString();
                                var msgLink = doc.CreateElement<IHtmlAnchorElement>(msgText, "message-link", dataHref);
                                msgLink.Href = dataHref;
                                break;
                            }
                            case "reaction":
                            {
                                if (node["data"]?["reaction"]?["customReaction"]?["png"]?.Type == JTokenType.String)
                                {
                                    JToken customReaction = node["data"]!["reaction"]!["customReaction"]!;
                                    string emojiName = customReaction["name"]!.ToString();

                                    string image = customReaction["apng"]!.Type == JTokenType.String
                                        ? customReaction["apng"]!.ToString()
                                        : customReaction["png"]!.ToString();
                                    if (image.StartsWith("/asset"))
                                        image = "https://img.guildedcdn.com" + image;

                                    string imageFilename = UrlFilename(image);
                                    DownloadFile(downloadTasks, image, Path.Join(channelFilesFolder, imageFilename));

                                    var msgImage = doc.CreateElement<IHtmlImageElement>(msgText, "message-emoji");
                                    msgImage.AlternativeText = emojiName;
                                    msgImage.Source = Path.Join("_files", imageFilename);
                                    msgImage.SetAttribute("loading", "lazy");
                                }
                                else
                                {
                                    foreach (JToken childNode in node.ValueOr("nodes", new JArray()))
                                    foreach (JToken childLeaf in childNode.ValueOr("leaves", new JArray()))
                                        msgText.InnerHtml += childLeaf["text"]?.ToString();
                                }

                                break;
                            }
                            default:
                            {
                                foreach (JToken childNode in node.ValueOr("nodes", new JArray()))
                                foreach (JToken childLeaf in childNode.ValueOr("leaves", new JArray()))
                                    msgText.InnerHtml += childLeaf["text"]?.ToString();
                                break;
                            }
                        }
                    }
                    
                    // large emojis
                    if (string.IsNullOrWhiteSpace(msgText.TextContent))
                        foreach (IElement child in msgText.Children.Where(c => c.ClassName == "message-emoji"))
                            child.ClassList.Add("message-emoji--large");

                    // attachments
                    if (docNode["data"]?["src"] == null)
                        continue;

                    string src = docNode["data"]!["src"]!.ToString();
                    string filename = UrlFilename(src);
                    DownloadFile(downloadTasks, src, Path.Join(channelFilesFolder, filename));

                    string? type = docNode["type"]?.ToString();
                    switch (type)
                    {
                        case "fileUpload":
                        {
                            var msgFileContainer = doc.CreateElement<IHtmlDivElement>(msgContent, "message-file-container");

                            var msgFile = doc.CreateElement<IHtmlAnchorElement>(msgFileContainer,
                                "message-file", docNode["data"]!["name"]?.ToString());
                            msgFile.Href = Path.Join("_files", filename);

                            doc.CreateElement<IHtmlSpanElement>(msgFileContainer, "message-file-size",
                                ByteSize.FromBytes(docNode["data"]!["fileSizeBytes"]?.Value<double>() ?? 0).ToString());
                            break;
                        }
                        case "image":
                        {
                            var msgImage = doc.CreateElement<IHtmlImageElement>(msgContent, "message-media");
                            msgImage.Source = Path.Join("_files", filename);
                            msgImage.SetAttribute("loading", "lazy");
                            break;
                        }
                        case "video":
                        {
                            var msgVideo = doc.CreateElement<IHtmlVideoElement>(msgContent, "message-media");
                            msgVideo.IsShowingControls = true;
                            msgVideo.Source = Path.Join("_files", filename);
                            msgVideo.Title = docNode["data"]!["name"]?.ToString() ?? filename;
                            break;
                        }
                    }
                }
            }
            
            // footer
            IElement footer = doc.CreateElement("footer", doc.Body);

            var backPage = doc.CreateElement<IHtmlAnchorElement>(footer, "message-link", "<");
            if (page == 1)
            {
                backPage.ClassList.Add("disabled");
                backPage.Href = "#";
            }
            else
            {
                backPage.Href = $"{channel["id"]}-page{page - 1}.html";
            }

            doc.CreateElement<IHtmlSpanElement>(footer, null, $"Page {page}");

            var forwardPage = doc.CreateElement<IHtmlAnchorElement>(footer, "message-link", ">");
            forwardPage.Href = $"{channel["id"]}-page{page + 1}.html";
            
            // write document to file
            await File.WriteAllTextAsync(
                Path.Join(channelFolder, $"{channel["id"]}-page{page}.html"),
                doc.DocumentElement.OuterHtml
            );
            
            // update vars
            beforeDate = msgs.Last()["createdAt"]?.ToString();
            hasMoreMsgs = msgs.Count >= 50;
            page++;
            
            Console.WriteLine($"Processed messages up to {beforeDate}");
            Thread.Sleep(1000);
        }

        downloadTasks.RemoveAll(t => t.IsCompleted);
        if (downloadTasks.Count > 0)
        {
            Console.WriteLine("Waiting for downloads to finish...");
            await Task.WhenAll(downloadTasks);
        }
    }
}