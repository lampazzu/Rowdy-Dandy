using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;

public static class IconKit
{
    public const int N = 24;
    public static string Root = "";
    static readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>();

    public static Bitmap Load(string rel)
    {
        Bitmap b;
        if (cache.TryGetValue(rel, out b)) return b;
        string path = Path.Combine(Root, rel);
        if (rel.EndsWith(".aseprite")) b = Aseprite(path);
        else { using (var tmp = new Bitmap(path)) b = new Bitmap(tmp); }
        cache[rel] = b;
        return b;
    }

    // frame k of a horizontal sheet (frames = count), trimmed to its drawn pixels
    public static Bitmap Frame(string rel, int frames, int k, int rows = 1, int row = 0)
    {
        Bitmap src = Load(rel);
        int fw = src.Width / Math.Max(1, frames), fh = src.Height / Math.Max(1, rows);
        return Trim(Sub(src, k * fw, row * fh, fw, fh));
    }

    public static Bitmap Sub(Bitmap src, int x, int y, int w, int h)
    {
        var r = new Bitmap(Math.Max(1, w), Math.Max(1, h), PixelFormat.Format32bppArgb);
        for (int yy = 0; yy < h; yy++)
            for (int xx = 0; xx < w; xx++)
            {
                int sx = x + xx, sy = y + yy;
                if (sx < 0 || sy < 0 || sx >= src.Width || sy >= src.Height) continue;
                r.SetPixel(xx, yy, src.GetPixel(sx, sy));
            }
        return r;
    }

