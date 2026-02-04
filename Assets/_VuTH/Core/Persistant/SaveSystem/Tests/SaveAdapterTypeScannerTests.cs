using System;
using _VuTH.Core.Persistant.SaveSystem.Editor;
using _VuTH.Core.Persistant.SaveSystem.Encrypt;
using _VuTH.Core.Persistant.SaveSystem.Serialize;
using NUnit.Framework;

namespace _VuTH.Core.Persistant.SaveSystem.Tests
{
    /// <summary>
    /// Unit tests for SaveAdapterTypeScanner.
    /// Tests helper methods and type validation logic.
    /// </summary>
    [TestFixture]
    public class SaveAdapterTypeScannerTests
    {
        #region HasPublicParameterlessCtor Tests

        [Test]
        public void HasPublicParameterlessCtor_ClassWithPublicParameterlessCtor_ReturnsTrue()
        {
            // Arrange
            var type = typeof(ValidSerializableClass);

            // Act
            var result = InvokeHasPublicParameterlessCtor(type);

            // Assert
            Assert.IsTrue(result, "Class with public parameterless ctor should return true");
        }

        [Test]
        public void HasPublicParameterlessCtor_ClassWithOnlyParameterizedCtor_ReturnsFalse()
        {
            // Arrange
            var type = typeof(ClassWithOnlyParameterizedCtor);

            // Act
            var result = InvokeHasPublicParameterlessCtor(type);

            // Assert
            Assert.IsFalse(result, "Class without public parameterless ctor should return false");
        }

        [Test]
        public void HasPublicParameterlessCtor_AbstractClass_ReturnsFalse()
        {
            // Arrange
            var type = typeof(AbstractBaseClass);

            // Act
            var result = InvokeHasPublicParameterlessCtor(type);

            // Assert
            Assert.IsFalse(result, "Abstract class should return false");
        }

        [Test]
        public void HasPublicParameterlessCtor_Interface_ReturnsFalse()
        {
            // Arrange
            var type = typeof(IDummyInterface);

            // Act
            var result = InvokeHasPublicParameterlessCtor(type);

            // Assert
            Assert.IsFalse(result, "Interface should return false");
        }

        #endregion

        #region HasSerializableAttribute Tests

        [Test]
        public void HasSerializableAttribute_ClassWithSerializableAttribute_ReturnsTrue()
        {
            // Arrange
            var type = typeof(ValidSerializableClass);

            // Act
            var result = InvokeHasSerializableAttribute(type);

            // Assert
            Assert.IsTrue(result, "Class with [Serializable] should return true");
        }

        [Test]
        public void HasSerializableAttribute_ClassWithoutSerializableAttribute_ReturnsFalse()
        {
            // Arrange
            var type = typeof(ClassWithoutSerializableAttribute);

            // Act
            var result = InvokeHasSerializableAttribute(type);

            // Assert
            Assert.IsFalse(result, "Class without [Serializable] should return false");
        }

        #endregion

        #region IsValidAdapterType Tests (Mock)

        // Note: We cannot directly test private IsValidAdapterType, but we can test the public
        // GetImplementations/GetAdapterItems methods with known types.
        // However, those methods use TypeCache which scans assemblies.
        // For unit tests, we test the helper methods indirectly.

        [Test]
        public void GetImplementations_ForISerializer_ReturnsSerializableTypes()
        {
            // Act
            var implementations = SaveAdapterTypeScanner.GetImplementations(typeof(ISerializer));

            // Assert
            Assert.IsNotNull(implementations, "Should return a list");
            // Known serializer in this project: JsonSerializer, NewtonsoftJsonSerializer
            // Both should be present if they have [Serializable] and a public parameterless ctor
            Assert.IsTrue(implementations.Count > 0, "Should find at least one ISerializer implementation");

            // Verify all returned types meet our criteria
            foreach (var type in implementations)
            {
                Assert.IsTrue(
                    type.GetConstructor(Type.EmptyTypes) != null,
                    $"{type.Name} should have public parameterless ctor");
                Assert.IsTrue(
                    Attribute.IsDefined(type, typeof(SerializableAttribute)),
                    $"{type.Name} should have [Serializable] attribute");
            }
        }

        [Test]
        public void GetAdapterItems_ForIEncryptor_ReturnsValidItems()
        {
            // Act
            var items = SaveAdapterTypeScanner.GetAdapterItems(typeof(IEncryptor));

            // Assert
            Assert.IsNotNull(items, "Should return a list");
            Assert.IsTrue(items.Count > 0, "Should find at least one IEncryptor implementation");

            foreach (var item in items)
            {
                Assert.IsNotNull(item.Type, "Item.Type should not be null");
                Assert.IsNotEmpty(item.DisplayName, "DisplayName should not be empty");
                Assert.IsNotNull(item.DisplayName, "DisplayName should not be empty");
                Assert.IsNotEmpty(item.FullName, "FullName should not be empty");
                Assert.IsNotNull(item.FullName, "FullName should not be empty");
                Assert.IsNotEmpty(item.AssemblyQualifiedName, "AssemblyQualifiedName should not be empty");
                Assert.IsNotNull(item.AssemblyQualifiedName, "AssemblyQualifiedName should not be empty");
            }
        }

        [Test]
        public void GetDisplayName_ReturnsSimpleTypeName()
        {
            // Arrange
            var type = typeof(JsonSerializer);

            // Act
            var displayName = SaveAdapterTypeScanner.GetDisplayName(type);

            // Assert
            Assert.AreEqual("JsonSerializer", displayName, "DisplayName should be the simple type name");
        }

        [Test]
        public void GetFullDisplayName_ReturnsFullTypeName()
        {
            // Arrange
            var type = typeof(JsonSerializer);

            // Act
            var fullName = SaveAdapterTypeScanner.GetFullDisplayName(type);

            // Assert
            Assert.IsTrue(fullName.Contains("JsonSerializer"), "FullName should contain the type name");
        }

        [Test]
        public void ClearCache_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(SaveAdapterTypeScanner.ClearCache, "ClearCache should not throw");
        }

        #endregion

        #region Helper Method Invokers (Reflection)

        private static bool InvokeHasPublicParameterlessCtor(Type t)
        {
            var method = typeof(SaveAdapterTypeScanner).GetMethod(
                "HasPublicParameterlessCtor",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(method, "Method should exist");
            return (bool)method.Invoke(null, new object[] { t });
        }

        private static bool InvokeHasSerializableAttribute(Type t)
        {
            var method = typeof(SaveAdapterTypeScanner).GetMethod(
                "HasSerializableAttribute",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(method, "Method should exist");
            return (bool)method.Invoke(null, new object[] { t });
        }

        #endregion

        #region Test Fixture Types

        // These types are used for testing the helper methods

        /// <summary>
        /// Valid test class: has [Serializable] and public parameterless ctor.
        /// </summary>
        [Serializable]
        private class ValidSerializableClass
        {
        }

        /// <summary>
        /// Invalid test class: has [Serializable] but no public parameterless ctor.
        /// </summary>
        [Serializable]
        private class ClassWithOnlyParameterizedCtor
        {
            public ClassWithOnlyParameterizedCtor(int value) { }
        }

        /// <summary>
        /// Invalid test class: no [Serializable] attribute.
        /// </summary>
        private class ClassWithoutSerializableAttribute
        {
        }

        /// <summary>
        /// Abstract base class for testing.
        /// </summary>
        [Serializable]
        private abstract class AbstractBaseClass
        {
        }

        /// <summary>
        /// Dummy interface for testing.
        /// </summary>
        private interface IDummyInterface { }

        #endregion
    }
}
