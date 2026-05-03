namespace StatisticsAnalysisTool.Tui;

public sealed record GameDataItem(
    int Index,
    string UniqueName,
    string DisplayName)
{
    public string RenderUrl => $"https://render.albiononline.com/v1/item/{Uri.EscapeDataString(UniqueName)}.png";
}
