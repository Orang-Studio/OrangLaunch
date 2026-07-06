using System.Numerics;

namespace OrangLauncher.Rendering
{
    /// <summary>Raw BGRA pixel output of the skin renderer; UIs wrap it in their own bitmap type.</summary>
    public sealed class SkinRenderResult
    {
        public int Width { get; init; }
        public int Height { get; init; }
        /// <summary>Premultiplied BGRA, Width*Height*4 bytes, row-major.</summary>
        public byte[] Bgra { get; init; } = Array.Empty<byte>();
    }

    /// <summary>
    /// Native software renderer for Minecraft player skins: textured boxes,
    /// z-buffered triangle rasterization, nearest-neighbour sampling.
    /// No UI framework or GPU involvement, so it works identically under WPF and WinUI
    /// (a hardware backend can be added behind the same API later).
    /// </summary>
    public static class SkinRenderer3D
    {
        // ---- skin texture access -------------------------------------------------

        private sealed class SkinTexture
        {
            public int Width, Height;
            public byte[] Rgba = Array.Empty<byte>();
            public bool Legacy => Height == 32;

            public uint Sample(int u, int v)
            {
                if ((uint)u >= Width || (uint)v >= Height) return 0;
                int i = (v * Width + u) * 4;
                return (uint)(Rgba[i] | Rgba[i + 1] << 8 | Rgba[i + 2] << 16 | Rgba[i + 3] << 24);
            }
        }

        // ---- model definition ----------------------------------------------------

        private readonly record struct Face(Vector3 A, Vector3 B, Vector3 C, Vector3 D,
                                            int U, int V, int Uw, int Vh, bool MirrorU, Vector3 Normal);

        private static void AddBox(List<Face> faces, Vector3 center, float w, float h, float d,
                                   int texU, int texV, int boxW, int boxH, int boxD,
                                   bool mirror, float inflate)
        {
            float hw = w / 2 + inflate, hh = h / 2 + inflate, hd = d / 2 + inflate;
            var c = center;
            // corners: x right, y up, z toward viewer
            Vector3 p000 = c + new Vector3(-hw, -hh, -hd), p001 = c + new Vector3(-hw, -hh, hd);
            Vector3 p010 = c + new Vector3(-hw, hh, -hd), p011 = c + new Vector3(-hw, hh, hd);
            Vector3 p100 = c + new Vector3(hw, -hh, -hd), p101 = c + new Vector3(hw, -hh, hd);
            Vector3 p110 = c + new Vector3(hw, hh, -hd), p111 = c + new Vector3(hw, hh, hd);
            int u = texU, v = texV, bw = boxW, bh = boxH, bd = boxD;
            // Standard Minecraft box UV layout.
            // front (+z)
            faces.Add(new Face(p011, p111, p101, p001, u + bd, v + bd, bw, bh, mirror, new Vector3(0, 0, 1)));
            // back (-z)
            faces.Add(new Face(p110, p010, p000, p100, u + bd + bw + bd, v + bd, bw, bh, mirror, new Vector3(0, 0, -1)));
            // left (+x, viewer's left of model's right side)
            faces.Add(new Face(p111, p110, p100, p101, u + bd + bw, v + bd, bd, bh, mirror, new Vector3(1, 0, 0)));
            // right (-x)
            faces.Add(new Face(p010, p011, p001, p000, u, v + bd, bd, bh, mirror, new Vector3(-1, 0, 0)));
            // top (+y)
            faces.Add(new Face(p010, p110, p111, p011, u + bd, v, bw, bd, mirror, new Vector3(0, 1, 0)));
            // bottom (-y)
            faces.Add(new Face(p001, p101, p100, p000, u + bd + bw, v, bw, bd, mirror, new Vector3(0, -1, 0)));
        }

