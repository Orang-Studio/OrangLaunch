using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OrangLauncher.Managers;
using OrangLauncher.Models;
using static OrangLauncher.UiKit;
namespace OrangLauncher.Pages
{
    public static class InstanceIcons
    {
        private static List<(string Jar, string Entry)>? _textures;
        public static string? LoaderIconPath(string? loader)
        {
            var l = string.IsNullOrWhiteSpace(loader) ? "vanilla" : loader.Trim().ToLowerInvariant();
            if (l is "vanilla" or "none") return FindAsset("images/minecraft-green.png");
            return FindAsset($"images/loaders/{l}.png") ?? FindAsset("images/minecraft-blue.png");
        }
        public static string? PathFor(MinecraftInstance instance)
        {
            if (!string.IsNullOrEmpty(instance.Icon) && File.Exists(instance.Icon)) return instance.Icon;
            foreach (var cand in new[] { "icon.png", "pack.png" })
            {
                var p = Path.Combine(instance.BasePath, cand);
                if (File.Exists(p)) return p;
            }
            return LoaderIconPath(instance.ModLoader);
        }
        public static ImageSource? For(MinecraftInstance instance, int size = 64) => LoadImage(PathFor(instance), size * 2);
        private static List<(string, string)> Textures()
        {
            if (_textures != null) return _textures;
            _textures = new();
            try
            {
                var versions = Path.Combine(PlatformPaths.GetMinecraftDir(), "versions");
                if (!Directory.Exists(versions)) return _textures;
                foreach (var jar in Directory.GetFiles(versions, "*.jar", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc))
                {
                    try
                    {
                        using var zip = ZipFile.OpenRead(jar);
                        var names = zip.Entries.Select(e => e.FullName)
                            .Where(n => n.StartsWith("assets/minecraft/textures/item/") && n.EndsWith(".png") &&
                                        !new[] { "empty_", "_overlay", "barrier", "structure_void", "debug" }.Any(n.Contains))
                            .ToList();
                        if (names.Count == 0) continue;
                        _textures.AddRange(names.Select(n => (jar, n)));
                        break;
                    }
                    catch { }
                }
            }
            catch { }
            return _textures;
        }
        private static BitmapSource? RandomSprite(Random rng)
        {
            var sources = Textures();
            for (int i = 0; i < (sources.Count > 0 ? 12 : 0); i++)
            {
                var (jar, entry) = sources[rng.Next(sources.Count)];
                try
                {
                    using var zip = ZipFile.OpenRead(jar);
                    using var s = zip.GetEntry(entry)!.Open();
                    using var ms = new MemoryStream();
                    s.CopyTo(ms);
                    var img = ImageFromBytes(ms.ToArray());
                    if (img != null && img.PixelWidth == img.PixelHeight) return img;
                }
                catch { }
            }
            return null;
        }
        private static BitmapSource Identicon(Random rng, Color color)
        {
            var pixels = new byte[8 * 8 * 4];
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 4; x++)
                {
                    if (rng.NextDouble() >= 0.5) continue;
                    foreach (var px in new[] { x, 7 - x })
                    {
                        var i = (y * 8 + px) * 4;
                        pixels[i] = color.B; pixels[i + 1] = color.G; pixels[i + 2] = color.R; pixels[i + 3] = 255;
                    }
                }
            return BitmapSource.Create(8, 8, 96, 96, PixelFormats.Bgra32, null, pixels, 32);
        }
        private static Color Hsv(double h, double s, double v)
        {
            h = (h % 1 + 1) % 1 * 6;
            int i = (int)Math.Floor(h);
            double f = h - i, p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
            var (r, g, b) = i switch { 0 => (v, t, p), 1 => (q, v, p), 2 => (p, v, t), 3 => (p, q, v), 4 => (t, p, v), _ => (v, p, q) };
            return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }
        private static BitmapSource NearestScale(BitmapSource source, int size, Color? fill)
        {
            var src = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int sw = src.PixelWidth, sh = src.PixelHeight;
            var input = new byte[sw * sh * 4];
            src.CopyPixels(input, sw * 4, 0);
            var output = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
            {
                int sy = y * sh / size;
                for (int x = 0; x < size; x++)
                {
                    int sx = x * sw / size;
                    int si = (sy * sw + sx) * 4, di = (y * size + x) * 4;
                    if (fill is Color c)
                    {
                        if (input[si + 3] == 0) continue;
                        output[di] = c.B; output[di + 1] = c.G; output[di + 2] = c.R; output[di + 3] = c.A;
                    }
                    else
                    {
                        output[di] = input[si]; output[di + 1] = input[si + 1]; output[di + 2] = input[si + 2]; output[di + 3] = input[si + 3];
                    }
                }
            }
            var bmp = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, output, size * 4);
            bmp.Freeze();
            return bmp;
        }
        public static string Generate()
        {
            var rng = new Random();
            const int size = 128;
            var hue = rng.NextDouble();
            BitmapSource sprite = RandomSprite(rng) ?? Identicon(rng, Hsv(hue, 0.15, 1.0));
            var big = NearestScale(sprite, 96, null);
            var shadow = NearestScale(sprite, 92, Color.FromArgb(90, 0, 0, 0));
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var grad = new LinearGradientBrush(Hsv(hue, 0.35 + rng.NextDouble() * 0.25, 0.95),
                    Hsv(hue + 0.06 + rng.NextDouble() * 0.16, 0.6 + rng.NextDouble() * 0.25, 0.5), new Point(0, 0), new Point(1, 1));
                dc.DrawRectangle(grad, null, new Rect(0, 0, size, size));
                dc.DrawImage(shadow, new Rect(20, 22, 92, 92));
                dc.DrawImage(big, new Rect(16, 16, 96, 96));
            }
            var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            var path = Path.Combine(Path.GetTempPath(), $"oranglauncher-icon-{Guid.NewGuid():N}.png");
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = File.Create(path);
            enc.Save(fs);
            return path;
}   }   }