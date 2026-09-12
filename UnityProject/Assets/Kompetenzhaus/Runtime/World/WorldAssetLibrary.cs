using System;
using UnityEngine;

namespace Kompetenzhaus.World
{
    [CreateAssetMenu(menuName = "Kompetenzhaus/Reviewed world assets")]
    public sealed class WorldAssetLibrary : ScriptableObject
    {
        [Serializable] public sealed class Entry { public string id; public GameObject model; }
        public Entry[] entries = Array.Empty<Entry>();
        public Material ground, path, navy, copper, foliage, ivory;
        public Font labelFont;
        public GameObject Get(string id)
        {
            foreach (var entry in entries) if (entry.id == id) return entry.model;
            throw new InvalidOperationException("Reviewed world model missing: " + id);
        }
    }
}
