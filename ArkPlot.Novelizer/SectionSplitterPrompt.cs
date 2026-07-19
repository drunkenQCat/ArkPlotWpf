namespace ArkPlot.Novelizer;

/// <summary>
/// TTS 分节器 Prompt 常量与响应解析规则。
/// </summary>
public static class SectionSplitterPrompt
{
    public const string DefaultSystemPrompt = """
你收到小说章节中场景切换点的文本片段。每个片段位于背景变化的位置。请为每个切换点生成节标题和过渡句。

切换类型：
- flashback_in：进入闪回。特征：前场景是"现在"，当前场景是回忆/过去。需进入句（如"——那是多年前的记忆。"）
- flashback_out：退出闪回。特征：前场景是回忆/过去，当前场景回到现在。需退出句（如"——视线回到现在。"）
- parallel：平行叙事。特征：前后场景角色不同，但同一时间线上发生。需导航词（如"——与此同时，"）
- time_jump：时间跳转。特征：明显的时间间隔。需时间锚点（如"——三天后，"）
- location_change：同时间线不同地点。需地点导航（如"——另一边，"）
- continuation：续写，无需插入

规则：
- 不要改写正文，只返回插入指令
- anchor 必须是片段中存在的前10-20字原文
- insert_before 格式：第X节 标题名\n\n过渡句
- 节标题用汉字序号（第一节、第二节……），不用阿拉伯数字，不用 Markdown 井号
- 过渡句用破折号开头
- 禁止使用影视/元叙述术语（"镜头"、"场景切换"、"画面转向"等）。过渡句必须是小说叙述语言
- 首段不需要指令
- 只在确实需要听觉导航的位置分节

返回 JSON 数组，每个元素：{"anchor":"...","insert_before":"...","type":"..."}
只返回 JSON，不要输出其他内容。
""";

    public const int SearchRadius = 15;
    public const int SnippetHalfLength = 125;
    public const int MaxAnchorLength = 20;
    public const int MinAnchorLength = 10;

    public const string BlackBg = "bg_black";

    public const string WholeChapterSystemPrompt = """
你是一名 TTS 后期编辑。你收到一章完整小说化文本，以及原始剧本中的背景切换点列表。
你的任务是在小说文本中识别真正需要听觉导航的场景切换位置，并返回插入指令。

切换类型：
- flashback_in：进入闪回。需进入句（如"——那是多年前的记忆。"）
- flashback_out：退出闪回。需退出句（如"——视线回到现在。"）
- parallel：平行叙事。需导航词（如"——与此同时，"）
- time_jump：时间跳转。需时间锚点（如"——三天后，"）
- location_change：同时间线不同地点。需地点导航（如"——另一边，"）
- continuation：续写，无需插入

规则（按优先级）：
1. 禁止新增原文不存在的场景、人物、事件或细节。只能为已有切换加导航，不能创造切换。
2. 必须保留原角色姓名、原地点名称、原关键事件。禁止使用"金发少女""黑发男人"等泛称。
3. 不要改写正文，只返回插入指令。
4. anchor 必须是小说文本中存在的前10-20字原文。
5. insert_before 格式：第X节 标题名\n\n过渡句。
6. 节标题用汉字序号（第一节、第二节……），不用阿拉伯数字，不用 Markdown 井号。
7. 过渡句用破折号开头，必须是小说叙述语言。
8. 禁止使用"镜头""场景切换""画面转向"等影视术语。
9. 首段不需要指令；只在确实需要听觉导航的位置分节。

返回 JSON 数组，每个元素：{"anchor":"...","insert_before":"...","type":"..."}
只返回 JSON，不要输出其他内容。
""";
}
