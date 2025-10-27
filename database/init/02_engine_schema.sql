-- Engine schema aligned with src/DataWorkflows.Data migrations
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Workflows
CREATE TABLE IF NOT EXISTS Workflows (
  Id              TEXT PRIMARY KEY,
  DisplayName     TEXT NOT NULL,
  CurrentVersion  INT NULL,
  Status          TEXT NOT NULL DEFAULT 'Draft' CHECK (Status IN ('Draft','Active','Archived')),
  IsEnabled       BOOLEAN NOT NULL DEFAULT TRUE,
  CreatedAt       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- WorkflowDefinitions
CREATE TABLE IF NOT EXISTS WorkflowDefinitions (
  WorkflowId      TEXT        NOT NULL,
  Version         INT         NOT NULL,
  DefinitionJson  JSONB       NOT NULL,
  Checksum        TEXT        NOT NULL,
  CreatedAt       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  PRIMARY KEY (WorkflowId, Version),
  CONSTRAINT uq_workflow_checksum UNIQUE (WorkflowId, Checksum),
  FOREIGN KEY (WorkflowId) REFERENCES Workflows(Id) ON DELETE CASCADE
);

-- WorkflowExecutions
CREATE TABLE IF NOT EXISTS WorkflowExecutions (
  Id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  WorkflowId        TEXT NOT NULL,
  WorkflowVersion   INT  NOT NULL,
  WorkflowRequestId TEXT NOT NULL,
  Status            TEXT NOT NULL CHECK (Status IN ('Pending','Running','Succeeded','Failed','Cancelled')),
  TriggerPayloadJson JSONB NOT NULL,
  StartTime         TIMESTAMPTZ NULL,
  EndTime           TIMESTAMPTZ NULL,
  CorrelationId     TEXT NULL,
  ContextSnapshotJson JSONB NULL,
  FOREIGN KEY (WorkflowId, WorkflowVersion)
    REFERENCES WorkflowDefinitions(WorkflowId, Version) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_wfexec_workflow_request
  ON WorkflowExecutions(WorkflowId, WorkflowRequestId);

-- ActionExecutions
CREATE TABLE IF NOT EXISTS ActionExecutions (
  Id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  WorkflowExecutionId UUID NOT NULL,
  NodeId              TEXT NOT NULL,
  ActionType          TEXT NOT NULL,
  Status              TEXT NOT NULL CHECK (Status IN ('Succeeded','Failed','RetriableFailure','Skipped')),
  Attempt             INT NOT NULL DEFAULT 1,
  RetryCount          INT NOT NULL DEFAULT 0,
  ParametersJson      JSONB NULL,
  OutputsJson         JSONB NULL,
  ErrorJson           JSONB NULL,
  StartTime           TIMESTAMPTZ NULL,
  EndTime             TIMESTAMPTZ NULL,
  FOREIGN KEY (WorkflowExecutionId) REFERENCES WorkflowExecutions(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_actionexec_by_exec_node
  ON ActionExecutions(WorkflowExecutionId, NodeId);

