CREATE TYPE camera_model AS ENUM(
  'PINHOLE',
  'SIMPLE_RADIAL',
  'RADIAL',
  'OPENCV',
  'FULL_OPENCV',
  'FOV',
  'SIMPLE_PINHOLE'
);

CREATE TABLE cameras(
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
    width int 
      NOT NULL,
    height int
      NOT NULL,
    camera_model camera_model
      NOT NULL,
    intrinsics jsonb
      NOT NULL,
    reference_sensor boolean
      NOT NULL
        DEFAULT false,
    -- translation_from_reference_sensor double precision[3],
    -- rotation_from_reference_sensor double precision[4],
    tx_from_reference_sensor double precision,
    ty_from_reference_sensor double precision,
    tz_from_reference_sensor double precision,
    qx_from_reference_sensor double precision,
    qy_from_reference_sensor double precision,
    qz_from_reference_sensor double precision,
    qw_from_reference_sensor double precision,

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

    PRIMARY KEY (capture_session_id, rig_id, camera_id)
);

-- Enforce that either reference_sensor is true and extrinsics are null, 
-- or reference_sensor is false and extrinsics are set
-- ALTER TABLE cameras
-- ADD CONSTRAINT camera_is_reference_or_has_extrinsics
-- CHECK (
--   (reference_sensor = true  AND translation_from_reference_sensor IS NULL AND rotation_from_reference_sensor IS NULL)
--   OR
--   (reference_sensor = false AND translation_from_reference_sensor IS NOT NULL AND rotation_from_reference_sensor IS NOT NULL)
-- );

ALTER TABLE cameras
  ADD CONSTRAINT camera_is_reference_or_has_extrinsics
  CHECK (
    (
      reference_sensor = true  
      AND tx_from_reference_sensor IS NULL 
      AND ty_from_reference_sensor IS NULL 
      AND tz_from_reference_sensor IS NULL 
      AND qx_from_reference_sensor IS NULL 
      AND qy_from_reference_sensor IS NULL 
      AND qz_from_reference_sensor IS NULL 
      AND qw_from_reference_sensor IS NULL
    ) OR (
      reference_sensor = false 
      AND tx_from_reference_sensor IS NOT NULL 
      AND ty_from_reference_sensor IS NOT NULL 
      AND tz_from_reference_sensor IS NOT NULL 
      AND qx_from_reference_sensor IS NOT NULL 
      AND qy_from_reference_sensor IS NOT NULL 
      AND qz_from_reference_sensor IS NOT NULL 
      AND qw_from_reference_sensor IS NOT NULL
    )
  );

-- enforce that if qx, qy, qz, qw are set, they form a unit quaternion
ALTER TABLE cameras
  ADD CONSTRAINT camera_extrinsics_quaternion_is_unit
  CHECK (
    (qx_from_reference_sensor IS NULL AND qy_from_reference_sensor IS NULL AND qz_from_reference_sensor IS NULL AND qw_from_reference_sensor IS NULL)
    OR
    (ABS(POWER(qx_from_reference_sensor, 2) + POWER(qy_from_reference_sensor, 2) + POWER(qz_from_reference_sensor, 2) + POWER(qw_from_reference_sensor, 2) - 1) < 0.0001)
  );
  
ALTER TABLE cameras ENABLE ROW LEVEL SECURITY;

CREATE POLICY cameras_rls_policy ON cameras
  FOR ALL
    USING (tenant_id = current_tenant())
    WITH CHECK (tenant_id = current_tenant());

-- Allow orchestrator role to bypass RLS for all operations
CREATE POLICY cameras_orchestrator_rls_policy ON cameras
  FOR ALL
    TO plerion_orchestration_user
    USING (true)
    WITH CHECK (true);

CREATE TRIGGER cameras_touch_updated_at_trigger
  BEFORE UPDATE ON cameras
  FOR EACH ROW EXECUTE FUNCTION touch_updated_at();