namespace DataWorkflows.Connector.Monday.Actions.Models;

/// <summary>
/// Output for updating a sub-item column value.
/// This is a cosmetic alias of UpdateColumnOutput for non-technical users.
/// Sub-items ARE items in Monday.com - the same update operation works for both.
/// </summary>
public class UpdateSubItemColumnOutput : UpdateColumnOutput
{
    // No additional properties needed - sub-items return the same output as regular items
    // The Item property will contain the updated sub-item with ParentId populated
}
