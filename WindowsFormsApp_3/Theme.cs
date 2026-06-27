using System.Drawing;
using System.Windows.Forms;

namespace ImageBatchSystem
{
    /// <summary>
    /// 全局 UI 设计令牌 —— 统一配色、字号、间距，所有控件工厂方法引用此处。
    /// </summary>
    public static class Theme
    {
        // ── 主色板 ──
        public static readonly Color Primary       = Color.FromArgb(25, 82, 64);
        public static readonly Color PrimaryDark   = Color.FromArgb(14, 53, 42);
        public static readonly Color PrimarySoft   = Color.FromArgb(226, 238, 232);
        public static readonly Color Accent        = Color.FromArgb(205, 153, 61);
        public static readonly Color Success       = Color.FromArgb(38, 139, 91);
        public static readonly Color SuccessDark   = Color.FromArgb(27, 105, 68);
        public static readonly Color Danger        = Color.FromArgb(184, 66, 50);
        public static readonly Color DangerDark    = Color.FromArgb(143, 45, 34);
        public static readonly Color Warning       = Color.FromArgb(190, 125, 35);
        public static readonly Color Info          = Color.FromArgb(41, 116, 130);

        // ── 中性色 ──
        public static readonly Color BgMain        = Color.FromArgb(244, 242, 235);
        public static readonly Color BgCard        = Color.FromArgb(255, 254, 250);
        public static readonly Color BgAccent      = Color.FromArgb(238, 238, 231);
        public static readonly Color TextPrimary   = Color.FromArgb(31, 43, 38);
        public static readonly Color TextSecondary = Color.FromArgb(103, 112, 107);
        public static readonly Color Border        = Color.FromArgb(218, 215, 203);
        public static readonly Color HeaderBg      = Color.FromArgb(13, 42, 34);

        // ── 控件色 ──
        public static readonly Color DgvHeaderBg   = Color.FromArgb(25, 82, 64);
        public static readonly Color DgvHeaderFg   = Color.White;
        public static readonly Color DgvRowBg      = Color.FromArgb(255, 254, 250);
        public static readonly Color DgvAltRowBg   = Color.FromArgb(247, 246, 240);
        public static readonly Color DgvGridLine   = Color.FromArgb(226, 223, 212);
        public static readonly Color DgvHoverBg    = Color.FromArgb(226, 238, 232);

        // ── 字体 ──
        public const string FontFamily = "Microsoft YaHei UI";
        public static Font FontDisplay  = new Font(FontFamily, 18f, FontStyle.Bold);
        public static Font FontCaption  = new Font(FontFamily, 14f, FontStyle.Bold);
        public static Font FontHeading  = new Font(FontFamily, 10.5f, FontStyle.Bold);
        public static Font FontBody     = new Font(FontFamily, 9.5f);
        public static Font FontSmall    = new Font(FontFamily, 8.5f);
        public static Font FontMono     = new Font("Consolas", 9f);

        // ── 间距 ──
        public const int PadXS  = 4;
        public const int PadSM  = 8;
        public const int PadMD  = 16;
        public const int PadLG  = 24;
        public const int PadXL  = 32;

        // ── 尺寸 ──
        public const int BtnHeight    = 34;
        public const int BtnHeightLg  = 40;
        public const int InputHeight  = 30;
        public const int SectionGap   = 12;

        // ── 圆角（GDI+ 手动绘制用，此处仅作标注）──
        public const int Radius = 6;

        // ── 工厂方法 ──

        public static Button MakeButton(string text, Color bg, Color fg, int width = 120, bool large = false)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = fg,
                Font = large ? FontHeading : FontBody,
                Width = width,
                Height = large ? BtnHeightLg : BtnHeight,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = ControlPaint.Dark(bg);
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bg, 0.08f);
            b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(bg, 0.06f);
            return b;
        }

        public static Button MakePrimary(string text, int width = 140, bool large = false)
        {
            return MakeButton(text, Primary, Color.White, width, large);
        }

        public static Button MakeSuccess(string text, int width = 100)
        {
            return MakeButton(text, Success, Color.White, width);
        }

        public static Button MakeDanger(string text, int width = 100)
        {
            return MakeButton(text, Danger, Color.White, width);
        }

        public static Button MakeSecondary(string text, int width = 120)
        {
            return MakeButton(text, BgCard, Primary, width);
        }

        public static Panel MakePageHeader(string title, string subtitle)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 76,
                BackColor = BgMain,
                Padding = new Padding(0, 4, 0, 12)
            };
            panel.Controls.Add(new Label { Text = subtitle, Dock = DockStyle.Bottom, Height = 24, Font = FontSmall, ForeColor = TextSecondary });
            panel.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 34, Font = FontDisplay, ForeColor = TextPrimary });
            return panel;
        }

        public static void StyleInput(Control control)
        {
            control.Font = FontBody;
            control.BackColor = BgCard;
            control.ForeColor = TextPrimary;
        }
        /// <summary>
        /// 统一美化 DataGridView：隐藏行头、隔行变色、自定义表头、禁用添加行。
        /// </summary>
        public static void StyleDataGridView(DataGridView dgv)
        {
            dgv.BackgroundColor = BgCard;
            dgv.BorderStyle = BorderStyle.FixedSingle;
            dgv.GridColor = DgvGridLine;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.RowHeadersVisible = false;
            dgv.AllowUserToAddRows = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.ReadOnly = true;

            // 默认行样式
            dgv.RowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = DgvRowBg,
                ForeColor = TextPrimary,
                SelectionBackColor = PrimarySoft,
                SelectionForeColor = TextPrimary,
                Font = FontBody,
                Padding = new Padding(PadSM / 2)
            };

            dgv.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = DgvAltRowBg,
                ForeColor = TextPrimary,
                SelectionBackColor = PrimarySoft,
                SelectionForeColor = TextPrimary,
                Font = FontBody,
                Padding = new Padding(PadSM / 2)
            };

            // 表头样式
            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = DgvHeaderBg,
                ForeColor = DgvHeaderFg,
                Font = new Font(FontFamily, 9.5f, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleCenter
            };
            dgv.ColumnHeadersHeight = 42;
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            dgv.RowTemplate.Height = 36;
        }
    }

    public sealed class SolidToolStripRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var brush = new SolidBrush(e.ToolStrip.BackColor))
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
        }
    }
}