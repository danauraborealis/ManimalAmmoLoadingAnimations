using System.Collections.Generic;

namespace Manimal.LoadAmmoAnim
{
    // mag template id -> the GameObject name we turn on inside the bundle prefab.
    // the mesh names have to match the bundle exactly. dont rename them without
    // re-exporting the bundle too.
    internal static class MagAnimLookup
    {
        private static readonly Dictionary<string, string> _meshByTemplateId =
            new Dictionary<string, string>
            {
                // standard stanag variants
                ["55d4887d4bdc2d962f8b4570"] = "stanag_MESH",           // stanag 30rd
                ["544a37c44bdc2d25388b4567"] = "stanag_60_MESH",        // stanag 60rd coffin
                ["5c6592372e221600133e47d7"] = "stanag_100_MESH",       // stanag 100rd drum

                // pmag variants
                ["5aaa5e60e5b5b000140293d6"] = "pmag_10_MESH",          // pmag 10rd
                ["5448c1d04bdc2dff2f8b4569"] = "pmag_20_MESH",          // pmag 20rd
                ["5aaa5dfee5b5b000140293d3"] = "pmag_MESH",             // pmag 30rd
                ["5d1340b3d7ad1a0b52682ed7"] = "pmag_FDE_MESH",         // pmag 30rd fde
                ["544a378f4bdc2d30388b4567"] = "pmag_40_MESH",          // pmag 40rd
                ["5d1340bdd7ad1a0e8d245aab"] = "pmag_40_FDE_MESH",      // pmag 40rd fde
                ["55802d5f4bdc2dac148b458e"] = "pmag_window_MESH",      // pmag 30rd window
                ["5d1340cad7ad1a0b0b249869"] = "pmag_window_FDE_MESH",  // pmag 30rd window fde

                // hk + other stanag-ish variants
                ["5c6d42cb2e2216000e69d7d1"] = "stanag_hk_polymer_MESH",            // hk polymer
                ["5c05413a0db834001c390617"] = "stanag_hk_416_steel_maritime_MESH", // hk 416 steel maritime
                ["5c6d450c2e221600114c997d"] = "stanag_hk_gen2_MESH",               // hk gen2
                ["5c6d46132e221601da357d56"] = "stanag_troy_battlemag_MESH",        // troy battlemag
                ["61840bedd92c473c77021635"] = "stanag_mk16_MESH",                  // mk16
                ["61840d85568c120fdd2962a5"] = "stanag_mk16_MESH",                  // mk16 fde
            };

        private const string FallbackMesh = "stanag_MESH";

        // which clip set a magazine loads with. the table is in MagAnimFamilies.cs.
        public static MagAnimFamily GetFamily(string templateId)
        {
            if (templateId != null && MagAnimFamily.ByTemplate.TryGetValue(templateId, out var family))
                return family;
            return MagAnimFamily.Stanag556;
        }

        // every mesh name the bundle has. ApplyMeshSelection uses this so we only
        // toggle our own mag meshes, and leave the hand/arm renderers alone.
        public static readonly HashSet<string> AllMeshNames =
            new HashSet<string>(_meshByTemplateId.Values);

        // returns the mesh name for a given mag template id. falls back to stanag_MESH
        // if the mag isnt in the table yet.
        public static string GetMeshName(string templateId)
        {
            if (templateId != null && _meshByTemplateId.TryGetValue(templateId, out var mesh))
                return mesh;

            return FallbackMesh;
        }
    }
}
