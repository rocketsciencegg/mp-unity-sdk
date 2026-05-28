using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using NUnit.Framework;

namespace RocketScience.Services.WireDirect.Tests.Stubs
{
    public class TestStub
    {
        public int mainThread { get; set; }
        private ConcurrentBag<Invocation> Calls { get; } = new ConcurrentBag<Invocation>();

        public void AssertWasCalled(string methodName)
        {
            if (!Calls.Any(x => x.MethodName.Equals(methodName, System.StringComparison.InvariantCultureIgnoreCase)))
                Assert.Fail($"Expected {methodName} to be called at least once, but it was not called.");
        }

        // Will throw an exception if called from a thread different from the instantiation thread.
        public void AssertIsOnMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThread)
            {
                Assert.Fail("Not on main thread.");
            }
        }

        public void AssertWasCalled(string methodName, int times)
        {
            var count = Calls.Count(x => x.MethodName.Equals(methodName, System.StringComparison.InvariantCultureIgnoreCase));
            if (count != times)
            {
                Assert.Fail($"Expected {methodName} to be called {times} times, but it was called {count} times.");
            }
        }

        public void AssertWasCalledWithArguments(string methodName, params object[] args)
        {
            var methodCalls = Calls.Where(x => x.MethodName.Equals(methodName, System.StringComparison.InvariantCultureIgnoreCase));
            var matches = methodCalls.Where(x =>
            {
                if (x.Arguments.Count != args.Length) return false;

                for (var i = 0; i < x.Arguments.Count; i++)
                {
                    if (x.Arguments[i] != args[i]) return false;
                }

                return true;
            });

            if (!matches.Any())
            {
                // This could probably be improved to detail the arguments and have a better message but for now can just use breakpoints
                Assert.Fail($"Expected {methodName} to have been called with the specified arguments, but it was not.");
            }
        }

        public void AssertArgumentsMatch(string methodName, Func<List<object>, bool> predicate)
        {
            var methodCalls = Calls.Where(x => x.MethodName.Equals(methodName, StringComparison.InvariantCultureIgnoreCase));
            var matches = methodCalls.Where(x => predicate(x.Arguments));

            if (!matches.Any())
            {
                // This could probably be improved to detail the arguments and have a better message but for now can just use breakpoints
                Assert.Fail($"Expected {methodName} to have been called with arguments that match the predicate, but it was not.");
            }
        }

        public void AddCall(string methodName, params object[] args)
        {
            Calls.Add(new Invocation
            {
                MethodName = methodName,
                Arguments = args.ToList()
            });
        }
    }

    public class Invocation
    {
        public string MethodName { get; set; }
        public List<object> Arguments { get; set; }
    }
}
