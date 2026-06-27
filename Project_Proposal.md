# 图像批处理工单审核系统 — 项目方案文档

## 一、项目背景

在日常图像处理工作中，经常需要对大量图片执行批处理操作（换底色、统一尺寸、格式转换等），并对处理结果进行质量把关。传统做法是手动逐张操作、用肉眼检查质量，效率低下且缺乏统一的流程管理。

本系统将"批处理任务"纳入工单化的业务流程，建立"提交→处理→审核→归档"的闭环，由系统自动执行质量检测并给出参考分数，人工审核做出最终通过/驳回决策。

---

## 二、功能需求分析

### 2.1 业务流程图

```
创建工单 → 导入图片 → 批处理执行 → 质量检测 → 人工审核 → 通过 → 导出归档
                ↑                                          ↓
                └────────── 驳回，重返处理 ←────────────────┘
```

### 2.2 工单状态机

| 状态 | 含义 | 可跳转至 |
|------|------|----------|
| 草稿 | 工单已创建但未提交 | 已提交 |
| 已提交 | 等待执行批处理 | 处理中 |
| 处理中 | 正在执行或已完成批处理 | 待审核、已驳回 |
| 待审核 | 批处理完成，等待人工审核 | 已通过、已驳回 |
| 已通过 | 审核通过，可归档 | 已归档 |
| 已驳回 | 审核不通过，需重新处理 | 处理中 |
| 已归档 | 终态，已完成导出 | — |

### 2.3 处理操作配置（工单级）

创建工单时可勾选以下处理项，工单内所有图片统一执行：

| 处理项 | 说明 | 可选参数 |
|--------|------|----------|
| 换底色 | 基于 HSV 色彩空间的背景替换 | 蓝底/红底/白底/自定义 RGB |
| 统一尺寸 | 批量缩放至指定分辨率 | 目标宽 × 高（像素） |
| 格式转换 | 批量转换图片格式 | JPEG / PNG / BMP |
| 质量检测 | 所有工单自动执行，不关闭 | 模糊度 + 分辨率 + 偏色 |

### 2.4 质量检测项

| 检测项 | 方法 | 阈值说明 |
|--------|------|----------|
| 模糊度 | 拉普拉斯方差（Laplacian Variance） | 低于阈值标记模糊 |
| 分辨率 | 读取图像宽高，与工单目标尺寸对比 | 不达标标记 |
| 偏色 | RGB 直方图通道均值偏差分析 | 某通道偏差过大标记偏色 |

检测结果汇总为"建议通过/建议驳回"，供人工审核参考。

### 2.5 审核模块

- 审核界面左右对照展示：原图 | 处理后图
- 每张图下方显示三项检测分数及系统建议
- 审核方式为**逐张审核**：审核人员逐张查看原图、处理后图和检测指标，对每张图片分别执行"通过"或"驳回"操作；只有工单内全部图片均通过后，工单状态才更新为"已通过"。驳回时需要填写驳回原因，工单进入"已驳回"状态，并可重新进入处理流程

### 2.6 导出归档

审核通过后，一键导出为 ZIP 包：

```
WorkOrder_xxx.zip
├── original/              # 原始图片
├── processed/             # 处理后的图片
├── detection_report.csv   # 检测报告
└── process_log.txt        # 操作日志
```

---

## 三、技术选型理由

### 3.1 语言与框架

| 技术 | 选型 | 理由 |
|------|------|------|
| 语言 | C# (.NET Framework 4.8) | 课程指定语言，WinForms 原生支持 |
| UI 框架 | Windows Forms | 课程核心教学内容，控件体系成熟 |
| 数据库 | SQLite | 免安装、零配置、单文件，适合桌面小系统 |
| ORM | ADO.NET（直接 SQL） | 课程考核点，避免引入 EF 增加复杂度 |
| 图像处理 | 纯 GDI+ / System.Drawing | 课程多媒体处理知识点，无第三方依赖 |
| 测试方式 | 独立 C# 测试驱动 + 手工回归测试 | 直接调用业务层并核验 SQLite、文件系统和 ZIP 结果，适合本项目的集成式 TDD 验证 |


### 3.2 技术覆盖对照

