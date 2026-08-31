using System.Collections.ObjectModel;
using RuneMobile.Models;
using RuneMobile.Services;

namespace RuneMobile;

public partial class MainPage : ContentPage
{
    public ObservableCollection<ChatMessage> Messages { get; set; } = new();
    private readonly LlamaService _llama = new();
    private readonly ImageService _image = new();
    private static readonly string[] ImageCommands = { "/image", "/picture", "/photo" };

    public MainPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    private async void OnImportChatModelClicked(object sender, EventArgs e) => await ImportModelAsync("chat-model.gguf");
    private async void OnImportImageModelClicked(object sender, EventArgs e) => await ImportModelAsync("sd-model.gguf");

    private async Task ImportModelAsync(string targetFileName)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = $"Select {targetFileName}" });
            if (result == null) return;

            var modelsDir = Path.Combine(Android.App.Application.Context.GetExternalFilesDir(null)!.AbsolutePath, "models");
            Directory.CreateDirectory(modelsDir);
            var targetPath = Path.Combine(modelsDir, targetFileName);

            Messages.Add(new ChatMessage { Text = $"Copying {targetFileName}...", IsUser = false });
            ChatList.ScrollTo(Messages.Count - 1);

            using var sourceStream = await result.OpenReadAsync();
            using var destStream = File.Create(targetPath);
            await sourceStream.CopyToAsync(destStream);

            var fileInfo = new FileInfo(targetPath);
            Messages.Add(new ChatMessage { Text = $"{targetFileName} imported successfully ({fileInfo.Length / 1024 / 1024} MB).", IsUser = false });
            ChatList.ScrollTo(Messages.Count - 1);
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage { Text = $"Import failed: {ex}", IsUser = false });
            ChatList.ScrollTo(Messages.Count - 1);
        }
    }

    private void OnSaveImageClicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not ChatMessage msg || msg.ImageBytes == null) return;
        try
        {
            var fileName = $"RUNE-{DateTime.Now:yyyyMMdd-HHmmss}.png";
            _image.SaveToGallery(msg.ImageBytes, fileName);
            Messages.Add(new ChatMessage { Text = $"Saved to Pictures/RUNE/{fileName}", IsUser = false });
            ChatList.ScrollTo(Messages.Count - 1);
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage { Text = $"Save failed: {ex.Message}", IsUser = false });
            ChatList.ScrollTo(Messages.Count - 1);
        }
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        var text = InputBox.Text;
        if (string.IsNullOrWhiteSpace(text)) return;

        Messages.Add(new ChatMessage { Text = text, IsUser = true });
        InputBox.Text = string.Empty;

        var matchedCommand = ImageCommands.FirstOrDefault(cmd => text.StartsWith(cmd, StringComparison.OrdinalIgnoreCase));
        if (matchedCommand != null)
        {
            await HandleImageRequestAsync(text[matchedCommand.Length..].Trim());
            return;
        }

        await HandleChatRequestAsync(text);
    }

    private async Task HandleChatRequestAsync(string text)
    {
        if (!_llama.ModelExists)
        {
            Messages.Add(new ChatMessage { Text = "No chat model found. Tap 'Import Chat Model' above.", IsUser = false });
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
            Messages.Add(new ChatMessage { Text = $"Error: {ex}", IsUser = false });
        }

        ChatList.ScrollTo(Messages.Count - 1);
    }

    private async Task HandleImageRequestAsync(string prompt)
    {
        if (!_image.ModelExists)
        {
            Messages.Add(new ChatMessage { Text = "No image model found. Tap 'Import Image Model' above.", IsUser = false });
            ChatList.ScrollTo(Messages.Count - 1);
            return;
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            Messages.Add(new ChatMessage { Text = "Add a description after the command, e.g. /image a red bicycle", IsUser = false });
            ChatList.ScrollTo(Messages.Count - 1);
            return;
        }

        var thinking = new ChatMessage { Text = "Starting...", IsUser = false };
        Messages.Add(thinking);
        ChatList.ScrollTo(Messages.Count - 1);

        try
        {
            var pngBytes = await _image.GenerateAsync(prompt, progressLine =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    thinking.Text = progressLine;
                    var idx = Messages.IndexOf(thinking);
                    if (idx >= 0) Messages[idx] = thinking;
                    ChatList.ScrollTo(Messages.Count - 1);
                });
            });

            Messages.Remove(thinking);
            Messages.Add(new ChatMessage
            {
                Image = ImageSource.FromStream(() => new MemoryStream(pngBytes)),
                ImageBytes = pngBytes,
                IsUser = false
            });
        }
        catch (Exception ex)
        {
            Messages.Remove(thinking);
            Messages.Add(new ChatMessage { Text = $"Image generation error: {ex.Message}", IsUser = false });
        }

        ChatList.ScrollTo(Messages.Count - 1);
    }
}
