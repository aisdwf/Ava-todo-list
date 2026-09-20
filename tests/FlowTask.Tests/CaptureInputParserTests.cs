using FlowTask.Core.Models;
using Xunit;

namespace FlowTask.Tests;

public class CaptureInputParserTests
{
    private static readonly string[] Projects = ["FlowTask", "Default", "家务"];
    private static readonly string[] Tags = ["紧急", "阅读", "bug"];

    [Fact]
    public void Parse_ExtractsProjectAndTags_LeavesTitle()
    {
        var result = CaptureInputParser.Parse(
            "修登录 @FlowTask #紧急 #bug",
            Projects,
            Tags);

        Assert.Equal("修登录", result.Title);
        Assert.Equal("FlowTask", result.ProjectName);
        Assert.Equal(new[] { "紧急", "bug" }, result.TagNames.ToArray());
    }

    [Fact]
    public void Parse_UnknownTokens_StrippedForCreate_NotLeftInTitle()
    {
        var result = CaptureInputParser.Parse(
            "灵感 @不存在的项目 #未知标签 后续",
            Projects,
            Tags);

        Assert.Equal("灵感 后续", result.Title);
        Assert.Equal("不存在的项目", result.ProjectName);
        Assert.Equal(new[] { "未知标签" }, result.TagNames.ToArray());
    }

    [Fact]
    public void Parse_NoSigils_TitleOnly()
    {
        var result = CaptureInputParser.Parse("只是一句话", Projects, Tags);

        Assert.Equal("只是一句话", result.Title);
        Assert.Null(result.ProjectName);
        Assert.Empty(result.TagNames);
    }

    [Fact]
    public void Parse_MatchesProjectIgnoreCase_ViaNormalize()
    {
        var result = CaptureInputParser.Parse("x @flowtask", Projects, Tags);

        Assert.Equal("x", result.Title);
        Assert.Equal("flowtask", result.ProjectName);
    }

    [Fact]
    public void Parse_DedupesTags_CaseInsensitive()
    {
        var result = CaptureInputParser.Parse("t #bug #BUG", Projects, Tags);

        Assert.Equal(new[] { "bug" }, result.TagNames.ToArray());
    }

    [Fact]
    public void Parse_DoubleHash_StaysInTitle_NotTag()
    {
        var result = CaptureInputParser.Parse("note ##like ###like", Projects, Tags);

        Assert.Equal("note ##like ###like", result.Title);
        Assert.Null(result.ProjectName);
        Assert.Empty(result.TagNames);
    }

    [Fact]
    public void Parse_DoubleAt_StaysInTitle_NotProject()
    {
        var result = CaptureInputParser.Parse("ping @@FlowTask", Projects, Tags);

        Assert.Equal("ping @@FlowTask", result.Title);
        Assert.Null(result.ProjectName);
    }

    [Fact]
    public void TryGetCompletionToken_DetectsAtPrefix()
    {
        const string text = "hello @Flo";
        var ok = CaptureInputParser.TryGetCompletionToken(text, text.Length, out var sigil, out var prefix, out var start);

        Assert.True(ok);
        Assert.Equal('@', sigil);
        Assert.Equal("Flo", prefix);
        Assert.Equal(6, start);
    }

    [Fact]
    public void TryGetCompletionToken_DetectsHashPrefix()
    {
        const string text = "#紧";
        var ok = CaptureInputParser.TryGetCompletionToken(text, text.Length, out var sigil, out var prefix, out _);

        Assert.True(ok);
        Assert.Equal('#', sigil);
        Assert.Equal("紧", prefix);
    }

    [Fact]
    public void TryGetCompletionToken_FalseWhenNoSigil()
    {
        const string text = "plain";
        Assert.False(CaptureInputParser.TryGetCompletionToken(text, text.Length, out _, out _, out _));
    }

    [Fact]
    public void TryGetCompletionToken_FalseForDoubleHash()
    {
        const string text = "##like";
        Assert.False(CaptureInputParser.TryGetCompletionToken(text, text.Length, out _, out _, out _));
    }
}
