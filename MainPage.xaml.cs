using System.Collections.ObjectModel;
using RuneMobile.Models;
using RuneMobile.Services;

namespace RuneMobile;

public partial class MainPage : ContentPage
{
    public ObservableCollection<ChatMessage> Messages { get; set; } = new();
    private readonly LlamaService _llama = new();

    public MainPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        var text = InputBox.Text;
        if (string.IsNullOrWhiteSpace(text)) return;

        Messages.Add(new ChatMessage { Text = text, IsUser = true });
        InputBox.Text = string.Empty;

        if (!_llama.ModelExists)
        {
            Messages.Add(new ChatMessage
            {
                Text = "No model found. Place chat-model.gguf in Android/data/com.onyx.runemobile/files/models/",
                IsUser = false
            });
            ChatList.ScrollTo(Messages.Count - 1);
            return;
        }

        var thinking = new ChatMessage { Text = "...", IsUser = false };
        Messages.Add(thinking);
        ChatList.ScrollTo(Messages.Count - 1);

        try
        {
            var reply = await _llama.GetReplyAsync(text);
            Messages.Remove(thinking);
            Messages.Add(new ChatMessage { Text = reply, IsUser = false });
        }
        catch (Exception ex)
        {
            Messages.Remove(thinking);
            Messages.Add(new ChatMessage { Text = $"Error: {ex.Message}", IsUser = false });
        }

        ChatList.ScrollTo(Messages.Count - 1);
    }
}
