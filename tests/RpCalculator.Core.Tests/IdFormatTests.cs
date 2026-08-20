using RpCalculator.Core;

namespace RpCalculator.Core.Tests;

/// <summary>
/// 识别码固定格式（16 位大写十六进制 4-4-4-4）的规范化与无效排除测试。
///
/// 核心规则：去掉短横线后以 '0' 开头的识别码会被另一个应用规范化成不同识别码，
/// 因此首字符为 '0' 一律视为无效；长度不为 16 或含非法字符也视为无效。
/// </summary>
public sealed class IdFormatTests
{
    [Theory]
    // 需求示例：去掉横线后以 '0' 开头 → 无效。
    [InlineData("0123-4567-890A-BCDE")]
    [InlineData("0000-0000-1234-5678")]
    // 长度不正确。
    [InlineData("ABCD-EF01-2345")]
    [InlineData("ABCDEF01234567890")]
    // 含非法字符。
    [InlineData("ABCD-EF01-2345-678G")]
    [InlineData("ABCD-EF01-2345-678!")]
    [InlineData("ABCD_EF01_2345_6789")]
    // 空白。
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryNormalize_RejectsInvalidInputs(string? raw)
    {
        Assert.False(IdFormat.TryNormalize(raw, out var id));
        Assert.Equal(string.Empty, id);
    }

    [Theory]
    [InlineData("7123-4567-890A-BCDE", "7123-4567-890A-BCDE")]
    [InlineData("abcd-ef01-2345-6789", "ABCD-EF01-2345-6789")]  // 小写转大写
    [InlineData("  ABCD-EF01-2345-6789  ", "ABCD-EF01-2345-6789")]  // 去首尾空格
    [InlineData("ABCDEF0123456789", "ABCD-EF01-2345-6789")]  // 无横线补格式
    [InlineData("aBcD-eF01-2345-6789", "ABCD-EF01-2345-6789")]  // 混合大小写
    public void TryNormalize_AcceptsAndFormats(string raw, string expected)
    {
        Assert.True(IdFormat.TryNormalize(raw, out var id));
        Assert.Equal(expected, id);
    }

    [Fact]
    public void IsValidId_RejectsLeadingZero()
    {
        Assert.False(IdFormat.IsValidId("0123-4567-890A-BCDE"));
        Assert.False(IdFormat.IsValidId("0000-0000-1234-5678"));
    }

    [Fact]
    public void IsValidId_AcceptsStandardFormat()
    {
        Assert.True(IdFormat.IsValidId("7123-4567-890A-BCDE"));
        Assert.True(IdFormat.IsValidId("FFFF-FFFF-FFFF-FFFF"));
        Assert.True(IdFormat.IsValidId("1234-5678-90AB-CDEF"));
    }

    [Fact]
    public void Format_GroupsAs4_4_4_4()
    {
        Assert.Equal("ABCD-EF01-2345-6789", IdFormat.Format("ABCDEF0123456789"));
        Assert.Equal("1234-5678-90AB-CDEF", IdFormat.Format("1234567890ABCDEF"));
    }

    [Fact]
    public void TryNormalize_ExampleFromRequirement_MatchesDocumentedBehavior()
    {
        // 需求文档示例：0123-4567-890A-BCDE 会被另一个应用规范化为 7123-4567-890A-BCDE，
        // 因此对当前计算而言原始输入无效，必须跳过。
        Assert.False(IdFormat.TryNormalize("0123-4567-890A-BCDE", out _));

        // 0000-0000-1234-5678 同理会被规范化为 7777-7777-1234-5678，原始输入无效。
        Assert.False(IdFormat.TryNormalize("0000-0000-1234-5678", out _));

        // 而规范化的目标值本身是有效输入。
        Assert.True(IdFormat.TryNormalize("7123-4567-890A-BCDE", out var valid));
        Assert.Equal("7123-4567-890A-BCDE", valid);
    }
}
