using NUnit.Framework;

namespace _VuTH.Core.Pool.Tests.EditMode
{
    public class PoolCategoryRegistryTests
    {
        [Test]
        public void Add_CreatesCategory_AndIgnoresDuplicateIds()
        {
            var registry = new PoolCategoryRegistry();

            registry.Add("Enemies", 1);
            registry.Add("Enemies", 2);
            registry.Add("Enemies", 1);

            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(registry.TryGetPrefabIds("Enemies", out var ids), Is.True);
            Assert.That(ids, Is.EquivalentTo(new[] { 1, 2 }));
        }

        [Test]
        public void TryGetPrefabIds_UnknownCategory_ReturnsFalse()
        {
            var registry = new PoolCategoryRegistry();

            Assert.That(registry.TryGetPrefabIds("Missing", out var ids), Is.False);
            Assert.That(ids, Is.Null);
        }

        [Test]
        public void Remove_And_Clear_DropCategories()
        {
            var registry = new PoolCategoryRegistry();
            registry.Add("Fx", 1);
            registry.Add("Ui", 2);
            registry.Add("Bullets", 3);

            Assert.That(registry.GetNames(), Is.EquivalentTo(new[] { "Fx", "Ui", "Bullets" }));
            Assert.That(registry.Names, Is.EquivalentTo(new[] { "Fx", "Ui", "Bullets" }));

            Assert.That(registry.Remove("Ui"), Is.True);
            Assert.That(registry.Remove("Ui"), Is.False);
            Assert.That(registry.GetNames(), Is.EquivalentTo(new[] { "Fx", "Bullets" }));

            registry.Clear();
            Assert.That(registry.Count, Is.EqualTo(0));
            Assert.That(registry.GetNames(), Is.Empty);
        }
    }
}
