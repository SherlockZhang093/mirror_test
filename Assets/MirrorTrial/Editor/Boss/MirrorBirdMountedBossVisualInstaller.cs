#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using MirrorTrial.Boss;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace MirrorTrial.Editor
{
    /// <summary>
    /// Installs the approved Mirror Bird visual hierarchy into the existing
    /// mounted-boss shell without changing its gameplay components.
    /// </summary>
    public static class MirrorBirdMountedBossVisualInstaller
    {
        private const string TargetPath =
            "Assets/MirrorTrial/Prefabs/Boss/MirrorArcherMountedBoss.prefab";
        private const string VisualPrefabPath =
            "Assets/MirrorTrial/Prefabs/Boss/MirrorBirdMountVisual.prefab";
        private const string InstalledRootName = "MirrorBirdMountVisual";
        private const string AnimationRoot = "Assets/MirrorTrial/Animations/Boss/AerialMount";
        private const string CombinedControllerPath = AnimationRoot + "/MirrorArcherMounted.controller";
        private const string MountPath = "MountVisual";
        private const string RiderPath = "RiderSeatAnchor/RiderVisual";
        private const string RiderSeatPath = "RiderSeatAnchor";
        private const string MountedRiderSheetPath =
            "Assets/MirrorTrial/Art/Boss/AerialMount/Sheets/MirrorHunter_MountedRider.png";

        [MenuItem("MirrorTrial/Boss/Install Mirror Bird Into Mounted Boss")]
        public static void InstallFromMenu()
        {
            Install();
        }

        private static void Install()
        {
            GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
            if (visualPrefab == null)
            {
                throw new InvalidOperationException("Missing Mirror Bird visual prefab: " + VisualPrefabPath);
            }

            AnimatorController combinedController = BuildCombinedController();

            GameObject root = PrefabUtility.LoadPrefabContents(TargetPath);
            try
            {
                Transform mountRoot = root.transform.Find("VisualRoot/MountRoot");
                if (mountRoot == null)
                {
                    throw new InvalidOperationException(
                        "Mounted boss is missing VisualRoot/MountRoot: " + TargetPath);
                }

                Transform previous = mountRoot.Find(InstalledRootName);
                if (previous != null)
                {
                    UnityEngine.Object.DestroyImmediate(previous.gameObject);
                }

                GameObject instance = PrefabUtility.InstantiatePrefab(visualPrefab, mountRoot) as GameObject;
                if (instance == null)
                {
                    throw new InvalidOperationException("Could not instantiate Mirror Bird visual prefab");
                }

                instance.name = InstalledRootName;
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                PrefabUtility.UnpackPrefabInstance(
                    instance,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);

                foreach (Animator childAnimator in instance.GetComponentsInChildren<Animator>(true))
                {
                    UnityEngine.Object.DestroyImmediate(childAnimator);
                }

                Transform riderSeat = instance.transform.Find(RiderSeatPath);
                SpriteRenderer riderVisual = instance.transform.Find(RiderPath)?.GetComponent<SpriteRenderer>();
                if (riderSeat == null || riderVisual == null)
                    throw new InvalidOperationException("Mirror Bird visual is missing the seated rider hierarchy");
                // v3 was authored against seat pixel (108,72) on the 192x128 bird canvas.
                riderSeat.localPosition = new Vector3(0.375f, -0.1875f, 0f);
                riderSeat.localRotation = Quaternion.identity;
                riderVisual.color = new Color(0.7375149f, 1f, 0.66981125f, 1f);

                Animator animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = combinedController;
                animator.applyRootMotion = false;
                if (!instance.GetComponent<MirrorArcherMountedVisualOrientation>())
                    instance.AddComponent<MirrorArcherMountedVisualOrientation>();

                var temporaryVisual = mountRoot.parent.GetComponent<MirrorArcherTemporaryVisual>();
                if (temporaryVisual != null)
                {
                    UnityEngine.Object.DestroyImmediate(temporaryVisual);
                }

                var actor = root.GetComponent<MirrorArcherMountedBoss>();
                if (actor != null)
                {
                    var actorData = new SerializedObject(actor);
                    actorData.FindProperty("animator").objectReferenceValue = animator;
                    actorData.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, TargetPath);
                Debug.Log("[MirrorBirdMountedBossVisualInstaller] Installed visual layers into " + TargetPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static AnimatorController BuildCombinedController()
        {
            ConfigureFixedMountSheet("AirIdle", 6);
            ConfigureFixedMountSheet("HorizontalFlight", 6);
            ConfigureFixedMountSheet("Turn", 8);
            ConfigureFixedRiderSheet(54);

            Sprite[] airIdle = LoadMountSprites("AirIdle", 6);
            Sprite[] flight = LoadMountSprites("HorizontalFlight", 6);
            Sprite[] turn = LoadMountSprites("Turn", 8);
            Sprite[] dive = LoadMountSprites("Dive", 6);
            Sprite[] hit = LoadMountSprites("Hit", 4);
            Sprite[] impact = LoadMountSprites("ImpactShatter", 8);
            Sprite[] mountedRider = LoadMountedRiderSprites(54);
            // The sheet contains up, level and down rows of 18 frames each.
            // Level frame 7 is the first stable seated aim pose.
            Sprite seatedAim = mountedRider[18 + 7];
            Vector2[] idleSeat = FixedSeat(airIdle.Length);
            // Integer canvas coordinates following the saddle/torso through the turn.
            Vector2[] turnSeat = SeatTrack(
                (108, 72), (108, 72), (106, 71), (99, 69),
                (93, 69), (86, 71), (84, 72), (84, 72));
            // Hit curls the torso inward before returning to the authored side pose.
            Vector2[] hitSeat = SeatTrack((108, 72), (105, 73), (101, 72), (108, 72));
            int[] bowTimeline = Enumerable.Range(0, 7)
                .Concat(Enumerable.Range(11, 5)).ToArray();
            Sprite[] seatedBowFire = bowTimeline.Select(index => mountedRider[18 + index]).ToArray();

            var clips = new List<AnimationClip>
            {
                BuildCombinedClip(
                    "MountedIdle", 8f, true,
                    airIdle,
                    Enumerable.Repeat(seatedAim, airIdle.Length).ToArray(),
                    idleSeat,
                    new float[airIdle.Length]),
                BuildCombinedClip(
                    "MountedFlight", 10f, true,
                    flight,
                    Enumerable.Repeat(seatedAim, flight.Length).ToArray(),
                    FixedSeat(flight.Length),
                    new float[flight.Length]),
                BuildCombinedClip(
                    "MountedTurn", 16f, false,
                    turn,
                    Enumerable.Repeat(seatedAim, turn.Length).ToArray(),
                    turnSeat,
                    new float[turn.Length]),
                BuildCombinedClip(
                    "MountedBowFire", 12f, false,
                    Enumerable.Range(0, seatedBowFire.Length)
                        .Select(i => airIdle[Mathf.FloorToInt(i * airIdle.Length / (float)seatedBowFire.Length) % airIdle.Length]).ToArray(),
                    seatedBowFire,
                    FixedSeat(seatedBowFire.Length),
                    new float[seatedBowFire.Length]),
                BuildCombinedClip(
                    "MountedDive", 10f, false,
                    dive,
                    Enumerable.Repeat(seatedAim, dive.Length).ToArray(),
                    FixedSeat(dive.Length),
                    new float[dive.Length]),
                BuildCombinedClip(
                    "MountedDiveRecover", 10f, false,
                    flight,
                    Enumerable.Repeat(seatedAim, flight.Length).ToArray(),
                    FixedSeat(flight.Length),
                    new float[flight.Length]),
                BuildCombinedClip(
                    "MountedHit", 12f, false,
                    hit,
                    Enumerable.Repeat(seatedAim, hit.Length).ToArray(),
                    hitSeat,
                    new float[hit.Length]),
                BuildCombinedClip(
                    "MountedFall", 10f, false,
                    hit,
                    Enumerable.Repeat(seatedAim, hit.Length).ToArray(),
                    hitSeat,
                    new float[hit.Length]),
                BuildCombinedClip(
                    "MountedFallLoop", 10f, true,
                    dive,
                    Enumerable.Repeat(seatedAim, dive.Length).ToArray(),
                    FixedSeat(dive.Length),
                    new float[dive.Length]),
                BuildCombinedClip(
                    "MountedImpact", 12f, false,
                    impact,
                    Enumerable.Repeat(seatedAim, impact.Length).ToArray(),
                    FixedSeat(impact.Length),
                    new float[impact.Length]),
            };

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CombinedControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(CombinedControllerPath);
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            stateMachine.states = Array.Empty<ChildAnimatorState>();
            stateMachine.anyStateTransitions = Array.Empty<AnimatorStateTransition>();
            stateMachine.entryTransitions = Array.Empty<AnimatorTransition>();
            foreach (AnimatorState staleState in AssetDatabase.LoadAllAssetsAtPath(CombinedControllerPath)
                         .OfType<AnimatorState>().ToArray())
                UnityEngine.Object.DestroyImmediate(staleState, true);
            for (int index = 0; index < clips.Count; index++)
            {
                AnimationClip clip = clips[index];
                AnimatorState state = stateMachine.AddState(clip.name,
                    new Vector3(220f + (index % 4) * 220f, 80f + (index / 4) * 100f));
                state.motion = clip;
                state.writeDefaultValues = true;
                if (clip.name == "MountedIdle")
                {
                    stateMachine.defaultState = state;
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void ConfigureFixedMountSheet(string sequence, int frameCount)
        {
            string path = "Assets/MirrorTrial/Art/Boss/AerialMount/Sheets/MirrorBird_" + sequence + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("Missing mount sheet importer: " + path);
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 32f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // The legacy TextureImporter.spritesheet API writes internalID 0 for
            // newly appended slices in current Unity versions. Those frames then
            // become null Sprite references at runtime, making the bird vanish.
            // The Sprite Editor data-provider API assigns and records a stable
            // GUID for every slice, including frames beyond the old sheet count.
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();

            Dictionary<string, SpriteRect> existingByName = dataProvider.GetSpriteRects()
                .GroupBy(rect => rect.name)
                .ToDictionary(group => group.Key, group => group.First());
            SpriteRect[] spriteRects = Enumerable.Range(0, frameCount).Select(index =>
            {
                string spriteName = "MirrorBird_" + sequence + "_" + index;
                UnityEditor.GUID spriteId = UnityEditor.GUID.Generate();
                if (existingByName.TryGetValue(spriteName, out SpriteRect existing) &&
                    IsUsableSpriteId(existing.spriteID))
                {
                    spriteId = existing.spriteID;
                }

                return new SpriteRect
                {
                    name = spriteName,
                    rect = new Rect(index * 192f, 0f, 192f, 128f),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(0.5f, 0.484375f),
                    spriteID = spriteId,
                };
            }).ToArray();

            dataProvider.SetSpriteRects(spriteRects);
            ISpriteNameFileIdDataProvider nameFileIdProvider =
                dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameFileIdProvider.SetNameFileIdPairs(spriteRects
                .Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            dataProvider.Apply();
            importer.SaveAndReimport();
        }

        private static void ConfigureFixedRiderSheet(int frameCount)
        {
            var importer = AssetImporter.GetAtPath(MountedRiderSheetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Missing mounted rider sheet: " + MountedRiderSheetPath);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 32f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            Dictionary<string, SpriteRect> existing = provider.GetSpriteRects()
                .GroupBy(rect => rect.name).ToDictionary(group => group.Key, group => group.First());
            SpriteRect[] rects = Enumerable.Range(0, frameCount).Select(index =>
            {
                string spriteName = "MirrorHunter_MountedRider_" + index;
                UnityEditor.GUID spriteId = UnityEditor.GUID.Generate();
                if (existing.TryGetValue(spriteName, out SpriteRect previous) &&
                    IsUsableSpriteId(previous.spriteID)) spriteId = previous.spriteID;
                int row = index / 18;
                int column = index % 18;
                return new SpriteRect
                {
                    name = spriteName,
                    rect = new Rect(column * 96f, (2 - row) * 84f, 96f, 84f),
                    alignment = SpriteAlignment.Custom,
                    // The authored pelvis/root is pixel (48,68) from top-left.
                    pivot = new Vector2(0.5f, 16f / 84f),
                    spriteID = spriteId,
                };
            }).ToArray();
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
        }

        private static bool IsUsableSpriteId(UnityEditor.GUID spriteId)
        {
            string value = spriteId.ToString();
            // Unity serializes a missing SpriteRect ID with this sentinel. It is
            // not the all-zero GUID, so a simple default/empty check misses it.
            return value.Trim('0').Length > 0 &&
                value != "00000000000000000800000000000000";
        }

        private static AnimationClip BuildCombinedClip(
            string name,
            float fps,
            bool loop,
            IReadOnlyList<Sprite> mountSprites,
            IReadOnlyList<Sprite> riderSprites,
            IReadOnlyList<Vector2> riderSeatPixels,
            IReadOnlyList<float> riderAngles)
        {
            if (mountSprites.Count != riderSprites.Count ||
                mountSprites.Count != riderSeatPixels.Count ||
                mountSprites.Count != riderAngles.Count)
            {
                throw new InvalidOperationException(name + " visual tracks have mismatched frame counts");
            }

            string path = AnimationRoot + "/" + name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = name };
                AssetDatabase.CreateAsset(clip, path);
            }
            else
            {
                clip.ClearCurves();
                clip.name = name;
            }

            clip.frameRate = fps;
            SetSpriteCurve(clip, MountPath, mountSprites, fps);
            SetSpriteCurve(clip, RiderPath, riderSprites, fps);
            SetSteppedFloatCurve(clip, RiderSeatPath, "m_LocalPosition.x",
                riderSeatPixels.Select(pixel => (pixel.x - 96f) / 32f).ToArray(), fps);
            SetSteppedFloatCurve(clip, RiderSeatPath, "m_LocalPosition.y",
                riderSeatPixels.Select(pixel => (66f - pixel.y) / 32f).ToArray(), fps);

            var angleBinding = EditorCurveBinding.FloatCurve(RiderSeatPath, typeof(Transform), "localEulerAnglesRaw.z");
            var angleKeys = new Keyframe[riderAngles.Count];
            for (int i = 0; i < angleKeys.Length; i++)
            {
                angleKeys[i] = new Keyframe(i / fps, riderAngles[i]);
            }
            AnimationUtility.SetEditorCurve(clip, angleBinding, new AnimationCurve(angleKeys));

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.stopTime = mountSprites.Count / fps;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static Vector2[] FixedSeat(int count)
        {
            return Enumerable.Repeat(new Vector2(108f, 72f), count).ToArray();
        }

        private static Vector2[] SeatTrack(params (int x, int y)[] pixels)
        {
            return pixels.Select(pixel => new Vector2(pixel.x, pixel.y)).ToArray();
        }

        private static void SetSteppedFloatCurve(
            AnimationClip clip, string path, string property, IReadOnlyList<float> values, float fps)
        {
            var binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), property);
            var curve = new AnimationCurve(values.Select((value, index) =>
                new Keyframe(index / fps, value)).ToArray());
            for (int index = 0; index < curve.length; index++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.Constant);
            }
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        private static void SetSpriteCurve(
            AnimationClip clip,
            string objectPath,
            IReadOnlyList<Sprite> sprites,
            float fps)
        {
            var binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = objectPath,
                propertyName = "m_Sprite",
            };
            var keys = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
            }
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        }

        private static Sprite[] LoadMountSprites(string sequence, int expected)
        {
            string path = "Assets/MirrorTrial/Art/Boss/AerialMount/Sheets/MirrorBird_" + sequence + ".png";
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(sprite => NumericSuffix(sprite.name))
                .ToArray();
            if (sprites.Length != expected)
            {
                throw new InvalidOperationException(sequence + " expected " + expected + " sprites, found " + sprites.Length);
            }
            return sprites;
        }

        private static Sprite[] LoadMountedRiderSprites(int expected)
        {
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(MountedRiderSheetPath)
                .OfType<Sprite>().OrderBy(sprite => NumericSuffix(sprite.name)).ToArray();
            if (sprites.Length != expected)
                throw new InvalidOperationException("Mounted rider expected " + expected +
                    " sprites, found " + sprites.Length);
            return sprites;
        }

        private static int NumericSuffix(string value)
        {
            int separator = value.LastIndexOf('_');
            return separator >= 0 && int.TryParse(value.Substring(separator + 1), out int result)
                ? result
                : int.MaxValue;
        }
    }
}
#endif
