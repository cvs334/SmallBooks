namespace SmallBooks.Modules;

/// <summary>Where a module lists its reports on the Reports screen.</summary>
public interface IReportRegistry
{
    void Add(ReportDefinition report);
}

/// <param name="Title">Report name, such as "Trial balance".</param>
/// <param name="Href">Address of the page that shows it.</param>
/// <param name="Category">Heading it is grouped under, such as "Accountant" or "Sales".</param>
/// <param name="Description">One sentence on what the report answers.</param>
public sealed record ReportDefinition(string Title, string Href, string Category, string Description, int Order = 100);
