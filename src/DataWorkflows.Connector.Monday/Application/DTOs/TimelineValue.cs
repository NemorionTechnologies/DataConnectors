namespace DataWorkflows.Connector.Monday.Application.DTOs;

/// <summary>
/// Represents a parsed timeline/daterange column value from Monday.com
/// </summary>
public class TimelineValue
{
    /// <summary>
    /// Start date of the timeline
    /// </summary>
    public DateTime? From { get; set; }

    /// <summary>
    /// End date of the timeline
    /// </summary>
    public DateTime? To { get; set; }

    /// <summary>
    /// When the timeline was last changed
    /// </summary>
    public DateTimeOffset? ChangedAt { get; set; }
}