    public static Bitmap Trim(Bitmap b)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
                if (b.GetPixel(x, y).A > 30) { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
        if (maxX < 0) return new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        return Sub(b, minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    // a 32 x 32 window of a (trimmed) image: centred on (ax, ay) as fractions of it; smaller images are centred whole
    public static Bitmap Window(Bitmap b, double ax = 0.5, double ay = 0.5)
    {
        int cx = b.Width <= N ? b.Width / 2 : (int)Math.Round(ax * b.Width);
        int cy = b.Height <= N ? b.Height / 2 : (int)Math.Round(ay * b.Height);
        int x0 = b.Width <= N ? (b.Width - N) / 2 : Math.Max(0, Math.Min(b.Width - N, cx - N / 2));
        int y0 = b.Height <= N ? (b.Height - N) / 2 : Math.Max(0, Math.Min(b.Height - N, cy - N / 2));
        return Sub(b, x0, y0, N, N);
    }

    public static Bitmap Canvas() { return new Bitmap(N, N, PixelFormat.Format32bppArgb); }

    // draws 'top' onto 'dst' with its top-left at (x, y), normal alpha over
    public static void Paste(Bitmap dst, Bitmap top, int x, int y)
    {
        for (int yy = 0; yy < top.Height; yy++)
            for (int xx = 0; xx < top.Width; xx++)
            {
                int dx = x + xx, dy = y + yy;
                if (dx < 0 || dy < 0 || dx >= dst.Width || dy >= dst.Height) continue;
                Color s = top.GetPixel(xx, yy);
                if (s.A == 0) continue;
                Color d = dst.GetPixel(dx, dy);
                float a = s.A / 255f, ia = 1f - a;
                float outA = a + d.A / 255f * ia;
                if (outA <= 0f) continue;
                int r = (int)((s.R * a + d.R * d.A / 255f * ia) / outA);
                int g = (int)((s.G * a + d.G * d.A / 255f * ia) / outA);
                int bl = (int)((s.B * a + d.B * d.A / 255f * ia) / outA);
                dst.SetPixel(dx, dy, Color.FromArgb((int)(outA * 255f), Clamp(r), Clamp(g), Clamp(bl)));
            }
    }

    // pasted centred around (cx, cy) on the canvas
    public static void PasteCentered(Bitmap dst, Bitmap top, int cx, int cy) { Paste(dst, top, cx - top.Width / 2, cy - top.Height / 2); }

    public static Bitmap FlipX(Bitmap b) { var r = new Bitmap(b); r.RotateFlip(RotateFlipType.RotateNoneFlipX); return r; }

    // a 1 px dark outline around everything drawn (so icons from soft VFX read on the medallion)
    public static Bitmap Outline(Bitmap b, Color ink)
    {
        var r = new Bitmap(b.Width, b.Height, PixelFormat.Format32bppArgb);
        for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
            {
                if (b.GetPixel(x, y).A > 30) continue;
                bool edge = false;
                for (int k = 0; k < 4 && !edge; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nx >= 0 && ny >= 0 && nx < b.Width && ny < b.Height && b.GetPixel(nx, ny).A > 30) edge = true;
                }
                if (edge) r.SetPixel(x, y, ink);
            }
        Paste(r, b, 0, 0);
        return r;
    }

    static int Clamp(int v) { return v < 0 ? 0 : v > 255 ? 255 : v; }

    public static void Save(Bitmap b, string path) { Directory.CreateDirectory(Path.GetDirectoryName(path)); b.Save(path, ImageFormat.Png); }

    // contact sheet: icons x4 on a grey medallion-ish background, with ids
    public static void Sheet(List<KeyValuePair<string, Bitmap>> icons, string path, int cols = 10)
    {
        int cell = N * 4 + 8, rows = (icons.Count + cols - 1) / cols;
        using (var sheet = new Bitmap(cols * cell, rows * (cell + 14), PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(255, 70, 62, 80));
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            var font = new Font("Consolas", 8f);
            for (int i = 0; i < icons.Count; i++)
            {
                int x = (i % cols) * cell + 4, y = (i / cols) * (cell + 14) + 4;
                g.FillRectangle(new SolidBrush(Color.FromArgb(255, 40, 34, 48)), x, y, N * 4, N * 4);
                g.DrawImage(icons[i].Value, new Rectangle(x, y, N * 4, N * 4), 0, 0, N, N, GraphicsUnit.Pixel);
                g.DrawString(icons[i].Key, font, Brushes.White, x, y + N * 4);
            }
            sheet.Save(path, ImageFormat.Png);
        }
    }

    // every frame of each sheet ("path|frames|max"), 2x, one row per sheet, frame numbers in yellow
    public static void Strip(string outPath, string[] specs)
    {
        const int scale = 2;
        int maxW = 0, totalH = 0;
        var rows = new List<Tuple<string, Bitmap, int, int>>();
        foreach (string s in specs)
        {
            string[] p = s.Split('|');
            Bitmap src = Load(p[0]);
            int n = int.Parse(p[1]), max = Math.Min(n, int.Parse(p[2]));
            rows.Add(Tuple.Create(p[0], src, n, max));
            maxW = Math.Max(maxW, src.Width / n * max * scale);
            totalH += src.Height * scale + 14;
        }
        using (var bmp = new Bitmap(Math.Min(maxW, 8000), totalH, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(255, 70, 62, 80));
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            var font = new Font("Consolas", 8f);
            int y = 0;
            foreach (var r in rows)
            {
                int fw = r.Item2.Width / r.Item3, fh = r.Item2.Height;
                for (int k = 0; k < r.Item4; k++)
                {
                    g.DrawImage(r.Item2, new Rectangle(k * fw * scale, y, fw * scale, fh * scale), k * fw, 0, fw, fh, GraphicsUnit.Pixel);
                    g.DrawRectangle(Pens.DimGray, k * fw * scale, y, fw * scale - 1, fh * scale - 1);
                    g.DrawString(k.ToString(), font, Brushes.Yellow, k * fw * scale, y);
                }
                g.DrawString(Path.GetFileName(r.Item1), font, Brushes.White, 0, y + fh * scale);
                y += fh * scale + 14;
            }
            bmp.Save(outPath, ImageFormat.Png);
        }
    }

    // frames ("path|frames|k") of a sheet, untrimmed, at 'scale', with a grid line every 8 px (labels every 16)
    public static void Zoom(string outPath, string[] specs, int scale)
    {
        var frames = new List<Tuple<string, Bitmap>>();
        int w = 0, h = 0;
        foreach (string s in specs)
        {
            string[] p = s.Split('|');
            Bitmap src = Load(p[0]);
            int n = int.Parse(p[1]), k = int.Parse(p[2]), fw = src.Width / n;
            Bitmap f = Sub(src, k * fw, 0, fw, src.Height);
            frames.Add(Tuple.Create(Path.GetFileNameWithoutExtension(p[0]) + "#" + k, f));
            w += f.Width * scale + 30; h = Math.Max(h, f.Height * scale + 30);
        }
        using (var bmp = new Bitmap(Math.Min(w, 9000), h, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(255, 70, 62, 80));
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            var font = new Font("Consolas", 7f);
            var pen = new Pen(Color.FromArgb(90, 255, 255, 255));
            int x = 0;
            foreach (var f in frames)
            {
                g.DrawImage(f.Item2, new Rectangle(x, 14, f.Item2.Width * scale, f.Item2.Height * scale), 0, 0, f.Item2.Width, f.Item2.Height, GraphicsUnit.Pixel);
                for (int gx = 0; gx <= f.Item2.Width; gx += 8) { g.DrawLine(pen, x + gx * scale, 14, x + gx * scale, 14 + f.Item2.Height * scale); if (gx % 16 == 0) g.DrawString(gx.ToString(), font, Brushes.Yellow, x + gx * scale, 2); }
                for (int gy = 0; gy <= f.Item2.Height; gy += 8) { g.DrawLine(pen, x, 14 + gy * scale, x + f.Item2.Width * scale, 14 + gy * scale); if (gy % 16 == 0) g.DrawString(gy.ToString(), font, Brushes.Cyan, x, 14 + gy * scale); }
                g.DrawString(f.Item1, font, Brushes.White, x + 20, 2);
                x += f.Item2.Width * scale + 30;
            }
            bmp.Save(outPath, ImageFormat.Png);
        }
    }

    // a 24 x 24 crop of frame k with its centre at pixel (cx, cy) of the frame
    public static Bitmap At(string rel, int frames, int k, int cx, int cy)
    {
        Bitmap src = Load(rel);
        int fw = src.Width / Math.Max(1, frames);
        return Sub(src, k * fw + cx - N / 2, cy - N / 2, N, N);
    }

    // cut to a disc (the boon medallions are round): everything outside radius r from the middle goes, and the
    // pixels on the rim are darkened a little so a full-bleed crop gets a soft edge instead of a jagged one
    public static Bitmap Circle(Bitmap b, double r)
    {
        var o = new Bitmap(b.Width, b.Height, PixelFormat.Format32bppArgb);
        double cx = (b.Width - 1) / 2.0, cy = (b.Height - 1) / 2.0;
        for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
            {
                double d = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d > r) continue;
                Color c = b.GetPixel(x, y);
                if (d > r - 1 && c.A > 0) c = Color.FromArgb(c.A, c.R * 3 / 4, c.G * 3 / 4, c.B * 3 / 4);
                o.SetPixel(x, y, c);
            }
        return o;
    }

