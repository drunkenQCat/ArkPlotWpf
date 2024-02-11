
using System.Text.RegularExpressions;

namespace ArkPlotWpf.Model;

public partial class PlotRegs
{
    [GeneratedRegex("(?<=(\\[name=[\'\"])|(\\[multiline\\(name=[\'\"])).*(?=[\'\"])", RegexOptions.Compiled)]
    private static partial Regex NameRegex();

    [GeneratedRegex("\\[name.*\\]|\\[multiline.*\\]", RegexOptions.Compiled)]
    private static partial Regex RegexToSubName();

    [GeneratedRegex("(?<=\\[)[A-Za-z]*(?=\\])", RegexOptions.Compiled)]
    private static partial Regex SegmentRegex();

    [GeneratedRegex("^[^\\[].*$", RegexOptions.Compiled)]
    private static partial Regex CommentRegex();

    [GeneratedRegex("(?<=(\\[(?!name))).*(?=\\()", RegexOptions.Compiled)]
    private static partial Regex SpecialTagRegex();
    /// <summary>
    /*
    ^：匹配输入字符串的开始位置。
    ([^@#$]+)：捕获组1，匹配一次或多次任何不是@、#、$的字符。
    (?: ... )?：非捕获组，后面跟随?表示这个组是可选的。
    ([@#$])：捕获组2，匹配一个@、#或$字符。
    ([a-z\d]+)：捕获组3，匹配一次或多次小写字母或数字。
    |：逻辑“或”操作符，表示匹配前面或后面的模式。
    #(\d+)\$(\d+)：如果前面的模式没有匹配，尝试匹配这个模式。
    #：匹配字符#。
    (\d+)：捕获组4，匹配一次或多次数字。
    \$：匹配字符$。
    (\d+)：捕获组5，匹配一次或多次数字。
    $：匹配输入字符串的结束位置。
     */
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"^([^@#$]+)(?:([@#$])([a-z\d]+)|#(\d+)\$(\d+))?$", RegexOptions.Compiled)]
    private static partial Regex CharPortraitCodeRegex();
    /// <summary>
    /*
        ### 正则表达式解释

        - `^\[\s*`：从字符串的开始处匹配一个左方括号`[`，后面跟随零个或多个空白字符。
        - `(?: ... | ... )`：一个非捕获组，包含两部分，用`|`分隔，表示匹配左边或右边的模式。
          - `(.*?)\((.*)\)`：第一部分，尝试捕获两个组：
            - `(.*?)`：捕获组1，非贪婪地匹配任意字符，直到遇到下一个模式。
            - `\((.*)\)`：匹配一对圆括号内的内容，圆括号内的任意字符被捕获为组2。
          - `([\\.|\\w]*)|(.*?)`：第二部分，又是两个选择：
            - `([\\.|\\w]*)`：捕获组3，匹配零个或多个字母、数字、下划线或点字符。
            - `(.*?)`：捕获组4，非贪婪地匹配任意字符。
        - `\\s*\\]\\s*`：匹配零个或多个空白字符，后面跟一个右方括号`]`，再后面是零个或多个空白字符。
        - `(.*)`：捕获组5，贪婪地匹配剩余的任意字符直到字符串结束。
这段正则是从prts摘抄下来的。它对于
"[HEADER(key="title_test", is_skippable=true, fit_mode="BLACK_MASK")] 古米 习惯"
这句的识别结果是：
[
    "[HEADER(key=\"title_test\", is_skippable=true, fit_mode=\"BLACK_MASK\")] 古米 习惯",
    "HEADER",
    "key=\"title_test\", is_skippable=true, fit_mode=\"BLACK_MASK\"",
    null,
    null,
    "古米 习惯"
]
    */ 

    ///</summary>
    [GeneratedRegex(@"^\[\s*(?:(.*?)\((.*)\)|(?:([\.\w]*)|(.*?)))\s*\]\s*(.*)", RegexOptions.Compiled)]
    private static partial Regex UniversalTagsRegex();
    /// <summary>
        /*
        然后第三个元素有以下解析方式：
        ```
String.prototype.toObject = function (sep1 = ",", sep2 = "=", tolower = true) {
    var regStr = `\\s*(.*?)\\s*${sep2}\\s*(?:[\'"](.*?)[\'"]|([\\w.-]+))\\s*${sep1}?`;
    var reg = new RegExp(regStr, 'g');
    var ms = this.matchAll(reg);
    var o = {};
    for (var m of ms) {
        var p = m[1], v = m[2] === undefined ? m[3] : m[2];
        if (tolower) p = p.toLowerCase();
        o[p] = v;
    }
    if (Object.keys(o).length == 0) {
        var m = this.match(regStr);
        if (m) {
            var p = m[1], v = m[2] === undefined ? m[3] : m[2];
            if (tolower) p = p.toLowerCase();
            o[p] = v;
        }
    }
    return o;
}
        ```
        之后也许可以用作新的解析方式。
         */
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"\s*(.*?)\s*=\s*(?:['""](.*?)['""]|([\w.-]+))\s*,?", RegexOptions.Compiled)]
    private static partial Regex TagParametersRegex();
}
