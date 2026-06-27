using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using ImageBatchSystem.Repositories;

namespace ImageBatchSystem.Services
{
    public static class ExportService
    {
        public static string Export(int workOrderId)
        {
            var wo = WorkOrderRepo.GetById(workOrderId);
            if (wo == null)
                throw new ArgumentException(string.Format("工单 {0} 不存在", workOrderId));

            string baseDir = BatchProcessService.GetWorkOrderBaseDir(workOrderId);
            string archiveDir = Path.Combine(baseDir, "archive");
            string originalDir = BatchProcessService.GetOriginalDir(workOrderId);
            string processedDir = BatchProcessService.GetProcessedDir(workOrderId);

            if (Directory.Exists(archiveDir)) Directory.Delete(archiveDir, true);
            Directory.CreateDirectory(archiveDir);

            if (Directory.Exists(originalDir))
                CopyDirectory(originalDir, Path.Combine(archiveDir, "original"));

            if (Directory.Exists(processedDir))
                CopyDirectory(processedDir, Path.Combine(archiveDir, "processed"));

            GenerateCsvReport(workOrderId, Path.Combine(archiveDir, "detection_report.csv"));
            GenerateProcessLog(workOrderId, Path.Combine(archiveDir, "process_log.txt"));

            string zipName = string.Format("WorkOrder_{0}.zip", workOrderId);
            string zipPath = Path.Combine(baseDir, zipName);
            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(archiveDir, zipPath, CompressionLevel.Optimal, false);

            Directory.Delete(archiveDir, true);
            return zipPath;
        }

        private static void GenerateCsvReport(int workOrderId, string outputPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("图片ID,文件名,模糊度分数,模糊度合格,分辨率宽,分辨率高,分辨率合格,R偏色,G偏色,B偏色,偏色合格,系统建议");

            var images = ImageItemRepo.GetByWorkOrderId(workOrderId);
            foreach (var img in images)
            {
                var dr = DetectionRepo.GetByImageId(img.Id);
                if (dr == null) continue;
                sb.AppendLine(string.Format("{0},{1},{2:F2},{3},{4},{5},{6},{7:F3},{8:F3},{9:F3},{10},{11}",
                    img.Id, img.FileName, dr.BlurScore, dr.BlurPassed,
                    dr.ResolutionW, dr.ResolutionH, dr.ResPassed,
                    dr.ColorBiasR, dr.ColorBiasG, dr.ColorBiasB, dr.ColorPassed, dr.SuggestPass));
            }
            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        }

        private static void GenerateProcessLog(int workOrderId, string outputPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("操作时间,操作描述,操作人,驳回原因,关联图片ID");

            var logs = ProcessLogRepo.GetByWorkOrderId(workOrderId);
            foreach (var log in logs)
            {
                string imgIdStr = log.ImageId.HasValue ? log.ImageId.Value.ToString() : "";
                sb.AppendLine(string.Format("{0:yyyy-MM-dd HH:mm:ss},{1},{2},{3},{4}",
                    log.CreatedAt, log.Action,
                    log.Operator ?? "", log.RejectReason ?? "", imgIdStr));
            }
            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        }

        private static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src))
            {
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            }
        }
    }
}
