using System;
using System.Drawing;
using System.Drawing.Imaging;
using ImageBatchSystem.Models;

namespace ImageBatchSystem.Services
{
    public class DetectionService
    {
        public double BlurThreshold { get; set; }
        public double ColorBiasThreshold { get; set; }
        private readonly int _targetWidth;
        private readonly int _targetHeight;

        public DetectionService(int targetWidth, int targetHeight)
        {
            BlurThreshold = 100.0;
            ColorBiasThreshold = 30.0;
            _targetWidth = targetWidth;
            _targetHeight = targetHeight;
        }

        public DetectionResult Detect(string imagePath, int imageId)
        {
            using (var bmp = new Bitmap(imagePath))
            {
                // 每次检测创建独立结果对象，并绑定当前图片ID，便于写入检测结果表。
                var result = new DetectionResult();
                result.ImageId = imageId;
                result.DetectedAt = DateTime.Now;

                // 拉普拉斯方差越低，图像越可能模糊；分数达到阈值才通过。
                result.BlurScore = ComputeLaplacianVariance(bmp);
                result.BlurPassed = result.BlurScore >= BlurThreshold;

                // 目标尺寸为0表示该方向不限制，否则实际宽高必须达到目标值。
                result.ResolutionW = bmp.Width;
                result.ResolutionH = bmp.Height;
                result.ResPassed = (_targetWidth <= 0 || bmp.Width >= _targetWidth) &&
                                   (_targetHeight <= 0 || bmp.Height >= _targetHeight);

                double rBias, gBias, bBias;
                ComputeColorBias(bmp, out rBias, out gBias, out bBias);
                result.ColorBiasR = rBias;
                result.ColorBiasG = gBias;
                result.ColorBiasB = bBias;
                // 比较最大、最小通道偏差的跨度，判断颜色是否明显失衡。
                double maxBias = Math.Max(rBias, Math.Max(gBias, bBias));
                double minBias = Math.Min(rBias, Math.Min(gBias, bBias));
                result.ColorPassed = (maxBias - minBias) <= ColorBiasThreshold;

                // 三项检测全部通过才给出建议通过，最终决定仍由人工审核完成。
                result.SuggestPass = result.BlurPassed && result.ResPassed && result.ColorPassed;

                return result;
            }
        }

        private double ComputeLaplacianVariance(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            var rect = new Rectangle(0, 0, w, h);
            // LockBits一次性读取24位BGR像素，避免循环中频繁调用GetPixel。
            BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            int stride = data.Stride;
            byte[] pixels = new byte[stride * h];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            bmp.UnlockBits(data);

            double sum = 0, sumSq = 0;
            int count = 0;
            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    // 对 (x,y) 处应用拉普拉斯卷积核 [0,1,0; 1,-4,1; 0,1,0]
                    int top    = ((y - 1) * stride + x * 3) + 1;      // 上方像素绿色通道
                    int left   = (y * stride + (x - 1) * 3) + 1;      // 左方像素绿色通道
                    int center = (y * stride + x * 3) + 1;             // 中心像素绿色通道
                    int right   = (y * stride + (x + 1) * 3) + 1;     // 右方像素绿色通道
                    int bottom = ((y + 1) * stride + x * 3) + 1;      // 下方像素绿色通道
                    int lap = pixels[top] + pixels[left] + pixels[right] + pixels[bottom]
                            - 4 * pixels[center];
                    sum += lap;
                    sumSq += lap * lap;
                    count++;
                }
            }
            if (count == 0) return 0;
            double mean = sum / count;
            return sumSq / count - mean * mean;
        }

        private void ComputeColorBias(Bitmap bmp, out double rBias, out double gBias, out double bBias)
        {
            int w = bmp.Width, h = bmp.Height;
            var rect = new Rectangle(0, 0, w, h);
            // LockBits一次性读取24位BGR像素，避免循环中频繁调用GetPixel。
            BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            int stride = data.Stride;
            byte[] pixels = new byte[stride * h];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            bmp.UnlockBits(data);

            long rSum = 0, gSum = 0, bSum = 0;
            int count = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Format24bppRgb在内存中的通道顺序为B、G、R。
                    int p = y * stride + x * 3;
                    bSum += pixels[p];
                    gSum += pixels[p + 1];
                    rSum += pixels[p + 2];
                    count++;
                }
            }
            double rMean = (double)rSum / count;
            double gMean = (double)gSum / count;
            double bMean = (double)bSum / count;
            double channelMean = (rMean + gMean + bMean) / 3.0;

            // 使用各通道相对整体均值的偏差（像素值单位），检测颜色通道失衡。
            rBias = rMean - channelMean;
            gBias = gMean - channelMean;
            bBias = bMean - channelMean;
        }
    }
}
