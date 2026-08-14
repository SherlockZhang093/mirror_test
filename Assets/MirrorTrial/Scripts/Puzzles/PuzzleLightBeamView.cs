using UnityEngine;

namespace MirrorTrial.Puzzles
{
    [DisallowMultipleComponent]
    public sealed class PuzzleLightBeamView : MonoBehaviour
    {
        [SerializeField] LineRenderer glow;
        [SerializeField] LineRenderer core;

        LineRenderer outerGlow;

        void Awake()
        {
            if (!glow || !core) return;

            outerGlow = Instantiate(glow, transform);
            outerGlow.name = "Outer Glow";
            outerGlow.transform.localPosition = Vector3.zero;
            outerGlow.startWidth = outerGlow.endWidth = 0.24f;
            outerGlow.startColor = outerGlow.endColor = new Color(1f, 0.5f, 0.12f, 0.025f);
            outerGlow.sortingOrder = glow.sortingOrder - 1;
            outerGlow.numCapVertices = 8;
            outerGlow.numCornerVertices = 6;

            glow.startWidth = glow.endWidth = 0.12f;
            glow.startColor = glow.endColor = new Color(1f, 0.62f, 0.2f, 0.09f);
            glow.numCapVertices = 8;
            glow.numCornerVertices = 6;

            core.startWidth = core.endWidth = 0.035f;
            core.startColor = core.endColor = new Color(1f, 0.88f, 0.56f, 0.52f);
            core.numCapVertices = 8;
            core.numCornerVertices = 6;
        }

        public void SetEndpoints(Vector3 from, Vector3 to)
        {
            SetLine(glow, from, to);
            SetLine(outerGlow, from, to);
            SetLine(core, from, to);
        }

        public void SetVisible(bool visible)
        {
            if (glow) glow.enabled = visible;
            if (outerGlow) outerGlow.enabled = visible;
            if (core) core.enabled = visible;
        }

        static void SetLine(LineRenderer line, Vector3 from, Vector3 to)
        {
            if (!line) return;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
        }
    }
}
