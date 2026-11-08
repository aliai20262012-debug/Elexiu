using NUnit.Framework;
using Nexus.Planet;
using UnityEngine;

namespace Nexus.Tests.EditMode
{
    /// <summary>
    /// View construction and selection state. The view builds its meshes and textures in
    /// EditMode (they are runtime objects, not assets), which makes its invariants cheap to
    /// assert without the player loop.
    /// </summary>
    public class PlanetViewTests
    {
        private GameObject _root;
        private PlanetView _view;
        private PlanetDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _definition = PlanetDefinition.CreateRuntimeDefault();

            _root = new GameObject("PlanetViewTest");
            _view = _root.AddComponent<PlanetView>();
            _view.Build(_definition, 4242, PlanetView.DefaultRenderRadius, false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }

            if (_definition != null)
            {
                Object.DestroyImmediate(_definition);
            }
        }

        [Test]
        public void Build_CreatesBodyRegionsAndSurface()
        {
            Assert.IsTrue(_view.IsBuilt);
            Assert.IsNotNull(_view.BodyTransform);
            Assert.IsNotNull(_view.Surface);
            Assert.AreEqual(_definition.Regions.Count, _view.Regions.Count);
            Assert.AreEqual(PlanetView.DefaultRenderRadius, _view.Radius, 0.0001f);
        }

        [Test]
        public void Build_UsesTheReducedQualityBudget()
        {
            Assert.LessOrEqual(_view.MeshSubdivisions, _definition.MeshSubdivisions);
            Assert.LessOrEqual(_view.TextureWidth, _definition.TextureResolution);
        }

        [Test]
        public void Build_Twice_IsIdempotent()
        {
            MeshFilter filter = _root.GetComponentInChildren<MeshFilter>();
            Assert.IsNotNull(filter);

            Mesh first = filter.sharedMesh;
            _view.Build(_definition, 999, PlanetView.DefaultRenderRadius, false);

            Assert.AreSame(first, filter.sharedMesh, "A second build must not rebuild the planet.");
        }

        [Test]
        public void SelectRegion_ShowsHighlightAndRaisesEventOnce()
        {
            int raised = 0;
            PlanetRegion selected = null;
            _view.RegionSelected += region =>
            {
                raised++;
                selected = region;
            };

            PlanetRegion region = _view.SelectRegion(_view.Regions[0].Id);

            Assert.IsNotNull(region);
            Assert.AreEqual(1, raised);
            Assert.AreEqual(region.Id, selected.Id);
            Assert.AreEqual(region.Id, _view.SelectedRegionId);
            Assert.IsTrue(_view.HasSelectionHighlight);

            _view.SelectRegion(region.Id);
            Assert.AreEqual(1, raised, "Re-selecting the same region must not raise another event.");
        }

        [Test]
        public void SelectRegion_UnknownId_ReturnsNull()
        {
            PlanetRegion region = _view.SelectRegion(9999);

            Assert.IsNull(region);
            Assert.AreEqual(RegionLocator.NoRegion, _view.SelectedRegionId);
            Assert.IsFalse(_view.HasSelectionHighlight);
        }

        [Test]
        public void ClearSelection_HidesHighlightAndRaisesOnce()
        {
            int cleared = 0;
            _view.SelectionCleared += () => cleared++;

            _view.SelectRegion(_view.Regions[1].Id);
            _view.ClearSelection(); 

            Assert.AreEqual(1, cleared);
            Assert.IsFalse(_view.HasSelectionHighlight);
            Assert.AreEqual(RegionLocator.NoRegion, _view.SelectedRegionId);

            _view.ClearSelection();
            Assert.AreEqual(1, cleared, "Clearing an empty selection must not raise again.");
        }

        [Test]
        public void WorldDirectionOf_FollowsTheBodyRotation()
        {
            PlanetRegion region = _view.Regions[0];
            Vector3 initial = _view.WorldDirectionOf(region);

            _view.BodyTransform.Rotate(Vector3.up, 90f, Space.Self);
            Vector3 rotated = _view.WorldDirectionOf(region);

            Assert.Less(Vector3.Angle(initial, region.Direction), 0.5f);
            Assert.Greater(Vector3.Angle(initial, rotated), 1f, "Rotating the body must rotate the target direction.");
            Assert.AreEqual(1f, rotated.magnitude, 0.001f);
        }

        [Test]
        public void SettlementAnchor_IsProvidedForKnownRegions()
        {
            PlanetRegion region = _view.Regions[0];

            Vector3 position;
            Vector3 normal;
            Assert.IsTrue(_view.TryGetSettlementAnchor(region.Id, out position, out normal));

            Assert.Less(Vector3.Angle(normal, region.Direction), 0.5f);
            Assert.Greater(position.magnitude, _view.Radius, "The anchor must sit above the surface.");

            Vector3 ignoredPosition;
            Vector3 ignoredNormal;
            Assert.IsFalse(_view.TryGetSettlementAnchor(9999, out ignoredPosition, out ignoredNormal));
        }

        [Test]
        public void RegionAtScreenPoint_WithoutCamera_ReturnsNull()
        {
            Assert.IsNull(_view.RegionAtScreenPoint(null, Vector2.zero));
            Assert.IsFalse(_view.TrySelectAtScreenPoint(null, Vector2.zero));
        }

        [Test]
        public void HighlightMeshes_AreRebuiltPerRegion()
        {
            MeshFilter cap = null;
            Transform highlight = _root.transform.Find("PlanetBody/RegionHighlight/RegionCap");
            if (highlight != null)
            {
                cap = highlight.GetComponent<MeshFilter>();
            }

            _view.SelectRegion(_view.Regions[0].Id);
            Assert.IsNotNull(cap, "The highlight cap object must exist.");
            Mesh first = cap.sharedMesh;

            _view.SelectRegion(_view.Regions[2].Id);
            Assert.IsNotNull(cap.sharedMesh);
            Assert.AreNotSame(first, cap.sharedMesh, "A different region gets a different cap mesh.");
        }
    }
}
