CREATE TABLE images (
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
    FOREIGN KEY (capture_session_id, rig_id)
      REFERENCES rigs(capture_session_id, rig_id)
      ON DELETE RESTRICT,
    camera_id text 
      NOT NULL,
    FOREIGN KEY (capture_session_id, rig_id, camera_id)
      REFERENCES cameras(capture_session_id, rig_id, camera_id)
      ON DELETE RESTRICT,
    frame_id text 
      NOT NULL,
    FOREIGN KEY (capture_session_id, rig_id, frame_id)
      REFERENCES frames(capture_session_id, rig_id, frame_id)
      ON DELETE RESTRICT,

    PRIMARY KEY (capture_session_id, rig_id, camera_id, frame_id)
);

ALTER TABLE images ENABLE ROW LEVEL SECURITY;

CREATE POLICY images_rls_policy ON images
  FOR ALL
    USING (tenant_id = current_tenant())
    WITH CHECK (tenant_id = current_tenant());

-- Allow orchestrator role to bypass RLS for all operations
CREATE POLICY images_orchestrator_rls_policy ON images
  FOR ALL
    TO plerion_orchestration_user
    USING (true)
    WITH CHECK (true);

CREATE TRIGGER images_touch_updated_at_trigger
  BEFORE UPDATE ON images
  FOR EACH ROW EXECUTE FUNCTION touch_updated_at();