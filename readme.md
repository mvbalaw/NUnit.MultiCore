This is not a distributed test runner.  This project allows you to run NUnit tests in parallel on a single computer e.g. build box or developer prior to checkin.

Usage:

- add the [Parallel] attribute to TestFixtures you want to run in parallel.
- replace the call to NUnit console in your build script with one that calls NUnit.MultiCore.ConsoleTestRunner.exe instead.

forked on 19 Dec 2011 from http://nunitmulticore.codeplex.com/SourceControl/list/changesets most recent build (1250)