| 课程技术点 | 系统对应模块 |
|------------|-------------|
| WinForm 基本控件 | DataGridView 工单列表、TabControl 流程切换、ListBox 图片列表、PictureBox 图片预览 |
| 对话框与交互设计 | OpenFileDialog 批量导入、MessageBox 反馈结果 |
| 文件操作 | 批量导入复制、ZIP 打包导出、检测报告 CSV 写入、操作日志 TXT 导出 |
| 多媒体处理 | 图像换底、缩放、格式转换、缩略图生成 |
| 图像处理 | 拉普拉斯模糊检测、直方图偏色分析、分辨率检测 |
| 数据库 | SQLite CRUD、ADO.NET 参数化查询、事务处理 |
| 布局与导航 | TabControl 分页、StatusStrip 状态栏、Panel 布局 |

### 3.3 复用已有成果

本项目复用「证件照换底工具」中的 `BackgroundChanger` 静态方法，基于 HSV 色彩空间的四角采样背景检测与双阈值边缘过渡算法，作为工单"换底色"处理项的核心实现。算法改为参数化调用（输入 Bitmap + 目标颜色 → 输出结果图），UI 层在整个新系统中重写。

---

## 四、系统架构设计

### 4.1 项目目录结构

```
ImageBatchSystem/
├── Models/                # 实体类
│   ├── WorkOrder.cs       # 工单实体
│   ├── ImageItem.cs       # 图片项实体
│   ├── DetectionResult.cs # 检测结果实体
│   └── ProcessLog.cs      # 处理日志实体
├── Services/              # 业务逻辑层
│   ├── WorkOrderService.cs      # 工单状态管理
│   ├── DetectionService.cs      # 质量检测算法
│   ├── BatchProcessService.cs   # 批处理执行
│   └── ExportService.cs         # ZIP 导出
├── Repositories/          # 数据访问层
│   ├── DbContext.cs         # SQLite 连接管理
│   ├── WorkOrderRepo.cs     # 工单 CRUD
│   ├── ImageItemRepo.cs     # 图片 CRUD
│   ├── DetectionRepo.cs     # 检测结果 CRUD
│   └── ProcessLogRepo.cs    # 操作日志 CRUD
├── Forms/
│   └── MainForm.cs          # 主窗体；代码创建五个业务页签及事件处理
├── Theme.cs                 # 全局配色、字体与控件样式
├── Utils/
│   └── BackgroundChanger.cs  # 换底算法（复用）
├── libs/
│   ├── System.Data.SQLite.dll
│   ├── x64/SQLite.Interop.dll
│   └── x86/SQLite.Interop.dll
├── Program.cs
├── ImageBatchSystem.csproj
├── ImageBatchSystem.sln
└── App.config
```

### 4.1.1 图片文件存储目录

系统运行时，所有图片文件存储在应用程序数据目录下，按工单 ID 分子目录管理：

```
AppData/
├── database.db                 # SQLite 数据库文件
└── WorkOrders/
    └── {WorkOrderId}/
        ├── original/           # 导入的原始图片
        ├── processed/          # 批处理后的图片
        ├── thumbnails/         # 缩略图（审核界面预览用）
        └── WorkOrder_{Id}.zip  # 审核通过后生成的归档文件
```

- `original/` — 工单创建时导入的原始图片，不受后续处理影响
- `processed/` — 批处理产出的图片（换底后、缩放后或格式转换后）
- `thumbnails/` — 审核界面左右对照展示用的缩略图
- `WorkOrder_{Id}.zip` — 由导出服务直接生成，包含原图、处理图、检测报告和操作日志

数据库文件 `database.db` 与 `WorkOrders/` 平级存放，便于备份和迁移。

### 4.2 分层架构图

```
┌─────────────────────────────────────┐
│           UI 层 (Forms/)            │
│   TabControl → 5 个 Tab 页签        │
├─────────────────────────────────────┤
│      业务逻辑层 (Services/)          │
│  检测/批处理/导出/状态管理            │
├─────────────────────────────────────┤
│      数据访问层 (Repositories/)       │
│  ADO.NET → SQLite CRUD             │
├─────────────────────────────────────┤
│      数据层 (SQLite 数据库)          │
│  WorkOrders | Images | Results      │
└─────────────────────────────────────┘
```

### 4.3 数据库表设计

**WorkOrders（工单表）**

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | INTEGER PK | 自增主键 |
| Title | TEXT NOT NULL | 工单标题 |
| Status | TEXT NOT NULL | 状态（Draft/Submitted/Processing/PendingReview/Approved/Rejected/Archived） |
| ProcessOptions | TEXT | JSON：勾选的处理项及参数 |
| TargetWidth | INTEGER | 目标尺寸宽（缩放用） |
| TargetHeight | INTEGER | 目标尺寸高 |
| TargetFormat | TEXT | 目标格式（JPG/PNG/BMP） |
| CreatedAt | DATETIME | 创建时间 |
| UpdatedAt | DATETIME | 最后更新时间 |

