using System.Runtime.InteropServices;

namespace RuneMobile.Services;

public class ImageService
{
    private const string SdLib = "libstable-diffusion";

    [DllImport(SdLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr new_sd_ctx(
        string modelPath, string vaePath, string taesdPath, string controlNetPath,
        string loraDir, string embedDir, string idEmbedDir,
        bool vaeDecodeOnly, bool vaeTiling, bool freeParamsImmediately,
        int nThreads, int wtype, int rngType, int schedule,
        bool keepClipOnCpu, bool keepControlNetOnCpu, bool keepVaeOnCpu);

    [DllImport(SdLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr txt2img(
        IntPtr sdCtx, string prompt, string negativePrompt,
        int clipSkip, float cfgScale, float guidance,
        int width, int height, int sampleMethod, int sampleSteps,
        long seed, int batchCount);

    public string ModelDir => Path.Combine(
        Android.App.Application.Context.GetExternalFilesDir(null)!.AbsolutePath,
        "models");

    public string ModelPath => Path.Combine(ModelDir, "sd-model.gguf");

    public bool ModelExists => File.Exists(ModelPath);

    public Task<byte[]> GenerateAsync(string prompt)
    {
        return Task.Run(() =>
        {
            var ctx = new_sd_ctx(
                ModelPath, "", "", "", "", "", "",
                true, false, true,
                4, 2, 0, 0,
                false, false, false);

            if (ctx == IntPtr.Zero)
                throw new Exception("Failed to load image model");

            var result = txt2img(
                ctx, prompt, "",
                -1, 7.0f, 3.5f,
                512, 512, 0, 20,
                -1, 1);

            if (result == IntPtr.Zero)
                throw new Exception("Image generation failed");

            // Result marshaling (raw pixel buffer -> PNG bytes) handled in next step
            return Array.Empty<byte>();
        });
    }
}
