using System.Text.RegularExpressions;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;

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

        var sampling = new DefaultSamplingPipeline
        {
            RepeatPenalty = 1.3f,
            Temperature = 0.7f
        };

        var inferenceParams = new InferenceParams
        {
            MaxTokens = 120,
            AntiPrompts = new List<string> { "User:", "\nUser", "<|im_end|>", "<|im_start|>" },
            SamplingPipeline = sampling
        };

        var prompt = $"<|im_start|>system\nYou are RUNE, a helpful offline assistant. Reply in plain text only. Never use emojis or special symbols.<|im_end|>\n<|im_start|>user\n{userMessage}<|im_end|>\n<|im_start|>assistant\n";
        var sb = new System.Text.StringBuilder();

        await foreach (var token in _executor!.InferAsync(prompt, inferenceParams))
        {
            sb.Append(token);
        }

        var result = sb.ToString().Trim();

        foreach (var stop in inferenceParams.AntiPrompts)
        {
            var idx = result.IndexOf(stop, StringComparison.Ordinal);
            if (idx >= 0) result = result[..idx].Trim();
        }

        // Hard safety net: strip any emoji/symbol characters that slip through
        result = Regex.Replace(result, @"[\u2190-\u2BFF\uD83C-\uDBFF][\uDC00-\uDFFF]?", "").Trim();

        return result;
    }
}
