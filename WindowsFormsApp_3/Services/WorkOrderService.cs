using System;
using System.Collections.Generic;
using System.IO;
using ImageBatchSystem.Models;
using ImageBatchSystem.Repositories;

namespace ImageBatchSystem.Services
{
    /// <summary>
    /// 工单状态机服务 —— 定义合法状态转换规则，校验后落库并记录日志。
    /// </summary>
    public static class WorkOrderService
    {
        private static readonly Dictionary<string, List<string>> Transitions =
            new Dictionary<string, List<string>>
            {
                { "Draft",          new List<string> { "Submitted" } },
                { "Submitted",      new List<string> { "Processing" } },
                { "Processing",     new List<string> { "PendingReview", "Rejected" } },
                { "PendingReview",  new List<string> { "Approved", "Rejected" } },
                { "Rejected",       new List<string> { "Processing" } },
                { "Approved",       new List<string> { "Archived" } },
                { "Archived",       new List<string> { } }
            };

        public static void Transition(int workOrderId, string newStatus)
        {
            var wo = WorkOrderRepo.GetById(workOrderId);
            if (wo == null)
                throw new ArgumentException(string.Format("工单 {0} 不存在", workOrderId));

            if (!Transitions.ContainsKey(wo.Status) ||
                !Transitions[wo.Status].Contains(newStatus))
            {
                throw new InvalidOperationException(
                    string.Format("非法状态跳转: {0} → {1}", wo.Status, newStatus));
            }

            WorkOrderRepo.UpdateStatus(workOrderId, newStatus);

            ProcessLogRepo.Insert(new ProcessLog
            {
                WorkOrderId = workOrderId,
                Action = string.Format("状态变更: {0} → {1}", wo.Status, newStatus),
                CreatedAt = DateTime.Now
            });
        }

        public static void Reject(int workOrderId, string reason, string operatorName = null)
        {
            Transition(workOrderId, "Rejected");
            ImageItemRepo.ResetProcessStatusByWorkOrder(workOrderId);
            DetectionRepo.DeleteByWorkOrderId(workOrderId);

            ProcessLogRepo.Insert(new ProcessLog
            {
                WorkOrderId = workOrderId,
                Action = "驳回工单，等待重新处理",
                Operator = operatorName,
                RejectReason = reason,
                CreatedAt = DateTime.Now
            });
        }

        public static int CreateDraft(WorkOrder wo)
        {
            wo.Status = "Draft";
            wo.CreatedAt = DateTime.Now;
            wo.UpdatedAt = DateTime.Now;
            return WorkOrderRepo.Insert(wo);
        }

        public static void Submit(int workOrderId)
        {
            Transition(workOrderId, "Submitted");
        }

        public static void StartProcessing(int workOrderId)
        {
            Transition(workOrderId, "Processing");
        }

        public static void CompleteProcessing(int workOrderId)
        {
            Transition(workOrderId, "PendingReview");
        }

        public static void Approve(int workOrderId)
        {
            if (!ImageItemRepo.AllImagesApproved(workOrderId))
                throw new InvalidOperationException("工单内还有图片未审核通过，无法通过工单");
            Transition(workOrderId, "Approved");
        }

        public static void Archive(int workOrderId)
        {
            Transition(workOrderId, "Archived");
        }

        public static void Delete(int workOrderId)
        {
            if (WorkOrderRepo.GetById(workOrderId) == null)
                throw new ArgumentException(string.Format("工单 {0} 不存在", workOrderId));

            WorkOrderRepo.Delete(workOrderId);

            string directory = BatchProcessService.GetWorkOrderBaseDir(workOrderId);
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
