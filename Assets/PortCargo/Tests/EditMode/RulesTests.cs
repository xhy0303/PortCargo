using System.IO;
using NUnit.Framework;
using PortCargo.Core;
using UnityEngine;

namespace PortCargo.Tests
{
    public sealed class RulesTests
    {
        private sealed class Codec : IJsonCodec
        {
            public string Encode<T>(T value) => JsonUtility.ToJson(value);
            public T Decode<T>(string text) => JsonUtility.FromJson<T>(text);
        }
        [Test]
        public void SpecificationRegression()
        {
            var source = File.ReadAllText("Assets/PortCargo/Data/Levels/catalog.json");
            var codec = new Codec();
            var report = RegressionSuite.Run(codec.Decode<CatalogData>(source), codec);
            Directory.CreateDirectory("Docs/Evidence");
            File.WriteAllLines("Docs/Evidence/rules-regression.txt", report);
            Assert.That(report.Count, Is.GreaterThanOrEqualTo(17));
        }
    }
}
