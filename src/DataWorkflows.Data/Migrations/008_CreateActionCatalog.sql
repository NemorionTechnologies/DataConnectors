-- Migration 008: Create ActionCatalog table for connector action registration
-- This table stores metadata about all available workflow actions that connectors expose

CREATE TABLE ActionCatalog (
    Id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ActionType        TEXT NOT NULL,           -- e.g. "monday.get-items", "core.echo"
    ConnectorId       TEXT NOT NULL,           -- e.g. "monday", "core"
    DisplayName       TEXT NOT NULL,           -- Human-readable action name
    Description       TEXT,                    -- Description of what the action does
    ParameterSchema   JSONB NOT NULL,          -- JSON Schema (draft 2020-12) for action parameters
    OutputSchema      JSONB NOT NULL,          -- JSON Schema (draft 2020-12) for action outputs
    IsEnabled         BOOLEAN NOT NULL DEFAULT TRUE,   -- Can this action be used?
    RequiresAuth      BOOLEAN NOT NULL DEFAULT TRUE,   -- Does this action require authentication?
    CreatedAt         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UpdatedAt         TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Ensure (ConnectorId, ActionType) is unique for upsert capability
    CONSTRAINT UQ_ActionCatalog_ConnectorId_ActionType UNIQUE (ConnectorId, ActionType)
);

-- Index for fast lookups by ActionType (used during workflow validation and execution)
CREATE INDEX IDX_ActionCatalog_ActionType ON ActionCatalog(ActionType);

-- Index for fast lookups by ConnectorId (used for connector-specific queries)
CREATE INDEX IDX_ActionCatalog_ConnectorId ON ActionCatalog(ConnectorId);

-- Index for fetching all enabled actions (used for ActionRegistry cache)
CREATE INDEX IDX_ActionCatalog_IsEnabled ON ActionCatalog(IsEnabled) WHERE IsEnabled = TRUE;

-- Add comment explaining the table's purpose
COMMENT ON TABLE ActionCatalog IS 'Registry of all workflow actions exposed by connectors. Connectors register their actions on startup via the admin API.';
COMMENT ON COLUMN ActionCatalog.ActionType IS 'Unique identifier for the action in format: connector.action-name (e.g., monday.get-items)';
COMMENT ON COLUMN ActionCatalog.ParameterSchema IS 'JSON Schema describing what parameters this action expects';
COMMENT ON COLUMN ActionCatalog.OutputSchema IS 'JSON Schema describing what this action will output to context.data[nodeId]';
