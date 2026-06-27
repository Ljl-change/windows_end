using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using ImageBatchSystem.Models;
using ImageBatchSystem.Repositories;
using ImageBatchSystem.Services;

internal static class SeedTenDebugCases
{
    private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly string DbPath = Path.Combine(BaseDir, "AppData", "database.db");
    private static readonly string InputsDir = Path.Combine(BaseDir, "TestData");
    private const string Prefix = "课程测试_TC-";

    private static int Main()
    {
        try
        {
            DbContext.Initialize(DbPath);
            RemovePreviousSeedData();
            EnsureInputs();

            CreateSubmitted("课程测试_TC-01_程序启动与界面导航", NormalOptions(),
                0, 0, "JPEG", "clear_portrait.jpg");

            CreateDraft("课程测试_TC-02_创建工单输入校验");

            CreateSubmitted("课程测试_TC-03_创建正常流程工单", TransformOptions(),
                600, 800, "PNG", "clear_portrait.jpg", "blur_image.jpg", "color_bias.png");

            int tc04 = CreateSubmitted("课程测试_TC-04_批量处理与进度显示", TransformOptions(),
                600, 800, "PNG", "clear_portrait.jpg", "blur_image.jpg", "color_bias.png");
            Process(tc04);

            int tc05 = CreateSubmitted("课程测试_TC-05_换底尺寸与格式输出", TransformOptions(),
                600, 800, "PNG", "clear_portrait.jpg", "blur_image.jpg", "color_bias.png");
            Process(tc05);

            int tc06 = CreateSubmitted("课程测试_TC-06_质量检测结果显示", NormalOptions(),
                0, 0, "JPEG", "clear_portrait.jpg", "blur_image.jpg", "color_bias.png");
            Process(tc06);

            int tc07 = CreateSubmitted("课程测试_TC-07_审核通过进入导出列表", NormalOptions(),
                0, 0, "JPEG", "clear_portrait.jpg");
            Process(tc07);
            Approve(tc07);

            int tc08 = CreateSubmitted("课程测试_TC-08_驳回原因与重新处理", NormalOptions(),
                0, 0, "JPEG", "clear_portrait.jpg");
            Process(tc08);
            Reject(tc08);

            int tc09 = CreateSubmitted("课程测试_TC-09_ZIP导出与归档", TransformOptions(),
                600, 800, "PNG", "clear_portrait.jpg", "blur_image.jpg", "color_bias.png");
            Process(tc09);
            Approve(tc09);
            ExportAndArchive(tc09);

            int tc10 = CreateSubmitted("课程测试_TC-10_持久化与删除回归", NormalOptions(),
                0, 0, "BMP", "normal_scene.bmp");
            VerifyDeleteAndRecord(tc10);

            VerifyTenCases();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("SEED_ERROR|" + ex);
            return 1;
        }
    }

    private static string NormalOptions()
    {
        return "{\"ChangeBg\":false,\"Resize\":false,\"ConvertFormat\":false}";
    }

    private static string TransformOptions()
    {
        return "{\"ChangeBg\":true,\"BgColor\":\"#FFFFFF\",\"Resize\":true," +
               "\"TargetWidth\":600,\"TargetHeight\":800,\"ConvertFormat\":true," +
               "\"TargetFormat\":\"PNG\"}";
    }

    private static void RemovePreviousSeedData()
    {
        foreach (WorkOrder order in WorkOrderRepo.GetAll()
            .Where(x => x.Title != null && x.Title.StartsWith(Prefix))
            .ToList())
        {
            WorkOrderService.Delete(order.Id);
        }
    }

    private static void EnsureInputs()
    {
        string[] required =
        {
            "clear_portrait.jpg", "blur_image.jpg", "color_bias.png", "normal_scene.bmp"
        };
        foreach (string file in required)
        {
            if (!File.Exists(Path.Combine(InputsDir, file)))
                throw new FileNotFoundException("缺少测试图片", Path.Combine(InputsDir, file));
        }
    }

    private static int CreateDraft(string title)
    {
        int id = WorkOrderService.CreateDraft(new WorkOrder
        {
            Title = title,
            ProcessOptions = NormalOptions(),
            TargetWidth = 0,
            TargetHeight = 0,
            TargetFormat = "JPEG"
        });
        Console.WriteLine("SEEDED|TC-02|id={0}|status=Draft|images=0", id);
        return id;
    }

    private static int CreateSubmitted(
        string title, string options, int width, int height, string format, params string[] files)
    {
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
        Console.WriteLine("SEEDED|{0}|id={1}|status=Submitted|images={2}",
            title.Substring(Prefix.Length, 5), id, files.Length);
        return id;
    }

    private static void Process(int id)
    {
        WorkOrderService.StartProcessing(id);
        new BatchProcessService(WorkOrderRepo.GetById(id)).Execute();
        WorkOrderService.CompleteProcessing(id);
    }

    private static void Approve(int id)
    {
        foreach (ImageItem image in ImageItemRepo.GetByWorkOrderId(id))
            ImageItemRepo.UpdateReview(image.Id, "Approved", null);
        WorkOrderService.Approve(id);
    }

    private static void Reject(int id)
    {
        ImageItem image = ImageItemRepo.GetByWorkOrderId(id).First();
        ImageItemRepo.UpdateReview(image.Id, "Rejected", "背景边缘处理不完整");
        WorkOrderService.Reject(id, "背景边缘处理不完整", "课程测试");
    }

    private static void ExportAndArchive(int id)
    {
        string zip = ExportService.Export(id);
        using (ZipArchive archive = ZipFile.OpenRead(zip))
        {
            if (archive.Entries.Count != 8)
                throw new InvalidOperationException("TC-09 ZIP条目数量不是8");
        }
        WorkOrderService.Archive(id);
    }

    private static void VerifyDeleteAndRecord(int evidenceOrderId)
    {
        int temporaryId = CreateSubmitted(
            "课程测试_TC-10_临时删除对象", NormalOptions(), 0, 0, "JPEG", "clear_portrait.jpg");
        WorkOrderService.Delete(temporaryId);
        if (WorkOrderRepo.GetById(temporaryId) != null)
            throw new InvalidOperationException("TC-10临时工单删除失败");

        ProcessLogRepo.Insert(new ProcessLog
        {
            WorkOrderId = evidenceOrderId,
            Action = "TC-10临时工单删除回归通过，已删除ID=" + temporaryId,
            Operator = "课程测试",
            CreatedAt = DateTime.Now
        });
    }

    private static void VerifyTenCases()
    {
        List<WorkOrder> cases = WorkOrderRepo.GetAll()
            .Where(x => x.Title != null && x.Title.StartsWith(Prefix))
            .OrderBy(x => x.Title)
            .ToList();
        if (cases.Count != 10)
            throw new InvalidOperationException("独立测试工单数量错误：" + cases.Count);

        foreach (WorkOrder order in cases)
        {
            int images = ImageItemRepo.GetByWorkOrderId(order.Id).Count;
            int detections = DetectionRepo.GetByWorkOrderId(order.Id).Count;
            Console.WriteLine("CASE|id={0}|title={1}|status={2}|images={3}|detections={4}",
                order.Id, order.Title, order.Status, images, detections);
        }
        Console.WriteLine("SEED_OK|database={0}|cases=10", DbPath);
    }
}