        private static List<Face> BuildModel(bool slim, bool overlay, bool legacy)
        {
            var f = new List<Face>(72);
            int armW = slim ? 3 : 4;
            float armXOff = slim ? 5.5f : 6f;

            // Base layer. Units: 1 = one skin pixel. Model stands centered at origin.
            AddBox(f, new Vector3(0, 10, 0), 8, 8, 8, 0, 0, 8, 8, 8, false, 0);           // head
            AddBox(f, new Vector3(0, 0, 0), 8, 12, 4, 16, 16, 8, 12, 4, false, 0);        // body
            AddBox(f, new Vector3(-armXOff, 0, 0), armW, 12, 4, 40, 16, armW, 12, 4, false, 0); // right arm
            if (legacy)
                AddBox(f, new Vector3(armXOff, 0, 0), armW, 12, 4, 40, 16, armW, 12, 4, true, 0); // left arm (mirrored)
            else
                AddBox(f, new Vector3(armXOff, 0, 0), armW, 12, 4, 32, 48, armW, 12, 4, false, 0);
            AddBox(f, new Vector3(-2, -12, 0), 4, 12, 4, 0, 16, 4, 12, 4, false, 0);      // right leg
            if (legacy)
                AddBox(f, new Vector3(2, -12, 0), 4, 12, 4, 0, 16, 4, 12, 4, true, 0);    // left leg (mirrored)
            else
                AddBox(f, new Vector3(2, -12, 0), 4, 12, 4, 16, 48, 4, 12, 4, false, 0);

            if (overlay)
            {
                AddBox(f, new Vector3(0, 10, 0), 8, 8, 8, 32, 0, 8, 8, 8, false, 0.5f);   // hat (always present)
                if (!legacy)
                {
                    AddBox(f, new Vector3(0, 0, 0), 8, 12, 4, 16, 32, 8, 12, 4, false, 0.25f);            // jacket
                    AddBox(f, new Vector3(-armXOff, 0, 0), armW, 12, 4, 40, 32, armW, 12, 4, false, 0.25f); // right sleeve
                    AddBox(f, new Vector3(armXOff, 0, 0), armW, 12, 4, 48, 48, armW, 12, 4, false, 0.25f);  // left sleeve
                    AddBox(f, new Vector3(-2, -12, 0), 4, 12, 4, 0, 32, 4, 12, 4, false, 0.25f);            // right leg overlay
                    AddBox(f, new Vector3(2, -12, 0), 4, 12, 4, 0, 48, 4, 12, 4, false, 0.25f);             // left leg overlay
                }
            }
            return f;
        }

        private static List<Face> BuildCape()
        {
            // Standard 64x32 cape texture: one 10x16x1 box at UV origin. The cape
            // hangs from the shoulder line, fully behind the body/jacket layer
            // (body back is z=-2, jacket -2.25), with its bottom tilted away.
            var raw = new List<Face>(6);
            AddBox(raw, Vector3.Zero, 10, 16, 1, 0, 0, 10, 16, 1, false, 0);
            // The box's front (+z) face carries the cape's outer texture, but a worn
            // cape shows its outer side backwards (away from the body). Spin the box
            // 180° around Y so the outer texture faces out when viewed from behind.
            var tilt = Matrix4x4.CreateRotationY((float)Math.PI) * Matrix4x4.CreateRotationX(10f * (float)Math.PI / 180f);
            var boxTop = new Vector3(0, 8, 0);          // top edge of the untransformed box
            var anchor = new Vector3(0, 6, -3.0f);      // shoulder height, behind the jacket
            var faces = new List<Face>(6);
            foreach (var f in raw)
            {
                Vector3 T(Vector3 p) => Vector3.Transform(p - boxTop, tilt) + anchor;
                faces.Add(new Face(T(f.A), T(f.B), T(f.C), T(f.D), f.U, f.V, f.Uw, f.Vh, f.MirrorU,
                                   Vector3.TransformNormal(f.Normal, tilt)));
            }
            return faces;
        }

        // ---- public API ------------------------------------------------------------

