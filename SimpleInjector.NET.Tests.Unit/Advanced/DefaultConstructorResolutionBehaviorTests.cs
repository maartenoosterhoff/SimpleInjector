﻿namespace SimpleInjector.Tests.Unit.Advanced
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DefaultConstructorResolutionBehaviorTests
    {
        [TestMethod]
        public void GetConstructor_WithNullArgument1_ThrowsException()
        {
            // Arrange
            var behavior = new ContainerOptions().ConstructorResolutionBehavior;

            // Act
            Action action = () => behavior.GetConstructor(null, typeof(TypeWithSinglePublicDefaultConstructor));

            // Assert
            AssertThat.Throws<ArgumentNullException>(action);
        }

        [TestMethod]
        public void GetConstructor_WithNullArgument2_ThrowsException()
        {
            // Arrange
            var behavior = new ContainerOptions().ConstructorResolutionBehavior;

            // Act
            Action action = () => behavior.GetConstructor(typeof(TypeWithSinglePublicDefaultConstructor), null);

            // Assert
            AssertThat.Throws<ArgumentNullException>(action);
        }

        [TestMethod]
        public void IsRegistrationPhase_InstancesResolvedFromTheContainer_ReturnsFalse()
        {
            // Arrange
            var behavior = new ContainerOptions().ConstructorResolutionBehavior;

            // Act
            var constructor = behavior.GetConstructor(typeof(TypeWithSinglePublicDefaultConstructor),
                typeof(TypeWithSinglePublicDefaultConstructor));

            // Assert
            Assert.IsNotNull(constructor, "The constructor was expected to be returned.");
        }

        [TestMethod]
        public void GetConstructor_TypeWithMultiplePublicConstructors_ThrowsExpectedException()
        {
            // Arrange
            var behavior = new ContainerOptions().ConstructorResolutionBehavior;

            try
            {
                // Act
                behavior.GetConstructor(typeof(TypeWithMultiplePublicConstructors),
                    typeof(TypeWithMultiplePublicConstructors));

                // Assert
                Assert.Fail("Exception expected.");
            }
            catch (ActivationException ex)
            {
                AssertThat.StringContains("For the container to be able to create " +
                    "DefaultConstructorResolutionBehaviorTests.TypeWithMultiplePublicConstructors, it should " +
                    "contain exactly one public constructor, but it has 2.", ex.Message);
            }
        }

        [TestMethod]
        public void GetConstructor_TypeWithSingleInternalConstructor_ThrowsExpectedException()
        {
            // Arrange
            var behavior = new ContainerOptions().ConstructorResolutionBehavior;

            try
            {
                // Act
                behavior.GetConstructor(typeof(TypeWithSingleInternalConstructor),
                    typeof(TypeWithSingleInternalConstructor));

                // Assert
                Assert.Fail("Exception expected.");
            }
            catch (ActivationException ex)
            {
                AssertThat.StringContains("For the container to be able to create " +
                    "DefaultConstructorResolutionBehaviorTests.TypeWithSingleInternalConstructor, it should " +
                    "contain exactly one public constructor, but it has 0.", ex.Message);
            }
        }

        private class TypeWithSinglePublicDefaultConstructor
        {
            public TypeWithSinglePublicDefaultConstructor()
            {
            }
        }

        private class TypeWithMultiplePublicConstructors
        {
            public TypeWithMultiplePublicConstructors()
            {
            }

            public TypeWithMultiplePublicConstructors(int a)
            {
            }
        }

        private class TypeWithSingleInternalConstructor
        {
            internal TypeWithSingleInternalConstructor()
            {
            }
        }
    }
}