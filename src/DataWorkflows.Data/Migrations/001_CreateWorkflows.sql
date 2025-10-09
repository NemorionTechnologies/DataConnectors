CREATE TABLE IF NOT EXISTS Workflows (
  Id              TEXT PRIMARY KEY,
  DisplayName     TEXT NOT NULL,
  CurrentVersion  INT NULL,
  Status          TEXT NOT NULL DEFAULT 'Draft' CHECK (Status IN ('Draft','Active','Archived')),
  IsEnabled       BOOLEAN NOT NULL DEFAULT TRUE,
  CreatedAt       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
