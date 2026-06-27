# 图像批处理工单审核系统

Windows 程序设计课程期末作业 — C# WinForms + SQLite 实现。

## 功能

创建工单 → 导入图片 → 批处理执行（换底色/统一尺寸/格式转换）→ 质量检测（模糊度/分辨率/偏色）→ 逐张人工审核 → ZIP 导出归档

## 技术栈

- C# / .NET Framework 4.8 / Windows Forms
- SQLite + ADO.NET
- GDI+ / System.Drawing 图像处理
- Claude Code 辅助开发（RDD + TDD）

## 项目结构

```
WindowsFormsApp_3/
├── Models/          # 实体类
├── Services/        # 业务逻辑层
├── Repositories/    # 数据访问层
├── Forms/           # UI 层
├── Utils/           # 工具类（含换底算法）
├── libs/            # SQLite 依赖
└── test_evidence/   # 测试截图
```

## 运行

```bash
git clone https://github.com/用户名/ImageBatchSystem
```
用 Visual Studio 2022 打开 `ImageBatchSystem.sln`，编译运行即可。SQLite 依赖已随源码提供在 `libs/` 中，无需额外安装。

## 文档

- [项目方案文档](Project_Proposal.md)
- [测试报告](Testing_Report.md)
- [AI 生成代码人机审核报告](AI生成代码人机审核报告.html)
