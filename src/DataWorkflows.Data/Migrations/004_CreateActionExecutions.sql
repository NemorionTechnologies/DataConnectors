CREATE TABLE IF NOT EXISTS ActionExecutions (
  Id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  WorkflowExecutionId UUID NOT NULL,
  NodeId              TEXT NOT NULL,
  ActionType          TEXT NOT NULL,
  Status              TEXT NOT NULL CHECK (Status IN ('Succeeded','Failed','RetriableFailure','Skipped')),
  Attempt             INT NOT NULL DEFAULT 1,
  OutputsJson         JSONB NULL,
  ErrorJson           JSONB NULL,
  StartTime           TIMESTAMPTZ NULL,
  EndTime             TIMESTAMPTZ NULL,
  FOREIGN KEY (WorkflowExecutionId) REFERENCES WorkflowExecutions(Id) ON DELETE CASCADE
);

CREATE INDEX ix_actionexec_by_exec_node
  ON ActionExecutions(WorkflowExecutionId, NodeId);
