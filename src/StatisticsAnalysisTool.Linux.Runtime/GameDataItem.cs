namespace StatisticsAnalysisTool.Linux.Runtime;

public sealed record GameDataItem(
    int Index,
    string UniqueName,
    string DisplayName)
{
    public string RenderUrl => $"/api/item-images/{Uri.EscapeDataString(UniqueName)}.png";
}
