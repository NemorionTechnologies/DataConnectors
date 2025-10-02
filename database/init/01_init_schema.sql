-- DataWorkflows Database Initialization Script
-- Version: 1.0
-- Description: Initial schema for workflow definitions and execution logs

-- Create workflows table
CREATE TABLE IF NOT EXISTS workflows (
    id SERIAL PRIMARY KEY,
    workflow_id VARCHAR(255) UNIQUE NOT NULL,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    definition JSONB NOT NULL,
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create workflow_executions table for telemetry
CREATE TABLE IF NOT EXISTS workflow_executions (
    id SERIAL PRIMARY KEY,
    execution_id UUID UNIQUE NOT NULL,
    workflow_id VARCHAR(255) NOT NULL,
    status VARCHAR(50) NOT NULL, -- 'running', 'completed', 'failed'
    started_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    completed_at TIMESTAMP,
    error_message TEXT,
    metadata JSONB,
    FOREIGN KEY (workflow_id) REFERENCES workflows(workflow_id) ON DELETE CASCADE
);

-- Create workflow_execution_steps table for step-level telemetry
CREATE TABLE IF NOT EXISTS workflow_execution_steps (
    id SERIAL PRIMARY KEY,
    execution_id UUID NOT NULL,
    step_number INT NOT NULL,
    step_name VARCHAR(255) NOT NULL,
    connector_service VARCHAR(100),
    status VARCHAR(50) NOT NULL, -- 'running', 'completed', 'failed'
    started_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    completed_at TIMESTAMP,
    duration_ms INT,
    input_payload JSONB,
    output_payload JSONB,
    error_message TEXT,
    FOREIGN KEY (execution_id) REFERENCES workflow_executions(execution_id) ON DELETE CASCADE
);

-- Create indexes for performance
CREATE INDEX idx_workflow_executions_workflow_id ON workflow_executions(workflow_id);
CREATE INDEX idx_workflow_executions_status ON workflow_executions(status);
CREATE INDEX idx_workflow_executions_started_at ON workflow_executions(started_at);
CREATE INDEX idx_workflow_execution_steps_execution_id ON workflow_execution_steps(execution_id);
CREATE INDEX idx_workflow_execution_steps_status ON workflow_execution_steps(status);

-- Insert sample workflow for testing
INSERT INTO workflows (workflow_id, name, description, definition) VALUES
(
    'test-workflow-001',
    'Test Workflow',
    'Sample workflow for testing the system',
    '{"steps": [{"name": "fetch_monday_data", "connector": "monday", "action": "get_board_items"}]}'::jsonb
)
ON CONFLICT (workflow_id) DO NOTHING;

-- Success message
DO $$
BEGIN
    RAISE NOTICE 'Database schema initialized successfully!';
END $$;
