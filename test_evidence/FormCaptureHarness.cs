using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ImageBatchSystem.Forms;
using ImageBatchSystem.Models;
using ImageBatchSystem.Repositories;

internal static class FormCaptureHarness
{
    private const uint WM_CLOSE = 0x0010;

    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr state);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr state);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll")] private static extern int GetClassName(IntPtr handle, StringBuilder value, int max);
    [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr handle, StringBuilder value, int max);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle, out Rect rect);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr handle, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppData", "database.db");
            DbContext.Initialize(dbPath);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string action = args[0].ToLowerInvariant();
            if (action == "capture")
                Capture(args[1], args[2]);
            else if (action == "validate")
                Validate(args[1]);
            else if (action == "approveui")
                ApproveThroughUi(args[1]);
            else if (action == "rejectui")
                RejectThroughUi(args[1]);
            else if (action == "exportui")
                ExportThroughUi(args[1]);
            else
                throw new ArgumentException("Unknown action: " + action);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR|" + ex.GetType().Name + "|" + ex.Message);
            return 1;
        }
    }

    private static void Capture(string mode, string outputPath)
    {
        using (var form = CreateForm())
        {
            var tabs = GetField<TabControl>(form, "_tabControl");
            int tabIndex = mode == "create" ? 1 : mode == "process" ? 2 : mode == "review" ? 3 : mode == "export" ? 4 : 0;
            tabs.SelectedIndex = tabIndex;
            Pump();

            if (mode == "create") PrepareCreateScreenshot(form);
            if (mode == "process") PrepareProcessScreenshot(form);
            Pump();

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            using (var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(outputPath, ImageFormat.Png);
            }
            Console.WriteLine("PASS|FORM_CAPTURE|mode={0}|path={1}", mode, outputPath);
        }
    }

    private static MainForm CreateForm()
    {
        var form = new MainForm
        {
            WindowState = FormWindowState.Normal,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(20, 20),
            Size = new Size(1600, 900)
        };
        form.Show();
        Pump();
        return form;
    }

    private static void PrepareCreateScreenshot(MainForm form)
    {
        GetField<TextBox>(form, "_txtOrderTitle").Text = "TDD_正常流程_001";
        GetField<CheckBox>(form, "_chkChangeBg").Checked = true;
        GetField<CheckBox>(form, "_chkResize").Checked = true;
        GetField<CheckBox>(form, "_chkConvertFormat").Checked = true;
        GetField<ComboBox>(form, "_cmbBgColor").SelectedIndex = 2;
        GetField<ComboBox>(form, "_cmbTargetFormat").SelectedItem = "PNG";
        GetField<NumericUpDown>(form, "_nudTargetW").Value = 600;
        GetField<NumericUpDown>(form, "_nudTargetH").Value = 800;

        var files = GetField<List<string>>(form, "_importedFiles");
        string inputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_inputs");
        files.Clear();
        files.Add(Path.Combine(inputDir, "clear_portrait.jpg"));
        files.Add(Path.Combine(inputDir, "blur_image.jpg"));
        files.Add(Path.Combine(inputDir, "color_bias.png"));
        Invoke(form, "RefreshImportGrid");
    }

    private static void PrepareProcessScreenshot(MainForm form)
    {
        WorkOrder order = WorkOrderRepo.GetAll().First(x => x.Title == "TDD_正常流程_001");
        var combo = GetField<ComboBox>(form, "_cmbProcessOrder");
        combo.DataSource = new List<WorkOrder> { order };
        combo.DisplayMember = "Title";
        combo.ValueMember = "Id";
        Invoke(form, "RefreshProcessGrid", order.Id);
        var progress = GetField<ProgressBar>(form, "_progressBar");
        progress.Maximum = 3;
        progress.Value = 3;
        GetField<Label>(form, "_lblProgress").Text = "处理完成：3/3";
    }

    private static void Validate(string screenshotDir)
    {
        using (var form = CreateForm())
        {
            GetField<TabControl>(form, "_tabControl").SelectedIndex = 1;
            Pump();

            string first = InvokeWithDialogCapture(form, screenshotDir, "TC-02-title-required.png");
            if (!first.Contains("请输入工单标题"))
                throw new InvalidOperationException("Unexpected first validation message: " + first);

            GetField<TextBox>(form, "_txtOrderTitle").Text = "TDD_输入校验";
            string second = InvokeWithDialogCapture(form, screenshotDir, "TC-02-image-required.png");
            if (!second.Contains("请先导入至少一张图片"))
                throw new InvalidOperationException("Unexpected second validation message: " + second);

            bool created = WorkOrderRepo.GetAll().Any(x => x.Title == "TDD_输入校验");
            if (created) throw new InvalidOperationException("Invalid order was unexpectedly created");
            Console.WriteLine("PASS|VALIDATION|titleMessage={0}|imageMessage={1}|created={2}", first, second, created);
        }
    }

    private static void ApproveThroughUi(string screenshotDir)
    {
        using (var form = CreateForm())
        {
            GetField<TabControl>(form, "_tabControl").SelectedIndex = 3;
            Pump();
            int previousId = GetField<int>(form, "_currentReviewImageId");
            var transitions = new List<string>();

            for (int index = 0; index < 3; index++)
            {
                if (index < 2)
                    Invoke(form, "ReviewCurrentImage", true);
                else
                    InvokeReviewWithDialogCapture(form, screenshotDir, "TC-07-all-approved.png");
                Pump();

                int currentId = GetField<int>(form, "_currentReviewImageId");
                int approved = ImageItemRepo.GetByWorkOrderId(1).Count(x => x.ReviewStatus == "Approved");
                transitions.Add(previousId + "->" + currentId + " (approved=" + approved + ")");
                if (index < 2 && currentId == previousId)
                    throw new InvalidOperationException("Review selection did not advance from image " + previousId);
                previousId = currentId;
            }

            WorkOrder order = WorkOrderRepo.GetById(1);
            if (order.Status != "Approved")
                throw new InvalidOperationException("Order status after UI approval: " + order.Status);

            GetField<TabControl>(form, "_tabControl").SelectedIndex = 4;
            Pump();
            var exportGrid = GetField<DataGridView>(form, "_dgvExportOrders");
            bool listed = exportGrid.Rows.Cast<DataGridViewRow>()
                .Any(row => Convert.ToString(row.Cells["Title"].Value) == "TDD_正常流程_001");
            if (!listed) throw new InvalidOperationException("Approved order is absent from export grid");

            Directory.CreateDirectory(screenshotDir);
            using (var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(screenshotDir, "TC-07-export-list.png"), ImageFormat.Png);
            }
            Console.WriteLine("PASS|APPROVE_UI|transitions={0}|status={1}|exportListed={2}",
                string.Join(",", transitions), order.Status, listed);
        }
    }

    private static void InvokeReviewWithDialogCapture(MainForm form, string directory, string fileName)
    {
        Exception threadError = null;
        var thread = new Thread(() =>
        {
            try
            {
                IntPtr dialog = WaitForDialog(5000);
                if (dialog == IntPtr.Zero) throw new InvalidOperationException("Approval dialog not found");
                string message = ReadDialogText(dialog);
                if (!message.Contains("所有图片审核通过"))
                    throw new InvalidOperationException("Unexpected approval message: " + message);
                CaptureNativeWindow(dialog, Path.Combine(directory, fileName));
                SendMessage(dialog, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception ex) { threadError = ex; }
        });
        thread.IsBackground = true;
        thread.Start();
        Invoke(form, "ReviewCurrentImage", true);
        thread.Join(6000);
        if (threadError != null) throw threadError;
    }
    private static void RejectThroughUi(string screenshotDir)
    {
        using (var form = CreateForm())
        {
            GetField<TabControl>(form, "_tabControl").SelectedIndex = 3;
            Pump();

            string emptyMessage = InvokeRejectWithDialogCapture(form, "", screenshotDir, "TC-08-reason-required.png");
            if (!emptyMessage.Contains("请填写驳回原因"))
                throw new InvalidOperationException("Unexpected empty reason message: " + emptyMessage);
            if (WorkOrderRepo.GetById(2).Status != "PendingReview")
                throw new InvalidOperationException("Empty reason changed order status");

            string rejectMessage = InvokeRejectWithDialogCapture(form, "背景边缘处理不完整", screenshotDir, "TC-08-rejected.png");
            if (!rejectMessage.Contains("工单返回处理中"))
                throw new InvalidOperationException("Unexpected reject message: " + rejectMessage);

            WorkOrder order = WorkOrderRepo.GetById(2);
            ImageItem image = ImageItemRepo.GetByWorkOrderId(2).First();
            int detections = DetectionRepo.GetByWorkOrderId(2).Count;
            var processCombo = GetField<ComboBox>(form, "_cmbProcessOrder");
            bool reprocessListed = processCombo.Items.Cast<object>()
                .OfType<WorkOrder>().Any(x => x.Id == order.Id);

            Console.WriteLine("PASS|REJECT_UI|emptyRequired=true|status={0}|imageProcess={1}|imageReview={2}|detections={3}",
                order.Status, image.ProcessStatus, image.ReviewStatus, detections);
            Console.WriteLine("{0}|REPROCESS_LIST|listed={1}", reprocessListed ? "PASS" : "FAIL", reprocessListed);
        }
    }

    private static string InvokeRejectWithDialogCapture(MainForm form, string reason, string directory, string fileName)
    {
        GetField<TextBox>(form, "_txtRejectReason").Text = reason;
        string message = null;
        Exception threadError = null;
        var thread = new Thread(() =>
        {
            try
            {
                IntPtr dialog = WaitForDialog(5000);
                if (dialog == IntPtr.Zero) throw new InvalidOperationException("Reject dialog not found");
                message = ReadDialogText(dialog);
                CaptureNativeWindow(dialog, Path.Combine(directory, fileName));
                SendMessage(dialog, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception ex) { threadError = ex; }
        });
        thread.IsBackground = true;
        thread.Start();
        Invoke(form, "ReviewCurrentImage", false);
        thread.Join(6000);
        if (threadError != null) throw threadError;
        Pump();
        return message ?? "";
    }
    private static void ExportThroughUi(string screenshotDir)
    {
        using (var form = CreateForm())
        {
            GetField<TabControl>(form, "_tabControl").SelectedIndex = 4;
            Pump();
            var grid = GetField<DataGridView>(form, "_dgvExportOrders");
            if (grid.Rows.Count == 0) throw new InvalidOperationException("Export grid is empty");
            grid.Rows[0].Selected = true;

            string message = null;
            Exception threadError = null;
            var thread = new Thread(() =>
            {
                try
                {
                    IntPtr dialog = WaitForDialog(10000);
                    if (dialog == IntPtr.Zero) throw new InvalidOperationException("Export dialog not found");
                    message = ReadDialogText(dialog);
                    CaptureNativeWindow(dialog, Path.Combine(screenshotDir, "TC-09-export-success.png"));
                    SendMessage(dialog, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }
                catch (Exception ex) { threadError = ex; }
            });
            thread.IsBackground = true;
            thread.Start();
            Invoke(form, "ExportSelected");
            thread.Join(11000);
            if (threadError != null) throw threadError;
            if (message == null || !message.Contains("导出成功"))
                throw new InvalidOperationException("Unexpected export message: " + message);

            WorkOrder order = WorkOrderRepo.GetById(1);
            string zipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppData", "WorkOrders", "1", "WorkOrder_1.zip");
            if (order.Status != "Archived" || !File.Exists(zipPath))
                throw new InvalidOperationException("Export did not archive order or create ZIP");
            Console.WriteLine("PASS|EXPORT_UI|status={0}|zip={1}|message={2}", order.Status, zipPath, message);
        }
    }
    private static string InvokeWithDialogCapture(MainForm form, string directory, string fileName)
    {
        string message = null;
        Exception threadError = null;
        var thread = new Thread(() =>
        {
            try
            {
                IntPtr dialog = WaitForDialog(5000);
                if (dialog == IntPtr.Zero) throw new InvalidOperationException("Dialog not found");
                message = ReadDialogText(dialog);
                CaptureNativeWindow(dialog, Path.Combine(directory, fileName));
                SendMessage(dialog, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception ex) { threadError = ex; }
        });
        thread.IsBackground = true;
        thread.Start();
        Invoke(form, "SubmitOrder");
        thread.Join(6000);
        if (threadError != null) throw threadError;
        return message ?? "";
    }

    private static IntPtr WaitForDialog(int timeoutMs)
    {
        DateTime end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        do
        {
            IntPtr result = FindDialog();
            if (result != IntPtr.Zero) return result;
            Thread.Sleep(50);
        } while (DateTime.UtcNow < end);
        return IntPtr.Zero;
    }

    private static IntPtr FindDialog()
    {
        IntPtr found = IntPtr.Zero;
        uint current = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        EnumWindows((handle, state) =>
        {
            uint processId;
            GetWindowThreadProcessId(handle, out processId);
            if (processId == current && GetClass(handle) == "#32770")
            {
                found = handle;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private static string ReadDialogText(IntPtr dialog)
    {
        var values = new List<string>();
        EnumChildWindows(dialog, (handle, state) =>
        {
            if (GetClass(handle) == "Static")
            {
                var builder = new StringBuilder(512);
                GetWindowText(handle, builder, builder.Capacity);
                if (builder.Length > 0) values.Add(builder.ToString());
            }
            return true;
        }, IntPtr.Zero);
        return string.Join(" ", values);
    }

    private static string GetClass(IntPtr handle)
    {
        var builder = new StringBuilder(256);
        GetClassName(handle, builder, builder.Capacity);
        return builder.ToString();
    }

    private static void CaptureNativeWindow(IntPtr handle, string path)
    {
        Rect rect;
        GetWindowRect(handle, out rect);
        using (var bitmap = new Bitmap(rect.Right - rect.Left, rect.Bottom - rect.Top))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            IntPtr hdc = graphics.GetHdc();
            try { PrintWindow(handle, hdc, 2); }
            finally { graphics.ReleaseHdc(hdc); }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            bitmap.Save(path, ImageFormat.Png);
        }
    }

    private static T GetField<T>(object instance, string name)
    {
        return (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
    }

    private static object Invoke(object instance, string name, params object[] args)
    {
        return instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, args);
    }

    private static void Pump()
    {
        for (int i = 0; i < 8; i++)
        {
            Application.DoEvents();
            Thread.Sleep(50);
        }
    }
}
