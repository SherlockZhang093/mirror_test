#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor.Level
{
    /// <summary>
    /// Lets automated project tooling request the same explicit builder without taking focus from Unity.
    /// The request lives in Library and is consumed exactly once.
    /// </summary>
    [InitializeOnLoad]
    static class Level02JungleMirrorGateBuildRequest
    {
        const string RequestPath = "Library/CodexLevel02JungleMirrorGate.request";

        static Level02JungleMirrorGateBuildRequest()
        {
            if (!File.Exists(RequestPath)) return;
            EditorApplication.delayCall += Consume;
        }

        static void Consume()
        {
            if (!File.Exists(RequestPath)) return;
            File.Delete(RequestPath);
            try
            {
                Level02JungleMirrorGateBuilder.BuildAndConnect();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
#endif
