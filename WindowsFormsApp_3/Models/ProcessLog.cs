using System;

namespace ImageBatchSystem.Models
{
    public class ProcessLog
    {
        public int Id { get; set; }
        public int WorkOrderId { get; set; }
        public int? ImageId { get; set; }
        public string Action { get; set; }
        public string Operator { get; set; }
        public string RejectReason { get; set; }
        public DateTime CreatedAt { get; set; }

        public ProcessLog()
        {
            CreatedAt = DateTime.Now;
        }
    }
}
