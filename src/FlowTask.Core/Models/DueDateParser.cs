using System.Globalization;

namespace FlowTask.Core.Models;

/// <summary>
/// 到期日纯数字 / 标准日期文本解析。
/// </summary>
/// <remarks>
/// 解析失败时由调用方保留原值；本类型不负责写回。
/// 一律以调用方传入的 <paramref name="today"/> 为日历日基准（须来自 <c>IClock.Today</c>）。
/// </remarks>
public static class DueDateParser
{
    public enum ParseStatus
    {
        /// <summary>空白输入，表示调用方应视为「清除」意图（仅当用户主动清空数字框并提交时使用）。</summary>
        Empty,

        /// <summary>成功解析为日历日。</summary>
        Success,

        /// <summary>无法识别，调用方必须保留原值。</summary>
        Invalid
    }

    public readonly record struct ParseResult(ParseStatus Status, DateTime? Date);

    /// <summary>
    /// 尝试解析用户输入。
    /// </summary>
    public static ParseResult TryParse(string? input, DateTime today)
    {
        var trimmed = input?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return new ParseResult(ParseStatus.Empty, null);
        }

        if (DateTime.TryParseExact(
                trimmed,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var iso))
        {
            return new ParseResult(ParseStatus.Success, DateOnly(iso));
        }

        if (!trimmed.All(char.IsDigit))
        {
            return new ParseResult(ParseStatus.Invalid, null);
        }

        return trimmed.Length switch
        {
            1 or 2 => ParseDayOfMonth(trimmed, today),
            4 => ParseMonthDay(trimmed, today),
            6 => ParseShortYearMonthDay(trimmed),
            8 => ParseFullYearMonthDay(trimmed),
            _ => new ParseResult(ParseStatus.Invalid, null)
        };
    }

    private static ParseResult ParseDayOfMonth(string digits, DateTime today)
    {
        if (!int.TryParse(digits, out var day) || day < 1 || day > 31)
        {
            return new ParseResult(ParseStatus.Invalid, null);
        }

        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        if (day > daysInMonth)
        {
            return new ParseResult(ParseStatus.Invalid, null);
        }

        return new ParseResult(ParseStatus.Success, new DateTime(today.Year, today.Month, day));
    }

    private static ParseResult ParseMonthDay(string digits, DateTime today)
    {
        var month = int.Parse(digits[..2], CultureInfo.InvariantCulture);
        var day = int.Parse(digits[2..], CultureInfo.InvariantCulture);
        if (month < 1 || month > 12)
        {
            return new ParseResult(ParseStatus.Invalid, null);
        }

        var daysInMonth = DateTime.DaysInMonth(today.Year, month);
        if (day < 1 || day > daysInMonth)
        {
            return new ParseResult(ParseStatus.Invalid, null);
        }

        return new ParseResult(ParseStatus.Success, new DateTime(today.Year, month, day));
    }

    private static ParseResult ParseShortYearMonthDay(string digits)
    {
        var year = 2000 + int.Parse(digits[..2], CultureInfo.InvariantCulture);
        var month = int.Parse(digits[2..4], CultureInfo.InvariantCulture);
        var day = int.Parse(digits[4..], CultureInfo.InvariantCulture);
        return TryCreate(year, month, day);
    }

    private static ParseResult ParseFullYearMonthDay(string digits)
    {
        var year = int.Parse(digits[..4], CultureInfo.InvariantCulture);
        var month = int.Parse(digits[4..6], CultureInfo.InvariantCulture);
        var day = int.Parse(digits[6..], CultureInfo.InvariantCulture);
        return TryCreate(year, month, day);
    }

    private static ParseResult TryCreate(int year, int month, int day)
    {
        if (year < 1 || year > 9999 || month < 1 || month > 12)
        {
            return new ParseResult(ParseStatus.Invalid, null);
        }

        var daysInMonth = DateTime.DaysInMonth(year, month);
        if (day < 1 || day > daysInMonth)
        {
            return new ParseResult(ParseStatus.Invalid, null);
        }

        return new ParseResult(ParseStatus.Success, new DateTime(year, month, day));
    }

    private static DateTime DateOnly(DateTime value) =>
        new(value.Year, value.Month, value.Day);
}
