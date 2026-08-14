using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using OrangLauncher.Rendering;

namespace OrangLauncher.Controls
{
    public class SkinViewer3D : UserControl
    {
        private readonly Image _image = new() { Stretch = Stretch.Uniform };
        private byte[]? _rgba;
        private int _texW, _texH;
        private byte[]? _capeRgba;
        private int _capeW, _capeH;
        private bool _slim;
        private double _yaw = 25, _pitch = 10;
        private Windows.Foundation.Point _lastDrag;
        private bool _dragging;
        private WriteableBitmap? _wb;

        public SkinViewer3D()
        {
            Content = _image;
            // transparent background so the whole control area is hit testable for drag.
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            PointerPressed += (s, e) =>
            {
                _dragging = true;
                _lastDrag = e.GetCurrentPoint(this).Position;
                CapturePointer(e.Pointer);
            };
            PointerReleased += (s, e) => { _dragging = false; ReleasePointerCapture(e.Pointer); };
            PointerMoved += OnDrag;
            SizeChanged += (s, e) => Render();
        }

        public bool LoadSkin(byte[] pngBytes, bool slimArms = false)
        {
            var decoded = SkinTextureLoader.DecodePng(pngBytes);
            if (decoded == null) return false;
            (_rgba, _texW, _texH) = decoded.Value;
            _slim = slimArms;
            Render();
            return true;
        }

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
            _image.Source = null;
        }

        private void OnDrag(object sender, PointerRoutedEventArgs e)
        {
            if (!_dragging || _rgba == null) return;
            var p = e.GetCurrentPoint(this).Position;
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
                _wb = new WriteableBitmap(w, h);
                _image.Source = _wb;
            }
            using (var stream = _wb.PixelBuffer.AsStream())
            {
                stream.Write(result.Bgra, 0, result.Bgra.Length);
            }
            _wb.Invalidate();
        }
    }
}