**Images（图片表）**

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | INTEGER PK | 自增主键 |
| WorkOrderId | INTEGER FK | 所属工单 |
| FileName | TEXT NOT NULL | 原始文件名 |
| OriginalPath | TEXT | 导入后存储路径 |
| ProcessedPath | TEXT | 处理后存储路径 |
| ProcessStatus | TEXT | 处理状态（Pending/Processing/Done/Failed） |
| ReviewStatus | TEXT | 审核状态（Pending/Approved/Rejected） |
| ReviewComment | TEXT | 审核意见或驳回原因 |
| ReviewedAt | DATETIME | 审核时间 |

**DetectionResults（检测结果表）**

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | INTEGER PK | 自增主键 |
| ImageId | INTEGER FK | 关联图片 |
| BlurScore | REAL | 模糊度分数 |
| BlurPassed | INTEGER | 模糊检测是否通过 |
| ResolutionW | INTEGER | 实际宽度 |
| ResolutionH | INTEGER | 实际高度 |
| ResPassed | INTEGER | 分辨率是否达标 |
| ColorBiasR | REAL | 红色通道均值偏差 |
| ColorBiasG | REAL | 绿色通道均值偏差 |
| ColorBiasB | REAL | 蓝色通道均值偏差 |
| ColorPassed | INTEGER | 偏色检测是否通过 |
| SuggestPass | INTEGER | 系统建议（0=建议驳回 1=建议通过） |
| DetectedAt | DATETIME | 检测时间 |

**ProcessLogs（操作日志表）**

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | INTEGER PK | 自增主键 |
| WorkOrderId | INTEGER FK | 关联工单 |
| ImageId | INTEGER FK | 关联图片，可为空；工单级操作时为空 |
| Action | TEXT NOT NULL | 操作描述 |
| Operator | TEXT | 操作人（人工审核填） |
| RejectReason | TEXT | 驳回原因 |
| CreatedAt | DATETIME | 操作时间 |

### 4.4 UI 导航结构

```
MainForm
└── TabControl
    ├── TabPage 1: 工单列表
    │   ├── DataGridView（工单列表）
    │   ├── ToolStrip（新建/查看/删除按钮）
    │   └── StatusStrip（状态栏）
    ├── TabPage 2: 创建工单
    │   ├── TextBox（工单标题）
    │   ├── CheckBox 组（处理项勾选）
    │   ├── DataGridView（已导入图片列表）
    │   ├── Button（导入图片/提交工单）
    │   └── 换底色参数面板（颜色选择，仅勾选时显示）
    ├── TabPage 3: 处理执行
    │   ├── ComboBox（选择工单）
    │   ├── DataGridView（图片列表+处理状态）
    │   ├── ProgressBar（批处理进度）
    │   └── Button（开始处理）
    ├── TabPage 4: 审核
    │   ├── ComboBox（选择工单）
    │   ├── ListBox（该工单图片列表）
    │   ├── PictureBox × 2（左：原图 | 右：处理后）
    │   ├── DataGridView（三项检测分数 + 系统建议）
    │   └── Button（通过/驳回 + 驳回原因输入）
    └── TabPage 5: 导出归档
        ├── DataGridView（已通过未归档工单）
        └── Button（一键导出 ZIP）
```

---

## 五、开发环境说明

| 项目 | 版本/工具 |
|------|-----------|
| 操作系统 | Windows 11 |
| 开发工具 | Visual Studio 2022 |
| .NET 版本 | .NET Framework 4.8 |
| 语言 | C# 7.3+ |
| 数据库 | SQLite 3.x（项目 `libs/` 中随源码提供 System.Data.SQLite） |
| 版本控制 | Git |
| AI 辅助工具 | Claude Code（RDD + TDD 开发模式） |
| 外部依赖 | `libs/` 中自包含 System.Data.SQLite + SQLite.Interop，无需额外 NuGet 还原 |

---

## 六、项目 Clone 地址

https://github.com/Ljl-change/windows_end.git

---

## 七、参考资料

- .NET Framework 文档：https://docs.microsoft.com/zh-cn/dotnet/framework/
- SQLite 官方文档：https://www.sqlite.org/docs.html
- System.Drawing 命名空间：https://docs.microsoft.com/zh-cn/dotnet/api/system.drawing
