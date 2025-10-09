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
