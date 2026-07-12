namespace ItTiger.TigerCli.Enums;


/// <summary>
/// Ordering mode for selectable option collections.
/// </summary>
public enum SelectOrder 
{ 
    /// <summary>Keep insertion order.</summary>
    Insertion, 
    /// <summary>Order by key.</summary>
    ByKey, 
    /// <summary>Order by label.</summary>
    ByLabel, 
    /// <summary>Use a custom ordering supplied by the caller.</summary>
    Custom 
}
