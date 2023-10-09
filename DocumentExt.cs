using AngleSharp.Dom;

namespace GuildedChatExporter;

public static class DocumentExt
{
    private static void SetUpElement<T>(ref T element, IElement? parent, string? className, string? content)
        where T : IElement
    {
        parent?.AppendChild(element);
        if (!string.IsNullOrWhiteSpace(className))
            element.ClassName = className;
        if (!string.IsNullOrWhiteSpace(content))
            element.TextContent = content;
    }

    public static IElement CreateElement(this IDocument doc, string name, IElement? parent,
        string? className = null, string? content = null)
    {
        IElement element = doc.CreateElement(name);
        SetUpElement(ref element, parent, className, content);
        return element;
    }

    public static T CreateElement<T>(this IDocument doc, IElement? parent, string? className = null,
        string? content = null) where T : IElement
    {
        T element = doc.CreateElement<T>();
        SetUpElement(ref element, parent, className, content);
        return element;
    }
}