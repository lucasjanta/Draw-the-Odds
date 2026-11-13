using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ItchDeploy.Editor
{
    public static class SceneValidator
    {
        private const string WarningPrefix = "Warning:";

        public static bool AutoValidate
        {
            get => EditorPrefs.GetBool("ItchDeploy_AutoValidateScenes", true);
            set => EditorPrefs.SetBool("ItchDeploy_AutoValidateScenes", value);
        }

        public static List<string> Validate()
        {
            var errors = new List<string>();
            var buildSettings = EditorBuildSettings.scenes;

            if (buildSettings.Length == 0)
            {
                errors.Add("No scenes in Build Settings");
                return errors;
            }

            foreach (var scene in buildSettings)
            {
                if (!scene.enabled) continue;

                if (string.IsNullOrEmpty(scene.path))
                {
                    errors.Add("Scene path is empty");
                    continue;
                }

                try
                {
                    var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
                    if (sceneAsset == null)
                        errors.Add($"Scene not found: {scene.path}");
                }
                catch
                {
                    errors.Add($"Invalid scene path: {scene.path}");
                }
            }

            if (buildSettings.Length == 1)
            {
                var scene = buildSettings[0];
                if (scene.path != null && scene.path.Contains("SampleScene"))
                    errors.Add(WarningPrefix + " Only SampleScene in build (test scene?)");
            }

            return errors;
        }

        public static bool IsWarning(string issue)
        {
            return !string.IsNullOrEmpty(issue) && issue.StartsWith(WarningPrefix);
        }

        public static List<string> ValidateSceneContent(Scene scene)
        {
            var allErrors = new List<string>();
            if (!scene.IsValid()) return allErrors;

            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                var allObjects = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in allObjects)
                {
                    var go = t.gameObject;
                    var path = GetScenePath(go);
                    var goErrors = CheckGameObject(go, path);

                    foreach (var e in goErrors)
                        allErrors.Add(e);

                    if (goErrors.Count > 0)
                        Debug.LogWarning($"[SceneValidator] {goErrors.Count} issue(s) on '{path}':\n" + string.Join("\n", goErrors), go);
                }
            }
            return allErrors;
        }

        private static List<string> CheckGameObject(GameObject go, string path)
        {
            var errors = new List<string>();
            var components = go.GetComponents<Component>();
            var compList = new List<Component>(components);

            foreach (var comp in compList)
            {
                if (comp == null)
                {
                    errors.Add($"Missing script");
                    continue;
                }

                var compName = comp.GetType().Name;

                if (compName == "Transform")
                {
                    var scale = go.transform.localScale;
                    if (Mathf.Approximately(scale.x, 0) || Mathf.Approximately(scale.y, 0) || Mathf.Approximately(scale.z, 0))
                        errors.Add("Zero scale on Transform");
                }

                if (comp is MeshFilter mf && mf.sharedMesh == null)
                    errors.Add($"Missing Mesh on MeshFilter");

                if (comp is MeshRenderer mr)
                {
                    if (mr.sharedMaterial == null)
                        errors.Add($"Missing Material on MeshRenderer");
                }

                if (comp is SkinnedMeshRenderer smr)
                {
                    if (smr.sharedMesh == null)
                        errors.Add($"Missing Mesh on SkinnedMeshRenderer");
                    if (smr.sharedMaterial == null)
                        errors.Add($"Missing Material on SkinnedMeshRenderer");
                }

                if (comp is AudioSource aus && aus.clip == null && aus.playOnAwake)
                    errors.Add($"Missing AudioClip on AudioSource (playOnAwake)");

                if (comp is Animator anim && anim.runtimeAnimatorController == null)
                    errors.Add($"Missing Animator Controller");

                if (comp is SpriteRenderer sr && sr.sprite == null)
                    errors.Add($"Missing Sprite on SpriteRenderer");

                if (comp is Light light)
                {
                    if (light.type == LightType.Directional && light.cookie != null)
                        errors.Add("Directional light with cookie (not supported)");
                }

                if (comp is MeshCollider mc && mc.sharedMesh == null)
                    errors.Add($"Missing Mesh on MeshCollider");

                if (comp is ParticleSystem ps)
                {
                    var main = ps.main;
                    if (main.scalingMode == ParticleSystemScalingMode.Local && Mathf.Approximately(ps.transform.localScale.magnitude, 0))
                        errors.Add("Zero scale on ParticleSystem");
                }

                if (comp is BoxCollider2D bc2d && bc2d.size == Vector2.zero)
                    errors.Add("Zero size BoxCollider2D");

                if (comp is CapsuleCollider2D cc2d && cc2d.size == Vector2.zero)
                    errors.Add("Zero size CapsuleCollider2D");

                if (comp is CircleCollider2D circ2d && Mathf.Approximately(circ2d.radius, 0))
                    errors.Add("Zero radius CircleCollider2D");

                        try
                {
                    var so = new SerializedObject(comp);
                    var prop = so.GetIterator();
                    while (prop.NextVisible(true))
                    {
                        if (prop.propertyType == SerializedPropertyType.ObjectReference &&
                            prop.objectReferenceValue == null &&
                            !IsOptionalField(prop.name))
                        {
                            try
                            {
                                errors.Add($"Missing ref in {compName}.{prop.displayName}");
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }

            if (PrefabUtility.IsPartOfPrefabInstance(go))
            {
                var prefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (prefab == null)
                    errors.Add("Broken prefab instance");
            }

            if (compList.Count == 1 && compList[0] is Transform)
            {
                if (go.transform.childCount == 0)
                    errors.Add("Empty GameObject (no components, no children)");
            }

            var duplicates = go.GetComponents<Component>()
                .GroupBy(c => c != null ? c.GetType() : null)
                .Where(g => g.Count() > 1 && g.Key != null &&
                    g.Key != typeof(Transform) &&
                    g.Key != typeof(RectTransform));
            foreach (var dup in duplicates)
                errors.Add($"Duplicate {dup.Key.Name}");

            return errors;
        }

        private static string GetScenePath(GameObject go)
        {
            var parts = new List<string>();
            var t = go.transform;
            while (t != null)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static readonly HashSet<string> OptionalFields = new()
        {
            "m_Script", "m_GameObject", "m_Transform", "m_Name", "m_Enabled",
            "m_EditorHideFlags", "m_EditorClassIdentifier",
            "m_ProbeAnchor", "m_LightProbeVolumeOverride", "m_LightmapParameters",
            "m_Material", "m_PhysicMaterial", "m_PhysicsMaterial",
            "m_TargetTexture",
            "m_VolumeTrigger", "m_VolumeMask",
            "m_FirstSelected", "m_XRTrackingOrigin",
            "m_Camera", "m_EventCamera",
            "m_CullingMask", "m_BackGroundColor",
            "m_Font", "m_FontMaterial",
            "m_Sprite", "m_SortingLayerID", "m_SortingOrder",
            "m_AdditionalShaderChannelsFlags",
            "m_Settings", "m_Gizmos", "m_Icon",
            "m_Controller", "m_AnimatorController",
            "m_Mesh", "m_SharedMesh", "m_StaticBatchRoot",
            "m_Cloth", "m_ParticleSystem", "m_AudioSource",
            "m_Motion", "m_Avatar", "m_ApplyRootMotion",
            "m_CollisionMode", "m_SendCollisionMessages",
        };

        private static bool IsOptionalField(string name)
        {
            if (OptionalFields.Contains(name)) return true;
            if (name.StartsWith("m_Previous") || name.StartsWith("m_PrePrefab")) return true;
            if (name.EndsWith("Cached")) return true;
            return false;
        }

        public static VisualElement CreateUI()
        {
            var root = new VisualElement();
            root.AddToClassList("settings-scene-card");

            var title = new Label(ItchDeployLocale._(
                "Scene validation", "Перевірка сцен"));
            title.AddToClassList("settings-card-title");
            root.Add(title);

            var help = new Label(ItchDeployLocale._(
                "Check Build Settings scenes before publishing and verify that every scene exists and is configured correctly.",
                "Перевіряє сцени з Build Settings перед публікацією: наявність файлів і коректність налаштування."));
            help.AddToClassList("settings-card-help");
            help.style.whiteSpace = WhiteSpace.Normal;
            root.Add(help);

            var autoToggleRow = ItchDeployUI.LabeledToggleRow(
                ItchDeployLocale._(
                    "Validate automatically before building",
                    "Перевіряти автоматично перед збіркою"),
                AutoValidate,
                out var autoToggle,
                "scene-auto-validate-toggle");
            autoToggle.RegisterValueChangedCallback(ev => AutoValidate = ev.newValue);
            root.Add(autoToggleRow);

            var actionGroup = new VisualElement();
            actionGroup.AddToClassList("settings-scene-actions");
            root.Add(actionGroup);

            var btn1 = new Button(() =>
            {
                var errors = Validate();
                var hasWarning = errors.Exists(IsWarning);
                var hasError = errors.Exists(e => !IsWarning(e));
                if (errors.Count == 0)
                    Debug.Log("[SceneValidator] All build scenes valid.");
                else if (hasWarning && !hasError)
                    Debug.LogWarning("[SceneValidator] " + string.Join("\n", errors));
                else
                    Debug.LogError("[SceneValidator] " + string.Join("\n", errors));
            });
            btn1.text = ItchDeployLocale._(
                "Validate build scenes", "Перевірити сцени збірки");
            btn1.AddToClassList("settings-wide-action");
            actionGroup.Add(btn1);

            var btn2 = new Button(() =>
            {
                var activeScene = SceneManager.GetActiveScene();
                if (!activeScene.IsValid())
                    Debug.LogError("[SceneValidator] No active scene open.");
                else if (activeScene.isDirty)
                    Debug.LogWarning("[SceneValidator] Scene has unsaved changes. Save first.");
                else
                {
                    var sceneErrors = ValidateSceneContent(activeScene);
                    if (sceneErrors.Count == 0)
                        Debug.Log($"[SceneValidator] Scene '{activeScene.name}' — no issues found.");
                    else
                        Debug.Log($"[SceneValidator] Scene '{activeScene.name}' — {sceneErrors.Count} total issue(s). Click warnings above to select GameObjects.");
                }
            });
            btn2.text = ItchDeployLocale._(
                "Validate open scene", "Перевірити відкриту сцену");
            btn2.AddToClassList("settings-wide-action");
            btn2.AddToClassList("settings-scene-action-secondary");
            actionGroup.Add(btn2);

            var sceneList = new VisualElement();
            sceneList.AddToClassList("settings-scene-list");
            root.Add(sceneList);

            var scenesLabel = new Label(ItchDeployLocale._(
                "Build scenes", "Сцени збірки"));
            scenesLabel.AddToClassList("settings-inline-heading");
            sceneList.Add(scenesLabel);

            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0)
            {
                var noScenes = new Label(ItchDeployLocale._(
                    "No scenes in Build Settings.",
                    "У Build Settings немає сцен."));
                noScenes.style.color = new StyleColor(Color.yellow);
                noScenes.AddToClassList("settings-scene-item");
                sceneList.Add(noScenes);
            }
            else
            {
                foreach (var scene in scenes)
                {
                    if (!scene.enabled) continue;
                    var sceneLabel = new Label(Path.GetFileNameWithoutExtension(scene.path));
                    sceneLabel.AddToClassList("settings-muted");
                    sceneLabel.AddToClassList("settings-scene-item");
                    sceneList.Add(sceneLabel);
                }
            }

            var openBtn = new Button(() =>
                EditorWindow.GetWindow(System.Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor")));
            openBtn.text = ItchDeployLocale._(
                "Open Build Settings", "Відкрити Build Settings");
            openBtn.AddToClassList("settings-wide-action");
            openBtn.AddToClassList("settings-scene-open-action");
            root.Add(openBtn);

            return root;
        }
    }
}
