using System.Text.RegularExpressions;

namespace ArkPlotWpf.Data;

internal partial class ArkPlotRegs
{
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
        /// <summary>
        /// 此正则表达式用于匹配特定格式的字符串，其中包含字符、数字和特殊字符的组合。
        /// 格式如下：
        /// - 字符串以非@、#、$字符开头。
        /// - 可选的捕获组2包含一个@、#或$字符，后面跟随捕获组3，它匹配一次或多次小写字母或数字。
        /// - 如果捕获组2未匹配，则尝试匹配#后跟捕获组4匹配一次或多次数字，然后是$后跟捕获组5匹配一次或多次数字。
        /// - 字符串以输入字符串的结束位置结束。
        /// </summary>
        /// <returns>匹配特定格式字符串的正则表达式。</returns>
        [GeneratedRegex(@"^([^@#$]+)(?:([@#$])([a-z\d]+)|#(\d+)\$(\d+))?$", RegexOptions.Compiled)]
        public static partial Regex CharPortraitCodeRegex();

        /// <summary>
        /// 此正则表达式用于匹配不包含方括号的字符串。
        /// </summary>
        /// <returns>匹配不包含方括号的字符串的正则表达式。</returns>
        [GeneratedRegex("^[^\\[].*$", RegexOptions.Compiled)]
        public static partial Regex CommentRegex();

        /// <summary>
        /// 此正则表达式用于匹配包含特定格式的名称字符串。
        /// </summary>
        /// <returns>匹配特定格式名称字符串的正则表达式。</returns>
        [GeneratedRegex("(?<=(\\[name=[\'\"])|(\\[multiline\\(name=[\'\"])).*(?=[\'\"])", RegexOptions.Compiled)]
        public static partial Regex NameRegex();

        /// <summary>
        /// 此正则表达式用于匹配包含特定格式的子名称字符串。
        /// </summary>
        /// <returns>匹配特定格式子名称字符串的正则表达式。</returns>
        [GeneratedRegex("\\[name.*\\]|\\[multiline.*\\]", RegexOptions.Compiled)]
        public static partial Regex RegexToSubName();

        /// <summary>
        /// 此正则表达式用于匹配特定格式的段落字符串。
        /// </summary>
        /// <returns>匹配特定格式段落字符串的正则表达式。</returns>
        [GeneratedRegex("(?<=\\[)[A-Za-z]*(?=\\])", RegexOptions.Compiled)]
        public static partial Regex SegmentRegex();

        /// <summary>
        /// 此正则表达式用于匹配包含特殊标签的字符串。
        /// </summary>
        /// <returns>匹配包含特殊标签字符串的正则表达式。</returns>
        [GeneratedRegex("(?<=(\\[(?!name))).*(?=\\()", RegexOptions.Compiled)]
        public static partial Regex SpecialTagRegex();

        /// <summary>
        /// 此正则表达式用于解析具有特定格式的字符串，例如 key1=value1, key2=value2。
        /// 解析后的对象可以方便地访问和操作其中的键值对。
        /// </summary>
        /// <returns>用于解析特定格式字符串的正则表达式。</returns>
        [GeneratedRegex(@"\s*(.*?)\s*=\s*(?:['""](.*?)['""]|([\w.-]+))\s*,?", RegexOptions.Compiled)]
        public static partial Regex TagParametersRegex();

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
        */
        /// <summary>
        /// 此正则表达式用于解析具有特定格式的字符串，其中包含通用标签。
        /// 解析后的对象可以方便地访问和操作其中的标签和内容。
        /// </summary>
        /// <returns>用于解析特定格式字符串的正则表达式。</returns>
        [GeneratedRegex(@"^\[\s*(?:(.*?)\((.*)\)|(?:([\.\w]*)|(.*?)))\s*\]\s*(.*)", RegexOptions.Compiled)]
        public static partial Regex UniversalTagsRegex();
}