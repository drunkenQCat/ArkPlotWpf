using NAudio.Wave;

namespace ArkPlot.Tts;

/// <summary>
/// MP3 音频合并器。
/// 使用 Mp3FileReader 逐帧流式读取，内存恒定。自动跳过 Xing/VBRI 头帧。
/// </summary>
public static class AudioMerger
{
    /// <summary>
    /// 合并多个 MP3 文件为单个 MP3 文件。
    /// </summary>
    /// <param name="inputFiles">输入 MP3 文件列表（按顺序）。</param>
    /// <param name="outputFile">输出 MP3 文件路径。</param>
    public static void MergeFiles(IReadOnlyList<string> inputFiles, string outputFile)
    {
        if (inputFiles == null || inputFiles.Count == 0)
            throw new ArgumentException("输入文件列表不能为空", nameof(inputFiles));

        var dir = Path.GetDirectoryName(outputFile);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write);

        foreach (var inputPath in inputFiles)
        {
            using var reader = new Mp3FileReader(inputPath);

            Mp3Frame? frame;
            while ((frame = reader.ReadNextFrame()) != null)
            {
                if (IsXingOrVbriFrame(frame))
                    continue;

                outputStream.Write(frame.RawData, 0, frame.RawData.Length);
            }
        }
    }

    /// <summary>
    /// 计算合并后 MP3 的帧数（不实际合并，仅统计）。
    /// 跳过 Xing/VBRI 头帧。
    /// </summary>
    public static int CountFrames(string filePath)
    {
        var count = 0;
        using var reader = new Mp3FileReader(filePath);
        Mp3Frame? frame;
        while ((frame = reader.ReadNextFrame()) != null)
        {
            if (!IsXingOrVbriFrame(frame))
                count++;
        }
        return count;
    }

    /// <summary>
    /// 判断是否为 Xing/VBRI 头帧。
    /// Xing 和 Info 是 LAME 编码器的 VBR/CBR 标签，VBRI 是 Fraunhofer 的 VBR 标签。
    /// </summary>
    internal static bool IsXingOrVbriFrame(Mp3Frame frame)
    {
        var data = frame.RawData;
        if (data.Length < 40)
            return false;

        int xingOffset = frame.MpegVersion == MpegVersion.Version1
            ? (frame.ChannelMode == ChannelMode.Mono ? 17 : 32)
            : (frame.ChannelMode == ChannelMode.Mono ? 9 : 17);

        if (xingOffset + 4 <= data.Length)
        {
            if (data[xingOffset] == 'X' && data[xingOffset + 1] == 'i' &&
                data[xingOffset + 2] == 'n' && data[xingOffset + 3] == 'g')
                return true;
            if (data[xingOffset] == 'I' && data[xingOffset + 1] == 'n' &&
                data[xingOffset + 2] == 'f' && data[xingOffset + 3] == 'o')
                return true;
        }

        if (data.Length >= 40 &&
            data[36] == 'V' && data[37] == 'B' && data[38] == 'R' && data[39] == 'I')
            return true;

        return false;
    }
}