        /// <summary>
        /// Renders a full-body skin view. Yaw/pitch in degrees (0/0 = facing the viewer).
        /// skinRgba must be raw RGBA pixels of the 64x64 (or legacy 64x32) skin.
        /// capeRgba, when given, must be a standard 64x32 cape texture; the cape is
        /// drawn on the model's back.
        /// </summary>
        public static SkinRenderResult RenderBody(byte[] skinRgba, int skinWidth, int skinHeight,
                                                  int width, int height,
                                                  double yawDegrees = 25, double pitchDegrees = 10,
                                                  bool slimArms = false, bool overlay = true,
                                                  byte[]? capeRgba = null, int capeWidth = 0, int capeHeight = 0)
        {
            var tex = new SkinTexture { Width = skinWidth, Height = skinHeight, Rgba = skinRgba };
            var faces = BuildModel(slimArms, overlay, tex.Legacy);

            float yaw = (float)(yawDegrees * Math.PI / 180.0);
            float pitch = (float)(pitchDegrees * Math.PI / 180.0);
            var rot = Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateRotationX(pitch);

            // Model is ~32 units tall (head top ~ +18, feet ~ -18 incl. overlay margin).
            float scale = Math.Min(width, height) / 42f * (height > width ? (float)height / width : 1f);
            scale = height / 40f;
            float cx = width / 2f, cy = height / 2f;

            var zbuf = new float[width * height];
            for (int i = 0; i < zbuf.Length; i++) zbuf[i] = float.NegativeInfinity;
            var pix = new byte[width * height * 4];

            var lightDir = Vector3.Normalize(new Vector3(-0.3f, 0.9f, 0.6f));

            void RenderFaces(List<Face> list, SkinTexture texture)
            {
                Span<Vector3> proj = stackalloc Vector3[4];
                Span<Vector3> corners = stackalloc Vector3[4];
                foreach (var face in list)
                {
                    // backface culling in view space
                    var n = Vector3.TransformNormal(face.Normal, rot);
                    if (n.Z <= 0.02f) continue;
                    float shade = 0.62f + 0.38f * Math.Max(0, Vector3.Dot(n, lightDir));

                    corners[0] = face.A; corners[1] = face.B; corners[2] = face.C; corners[3] = face.D;
                    for (int i = 0; i < 4; i++)
                    {
                        var p = Vector3.Transform(corners[i] - new Vector3(0, 2, 0), rot);
                        // gentle perspective
                        float persp = 140f / (140f - p.Z);
                        proj[i] = new Vector3(cx + p.X * scale * persp, cy - p.Y * scale * persp, p.Z);
                    }
                    RasterizeQuad(pix, zbuf, width, height, proj, face, texture, shade);
                }
            }

            RenderFaces(faces, tex);
            if (capeRgba != null && capeWidth > 0 && capeHeight > 0)
            {
                var capeTex = new SkinTexture { Width = capeWidth, Height = capeHeight, Rgba = capeRgba };
                RenderFaces(BuildCape(), capeTex);
            }
            return new SkinRenderResult { Width = width, Height = height, Bgra = pix };
        }

        private static void RasterizeQuad(byte[] pix, float[] zbuf, int width, int height,
                                          Span<Vector3> p, in Face face, SkinTexture tex, float shade)
        {
            // Two triangles: (0,1,2) and (0,2,3), with per-corner UVs of the quad:
            // A=(0,0) B=(1,0) C=(1,1) D=(0,1) in face-local UV space.
            Span<Vector2> uv = stackalloc Vector2[] { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };
            RasterizeTri(pix, zbuf, width, height, p[0], p[1], p[2], uv[0], uv[1], uv[2], face, tex, shade);
            RasterizeTri(pix, zbuf, width, height, p[0], p[2], p[3], uv[0], uv[2], uv[3], face, tex, shade);
        }

        private static void RasterizeTri(byte[] pix, float[] zbuf, int width, int height,
                                         Vector3 a, Vector3 b, Vector3 c,
                                         Vector2 ta, Vector2 tb, Vector2 tc,
                                         in Face face, SkinTexture tex, float shade)
        {
            int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))));
            int maxX = Math.Min(width - 1, (int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))));
            int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))));
            int maxY = Math.Min(height - 1, (int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))));
            if (minX > maxX || minY > maxY) return;

            float denom = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
            if (MathF.Abs(denom) < 1e-6f) return;
            float inv = 1f / denom;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float w0 = ((b.Y - c.Y) * (px - c.X) + (c.X - b.X) * (py - c.Y)) * inv;
                    float w1 = ((c.Y - a.Y) * (px - c.X) + (a.X - c.X) * (py - c.Y)) * inv;
                    float w2 = 1f - w0 - w1;
                    if (w0 < 0 || w1 < 0 || w2 < 0) continue;

                    float z = w0 * a.Z + w1 * b.Z + w2 * c.Z;
                    int zi = y * width + x;
                    if (z <= zbuf[zi]) continue;

                    float fu = w0 * ta.X + w1 * tb.X + w2 * tc.X;
                    float fv = w0 * ta.Y + w1 * tb.Y + w2 * tc.Y;
                    if (face.MirrorU) fu = 1f - fu;
                    int u = face.U + Math.Min(face.Uw - 1, (int)(fu * face.Uw));
                    int v = face.V + Math.Min(face.Vh - 1, (int)(fv * face.Vh));

                    uint rgba = tex.Sample(u, v);
                    byte alpha = (byte)(rgba >> 24);
                    if (alpha < 8) continue; // transparent texel (overlay holes)

                    zbuf[zi] = z;
                    int pi = zi * 4;
                    byte r = (byte)(rgba & 0xFF), g = (byte)(rgba >> 8 & 0xFF), bl = (byte)(rgba >> 16 & 0xFF);
                    pix[pi + 0] = (byte)(bl * shade); // B
                    pix[pi + 1] = (byte)(g * shade);  // G
                    pix[pi + 2] = (byte)(r * shade);  // R
                    pix[pi + 3] = 255;
                }
            }
        }
    }
}
