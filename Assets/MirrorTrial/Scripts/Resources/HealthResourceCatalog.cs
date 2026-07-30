using System.Collections.Generic;
using UnityEngine;

namespace MirrorTrial.HealthResources
{
    [CreateAssetMenu(menuName = "Mirror Trial/生命资源目录", fileName = "HealthResourceCatalog")]
    public sealed class HealthResourceCatalog : ScriptableObject
    {
        [SerializeField] List<GameObject> prefabs = new List<GameObject>();

        public IReadOnlyList<GameObject> Prefabs => prefabs;
    }
}
