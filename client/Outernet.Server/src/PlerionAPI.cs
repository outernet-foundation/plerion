using Outernet.Shared;

namespace Outernet.Server
{
    public struct NodeRecord
    {
        public int id { get; set; }
        public Guid uuid { get; set; }
        public string name { get; set; }
        public bool active { get; set; }
        public double position_x { get; set; }
        public double position_y { get; set; }
        public double position_z { get; set; }
        public float rotation_x { get; set; }
        public float rotation_y { get; set; }
        public float rotation_z { get; set; }
        public float rotation_w { get; set; }
        public float label_scale { get; set; }
        public float label_width { get; set; }
        public float label_height { get; set; }
        public string link { get; set; }
        public LinkType link_type { get; set; }
        public string label { get; set; }
        public LabelType label_type { get; set; }
        public Guid layer { get; set; }
    }

    public struct NodesWithRadiusOfAnyUserRequest
    {
        public double[][] user_positions { get; set; }
        public double radius { get; set; }
        public int limit_count { get; set; }
    }

    static public class PlerionAPI
    {
        private static readonly string schema = "dev2";
        private static readonly string SUPABASE_URL = "https://ysugcfsqhmladpfnqgvs.supabase.co";
        private static readonly string SUPABASE_API_KEY = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InlzdWdjZnNxaG1sYWRwZm5xZ3ZzIiwicm9sZSI6ImFub24iLCJpYXQiOjE3MjcyNzU2MzksImV4cCI6MjA0Mjg1MTYzOX0.cIdcZuyN28iFbiUChId4fc-6YjleIkAUeAQs7UQ6gRE";

        private static Dictionary<string, string> SUPABASE_HEADERS = new Dictionary<string, string>
        {
            { "apiKey", SUPABASE_API_KEY },
            { "Authorization", $"Bearer {SUPABASE_API_KEY}" },
            { "Accept-Profile", schema },
            { "Content-Profile", schema }
        };

        private static async Task<T> Post<T, U>(string url, U content)
        {
            return await RestClient.Post<U, T>(url, content, SUPABASE_HEADERS);
        }

        public static async Task<List<NodeRecord>> GetNodes(IEnumerable<Double3> userPositions, double radius, int limit_count)
        {
            return await Post<List<NodeRecord>, NodesWithRadiusOfAnyUserRequest>(
                $"{SUPABASE_URL}/rest/v1/rpc/get_nodes_within_radius_of_any_user",
                new NodesWithRadiusOfAnyUserRequest
                {
                    user_positions = userPositions
                        .Select(position => new double[] { position.x, position.y, position.z })
                        .ToArray(),
                    radius = radius,
                    limit_count = limit_count
                });
        }
    }
}