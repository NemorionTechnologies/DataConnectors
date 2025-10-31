namespace DataWorkflows.Connector.Monday.Actions.Models;

/// <summary>
/// Parameters for updating a sub-item column value.
/// This is a cosmetic alias of UpdateColumnParameters for non-technical users.
/// Sub-items ARE items in Monday.com - the same update operation works for both.
/// </summary>
public class UpdateSubItemColumnParameters : UpdateColumnParameters
{
    // No additional properties needed - sub-items use the same parameters as regular items
    // The ItemId parameter will contain the sub-item's ID
}
