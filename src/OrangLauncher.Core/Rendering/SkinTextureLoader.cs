using SkiaSharp;

namespace OrangLauncher.Rendering
{
    public static class SkinTextureLoader
    {
        public static (byte[] Rgba, int Width, int Height)? DecodePng(byte[] pngBytes)
        {
            var d = Decode(pngBytes, SKColorType.Rgba8888);
            return d == null ? null : (d.Value.Pixels, d.Value.Width, d.Value.Height);
        }

        public static (byte[] Bgra, int Width, int Height)? DecodeToBgra(byte[] imageBytes)
        {
            var d = Decode(imageBytes, SKColorType.Bgra8888);
            return d == null ? null : (d.Value.Pixels, d.Value.Width, d.Value.Height);
        }

        private static (byte[] Pixels, int Width, int Height)? Decode(byte[] bytes, SKColorType colorType)
        {
            try
            {
                using var bmp = SKBitmap.Decode(bytes);
                if (bmp == null) return null;
                var info = new SKImageInfo(bmp.Width, bmp.Height, colorType,
                    colorType == SKColorType.Bgra8888 ? SKAlphaType.Premul : SKAlphaType.Unpremul);
                var pixels = new byte[info.BytesSize];
                using var pixmap = bmp.PeekPixels();
                if (pixmap == null) return null;
                unsafe
                {
                    fixed (byte* p = pixels)
                    {
                        if (!pixmap.ReadPixels(info, (IntPtr)p, info.RowBytes)) return null;
                    }
                }
                return (pixels, bmp.Width, bmp.Height);
            }
            catch
            {
                return null;
            }
        }
    }
}
