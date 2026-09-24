using FlowTask.Core.Models;
using Xunit;

namespace FlowTask.Tests;

public class CaptureInputParserTests
{
    private static readonly string[] Projects = ["FlowTask", "Default", "家务"];

    [Fact]
    public void Parse_ExtractsProject_LeavesTitle()
    {
        var result = CaptureInputParser.Parse(
            "修登录 @FlowTask",
            Projects);

        Assert.Equal("修登录", result.Title);
        Assert.Equal("FlowTask", result.ProjectName);
    }

    [Fact]
    public void Parse_UnknownProjectToken_StrippedForCreate_NotLeftInTitle()
    {
        var result = CaptureInputParser.Parse(
            "灵感 @不存在的项目 后续",
            Projects);

        Assert.Equal("灵感 后续", result.Title);
        Assert.Equal("不存在的项目", result.ProjectName);
    }

    [Fact]
    public void Parse_NoSigils_TitleOnly()
    {
        var result = CaptureInputParser.Parse("只是一句话", Projects);

        Assert.Equal("只是一句话", result.Title);
        Assert.Null(result.ProjectName);
    }

    [Fact]
    public void Parse_MatchesProjectIgnoreCase_ViaNormalize()
    {
        var result = CaptureInputParser.Parse("x @flowtask", Projects);

        Assert.Equal("x", result.Title);
        Assert.Equal("flowtask", result.ProjectName);
    }

    [Fact]
    public void Parse_DoubleAt_StaysInTitle_NotProject()
    {
        var result = CaptureInputParser.Parse("ping @@FlowTask", Projects);

        Assert.Equal("ping @@FlowTask", result.Title);
        Assert.Null(result.ProjectName);
    }

    [Fact]
    public void Parse_HashToken_StaysInTitle_NotParsedAsSigil()
    {
        var result = CaptureInputParser.Parse("note #紧急", Projects);

        Assert.Equal("note #紧急", result.Title);
        Assert.Null(result.ProjectName);
    }

    [Fact]
    public void TryGetCompletionToken_DetectsAtPrefix()
    {
        const string text = "hello @Flo";
        var ok = CaptureInputParser.TryGetCompletionToken(text, text.Length, out var prefix, out var start);

        Assert.True(ok);
        Assert.Equal("Flo", prefix);
        Assert.Equal(6, start);
    }

    [Fact]
    public void TryGetCompletionToken_FalseWhenNoSigil()
    {
        const string text = "plain";
        Assert.False(CaptureInputParser.TryGetCompletionToken(text, text.Length, out _, out _));
    }

    [Fact]
    public void TryGetCompletionToken_FalseForDoubleAt()
    {
        const string text = "@@like";
        Assert.False(CaptureInputParser.TryGetCompletionToken(text, text.Length, out _, out _));
    }
}
