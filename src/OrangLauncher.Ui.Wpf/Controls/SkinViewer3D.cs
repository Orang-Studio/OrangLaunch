using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OrangLauncher.Rendering;

namespace OrangLauncher.Controls
{
    /// <summary>
    /// Interactive, fully native 3D skin preview (drag to rotate).
    /// Rendering is done by Core's software rasterizer - no WebView, no network renders.
    /// </summary>
    public class SkinViewer3D : Image
    {
        private byte[]? _rgba;
        private int _texW, _texH;
        private byte[]? _capeRgba;
        private int _capeW, _capeH;
        private bool _slim;
        private double _yaw = 25, _pitch = 10;
        private Point _lastDrag;
        private bool _dragging;
        private WriteableBitmap? _wb;

        public SkinViewer3D()
        {
            Stretch = Stretch.Uniform;
            SnapsToDevicePixels = true;
            MouseLeftButtonDown += (s, e) => { _dragging = true; _lastDrag = e.GetPosition(this); CaptureMouse(); };
            MouseLeftButtonUp += (s, e) => { _dragging = false; ReleaseMouseCapture(); };
            MouseMove += OnDrag;
            SizeChanged += (s, e) => Render();
        }

        /// <summary>Loads a skin from raw PNG bytes and renders it.</summary>
        public bool LoadSkin(byte[] pngBytes, bool slimArms = false)
        {
            var decoded = SkinTextureLoader.DecodePng(pngBytes);
            if (decoded == null) return false;
            (_rgba, _texW, _texH) = decoded.Value;
            _slim = slimArms;
            Render();
            return true;
        }

        /// <summary>Loads a cape from raw PNG bytes; pass null to remove it.</summary>
        public bool LoadCape(byte[]? pngBytes)
        {
            if (pngBytes == null)
            {
                _capeRgba = null;
                Render();
                return true;
            }
            var decoded = SkinTextureLoader.DecodePng(pngBytes);
            if (decoded == null) return false;
            (_capeRgba, _capeW, _capeH) = decoded.Value;
            Render();
            return true;
        }

        public void Clear()
        {
            _rgba = null;
            _capeRgba = null;
            Source = null;
        }

        private void OnDrag(object sender, MouseEventArgs e)
        {
            if (!_dragging || _rgba == null) return;
            var p = e.GetPosition(this);
            _yaw += (p.X - _lastDrag.X) * 0.8;
            _pitch = Math.Clamp(_pitch + (p.Y - _lastDrag.Y) * 0.4, -60, 60);
            _lastDrag = p;
            Render();
        }

        private void Render()
        {
            if (_rgba == null) return;
            int w = Math.Max(64, (int)(ActualWidth > 0 ? ActualWidth : 240));
            int h = Math.Max(64, (int)(ActualHeight > 0 ? ActualHeight : 320));
            var result = SkinRenderer3D.RenderBody(_rgba, _texW, _texH, w, h, _yaw, _pitch, _slim,
                                                   capeRgba: _capeRgba, capeWidth: _capeW, capeHeight: _capeH);
            if (_wb == null || _wb.PixelWidth != w || _wb.PixelHeight != h)
            {
                _wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
                Source = _wb;
            }
            _wb.WritePixels(new Int32Rect(0, 0, w, h), result.Bgra, w * 4, 0);
        }
    }
}
