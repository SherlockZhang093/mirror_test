#if UNITY_EDITOR
using System.IO;
using System.Linq;
using MirrorTrial.Combat;
using MirrorTrial.Level;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MirrorTrial.Editor.Level
{
    // Builds the deterministic, non-physics pulley mechanism from the existing Level 02 sprite sheet.
    public static class PulleyLiftPrefabBuilder
    {
        const string SheetPath = "Assets/MirrorTrial/Art/Level02_Jungle/Mechanisms/Platforms/jungle_modules_sheet_v1.png";
        const string RopeMiddlePath = "Assets/MirrorTrial/Art/Level02_Jungle/Mechanisms/Rope/rope_line_tile_v1.png";
        const string PrefabFolder = "Assets/MirrorTrial/Prefabs/Level/Mechanisms";
        const string PrefabPath = PrefabFolder + "/PulleyLift.prefab";
        const string RopeMaterialPath = PrefabFolder + "/PulleyRopeLine.mat";
        const string AnimationFolder = "Assets/MirrorTrial/Animations/Level/PulleyLift";
        const string ControllerPath = AnimationFolder + "/PulleyLift.controller";

        [InitializeOnLoadMethod]
        static void UpgradeLegacyRopePrefab()
        {
            EditorApplication.delayCall += () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var ropeMaterial = AssetDatabase.LoadAssetAtPath<Material>(RopeMaterialPath);
                var expectedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(RopeMiddlePath);
                if (prefab && prefab.GetComponentInChildren<PulleyRopeLineVisual>(true) &&
                    ropeMaterial && ropeMaterial.mainTexture == expectedTexture)
                    return;
                CreatePrefab();
            };
        }

        [MenuItem("MirrorTrial/Level 02/Create Pulley Lift Prefab")]
        public static void CreatePrefab()
        {
            PrepareRopeTexture(RopeMiddlePath);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().ToArray();
            var platformSprite = FindSprite(sprites, "jungle_modules_sheet_v1_11");
            var pulleySprite = FindSprite(sprites, "jungle_modules_sheet_v1_12");
            var weightSprite = FindSprite(sprites, "jungle_modules_sheet_v1_13");
            var ropeTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(RopeMiddlePath);
            if (!platformSprite || !pulleySprite || !weightSprite || !ropeTexture)
            {
                Debug.LogError("Pulley Lift: required sprites 11, 12 and 13 were not found in the jungle module sheet.");
                return;
            }

            EnsureFolder(PrefabFolder);
            var ropeMaterial = CreateRopeMaterial(ropeTexture);
            var root = new GameObject("PulleyLift");
            try
            {
                var controller = root.AddComponent<PulleyLiftController>();

                var markers = Child(root.transform, "MotionPoints");
                var platformRaised = Point(markers, "PlatformRaisedPoint", new Vector3(-0.9f, 1.25f, 0f));
                var platformLowered = Point(markers, "PlatformLoweredPoint", new Vector3(-0.9f, -2.5f, 0f));
                var weightRaised = Point(markers, "WeightRaisedPoint", new Vector3(1.25f, 1.15f, 0f));
                var weightLowered = Point(markers, "WeightLoweredPoint", new Vector3(1.25f, -2.6f, 0f));

                var pulley = SpriteObject(root.transform, "Pulley", pulleySprite, new Vector3(0f, 3.45f, 0f), 4);
                CenterSprite(pulley);
                var pulleyCenter = Point(root.transform, "PulleyCenter", new Vector3(0f, 3.45f, 0f));

                var platform = new GameObject("Platform");
                platform.transform.SetParent(root.transform, false);
                platform.transform.position = platformRaised.position;
                var platformBody = platform.AddComponent<Rigidbody2D>();
                platformBody.bodyType = RigidbodyType2D.Kinematic;
                platformBody.freezeRotation = true;
                platformBody.interpolation = RigidbodyInterpolation2D.Interpolate;
                var platformCollider = platform.AddComponent<BoxCollider2D>();
                platformCollider.size = new Vector2(3.75f, 0.42f);
                platformCollider.offset = new Vector2(0f, 0.27f);
                var platformVisual = SpriteObject(platform.transform, "Visual", platformSprite, Vector3.zero, 3);
                CenterSprite(platformVisual);

                var weight = new GameObject("Counterweight");
                weight.transform.SetParent(root.transform, false);
                weight.transform.localPosition = weightRaised.localPosition;
                var weightVisual = SpriteObject(weight.transform, "Visual", weightSprite, Vector3.zero, 3);
                CenterSprite(weightVisual);

                var leftBottom = Point(platform.transform, "LeftRopeAttach", new Vector3(0.28f, 0.36f, 0f));
                var rightWeightAttach = Point(weight.transform, "RightRopeAttach", new Vector3(-0.63f, 1.05f, 0f));

                var intactRope = LineRope(root.transform, "Rope_Intact", PulleyRopeLineVisual.RopePath.OverPulley,
                    leftBottom, rightWeightAttach, pulleyCenter, ropeMaterial);

                var hitTarget = new GameObject("RopeHitTarget");
                hitTarget.transform.SetParent(root.transform, false);
                hitTarget.transform.localPosition = new Vector3(0.94f, 1.9f, 0f);
                var hitCollider = hitTarget.AddComponent<BoxCollider2D>();
                hitCollider.isTrigger = true;
                hitCollider.size = new Vector2(0.55f, 2.2f);
                hitTarget.AddComponent<Hurtbox>();

                var brokenGroup = Child(root.transform, "Rope_BrokenTop").gameObject;
                var brokenEnd = Point(brokenGroup.transform, "BrokenTopEnd", new Vector3(0.68f, 2.55f, 0f));
                LineRope(brokenGroup.transform, "Line", PulleyRopeLineVisual.RopePath.OverPulley,
                    leftBottom, brokenEnd, pulleyCenter, ropeMaterial);

                var lowerBrokenEnd = Point(weight.transform, "BrokenLowerEnd", new Vector3(-0.63f, 1.55f, 0f));
                var brokenLowerRope = LineRope(weight.transform, "Rope_BrokenLower",
                    PulleyRopeLineVisual.RopePath.Straight, lowerBrokenEnd,
                    rightWeightAttach, null, ropeMaterial);
                brokenGroup.SetActive(false);
                brokenLowerRope.SetActive(false);

                var animatorController = CreateAnimationAssets(
                    platformRaised.localPosition, platformLowered.localPosition,
                    weightRaised.localPosition, weightLowered.localPosition);
                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = animatorController;
                animator.updateMode = AnimatorUpdateMode.AnimatePhysics;

                ConfigureController(controller, platformBody, platformRaised, platformLowered, weight.transform,
                    weightRaised, weightLowered, hitCollider, intactRope, brokenGroup,
                    brokenLowerRope, animator);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                Debug.Log("Created pulley lift prefab: " + PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void ConfigureController(PulleyLiftController controller, Rigidbody2D platformBody,
            Transform platformRaised, Transform platformLowered, Transform weight,
            Transform weightRaised, Transform weightLowered, Collider2D hitCollider,
            GameObject intactRope, GameObject brokenRope, GameObject brokenLowerRope, Animator animator)
        {
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("animator").objectReferenceValue = animator;
            serialized.FindProperty("useAnimator").boolValue = true;
            serialized.FindProperty("platformBody").objectReferenceValue = platformBody;
            serialized.FindProperty("platformRaisedPoint").objectReferenceValue = platformRaised;
            serialized.FindProperty("platformLoweredPoint").objectReferenceValue = platformLowered;
            serialized.FindProperty("counterweight").objectReferenceValue = weight;
            serialized.FindProperty("weightRaisedPoint").objectReferenceValue = weightRaised;
            serialized.FindProperty("weightLoweredPoint").objectReferenceValue = weightLowered;
            serialized.FindProperty("ropeHitCollider").objectReferenceValue = hitCollider;
            serialized.FindProperty("intactRopeVisual").objectReferenceValue = intactRope;
            serialized.FindProperty("brokenRopeVisual").objectReferenceValue = brokenRope;
            serialized.FindProperty("brokenLowerRopeVisual").objectReferenceValue = brokenLowerRope;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static AnimatorController CreateAnimationAssets(Vector3 platformRaised, Vector3 platformLowered,
            Vector3 weightRaised, Vector3 weightLowered)
        {
            EnsureFolder(AnimationFolder);
            var raisedPath = AnimationFolder + "/PulleyLift_Raised.anim";
            var dropPath = AnimationFolder + "/PulleyLift_Drop.anim";
            var loweredPath = AnimationFolder + "/PulleyLift_Lowered.anim";
            DeleteGeneratedAsset(ControllerPath);
            DeleteGeneratedAsset(raisedPath);
            DeleteGeneratedAsset(dropPath);
            DeleteGeneratedAsset(loweredPath);

            var raised = NewClip("PulleyLift_Raised");
            SetPosition(raised, "Platform", platformRaised, platformRaised, 0.01f);
            SetPosition(raised, "Counterweight", weightRaised, weightRaised, 0.01f);
            AssetDatabase.CreateAsset(raised, raisedPath);

            var lowered = NewClip("PulleyLift_Lowered");
            SetPosition(lowered, "Platform", platformLowered, platformLowered, 0.01f);
            SetPosition(lowered, "Counterweight", weightLowered, weightLowered, 0.01f);
            AssetDatabase.CreateAsset(lowered, loweredPath);

            var drop = NewClip("PulleyLift_Drop");
            SetPlatformDrop(drop, platformRaised, platformLowered);
            SetWeightSmash(drop, weightRaised, weightLowered);
            AnimationUtility.SetAnimationEvents(drop, new[]
            {
                new AnimationEvent { time = 0.45f, functionName = "OnAnimatedWeightImpact" },
                new AnimationEvent { time = 1.2f, functionName = "OnAnimatedDropFinished" }
            });
            AssetDatabase.CreateAsset(drop, dropPath);

            var animatorController = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            animatorController.AddParameter("TriggerDrop", AnimatorControllerParameterType.Trigger);
            var stateMachine = animatorController.layers[0].stateMachine;
            var raisedState = stateMachine.AddState("Raised");
            var dropState = stateMachine.AddState("Drop");
            var loweredState = stateMachine.AddState("Lowered");
            raisedState.motion = raised;
            dropState.motion = drop;
            loweredState.motion = lowered;
            stateMachine.defaultState = raisedState;
            var dropTransition = raisedState.AddTransition(dropState);
            dropTransition.hasExitTime = false;
            dropTransition.duration = 0f;
            dropTransition.AddCondition(AnimatorConditionMode.If, 0f, "TriggerDrop");
            var loweredTransition = dropState.AddTransition(loweredState);
            loweredTransition.hasExitTime = true;
            loweredTransition.exitTime = 1f;
            loweredTransition.duration = 0f;
            EditorUtility.SetDirty(animatorController);
            AssetDatabase.SaveAssets();
            return animatorController;
        }

        static AnimationClip NewClip(string name) => new AnimationClip
        {
            name = name,
            frameRate = 60f,
            wrapMode = WrapMode.ClampForever
        };

        [MenuItem("MirrorTrial/Level 02/Sync Pulley Raised Lowered From Drop")]
        public static void SyncRaisedLoweredFromDrop()
        {
            var drop = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                AnimationFolder + "/PulleyLift_Drop.anim");
            var raised = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                AnimationFolder + "/PulleyLift_Raised.anim");
            var lowered = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                AnimationFolder + "/PulleyLift_Lowered.anim");

            if (!drop || !raised || !lowered)
            {
                Debug.LogError("Pulley animation clips are missing; cannot sync endpoint poses.");
                return;
            }

            RemoveAccidentalChildMotion(drop);
            CopyEndpointPose(drop, raised, false);
            CopyEndpointPose(drop, lowered, true);
            EnsureDropFinishedEvent(drop);
            EditorUtility.SetDirty(drop);
            EditorUtility.SetDirty(raised);
            EditorUtility.SetDirty(lowered);
            AssetDatabase.SaveAssets();
            Debug.Log("Synced PulleyLift Raised/Lowered poses from the Drop clip endpoints.");
        }

        [InitializeOnLoadMethod]
        static void SyncEndpointPosesAfterScriptsReload()
        {
            EditorApplication.delayCall += () =>
            {
                const string sessionKey = "MirrorTrial.PulleyLift.EndpointPosesSynced.v4";
                if (SessionState.GetBool(sessionKey, false))
                    return;
                SessionState.SetBool(sessionKey, true);
                SyncRaisedLoweredFromDrop();
            };
        }

        static void CopyEndpointPose(AnimationClip source, AnimationClip destination, bool useLastKey)
        {
            foreach (var oldBinding in AnimationUtility.GetCurveBindings(destination))
                AnimationUtility.SetEditorCurve(destination, oldBinding, null);

            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                if (binding.path != "Platform" && binding.path != "Counterweight")
                    continue;
                var sourceCurve = AnimationUtility.GetEditorCurve(source, binding);
                if (sourceCurve == null || sourceCurve.length == 0)
                    continue;

                var value = sourceCurve.Evaluate(useLastKey ? source.length : 0f);
                var poseCurve = new AnimationCurve(
                    new Keyframe(0f, value, 0f, 0f),
                    new Keyframe(0.01f, value, 0f, 0f));
                AnimationUtility.SetEditorCurve(destination, binding, poseCurve);
            }

            AnimationUtility.SetAnimationEvents(destination, System.Array.Empty<AnimationEvent>());
        }

        static void RemoveAccidentalChildMotion(AnimationClip drop)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(drop))
            {
                if (binding.path == "Platform" || binding.path == "Counterweight")
                    continue;
                AnimationUtility.SetEditorCurve(drop, binding, null);
            }
        }

        static void EnsureDropFinishedEvent(AnimationClip drop)
        {
            var events = new System.Collections.Generic.List<AnimationEvent>(
                AnimationUtility.GetAnimationEvents(drop));
            events.RemoveAll(animationEvent =>
                animationEvent.functionName == "OnAnimatedDropFinished");
            events.Add(new AnimationEvent
            {
                time = drop.length,
                functionName = "OnAnimatedDropFinished"
            });
            AnimationUtility.SetAnimationEvents(drop, events.ToArray());
        }

        static void SetPlatformDrop(AnimationClip clip, Vector3 raised, Vector3 lowered)
        {
            SetCurve(clip, "Platform", "m_LocalPosition.x", new AnimationCurve(
                new Keyframe(0f, raised.x), new Keyframe(1.2f, lowered.x)));
            SetCurve(clip, "Platform", "m_LocalPosition.y", AnimationCurve.EaseInOut(
                0f, raised.y, 1.2f, lowered.y));
            SetCurve(clip, "Platform", "m_LocalPosition.z", new AnimationCurve(
                new Keyframe(0f, raised.z), new Keyframe(1.2f, lowered.z)));
        }

        static void SetWeightSmash(AnimationClip clip, Vector3 raised, Vector3 lowered)
        {
            SetCurve(clip, "Counterweight", "m_LocalPosition.x", new AnimationCurve(
                new Keyframe(0f, raised.x), new Keyframe(1.2f, lowered.x)));
            var y = new AnimationCurve(
                new Keyframe(0f, raised.y),
                new Keyframe(0.05f, raised.y),
                new Keyframe(0.45f, lowered.y - 0.08f),
                new Keyframe(0.54f, lowered.y + 0.1f),
                new Keyframe(0.65f, lowered.y),
                new Keyframe(1.2f, lowered.y));
            AnimationUtility.SetKeyRightTangentMode(y, 1, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyLeftTangentMode(y, 2, AnimationUtility.TangentMode.ClampedAuto);
            SetCurve(clip, "Counterweight", "m_LocalPosition.y", y);
            SetCurve(clip, "Counterweight", "m_LocalPosition.z", new AnimationCurve(
                new Keyframe(0f, raised.z), new Keyframe(1.2f, lowered.z)));
        }

        static void SetPosition(AnimationClip clip, string path, Vector3 from, Vector3 to, float duration)
        {
            SetCurve(clip, path, "m_LocalPosition.x", new AnimationCurve(
                new Keyframe(0f, from.x), new Keyframe(duration, to.x)));
            SetCurve(clip, path, "m_LocalPosition.y", new AnimationCurve(
                new Keyframe(0f, from.y), new Keyframe(duration, to.y)));
            SetCurve(clip, path, "m_LocalPosition.z", new AnimationCurve(
                new Keyframe(0f, from.z), new Keyframe(duration, to.z)));
        }

        static void SetCurve(AnimationClip clip, string path, string property, AnimationCurve curve)
        {
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }

        static void DeleteGeneratedAsset(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path))
                AssetDatabase.DeleteAsset(path);
        }

        static GameObject LineRope(Transform parent, string name, PulleyRopeLineVisual.RopePath path,
            Transform start, Transform end, Transform pulleyCenter, Material material)
        {
            var rope = new GameObject(name);
            rope.transform.SetParent(parent, false);
            rope.AddComponent<LineRenderer>();
            var visual = rope.AddComponent<PulleyRopeLineVisual>();
            var radius = path == PulleyRopeLineVisual.RopePath.OverPulley ? 0.49f : 0.68f;
            var width = path == PulleyRopeLineVisual.RopePath.OverPulley ? 0.2f : 0.12f;
            visual.Configure(path, start, end, pulleyCenter, material, radius, width);
            return rope;
        }

        static Material CreateRopeMaterial(Texture2D texture)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(RopeMaterialPath);
            if (!material)
            {
                material = new Material(Shader.Find("Sprites/Default")) { name = "PulleyRopeLine" };
                AssetDatabase.CreateAsset(material, RopeMaterialPath);
            }
            material.mainTexture = texture;
            material.color = Color.white;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void PrepareRopeTexture(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                return;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        static GameObject SpriteObject(Transform parent, string name, Sprite sprite, Vector3 position, int sortingOrder)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = position;
            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return gameObject;
        }

        static void CenterSprite(GameObject gameObject)
        {
            var renderer = gameObject.GetComponent<SpriteRenderer>();
            if (!renderer || !renderer.sprite)
                return;
            gameObject.transform.localPosition -= (Vector3)renderer.sprite.bounds.center;
        }

        static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        static Transform Point(Transform parent, string name, Vector3 position)
        {
            var point = Child(parent, name);
            point.localPosition = position;
            return point;
        }

        static Sprite FindSprite(Sprite[] sprites, string name) => sprites.FirstOrDefault(sprite => sprite.name == name);

        static void EnsureFolder(string path)
        {
            var current = "Assets";
            foreach (var part in path.Substring("Assets/".Length).Split('/'))
            {
                var next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }
    }
}
#endif
