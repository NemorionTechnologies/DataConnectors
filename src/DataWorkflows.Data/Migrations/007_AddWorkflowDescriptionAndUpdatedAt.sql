-- Add Description and UpdatedAt columns to Workflows table
ALTER TABLE Workflows
  ADD COLUMN IF NOT EXISTS Description TEXT NULL,
  ADD COLUMN IF NOT EXISTS UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT NOW();

-- Create trigger to auto-update UpdatedAt on modifications
CREATE OR REPLACE FUNCTION update_workflows_updated_at()
RETURNS TRIGGER AS $$
BEGIN
  NEW.UpdatedAt = NOW();
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trigger_workflows_updated_at
  BEFORE UPDATE ON Workflows
  FOR EACH ROW
  EXECUTE FUNCTION update_workflows_updated_at();
