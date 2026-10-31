using System;
using System.IO;
using NUnit.Framework;

namespace YLFramework.Testing.Contracts
{
    [Category("YL.Testing.Contract")]
    public class OfflineFixture
    {
        private bool _initialized;
        private int _setups;
        private int _teardowns;

        [OneTimeSetUp]
        public void Initialize() => _initialized = true;

        [SetUp]
        public void SetUp()
        {
            Assert.That(_initialized, Is.True);
            Assert.That(_setups, Is.EqualTo(_teardowns));
            _setups++;
        }

        [TestCase("first")]
        [TestCase("second")]
        public void ParameterizedCasesUseRealFixtureLifecycle(string value)
        {
            Assert.That(value, Is.Not.Empty);
            Assert.That(_setups, Is.EqualTo(_teardowns + 1));
        }

#if YL_TESTING_ADDED_CASE
        [Test]
        public void AddedCategorizedCaseIsDiscovered() => Assert.That(_initialized, Is.True);
#endif

        [TearDown]
        public void TearDown() => _teardowns++;

        [OneTimeTearDown]
        public void Complete()
        {
            Assert.That(_setups, Is.EqualTo(_teardowns));
            File.WriteAllText(Path.Combine(TestContext.CurrentContext.WorkDirectory, "lifecycle.txt"), $"{_setups}/{_teardowns}");
        }
    }

    [Category("YL.Testing.Other")]
    public class UnselectedFixture
    {
        [Test]
        public void OtherCategoryMustNotRun() => Assert.Fail("Unselected category ran.");
    }

    [Category("YL.Testing.TeardownFailure")]
    public class TeardownFailureFixture
    {
        [Test]
        public void BodyPasses() => Assert.Pass();

        [OneTimeTearDown]
        public void CleanupFails() => throw new InvalidOperationException("teardown-marker");
    }
}
