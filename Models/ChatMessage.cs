namespace RuneMobile.Models;

public class ChatMessage
{
    public string Text { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public ImageSource? Image { get; set; }
    public bool HasImage => Image != null;
    public bool HasText => !string.IsNullOrEmpty(Text);
    public Color BubbleColor => IsUser ? Color.FromArgb("#1f6feb") : Color.FromArgb("#21262d");
    public LayoutOptions Alignment => IsUser ? LayoutOptions.End : LayoutOptions.Start;
}
