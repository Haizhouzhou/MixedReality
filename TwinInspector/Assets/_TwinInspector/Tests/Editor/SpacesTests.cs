using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace TwinInspector.Tests
{
    public class SpacesTests
    {
        static SpaceCatalog Packaged() => JsonConvert.DeserializeObject<SpaceCatalog>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "twins", "spaces.json")));

        [Test] public void PackagedSpacesHaveValidManifests()
        {
            var catalog = Packaged(); catalog.Validate();
            foreach (var space in catalog.spaces)
            {
                var root = Path.Combine(Application.streamingAssetsPath, "twins", space.id);
                Assert.That(Directory.Exists(root), Is.EqualTo(space.packaged), space.id + ": packaged folder is named by id");
                if (!space.packaged) continue;
                var manifest = JsonConvert.DeserializeObject<TwinManifest>(File.ReadAllText(Path.Combine(root, "manifest.json")));
                manifest.Validate(space.scan);
                Assert.That(manifest.demo, Is.True, space.id);
                foreach (var tile in manifest.tiles) FileAssert.Exists(Path.Combine(root, tile.files["2"]), space.id);
            }
        }

        [Test] public void SkipOpensHgMainHallAndUnknownDefaultFallsBack()
        {
            var catalog = Packaged();
            Assert.That(catalog.Default(Resources.Load<Settings>("Settings").DefaultSpace).id, Is.EqualTo("HG_MainHall"));
            Assert.That(catalog.Default("missing").id, Is.EqualTo(catalog.spaces[0].id));
            Assert.That(catalog.Find("CAB_MainHall").scan, Is.EqualTo("navvis_2022-06-25_11.14.36"));
        }

        [Test] public void SpacesKeepSeparateStorageEvenFromOneScan()
        {
            var catalog = Packaged();
            Assert.That(catalog.Find("HG_E3_Hall").scan, Is.EqualTo(catalog.Find("HG_MainHall").scan));
            CollectionAssert.AllItemsAreUnique(catalog.spaces.ConvertAll(space => space.Storage));
            foreach (var space in catalog.spaces) Assert.That(space.Storage, Is.EqualTo(space.id));
        }

        [Test] public void DefaultSkipsSpacesWithoutData()
        {
            var catalog = Packaged();
            Assert.That(catalog.Default("HG_E3_Hall", space => space.packaged).id, Is.EqualTo("HG_MainHall"));
            Assert.That(catalog.Default("CAB_MainHall", space => space.packaged).id, Is.EqualTo("CAB_MainHall"));
        }

        [Test] public void CatalogRejectsUnsafeOrDuplicateEntries()
        {
            var catalog = Packaged(); catalog.spaces[1].id = "../outside";
            Assert.Throws<InvalidDataException>(catalog.Validate);
            catalog = Packaged(); catalog.spaces[1].id = catalog.spaces[0].id;
            Assert.Throws<InvalidDataException>(catalog.Validate);
            catalog = Packaged(); catalog.spaces[2].scan = " ";
            Assert.Throws<InvalidDataException>(catalog.Validate);
        }

        [Test] public void CabManifestCarriesItsFloorDatum()
        {
            var manifest = JsonConvert.DeserializeObject<TwinManifest>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "twins", "CAB_MainHall", "manifest.json")));
            Assert.That(manifest.datum_z, Is.EqualTo(-4.8f).Within(1e-4f));
            var hg = JsonConvert.DeserializeObject<TwinManifest>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "twins", "HG_MainHall", "manifest.json")));
            Assert.That(hg.datum_z, Is.EqualTo(0));
        }
    }
}
