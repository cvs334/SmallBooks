using SmallBooks.Core;

namespace SmallBooks.Modules;

/// <summary>
/// Where invoice and bill lines get their items. Sales provides a basic list of
/// services and products; Inventory will replace it with stock items, without
/// any change to Sales or Purchases.
/// </summary>
public interface IItemCatalog
{
    Task<IReadOnlyList<CatalogItem>> SearchAsync(string? text, CancellationToken cancellationToken = default);
    Task<CatalogItem?> GetAsync(string itemId, CancellationToken cancellationToken = default);
}

/// <param name="IncomeAccountId">Account credited when the item is sold.</param>
/// <param name="ExpenseAccountId">Account debited when the item is bought, if it is bought.</param>
public sealed record CatalogItem(
    string Id,
    string Name,
    string? Description,
    Money Price,
    int IncomeAccountId,
    int? ExpenseAccountId,
    bool IsService,
    string? TaxCode);

/// <summary>
/// Lets a module offer "create from" actions on another module's screens, such as
/// making a bill from a purchase order.
/// </summary>
public interface IDocumentSource
{
    /// <summary>The kind of document this source can create, such as "bill".</summary>
    string TargetDocumentType { get; }

    Task<IReadOnlyList<SourceDocument>> FindAvailableAsync(int? contactId, CancellationToken cancellationToken = default);
}

public sealed record SourceDocument(string ModuleId, string DocumentType, string DocumentId, string Title, Money Total);
