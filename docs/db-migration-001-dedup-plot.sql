-- ========================================================
-- Migration 001: Plot 表去重 + 补全 StoryChapterId
-- 
-- 背景：同一章节因 Pipeline 两次 INSERT（Status=1 再 Status=2）
--       导致重复记录。且 Act#57 的 Plot 缺少 StoryChapterId。
--
-- 操作：
--   1. 删除 Status=1 的 Plot (以及其 FormattedTextEntry)
--   2. 为 Act#57 的 Plot 补全 StoryChapterId
--   3. 建唯一索引 uk_plot_act_chapter
-- ========================================================

-- === 执行前状态 ===
-- Plot 表: 8 行，其中 Status=1 ×4, Status=2 ×4
--   Plot#1 Act#57 StoryChapterId=NULL Status=1 " 无续集 幕间"
--   Plot#2 Act#57 StoryChapterId=NULL Status=1 " 荒野路漫漫 幕间"
--   Plot#3 Act#57 StoryChapterId=NULL Status=2 " 无续集 幕间"
--   Plot#4 Act#57 StoryChapterId=NULL Status=2 " 荒野路漫漫 幕间"
--   Plot#5 Act#52 StoryChapterId=1010 Status=1 " 破局者 幕间"
--   Plot#6 Act#52 StoryChapterId=1009 Status=1 " 无名氏的战争 幕间"
--   Plot#7 Act#52 StoryChapterId=1009 Status=2 " 无名氏的战争 幕间"
--   Plot#8 Act#52 StoryChapterId=1010 Status=2 " 破局者 幕间"
-- FormattedTextEntry: 4930 行（Status=1 有 2465 行冗余原始线数据）

BEGIN TRANSACTION;

-- Step 1: 删除 Status=1 的 FormattedTextEntry
-- 删除 2465 行（Plot#1/2/5/6 的原始行分割数据，不含已解析内容）
DELETE FROM FormattedTextEntry
WHERE PlotId IN (
    SELECT Id FROM Plot WHERE Status = 1
);

-- Step 2: 删除 Status=1 的 Plot 记录
-- 删除 4 行
DELETE FROM Plot WHERE Status = 1;

-- Step 3: 为 Plot 补全 StoryChapterId
-- 匹配逻辑：TRIM(Title) == TRIM(StoryCode || ' ' || StoryName || ' ' || AvgTag)
-- 更新 2 行：Plot#3 → Ch#1039, Plot#4 → Ch#1040
UPDATE Plot
SET StoryChapterId = (
    SELECT sc.Id FROM StoryChapters sc
    WHERE sc.ActId = Plot.ActId
      AND TRIM(sc.StoryCode || ' ' || sc.StoryName || ' ' || COALESCE(sc.AvgTag, '')) = TRIM(Plot.Title)
)
WHERE Plot.StoryChapterId IS NULL
  AND EXISTS (
    SELECT 1 FROM StoryChapters sc
    WHERE sc.ActId = Plot.ActId
      AND TRIM(sc.StoryCode || ' ' || sc.StoryName || ' ' || COALESCE(sc.AvgTag, '')) = TRIM(Plot.Title)
);

COMMIT;

-- === 执行后状态 ===
-- Plot 表: 4 行，全为 Status=2
--   Plot#3 Act#57 StoryChapterId=1039 Status=2 " 无续集 幕间"
--   Plot#4 Act#57 StoryChapterId=1040 Status=2 " 荒野路漫漫 幕间"
--   Plot#7 Act#52 StoryChapterId=1009 Status=2 " 无名氏的战争 幕间"
--   Plot#8 Act#52 StoryChapterId=1010 Status=2 " 破局者 幕间"
-- FormattedTextEntry: 2465 行（全部为已解析数据）
-- NULL StoryChapterId: 0

-- Step 4: 建唯一索引（走代码 DbFactory 或用以下 SQL）
-- CREATE UNIQUE INDEX IF NOT EXISTS uk_plot_act_chapter
--     ON Plot(ActId, StoryChapterId);
