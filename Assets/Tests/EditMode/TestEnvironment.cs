using System.IO;
using NUnit.Framework;
using UnityEngine;
using Roughlike.Core;

namespace Roughlike.Tests.EditMode
{
    /// <summary>Runs once for every test in this assembly: points saves at a throwaway folder so tests never touch (or read) real player saves and settings.</summary>
    [SetUpFixture]
    public class TestEnvironment
    {
        string _dir;

        [OneTimeSetUp]
        public void Setup()
        {
            _dir = Path.Combine(Path.GetTempPath(), "roughlike-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            SaveService.SetDirectory(_dir);
            SettingsService.Unload();
        }

        [OneTimeTearDown]
        public void Teardown()
        {
            SaveService.SetDirectory(null);
            SettingsService.Unload();
            try { Directory.Delete(_dir, true); } catch { }
        }
    }
}
