// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="NUnit.MultiCore Development Team">
//   NUnit.MultiCore Development Team
// </copyright>
// <summary>
//   Defines the main console test runner.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace NUNit.MultiCore.ConsoleTestRunner
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Reflection;

    /// <summary>Defines the main console test runner.</summary>
    [Serializable]
    public class Program
    {
        /// <summary>Path to the assembly under test.</summary>
        private static string assemblyPath;

        /// <summary>File path to output xml to.</summary>
        private static string outputXmlPath;

        /// <summary>Set to true if this assembly is running under a new domain.</summary>
        private static bool newDomain;

        /// <summary>
        /// Entry point of the application.
        /// </summary>
        /// <param name="args">The command line arguments that have been specified.</param>
        public static void Main(string[] args)
        {
            Console.WriteLine("Startup");

            var watch = Stopwatch.StartNew();

            PopulateParameters(args);

            var fullAssemblyPath = assemblyPath;

            Console.WriteLine(fullAssemblyPath);

            // If we're not in the correct app domain, set up a new app domain to run in 
            if (!newDomain)
            {
                var setupInfo = new AppDomainSetup
                {
                    ApplicationBase = System.IO.Path.GetDirectoryName(fullAssemblyPath),
                    PrivateBinPath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase),
                    ConfigurationFile = fullAssemblyPath + ".config",
                };

                var domain = AppDomain.CreateDomain("ParallelTestsDomain", null, setupInfo);

                var newArgs = new string[args.Length + 1];
                args.CopyTo(newArgs, 0);
                newArgs[newArgs.Length - 1] = "/innewdomain";

                // Execute this program in the new application domain.  We HAVE to run in the 
                // new domain so we'll have the correct app.config and all the tests will be running in 
                // the correct context. 
                domain.ExecuteAssembly(Assembly.GetExecutingAssembly().CodeBase, newArgs);
            }
            else
            {
                Console.WriteLine("Running tests from {0}", fullAssemblyPath);

                // as we're running in the correct domain, just run the tests. 
                new Runner().RunTests(fullAssemblyPath, outputXmlPath);

                Console.WriteLine("Completed tests in: {0}", watch.Elapsed);
            }
        }

        /// <summary>
        /// Populates the setup variables with the arguments supplied.
        /// </summary>
        /// <param name="args">The command line arguments that have been supplied.</param>
        private static void PopulateParameters(IEnumerable<string> args)
        {
            Console.WriteLine("Populating parameters");

            bool nextIsXmlPath = false;

            foreach (var arg in args)
            {
                switch (arg)
                {
                    case "/xml":
                        nextIsXmlPath = true;
                        break;
                    case "/innewdomain":
                        newDomain = true;
                        break;
                    default:
                        if (arg.StartsWith("/"))
                        {
                            nextIsXmlPath = false;
                        }
                        else if (nextIsXmlPath)
                        {
                            outputXmlPath = System.IO.Directory.GetCurrentDirectory() + @"\" + arg;
                            nextIsXmlPath = false;
                        }
                        else
                        {
                                assemblyPath = arg;
                        }

                        break;
                }
            }
        }
    }
}
