using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YLFramework.Core;
using Object = UnityEngine.Object;

namespace YLFramework.Editor
{
    /// <summary>集成后显式执行的 UI Panel 资产迁移。不会自动运行、保存场景或改写非 UI 内容。</summary>
    public static class UIPanelAssetMigration
    {
        public const string BaseRootPath = "Assets/YLFramework/Runtime/Core/Resources/YLFramework/Core/Canvas.prefab";
        public const string TemplatePath = "Assets/YLFramework/Prefabs/YLFramework.prefab";
        public const string TipsPath = "Assets/YLFramework/Prefabs/UITips.prefab";
        public const string TipsVariantPath = "Assets/YLFramework/Prefabs/UITipsOverlay.prefab";
        public const string ColorPath = "Assets/YLFramework/Prefabs/ColorSelector.prefab";
        private static readonly string[] FixturePanelPaths =
        {
            "Assets/YLFramework/Tests/Core/Fixtures/YLCoreSmokeModule/YLCoreSmokePanel.prefab",
            "Assets/YLFramework/Tests/Core/Fixtures/YLCoreResourceModule/YLCoreSmokePanel.prefab"
        };

        [MenuItem("YLFramework/Core/UI Panel/核验资产迁移")]
        private static void ValidateMenu() => Debug.Log(ValidateAssets());

        /// <summary>只读预检现有资产、根和提示引用；允许尚未迁移，不把结构存在当成实例化验收通过。</summary>
        public static string ValidateAssets()
        {
            var basic = Required(BaseRootPath);
            var template = Required(TemplatePath);
            var tips = Required(TipsPath);
            var color = Required(ColorPath);
            foreach (var prefab in new[] { basic, template, tips, color }) Objects(prefab).ToArray();
            if (!color.GetComponent<ColorSelectorWindow>()) throw new InvalidOperationException("取色 Prefab 根不是正式 ColorSelectorWindow。");
            if (!basic.GetComponent<Canvas>() || !basic.GetComponent<CanvasScaler>() || !basic.GetComponent<GraphicRaycaster>())
                throw new InvalidOperationException("基础根缺少既有 Canvas/CanvasScaler/GraphicRaycaster，停止以免覆盖未知结构。");
            var host = template.GetComponent<YLFrameworkRoot>();
            var contexts = template.GetComponentsInChildren<UIRootContext>(true);
            if (!host || contexts.Length != 1) throw new InvalidOperationException("完整模板必须有唯一宿主和唯一实际 UI 根。");
            var context = contexts[0];
            if (!context.GetComponent<Canvas>() || !context.GetComponent<CanvasScaler>() || !context.GetComponent<GraphicRaycaster>())
                throw new InvalidOperationException("完整模板原生 Canvas 组件不完整。");
            ValidateLayers(context, false);
            if (new SerializedObject(context).FindProperty("uiLayers").arraySize <= 4) throw new InvalidOperationException("完整模板需要明确 Layer4 承载 Tips Variant，不自动扩层。");
            var presenters = template.GetComponentsInChildren<UITipsPresenter>(true);
            if (presenters.Length != 1 || !tips.GetComponent<UITipsItem>()) throw new InvalidOperationException("Tips 承载不明确，停止自动替换引用。");
            if (!presenters[0].transform.IsChildOf(context.transform)) throw new InvalidOperationException("Tips Presenter 不属于模板 UI 根，停止迁移。");
            var current = new SerializedObject(presenters[0]).FindProperty("uiTipItemPrefab").objectReferenceValue;
            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(TipsVariantPath);
            if (current != tips && (!variant || current != variant)) throw new InvalidOperationException("模板 Tips 使用自定义来源，请先人工确认，不覆盖其绑定。");
            if (variant && PrefabUtility.GetPrefabAssetType(variant) != PrefabAssetType.Variant)
                throw new InvalidOperationException("UITipsOverlay 路径已存在非 Variant，停止覆盖。");
            if (variant)
            {
                Objects(variant).ToArray();
                if (PrefabUtility.GetCorrespondingObjectFromSource(variant) != tips)
                    throw new InvalidOperationException("现有 Tips Variant 不是原 UITips 的直接 Variant，停止改变来源。");
            }
            return "UI Panel 资产预检完成。基础根：" + (basic.GetComponent<UIRootContext>() ? "已有根组件" : "待补显式 Layer0") +
                "；完整模板：" + (PrefabUtility.GetCorrespondingObjectFromSource(context.gameObject) == basic ? "已引用基础根" : "待转换基础根实例") +
                "；Tips Variant：" + (variant ? "已存在，仍需核验配置" : "待创建并由 Unity 生成 meta") +
                "。此结果不是 PlayMode、输入或 Player 验收。";
        }

