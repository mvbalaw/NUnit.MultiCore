// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ParallelizableAttribute.cs" company="NUnit.MultiCore Development Team">
//   NUnit.MultiCore Development Team
// </copyright>
// <summary>
//   Marks a test fixture as being parallelizable - that is, it can be run at the
//   same time as other test fixtures.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace NUnit.MultiCore.Interfaces
{
    using System;

    /// <summary>
    /// Marks a test fixture as being parallelizable - that is, it can be run at the
    /// same time as other test fixtures.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ParallelizableAttribute : Attribute
    {
    }
}
