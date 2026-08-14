using System.Collections.ObjectModel;
using RuneMobile.Models;

namespace RuneMobile;

public partial class MainPage : ContentPage
{
    public ObservableCollection<ChatMessage> Messages { get; set; } = new();

    public MainPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    private void OnSendClicked(object sender, EventArgs e)
    {
        var text = InputBox.Text;
        if (string.IsNullOrWhiteSpace(text)) return;

        Messages.Add(new ChatMessage { Text = text, IsUser = true });
        InputBox.Text = string.Empty;

        Messages.Add(new ChatMessage { Text = "[model not wired yet — next step]", IsUser = false });
        ChatList.ScrollTo(Messages.Count - 1);
    }
}
