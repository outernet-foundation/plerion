CREATE TABLE frames(
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
    tx_capture_session double precision
      NOT NULL,
    ty_capture_session double precision
      NOT NULL,
    tz_capture_session double precision
      NOT NULL,
    qx_capture_session double precision
      NOT NULL,
    qy_capture_session double precision
      NOT NULL,
    qz_capture_session double precision
      NOT NULL,
    qw_capture_session double precision
      NOT NULL,

    capture_session_id uuid 
      NOT NULL 
      REFERENCES capture_sessions(id) 
      ON DELETE RESTRICT,
    rig_id text
      NOT NULL,
    FOREIGN KEY (capture_session_id, rig_id)
      REFERENCES rigs(capture_session_id, rig_id)
      ON DELETE RESTRICT,
    frame_id text 
      NOT NULL,

    PRIMARY KEY (capture_session_id, rig_id, frame_id)
);

ALTER TABLE frames ENABLE ROW LEVEL SECURITY;

CREATE POLICY frames_rls_policy ON frames
  FOR ALL
    USING (tenant_id = current_tenant())
    WITH CHECK (tenant_id = current_tenant());

-- Allow orchestrator role to bypass RLS for all operations
CREATE POLICY frames_orchestrator_rls_policy ON frames
  FOR ALL
    TO plerion_orchestration_user
    USING (true)
    WITH CHECK (true);

CREATE TRIGGER frames_touch_updated_at_trigger
  BEFORE UPDATE ON frames
  FOR EACH ROW EXECUTE FUNCTION touch_updated_at();