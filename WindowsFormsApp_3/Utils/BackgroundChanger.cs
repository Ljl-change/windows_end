using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace ImageBatchSystem.Utils
{
    public static class BackgroundChanger
    {
        private static void RgbToHsv(byte r, byte g, byte b,
            out double h, out double s, out double v)
        {
            double rd = r / 255.0, gd = g / 255.0, bd = b / 255.0;
            double max = rd > gd ? (rd > bd ? rd : bd) : (gd > bd ? gd : bd);
            double min = rd < gd ? (rd < bd ? rd : bd) : (gd < bd ? gd : bd);
            double delta = max - min;
            v = max;
            s = (max == 0) ? 0 : delta / max;
            if (delta == 0) { h = 0; return; }
            if (max == rd) h = 60 * (((gd - bd) / delta) % 6);
            else if (max == gd) h = 60 * (((bd - rd) / delta) + 2);
            else h = 60 * (((rd - gd) / delta) + 4);
            if (h < 0) h += 360;
        }

        private static double HsvDistance(
            double h1, double s1, double v1,
            double h2, double s2, double v2)
        {
            double dh = h1 - h2;
            if (dh < 0) dh = -dh;
            if (dh > 180) dh = 360 - dh;
            dh /= 180.0;
            double ds = s1 - s2;
            if (ds < 0) ds = -ds;
            double dv = v1 - v2;
            if (dv < 0) dv = -dv;
            return System.Math.Sqrt(2.0 * dh * dh + ds * ds + 0.3 * dv * dv);
        }

        public static Bitmap ChangeBackground(Bitmap src, Color newBg,
            double tLow = 0.12, double tHigh = 0.28)
        {
            int w = src.Width, h = src.Height;
            Bitmap dst = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            Rectangle rect = new Rectangle(0, 0, w, h);
            BitmapData sd = src.LockBits(rect, ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);
            BitmapData dd = dst.LockBits(rect, ImageLockMode.WriteOnly,
                PixelFormat.Format24bppRgb);
            int stride = sd.Stride;
            int msz = System.Math.Max(8, System.Math.Min(w, h) / 20);
            int rx = System.Math.Max(0, w - msz);
            int ry = System.Math.Max(0, h - msz);
            int[][] corners = {
                new[] { 0, 0 }, new[] { rx, 0 },
                new[] { 0, ry }, new[] { rx, ry }
            };
            unsafe
            {
                byte* sp = (byte*)sd.Scan0;
                byte* dp = (byte*)dd.Scan0;
                long sb = 0, sg = 0, sr = 0; int cnt = 0;
                foreach (int[] c in corners)
                    for (int y = c[1]; y < c[1] + msz && y < h; y++)
                        for (int x = c[0]; x < c[0] + msz && x < w; x++)
                        {
                            int p = y * stride + x * 3;
                            sb += sp[p]; sg += sp[p + 1]; sr += sp[p + 2]; cnt++;
                        }
                double bh, bs, bv;
                RgbToHsv((byte)(sr / cnt), (byte)(sg / cnt), (byte)(sb / cnt),
                         out bh, out bs, out bv);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int p = y * stride + x * 3;
                        double ph, ps, pv;
                        RgbToHsv(sp[p + 2], sp[p + 1], sp[p],
                            out ph, out ps, out pv);
                        double dist = HsvDistance(ph, ps, pv, bh, bs, bv);
                        double a;
                        if (dist <= tLow) a = 0;
                        else if (dist >= tHigh) a = 1;
                        else a = (dist - tLow) / (tHigh - tLow);
                        dp[p] = (byte)(sp[p] * a + newBg.B * (1 - a));
                        dp[p + 1] = (byte)(sp[p + 1] * a + newBg.G * (1 - a));
                        dp[p + 2] = (byte)(sp[p + 2] * a + newBg.R * (1 - a));
                    }
            }
            src.UnlockBits(sd);
            dst.UnlockBits(dd);
            return dst;
        }
    }
}
