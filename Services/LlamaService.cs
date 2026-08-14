using LLama;
using LLama.Common;
using LLama.Native;

namespace RuneMobile.Services;

public class LlamaService
{
    private LLamaWeights? _model;
    private LLamaContext? _context;
    private InteractiveExecutor? _executor;
    private bool _initialized;

    public string ModelPath => Path.Combine(
        Android.App.Application.Context.GetExternalFilesDir(null)!.AbsolutePath,
        "models", "chat-model.gguf");

    public bool ModelExists => File.Exists(ModelPath);

    public void Initialize()
    {
        if (_initialized) return;

        var nativeLibDir = Android.App.Application.Context.ApplicationInfo!.NativeLibraryDir;
        var llamaLib = Path.Combine(nativeLibDir!, "libllama.so");
        NativeLibraryConfig.All.WithLibrary(llamaLib, null);

        var parameters = new ModelParams(ModelPath)
        {
            ContextSize = 2048,
            GpuLayerCount = 0
        };

        _model = LLamaWeights.LoadFromFile(parameters);
        _context = _model.CreateContext(parameters);
        _executor = new InteractiveExecutor(_context);

        _initialized = true;
    }

    public async Task<string> GetReplyAsync(string userMessage)
    {
        if (!_initialized) Initialize();

        var inferenceParams = new InferenceParams
        {
            MaxTokens = 256,
            AntiPrompts = new List<string> { "User:", "\n\n" }
        };

        var prompt = $"User: {userMessage}\nAssistant:";
        var sb = new System.Text.StringBuilder();

        await foreach (var token in _executor!.InferAsync(prompt, inferenceParams))
        {
            sb.Append(token);
        }

        return sb.ToString().Trim();
    }
}