        /// <summary>显式、可重复执行；先完整预检，再依次保存基础根、独立 Tips Variant 和完整模板。</summary>
        [MenuItem("YLFramework/Core/UI Panel/执行资产迁移")]
        public static void MigrateAssets()
        {
            EnsureCanMigrate();
            ValidateAssets();
            MigrateBase();
            MigrateTips();
            MigratePanel(ColorPath);
            MigrateTemplate();
            Debug.Log(ValidateAssets() + " 请核查资产 diff、生成的 meta、旧 fileID 和实例化结果，再执行引擎验收。");
        }

        /// <summary>仓库测试夹具的独立显式入口；正式框架导出不包含 Tests，不依赖该入口运行。</summary>
        [MenuItem("YLFramework/Core/UI Panel/迁移测试 Panel 夹具")]
        public static void MigrateTestFixtures()
        {
            EnsureCanMigrate();
            foreach (var path in FixturePanelPaths)
            {
                var fixture = Required(path);
                Objects(fixture).ToArray();
                if (!fixture.GetComponent<Panel>()) throw new InvalidOperationException("测试 Panel 脚本未导入或类型不匹配，请先完成测试程序集导入：" + path);
            }
            foreach (var path in FixturePanelPaths) MigratePanel(path);
            Debug.Log("测试 Panel 七字段已显式保存，仍需重载/实例化和运行测试验证。");
        }
        private static void EnsureCanMigrate()
        {
            if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("请退出 Play，等待编译/导入结束并关闭 Prefab Stage 后执行资产迁移。");
        }

        private static GameObject Required(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path) ??
            throw new InvalidOperationException("缺少现有资产，不创建替代身份：" + path);

