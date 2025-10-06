CREATE TABLE rigs(
    tenant_id uuid 
      NOT NULL 
      REFERENCES auth.tenants(id) 
      ON DELETE RESTRICT 
      DEFAULT current_tenant(),
    created_at timestamptz 
      NOT NULL
        DEFAULT now(),
    updated_at timestamptz 
      NOT NULL
      DEFAULT now(),

    capture_session_id uuid 
      NOT NULL 
      REFERENCES capture_sessions(id) 
      ON DELETE RESTRICT,
    rig_id text 
      NOT NULL,

    PRIMARY KEY (capture_session_id, rig_id)
);

ALTER TABLE rigs ENABLE ROW LEVEL SECURITY;

CREATE POLICY rigs_rls_policy ON rigs
  FOR ALL
    USING (tenant_id = current_tenant())
    WITH CHECK (tenant_id = current_tenant());

-- Allow orchestrator role to bypass RLS for all operations
CREATE POLICY rigs_orchestrator_rls_policy ON rigs
  FOR ALL
    TO plerion_orchestration_user
    USING (true)
    WITH CHECK (true);

CREATE TRIGGER rigs_touch_updated_at_trigger
  BEFORE UPDATE ON rigs
  FOR EACH ROW EXECUTE FUNCTION touch_updated_at();