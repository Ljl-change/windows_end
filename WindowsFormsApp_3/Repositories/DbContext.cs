using System.Data.SQLite;

namespace ImageBatchSystem.Repositories
{
    /// <summary>
    /// SQLite 数据库连接管理与建表初始化。
    /// 所有 Repository 通过此类的 GetConnection() 获取连接。
    /// </summary>
    public static class DbContext
    {
        private static string _connectionString;

        public static void Initialize(string dbPath)
        {
            _connectionString = string.Format("Data Source={0};Version=3;", dbPath);

            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS WorkOrders (
                            Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                            Title           TEXT    NOT NULL,
                            Status          TEXT    NOT NULL DEFAULT 'Draft',
                            ProcessOptions  TEXT,
                            TargetWidth     INTEGER DEFAULT 0,
                            TargetHeight    INTEGER DEFAULT 0,
                            TargetFormat    TEXT    DEFAULT 'JPEG',
                            CreatedAt       DATETIME NOT NULL,
                            UpdatedAt       DATETIME NOT NULL
                        );

                        CREATE TABLE IF NOT EXISTS Images (
                            Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                            WorkOrderId     INTEGER NOT NULL,
                            FileName        TEXT    NOT NULL,
                            OriginalPath    TEXT,
                            ProcessedPath   TEXT,
                            ProcessStatus   TEXT    NOT NULL DEFAULT 'Pending',
                            ReviewStatus    TEXT    NOT NULL DEFAULT 'Pending',
                            ReviewComment   TEXT,
                            ReviewedAt      DATETIME,
                            FOREIGN KEY (WorkOrderId) REFERENCES WorkOrders(Id)
                        );

                        CREATE TABLE IF NOT EXISTS DetectionResults (
                            Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                            ImageId     INTEGER NOT NULL,
                            BlurScore   REAL    DEFAULT 0,
                            BlurPassed  INTEGER DEFAULT 0,
                            ResolutionW INTEGER DEFAULT 0,
                            ResolutionH INTEGER DEFAULT 0,
                            ResPassed   INTEGER DEFAULT 0,
                            ColorBiasR  REAL    DEFAULT 0,
                            ColorBiasG  REAL    DEFAULT 0,
                            ColorBiasB  REAL    DEFAULT 0,
                            ColorPassed INTEGER DEFAULT 0,
                            SuggestPass INTEGER DEFAULT 0,
                            DetectedAt  DATETIME NOT NULL,
                            FOREIGN KEY (ImageId) REFERENCES Images(Id)
                        );

                        CREATE TABLE IF NOT EXISTS ProcessLogs (
                            Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                            WorkOrderId   INTEGER NOT NULL,
                            ImageId       INTEGER,
                            Action        TEXT    NOT NULL,
                            Operator      TEXT,
                            RejectReason  TEXT,
                            CreatedAt     DATETIME NOT NULL,
                            FOREIGN KEY (WorkOrderId) REFERENCES WorkOrders(Id),
                            FOREIGN KEY (ImageId) REFERENCES Images(Id)
                        );
                    ";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// 创建并返回一个新的 SQLite 连接。调用方负责 using 释放。
        /// </summary>
        public static SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(_connectionString);
        }
    }
}
