using System;
using System.Collections.Generic;
using System.Data.SQLite;
using ImageBatchSystem.Models;

namespace ImageBatchSystem.Repositories
{
    /// <summary>
    /// 操作日志表数据访问 —— 记录工单生命周期中每一次操作。
    /// </summary>
    public static class ProcessLogRepo
    {
        public static void Insert(ProcessLog log)
        {
            using (var conn = DbContext.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO ProcessLogs (WorkOrderId, ImageId, Action, Operator, RejectReason, CreatedAt)
                        VALUES (@wo, @img, @action, @op, @reason, @created);";
                    cmd.Parameters.AddWithValue("@wo", log.WorkOrderId);
                    cmd.Parameters.AddWithValue("@img", (object)log.ImageId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@action", log.Action);
                    cmd.Parameters.AddWithValue("@op", (object)log.Operator ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@reason", (object)log.RejectReason ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@created", log.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static List<ProcessLog> GetByWorkOrderId(int workOrderId)
        {
            var list = new List<ProcessLog>();
            using (var conn = DbContext.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM ProcessLogs WHERE WorkOrderId = @wo ORDER BY CreatedAt";
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

        private static ProcessLog Map(SQLiteDataReader reader)
        {
            return new ProcessLog
            {
                Id = Convert.ToInt32(reader["Id"]),
                WorkOrderId = Convert.ToInt32(reader["WorkOrderId"]),
                ImageId = reader["ImageId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["ImageId"]),
                Action = reader["Action"].ToString(),
                Operator = reader["Operator"] == DBNull.Value ? null : reader["Operator"].ToString(),
                RejectReason = reader["RejectReason"] == DBNull.Value ? null : reader["RejectReason"].ToString(),
                CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
            };
        }
    }
}
