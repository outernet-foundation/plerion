CREATE TABLE localization_maps(
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
    reconstruction_id uuid 
      NOT NULL
      REFERENCES reconstructions(id) 
      ON DELETE RESTRICT,
    
    id uuid 
      PRIMARY KEY
      DEFAULT uuid_generate_v4()
);

ALTER TABLE localization_maps ENABLE ROW LEVEL SECURITY;

CREATE POLICY localization_maps_rls_policy ON localization_maps
  FOR ALL
    USING (tenant_id = current_tenant())
    WITH CHECK (tenant_id = current_tenant());

-- Allow orchestrator role to bypass RLS for all operations
CREATE POLICY localization_maps_orchestrator_rls_policy ON localization_maps
  FOR ALL
    TO plerion_orchestration_user
    USING (true)
    WITH CHECK (true);

CREATE TRIGGER localization_maps_touch_updated_at_trigger
  BEFORE UPDATE ON localization_maps
  FOR EACH ROW EXECUTE FUNCTION touch_updated_at();