using System;

namespace ImageBatchSystem.Models
{
    public class WorkOrder
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public string ProcessOptions { get; set; }
        public int TargetWidth { get; set; }
        public int TargetHeight { get; set; }
        public string TargetFormat { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public WorkOrder()
        {
            Status = "Draft";
            TargetFormat = "JPEG";
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }
    }
}
