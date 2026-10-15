namespace SenderPro.Core;

public sealed class ContentItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
    public string Tags { get; set; } = "";
    public List<string> MediaPaths { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
