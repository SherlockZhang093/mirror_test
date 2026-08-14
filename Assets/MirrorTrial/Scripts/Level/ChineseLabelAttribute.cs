using UnityEngine;

namespace MirrorTrial.Level
{
    public class ChineseLabelAttribute : PropertyAttribute
    {
        public readonly string label;

        public ChineseLabelAttribute(string label)
        {
            this.label = label;
        }
    }
}
