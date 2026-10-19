using System;
using System.Collections;
using UnityEngine;
using YLFramework.Character.Controller2D;
using YLFramework.FSM;
using YLFramework.Overlap;
using YLFramework.Platform2D;
using YLFramework.StateMachine;

namespace YLFramework.Behavior.Acceptance
{
    // 仅由专属验收场景使用；无自动启动、全局注册或产品场景引用。
    public sealed class BehaviorPlayerProbe : MonoBehaviour
    {
        public RuntimeFSMController source;
        public string sourceGuid;
        public bool expectedDevelopment;
        private FSM<int> lightweight;
        private int stateCreates, stateEnters, stateExits;
        private string result = "Not run";

        public void RecordState(string phase)
        {
            if (phase == "create") stateCreates++;
            else if (phase == "enter") stateEnters++;
            else stateExits++;
        }

        private GameObject Own(string name, Vector3 position, bool active = true)
        {
            var obj = new GameObject("YL.Acceptance." + name);
            obj.transform.SetParent(transform, false);
            obj.transform.position = position;
            obj.SetActive(active);
            return obj;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private IEnumerator Start()
        {
            var run = Run();
            Exception failure = null;
            while (true)
            {
                bool hasNext;
                try { hasNext = run.MoveNext(); }
                catch (Exception exception) { failure = exception; break; }
                if (!hasNext) break;
                yield return run.Current;
            }
            result = failure == null ? "PASS" : "FAIL: " + failure.Message;
#if ENABLE_IL2CPP
            const string backend = "IL2CPP";
#else
            const string backend = "Mono";
#endif
            string report = $"YL_BEHAVIOR_ACCEPTANCE result={result}; unity={Application.unityVersion}; backend={backend}; development={Debug.isDebugBuild}; sourceGuid={sourceGuid}; creates={stateCreates}; enters={stateEnters}; exits={stateExits}";
            if (failure == null) Debug.Log(report);
            else { Debug.LogError(report); Debug.LogException(failure); }
            if (!Application.isEditor) Application.Quit(failure == null ? 0 : 1);
        }

        private IEnumerator Run()
        {
            Require(source && !string.IsNullOrEmpty(sourceGuid), "Generate the dedicated fixture asset and scene before running.");
            if (!Application.isEditor) Require(Debug.isDebugBuild == expectedDevelopment, "Player build flavor differs from the generated fixture.");
            var origin = new Vector3(3200, 3200);
            var target = Own("OverlapTarget", origin).AddComponent<BoxCollider2D>();
            var query = Own("Overlap", origin).AddComponent<OverlapCircle2D>();
            query.Running = false;
            query.Radius = 2;
            query.LayerMask = 1;
            query.onColliderEnter = new OverlapEvent2D();
            query.onColliderExit = new OverlapEvent2D();
            int enters = 0, exits = 0;
            query.onColliderEnter.AddListener(_ => enters++);
            query.onColliderExit.AddListener(_ => exits++);
            Physics2D.SyncTransforms();
            query.CheckOverlap();
            Require(query.Results.Count == 1 && enters == 1, "Overlap2D enter/Results failed.");
            target.transform.position += Vector3.up * 10;
            Physics2D.SyncTransforms();
            query.CheckOverlap();
            Require(query.Results.Count == 0 && exits == 1, "Overlap2D exit failed.");

            var spherePosition = origin + Vector3.forward * 20;
            Own("3DTarget", spherePosition).AddComponent<BoxCollider>();
            var sphere = Own("Sphere", spherePosition).AddComponent<OverlapSphere3D>();
            sphere.Running = false;
            sphere.Radius = 2;
            sphere.LayerMask = 1;
            Physics.SyncTransforms();
            sphere.CheckOverlap();
            Require(sphere.Results.Count == 1, "Overlap3D failed.");

            var motorPosition = origin + Vector3.right * 5;
            var floor = Own("Floor", motorPosition + Vector3.down * 1.5f).AddComponent<BoxCollider2D>();
            floor.size = new Vector2(4, 1);
            var motorObject = Own("Motor", motorPosition, false);
            motorObject.AddComponent<Rigidbody2D>().gravityScale = 2;
            motorObject.AddComponent<BoxCollider2D>().size = new Vector2(1, 2);
            var motor = motorObject.AddComponent<CharacterController2D>();
            motorObject.SetActive(true);

            var platformPosition = origin + Vector3.right * 12;
            var platformObject = Own("Platform", platformPosition, false);
            platformObject.AddComponent<BoxCollider2D>().size = new Vector2(4, 1);
            var platform = platformObject.AddComponent<MoveablePlatform2D>();
            platform.Positions.Add(platformPosition);
            platform.Positions.Add(platformPosition + Vector3.right);
            platform.Speed = 2;
            platform.MoveType = MoveType.Once;
            var catcher = platformObject.GetComponent<PlatformCatcher2D>();
            catcher.areas = new[] { new Area { from = new Vector3(-1.5f, 1), to = new Vector3(1.5f, 1) } };
            var passenger = Own("Passenger", platformPosition + Vector3.up);
            var passengerBody = passenger.AddComponent<Rigidbody2D>();
            passengerBody.gravityScale = 0;
            passenger.AddComponent<BoxCollider2D>();
            passenger.AddComponent<CircleCollider2D>().radius = 0.25f;
            var passengerStart = passengerBody.position;
            platform.onMove = new Vector3UnityEvent();
            platform.onMove.AddListener(_ => { Physics2D.SyncTransforms(); catcher.RefreshContacts(); });
            platformObject.SetActive(true);

            var assetObject = Own("AssetFSM", origin, false);
            var asset = assetObject.AddComponent<FSMController>();
            asset.UserData = this;
            asset.AddRuntimeFSMController(source, "Acceptance");
            assetObject.SetActive(true);
            lightweight = FSM<int>.Allocate().Init(null);
            lightweight.State(1);
            lightweight.State(2);
            Require(lightweight.SetUp(1), "Lightweight FSM startup failed.");
            yield return null;
            Require(asset.GetCurrentStateInfo(0)?.data.name == "Idle", "Asset FSM startup failed.");
            asset.SetBool("Go", true);
            Require(asset.GetCurrentStateInfo(0)?.data.name == "Active" && lightweight.CurrentStateID == 1, "Independent asset transition failed.");
            Require(lightweight.ChangeState(2), "Lightweight FSM transition failed.");
            asset.SetTrigger("Fire");
            asset.SetTrigger("Fire");
            Require(asset.GetTrigger("Fire"), "Trigger/queue failed.");
            asset.ResetTrigger("Fire");
            Require(!asset.GetTrigger("Fire"), "Trigger reset failed.");

            for (int i = 0; i < 80; i++)
            {
                motor.MoveHorizontal(0);
                yield return new WaitForFixedUpdate();
                yield return null;
            }
            Require(motor.IsGrounded && motor.GroundInfo.collider == floor, "CC2D real ground contact failed.");
            var carriedDelta = passengerBody.position - passengerStart;
            Require(!platform.IsRunning && Vector2.Distance(carriedDelta, Vector2.right) < 0.05f, "Carrier displacement or Rigidbody deduplication failed.");
            motor.enabled = false;
            Require(motor.Rigidbody.gravityScale == 2, "CC2D gravity ownership was not restored.");
            asset.enabled = false;
            Require(stateCreates == 2 && stateEnters == 2 && stateExits == 2, "State create/enter/exit lifecycle failed.");
            Require(source.parameters[0].Value == 0 && lightweight.CurrentStateID == 2, "Source asset or lightweight FSM was mutated by the asset FSM.");
            lightweight.RecycleToCache();
            lightweight = null;
        }

        private void OnGUI() => GUI.Label(new Rect(20, 20, 900, 60), "YL Behavior Acceptance: " + result);

        private void OnDestroy()
        {
            if (lightweight != null) { lightweight.RecycleToCache(); lightweight = null; }
        }
    }
}
