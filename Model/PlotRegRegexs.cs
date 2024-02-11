
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
    ^��ƥ�������ַ����Ŀ�ʼλ�á�
    ([^@#$]+)��������1��ƥ��һ�λ����κβ���@��#��$���ַ���
    (?: ... )?���ǲ����飬�������?��ʾ������ǿ�ѡ�ġ�
    ([@#$])��������2��ƥ��һ��@��#��$�ַ���
    ([a-z\d]+)��������3��ƥ��һ�λ���Сд��ĸ�����֡�
    |���߼����򡱲���������ʾƥ��ǰ�������ģʽ��
    #(\d+)\$(\d+)�����ǰ���ģʽû��ƥ�䣬����ƥ�����ģʽ��
    #��ƥ���ַ�#��
    (\d+)��������4��ƥ��һ�λ������֡�
    \$��ƥ���ַ�$��
    (\d+)��������5��ƥ��һ�λ������֡�
    $��ƥ�������ַ����Ľ���λ�á�
     */
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"^([^@#$]+)(?:([@#$])([a-z\d]+)|#(\d+)\$(\d+))?$", RegexOptions.Compiled)]
    private static partial Regex CharPortraitCodeRegex();
    /// <summary>
    /*
        ### ������ʽ����

        - `^\[\s*`�����ַ����Ŀ�ʼ��ƥ��һ��������`[`�����������������հ��ַ���
        - `(?: ... | ... )`��һ���ǲ����飬���������֣���`|`�ָ�����ʾƥ����߻��ұߵ�ģʽ��
          - `(.*?)\((.*)\)`����һ���֣����Բ��������飺
            - `(.*?)`��������1����̰����ƥ�������ַ���ֱ��������һ��ģʽ��
            - `\((.*)\)`��ƥ��һ��Բ�����ڵ����ݣ�Բ�����ڵ������ַ�������Ϊ��2��
          - `([\\.|\\w]*)|(.*?)`���ڶ����֣���������ѡ��
            - `([\\.|\\w]*)`��������3��ƥ�����������ĸ�����֡��»��߻���ַ���
            - `(.*?)`��������4����̰����ƥ�������ַ���
        - `\\s*\\]\\s*`��ƥ����������հ��ַ��������һ���ҷ�����`]`���ٺ�������������հ��ַ���
        - `(.*)`��������5��̰����ƥ��ʣ��������ַ�ֱ���ַ���������
��������Ǵ�prtsժ�������ġ�������
"[HEADER(key="title_test", is_skippable=true, fit_mode="BLACK_MASK")] ���� ϰ��"
����ʶ�����ǣ�
[
    "[HEADER(key=\"title_test\", is_skippable=true, fit_mode=\"BLACK_MASK\")] ���� ϰ��",
    "HEADER",
    "key=\"title_test\", is_skippable=true, fit_mode=\"BLACK_MASK\"",
    null,
    null,
    "���� ϰ��"
]
    */ 

    ///</summary>
    [GeneratedRegex(@"^\[\s*(?:(.*?)\((.*)\)|(?:([\.\w]*)|(.*?)))\s*\]\s*(.*)", RegexOptions.Compiled)]
    private static partial Regex UniversalTagsRegex();
    /// <summary>
        /*
        Ȼ�������Ԫ�������½�����ʽ��
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
        ֮��Ҳ����������µĽ�����ʽ��
         */
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"\s*(.*?)\s*=\s*(?:['""](.*?)['""]|([\w.-]+))\s*,?", RegexOptions.Compiled)]
    private static partial Regex TagParametersRegex();
}
