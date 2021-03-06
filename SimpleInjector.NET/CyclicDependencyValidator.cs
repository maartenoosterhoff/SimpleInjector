﻿#region Copyright Simple Injector Contributors
/* The Simple Injector is an easy-to-use Inversion of Control library for .NET
 * 
 * Copyright (c) 2013 - 2014 Simple Injector Contributors
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
    using System.Threading;

    /// <summary>
    /// Allows verifying whether a given type has a direct or indirect dependency on itself. Verifying is done
    /// by preventing recursive calls to a IInstanceProvider. An instance of this type is related to a single 
    /// instance of a IInstanceProvider. A RecursiveDependencyValidator instance checks a single 
    /// IInstanceProvider and therefore a single service type.
    /// </summary>
    internal sealed class CyclicDependencyValidator
    {
        private readonly Type typeToValidate;      
        private List<Thread> threads;

        internal CyclicDependencyValidator(Type typeToValidate)
        {
            this.typeToValidate = typeToValidate;
        }

        // Checks whether this is a recursive call (and thus a cyclic dependency) and throw in that case.
        internal void Check()
        {
            // We can lock on this, because RecursiveDependencyValidator is an internal type.
            lock (this)
            {
                if (this.threads == null)
                {
                    this.threads = new List<Thread>(1);
                }

                // We store the current thread to prevent the validator to incorrectly fail when two threads
                // simultaneously trigger the validation.
                if (this.threads.Contains(Thread.CurrentThread))
                {
                    // We currently don't supply any information through the exception message about the 
                    // actual dependency cycle that causes the problem. Using call stack analysis we would be 
                    // able to build a dependency graph and supply it in this exception message, but not 
                    // something we currently do.
                    throw new ActivationException(StringResources.TypeDependsOnItself(this.typeToValidate));
                }

                this.threads.Add(Thread.CurrentThread);
            }
        }

        // Removes the current thread from the list of threads.
        internal void RollBack()
        {
            lock (this)
            {
                this.threads.Remove(Thread.CurrentThread);

                if (this.threads.Count == 0)
                {
                    // An InstanceProducer instance holds a reference to a CyclicDependencyValidator instance,
                    // but will only remove a reference to it when GetInstance is called on that producer
                    // (which only happens for root types). So since the CyclicDependencyValidator instances
                    // will typically stay referenced, we remove the list to lower the amount of memory that
                    // will be in use by Simple Injector.
                    this.threads = null;
                }
            }            
        }
    }
}