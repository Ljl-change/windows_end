using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ImageBatchSystem.Models;
using ImageBatchSystem.Repositories;
using ImageBatchSystem.Services;

namespace ImageBatchSystem.Forms
{
    public partial class MainForm : Form
    {
        private TabControl _tabControl;
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusLabel;

        // Tab1
        private DataGridView _dgvOrders;
        // Tab2
        private TextBox _txtOrderTitle;
        private CheckBox _chkChangeBg, _chkResize, _chkConvertFormat;
        private ComboBox _cmbBgColor, _cmbTargetFormat;
        private NumericUpDown _nudTargetW, _nudTargetH;
        private Panel _pnlBgOptions, _pnlResizeOptions, _pnlFormatOptions;
        private DataGridView _dgvImportImages;
        // Tab3
        private ComboBox _cmbProcessOrder;
        private DataGridView _dgvProcessImages;
        private ProgressBar _progressBar;
        private Label _lblProgress;
        // Tab4
        private ComboBox _cmbReviewOrder;
        private ListBox _lstReviewImages;
        private PictureBox _picOriginal, _picProcessed;
        private DataGridView _dgvDetection;
        private Button _btnApprove, _btnReject;
        private TextBox _txtRejectReason;
        private Label _lblReviewHint;
        // Tab5
        private DataGridView _dgvExportOrders;

        private List<string> _importedFiles = new List<string>();
        private int _currentReviewImageId;
        private bool _suppressComboEvents = false;

        public MainForm() { InitializeComponent(); SetupTabs(); _statusLabel.Text = "系统就绪"; }

        private void InitializeComponent()
        {
            this.Text = "图像批处理工单审核系统";
            this.BackColor = Theme.BgMain;
            this.WindowState = FormWindowState.Maximized;
            this.MinimumSize = new Size(1180, 760);

            _tabControl = new TabControl { Dock = DockStyle.Fill, Font = Theme.FontBody, DrawMode = TabDrawMode.OwnerDrawFixed, SizeMode = TabSizeMode.Fixed, ItemSize = new Size(176, 46), Padding = new Point(18, 6) };
            _tabControl.DrawItem += DrawTabItem;

            _statusLabel = new ToolStripStatusLabel { Text = "", Font = Theme.FontSmall, ForeColor = Color.FromArgb(215, 226, 221), Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _statusStrip = new StatusStrip { BackColor = Theme.PrimaryDark, SizingGrip = false, Padding = new Padding(18, 0, 12, 0), Renderer = new SolidToolStripRenderer() };
            _statusStrip.Items.Add(_statusLabel);

            var brandBar = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Theme.HeaderBg, Padding = new Padding(28, 14, 28, 10) };
            var mark = new Label { Text = "IB", Width = 48, Dock = DockStyle.Left, BackColor = Theme.Accent, ForeColor = Theme.HeaderBg, Font = new Font(Theme.FontFamily, 14f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
            var brandText = new Panel { Dock = DockStyle.Left, Width = 440, Padding = new Padding(16, 0, 0, 0) };
            brandText.Controls.Add(new Label { Text = "IMAGE BATCH WORKFLOW", Dock = DockStyle.Bottom, Height = 22, Font = Theme.FontSmall, ForeColor = Color.FromArgb(157, 181, 171) });
            brandText.Controls.Add(new Label { Text = "图像批处理工单审核系统", Dock = DockStyle.Top, Height = 34, Font = Theme.FontCaption, ForeColor = Color.White });
            brandBar.Controls.Add(brandText);
            brandBar.Controls.Add(mark);

            this.Controls.Add(_tabControl);
            this.Controls.Add(brandBar);
            this.Controls.Add(_statusStrip);
        }

        private void DrawTabItem(object sender, DrawItemEventArgs e)
        {
            var tabCtrl = (TabControl)sender;
            string text = tabCtrl.TabPages[e.Index].Text;
            bool sel = e.Index == tabCtrl.SelectedIndex;
            Color bg = sel ? Theme.Primary : Theme.HeaderBg;
            Color fg = sel ? Color.White : Color.FromArgb(183, 202, 194);
            using (var b = new SolidBrush(bg)) e.Graphics.FillRectangle(b, e.Bounds);
            if (sel)
            {
                using (var accent = new SolidBrush(Theme.Accent))
                    e.Graphics.FillRectangle(accent, e.Bounds.Left, e.Bounds.Bottom - 4, e.Bounds.Width, 4);
            }
            TextRenderer.DrawText(e.Graphics, text, Theme.FontHeading, e.Bounds, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        private void SetupTabs()
        {
            SetupTab1(); SetupTab2(); SetupTab3(); SetupTab4(); SetupTab5();
            _tabControl.SelectedIndexChanged += (s, e) =>
            {
                if (_tabControl.SelectedIndex == 4) RefreshExportGrid();
                else if (_tabControl.SelectedIndex == 3) RefreshReviewOrderCombo();
                else if (_tabControl.SelectedIndex == 2) RefreshProcessOrderCombo();
            };
        }

        private TableLayoutPanel CreatePaddedPanel(int cols)
        {
            return new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = cols, RowCount = 0, AutoScroll = true, Padding = new Padding(Theme.PadMD), BackColor = Theme.BgCard };
        }

        private void AddRow(TableLayoutPanel t, string label, Control c)
        {
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(new Label { Text = label, AutoSize = true, Font = Theme.FontBody, ForeColor = Theme.TextPrimary, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, Theme.SectionGap / 2, 0, Theme.SectionGap / 2) }, 0, t.RowCount);
            c.Margin = new Padding(0, Theme.SectionGap / 2, 0, Theme.SectionGap / 2);
            t.Controls.Add(c, 1, t.RowCount);
            t.RowCount++;
        }

        private void StyleCombo(ComboBox c, int w = 300) { c.DropDownStyle = ComboBoxStyle.DropDownList; c.Width = w; c.Font = Theme.FontBody; c.FlatStyle = FlatStyle.Flat; Theme.StyleInput(c); }

        private ToolStripButton ToolButton(string text, Action a) { var b = new ToolStripButton(text) { Font = Theme.FontBody, ForeColor = Theme.Primary, Padding = new Padding(10, 6, 10, 6), Margin = new Padding(0, 0, 8, 0) }; b.Click += (s, e) => a(); return b; }

        private Panel CreatePageSurface(TabPage tab, string title, string subtitle)
        {
            tab.BackColor = Theme.BgMain;
            var shell = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgMain, Padding = new Padding(28, 20, 28, 28) };
            var content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgCard, Padding = new Padding(20), BorderStyle = BorderStyle.FixedSingle };
            shell.Controls.Add(content);
            shell.Controls.Add(Theme.MakePageHeader(title, subtitle));
            tab.Controls.Add(shell);
            return content;
        }

