using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using YLFramework.Character.Controller2D;
using YLFramework.Overlap;
using YLFramework.Platform2D;
using YLFramework.StateMachine;

namespace YLFramework.Tests.Extensions.Behavior
{
    public class BehaviorAuthoringTests
    {
        private const string Root = "Assets/YLFramework/Tests/Fixtures/Extensions/Behavior";
        private string folder;
        private readonly List<GameObject> owned = new List<GameObject>();

        [SetUp]
        public void Setup()
        {
            string name = "GeneratedTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder(Root, name);
            folder = Root + "/" + name;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var obj in owned) if (obj) UnityEngine.Object.DestroyImmediate(obj);
            owned.Clear();
            if (!string.IsNullOrEmpty(folder) && folder.StartsWith(Root + "/GeneratedTest_", StringComparison.Ordinal)) AssetDatabase.DeleteAsset(folder);
        }

        private RuntimeFSMController CreateAsset()
        {
            var asset = ScriptableObject.CreateInstance<RuntimeFSMController>();
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(folder + "/FSM.asset"));
            return asset;
        }

        [Test]
        public void StateTransitionHierarchyAndConditionGroupsSurviveNativeAssetSaveImport()
        {
            var asset = CreateAsset();
            var root = FSMStateNodeFactory.CreateStateNode(asset, "Root", new Rect(0, 0, 200, 40), true, true);
            FSMStateNodeFactory.CreateStateNode(asset, "Idle", new Rect(0, 100, 200, 40), true, false, new List<string> { root.name });
            FSMStateNodeFactory.CreateStateNode(asset, "Run", new Rect(200, 100, 200, 40), false, false, new List<string> { root.name });
            asset.AddParameters(new FSMParameterData { name = "Go", parameterType = ParameterType.Bool });
            FSMTransitionFactory.CreateTransition(asset, "Idle", "Run");
            var transition = asset.GetTransitionData("Idle", "Run");
            transition.conditions.Add(new FSMConditionData { parameterName = "Go", targetValue = 1 });
            transition.group_conditions.Add(new GroupCondition { conditions = new List<FSMConditionData> { new FSMConditionData { parameterName = "Go", targetValue = 0 } } });
            asset.Save();
            string path = AssetDatabase.GetAssetPath(asset);
            string guid = AssetDatabase.AssetPathToGUID(path);
            AssetDatabase.SaveAssetIfDirty(asset);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var reloaded = AssetDatabase.LoadAssetAtPath<RuntimeFSMController>(path);
            reloaded.ClearCache();
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(reloaded.GetDefaultStateNodeData("Root").name, Is.EqualTo("Idle"));
            Assert.That(reloaded.GetTransitionData("Idle", "Run").group_conditions[0].conditions[0].parameterName, Is.EqualTo("Go"));
            Assert.That(reloaded.ValidateConfiguration(out string error), Is.True, error);
        }

        [Test]
        public void RenamingStateAndItsTransitionsSupportsUndoRedo()
        {
            var asset = CreateAsset();
            var idle = FSMStateNodeFactory.CreateStateNode(asset, "Idle", new Rect(0, 0, 200, 40), true);
            FSMStateNodeFactory.CreateStateNode(asset, "Run", new Rect(200, 0, 200, 40), false);
            FSMTransitionFactory.CreateTransition(asset, "Idle", "Run");
            Undo.FlushUndoRecordObjects();
            Undo.ClearUndo(asset);
            Undo.IncrementCurrentGroup();
            Assert.That(FSMStateNodeFactory.Rename(asset, idle, "Rest"), Is.True);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            asset.ClearCache();
            Assert.That(asset.GetStateNodeData("Idle"), Is.Not.Null);
            Assert.That(asset.transitions[0].fromStateName, Is.EqualTo("Idle"));
            Undo.PerformRedo();
            asset.ClearCache();
            Assert.That(asset.GetStateNodeData("Rest"), Is.Not.Null);
            Assert.That(asset.transitions[0].fromStateName, Is.EqualTo("Rest"));
        }

        private static void CheckUndoRedo(RuntimeFSMController asset, Action change, Action before, Action after)
        {
            Undo.FlushUndoRecordObjects();
            Undo.ClearUndo(asset);
            Undo.IncrementCurrentGroup();
            before();
            change();
            Undo.FlushUndoRecordObjects();
            after();
            Undo.PerformUndo();
            asset.ClearCache();
            before();
            Undo.PerformRedo();
            asset.ClearCache();
            after();
        }

        [Test]
        public void ConditionValuesGroupsAndSerializedReorderingSupportUndoRedo()
        {
            var asset = CreateAsset();
            var untouched = CreateAsset();
            AssetDatabase.SaveAssetIfDirty(untouched);
            asset.states.Add(new FSMStateNodeData { name = "Idle", defaultState = true });
            asset.states.Add(new FSMStateNodeData { name = "Run" });
            asset.parameters.Add(new FSMParameterData { name = "Count", parameterType = ParameterType.Int });
            asset.transitions.Add(new FSMTransitionData { fromStateName = "Idle", toStateName = "Run" });
            Func<FSMTransitionData> transition = () => asset.GetTransitionData("Idle", "Run");
            CheckUndoRedo(asset, () => FSMTransitionFactory.SetAutoSwitch(asset, transition(), true),
                () => Assert.That(transition().AutoSwtich, Is.False), () => Assert.That(transition().AutoSwtich, Is.True));
            CheckUndoRedo(asset, () => FSMConditionFactory.AddGroup(asset, transition()),
                () => Assert.That(transition().group_conditions.Count, Is.Zero), () => Assert.That(transition().group_conditions.Count, Is.EqualTo(1)));
            Func<GroupCondition> group = () => transition().group_conditions[0];
            CheckUndoRedo(asset, () => FSMConditionFactory.AddGroupCondition(asset, group()),
                () => Assert.That(group().conditions.Count, Is.Zero), () => Assert.That(group().conditions.Count, Is.EqualTo(1)));
            Func<FSMConditionData> condition = () => group().conditions[0];
            float old = condition().targetValue;
            CheckUndoRedo(asset, () => FSMConditionFactory.SetTargetValue(asset, condition(), 7),
                () => Assert.That(condition().targetValue, Is.EqualTo(old)), () => Assert.That(condition().targetValue, Is.EqualTo(7)));
            var oldComparison = condition().compareType;
            CheckUndoRedo(asset, () => FSMConditionFactory.SetComparison(asset, condition(), CompareType.Less),
                () => Assert.That(condition().compareType, Is.EqualTo(oldComparison)), () => Assert.That(condition().compareType, Is.EqualTo(CompareType.Less)));
            CheckUndoRedo(asset, () => FSMConditionFactory.RemoveGroupCondition(asset, group(), 0),
                () => Assert.That(group().conditions.Count, Is.EqualTo(1)), () => Assert.That(group().conditions.Count, Is.Zero));
            CheckUndoRedo(asset, () => FSMConditionFactory.RemoveGroup(asset, transition(), group()),
                () => Assert.That(transition().group_conditions.Count, Is.EqualTo(1)), () => Assert.That(transition().group_conditions.Count, Is.Zero));

            transition().conditions.Add(new FSMConditionData { parameterName = "Count", targetValue = 1 });
            transition().conditions.Add(new FSMConditionData { parameterName = "Count", targetValue = 2 });
            transition().group_conditions.Add(new GroupCondition { conditions = new List<FSMConditionData>
            {
                new FSMConditionData { parameterName = "Count", targetValue = 1 },
                new FSMConditionData { parameterName = "Count", targetValue = 2 }
            } });
            asset.Save();
            foreach (bool nested in new[] { false, true })
            {
                Func<float> firstValue = () => nested ? transition().group_conditions[0].conditions[0].targetValue : transition().conditions[0].targetValue;
                CheckUndoRedo(asset, () =>
                {
                    using (var serialized = new SerializedObject(asset))
                    {
                        var property = serialized.FindProperty("transitions").GetArrayElementAtIndex(0);
                        if (nested) property = property.FindPropertyRelative("group_conditions").GetArrayElementAtIndex(0);
                        property.FindPropertyRelative("conditions").MoveArrayElement(0, 1);
                        serialized.ApplyModifiedProperties();
                    }
                    asset.ClearCache();
                }, () => Assert.That(firstValue(), Is.EqualTo(1)), () => Assert.That(firstValue(), Is.EqualTo(2)));
            }
            Assert.That(EditorUtility.IsDirty(untouched), Is.False);
        }

        [Test]
        public void MissingParameterDoesNotHideEditableEdgesOrBreakTheirInspectorLookup()
        {
            var asset = CreateAsset();
            asset.states.Add(new FSMStateNodeData { name = "Idle", defaultState = true });
            asset.states.Add(new FSMStateNodeData { name = "Run" });
            var edge = new FSMTransitionData { fromStateName = "Idle", toStateName = "Run" };
            edge.conditions.Add(new FSMConditionData());
            asset.transitions.Add(edge);
            Assert.That(asset.ValidateConfiguration(out _), Is.False);
            Assert.That(asset.GetCurrentShowTransitionData(string.Empty), Does.Contain(edge));
            Assert.That(asset.GetParameterData(null), Is.Null);
            Assert.That(asset.GetParameterData(string.Empty), Is.Null);
        }

        [Test]
        public void ActualScriptCreatorRefusesAnOccupiedUnimportedPath()
        {
            string directory = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "Behavior.Offline", "Creator_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory, "OccupiedUnimported.cs");
                File.WriteAllText(path, "class DifferentTypeName {}\n");
                byte[] before = File.ReadAllBytes(path);
                var create = typeof(FSMStateCreator).GetMethod("CreateFSMState", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                Assert.That(create.Invoke(null, new object[] { path, false, null, null }), Is.EqualTo(false));
                CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
            }
            finally { Directory.Delete(directory, true); }
        }

        public sealed class DrawerScopeWindow : EditorWindow
        {
            public FSMController Host;
            public bool Complete;
            public Exception Failure;
            private void OnGUI()
            {
                if (!Host || Complete) return;
                try
                {
                    using (var serialized = new SerializedObject(Host))
                    using (new EditorGUI.DisabledScope(true))
                    {
                        bool before = GUI.enabled;
                        var layers = serialized.FindProperty("_runtimeFSMControllerLayer");
                        var drawer = new CustomRuntimeFSMControllerLayer();
                        for (int i = 0; i < layers.arraySize; i++) drawer.OnGUI(new Rect(0, 25 * i, 400, 20), layers.GetArrayElementAtIndex(i), GUIContent.none);
                        if (GUI.enabled != before) throw new InvalidOperationException("Drawer consumed the caller's disabled scope.");
                    }
                }
                catch (Exception error) { Failure = error; }
                Complete = true;
            }
        }

        public sealed class GraphPreviewWindow : EditorWindow
        {
            public FSMStateGraphView Graph;
            public RuntimeFSMController Source => source;
            private Context previous;
            private RuntimeFSMController source;
            private System.Reflection.FieldInfo contextField;
            private readonly string[] integerKeys = { "YL.AssetFSMControllerInstanceID", "FSMControllerIndex" };
            private bool[] hadIntegerKeys;
            private int[] previousIntegers;
            private bool hadGuid;
            private string previousGuid;

            public void Initialize()
            {
                hadIntegerKeys = integerKeys.Select(EditorPrefs.HasKey).ToArray();
                previousIntegers = integerKeys.Select(key => EditorPrefs.GetInt(key)).ToArray();
                hadGuid = EditorPrefs.HasKey("YL.AssetFSMRuntimeFSMControllerGUID");
                previousGuid = EditorPrefs.GetString("YL.AssetFSMRuntimeFSMControllerGUID");
                foreach (string key in integerKeys) EditorPrefs.SetInt(key, 0);
                contextField = typeof(Context).GetField("_instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                previous = (Context)contextField.GetValue(null);
                var context = (Context)Activator.CreateInstance(typeof(Context), true);
                source = ScriptableObject.CreateInstance<RuntimeFSMController>();
                source.states.Add(new FSMStateNodeData { name = "Entry", isBuildInState = true, buildInStateName = "Entry", rect = new Rect(80, 280, 200, 40) });
                source.states.Add(new FSMStateNodeData { name = "Any State", isBuildInState = true, buildInStateName = "Any State", rect = new Rect(80, 80, 200, 40) });
                source.states.Add(new FSMStateNodeData { name = "Idle", defaultState = true, rect = new Rect(440, 280, 200, 40) });
                source.states.Add(new FSMStateNodeData { name = "Nested", isSubStateMachine = true, rect = new Rect(440, 80, 200, 40) });
                source.transitions.Add(new FSMTransitionData { fromStateName = "Any State", toStateName = "Nested" });
                typeof(Context).GetField("_runtimeFSMController", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(context, source);
                contextField.SetValue(null, context);
                Graph = new FSMStateGraphView();
                Graph.style.flexGrow = 1;
                rootVisualElement.Add(Graph);
            }

            private void OnDisable()
            {
                rootVisualElement.Clear();
                if (contextField != null) contextField.SetValue(null, previous);
                if (source) DestroyImmediate(source);
                if (hadIntegerKeys == null) return;
                for (int i = 0; i < integerKeys.Length; i++)
                    if (hadIntegerKeys[i]) EditorPrefs.SetInt(integerKeys[i], previousIntegers[i]); else EditorPrefs.DeleteKey(integerKeys[i]);
                if (hadGuid) EditorPrefs.SetString("YL.AssetFSMRuntimeFSMControllerGUID", previousGuid); else EditorPrefs.DeleteKey("YL.AssetFSMRuntimeFSMControllerGUID");
            }
        }

        [Test]
        public void GraphPreviewDoesNotResolveThePreviouslySelectedUserHost()
        {
            string[] keys = { "YL.AssetFSMControllerInstanceID", "FSMControllerIndex" };
            var had = keys.Select(EditorPrefs.HasKey).ToArray();
            var values = keys.Select(key => EditorPrefs.GetInt(key)).ToArray();
            string guidKey = "YL.AssetFSMRuntimeFSMControllerGUID";
            bool hadGuid = EditorPrefs.HasKey(guidKey);
            string guid = EditorPrefs.GetString(guidKey);
            var hostObject = new GameObject("OwnedPreviousGraphHost"); owned.Add(hostObject);
            var asset = CreateAsset();
            asset.states.Add(new FSMStateNodeData { name = "DoNotSnap", defaultState = true, rect = new Rect(3, 7, 173, 37) });
            var host = hostObject.AddComponent<FSMController>(); host.AddRuntimeFSMController(asset, "Previous");
            var window = ScriptableObject.CreateInstance<GraphPreviewWindow>();
            try
            {
                EditorPrefs.SetInt(keys[0], hostObject.GetInstanceID()); EditorPrefs.SetInt(keys[1], 3);
                window.ShowUtility();
                window.Initialize();
                Assert.That(Context.Instance.RuntimeFSMController, Is.SameAs(window.Source));
                Assert.That(window.Graph.StateNodes.ContainsKey("DoNotSnap"), Is.False);
                window.Close();
                Assert.That(EditorPrefs.GetInt(keys[0]), Is.EqualTo(hostObject.GetInstanceID()));
                Assert.That(EditorPrefs.GetInt(keys[1]), Is.EqualTo(3));
                Assert.That(asset.states[0].rect, Is.EqualTo(new Rect(3, 7, 173, 37)));
            }
            finally
            {
                if (window) window.Close();
                for (int i = 0; i < keys.Length; i++)
                    if (had[i]) EditorPrefs.SetInt(keys[i], values[i]); else EditorPrefs.DeleteKey(keys[i]);
                if (hadGuid) EditorPrefs.SetString(guidKey, guid); else EditorPrefs.DeleteKey(guidKey);
            }
        }

        [UnityTest]
        public IEnumerator GraphGridFillsViewportBehindContentAtDifferentZooms()
        {
            var window = ScriptableObject.CreateInstance<GraphPreviewWindow>();
            try
            {
                window.Initialize(); window.position = new Rect(120, 120, 760, 480); window.ShowUtility();
                for (int i = 0; i < 10; i++) { window.Repaint(); yield return null; }
                var graph = window.Graph;
                var grid = graph.Q<GridBackground>();
                Assert.That(grid, Is.Not.Null);
                Assert.That(grid.parent, Is.SameAs(graph), "Grid must be an unscaled GraphView background, not a content child.");
                foreach (float zoom in new[] { 0.5f, 1f, 2f })
                {
                    graph.UpdateViewTransform(new Vector3(30, 20), Vector3.one * zoom);
                    for (int i = 0; i < 4; i++) { window.Repaint(); yield return null; }
                    Assert.That(grid.layout.width, Is.EqualTo(graph.layout.width).Within(1));
                    Assert.That(grid.layout.height, Is.EqualTo(graph.layout.height).Within(1));
                    Assert.That(grid.worldBound.size, Is.EqualTo(graph.worldBound.size));
                    Assert.That(grid.customStyle.TryGetValue(new CustomStyleProperty<Color>("--line-color"), out var minor), Is.True, "The dark/light theme must define visible grid lines.");
                    Assert.That(grid.customStyle.TryGetValue(new CustomStyleProperty<Color>("--grid-background-color"), out var background), Is.True);
                    Assert.That(Mathf.Abs(minor.grayscale - background.grayscale) * minor.a, Is.GreaterThan(0.035f));
                }
            }
            finally { window.Close(); }
        }

        [UnityTest]
        public IEnumerator GraphTransitionOverlayUsesCrispWidthsAcrossZoomLevels()
        {
            var window = ScriptableObject.CreateInstance<GraphPreviewWindow>();
            try
            {
                window.Initialize(); window.position = new Rect(120, 120, 760, 480); window.ShowUtility();
                for (int i = 0; i < 10; i++) { window.Repaint(); yield return null; }
                var graph = window.Graph;
                var overlay = graph.Q<FSMTransitionBackground>();
                Assert.That(overlay.parent, Is.SameAs(graph));
                var width = typeof(FSMTransitionBackground).GetProperty("lineWidth", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(width, Is.Not.Null, "Transition widths need a bounded screen-space stroke instead of a wide feathered band.");
                foreach (float zoom in new[] { 0.1f, 0.5f, 1f, 3f })
                {
                    graph.UpdateViewTransform(new Vector3(20, 10), Vector3.one * zoom);
                    for (int i = 0; i < 4; i++) { window.Repaint(); yield return null; }
                    Assert.That(overlay.layout.width, Is.EqualTo(graph.layout.width).Within(1));
                    Assert.That(overlay.layout.height, Is.EqualTo(graph.layout.height).Within(1));
                    Assert.That((float)width.GetValue(overlay), Is.InRange(1f, 2.5f));
                }
            }
            finally { window.Close(); }
        }

        [UnityTest]
        public IEnumerator LayerDrawerPreservesCallerDisabledScopeAcrossMultipleRows()
        {
            var obj = new GameObject("YL.Behavior.DrawerHost");
            owned.Add(obj);
            var host = obj.AddComponent<FSMController>();
            host.AddRuntimeFSMController(CreateAsset(), "A");
            host.AddRuntimeFSMController(CreateAsset(), "B");
            var window = ScriptableObject.CreateInstance<DrawerScopeWindow>();
            try
            {
                window.Host = host;
                window.ShowUtility();
                window.Repaint();
                for (int i = 0; i < 20 && !window.Complete; i++) yield return null;
                Assert.That(window.Complete, Is.True, "GUI gate requires a live Editor window.");
                Assert.That(window.Failure, Is.Null);
            }
            finally { window.Close(); }
        }

        [Test]
        public void MainPlayModeSaveFencePreservesUnrelatedDirtyAsset()
        {
            var fence = Type.GetType("YLFramework.Behavior.Acceptance.BehaviorMainSaveFence, YLFramework.Behavior.Acceptance.Editor", false);
            Assert.That(fence, Is.Not.Null, "UTF PlayMode bootstrap must not save unrelated dirty assets.");
            var main = Type.GetType("YLFramework.Behavior.Acceptance.BehaviorMainAcceptance, YLFramework.Behavior.Acceptance.Editor", true);
            string key = (string)main.GetField("TestSaveGuardKey", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetRawConstantValue();
            string path = folder + "/Unrelated.mat";
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            AssetDatabase.CreateAsset(material, path); AssetDatabase.SaveAssetIfDirty(material);
            byte[] before = File.ReadAllBytes(path);
            bool old = SessionState.GetBool(key, false);
            SessionState.SetBool(key, true);
            try
            {
                material.name = "UnsavedUserValue"; EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before), "The UTF/global save must not write the unrelated material.");
                Assert.That(material.name, Is.EqualTo("UnsavedUserValue"));
                Assert.That(EditorUtility.IsDirty(material), Is.True);
            }
            finally { SessionState.SetBool(key, old); }
        }

        [Test]
        public void PlayerFixturePersistsGenericEventConsumersThroughNativeSaveAndReload()
        {
            var builder = Type.GetType("YLFramework.Behavior.Acceptance.BehaviorAcceptanceBuilder, YLFramework.Behavior.Acceptance.Editor", true);
            var create = builder.GetMethod("CreateEventsPrefab", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.That(create, Is.Not.Null, "The Player fixture needs a native serialized event consumer.");
            create.Invoke(null, new object[] { folder });
            string path = folder + "/BehaviorEvents.prefab";
            GameObject loaded = null;
            try
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path), Is.Not.Null, "The Player scene must consume serialized event listeners, not runtime AddListener substitutes.");
                string guid = AssetDatabase.AssetPathToGUID(path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                loaded = PrefabUtility.LoadPrefabContents(path);
                var receiverType = Type.GetType("YLFramework.Behavior.Acceptance.BehaviorEventReceiver, YLFramework.Behavior.Acceptance", true);
                var receiver = loaded.GetComponentInChildren(receiverType, true);
                Assert.That(receiver, Is.Not.Null);
                var motor = loaded.GetComponentInChildren<CharacterController2D>(true);
                var overlap = loaded.GetComponentInChildren<OverlapCircle2D>(true);
                var sphere = loaded.GetComponentInChildren<OverlapSphere3D>(true);
                var platform = loaded.GetComponentInChildren<Moveable>(true);
                var events = new UnityEventBase[] { motor.onGroundValueChange, motor.onTouchWallValueChange, motor.onTopValueChange,
                    motor.onJumpCountValueChange, motor.onVelocityValueChange, platform.onMove,
                    overlap.onColliderEnter, overlap.onColliderExit, sphere.onColliderEnter, sphere.onColliderExit };
                string[] methods = { "ReceiveBool", "ReceiveWall", "ReceiveTop", "ReceiveInt", "ReceiveVector2", "ReceiveVector3", "ReceiveHit", "ReceiveHit", "ReceiveHit3D", "ReceiveHit3D" };
                for (int i = 0; i < events.Length; i++)
                {
                    Assert.That(events[i].GetPersistentEventCount(), Is.EqualTo(1), methods[i]);
                    Assert.That(events[i].GetPersistentTarget(0), Is.EqualTo(receiver), methods[i]);
                    Assert.That(events[i].GetPersistentMethodName(0), Is.EqualTo(methods[i]));
                    Assert.That(events[i].GetPersistentListenerState(0), Is.EqualTo(UnityEventCallState.RuntimeOnly));
                }
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            }
            finally
            {
                if (loaded) PrefabUtility.UnloadPrefabContents(loaded);
            }
        }

        [Test]
        public void OverlapMotorAndMoveablePersistentEventsSurvivePrefabSaveReload()
        {
            var receiverType = Type.GetType("YLFramework.Behavior.Acceptance.BehaviorEventReceiver, YLFramework.Behavior.Acceptance", true);
            var obj = new GameObject("YL.Behavior.EventPrefab");
            obj.SetActive(false);
            owned.Add(obj);
            var receiver = obj.AddComponent(receiverType);
            var overlap = obj.AddComponent<OverlapCircle2D>();
            overlap.onColliderEnter = new OverlapEvent2D();
            overlap.onColliderExit = new OverlapEvent2D();
            var hit = (UnityAction<OverlapHit<Collider2D>>)Delegate.CreateDelegate(typeof(UnityAction<OverlapHit<Collider2D>>), receiver, "ReceiveHit");
            UnityEventTools.AddPersistentListener(overlap.onColliderEnter, hit);
            UnityEventTools.AddPersistentListener(overlap.onColliderExit, hit);
            Assert.That(overlap.onColliderEnter.GetPersistentListenerState(0), Is.EqualTo(UnityEventCallState.RuntimeOnly));
            // 本用例在EditMode实际Invoke；明确序列化为EditorAndRuntime，不把RuntimeOnly的未调用误判为丢失绑定。
            overlap.onColliderEnter.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
            overlap.onColliderExit.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
            obj.AddComponent<BoxCollider2D>();
            var motor = obj.AddComponent<CharacterController2D>();
            motor.onGroundValueChange = new YLFramework.Character.Controller2D.BoolUnityEvent();
            motor.onJumpCountValueChange = new IntUnityEvent();
            motor.onVelocityValueChange = new Vector2UnityEvent();
            UnityEventTools.AddPersistentListener(motor.onGroundValueChange, (UnityAction<bool>)Delegate.CreateDelegate(typeof(UnityAction<bool>), receiver, "ReceiveBool"));
            UnityEventTools.AddPersistentListener(motor.onJumpCountValueChange, (UnityAction<int>)Delegate.CreateDelegate(typeof(UnityAction<int>), receiver, "ReceiveInt"));
            UnityEventTools.AddPersistentListener(motor.onVelocityValueChange, (UnityAction<Vector2>)Delegate.CreateDelegate(typeof(UnityAction<Vector2>), receiver, "ReceiveVector2"));
            motor.onGroundValueChange.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
            motor.onJumpCountValueChange.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
            motor.onVelocityValueChange.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
            var moveable = obj.AddComponent<Moveable>();
            moveable.Positions.Add(Vector3.zero);
            moveable.Positions.Add(Vector3.right);
            moveable.onMove = new Vector3UnityEvent();
            UnityEventTools.AddPersistentListener(moveable.onMove, (UnityAction<Vector3>)Delegate.CreateDelegate(typeof(UnityAction<Vector3>), receiver, "ReceiveVector3"));
            moveable.onMove.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
            string path = folder + "/Events.prefab";
            Assert.That(PrefabUtility.SaveAsPrefabAsset(obj, path), Is.Not.Null);
            var loaded = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var loadedReceiver = loaded.GetComponent(receiverType);
                var loadedOverlap = loaded.GetComponent<OverlapCircle2D>();
                Assert.That(loadedOverlap.onColliderEnter.GetPersistentEventCount(), Is.EqualTo(1));
                Assert.That(loadedOverlap.onColliderEnter.GetPersistentListenerState(0), Is.EqualTo(UnityEventCallState.EditorAndRuntime));
                loadedOverlap.onColliderEnter.Invoke(default);
                loadedOverlap.onColliderExit.Invoke(default);
                var loadedMotor = loaded.GetComponent<CharacterController2D>();
                loadedMotor.onGroundValueChange.Invoke(true);
                loadedMotor.onJumpCountValueChange.Invoke(2);
                loadedMotor.onVelocityValueChange.Invoke(Vector2.one);
                loaded.GetComponent<Moveable>().onMove.Invoke(Vector3.one);
                var serialized = new SerializedObject(loadedReceiver);
                Assert.That(serialized.FindProperty("hitCount").intValue, Is.EqualTo(2));
                foreach (string name in new[] { "boolCount", "intCount", "vector2Count", "vector3Count" }) Assert.That(serialized.FindProperty(name).intValue, Is.EqualTo(1), name);
            }
            finally { PrefabUtility.UnloadPrefabContents(loaded); }
        }
    }
}
