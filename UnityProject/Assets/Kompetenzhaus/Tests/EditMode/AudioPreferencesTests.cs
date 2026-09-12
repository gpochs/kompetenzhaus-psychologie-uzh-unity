using Kompetenzhaus.State;
using NUnit.Framework;
using UnityEngine;

namespace Kompetenzhaus.Tests
{
    public sealed class AudioPreferencesTests
    {
        [Test]
        public void OldSaveWithoutCategoryControlsRetainsMasterAndOriginalMix()
        {
            const string oldSave = "{\"schemaVersion\":1,\"accessibility\":{\"masterVolume\":0.4,\"textScale\":1,\"lookSensitivity\":1}}";
            Assert.That(ProgressStore.TryParseValidated(oldSave, null, out var data, out var error), Is.True, error);
            Assert.That(data.accessibility.masterVolume, Is.EqualTo(0.4f));
            Assert.That(data.accessibility.audioMixVersion, Is.EqualTo(1));
            Assert.That(data.accessibility.ambienceVolume, Is.EqualTo(1f));
            Assert.That(data.accessibility.effectsVolume, Is.EqualTo(1f));
            Assert.That(data.accessibility.footstepsVolume, Is.EqualTo(1f));
        }

        [Test]
        public void MutedCategoriesSurviveSaveRoundTripWithoutChangingOtherControls()
        {
            var source = new ProgressData();
            source.accessibility.masterVolume = 0.6f;
            source.accessibility.ambienceVolume = 0f;
            source.accessibility.effectsVolume = 0.8f;
            source.accessibility.footstepsVolume = 0f;
            source.accessibility.reducedMotion = true;
            Assert.That(ProgressStore.TryParseValidated(JsonUtility.ToJson(source), null, out var loaded, out var error), Is.True, error);
            Assert.That(loaded.accessibility.masterVolume, Is.EqualTo(0.6f));
            Assert.That(loaded.accessibility.ambienceVolume, Is.Zero);
            Assert.That(loaded.accessibility.effectsVolume, Is.EqualTo(0.8f));
            Assert.That(loaded.accessibility.footstepsVolume, Is.Zero);
            Assert.That(loaded.accessibility.reducedMotion, Is.True);
        }

        [TestCase("ambience", -0.1f)]
        [TestCase("effects", 1.1f)]
        [TestCase("footsteps", -0.5f)]
        public void InvalidCategoryGainIsRejected(string category, float value)
        {
            var source = new ProgressData();
            if (category == "ambience") source.accessibility.ambienceVolume = value;
            if (category == "effects") source.accessibility.effectsVolume = value;
            if (category == "footsteps") source.accessibility.footstepsVolume = value;
            Assert.That(ProgressStore.TryParseValidated(JsonUtility.ToJson(source), null, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("accessibility"));
        }

        [Test]
        public void UnknownMixVersionDoesNotSilentlyResetPreferences()
        {
            var source = new ProgressData();
            source.accessibility.audioMixVersion = 2;
            Assert.That(ProgressStore.TryParseValidated(JsonUtility.ToJson(source), null, out _, out _), Is.False);
        }
    }
}
