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
