// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ParallelTestRunner.cs" company="NUnit.MultiCore Development Team">
//   NUnit.MultiCore Development Team
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace NUnit.MultiCore.TestRunner
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;

    using Core;
    using Interfaces;

    /// <summary>
    /// Class used to run tests within an assembly in parallel.
    /// </summary>
    public class ParallelTestRunner
    {
        #region Constants and Fields

        /// <summary>Used to hold exclusive locks on resources.</summary>
        private readonly object lockObject = new object();

        #endregion

        #region Properties

        /// <summary>Gets or sets the filter to use to exclude ignored tests.</summary>
        private IgnoreFilter Filter { get; set; }

        /// <summary>Gets or sets the listener used to capture test results.</summary>
        private ParallelListener Listener { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Runs all of the tests defined in the calling assembly. 
        /// This is intended to be called directly from a test fixture.
        /// </summary>
        /// <returns>
        /// The result of running the suite of tests.
        /// </returns>
        public TestResult RunTestsInParallel()
        {
            return this.RunTestsInParallelImpl(
                Assembly.GetCallingAssembly(), new StackTrace().GetFrame(1).GetMethod().DeclaringType);
        }

        /// <summary>
        /// Runs all of the tests defined in the specified assembly. 
        /// </summary>
        /// <param name="assemblyToTest">
        /// The assembly defining the tests to be run.
        /// </param>
        /// <returns>
        /// The result of running the suite of tests.
        /// </returns>
        public TestResult RunTestsInParallel(Assembly assemblyToTest)
        {
            return this.RunTestsInParallelImpl(assemblyToTest, new StackTrace().GetFrame(1).GetMethod().DeclaringType);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if the specified type has the Parallelizable attribute. 
        /// </summary>
        /// <param name="type">
        /// The type to be tested.
        /// </param>
        /// <returns>
        /// True if the specified type has the Parallelizable attribute.
        /// </returns>
        private static bool IsParallelTest(Type type)
        {
            return type.GetCustomAttributes(false).OfType<ParallelizableAttribute>().Any();
        }

        /// <summary>
        /// Flattens the tree of results into a single list. 
        /// </summary>
        /// <param name="results">
        /// The results to be flattened.
        /// </param>
        /// <returns>
        /// An enumerator over a list of the results and flattened sub results.
        /// </returns>
        private IEnumerable<TestResult> AllResults(IList results)
        {
            if (results != null)
            {
                foreach (object result in results)
                {
                    if (result is TestResult)
                    {
                        yield return result as TestResult;
                        foreach (var subResult in this.AllResults((result as TestResult).Results))
                        {
                            yield return subResult;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Runs all of the tests in the specified test queue.
        /// </summary>
        /// <param name="testQueue">
        /// The test queue.
        /// </param>
        private void RunTestsInQueue(object testQueue)
        {
            var queue = testQueue as Queue<Test>;
            if (queue != null)
            {
                while (queue.Count > 0)
                {
                    Test nextTest = null;
                    lock (this.lockObject)
                    {
                        if (queue.Count > 0)
                        {
                            nextTest = queue.Dequeue();
                        }
                    }

                    if (nextTest != null)
                    {
                        TestContext.Save();
                        nextTest.Run(this.Listener, this.Filter);
                    }
                }
            }
        }

        /// <summary>
        /// Runs all of the tests defined in the specified assembly, specifically not running 
        /// any of the tests in the calling type. 
        /// </summary>
        /// <param name="assemblyToTest">
        /// The assembly to be tested.
        /// </param>
        /// <param name="callingType">
        /// No tests will be run that are defined in this type.
        /// </param>
        /// <returns>
        /// The result of running the suite of tests.
        /// </returns>
        private TestResult RunTestsInParallelImpl(Assembly assemblyToTest, Type callingType)
        {
            // NUnit requires this initialization step before any tests can be run 
            CoreExtensions.Host.InitializeService();

            this.Listener = new ParallelListener();
            this.Filter = new IgnoreFilter(this.Listener);

            var concurrentTestFixtures = new Queue<Test>();
            var nonConcurrentTestFixtures = new List<Test>();

            try
            {
                foreach (var type in assemblyToTest.GetTypes())
                {
                    if (callingType != type && TestFixtureBuilder.CanBuildFrom(type))
                    {
                        Test newFixture = TestFixtureBuilder.BuildFrom(type);
						
                        if (IsParallelTest(newFixture.FixtureType))
                        {
                            concurrentTestFixtures.Enqueue(newFixture);
                        }
                        else
                        {
                            nonConcurrentTestFixtures.Add(newFixture);
                        }
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                Console.WriteLine("ReflectionTypeLoadException caught...");
                foreach (Exception loaderException in ex.LoaderExceptions)
                {
                    Console.WriteLine("Loader Exception --------------------");
                    Console.WriteLine("Message:{0}", loaderException.Message);
                    if (loaderException is FileNotFoundException)
                    {
                        Console.WriteLine((loaderException as FileNotFoundException).FusionLog);
                    }

                    Console.WriteLine("StackTrace: {0}", loaderException.StackTrace);
                }

                throw;
            }

            var threads = new List<Thread>();

            // Start up threads to run the concurrent fixtures 
            // Note: We start up twice the number of threads as there are processors
            // This is because ad-hoc testing has shown this to give a better speed increase.
            for (int i = 0; i < Environment.ProcessorCount << 1; ++i)
            {
                var ts = new ParameterizedThreadStart(this.RunTestsInQueue);
                var t = new Thread(ts);

                t.Start(concurrentTestFixtures);

                threads.Add(t);
            }

            // Wait for all of the concurrent tests to finish 
            foreach (var thread in threads)
            {
                thread.Join();
            }

            // Run the non-concurrent tests 
            foreach (var test in nonConcurrentTestFixtures)
            {
                TestContext.Save();
                test.Run(this.Listener, this.Filter);
            }

            // Find out what the failures were 
            IEnumerable<TestResult> failuresAndErrors = from result in this.AllResults(this.Listener.Results)
                                                        where
                                                            (result.IsFailure || result.IsError) &&
                                                            (result.Results == null)
                                                        select result;

            // Find out was the successes were 
            IEnumerable<TestResult> successes = from result in this.AllResults(this.Listener.Results)
                                                where result.IsSuccess && (result.Results == null)
                                                select result;

            // Report the errors if there are any 
			Console.WriteLine();
            if (failuresAndErrors.Any())
            {
                foreach (TestResult failure in failuresAndErrors)
                {
                    Console.WriteLine("------------------------------------------------");
                    Console.WriteLine(failure.Test.TestName + " failed");
                    Console.WriteLine(failure.Message);
                    Console.WriteLine(failure.StackTrace);
                }
            }

            Console.WriteLine("=================================================");
            Console.WriteLine(
                string.Format(
                    "ParallelTestRunner finished: {0} passed, {1} failed, {2} skipped.", 
                    successes.Count(), 
                    failuresAndErrors.Count(), 
                    this.Listener.Skipped));
            Console.WriteLine("=================================================");

            // Build up the results for return 
            var finalResult = new TestResult(new TestName());
            foreach (var result in this.Listener.Results)
            {
                finalResult.AddResult(result);
            }

            return finalResult;
        }

        #endregion

        /// <summary>
        /// The parallel listener.
        /// </summary>
        private class ParallelListener : EventListener
        {
            #region Constants and Fields

            /// <summary>The results of the tests.</summary>
            private readonly List<TestResult> results = new List<TestResult>();

            /// <summary>The number skipped tests.</summary>
            private int skipped;

            #endregion

            #region Properties

            /// <summary>
            /// Gets the number of skipped tests.
            /// </summary>
            public int Skipped
            {
                get
                {
                    return this.skipped;
                }
            }

            /// <summary>
            /// Gets the results of the tests.
            /// </summary>
            public List<TestResult> Results 
            { 
                get
                {
                    return this.results; 
                } 
            }

            #endregion

            #region Public Methods

            /// <summary>
            /// Increases the number of skipped tests.
            /// </summary>
            public void IncreaseSkipped()
            {
                Interlocked.Increment(ref this.skipped);
            }

            #endregion

            #region Implemented Interfaces

            #region EventListener

            /// <summary>
            /// Called when a run is finished with an exception.
            /// </summary>
            /// <param name="exception">
            /// The exception that was thrown.
            /// </param>
            public void RunFinished(Exception exception)
            {
			}

            /// <summary>
            /// Called when a run is finished with a result.
            /// </summary>
            /// <param name="result">
            /// The result of the test run.
            /// </param>
            public void RunFinished(TestResult result)
            {
            }

            /// <summary>
            /// Called when a run of tests is started.
            /// </summary>
            /// <param name="name">
            /// The name of the tests.
            /// </param>
            /// <param name="testCount">
            /// The number of tests to be run.
            /// </param>
            public void RunStarted(string name, int testCount)
            {
            }

            /// <summary>
            /// Called when the suite finished running.
            /// </summary>
            /// <param name="result">
            /// The result of the test run.
            /// </param>
            public void SuiteFinished(TestResult result)
            {
                this.results.Add(result);
            }

            /// <summary>
            /// Called when the test suite run is started.
            /// </summary>
            /// <param name="testName">
            /// The test name.
            /// </param>
            public void SuiteStarted(TestName testName)
            {
            }

            /// <summary>
            /// Called when an individual test has finished.
            /// </summary>
            /// <param name="result">
            /// The result.
            /// </param>
            public void TestFinished(TestResult result)
            {
				Console.Write(result.IsFailure ? "F" : ".");
            }

            /// <summary>
            /// Called to communicate the output of a test run.
            /// </summary>
            /// <param name="testOutput">
            /// The test output.
            /// </param>
            public void TestOutput(TestOutput testOutput)
            {
            }

            /// <summary>
            /// Called when a test is started.
            /// </summary>
            /// <param name="testName">
            /// The test name.
            /// </param>
            public void TestStarted(TestName testName)
            {
            }

            /// <summary>
            /// Called when a test causes an unhandled exception.
            /// </summary>
            /// <param name="exception">
            /// The exception that was unhandled.
            /// </param>
            public void UnhandledException(Exception exception)
            {
            }

            #endregion

            #endregion
        }

        /// <summary>
        /// Filter for filtering out ignored tests.
        /// </summary>
        private class IgnoreFilter : ITestFilter
        {
            #region Constants and Fields

            /// <summary>
            /// The listener.
            /// </summary>
            private readonly ParallelListener listener;

            #endregion

            #region Constructors and Destructors

            /// <summary>
            /// Initializes a new instance of the <see cref="IgnoreFilter"/> class.
            /// </summary>
            /// <param name="listener">
            /// The listener.
            /// </param>
            public IgnoreFilter(ParallelListener listener)
            {
                this.listener = listener;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Gets a value indicating whether the filter is empty.
            /// </summary>
            public bool IsEmpty
            {
                get
                {
                    return false;
                }
            }

            #endregion

            #region Implemented Interfaces

            #region ITestFilter

            /// <summary>
            /// Returns true if the specified test should be included (ignored tests
            /// should not be included in a test run). Will also 
            /// tell the test listener that a test has been ignored.
            /// </summary>
            /// <param name="test">
            /// The test to be checked.
            /// </param>
            /// <returns>
            /// True if the test is NOT ignored, false if the test should be ignored.
            /// </returns>
            public bool Match(ITest test)
            {
            	if (test.RunState == RunState.Ignored ||
					test.Parent.RunState == RunState.Ignored)
                {
					Console.Write("I");
                    this.listener.IncreaseSkipped();
                    return false;
                }
            	if (test.RunState == RunState.Explicit ||
					test.Parent.RunState == RunState.Explicit)
                {
					Console.Write("X");
					this.listener.IncreaseSkipped();
                    return false;
                }

                return true;
            }

            /// <summary>
            /// Determines if a test should be passed to the test runner.
            /// </summary>
            /// <param name="test">
            /// The test to be checked.
            /// </param>
            /// <returns>
            /// True if this test should be run, false otherwise.
            /// </returns>
            public bool Pass(ITest test)
            {
                return this.Match(test);
            }

            #endregion

            #endregion
        }
    }
}