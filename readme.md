This is not a distributed test runner.  This project allows you to run NUnit tests in parallel on a single computer e.g. build box or developer prior to checkin.

Usage:

- replace the call to NUnit console in your build script with one that calls NUnit.MultiCore.ConsoleTestRunner.exe instead.

all TestFixtures in your assemblies will be run in parallel by default.

forked on 19 Dec 2011 from http://nunitmulticore.codeplex.com/SourceControl/list/changesets most recent build (1250)

If you have questions or comments about this project, please contact us at <mailto:opensource@mvbalaw.com>
