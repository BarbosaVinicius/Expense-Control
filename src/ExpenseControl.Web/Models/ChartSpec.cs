namespace ExpenseControl.Web.Models;

/// <summary>One data series of a chart. <see cref="Color"/> and <see cref="PointColors"/> name --viz-* CSS variables ("expense", "cat-food").</summary>
public sealed record ChartSeries(
    string Name,
    string Color,
    IReadOnlyList<decimal> Data,
    IReadOnlyList<string>? PointColors = null,
    IReadOnlyList<string>? Extra = null);

/// <summary>What wwwroot/js/charts.js needs to draw a chart. <see cref="Kind"/> is "columns", "barsH" or "line".</summary>
public sealed record ChartSpec(
    string Kind,
    IReadOnlyList<string> Labels,
    IReadOnlyList<ChartSeries> Series,
    bool Legend,
    string Locale,
    string CurrencySymbol,
    string AriaLabel);
