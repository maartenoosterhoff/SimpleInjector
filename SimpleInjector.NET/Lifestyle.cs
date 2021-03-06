﻿#region Copyright Simple Injector Contributors
/* The Simple Injector is an easy-to-use Inversion of Control library for .NET
 * 
 * Copyright (c) 2013 Simple Injector Contributors
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and 
 * associated documentation files (the "Software"), to deal in the Software without restriction, including 
 * without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
 * copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the 
 * following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all copies or substantial 
 * portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT 
 * LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO 
 * EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER 
 * IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE 
 * USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
#endregion

namespace SimpleInjector
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq.Expressions;
    using System.Reflection;
    using SimpleInjector.Advanced;
    using SimpleInjector.Lifestyles;

    /// <summary>
    /// Factory for the creation of a delegate that applies caching to the supplied 
    /// <paramref name="transientInstanceCreator"/>.
    /// </summary>
    /// <param name="transientInstanceCreator">A factory for creating new instances.</param>
    /// <returns>A factory that returns cached instances.</returns>
    public delegate Func<object> CreateLifestyleApplier(Func<object> transientInstanceCreator);

    /// <summary>
    /// Instances returned from the container can be cached. The <see cref="Container"/> contains several
    /// overloads of the <b>Register</b> method that take a <b>Lifestyle</b> instance as argument to define 
    /// how returned instances should be cached. The core library contains two lifestyles out of the box. By
    /// supplying <see cref="Lifestyle.Transient">Lifestyle.Transient</see>, the registered instance is not
    /// cached; a new instance is returned every time it is requested or injected. By supplying
    /// <see cref="Lifestyle.Singleton">Lifestyle.Singleton</see> instances can be cached indefinitely; only
    /// a single instance of the registered component will be returned by that container instance. Other
    /// lifestyles are defined in integration and extension packages. The 
    /// <see cref="Lifestyle.CreateCustom">CreateCustom</see> method allows defining a custom lifestyle and 
    /// the <see cref="Lifestyle.CreateHybrid(Func{bool}, Lifestyle, Lifestyle)">CreateHybrid</see> method 
    /// allows creating a lifestyle that mixes multiple other lifestyles.
    /// </summary>
    /// <remarks>
    /// This type is abstract and can be overridden to implement a custom lifestyle.
    /// </remarks>
    [DebuggerDisplay("{Name,nq}")]
    public abstract class Lifestyle
    {
        /// <summary>
        /// The lifestyle instance that doesn't cache instances. A new instance of the specified
        /// component is created every time the registered service it is requested or injected.
        /// </summary>
        /// <example>
        /// The following example registers the <c>SomeServiceImpl</c> implementation for the
        /// <c>ISomeService</c> service type using the <b>Transient</b> lifestyle:
        /// <code lang="cs"><![CDATA[
        /// var container = new Container();
        /// 
        /// container.Register<ISomeService, SomeServiceImpl>(Lifestyle.Transient);
        /// ]]></code>
        /// Note that <b>Transient</b> is the default lifestyle, the previous registration can be reduced to
        /// the following:
        /// <code lang="cs"><![CDATA[
        /// var container = new Container();
        /// 
        /// // Transient registration.
        /// container.Register<ISomeService, SomeServiceImpl>();
        /// ]]></code>
        /// </example>
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes",
            Justification = "It's not mutable.")]
        public static readonly Lifestyle Transient = new TransientLifestyle();

        /// <summary>
        /// The lifestyle that caches components during the lifetime of the <see cref="Container"/> instance
        /// and guarantees that only a single instance of that component is created for that instance. Since
        /// general use is to create a single <b>Container</b> instance for the lifetime of the application /
        /// AppDomain, this would mean that only a single instance of that component would exist during the
        /// lifetime of the application. In a multi-threaded applications, implementations registered using 
        /// this lifestyle must be thread-safe.
        /// </summary>
        /// <example>
        /// The following example registers the <c>RealTimeProvider</c> implementation for the
        /// <c>ITimeProvider</c> service type using the <b>Singleton</b> lifestyle:
        /// <code lang="cs"><![CDATA[
        /// var container = new Container();
        /// 
        /// container.Register<ITimeProvider, RealTimeProvider>(Lifestyle.Singleton);
        /// ]]></code>
        /// Note that using the 
        /// <see cref="Container.RegisterSingle{TService, TImplementation}">RegisterSingle</see> method has 
        /// the same effect:
        /// <code lang="cs"><![CDATA[
        /// var container = new Container();
        /// 
        /// // Singleton registration.
        /// container.RegisterSingle<ITimeProvider, RealTimeProvider>();
        /// ]]></code>
        /// </example>
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", 
            Justification = "It's not mutable.")]
        public static readonly Lifestyle Singleton = new SingletonLifestyle();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal static readonly Lifestyle Unknown = new UnknownLifestyle();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly MethodInfo OpenCreateRegistrationTServiceTImplementationMethod =
            GetMethod(lifestyle => lifestyle.CreateRegistration<object, object>(null));

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly MethodInfo OpenCreateRegistrationTServiceFuncMethod =
            GetMethod(lifestyle => lifestyle.CreateRegistration<object>(null, null));

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly string name;

        /// <summary>Initializes a new instance of the <see cref="Lifestyle"/> class.</summary>
        /// <param name="name">The user friendly name of this lifestyle.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null (Nothing in VB) 
        /// or an empty string.</exception>
        protected Lifestyle(string name)
        {
            Requires.IsNotNullOrEmpty(name, "name");

            this.name = name;
        }

        /// <summary>Gets the user friendly name of this lifestyle.</summary>
        /// <value>The user friendly name of this lifestyle.</value>
        public string Name 
        { 
            get { return this.name; } 
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal virtual int ComponentLength 
        {
            get { return this.Length; }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal virtual int DependencyLength 
        {
            get { return this.Length; }
        }

        /// <summary>
        /// Gets the length of the lifestyle. Implementers must implement this property. The diagnostic
        /// services use this value to compare lifestyles with each other to determine lifestyle 
        /// misconfigurations.
        /// </summary>
        /// <value>The <see cref="Int32"/> representing the length of this lifestyle.</value>
        protected abstract int Length
        {
            get;
        }

        /// <summary>
        /// The hybrid lifestyle allows mixing two lifestyles in a single registration. Based on the supplied
        /// <paramref name="lifestyleSelector"/> delegate the hybrid lifestyle will redirect the creation of 
        /// the instance to the correct lifestyle. The result of the <paramref name="lifestyleSelector"/> 
        /// delegate will not be cached; it is invoked each time an instance is requested or injected. By 
        /// nesting hybrid lifestyles, any number of lifestyles can be mixed.
        /// </summary>
        /// <param name="lifestyleSelector">The <see cref="Func{TResult}"/> delegate that determines which 
        /// lifestyle should be used. The <paramref name="trueLifestyle"/> will be used if <b>true</b> is 
        /// returned; the <paramref name="falseLifestyle"/> otherwise. This delegate will be called every
        /// time an instance needs to be resolved or injected.</param>
        /// <param name="trueLifestyle">The lifestyle to use when <paramref name="lifestyleSelector"/> 
        /// returns <b>true</b>.</param>
        /// <param name="falseLifestyle">The lifestyle to use when <paramref name="lifestyleSelector"/> 
        /// returns <b>false</b>.</param>
        /// <returns>A new hybrid lifestyle that wraps the supplied lifestyles.</returns>
        /// <exception cref="ArgumentNullException">Thrown when one of the supplied arguments is a null
        /// reference (Nothing in VB).</exception>
        /// <example>
        /// <para>
        /// The following example shows the creation of a <b>HybridLifestyle</b> that mixes an 
        /// <b>WebRequestLifestyle</b> and <b>LifetimeScopeLifestyle</b>:
        /// </para>
        /// <code lang="cs"><![CDATA[
        /// // NOTE: WebRequestLifestyle is located in SimpleInjector.Integration.Web.dll.
        /// // NOTE: LifetimeScopeLifestyle is located in SimpleInjector.Extensions.LifetimeScoping.dll.
        /// var mixedScopeLifestyle = Lifestyle.CreateHybrid(
        ///     () => HttpContext.Current != null,
        ///     new WebRequestLifestyle(),
        ///     new LifetimeScopeLifestyle());
        /// 
        /// // The created lifestyle can be reused for many registrations.
        /// container.Register<IUserRepository, SqlUserRepository>(mixedScopeLifestyle);
        /// container.Register<ICustomerRepository, SqlCustomerRepository>(mixedScopeLifestyle);
        /// ]]></code>
        /// <para>
        /// Hybrid lifestyles can be nested:
        /// </para>
        /// <code lang="cs"><![CDATA[
        /// var mixedLifetimeTransientLifestyle = Lifestyle.CreateHybrid(
        ///     () => container.GetCurrentLifetimeScope() != null,
        ///     new LifetimeScopeLifestyle(),
        ///     Lifestyle.Transient);
        /// 
        /// var mixedScopeLifestyle = Lifestyle.CreateHybrid(
        ///     () => HttpContext.Current != null,
        ///     new WebRequestLifestyle(),
        ///     mixedLifetimeTransientLifestyle);
        /// ]]></code>
        /// <para>
        /// The <b>mixedScopeLifestyle</b> now mixed three lifestyles: Web Request, Lifetime Scope and 
        /// Transient.
        /// </para>
        /// </example>
        public static Lifestyle CreateHybrid(Func<bool> lifestyleSelector, Lifestyle trueLifestyle, 
            Lifestyle falseLifestyle)
        {
            Requires.IsNotNull(lifestyleSelector, "lifestyleSelector");
            Requires.IsNotNull(trueLifestyle, "trueLifestyle");
            Requires.IsNotNull(falseLifestyle, "falseLifestyle");

            return new HybridLifestyle(lifestyleSelector, trueLifestyle, falseLifestyle);
        }

        /// <summary>
        /// The hybrid lifestyle allows mixing two lifestyles in a single registration. Based on the supplied
        /// <paramref name="lifestyleSelector"/> delegate the hybrid lifestyle will redirect the creation of 
        /// the instance to the correct lifestyle. The result of the <paramref name="lifestyleSelector"/> 
        /// delegate will not be cached; it is invoked each time an instance is requested or injected. By 
        /// nesting hybrid lifestyles, any number of lifestyles can be mixed.
        /// </summary>
        /// <param name="lifestyleSelector">The <see cref="Func{TResult}"/> delegate that determines which 
        /// lifestyle should be used. The <paramref name="trueLifestyle"/> will be used if <b>true</b> is 
        /// returned; the <paramref name="falseLifestyle"/> otherwise. This delegate will be called every
        /// time an instance needs to be resolved or injected.</param>
        /// <param name="trueLifestyle">The scoped lifestyle to use when <paramref name="lifestyleSelector"/> 
        /// returns <b>true</b>.</param>
        /// <param name="falseLifestyle">The scoped lifestyle to use when <paramref name="lifestyleSelector"/> 
        /// returns <b>false</b>.</param>
        /// <returns>A new scoped hybrid lifestyle that wraps the supplied lifestyles.</returns>
        /// <exception cref="ArgumentNullException">Thrown when one of the supplied arguments is a null
        /// reference (Nothing in VB).</exception>
        /// <example>
        /// <para>
        /// The following example shows the creation of a <b>HybridLifestyle</b> that mixes an 
        /// <b>WebRequestLifestyle</b> and <b>LifetimeScopeLifestyle</b>:
        /// </para>
        /// <code lang="cs"><![CDATA[
        /// // NOTE: WebRequestLifestyle is located in SimpleInjector.Integration.Web.dll.
        /// // NOTE: LifetimeScopeLifestyle is located in SimpleInjector.Extensions.LifetimeScoping.dll.
        /// var mixedScopeLifestyle = Lifestyle.CreateHybrid(
        ///     () => HttpContext.Current != null,
        ///     new WebRequestLifestyle(),
        ///     new LifetimeScopeLifestyle());
        /// 
        /// // The created lifestyle can be reused for many registrations.
        /// container.Register<IUserRepository, SqlUserRepository>(mixedScopeLifestyle);
        /// container.Register<ICustomerRepository, SqlCustomerRepository>(mixedScopeLifestyle);
        /// ]]></code>
        /// </example>
        public static ScopedLifestyle CreateHybrid(Func<bool> lifestyleSelector, ScopedLifestyle trueLifestyle,
            ScopedLifestyle falseLifestyle)
        {
            Requires.IsNotNull(lifestyleSelector, "lifestyleSelector");
            Requires.IsNotNull(trueLifestyle, "trueLifestyle");
            Requires.IsNotNull(falseLifestyle, "falseLifestyle");

            return new ScopedHybridLifestyle(lifestyleSelector, trueLifestyle, falseLifestyle);
        }
        
        /// <summary>
        /// Creates a custom lifestyle using the supplied <paramref name="lifestyleApplierFactory"/> delegate.
        /// </summary>
        /// <remarks>
        /// The supplied <paramref name="lifestyleApplierFactory" /> will be called just once per registered 
        /// service. The supplied <paramref name="lifestyleApplierFactory" /> will be called by the framework
        /// when the type is resolved for the first time, and the framework will supply the factory with a
        /// <b>Func&lt;object&gt;</b> for creating new (transient) instances of that type (that might
        /// have been <see cref="Container.ExpressionBuilding">intercepted</see> and
        /// <see cref="Container.RegisterInitializer{TService}">initializers</see> might have been applied). 
        /// It is the job of the <paramref name="lifestyleApplierFactory" /> to return a <b>Func&lt;object&gt;</b>
        /// that applies the proper caching. The <b>Func&lt;object&gt;</b> that is returned by the 
        /// <paramref name="lifestyleApplierFactory" /> will be stored for that registration (every 
        /// registration will store its own <b>Func&lt;object&gt;</b> delegate) and this delegate will be
        /// called every time the service is resolved (by calling 
        /// <code>container.GetInstance&lt;TService&gt;</code> or when that service is injected into another
        /// type). 
        /// </remarks>
        /// <param name="name">The name of the lifestyle to create. The name is used to display the lifestyle
        /// in the debugger.</param>
        /// <param name="lifestyleApplierFactory">A factory delegate that takes a <b>Func&lt;object&gt;</b> delegate
        /// that will produce a transient instance and returns a delegate that returns cached instances.</param>
        /// <returns>A new <see cref="Lifestyle"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when one of the arguments is a null reference
        /// (Nothing in VB).</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is an empty string.</exception>
        /// <example>
        /// The following example shows the creation of a lifestyle that caches registered instances for 10
        /// minutes:
        /// <code lang="cs"><![CDATA[
        /// var customLifestyle = Lifestyle.CreateCustom("Absolute 10 Minute Expiration", instanceCreator =>
        /// {
        ///     TimeSpan timeout = TimeSpan.FromMinutes(10);
        ///     var syncRoot = new object();
        ///     var expirationTime = DateTime.MinValue;
        ///     object instance = null;
        /// 
        ///     // If the application has multiple registrations using this lifestyle, each registration
        ///     // will get its own Func<object> delegate (created here) and therefore get its own set
        ///     // of variables as defined above.
        ///     return () =>
        ///     {
        ///         lock (syncRoot)
        ///         {
        ///             if (expirationTime < DateTime.UtcNow)
        ///             {
        ///                 instance = instanceCreator();
        ///                 expirationTime = DateTime.UtcNow.Add(timeout);
        ///             }
        /// 
        ///             return instance;
        ///         }
        ///     };
        /// });
        /// 
        /// var container = new Container();
        /// 
        /// // We can reuse the created lifestyle for multiple registrations.
        /// container.Register<IService, MyService>(customLifestyle);
        /// container.Register<AnotherService, MeTwoService>(customLifestyle);
        /// ]]></code>
        /// </example>
        public static Lifestyle CreateCustom(string name, CreateLifestyleApplier lifestyleApplierFactory)
        {
            Requires.IsNotNullOrEmpty(name, "name");
            Requires.IsNotNull(lifestyleApplierFactory, "lifestyleApplierFactory");

            return new CustomLifestyle(name, lifestyleApplierFactory);
        }

        /// <summary>
        /// Creates a new <see cref="InstanceProducer"/> instance for the given <typeparamref name="TService"/>
        /// that will create new instances of specified <typeparamref name="TImplementation"/> with the 
        /// caching as specified by this lifestyle.
        /// </summary>
        /// <typeparam name="TService">The interface or base type that can be used to retrieve the instances.</typeparam>
        /// <typeparam name="TImplementation">The concrete type that will be created.</typeparam>
        /// <param name="container">The <see cref="Container"/> instance for which a 
        /// <see cref="InstanceProducer"/> must be created.</param>
        /// <returns>A new <see cref="InstanceProducer"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="container"/> is a null
        /// reference (Nothing in VB).</exception>
        public InstanceProducer CreateProducer<TService, TImplementation>(Container container)
            where TImplementation : class, TService
            where TService : class
        {
            return new InstanceProducer(typeof(TService),
                this.CreateRegistration<TService, TImplementation>(container));
        }

        /// <summary>
        /// Creates a new <see cref="InstanceProducer"/> instance for the given <typeparamref name="TService"/>
        /// that will create new instances instance using the supplied <paramref name="instanceCreator"/> 
        /// with the caching as specified by this lifestyle.
        /// </summary>
        /// <typeparam name="TService">The interface or base type that can be used to retrieve the instances.</typeparam>
        /// <param name="instanceCreator">A delegate that will create a new instance of 
        /// <typeparamref name="TService"/> every time it is called.</param>
        /// <param name="container">The <see cref="Container"/> instance for which a 
        /// <see cref="InstanceProducer"/> must be created.</param>
        /// <returns>A new <see cref="InstanceProducer"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either <paramref name="instanceCreator"/> or
        /// <paramref name="container"/> are null references (Nothing in VB).</exception>
        public InstanceProducer CreateProducer<TService>(Func<TService> instanceCreator,
            Container container)
            where TService : class
        {
            return new InstanceProducer(typeof(TService),
                this.CreateRegistration<TService>(instanceCreator, container));
        }
        
        /// <summary>
        /// Creates a new <see cref="InstanceProducer"/> instance for the given <paramref name="serviceType"/>
        /// that will create new instances of specified <paramref name="implementationType"/> with the 
        /// caching as specified by this lifestyle.
        /// </summary>
        /// <param name="serviceType">The interface or base type that can be used to retrieve the instances.</param>
        /// <param name="implementationType">The concrete type that will be registered.</param>
        /// <param name="container">The <see cref="Container"/> instance for which a 
        /// <see cref="Registration"/> must be created.</param>
        /// <returns>A new <see cref="InstanceProducer"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when on of the supplied arguments is a null 
        /// reference (Nothing in VB).</exception>
        public InstanceProducer CreateProducer(Type serviceType, Type implementationType, Container container)
        {
            return new InstanceProducer(serviceType,
                this.CreateRegistration(serviceType, implementationType, container));
        }

        /// <summary>
        /// Creates a new <see cref="Registration"/> instance defining the creation of the
        /// specified <typeparamref name="TConcrete"/> with the caching as specified by this lifestyle.
        /// </summary>
        /// <typeparam name="TConcrete">The concrete type that will be registered.</typeparam>
        /// <param name="container">The <see cref="Container"/> instance for which a 
        /// <see cref="Registration"/> must be created.</param>
        /// <returns>A new <see cref="Registration"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="container"/> is a null
        /// reference (Nothing in VB).</exception>
        public Registration CreateRegistration<TConcrete>(Container container)
            where TConcrete : class
        {
            Requires.IsNotNull(container, "container");
            
            return this.CreateRegistrationCore<TConcrete, TConcrete>(container);
        }

        /// <summary>
        /// Creates a new <see cref="Registration"/> instance defining the creation of the
        /// specified <typeparamref name="TImplementation"/> with the caching as specified by this lifestyle.
        /// </summary>
        /// <typeparam name="TService">The interface or base type that can be used to retrieve the instances.</typeparam>
        /// <typeparam name="TImplementation">The concrete type that will be created.</typeparam>
        /// <param name="container">The <see cref="Container"/> instance for which a 
        /// <see cref="Registration"/> must be created.</param>
        /// <returns>A new <see cref="Registration"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="container"/> is a null
        /// reference (Nothing in VB).</exception>
        public Registration CreateRegistration<TService, TImplementation>(Container container)
            where TImplementation : class, TService
            where TService : class
        {
            Requires.IsNotNull(container, "container");

            return this.CreateRegistrationCore<TService, TImplementation>(container);
        }

        /// <summary>
        /// Creates a new <see cref="Registration"/> instance defining the creation of the
        /// specified <typeparamref name="TService"/> using the supplied <paramref name="instanceCreator"/> 
        /// with the caching as specified by this lifestyle.
        /// </summary>
        /// <typeparam name="TService">The interface or base type that can be used to retrieve the instances.</typeparam>
        /// <param name="instanceCreator">A delegate that will create a new instance of 
        /// <typeparamref name="TService"/> every time it is called.</param>
        /// <param name="container">The <see cref="Container"/> instance for which a 
        /// <see cref="Registration"/> must be created.</param>
        /// <returns>A new <see cref="Registration"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either <paramref name="instanceCreator"/> or
        /// <paramref name="container"/> are null references (Nothing in VB).</exception>
        public Registration CreateRegistration<TService>(Func<TService> instanceCreator, Container container)
            where TService : class
        {
            Requires.IsNotNull(instanceCreator, "instanceCreator");
            Requires.IsNotNull(container, "container");

            var registration = this.CreateRegistrationCore<TService>(instanceCreator, container);

            registration.WrapsInstanceCreationDelegate = true;

            return registration;
        }
                
        /// <summary>
        /// Creates a new <see cref="Registration"/> instance defining the creation of the
        /// specified <paramref name="concreteType"/> with the caching as specified by this lifestyle.
        /// This method might fail when run in a partial trust sandbox when <paramref name="concreteType"/>
        /// is an internal type.
        /// </summary>
        /// <param name="concreteType">The concrete type that will be registered.</param>
        /// <param name="container">The <see cref="Container"/> instance for which a 
        /// <see cref="Registration"/> must be created.</param>
        /// <returns>A new <see cref="Registration"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when on of the supplied arguments is a null 
        /// reference (Nothing in VB).</exception>
        public Registration CreateRegistration(Type concreteType, Container container)
        {
            Requires.IsNotNull(concreteType, "concreteType");

            return this.CreateRegistration(concreteType, concreteType, container);
        }

        /// <summary>
        /// Creates a new <see cref="Registration"/> instance defining the creation of the
        /// specified <paramref name="implementationType"/> with the caching as specified by this lifestyle.
        /// This method might fail when run in a partial trust sandbox when <paramref name="implementationType"/>
        /// is an internal type.
        /// </summary>
        /// <param name="serviceType">The interface or base type that can be used to retrieve the instances.</param>
        /// <param name="implementationType">The concrete type that will be registered.</param>
        /// <param name="container">The <see cref="Container"/> instance for which a 
        /// <see cref="Registration"/> must be created.</param>
        /// <returns>A new <see cref="Registration"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when on of the supplied arguments is a null 
        /// reference (Nothing in VB).</exception>
        public Registration CreateRegistration(Type serviceType, Type implementationType, Container container)
        {
            Requires.IsNotNull(serviceType, "serviceType");
            Requires.IsNotNull(implementationType, "implementationType");
            Requires.IsNotNull(container, "container");

            Requires.IsReferenceType(serviceType, "serviceType");
            Requires.IsReferenceType(implementationType, "implementationType");

            Requires.ServiceIsAssignableFromImplementation(serviceType, implementationType, 
                "implementationType");

            var closedCreateRegistrationMethod = OpenCreateRegistrationTServiceTImplementationMethod
                .MakeGenericMethod(serviceType, implementationType);

            try
            {
                return (Registration)closedCreateRegistrationMethod.Invoke(this, new object[] { container });
            }
            catch (MemberAccessException ex)
            {
                throw BuildUnableToResolveTypeDueToSecurityConfigException(implementationType, ex, 
                    "implementationType");
            }
        }
       
        /// <summary>
        /// Creates a new <see cref="Registration"/> instance defining the creation of the
        /// specified <paramref name="serviceType"/>  using the supplied <paramref name="instanceCreator"/> 
        /// with the caching as specified by this lifestyle.
        /// </summary>
        /// <param name="serviceType">The interface or base type that can be used to retrieve the instances.</param>
        /// <param name="instanceCreator">The delegate that will be responsible for creating new instances.</param>
        /// <param name="container">The <see cref="Container"/> instance for which a 
        /// <see cref="Registration"/> must be created.</param>
        /// <returns>A new <see cref="Registration"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when on of the supplied arguments is a null 
        /// reference (Nothing in VB).</exception>
        public Registration CreateRegistration(Type serviceType, Func<object> instanceCreator,
            Container container)
        {
            Requires.IsNotNull(serviceType, "serviceType");
            Requires.IsNotNull(instanceCreator, "instanceCreator");
            Requires.IsNotNull(container, "container");

            Requires.IsReferenceType(serviceType, "serviceType");

            var closedCreateRegistrationMethod = OpenCreateRegistrationTServiceFuncMethod
                .MakeGenericMethod(serviceType);

            try
            {
                // Build the following delegate: () => (ServiceType)instanceCreator();
                var typeSafeInstanceCreator = ConvertDelegateToTypeSafeDelegate(serviceType, instanceCreator);
                
                return (Registration)closedCreateRegistrationMethod.Invoke(this, 
                    new object[] { typeSafeInstanceCreator, container });
            }
            catch (MemberAccessException ex)
            {
                throw BuildUnableToResolveTypeDueToSecurityConfigException(serviceType, ex, "serviceType");
            }
        }

        internal Registration CreateRegistration(Type serviceType, Type implementationType,
            Container container, params OverriddenParameter[] overriddenParameters)
        {
            var registration = this.CreateRegistration(serviceType, implementationType, container);

            registration.SetParameterOverrides(overriddenParameters);

            return registration;
        }
        
        /// <summary>
        /// When overridden in a derived class, 
        /// creates a new <see cref="Registration"/> instance defining the creation of the
        /// specified <typeparamref name="TImplementation"/> with the caching as specified by this lifestyle.
        /// </summary>
        /// <typeparam name="TService">The interface or base type that can be used to retrieve the instances.</typeparam>
        /// <typeparam name="TImplementation">The concrete type that will be registered.</typeparam>
        /// <param name="container">The <see cref="Container"/> instance for which a 
        /// <see cref="Registration"/> must be created.</param>
        /// <returns>A new <see cref="Registration"/> instance.</returns>
        /// <remarks>
        /// If you are implementing your own lifestyle, override this method to implement the code necessary 
        /// to create and return a new <see cref="Registration"/>. Note that you should <b>always</b> create
        /// a new <see cref="Registration"/> instance. They should never be cached.
        /// </remarks>
        protected abstract Registration CreateRegistrationCore<TService, TImplementation>(Container container)
            where TImplementation : class, TService
            where TService : class;

        /// <summary>
        /// When overridden in a derived class, 
        /// creates a new <see cref="Registration"/> instance defining the creation of the
        /// specified <typeparamref name="TService"/> using the supplied <paramref name="instanceCreator"/> 
        /// with the caching as specified by this lifestyle.
        /// </summary>
        /// <typeparam name="TService">The interface or base type that can be used to retrieve the instances.</typeparam>
        /// <param name="instanceCreator">A delegate that will create a new instance of 
        /// <typeparamref name="TService"/> every time it is called.</param>
        /// <param name="container">The <see cref="Container"/> instance for which a 
        /// <see cref="Registration"/> must be created.</param>
        /// <returns>A new <see cref="Registration"/> instance.</returns>
        /// <remarks>
        /// If you are implementing your own lifestyle, override this method to implement the code necessary 
        /// to create and return a new <see cref="Registration"/>. Note that you should <b>always</b> create
        /// a new <see cref="Registration"/> instance. They should never be cached.
        /// </remarks>
        protected abstract Registration CreateRegistrationCore<TService>(Func<TService> instanceCreator,
            Container container)
            where TService : class;

        private static object ConvertDelegateToTypeSafeDelegate(Type serviceType, Func<object> instanceCreator)
        {
            // Build the following delegate: () => (ServiceType)instanceCreator();
            var invocationExpression =
                Expression.Invoke(Expression.Constant(instanceCreator), new Expression[0]);

            var convertExpression = Expression.Convert(invocationExpression, serviceType);

            var parameters = new ParameterExpression[0];

            // This might throw an MemberAccessException when serviceType is internal while we're running in
            // a Silverlight sandbox.
            return Expression.Lambda(convertExpression, parameters).Compile();
        }

        private static ArgumentException BuildUnableToResolveTypeDueToSecurityConfigException(
            Type type, MemberAccessException innerException, string paramName)
        {
            // This happens when the user tries to resolve an internal type inside a (Silverlight) sandbox.
            return new ArgumentException(
                StringResources.UnableToResolveTypeDueToSecurityConfiguration(type, innerException) + 
                "\nparamName: " + paramName, innerException);
        }

        private static MethodInfo GetMethod(Expression<Action<Lifestyle>> methodCall)
        {
            var body = methodCall.Body as MethodCallExpression;
            return body.Method.GetGenericMethodDefinition();
        }
    }
}