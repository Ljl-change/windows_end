using System;

namespace ImageBatchSystem.Models
{
    public class ImageItem
    {
        public int Id { get; set; }
        public int WorkOrderId { get; set; }
        public string FileName { get; set; }
        public string OriginalPath { get; set; }
        public string ProcessedPath { get; set; }
        public string ProcessStatus { get; set; }
        public string ReviewStatus { get; set; }
        public string ReviewComment { get; set; }
        public DateTime? ReviewedAt { get; set; }

        public ImageItem()
        {
            ProcessStatus = "Pending";
            ReviewStatus = "Pending";
        }
    }
}
