using DataWorkflows.Connector.Monday.Application.Filters;

namespace DataWorkflows.Connector.Monday.Application.Interfaces;

public interface IMondayFilterTranslator
{
    MondayFilterTranslationResult Translate(MondayFilterDefinition? filterDefinition);
}