        private void StatusCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var grid = (DataGridView)sender;
            string name = grid.Columns[e.ColumnIndex].Name;
            if (name != "状态" && name != "处理状态") return;
            string value = Convert.ToString(e.Value);
            if (value == "已通过" || value == "已归档" || value == "Done") e.CellStyle.ForeColor = Theme.Success;
            else if (value == "已驳回" || value == "Failed") e.CellStyle.ForeColor = Theme.Danger;
            else if (value == "待审核" || value == "处理中" || value == "Processing") e.CellStyle.ForeColor = Theme.Warning;
            else e.CellStyle.ForeColor = Theme.Primary;
            e.CellStyle.Font = Theme.FontHeading;
        }

        // ── Tab1: 工单列表 ──
        private void SetupTab1()
        {
            var tab = new TabPage("01  工单总览");
            var p = CreatePageSurface(tab, "工单总览", "查看全流程工单状态，快速进入创建、处理与归档环节");
            var ts = new ToolStrip { Dock = DockStyle.Top, Height = 44, GripStyle = ToolStripGripStyle.Hidden, BackColor = Theme.BgCard, Padding = new Padding(0, 3, 0, 5) };
            ts.Items.AddRange(new[] { ToolButton("新建工单", () => _tabControl.SelectedIndex = 1), ToolButton("刷新", RefreshOrderList), ToolButton("删除选中", DeleteOrder) });
            _dgvOrders = new DataGridView { Dock = DockStyle.Fill }; Theme.StyleDataGridView(_dgvOrders); _dgvOrders.CellFormatting += StatusCellFormatting;
            p.Controls.Add(_dgvOrders); p.Controls.Add(ts); p.Controls.SetChildIndex(ts, 0);
            _tabControl.TabPages.Add(tab);
        }

