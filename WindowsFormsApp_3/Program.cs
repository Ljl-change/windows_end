using System;
using System.Windows.Forms;

namespace ImageBatchSystem
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 初始化数据库——启动时自动建库建表
            string dbPath = System.IO.Path.Combine(
                Application.StartupPath, "AppData", "database.db");
            string dbDir = System.IO.Path.GetDirectoryName(dbPath);
            if (!System.IO.Directory.Exists(dbDir))
                System.IO.Directory.CreateDirectory(dbDir);

            Repositories.DbContext.Initialize(dbPath);

            Application.Run(new Forms.MainForm());
        }
    }
}
