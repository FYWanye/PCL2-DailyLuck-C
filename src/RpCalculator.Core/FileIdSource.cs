namespace RpCalculator.Core;

/// <summary>
/// 从文本文件流式读取识别码，每行一个。
/// 使用 File.ReadLines 惰性枚举，不会把整个文件加载到内存。
/// </summary>
public static class FileIdSource
{
    public static IEnumerable<string> ReadLines(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("文件路径不能为空。", nameof(path));
        }

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length > 0)
            {
                yield return line;
            }
        }
    }
}