    // ---------------------------------------------------------------- minimal .aseprite reader (frame 0, visible layers, RGBA)
    public static Bitmap Aseprite(string path)
    {
        byte[] d = File.ReadAllBytes(path);
        int frames = BitConverter.ToUInt16(d, 6), w = BitConverter.ToUInt16(d, 8), h = BitConverter.ToUInt16(d, 10), depth = BitConverter.ToUInt16(d, 12);
        int transparentIndex = d[28];
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var layerVisible = new List<bool>();
        Color[] palette = new Color[256];
        int p = 128;
        for (int f = 0; f < frames && f < 1; f++)
        {
            int frameBytes = BitConverter.ToInt32(d, p);
            int oldChunks = BitConverter.ToUInt16(d, p + 6);
            int newChunks = BitConverter.ToInt32(d, p + 12);
            int chunks = newChunks != 0 ? newChunks : oldChunks;
            int c = p + 16;
            var cels = new List<Tuple<int, Bitmap, int, int, int>>();
            for (int k = 0; k < chunks; k++)
            {
                int size = BitConverter.ToInt32(d, c), type = BitConverter.ToUInt16(d, c + 4);
                int data = c + 6;
                if (type == 0x2004) { int flags = BitConverter.ToUInt16(d, data); layerVisible.Add((flags & 1) != 0); }
                else if (type == 0x2019)
                {
                    int count = BitConverter.ToInt32(d, data), first = BitConverter.ToInt32(d, data + 4);
                    int q = data + 20;
                    for (int i = first; i < first + count && i < 256; i++)
                    {
                        int pf = BitConverter.ToUInt16(d, q);
                        palette[i] = Color.FromArgb(d[q + 5], d[q + 2], d[q + 3], d[q + 4]);
                        q += 6;
                        if ((pf & 1) != 0) { int len = BitConverter.ToUInt16(d, q); q += 2 + len; }
                    }
                }
                else if (type == 0x2005)
                {
                    int layer = BitConverter.ToUInt16(d, data), x = BitConverter.ToInt16(d, data + 2), y = BitConverter.ToInt16(d, data + 4);
                    int opacity = d[data + 6], celType = BitConverter.ToUInt16(d, data + 7);
                    int q = data + 16;
                    if (celType == 0 || celType == 2)
                    {
                        int cw = BitConverter.ToUInt16(d, q), ch = BitConverter.ToUInt16(d, q + 2);
                        q += 4;
                        byte[] px;
                        int bpp = depth / 8;
                        if (celType == 0) { px = new byte[cw * ch * bpp]; Array.Copy(d, q, px, 0, px.Length); }
                        else
                        {
                            using (var ms = new MemoryStream(d, q + 2, size - (q - c) - 2))
                            using (var z = new DeflateStream(ms, CompressionMode.Decompress))
                            using (var outMs = new MemoryStream()) { z.CopyTo(outMs); px = outMs.ToArray(); }
                        }
                        var cel = new Bitmap(cw, ch, PixelFormat.Format32bppArgb);
                        for (int yy = 0; yy < ch; yy++)
                            for (int xx = 0; xx < cw; xx++)
                            {
                                int i = (yy * cw + xx) * bpp;
                                if (i + bpp > px.Length) continue;
                                Color col;
                                if (bpp == 4) col = Color.FromArgb(px[i + 3], px[i], px[i + 1], px[i + 2]);
                                else if (bpp == 2) col = Color.FromArgb(px[i + 1], px[i], px[i], px[i]);
                                else col = px[i] == transparentIndex ? Color.Transparent : palette[px[i]];
                                if (opacity < 255) col = Color.FromArgb(col.A * opacity / 255, col);
                                cel.SetPixel(xx, yy, col);
                            }
                        cels.Add(Tuple.Create(layer, cel, x, y, opacity));
                    }
                }
                c += size;
            }
            cels.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            foreach (var cel in cels)
                if (cel.Item1 >= layerVisible.Count || layerVisible[cel.Item1]) Paste(bmp, cel.Item2, cel.Item3, cel.Item4);
            p += frameBytes;
        }
        return bmp;
    }
}
