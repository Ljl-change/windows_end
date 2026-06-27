using System;
using System.Collections.Generic;
using System.Data.SQLite;
using ImageBatchSystem.Models;

namespace ImageBatchSystem.Repositories
{
    /// <summary>
    /// 检测结果表数据访问 —— 单条插入与按图片/工单查询。
    /// </summary>
    public static class DetectionRepo
    {
        public static void Insert(DetectionResult dr)
        {
            using (var conn = DbContext.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO DetectionResults (ImageId, BlurScore, BlurPassed, ResolutionW, ResolutionH, ResPassed,
                            ColorBiasR, ColorBiasG, ColorBiasB, ColorPassed, SuggestPass, DetectedAt)
                        VALUES (@img, @bs, @bp, @rw, @rh, @rp, @cr, @cg, @cb, @cp, @sp, @da);";
                    cmd.Parameters.AddWithValue("@img", dr.ImageId);
                    cmd.Parameters.AddWithValue("@bs", dr.BlurScore);
                    cmd.Parameters.AddWithValue("@bp", dr.BlurPassed ? 1 : 0);
                    cmd.Parameters.AddWithValue("@rw", dr.ResolutionW);
                    cmd.Parameters.AddWithValue("@rh", dr.ResolutionH);
                    cmd.Parameters.AddWithValue("@rp", dr.ResPassed ? 1 : 0);
                    cmd.Parameters.AddWithValue("@cr", dr.ColorBiasR);
                    cmd.Parameters.AddWithValue("@cg", dr.ColorBiasG);
                    cmd.Parameters.AddWithValue("@cb", dr.ColorBiasB);
                    cmd.Parameters.AddWithValue("@cp", dr.ColorPassed ? 1 : 0);
                    cmd.Parameters.AddWithValue("@sp", dr.SuggestPass ? 1 : 0);
                    cmd.Parameters.AddWithValue("@da", dr.DetectedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static DetectionResult GetByImageId(int imageId)
        {
            using (var conn = DbContext.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM DetectionResults WHERE ImageId = @id";
                    cmd.Parameters.AddWithValue("@id", imageId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read()) return Map(reader);
                    }
                }
            }
            return null;
        }

        public static List<DetectionResult> GetByWorkOrderId(int workOrderId)
        {
            var list = new List<DetectionResult>();
            using (var conn = DbContext.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT dr.* FROM DetectionResults dr
                        JOIN Images img ON dr.ImageId = img.Id
                        WHERE img.WorkOrderId = @wo
                        ORDER BY img.Id";
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

        /// <summary>
        /// 删除某工单下所有检测结果（驳回重处理时清理旧检测数据）。
        /// </summary>
        public static void DeleteByWorkOrderId(int workOrderId)
        {
            using (var conn = DbContext.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        DELETE FROM DetectionResults WHERE ImageId IN
                        (SELECT Id FROM Images WHERE WorkOrderId = @wo)";
                    cmd.Parameters.AddWithValue("@wo", workOrderId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static DetectionResult Map(SQLiteDataReader reader)
        {
            return new DetectionResult
            {
                Id = Convert.ToInt32(reader["Id"]),
                ImageId = Convert.ToInt32(reader["ImageId"]),
                BlurScore = Convert.ToDouble(reader["BlurScore"]),
                BlurPassed = Convert.ToInt32(reader["BlurPassed"]) != 0,
                ResolutionW = Convert.ToInt32(reader["ResolutionW"]),
                ResolutionH = Convert.ToInt32(reader["ResolutionH"]),
                ResPassed = Convert.ToInt32(reader["ResPassed"]) != 0,
                ColorBiasR = Convert.ToDouble(reader["ColorBiasR"]),
                ColorBiasG = Convert.ToDouble(reader["ColorBiasG"]),
                ColorBiasB = Convert.ToDouble(reader["ColorBiasB"]),
                ColorPassed = Convert.ToInt32(reader["ColorPassed"]) != 0,
                SuggestPass = Convert.ToInt32(reader["SuggestPass"]) != 0,
                DetectedAt = Convert.ToDateTime(reader["DetectedAt"])
            };
        }
    }
}