        private void RefreshOrderList()
        {
            var orders = WorkOrderRepo.GetAll(); _dgvOrders.DataSource = null;
            _dgvOrders.DataSource = orders.Select(o => new { o.Id, o.Title, 状态 = StatusDisplay(o.Status), 处理 = string.Format("{0}x{1}", o.TargetWidth, o.TargetHeight), 创建时间 = o.CreatedAt.ToString("yyyy-MM-dd HH:mm") }).ToList();
        }

        private void DeleteOrder()
        {
            if (_dgvOrders.SelectedRows.Count == 0) return;
            int id = (int)_dgvOrders.SelectedRows[0].Cells["Id"].Value;
            if (MessageBox.Show(string.Format("确定删除工单 #{0}？", id), "确认", MessageBoxButtons.YesNo) == DialogResult.Yes)
            { WorkOrderService.Delete(id); RefreshAll(); _statusLabel.Text = string.Format("已删除工单 #{0}", id); }
        }

        // ── Tab2: 创建工单 ──
        private void SetupTab2()
        {
            var tab = new TabPage("02  创建工单");
            var p = CreatePageSurface(tab, "创建新工单", "配置批处理规则并导入需要处理的图片");
            var t = CreatePaddedPanel(2); t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _txtOrderTitle = new TextBox { Width = 420, Font = Theme.FontBody, Height = Theme.InputHeight, BackColor = Theme.BgCard, ForeColor = Theme.TextPrimary };
            AddRow(t, "工单标题:", _txtOrderTitle);

            _chkChangeBg = new CheckBox { Text = "换底色", AutoSize = true, Font = Theme.FontBody };
            _chkChangeBg.CheckedChanged += (s, e) => _pnlBgOptions.Visible = _chkChangeBg.Checked;
            _pnlBgOptions = new Panel { Height = Theme.InputHeight, Visible = false };
            _cmbBgColor = new ComboBox { Width = 150 }; _cmbBgColor.Items.AddRange(new[] { "蓝底 #438EDB", "红底 #C81E1E", "白底 #FFFFFF" }); _cmbBgColor.SelectedIndex = 0;
            _pnlBgOptions.Controls.Add(_cmbBgColor); AddRow(t, "", _chkChangeBg); AddRow(t, "", _pnlBgOptions);

            _chkResize = new CheckBox { Text = "统一尺寸", AutoSize = true, Font = Theme.FontBody };
            _chkResize.CheckedChanged += (s, e) => _pnlResizeOptions.Visible = _chkResize.Checked;
            _pnlResizeOptions = new Panel { Height = Theme.InputHeight, Visible = false };
            _nudTargetW = new NumericUpDown { Width = 80, Minimum = 1, Maximum = 10000, Value = 600 };
            _nudTargetH = new NumericUpDown { Width = 80, Minimum = 1, Maximum = 10000, Value = 800, Left = 90 };
            _pnlResizeOptions.Controls.Add(_nudTargetW); _pnlResizeOptions.Controls.Add(_nudTargetH);
            _pnlResizeOptions.Controls.Add(new Label { Text = "宽 x 高", Location = new Point(0, 3), AutoSize = true, Font = Theme.FontSmall });
            AddRow(t, "", _chkResize); AddRow(t, "", _pnlResizeOptions);

            _chkConvertFormat = new CheckBox { Text = "格式转换", AutoSize = true, Font = Theme.FontBody };
            _chkConvertFormat.CheckedChanged += (s, e) => _pnlFormatOptions.Visible = _chkConvertFormat.Checked;
            _pnlFormatOptions = new Panel { Height = Theme.InputHeight, Visible = false };
            _cmbTargetFormat = new ComboBox { Width = 120 }; _cmbTargetFormat.Items.AddRange(new[] { "JPEG", "PNG", "BMP" }); _cmbTargetFormat.SelectedIndex = 0;
            _pnlFormatOptions.Controls.Add(_cmbTargetFormat); AddRow(t, "", _chkConvertFormat); AddRow(t, "", _pnlFormatOptions);

            var bib = new FlowLayoutPanel { AutoSize = true };
            bib.Controls.Add(Theme.MakeSecondary("导入图片", 120)); ((Button)bib.Controls[0]).Click += (s, e) => ImportImages();
            AddRow(t, "图片:", bib);

            _dgvImportImages = new DataGridView { Width = 700, Height = 150 }; Theme.StyleDataGridView(_dgvImportImages);
            AddRow(t, "", _dgvImportImages);

            var bsb = new FlowLayoutPanel { AutoSize = true };
            bsb.Controls.Add(Theme.MakePrimary("提交工单", 140, true)); ((Button)bsb.Controls[0]).Click += (s, e) => SubmitOrder();
            AddRow(t, "", bsb);

            p.Controls.Add(t); _tabControl.TabPages.Add(tab);
        }

