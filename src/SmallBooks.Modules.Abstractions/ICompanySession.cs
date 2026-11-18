namespace SmallBooks.Modules;

/// <summary>The company file that is open right now, if any.</summary>
public interface ICompanySession
{
    bool IsOpen { get; }
    string? FilePath { get; }
    string? CompanyName { get; }

    /// <summary>Raised after a company is opened, closed or renamed.</summary>
    event Action? Changed;
}

/// <summary>Which modules are turned on for the open company.</summary>
public interface IModuleState
{
    bool IsEnabled(string moduleId);

    /// <summary>Raised after a module is turned on or off, or another company is opened.</summary>
    event Action? Changed;
}
