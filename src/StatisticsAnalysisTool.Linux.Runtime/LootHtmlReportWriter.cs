using System.Net;
using System.Text;
using StatisticsAnalysisTool.Linux.Core;

namespace StatisticsAnalysisTool.Linux.Runtime;

public sealed class LootHtmlReportWriter
{
    private readonly LinuxAppPaths _paths;

    public LootHtmlReportWriter(LinuxAppPaths paths)
    {
        _paths = paths;
    }

    public string Write(IReadOnlyList<LootLogEntry> entries)
    {
        Directory.CreateDirectory(_paths.ReportsDirectory);
        var filePath = Path.Combine(_paths.ReportsDirectory, $"loot-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.html");
        File.WriteAllText(filePath, BuildHtml(entries), Encoding.UTF8);
        return filePath;
    }

    private static string BuildHtml(IReadOnlyList<LootLogEntry> entries)
    {
        var groupedItems = entries
            .Where(x => !x.IsSilver)
            .GroupBy(x => new { x.ItemIndex, x.ItemUniqueName, x.ItemName })
            .Select(x => new
            {
                x.Key.ItemIndex,
                x.Key.ItemUniqueName,
                x.Key.ItemName,
                Quantity = x.Sum(entry => entry.Quantity),
                LastSeen = x.Max(entry => entry.Time)
            })
            .OrderByDescending(x => x.Quantity)
            .ThenBy(x => x.ItemName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"en\"><head><meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine("<title>Albion Loot Report</title>");
        html.AppendLine("<style>");
        html.AppendLine("body{margin:0;background:#0b0f14;color:#d7dde5;font:14px system-ui,Segoe UI,sans-serif}");
        html.AppendLine("main{max-width:1120px;margin:0 auto;padding:24px}");
        html.AppendLine("h1{font-size:22px;margin:0 0 4px;color:#f5d76e}");
        html.AppendLine(".meta{color:#93a4b7;margin-bottom:18px}");
        html.AppendLine(".grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:10px}");
        html.AppendLine(".item{display:grid;grid-template-columns:56px 1fr;gap:10px;align-items:center;border:1px solid #243244;background:#121923;padding:8px;border-radius:6px}");
        html.AppendLine("img{width:56px;height:56px;object-fit:contain}");
        html.AppendLine(".name{font-weight:650;color:#edf2f7}.sub{color:#93a4b7;font-size:12px;margin-top:3px}.qty{color:#7ee787}");
        html.AppendLine("</style></head><body><main>");
        html.AppendLine("<h1>Albion Loot Report</h1>");
        html.AppendLine($"<div class=\"meta\">Generated {WebUtility.HtmlEncode(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"))} - {groupedItems.Count:N0} item type(s)</div>");
        html.AppendLine("<section class=\"grid\">");

        foreach (var item in groupedItems)
        {
            var name = WebUtility.HtmlEncode(item.ItemName);
            var uniqueName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(item.ItemUniqueName) ? $"item #{item.ItemIndex}" : item.ItemUniqueName);
            var imageUrl = string.IsNullOrWhiteSpace(item.ItemUniqueName)
                ? string.Empty
                : $"https://render.albiononline.com/v1/item/{Uri.EscapeDataString(item.ItemUniqueName)}.png";

            html.AppendLine("<article class=\"item\">");
            html.AppendLine(string.IsNullOrWhiteSpace(imageUrl)
                ? "<div></div>"
                : $"<img src=\"{imageUrl}\" alt=\"\">");
            html.AppendLine("<div>");
            html.AppendLine($"<div class=\"name\">{name}</div>");
            html.AppendLine($"<div class=\"sub\">{uniqueName}</div>");
            html.AppendLine($"<div class=\"sub\"><span class=\"qty\">{item.Quantity:N0}</span> looted - last {WebUtility.HtmlEncode(item.LastSeen.ToString("HH:mm:ss"))}</div>");
            html.AppendLine("</div></article>");
        }

        html.AppendLine("</section></main></body></html>");
        return html.ToString();
    }
}