        private static void BindRoot(UIRootContext context)
        {
            var serialized = new SerializedObject(context);
            serialized.FindProperty("canvas").objectReferenceValue = context.GetComponent<Canvas>();
            serialized.FindProperty("graphicRaycaster").objectReferenceValue = context.GetComponent<GraphicRaycaster>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void ValidateLayers(UIRootContext context, bool allowEmpty)
        {
            var layers = new SerializedObject(context).FindProperty("uiLayers");
            if (!allowEmpty && layers.arraySize == 0) throw new InvalidOperationException("完整模板没有既有层映射，停止猜测。");
            for (int i = 0; i < layers.arraySize; i++)
            {
                var layer = layers.GetArrayElementAtIndex(i);
                var root = layer.FindPropertyRelative("root").objectReferenceValue as Transform;
                var image = layer.FindPropertyRelative("maskImage").objectReferenceValue as Image;
                if (!root || !root.IsChildOf(context.transform)) throw new InvalidOperationException("层容器丢失或跨根。");
                if (layer.FindPropertyRelative("enableMask").boolValue && (!image || !image.transform.IsChildOf(root)))
                    throw new InvalidOperationException("模态层遮罩丢失或不属于层容器。");
            }
        }
        private static void MigrateBase()
        {
            var identity = CaptureIdentity(BaseRootPath);
            var contents = PrefabUtility.LoadPrefabContents(BaseRootPath);
            try
            {
                var context = contents.GetComponent<UIRootContext>();
                if (!context) context = contents.AddComponent<UIRootContext>();
                BindRoot(context);
                var serialized = new SerializedObject(context);
                var layers = serialized.FindProperty("uiLayers");
                if (layers.arraySize == 0)
                {
                    if (contents.transform.Find("Layer0")) throw new InvalidOperationException("已有未绑定 Layer0，请先核查，不能重复创建。");
                    var layer = new GameObject("Layer0", typeof(RectTransform));
                    layer.transform.SetParent(contents.transform, false);
                    Stretch((RectTransform)layer.transform);
                    layers.arraySize = 1;
                    var entry = layers.GetArrayElementAtIndex(0);
                    entry.FindPropertyRelative("root").objectReferenceValue = layer.transform;
                    entry.FindPropertyRelative("enableMask").boolValue = false;
                    entry.FindPropertyRelative("maskImage").objectReferenceValue = null;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                ValidateLayers(context, false);
                SaveChecked(contents, BaseRootPath, identity);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        private static void Stretch(RectTransform transform)
        {
            transform.anchorMin = Vector2.zero; transform.anchorMax = Vector2.one;
            transform.offsetMin = transform.offsetMax = Vector2.zero;
            transform.localScale = Vector3.one;
        }
        private static void MigrateTips()
        {
            var sourceIdentity = CaptureIdentity(TipsPath);
            var sourceContents = PrefabUtility.LoadPrefabContents(TipsPath);
            try { ConfigureTips(sourceContents.GetComponent<UITipsItem>(), false); SaveChecked(sourceContents, TipsPath, sourceIdentity); }
            finally { PrefabUtility.UnloadPrefabContents(sourceContents); }
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TipsVariantPath);
            if (existing)
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(existing) != Required(TipsPath))
                    throw new InvalidOperationException("现有 Tips Variant 不是原 UITips 的直接 Variant，不改变其来源。");
                var identity = CaptureIdentity(TipsVariantPath);
                var contents = PrefabUtility.LoadPrefabContents(TipsVariantPath);
                try { ConfigureTips(contents.GetComponent<UITipsItem>(), true); SaveChecked(contents, TipsVariantPath, identity); }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                return;
            }
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(Required(TipsPath), preview);
                ConfigureTips(instance.GetComponent<UITipsItem>(), true);
                if (!PrefabUtility.SaveAsPrefabAsset(instance, TipsVariantPath)) throw new InvalidOperationException("Tips Variant 保存失败。");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
        private static void MigratePanel(string path)
        {
            var identity = CaptureIdentity(path);
            var contents = PrefabUtility.LoadPrefabContents(path);
            try { WriteSettings(contents.GetComponent<Panel>(), PanelConfig.Default); SaveChecked(contents, path, identity); }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        private static void WriteSettings(Panel panel, PanelConfig value)
        {
            var serialized = new SerializedObject(panel);
            var settings = serialized.FindProperty("settings");
            settings.FindPropertyRelative("Layer").intValue = value.Layer;
            settings.FindPropertyRelative("Priority").intValue = value.Priority;
            settings.FindPropertyRelative("Modal").boolValue = value.Modal;
            settings.FindPropertyRelative("CoversRoot").boolValue = value.CoversRoot;
            settings.FindPropertyRelative("ParticipatesInTop").boolValue = value.ParticipatesInTop;
            settings.FindPropertyRelative("InstanceMode").enumValueIndex = (int)value.InstanceMode;
            settings.FindPropertyRelative("CacheOnClose").boolValue = value.CacheOnClose;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void ConfigureTips(UITipsItem tip, bool overlay)
        {
            if (!tip) throw new InvalidOperationException("Tips Variant 根缺少 UITipsItem。");
            if (!overlay) WriteSettings(tip, new PanelOverrides { ParticipatesInTop = false, InstanceMode = PanelInstanceMode.Multiple }.ApplyTo(PanelConfig.Default));
            else
            {
                var serialized = new SerializedObject(tip);
                var settings = serialized.FindProperty("settings");
                foreach (var field in new[] { "Priority", "Modal", "CoversRoot", "ParticipatesInTop", "InstanceMode", "CacheOnClose" })
                {
                    var property = settings.FindPropertyRelative(field);
                    if (property.prefabOverride) PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
                }
                serialized.Update();
                serialized.FindProperty("settings").FindPropertyRelative("Layer").intValue = 4;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var graphic in tip.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        }
        private static void MigrateTemplate()
        {
            var identity = CaptureIdentity(TemplatePath);
            var contents = PrefabUtility.LoadPrefabContents(TemplatePath);
            try
            {
                var context = contents.GetComponentsInChildren<UIRootContext>(true).Single();
                bool active = context.gameObject.activeSelf;
                context.gameObject.SetActive(false);
                if (!context.GetComponent<AutomaticAdaptationCanvasScaler>())
                {
                    var scaler = context.GetComponent<CanvasScaler>();
                    var resolution = scaler.referenceResolution;
                    var match = scaler.matchWidthOrHeight;
                    var adaptation = context.gameObject.AddComponent<AutomaticAdaptationCanvasScaler>();
                    adaptation.Configure(resolution, false, match);
                }
                var basic = Required(BaseRootPath);
                var source = PrefabUtility.GetCorrespondingObjectFromSource(context.gameObject);
                if (source != basic)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(context.gameObject))
                        throw new InvalidOperationException("完整模板根已有不同 Prefab 来源，不自动解包或重定向。");
                    // ByHierarchy 保留根和匹配组件，其余层、Tips、Camera 作为 added override 保留。
                    // 匹配组件记录原值 override，特别保护两份预设的 CanvasScaler/AutomaticAdaptation 配置差异。
                    PrefabUtility.ConvertToPrefabInstance(context.gameObject, basic, new ConvertToPrefabInstanceSettings
                    {
                        objectMatchMode = ObjectMatchMode.ByHierarchy,
                        recordPropertyOverridesOfMatches = true,
                        componentsNotMatchedBecomesOverride = true,
                        gameObjectsNotMatchedBecomesOverride = true,
                        changeRootNameToAssetName = false
                    }, InteractionMode.AutomatedAction);
                    context = contents.GetComponentsInChildren<UIRootContext>(true).Single();
                }
                BindRoot(context);
                ValidateLayers(context, false);
                var host = new SerializedObject(contents.GetComponent<YLFrameworkRoot>());
                host.FindProperty("panelRoot").objectReferenceValue = context;
                host.ApplyModifiedPropertiesWithoutUndo();
                var presenter = new SerializedObject(context.GetComponentsInChildren<UITipsPresenter>(true).Single());
                presenter.FindProperty("uiTipItemPrefab").objectReferenceValue = Required(TipsVariantPath);
                presenter.ApplyModifiedPropertiesWithoutUndo();
                context.gameObject.SetActive(active);
                SaveChecked(contents, TemplatePath, identity);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private sealed class Identity
        {
            internal string Guid;
            internal Dictionary<long, string> Objects;
            internal float[] Scaler;
        }
        private static IEnumerable<Object> Objects(GameObject root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                yield return transform.gameObject;
                foreach (var component in transform.GetComponents<Component>())
                { if (!component) throw new InvalidOperationException("资产有 Missing Script，迁移前必须先解决。"); yield return component; }
            }
        }
        private static Dictionary<long, string> FileIds(GameObject root)
        {
            var ids = new Dictionary<long, string>();
            foreach (var obj in Objects(root))
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out long id)) ids[id] = obj.GetType().FullName;
            return ids;
        }
        private static Identity CaptureIdentity(string path)
        {
            var prefab = Required(path);
            var scaler = prefab.GetComponentInChildren<CanvasScaler>(true);
            return new Identity { Guid = AssetDatabase.AssetPathToGUID(path), Objects = FileIds(prefab), Scaler = scaler ? ScaleValues(scaler) : null };
        }
        private static float[] ScaleValues(CanvasScaler scaler) => new[] { (float)scaler.uiScaleMode, scaler.referencePixelsPerUnit,
            scaler.scaleFactor, scaler.referenceResolution.x, scaler.referenceResolution.y, (float)scaler.screenMatchMode,
            scaler.matchWidthOrHeight, (float)scaler.physicalUnit, scaler.fallbackScreenDPI, scaler.defaultSpriteDPI, scaler.dynamicPixelsPerUnit };
        private static void SaveChecked(GameObject contents, string path, Identity before)
        {
            var scaler = contents.GetComponentInChildren<CanvasScaler>(true);
            if (before.Scaler != null)
            {
                if (!scaler) throw new InvalidOperationException("迁移丢失既有 CanvasScaler，拒绝保存：" + path);
                // ExecuteInEditMode 的适配组件可能在加载内容时更新 match；资产写入仍保留原生序列化配置。
                var values = before.Scaler;
                scaler.uiScaleMode = (CanvasScaler.ScaleMode)values[0]; scaler.referencePixelsPerUnit = values[1];
                scaler.scaleFactor = values[2]; scaler.referenceResolution = new Vector2(values[3], values[4]);
                scaler.screenMatchMode = (CanvasScaler.ScreenMatchMode)values[5]; scaler.matchWidthOrHeight = values[6];
                scaler.physicalUnit = (CanvasScaler.Unit)values[7]; scaler.fallbackScreenDPI = values[8];
                scaler.defaultSpriteDPI = values[9]; scaler.dynamicPixelsPerUnit = values[10];
                if (!ScaleValues(scaler).SequenceEqual(values)) throw new InvalidOperationException("无法保留 CanvasScaler 配置。");
            }
            if (!PrefabUtility.SaveAsPrefabAsset(contents, path)) throw new InvalidOperationException("Prefab 保存失败：" + path);
            var after = FileIds(Required(path));
            if (AssetDatabase.AssetPathToGUID(path) != before.Guid || before.Objects.Any(pair => !after.TryGetValue(pair.Key, out var type) || type != pair.Value))
                throw new InvalidOperationException("保存后 GUID/fileID 核验失败；迁移尚未验收，请检查该资产 diff，不要继续集成：" + path);
        }
    }
}
