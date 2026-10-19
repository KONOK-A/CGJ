using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using YLFramework.Events;

namespace YLFramework.Tests.Extensions.Domain
{
    public class ReddotTests
    {
        private Type Manager => typeof(EventChannel).Assembly.GetType("YLFramework.ReddotSystem.ReddotManager");
        private object Call(string method, params object[] args)
        {
            Assert.That(Manager, Is.Not.Null, "YL ReddotManager must exist.");
            var types = Array.ConvertAll(args, a => a?.GetType() ?? typeof(string));
            return Manager.GetMethod(method, types).Invoke(null, args);
        }

        [TearDown] public void Clean() { if (Manager != null) Call("Reset"); }

        [TestCase("Mail", true)]
        [TestCase("Mail/Gift", true)]
        [TestCase("Mai", false)]
        [TestCase("Mailbox", false)]
        [TestCase(null, false)]
        public void PathsMatchWholeSegments(string path, bool expected)
        {
            Call("AddPath", " /Mail//Gift/ ");
            Assert.That(Call("ShouldBeDisplay", path), Is.EqualTo(expected));
        }

        [Test] public void ResetNotifiesAfterClearingAndDuplicateAddDoesNotNotify()
        {
            var displays = new List<bool>();
            Action changed = () => displays.Add((bool)Call("ShouldBeDisplay", "Mail"));
            var token = (IUnRegister)Call("RegisterChanged", changed);
            try
            {
                Call("AddPath", "Mail");
                Call("AddPath", "/Mail/");
                Call("Reset");
                Assert.That(displays, Is.EqualTo(new[] { true, false }));
                token.UnRegister();
                Call("AddPath", "Mail");
                Assert.That(displays.Count, Is.EqualTo(2));
            }
            finally { token.UnRegister(); }
        }

        [Test] public void SamePathSourcesAreReleasedIndependently()
        {
            var first = (IDisposable)Call("RegisterPath", "Mail/Gift");
            var second = (IDisposable)Call("RegisterPath", "Mail/Gift/");
            first.Dispose();
            first.Dispose();
            Assert.That(Call("ShouldBeDisplay", "Mail"), Is.True);
            second.Dispose();
            Assert.That(Call("ShouldBeDisplay", "Mail"), Is.False);
        }

        [Test] public void OldRegistrationCannotRemoveNewPathAfterReset()
        {
            var old = (IDisposable)Call("RegisterPath", "Mail");
            Call("Reset");
            var current = (IDisposable)Call("RegisterPath", "Mail");
            old.Dispose();
            Assert.That(Call("ShouldBeDisplay", "Mail"), Is.True);
            current.Dispose();
        }

        [Test] public void LegacyRemoveDoesNotStealIndependentSource()
        {
            var source = (IDisposable)Call("RegisterPath", "Mail");
            Call("AddPath", "Mail");
            Call("RemovePath", "Mail");
            Call("RemovePath", (string)null);
            Assert.That(Call("ShouldBeDisplay", "Mail"), Is.True);
            source.Dispose();
            Assert.That(Call("ShouldBeDisplay", "Mail"), Is.False);
        }
    }
}
