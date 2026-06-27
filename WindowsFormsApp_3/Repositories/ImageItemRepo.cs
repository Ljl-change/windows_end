using System;
using System.Collections.Generic;
using System.Data.SQLite;
using ImageBatchSystem.Models;

namespace ImageBatchSystem.Repositories
{
    /// <summary>
    /// 图片表数据访问 —— 管理工单下图片的 CRUD 和状态更新。
    /// 包含逐张审核更新的方法。
    /// </summary>
    public static class ImageItemRepo
    {
        public static int Insert(ImageItem img)
        {
            using (var conn = DbContext.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO Images (WorkOrderId, FileName, OriginalPath, ProcessedPath, ProcessStatus, ReviewStatus)
                        VALUES (@wo, @fn, @op, @pp, @ps, @rs);
                        SELECT last_insert_rowid();";
                    cmd.Parameters.AddWithValue("@wo", img.WorkOrderId);
                    cmd.Parameters.AddWithValue("@fn", img.FileName);
                    cmd.Parameters.AddWithValue("@op", (object)img.OriginalPath ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@pp", (object)img.ProcessedPath ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ps", img.ProcessStatus);
                    cmd.Parameters.AddWithValue("@rs", img.ReviewStatus);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public static List<ImageItem> GetByWorkOrderId(int workOrderId)
        {
            var list = new List<ImageItem>();
            using (var conn = DbContext.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM Images WHERE WorkOrderId = @wo ORDER BY Id";
                    cmd.Parameters.AddWithValue("@wo", workOrderId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            list.Add(Map(reader));
                    }
                }
            }
            return list;
        }

        public static void UpdateProcessedPath(int id, string path)
        {
            using (var conn = DbContext.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE Images SET ProcessedPath = @pp, ProcessStatus = 'Done' WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@pp", path);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void UpdateProcessStatus(int id, string status)
        {
            using (var conn = DbContext.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE Images SET ProcessStatus = @s WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@s", status);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// 驳回后重置工单内所有图片的处理和审核状态，准备重新处理。
        /// </summary>
        public static void ResetProcessStatusByWorkOrder(int workOrderId)
        {
            using (var conn = DbContext.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"UPDATE Images
                        SET ProcessStatus = 'Pending', ProcessedPath = NULL,
                            ReviewStatus = 'Pending', ReviewComment = NULL, ReviewedAt = NULL
                        WHERE WorkOrderId = @wo";
                    cmd.Parameters.AddWithValue("@wo", workOrderId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// 逐张审核 —— 更新单张图片的审核状态和意见。
        /// </summary>
        public static void UpdateReview(int id, string reviewStatus, string comment)
        {
            using (var conn = DbContext.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        UPDATE Images
                        SET ReviewStatus = @rs, ReviewComment = @rc, ReviewedAt = @ra
                        WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@rs", reviewStatus);
                    cmd.Parameters.AddWithValue("@rc", (object)comment ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ra", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// 检查工单内是否所有图片都已审核通过。
        /// </summary>
        public static bool AllImagesApproved(int workOrderId)
        {
            using (var conn = DbContext.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM Images WHERE WorkOrderId = @wo AND ReviewStatus != 'Approved'";
                    cmd.Parameters.AddWithValue("@wo", workOrderId);
                    long notApproved = (long)cmd.ExecuteScalar();
                    return notApproved == 0;
                }
            }
        }

        private static ImageItem Map(SQLiteDataReader reader)
        {
            return new ImageItem
            {
                Id = Convert.ToInt32(reader["Id"]),
                WorkOrderId = Convert.ToInt32(reader["WorkOrderId"]),
                FileName = reader["FileName"].ToString(),
                OriginalPath = reader["OriginalPath"] == DBNull.Value ? null : reader["OriginalPath"].ToString(),
                ProcessedPath = reader["ProcessedPath"] == DBNull.Value ? null : reader["ProcessedPath"].ToString(),
                ProcessStatus = reader["ProcessStatus"].ToString(),
                ReviewStatus = reader["ReviewStatus"].ToString(),
                ReviewComment = reader["ReviewComment"] == DBNull.Value ? null : reader["ReviewComment"].ToString(),
                ReviewedAt = reader["ReviewedAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["ReviewedAt"])
            };
        }
    }
}
