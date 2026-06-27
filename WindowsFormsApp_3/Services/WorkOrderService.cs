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
            // 始终以数据库中的最新状态作为判断依据，避免界面缓存状态与数据库不一致。
            var wo = WorkOrderRepo.GetById(workOrderId);
            if (wo == null)
                throw new ArgumentException(string.Format("工单 {0} 不存在", workOrderId));

            // 状态机白名单校验：只有Transitions中明确声明的跳转才允许执行。
            // 例如Draft不能直接变为Approved，Archived也不能再次进入处理流程。
            if (!Transitions.ContainsKey(wo.Status) ||
                !Transitions[wo.Status].Contains(newStatus))
            {
                throw new InvalidOperationException(
                    string.Format("非法状态跳转: {0} → {1}", wo.Status, newStatus));
            }

            // 校验通过后先持久化新状态，再写入一条可追踪的状态变更日志。
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
            // 工单通过以数据库中的逐图审核结果为准，不能只依赖界面成功提示。
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
