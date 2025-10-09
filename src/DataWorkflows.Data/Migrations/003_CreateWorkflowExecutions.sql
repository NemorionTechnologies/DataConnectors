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
  FOREIGN KEY (WorkflowId, WorkflowVersion)
    REFERENCES WorkflowDefinitions(WorkflowId, Version) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX ux_wfexec_workflow_request
  ON WorkflowExecutions(WorkflowId, WorkflowRequestId);
