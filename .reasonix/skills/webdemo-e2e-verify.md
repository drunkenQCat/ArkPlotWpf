---
name: webdemo-e2e-verify
description: 复制带数据的 DB → 启动 Blazor WebDemo → 用 Puppeteer 访问 /cache 页面 → 截图验证 → 返回 URL 和检查结论
runAs: subagent
allowed-tools: run_command, run_background, job_output, wait_for_job, stop_job, list_jobs, puppeteer_puppeteer_navigate, puppeteer_puppeteer_screenshot, puppeteer_puppeteer_evaluate, puppeteer_puppeteer_click, puppeteer_puppeteer_fill, read_file, search_content
---
# WebDemo E2E 验证 Skill

按顺序执行以下步骤，**不要并行执行**。启动前先复制带真实数据的 DB，确保页面展示真实缓存状态。

## Step 0: 复制带数据的 DB

WebDemo 默认读的是自己输出目录下的 `arkplot.db`，里面没有缓存数据。
需要把 Avalonia Debug 目录下已迁移的 DB 复制过去：

```bash
copy /Y ArkPlot.Avalonia\bin\Debug\net9.0\arkplot.db ArkPlot.WebDemo\bin\Debug\net9.0\arkplot.db
```

验证复制后的文件大小是否 > 1 MB（确保是有真实数据的 DB）。

## Step 0.5: 确保端口 5000 空闲

上次运行的进程可能还没退出：

```bash
taskkill /F /IM ArkPlot.WebDemo.exe 2>nul
```

忽略 "not found" 错误。

## Step 1: 启动 Blazor Server

```bash
dotnet run --project ArkPlot.WebDemo/ArkPlot.WebDemo.csproj --urls "http://localhost:5000"
```

使用 `run_background` 启动，`waitSec: 8`，等待启动信号。

从启动日志中提取 URL（通常是 `http://localhost:5000`）。记住这个 URL，后续步骤要用。

## Step 2: 验证服务正在运行

用 `job_output` 检查输出是否包含 `listening on` 或 `Now listening on` 字样。
如果没有，用 `wait_for_job` 再等一会（timeoutMs: 10000）。

## Step 3: 用 Puppeteer 访问 `/cache` 页面

调用 `puppeteer_puppeteer_navigate` 访问 `{url}/cache`。

然后用 `puppeteer_puppeteer_evaluate` 执行以下 JS 检查页面内容：

```javascript
// 页面标题
document.title

// 4 个统计卡片的数值
document.querySelector('.card.text-bg-primary h5')?.textContent      // 活动总数
document.querySelector('.card.text-bg-success h5')?.textContent     // 已解析/总数
document.querySelector('.card.text-bg-warning h5')?.textContent     // PRTS 资源
document.querySelector('.card.text-bg-info h5')?.textContent        // 图片描述

// 下拉框是否有选项
document.querySelector('select.form-select')?.options.length

// 章节表格是否存在
document.querySelector('table.table') ? '有' : '无'

// 页面文字快照（前 500 字）
document.body.innerText.substring(0, 500)
```

## Step 4: 截图

调用 `puppeteer_puppeteer_screenshot` 截取全页截图，name=`cache-page`。

## Step 5: 验证关键指标

四大断言：

1. **活动总数卡片** (`text-bg-primary`) — 数值应当 > 0（Avalonia DB 有 461 个活动）
2. **已解析章节卡片** (`text-bg-success`) — 数值应当 > 0（有 4 个已解析 Plot）
3. **PRTS 资源卡片** (`text-bg-warning`) — 数值应当 > 1000（实际 17091）
4. **章节表格** (`table.table`) — 应当出现，且行数 > 0

如果以上任意一项不满足，记录到 errors 数组中并说明期望值和实际值。

## Step 6: 尝试点击"查看"按钮

如果表格中有 `btn-outline-primary` 的"查看"按钮，用 `puppeteer_puppeteer_click` 点击第一个。
等 1 秒后用 `puppeteer_puppeteer_evaluate` 检查详情面板是否展开（找 `.card.border-primary`）。

## Step 7: 返回结果

返回一个 JSON 格式的报告：

```json
{
  "url": "http://localhost:5000",
  "db_copied": true,
  "db_size_mb": 4.9,
  "status": "passed",
  "errors": [],
  "stats_found": {
    "acts": "461",
    "parsed": "4 / 1937",
    "prts_resources": "17091",
    "pic_descriptions": "0"
  },
  "has_table": true,
  "table_rows": 7,
  "has_detail_panel": true|false,
  "screenshot": "cache-page"
}
```

如果任何断言失败，`status` 设为 `"failed"`，并在 `errors` 中写明具体哪一项不符合预期。

**不要关闭服务器** — 父进程会处理。

## 注意事项

1. 所有 URL 使用 HTTP 而非 HTTPS
2. 如果服务器启动失败，重试一次
3. 不要抛异常退出 — 所有失败都记录到 errors 中
4. 每个步骤完成后打印简短的进度信息
