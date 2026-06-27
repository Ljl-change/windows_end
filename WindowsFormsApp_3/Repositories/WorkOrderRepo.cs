using System;
using System.Collections.Generic;
using ImageBatchSystem.Models;

namespace ImageBatchSystem.Repositories
{
    /// <summary>
    /// 工单表数据访问 —— 提供 CRUD 和状态更新。
    /// 状态机转换由 WorkOrderService 校验后调用此处落库。
    /// </summary>
    public static class WorkOrderRepo
    {
        public static int Insert(WorkOrder wo)
        {
            using (var conn = DbContext.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO WorkOrders (Title, Status, ProcessOptions, TargetWidth, TargetHeight, TargetFormat, CreatedAt, UpdatedAt)
                        VALUES (@title, @status, @opts, @tw, @th, @fmt, @created, @updated);
                        SELECT last_insert_rowid();";
                    AddParams(cmd, wo);
                    cmd.Parameters.AddWithValue("@created", wo.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@updated", wo.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public static WorkOrder GetById(int id)
        {
            using (var conn = DbContext.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM WorkOrders WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read()) return Map(reader);
                    }
                }
            }
            return null;
        }

        public static List<WorkOrder> GetAll()
        {
            var list = new List<WorkOrder>();
            using (var conn = DbContext.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM WorkOrders ORDER BY CreatedAt DESC";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            list.Add(Map(reader));
                    }
                }
            }
            return list;
        }

        public static List<WorkOrder> GetByStatus(string status)
        {
            var list = new List<WorkOrder>();
            using (var conn = DbContext.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM WorkOrders WHERE Status = @status ORDER BY CreatedAt DESC";
                    cmd.Parameters.AddWithValue("@status", status);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            list.Add(Map(reader));
                    }
                }
            }
            return list;
        }

        public static void UpdateStatus(int id, string newStatus)
        {
            using (var conn = DbContext.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE WorkOrders SET Status = @s, UpdatedAt = @u WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@s", newStatus);
                    cmd.Parameters.AddWithValue("@u", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void Delete(int id)
        {
            using (var conn = DbContext.GetConnection())
            {
                conn.Open();
                // 关联表删除位于同一事务中，任一步失败都不能留下半删除状态。
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = transaction;
                            // 按依赖顺序删除：检测结果和日志→图片→工单主记录。
                            // @id使用参数绑定，避免把外部数据直接拼接到SQL中。
                            cmd.CommandText = @"
                                DELETE FROM DetectionResults
                                WHERE ImageId IN (SELECT Id FROM Images WHERE WorkOrderId = @id);
                                DELETE FROM ProcessLogs WHERE WorkOrderId = @id;
                                DELETE FROM Images WHERE WorkOrderId = @id;
                                DELETE FROM WorkOrders WHERE Id = @id;";
                            cmd.Parameters.AddWithValue("@id", id);
                            cmd.ExecuteNonQuery();
                        }
                        // 所有SQL执行成功后一次性提交。
                        transaction.Commit();
                    }
                    catch
                    {
                        // 出现异常时回滚全部删除操作，并把异常交给上层处理。
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private static void AddParams(System.Data.SQLite.SQLiteCommand cmd, WorkOrder wo)
        {
            cmd.Parameters.AddWithValue("@title", wo.Title);
            cmd.Parameters.AddWithValue("@status", wo.Status);
            cmd.Parameters.AddWithValue("@opts", (object)wo.ProcessOptions ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tw", wo.TargetWidth);
            cmd.Parameters.AddWithValue("@th", wo.TargetHeight);
            cmd.Parameters.AddWithValue("@fmt", (object)wo.TargetFormat ?? "JPEG");
        }

        private static WorkOrder Map(System.Data.SQLite.SQLiteDataReader reader)
        {
            return new WorkOrder
            {
                Id = Convert.ToInt32(reader["Id"]),
                Title = reader["Title"].ToString(),
                Status = reader["Status"].ToString(),
                ProcessOptions = reader["ProcessOptions"] == DBNull.Value ? null : reader["ProcessOptions"].ToString(),
                TargetWidth = Convert.ToInt32(reader["TargetWidth"]),
                TargetHeight = Convert.ToInt32(reader["TargetHeight"]),
                TargetFormat = reader["TargetFormat"].ToString(),
                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                UpdatedAt = Convert.ToDateTime(reader["UpdatedAt"])
            };
        }
    }
}
