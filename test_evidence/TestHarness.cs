using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using ImageBatchSystem.Models;
using ImageBatchSystem.Repositories;
using ImageBatchSystem.Services;
using ImageBatchSystem.Forms;

internal static class TestHarness
{
    private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly string DbPath = Path.Combine(BaseDir, "AppData", "database.db");
    private static readonly string InputsDir = Path.Combine(BaseDir, "test_inputs");

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DbPath));
            DbContext.Initialize(DbPath);
            string command = args.Length == 0 ? "inspect" : args[0].ToLowerInvariant();

            switch (command)
            {
                case "setup": Setup(); break;
                case "process": ProcessOrder(args[1]); break;
                case "approve": ApproveOrder(args[1]); break;
                case "reject": RejectOrder(args[1]); break;
                case "export": ExportOrder(args[1]); break;
                case "persist": Persist(args[1]); break;
                case "delete": DeleteOrder(args[1]); break;
                case "thumbnails": GenerateThumbnails(args[1]); break;
                case "reprocesscheck": CheckReprocessList(args[1]); break;
                case "inspect": Inspect(); break;
                default: throw new ArgumentException("Unknown command: " + command);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR|" + ex.GetType().Name + "|" + ex.Message);
            return 1;
        }
    }

    private static void Setup()
    {
        Directory.CreateDirectory(InputsDir);
        GenerateImages();

        CreateOrder(
            "TDD_正常流程_001",
            "{\"ChangeBg\":true,\"BgColor\":\"#FFFFFF\",\"Resize\":true,\"TargetWidth\":600,\"TargetHeight\":800,\"ConvertFormat\":true,\"TargetFormat\":\"PNG\"}",
            600, 800, "PNG",
            "clear_portrait.jpg", "blur_image.jpg", "color_bias.png");

        CreateOrder(
            "TDD_驳回流程_002",
            "{\"ChangeBg\":false,\"Resize\":false,\"ConvertFormat\":false}",
            0, 0, "JPEG",
            "clear_portrait.jpg");

        CreateOrder(
            "TDD_持久化_003",
            "{\"ChangeBg\":false,\"Resize\":false,\"ConvertFormat\":false}",
            0, 0, "BMP",
            "normal_scene.bmp");

        Console.WriteLine("PASS|SETUP|orders=3|database=" + DbPath);
        Inspect();
    }

    private static void CreateOrder(string title, string options, int width, int height, string format, params string[] files)
    {
        if (WorkOrderRepo.GetAll().Any(x => x.Title == title))
            throw new InvalidOperationException("Duplicate test order: " + title);

        int id = WorkOrderService.CreateDraft(new WorkOrder
        {
            Title = title,
            ProcessOptions = options,
            TargetWidth = width,
            TargetHeight = height,
            TargetFormat = format
        });

        string originalDir = BatchProcessService.GetOriginalDir(id);
        Directory.CreateDirectory(originalDir);
        foreach (string file in files)
        {
            string source = Path.Combine(InputsDir, file);
            string destination = Path.Combine(originalDir, file);
            File.Copy(source, destination, true);
            ImageItemRepo.Insert(new ImageItem
            {
                WorkOrderId = id,
                FileName = file,
                OriginalPath = destination
            });
        }

        WorkOrderService.Submit(id);
        Console.WriteLine("PASS|CREATE|id={0}|title={1}|images={2}", id, title, files.Length);
    }

    private static void ProcessOrder(string title)
    {
        WorkOrder order = FindOrder(title);
        WorkOrderService.StartProcessing(order.Id);
        var progress = new List<string>();
        new BatchProcessService(WorkOrderRepo.GetById(order.Id)).Execute(
            (current, total) => progress.Add(current + "/" + total));
        WorkOrderService.CompleteProcessing(order.Id);

        List<ImageItem> images = ImageItemRepo.GetByWorkOrderId(order.Id);
        int done = images.Count(x => x.ProcessStatus == "Done");
        int detected = DetectionRepo.GetByWorkOrderId(order.Id).Count;
        if (done != images.Count || detected != images.Count)
            throw new InvalidOperationException("Processing incomplete: done=" + done + ", detected=" + detected);

        Console.WriteLine("PASS|PROCESS|title={0}|done={1}|detected={2}|progress={3}",
            title, done, detected, string.Join(",", progress));
        foreach (ImageItem image in images)
        {
            using (var bitmap = new Bitmap(image.ProcessedPath))
                Console.WriteLine("OUTPUT|{0}|{1}x{2}|{3}", Path.GetFileName(image.ProcessedPath), bitmap.Width, bitmap.Height, Path.GetExtension(image.ProcessedPath));
            DetectionResult result = DetectionRepo.GetByImageId(image.Id);
            Console.WriteLine("DETECT|{0}|blur={1:F2}|blurPass={2}|resPass={3}|colorPass={4}|suggest={5}",
                image.FileName, result.BlurScore, result.BlurPassed, result.ResPassed, result.ColorPassed, result.SuggestPass);
        }
    }

    private static void ApproveOrder(string title)
    {
        WorkOrder order = FindOrder(title);
        List<ImageItem> images = ImageItemRepo.GetByWorkOrderId(order.Id);
        foreach (ImageItem image in images)
        {
            ImageItemRepo.UpdateReview(image.Id, "Approved", null);
            ProcessLogRepo.Insert(new ProcessLog
            {
                WorkOrderId = order.Id,
                ImageId = image.Id,
                Action = "测试：图片审核通过",
                Operator = "Codex测试",
                CreatedAt = DateTime.Now
            });
        }
        WorkOrderService.Approve(order.Id);
        Console.WriteLine("PASS|APPROVE|title={0}|images={1}|status={2}",
            title, images.Count, WorkOrderRepo.GetById(order.Id).Status);
    }

    private static void RejectOrder(string title)
    {
        WorkOrder order = FindOrder(title);
        ImageItem image = ImageItemRepo.GetByWorkOrderId(order.Id).First();
        ImageItemRepo.UpdateReview(image.Id, "Rejected", "背景边缘处理不完整");
        WorkOrderService.Reject(order.Id, "背景边缘处理不完整", "Codex测试");
        image = ImageItemRepo.GetByWorkOrderId(order.Id).First();
        int detectionCount = DetectionRepo.GetByWorkOrderId(order.Id).Count;
        Console.WriteLine("PASS|REJECT|title={0}|status={1}|imageProcess={2}|imageReview={3}|detections={4}",
            title, WorkOrderRepo.GetById(order.Id).Status, image.ProcessStatus, image.ReviewStatus, detectionCount);
    }

    private static void ExportOrder(string title)
    {
        WorkOrder order = FindOrder(title);
        string zipPath = ExportService.Export(order.Id);
        string[] entries;
        using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            entries = archive.Entries.Select(x => x.FullName).OrderBy(x => x).ToArray();

        string report = entries.FirstOrDefault(x => x == "detection_report.csv");
        string log = entries.FirstOrDefault(x => x == "process_log.txt");
        if (report == null || log == null || !entries.Any(x => x.StartsWith("original/") || x.StartsWith("original\\")) || !entries.Any(x => x.StartsWith("processed/") || x.StartsWith("processed\\")))
            throw new InvalidOperationException("ZIP structure incomplete");

        WorkOrderService.Archive(order.Id);
        Console.WriteLine("PASS|EXPORT|title={0}|status={1}|zip={2}|entries={3}",
            title, WorkOrderRepo.GetById(order.Id).Status, zipPath, entries.Length);
        foreach (string entry in entries) Console.WriteLine("ZIP|" + entry);
    }

    private static void CheckReprocessList(string title)
    {
        WorkOrder order = FindOrder(title);
        using (var form = new MainForm())
        {
            var refresh = typeof(MainForm).GetMethod(
                "RefreshAll",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            refresh.Invoke(form, null);
            var field = typeof(MainForm).GetField(
                "_cmbProcessOrder",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var combo = (System.Windows.Forms.ComboBox)field.GetValue(form);
            bool listed = combo.Items.Cast<object>().OfType<WorkOrder>().Any(x => x.Id == order.Id);
            if (!listed) throw new InvalidOperationException("Rejected order missing from process combo");
            Console.WriteLine("PASS|REPROCESS_LIST|title={0}|status={1}|listed={2}", title, order.Status, listed);
        }
    }
    private static void GenerateThumbnails(string title)
    {
        WorkOrder order = FindOrder(title);
        using (var form = new MainForm())
        {
            var method = typeof(MainForm).GetMethod(
                "GenerateThumbnails",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method.Invoke(form, new object[] { order.Id });
        }
        string directory = BatchProcessService.GetThumbnailDir(order.Id);
        int count = Directory.Exists(directory) ? Directory.GetFiles(directory).Length : 0;
        if (count != ImageItemRepo.GetByWorkOrderId(order.Id).Count * 2)
            throw new InvalidOperationException("Thumbnail count mismatch: " + count);
        Console.WriteLine("PASS|THUMBNAILS|title={0}|count={1}", title, count);
    }
    private static void Persist(string title)
    {
        DbContext.Initialize(DbPath);
        WorkOrder order = FindOrder(title);
        Console.WriteLine("PASS|PERSIST|id={0}|title={1}|status={2}", order.Id, order.Title, order.Status);
    }

    private static void DeleteOrder(string title)
    {
        WorkOrder order = FindOrder(title);
        WorkOrderService.Delete(order.Id);
        bool exists = WorkOrderRepo.GetById(order.Id) != null;
        Console.WriteLine("PASS|DELETE|id={0}|title={1}|exists={2}", order.Id, title, exists);
    }

    private static WorkOrder FindOrder(string title)
    {
        WorkOrder order = WorkOrderRepo.GetAll().FirstOrDefault(x => x.Title == title);
        if (order == null) throw new InvalidOperationException("Order not found: " + title);
        return order;
    }

    private static void Inspect()
    {
        foreach (WorkOrder order in WorkOrderRepo.GetAll().OrderBy(x => x.Id))
        {
            List<ImageItem> images = ImageItemRepo.GetByWorkOrderId(order.Id);
            Console.WriteLine("ORDER|id={0}|title={1}|status={2}|images={3}|approved={4}|pending={5}",
                order.Id, order.Title, order.Status, images.Count,
                images.Count(x => x.ReviewStatus == "Approved"),
                images.Count(x => x.ReviewStatus == "Pending"));
        }
    }

    private static void GenerateImages()
    {
        using (var bitmap = new Bitmap(1200, 1600, PixelFormat.Format24bppRgb))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.Clear(Color.FromArgb(67, 142, 219));
            using (var body = new SolidBrush(Color.FromArgb(42, 55, 72)))
                graphics.FillRectangle(body, 360, 980, 480, 520);
            using (var face = new SolidBrush(Color.FromArgb(239, 195, 156)))
                graphics.FillEllipse(face, 390, 260, 420, 560);
            using (var hair = new SolidBrush(Color.FromArgb(45, 35, 30)))
                graphics.FillPie(hair, 370, 210, 460, 420, 180, 180);
            using (var pen = new Pen(Color.White, 18))
                graphics.DrawRectangle(pen, 180, 120, 840, 1360);
            bitmap.Save(Path.Combine(InputsDir, "clear_portrait.jpg"), ImageFormat.Jpeg);
        }

        using (var bitmap = new Bitmap(400, 300, PixelFormat.Format24bppRgb))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (var brush = new LinearGradientBrush(new Rectangle(0, 0, 400, 300), Color.Gray, Color.LightGray, 20f))
        {
            graphics.FillRectangle(brush, 0, 0, 400, 300);
            bitmap.Save(Path.Combine(InputsDir, "blur_image.jpg"), ImageFormat.Jpeg);
        }

        using (var bitmap = new Bitmap(800, 800, PixelFormat.Format24bppRgb))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.FromArgb(220, 50, 45));
            using (var brush = new SolidBrush(Color.FromArgb(250, 170, 80)))
                graphics.FillEllipse(brush, 160, 160, 480, 480);
            using (var pen = new Pen(Color.FromArgb(90, 10, 10), 24))
                graphics.DrawLine(pen, 80, 720, 720, 80);
            bitmap.Save(Path.Combine(InputsDir, "color_bias.png"), ImageFormat.Png);
        }

        using (var bitmap = new Bitmap(1024, 768, PixelFormat.Format24bppRgb))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 16; x++)
                    graphics.FillRectangle(((x + y) % 2 == 0) ? Brushes.White : Brushes.DarkSlateGray, x * 64, y * 64, 64, 64);
            bitmap.Save(Path.Combine(InputsDir, "normal_scene.bmp"), ImageFormat.Bmp);
        }
    }
}
