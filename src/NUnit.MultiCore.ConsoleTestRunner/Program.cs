// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="NUnit.MultiCore Development Team">
//   NUnit.MultiCore Development Team
// </copyright>
// <summary>
//   Defines the main console test runner.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.IO;
using System.Linq;

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
		private const string SwitchInNewDomain = "/innewdomain";

		/// <summary>Path to the assembly under test.</summary>
		private static List<string> assemblies = new List<string>();

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
			var watch = Stopwatch.StartNew();

			PopulateParameters(args);

			var currentDirectory = Environment.CurrentDirectory;

			// If we're not in the correct app domain, set up a new app domain to run in 
			if (!newDomain)
			{
				foreach (var assembly in assemblies)
				{
					var fullAssemblyPath = Path.Combine(currentDirectory, assembly);
					var setupInfo = new AppDomainSetup
					                	{
					                		ApplicationBase = Path.Combine(currentDirectory, Path.GetDirectoryName(assembly)),
					                		PrivateBinPath = fullAssemblyPath,
					                		ConfigurationFile = fullAssemblyPath + ".config",
					                	};

					var domain = AppDomain.CreateDomain("ParallelTestsDomain", null, setupInfo);

					// Execute this program in the new application domain.  We HAVE to run in the 
					// new domain so we'll have the correct app.config and all the tests will be running in 
					// the correct context. 
					var newArgs = new[] {SwitchInNewDomain, fullAssemblyPath, outputXmlPath != null ? "/xml " + outputXmlPath : ""};
					domain.ExecuteAssembly(Assembly.GetExecutingAssembly().Location, newArgs);
				}
			}
			else
			{
				var assemblyPath = assemblies.First();
				Console.WriteLine("Running tests from {0}", assemblyPath);

				// as we're running in the correct domain, just run the tests. 
				var runner = new Runner();
				runner.RunTests(assemblyPath, outputXmlPath);

				Console.WriteLine("Completed tests in: {0}", watch.Elapsed);
			}
		}
	


        /// <summary>
        /// Populates the setup variables with the arguments supplied.
        /// </summary>
        /// <param name="args">The command line arguments that have been supplied.</param>
        private static void PopulateParameters(IEnumerable<string> args)
        {
            bool nextIsXmlPath = false;

            foreach (var arg in args)
            {
                switch (arg)
                {
                    case "/xml":
                        nextIsXmlPath = true;
                        break;
                    case SwitchInNewDomain:
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
                            assemblies.Add(arg);
						}

                        break;
                }
            }
        }
    }
}
