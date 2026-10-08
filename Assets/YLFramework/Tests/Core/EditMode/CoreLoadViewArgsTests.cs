using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace YLFramework.Core.Tests
{
    public class CoreLoadViewArgsTests
    {
        [Test]
        public void ReusedArgumentsClearEveryValueCategory()
        {
            var recycle = typeof(LoadViewArgs).GetMethod("Recycle", BindingFlags.Static | BindingFlags.NonPublic);
            var instanceField = typeof(ClassObjectPoolManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            var previousPool = instanceField.GetValue(null);
            var previousArgs = new HashSet<object>();
            if (previousPool != null)
            {
                var previousObjects = (IDictionary)previousPool.GetType().GetField("objects", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(previousPool);
                if (previousObjects.Contains(typeof(LoadViewArgs)))
                    foreach (var value in (IList)previousObjects[typeof(LoadViewArgs)]) previousArgs.Add(value);
            }
            var first = LoadViewArgs.New();
            LoadViewArgs reused = null;
            try
            {
                var marker = new object();
                first.SetString("value", "fixture");
                first.SetInt("value", 7);
                first.SetBool("value", true);
                first.SetFloat("value", 2.5f);
                first.SetLong("value", 1234567890123);
                first.SetDouble("value", 3.25);
                first.SetObject("value", marker);
                Assert.That(first.GetString("value"), Is.EqualTo("fixture"));
                Assert.That(first.GetInt("value"), Is.EqualTo(7));
                Assert.That(first.GetBool("value"), Is.True);
                Assert.That(first.GetFloat("value"), Is.EqualTo(2.5f));
                Assert.That(first.GetLong("value"), Is.EqualTo(1234567890123));
                Assert.That(first.GetDouble("value"), Is.EqualTo(3.25));
                Assert.That(first.GetObject("value"), Is.SameAs(marker));
                recycle.Invoke(null, new object[] { first });
                reused = LoadViewArgs.New();
                Assert.That(reused, Is.SameAs(first));
                Assert.That(reused.GetString("value"), Is.Empty);
                Assert.That(reused.GetInt("value"), Is.Zero);
                Assert.That(reused.GetBool("value"), Is.False);
                Assert.That(reused.GetFloat("value"), Is.Zero);
                Assert.That(reused.GetLong("value"), Is.Zero);
                Assert.That(reused.GetDouble("value"), Is.Zero);
                Assert.That(reused.GetObject("value"), Is.Null);
            }
            finally
            {
                var pool = instanceField.GetValue(null);
                var objects = (IDictionary)pool.GetType().GetField("objects", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pool);
                var times = (IDictionary)pool.GetType().GetField("unloaded_times", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pool);
                foreach (var args in new[] { first, reused })
                {
                    if (args == null) continue;
                    args.OnLoaded();
                    recycle.Invoke(null, new object[] { args });
                    if (previousArgs.Contains(args)) continue;
                    ((IList)objects[typeof(LoadViewArgs)]).Remove(args);
                    times.Remove(args);
                }
                if (((IList)objects[typeof(LoadViewArgs)]).Count == 0) objects.Remove(typeof(LoadViewArgs));
                if (previousPool == null)
                {
                    // EditMode creates no checker coroutine, so this newly created empty state is safe to restore.
                    var callback = (Action<PlayModeStateChange>)Delegate.CreateDelegate(typeof(Action<PlayModeStateChange>),
                        typeof(ClassObjectPoolManager).GetMethod("EditorApplication_playModeStateChanged", BindingFlags.Static | BindingFlags.NonPublic));
                    EditorApplication.playModeStateChanged -= callback;
                    instanceField.SetValue(null, null);
                }
            }
        }
    }
}