        private void ImportImages()
        {
            using (var dlg = new OpenFileDialog { Title = "选择图片", Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp|所有文件|*.*", Multiselect = true })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                foreach (var f in dlg.FileNames) if (!_importedFiles.Contains(f)) _importedFiles.Add(f);
                RefreshImportGrid(); _statusLabel.Text = string.Format("已导入 {0} 张图片", _importedFiles.Count);
            }
        }

        private void RefreshImportGrid() { _dgvImportImages.DataSource = null; _dgvImportImages.DataSource = _importedFiles.Select(f => new { 文件名 = Path.GetFileName(f), 路径 = f }).ToList(); }

        private void SubmitOrder()
        {
            string title = _txtOrderTitle.Text.Trim();
            if (string.IsNullOrEmpty(title)) { MessageBox.Show("请输入工单标题"); return; }
            if (_importedFiles.Count == 0) { MessageBox.Show("请先导入至少一张图片"); return; }
            var opts = new List<string>();
            if (_chkChangeBg.Checked) opts.Add(string.Format("\"ChangeBg\":true,\"BgColor\":\"{0}\"", SelectedBgColor())); else opts.Add("\"ChangeBg\":false");
            if (_chkResize.Checked) opts.Add(string.Format("\"Resize\":true,\"TargetWidth\":{0},\"TargetHeight\":{1}", (int)_nudTargetW.Value, (int)_nudTargetH.Value)); else opts.Add("\"Resize\":false");
            if (_chkConvertFormat.Checked) opts.Add(string.Format("\"ConvertFormat\":true,\"TargetFormat\":\"{0}\"", _cmbTargetFormat.Text)); else opts.Add("\"ConvertFormat\":false");
            var wo = new WorkOrder { Title = title, ProcessOptions = "{" + string.Join(",", opts) + "}", TargetWidth = _chkResize.Checked ? (int)_nudTargetW.Value : 0, TargetHeight = _chkResize.Checked ? (int)_nudTargetH.Value : 0, TargetFormat = _chkConvertFormat.Checked ? _cmbTargetFormat.Text : "JPEG" };
            try
            {
                int orderId = WorkOrderService.CreateDraft(wo);
                string origDir = BatchProcessService.GetOriginalDir(orderId); Directory.CreateDirectory(origDir);
                foreach (var f in _importedFiles) { string dest = Path.Combine(origDir, Path.GetFileName(f)); File.Copy(f, dest, true); ImageItemRepo.Insert(new ImageItem { WorkOrderId = orderId, FileName = Path.GetFileName(f), OriginalPath = dest }); }
                WorkOrderService.Submit(orderId);
                _statusLabel.Text = string.Format("工单 #{0} 创建并提交成功", orderId); MessageBox.Show(string.Format("工单 #{0} 创建成功！", orderId));
                _txtOrderTitle.Clear(); _importedFiles.Clear(); RefreshImportGrid();
                _chkChangeBg.Checked = _chkResize.Checked = _chkConvertFormat.Checked = false;
                RefreshAll();
            }
            catch (Exception ex) { MessageBox.Show(string.Format("创建失败: {0}", ex.Message), "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private string SelectedBgColor() { string s = _cmbBgColor.Text; int i = s.LastIndexOf('#'); return i >= 0 ? s.Substring(i) : "#438EDB"; }

        // ── Tab3: 处理执行 ──
        private void SetupTab3()
        {
            var tab = new TabPage("03  批量处理");
            var p = CreatePageSurface(tab, "批量处理", "执行换底、缩放、格式转换与质量检测");
            var t = CreatePaddedPanel(2); t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _cmbProcessOrder = new ComboBox(); StyleCombo(_cmbProcessOrder);
            _cmbProcessOrder.SelectedIndexChanged += (s, e) => { if (_suppressComboEvents) return; if (_cmbProcessOrder.SelectedValue is int) { int id = (int)_cmbProcessOrder.SelectedValue; RefreshProcessGrid(id); } };
            AddRow(t, "选择工单:", _cmbProcessOrder);
            _dgvProcessImages = new DataGridView { Width = 820, Height = 280 }; Theme.StyleDataGridView(_dgvProcessImages); _dgvProcessImages.CellFormatting += StatusCellFormatting; AddRow(t, "", _dgvProcessImages);
            _lblProgress = new Label { AutoSize = true, Font = Theme.FontBody, ForeColor = Theme.TextSecondary }; AddRow(t, "", _lblProgress);
            _progressBar = new ProgressBar { Width = 400, Height = 22 }; AddRow(t, "", _progressBar);
            var bb = new FlowLayoutPanel { AutoSize = true }; bb.Controls.Add(Theme.MakePrimary("开始处理", 140, true)); ((Button)bb.Controls[0]).Click += (s, e) => StartProcessing(); AddRow(t, "", bb);
            p.Controls.Add(t); _tabControl.TabPages.Add(tab);
        }

        private void RefreshProcessGrid(int orderId) { var imgs = ImageItemRepo.GetByWorkOrderId(orderId); _dgvProcessImages.DataSource = null; _dgvProcessImages.DataSource = imgs.Select(i => new { i.Id, i.FileName, 处理状态 = i.ProcessStatus }).ToList(); }

        private void StartProcessing()
        {
            object pso = _cmbProcessOrder.SelectedValue; if (!(pso is int)) return; int orderId = (int)pso;
            try
            {
                WorkOrderService.StartProcessing(orderId); var wo = WorkOrderRepo.GetById(orderId); var svc = new BatchProcessService(wo); _progressBar.Value = 0;
                svc.Execute((cur, tot) => { this.Invoke(new Action(() => { _progressBar.Maximum = tot; _progressBar.Value = cur; _lblProgress.Text = string.Format("处理中: {0}/{1}", cur, tot); })); });
                WorkOrderService.CompleteProcessing(orderId); GenerateThumbnails(orderId);
                _lblProgress.Text = "处理完成！"; _statusLabel.Text = string.Format("工单 #{0} 处理完成", orderId);
                RefreshProcessGrid(orderId); RefreshAll(); MessageBox.Show("批处理及质量检测完成！");
            }
            catch (Exception ex) { MessageBox.Show(string.Format("处理失败: {0}", ex.Message), "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void GenerateThumbnails(int orderId)
        {
            var imgs = ImageItemRepo.GetByWorkOrderId(orderId); string td = BatchProcessService.GetThumbnailDir(orderId); Directory.CreateDirectory(td);
            foreach (var img in imgs)
            {
                try
                {
                    if (!string.IsNullOrEmpty(img.OriginalPath) && File.Exists(img.OriginalPath)) GenThumb(img.OriginalPath, Path.Combine(td, "orig_" + img.FileName + ".jpg"));
                    if (!string.IsNullOrEmpty(img.ProcessedPath) && File.Exists(img.ProcessedPath)) GenThumb(img.ProcessedPath, Path.Combine(td, "proc_" + img.FileName + ".jpg"));
                }
                catch { }
            }
        }

        private void GenThumb(string src, string dst) { using (var bmp = new Bitmap(src)) { int tw = 300, th = Math.Min((int)((double)bmp.Height / bmp.Width * 300), 500); using (var thumb = new Bitmap(bmp, new Size(tw, th))) thumb.Save(dst, System.Drawing.Imaging.ImageFormat.Jpeg); } }

        // ── Tab4: 审核 ──
        private void SetupTab4()
        {
            var tab = new TabPage("04  质量审核");
            var outer = CreatePageSurface(tab, "质量审核", "逐张对照原图与处理结果，结合检测指标完成人工复核");

            var top = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Theme.BgCard, Padding = new Padding(0, 4, 0, 10) };
            top.Controls.Add(new Label { Text = "选择工单:", Location = new Point(0, 10), AutoSize = true, Font = Theme.FontBody });
            _cmbReviewOrder = new ComboBox { Location = new Point(110, 7), Width = 460 }; StyleCombo(_cmbReviewOrder, 460);
            _cmbReviewOrder.SelectedIndexChanged += (s, e) => { if (_suppressComboEvents) return; LoadReviewOrder(); };
            top.Controls.Add(_cmbReviewOrder);

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterWidth = 6, SplitterDistance = 340, Panel1MinSize = 280 };
            split.SizeChanged += (s, e) =>
            {
                int available = split.ClientSize.Width - split.SplitterWidth;
                if (available > 800)
                    split.SplitterDistance = Math.Min(520, Math.Max(320, (int)(available * 0.30)));
            };
            _lstReviewImages = new ListBox { Dock = DockStyle.Top, Height = 280, Font = Theme.FontBody, IntegralHeight = false, BackColor = Theme.BgCard, ForeColor = Theme.TextPrimary, BorderStyle = BorderStyle.FixedSingle, ItemHeight = 28 };
            _lstReviewImages.SelectedIndexChanged += (s, e) => LoadReviewImage();
            _dgvDetection = new DataGridView { Dock = DockStyle.Top, Height = 80 }; Theme.StyleDataGridView(_dgvDetection);
            _lblReviewHint = new Label { Dock = DockStyle.Top, Height = 22, Font = Theme.FontSmall, ForeColor = Theme.Warning };
            _txtRejectReason = new TextBox { Dock = DockStyle.Top, Font = Theme.FontBody };
            var bb = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, Theme.SectionGap, 0, 0) };
            _btnApprove = Theme.MakeSuccess("通过", 100); _btnReject = Theme.MakeDanger("驳回", 100);
            _btnApprove.Click += (s, e) => ReviewCurrentImage(true); _btnReject.Click += (s, e) => ReviewCurrentImage(false);
            bb.Controls.Add(_btnApprove); bb.Controls.Add(_btnReject);
            split.Panel1.Controls.Add(bb); split.Panel1.Controls.Add(_txtRejectReason); split.Panel1.Controls.Add(_lblReviewHint); split.Panel1.Controls.Add(_dgvDetection); split.Panel1.Controls.Add(_lstReviewImages);

            var ps = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 430, SplitterWidth = 8, BackColor = Theme.BgMain };
            ps.SizeChanged += (s, e) =>
            {
                int available = ps.ClientSize.Width - ps.SplitterWidth;
                if (available > 600)
                    ps.SplitterDistance = available / 2;
            };

            var pt = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Theme.BgCard, Padding = new Padding(4) };
            pt.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            pt.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            pt.Controls.Add(new Label { Text = "原始图片", Dock = DockStyle.Fill, Font = Theme.FontHeading, ForeColor = Theme.TextSecondary, TextAlign = ContentAlignment.MiddleCenter }, 0, 0);
            _picOriginal = new PictureBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(32, 39, 36) };
            pt.Controls.Add(_picOriginal, 0, 1);

            var pb = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Theme.BgCard, Padding = new Padding(4) };
            pb.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            pb.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            pb.Controls.Add(new Label { Text = "处理结果", Dock = DockStyle.Fill, Font = Theme.FontHeading, ForeColor = Theme.TextSecondary, TextAlign = ContentAlignment.MiddleCenter }, 0, 0);
            _picProcessed = new PictureBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(32, 39, 36) };
            pb.Controls.Add(_picProcessed, 0, 1);
            ps.Panel1.Controls.Add(pt); ps.Panel2.Controls.Add(pb);
            split.Panel2.Controls.Add(ps);
            outer.Controls.Add(split); outer.Controls.Add(top); outer.Controls.SetChildIndex(top, 0);
            _tabControl.TabPages.Add(tab);
        }

        private void LoadReviewOrder()
        {
            int rOId; if (!(_cmbReviewOrder.SelectedValue is int)) return; else rOId = (int)_cmbReviewOrder.SelectedValue;
            var imgs = ImageItemRepo.GetByWorkOrderId(rOId);
            _lstReviewImages.DataSource = null; _lstReviewImages.DisplayMember = "FileName"; _lstReviewImages.ValueMember = "Id"; _lstReviewImages.DataSource = imgs;
            _picOriginal.Image = _picProcessed.Image = null; _dgvDetection.DataSource = null;
        }

        private void LoadReviewImage()
        {
            int iId; if (!(_lstReviewImages.SelectedValue is int)) return; else iId = (int)_lstReviewImages.SelectedValue;
            int rOId; if (!(_cmbReviewOrder.SelectedValue is int)) return; else rOId = (int)_cmbReviewOrder.SelectedValue;
            _currentReviewImageId = iId;
            var img = ImageItemRepo.GetByWorkOrderId(rOId).FirstOrDefault(x => x.Id == iId); if (img == null) return;
            if (!string.IsNullOrEmpty(img.OriginalPath) && File.Exists(img.OriginalPath)) _picOriginal.Image = Image.FromFile(img.OriginalPath); else _picOriginal.Image = null;
            if (!string.IsNullOrEmpty(img.ProcessedPath) && File.Exists(img.ProcessedPath)) _picProcessed.Image = Image.FromFile(img.ProcessedPath); else _picProcessed.Image = null;
            var dr = DetectionRepo.GetByImageId(iId);
            if (dr != null) { _dgvDetection.DataSource = null; _dgvDetection.DataSource = new List<object> { new { 模糊度 = dr.BlurScore.ToString("F2"), 模合 = dr.BlurPassed ? "OK" : "NG", 分辨率 = string.Format("{0}x{1}", dr.ResolutionW, dr.ResolutionH), 分合 = dr.ResPassed ? "OK" : "NG", R偏 = dr.ColorBiasR.ToString("F3"), G偏 = dr.ColorBiasG.ToString("F3"), B偏 = dr.ColorBiasB.ToString("F3"), 色合 = dr.ColorPassed ? "OK" : "NG", 建议 = dr.SuggestPass ? "建议通过" : "建议驳回" } }; }
            _lblReviewHint.Text = img.ReviewStatus == "Approved" ? "此图片已审核通过" : img.ReviewStatus == "Rejected" ? string.Format("此图片已驳回: {0}", img.ReviewComment ?? "") : "";
        }

        private void ReviewCurrentImage(bool approved)
        {
            // 没有有效图片或工单时直接返回，避免对空选择执行数据库更新。
            if (_currentReviewImageId == 0) return;
            object v = _cmbReviewOrder.SelectedValue; if (!(v is int)) return; int oId = (int)v;
            string st = approved ? "Approved" : "Rejected", cmt = null;
            // 驳回原因属于必填业务数据，空原因不会进入数据库。
            if (!approved) { cmt = _txtRejectReason.Text.Trim(); if (string.IsNullOrEmpty(cmt)) { MessageBox.Show("请填写驳回原因"); return; } }
            // 先保存当前图片的审核结论，再根据全部图片状态决定工单下一步。
            ImageItemRepo.UpdateReview(_currentReviewImageId, st, cmt);
            ProcessLogRepo.Insert(new ProcessLog { WorkOrderId = oId, ImageId = _currentReviewImageId, Action = approved ? "图片审核通过" : string.Format("图片审核驳回: {0}", cmt), Operator = "审核员", RejectReason = cmt, CreatedAt = DateTime.Now });
            // 任意图片被驳回时，整个工单返回Rejected并重置为可重新处理状态。
            if (!approved) { WorkOrderService.Reject(oId, cmt, "审核员"); _statusLabel.Text = string.Format("工单 #{0} 已驳回", oId); RefreshAll(); _currentReviewImageId = 0; _txtRejectReason.Clear(); MessageBox.Show("该图片已驳回，工单返回处理中。"); }
            // 只有所有图片都通过，工单才进入Approved并出现在导出列表。
            else if (ImageItemRepo.AllImagesApproved(oId)) { WorkOrderService.Approve(oId); _statusLabel.Text = string.Format("工单 #{0} 审核全部通过", oId); RefreshAll(); _currentReviewImageId = 0; _txtRejectReason.Clear(); MessageBox.Show("所有图片审核通过，工单已通过！"); }
            else
            {
                _statusLabel.Text = string.Format("图片 #{0} 审核通过，继续审核剩余图片", _currentReviewImageId);
                // 重新查询最新审核状态，定位第一张尚未通过的图片。
                var images = ImageItemRepo.GetByWorkOrderId(oId);
                int nextIndex = images.FindIndex(i => i.ReviewStatus != "Approved");
                // 重新绑定并改变SelectedIndex，触发界面显示下一张图片。
                _lstReviewImages.DataSource = null;
                _lstReviewImages.DisplayMember = "FileName";
                _lstReviewImages.ValueMember = "Id";
                _lstReviewImages.DataSource = images;
                if (nextIndex >= 0)
                    _lstReviewImages.SelectedIndex = nextIndex;
            }
        }

        // ── Tab5: 导出归档 ──
        private void SetupTab5()
        {
            var tab = new TabPage("05  导出归档");
            var p = CreatePageSurface(tab, "导出归档", "打包原图、处理结果、检测报告与操作日志");
            _dgvExportOrders = new DataGridView { Dock = DockStyle.Top, Height = 300 }; Theme.StyleDataGridView(_dgvExportOrders);
            var btn = Theme.MakePrimary("一键导出 ZIP", 160, true); btn.Location = new Point(Theme.PadLG, 320); btn.Click += (s, e) => ExportSelected();
            p.Controls.Add(_dgvExportOrders); p.Controls.Add(btn);
            _tabControl.TabPages.Add(tab);
        }

        private void ExportSelected()
        {
            if (_dgvExportOrders.Rows.Count == 0) { MessageBox.Show("没有可导出的已通过工单"); return; }
            int oId = _dgvExportOrders.SelectedRows.Count > 0 ? (int)_dgvExportOrders.SelectedRows[0].Cells["Id"].Value : (int)_dgvExportOrders.Rows[0].Cells["Id"].Value;
            try { string zp = ExportService.Export(oId); WorkOrderService.Archive(oId); RefreshAll(); _statusLabel.Text = string.Format("导出完成: {0}", zp); MessageBox.Show(string.Format("导出成功！\n{0}", zp)); }
            catch (Exception ex) { MessageBox.Show(string.Format("导出失败: {0}", ex.Message), "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // ── 公共 ──
        private static string StatusDisplay(string s) { switch (s) { case "Draft": return "草稿"; case "Submitted": return "已提交"; case "Processing": return "处理中"; case "PendingReview": return "待审核"; case "Approved": return "已通过"; case "Rejected": return "已驳回"; case "Archived": return "已归档"; default: return s; } }
        private void RefreshAll() { RefreshOrderList(); RefreshProcessOrderCombo(); RefreshReviewOrderCombo(); RefreshExportGrid(); }
        private void RefreshProcessOrderCombo()
        {
            _suppressComboEvents = true;
            _cmbProcessOrder.DataSource = null;
            var l = WorkOrderRepo.GetByStatus("Submitted");
            l.AddRange(WorkOrderRepo.GetByStatus("Rejected"));
            l = l.OrderByDescending(o => o.CreatedAt).ToList();
            _cmbProcessOrder.DisplayMember = "Title";
            _cmbProcessOrder.ValueMember = "Id";
            _cmbProcessOrder.DataSource = l;
            _suppressComboEvents = false;
        }
        private void RefreshReviewOrderCombo()
        {
            _suppressComboEvents = true;
            _cmbReviewOrder.DataSource = null;
            var l = WorkOrderRepo.GetByStatus("PendingReview");
            _cmbReviewOrder.DisplayMember = "Title";
            _cmbReviewOrder.ValueMember = "Id";
            _cmbReviewOrder.DataSource = l;
            _suppressComboEvents = false;
            if (l.Count > 0)
                LoadReviewOrder();
            else
            {
                _lstReviewImages.DataSource = null;
                _currentReviewImageId = 0;
                _picOriginal.Image = _picProcessed.Image = null;
                _dgvDetection.DataSource = null;
            }
        }
        private void RefreshExportGrid() { var l = WorkOrderRepo.GetByStatus("Approved"); _dgvExportOrders.DataSource = null; _dgvExportOrders.DataSource = l.Select(o => new { o.Id, o.Title, 创建时间 = o.CreatedAt.ToString("yyyy-MM-dd HH:mm") }).ToList(); }
        protected override void OnLoad(EventArgs e) { base.OnLoad(e); RefreshAll(); }
    }
}
