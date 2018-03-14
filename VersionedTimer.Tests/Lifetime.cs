using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VersionedTimer.Tests.Harness;

namespace VersionedTimer.Tests
{
    [TestClass]
    public class Lifetime
    {
        /// <summary>
        /// Verifies the constructor does not abide null callback parameters.
        /// </summary>
        [TestMethod]
        public void Constructor_DoesNotAllow_NullCallbacks()
        {
            Assert2.Throws<ArgumentNullException>( () => new VersionedTimer<int>( 0, null ) );
        }
    }
}
