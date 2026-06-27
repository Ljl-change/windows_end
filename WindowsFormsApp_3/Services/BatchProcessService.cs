using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using ImageBatchSystem.Repositories;
using ImageBatchSystem.Utils;

namespace ImageBatchSystem.Services
{
    public class BatchProcessService
    {
        private readonly Models.WorkOrder _workOrder;

        public BatchProcessService(Models.WorkOrder workOrder)
        {
            _workOrder = workOrder;
        }

        public void Execute(Action<int, int> progressCallback = null)
        {
            // 一次读取当前工单的全部图片，并为处理结果建立独立输出目录。
            var images = ImageItemRepo.GetByWorkOrderId(_workOrder.Id);
            string processedDir = GetProcessedDir(_workOrder.Id);
            Directory.CreateDirectory(processedDir);

            // 工单级参数只解析一次；质量检测器复用同一组目标宽高。
            var opts = ParseOptions();
            var detectionService = new DetectionService(_workOrder.TargetWidth, _workOrder.TargetHeight);

            for (int i = 0; i < images.Count; i++)
            {
                var img = images[i];
                // 回调只传递进度数据，界面层决定如何更新进度条和提示文字。
                if (progressCallback != null)
                    progressCallback(i + 1, images.Count);

                try
                {
                    // 先标记为Processing，使数据库能够反映当前处理进度。
                    ImageItemRepo.UpdateProcessStatus(img.Id, "Processing");

                    using (var bmp = new Bitmap(img.OriginalPath))
                    {
                        // current指向当前处理阶段的位图；产生新位图后释放旧中间对象。
                        Bitmap current = bmp;

                        // 步骤 1: 换底色
                        if (opts.ChangeBg)
                        {
                            var bgColor = ColorTranslator.FromHtml(opts.BgColor);
                            var changed = BackgroundChanger.ChangeBackground(current, bgColor);
                            if (current != bmp) current.Dispose();
                            current = changed;
                        }

                        // 步骤 2: 统一尺寸
                        if (opts.Resize && (opts.TargetWidth > 0 || opts.TargetHeight > 0))
                        {
                            int tw = opts.TargetWidth > 0 ? opts.TargetWidth : current.Width;
                            int th = opts.TargetHeight > 0 ? opts.TargetHeight : current.Height;
                            var resized = new Bitmap(current, new Size(tw, th));
                            if (current != bmp) current.Dispose();
                            current = resized;
                        }

                        // 步骤 3: 格式转换 + 保存
                        string ext;
                        if (opts.ConvertFormat)
                            ext = opts.TargetFormat.ToLower();
                        else
                            ext = Path.GetExtension(img.OriginalPath).TrimStart('.').ToLower();
                        if (ext == "jpg" || ext == "jpeg") ext = "jpg";
                        string outName = Path.GetFileNameWithoutExtension(img.FileName) + "." + ext;
                        string outPath = Path.Combine(processedDir, outName);

                        current.Save(outPath, GetImageFormat(ext));

                        // 保存成功后回写真实输出路径，审核和导出均读取该字段。
                        ImageItemRepo.UpdateProcessedPath(img.Id, outPath);

                        if (current != bmp) current.Dispose();

                        // 步骤 4: 质量检测
                        var result = detectionService.Detect(outPath, img.Id);
                        DetectionRepo.Insert(result);
                    }

                    // 图像处理、保存和检测全部完成后，才将图片标记为Done。
                    ImageItemRepo.UpdateProcessStatus(img.Id, "Done");
                }
                catch (Exception ex)
                {
                    // 单张图片失败时记录失败状态和原因，避免异常静默丢失。
                    ImageItemRepo.UpdateProcessStatus(img.Id, "Failed");
                    ProcessLogRepo.Insert(new Models.ProcessLog
                    {
                        WorkOrderId = _workOrder.Id,
                        ImageId = img.Id,
                        Action = string.Format("处理失败: {0}", ex.Message),
                        CreatedAt = DateTime.Now
                    });
                }
            }
        }

        public static string GetOriginalDir(int workOrderId)
        {
            return Path.Combine(GetWorkOrderBaseDir(workOrderId), "original");
        }

        public static string GetProcessedDir(int workOrderId)
        {
            return Path.Combine(GetWorkOrderBaseDir(workOrderId), "processed");
        }

        public static string GetThumbnailDir(int workOrderId)
        {
            return Path.Combine(GetWorkOrderBaseDir(workOrderId), "thumbnails");
        }

        public static string GetWorkOrderBaseDir(int workOrderId)
        {
            string appData = Path.Combine(
                System.Windows.Forms.Application.StartupPath, "AppData", "WorkOrders");
            return Path.Combine(appData, workOrderId.ToString());
        }

        private ProcessOptions ParseOptions()
        {
            var opts = new ProcessOptions();
            if (string.IsNullOrWhiteSpace(_workOrder.ProcessOptions))
                return opts;

            try
            {
                var json = _workOrder.ProcessOptions;
                opts.ChangeBg = GetJsonBool(json, "ChangeBg");
                opts.BgColor = GetJsonString(json, "BgColor") ?? "#438EDB";
                opts.Resize = GetJsonBool(json, "Resize");
                opts.TargetWidth = GetJsonInt(json, "TargetWidth");
                opts.TargetHeight = GetJsonInt(json, "TargetHeight");
                opts.ConvertFormat = GetJsonBool(json, "ConvertFormat");
                opts.TargetFormat = GetJsonString(json, "TargetFormat") ?? "JPEG";
            }
            catch { }
            return opts;
        }

        private class ProcessOptions
        {
            public bool ChangeBg;
            public string BgColor;
            public bool Resize;
            public int TargetWidth;
            public int TargetHeight;
            public bool ConvertFormat;
            public string TargetFormat;

            public ProcessOptions()
            {
                BgColor = "#438EDB";
                TargetFormat = "JPEG";
            }
        }

        private ImageFormat GetImageFormat(string ext)
        {
            switch (ext.ToLower())
            {
                case "png": return ImageFormat.Png;
                case "bmp": return ImageFormat.Bmp;
                default: return ImageFormat.Jpeg;
            }
        }

        private static bool GetJsonBool(string json, string key)
        {
            string search = "\"" + key + "\"";
            int idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;
            int colon = json.IndexOf(':', idx);
            if (colon < 0) return false;
            string rest = json.Substring(colon + 1).Trim();
            return rest.StartsWith("true", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetJsonString(string json, string key)
        {
            string search = "\"" + key + "\"";
            int idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx);
            if (colon < 0) return null;
            string rest = json.Substring(colon + 1).Trim();
            if (!rest.StartsWith("\"")) return null;
            int endQuote = rest.IndexOf('"', 1);
            if (endQuote < 0) return null;
            return rest.Substring(1, endQuote - 1);
        }

        private static int GetJsonInt(string json, string key)
        {
            string search = "\"" + key + "\"";
            int idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return 0;
            int colon = json.IndexOf(':', idx);
            if (colon < 0) return 0;
            string rest = json.Substring(colon + 1).Trim();
            int end = 0;
            while (end < rest.Length && (char.IsDigit(rest[end]) || rest[end] == '-'))
                end++;
            int v; if (end > 0 && int.TryParse(rest.Substring(0, end), out v)) return v;
            return 0;
        }
    }
}
