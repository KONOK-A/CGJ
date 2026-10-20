using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace YLFramework.Tests.Extensions.Behavior
{
    public sealed class OriginalGameNonInterferenceTests
    {
        [UnityTest]
        public IEnumerator OriginalMenuSettingsPauseReturnAndReentryKeepLegacyPaths()
        {
            // 复用既有真实按钮/Slider与退出清理流程，不重绑原游戏或重建 Bundle。
            var type = Type.GetType("YLFramework.Core.Tests.AudioGameAcceptanceFlow, YLFramework.UI.Acceptance", true);
            var flow = (IEnumerator)type.GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            var overlapType = Type.GetType("XFGameFramework.Overlap.OverlapBase2D, XFGameFramework.Overlap", true);
            FieldInfo timer = null;
            for (var current = overlapType; current != null && timer == null; current = current.BaseType)
                timer = current.GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.That(timer, Is.Not.Null);
            var scenes = new HashSet<int>();
            var times = new Dictionary<int, float>();
            var roundCycles = new List<int>();
            int components = 0, automaticCycles = 0;
            try
            {
                while (flow.MoveNext())
                {
                    yield return flow.Current;
                    var scene = SceneManager.GetActiveScene();
                    if (scene.name != "Game" || !scenes.Add(scene.handle)) continue;
                    // 在推进原流程的暂停/返回按钮前，留出真实 Update 以观察旧 Overlap 自身的计时归零。
                    int beforeCycles = automaticCycles, rawCount = 0;
                    float deadline = Time.realtimeSinceStartup + 10f;
                    while (Time.realtimeSinceStartup < deadline && automaticCycles == beforeCycles)
                    {
                        var overlaps = UnityEngine.Object.FindObjectsOfType(overlapType);
                        rawCount = Math.Max(rawCount, overlaps.Length);
                        foreach (var item in overlaps)
                        {
                            var component = (MonoBehaviour)item;
                            if (!component.isActiveAndEnabled || !(bool)overlapType.GetProperty("Running").GetValue(item)) continue;
                            float value = (float)timer.GetValue(item);
                            int id = item.GetInstanceID();
                            if (times.TryGetValue(id, out float before) && value < before) automaticCycles++;
                            if (!times.ContainsKey(id)) components++;
                            times[id] = value;
                        }
                        yield return null;
                    }
                    Debug.Log($"BM26_LEGACY_OBSERVE scene={scene.handle}; rawComponents={rawCount}; runningComponents={components}; automaticCycles={automaticCycles}; timeScale={Time.timeScale}");
                    roundCycles.Add(automaticCycles - beforeCycles);
                }
            }
            finally { (flow as IDisposable)?.Dispose(); }
            Assert.That(scenes.Count, Is.EqualTo(2), "Both original Game scene entries must execute.");
            Assert.That(roundCycles, Has.Count.EqualTo(2));
            Assert.That(roundCycles, Has.All.GreaterThan(0), "Each reentry round must run original Overlap queries independently.");
            Assert.That(components, Is.GreaterThan(0), "Original gameplay must retain live XF Overlap components.");
            Assert.That(automaticCycles, Is.GreaterThan(0), "Original Overlap Update must really query, not only have its type present.");
            Debug.Log($"BM26_LEGACY_PATH PASS scenes={scenes.Count}; overlapComponents={components}; automaticQueryCycles={automaticCycles}; overlapAssembly={overlapType.Assembly.FullName}; flowAssembly={type.Assembly.FullName}; original XF/XFAB/XFFSM button flow completed.");
        }
    }
}
