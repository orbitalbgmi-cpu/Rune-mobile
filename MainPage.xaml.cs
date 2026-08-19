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
        CheckForCrashLog();
    }

    private void CheckForCrashLog()
    {
        try
        {
            var dir = Android.App.Application.Context.GetExternalFilesDir(null)!.AbsolutePath;
            var path = Path.Combine(dir, "crash-log.txt");
            if (File.Exists(path))
            {
                var content = File.ReadAllText(path);
                Messages.Add(new ChatMessage { Text = $"Previous crash log:\n{content}", IsUser = false });
                File.Delete(path);
            }
        }
        catch { }
    }

    private async void OnImportChatModelClicked(object sender, EventArgs e)
    {
        await ImportModelAsync("chat-model.gguf");
    }

    private async void OnImportImageModelClicked(object sender, EventArgs e)
    {
        await ImportModelAsync("sd-model.gguf");
    }

    private async Task ImportModelAsync(string targetFileName)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = $"Select {targetFileName}"
            });

            if (result == null) return;

            var modelsDir = Path.Combine(
                Android.App.Application.Context.GetExternalFilesDir(null)!.AbsolutePath,
                "models");
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
                Text = "No chat model found. Tap 'Import Chat Model' above.",
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
            Messages.Add(new ChatMessage { Text = $"Error: {ex}", IsUser = false });
        }

        ChatList.ScrollTo(Messages.Count - 1);
    }
}
