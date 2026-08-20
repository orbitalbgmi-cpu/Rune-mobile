using System.Runtime.InteropServices;

namespace RuneMobile.Services;

public class ImageService
{
    private const string SdLib = "libstable-diffusion";

    private enum SdType { SD_TYPE_F32 = 0, SD_TYPE_F16 = 1 }
    private enum RngType { STD_DEFAULT_RNG = 0 }
    private enum ScheduleType { DEFAULT = 0 }
    private enum SampleMethod { EULER_A = 0 }

    [StructLayout(LayoutKind.Sequential)]
    private struct SdImage
    {
        public uint width;
        public uint height;
        public uint channel;
        public IntPtr data;
    }

    [DllImport(SdLib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr new_sd_ctx(
        string model_path, string vae_path, string taesd_path,
        string control_net_path, string lora_model_dir, string embed_dir,
        string stacked_id_embed_dir,
        [MarshalAs(UnmanagedType.I1)] bool vae_decode_only,
        [MarshalAs(UnmanagedType.I1)] bool vae_tiling,
        [MarshalAs(UnmanagedType.I1)] bool free_params_immediately,
        int n_threads, SdType wtype, RngType rng_type, ScheduleType schedule,
        [MarshalAs(UnmanagedType.I1)] bool keep_clip_on_cpu,
        [MarshalAs(UnmanagedType.I1)] bool keep_control_net_on_cpu,
        [MarshalAs(UnmanagedType.I1)] bool keep_vae_on_cpu);

    [DllImport(SdLib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr txt2img(
        IntPtr sd_ctx, string prompt, string negative_prompt,
        int clip_skip, float cfg_scale, float guidance,
        int width, int height, SampleMethod sample_method, int sample_steps,
        long seed, int batch_count,
        IntPtr control_cond, float control_strength, float style_strength,
        [MarshalAs(UnmanagedType.I1)] bool normalize_input,
        string input_id_images_path);

    [DllImport(SdLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void free_sd_ctx(IntPtr sd_ctx);

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
                4, SdType.SD_TYPE_F16, RngType.STD_DEFAULT_RNG, ScheduleType.DEFAULT,
                false, false, false);

            if (ctx == IntPtr.Zero)
                throw new Exception("Failed to load image model (new_sd_ctx returned null)");

            try
            {
                var resultPtr = txt2img(
                    ctx, prompt, "",
                    -1, 7.0f, 3.5f,
                    256, 256, SampleMethod.EULER_A, 12,
                    -1, 1,
                    IntPtr.Zero, 0.9f, 0.0f,
                    false, "");

                if (resultPtr == IntPtr.Zero)
                    throw new Exception("txt2img returned null");

                var img = Marshal.PtrToStructure<SdImage>(resultPtr);
                var byteCount = (int)(img.width * img.height * img.channel);
                var pixelData = new byte[byteCount];
                Marshal.Copy(img.data, pixelData, 0, byteCount);

                return EncodePng(pixelData, (int)img.width, (int)img.height, (int)img.channel);
            }
            finally
            {
                free_sd_ctx(ctx);
            }
        });
    }

    private static byte[] EncodePng(byte[] rawPixels, int width, int height, int channels)
    {
        using var bitmap = new SkiaSharp.SKBitmap(width, height, SkiaSharp.SKColorType.Rgb888x, SkiaSharp.SKAlphaType.Opaque);
        var ptr = bitmap.GetPixels();
        for (int i = 0; i < width * height; i++)
        {
            var srcIdx = i * channels;
            var r = rawPixels[srcIdx];
            var g = channels > 1 ? rawPixels[srcIdx + 1] : r;
            var b = channels > 2 ? rawPixels[srcIdx + 2] : r;
            Marshal.WriteByte(ptr, i * 4, r);
            Marshal.WriteByte(ptr, i * 4 + 1, g);
            Marshal.WriteByte(ptr, i * 4 + 2, b);
            Marshal.WriteByte(ptr, i * 4 + 3, 255);
        }
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
