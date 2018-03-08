﻿using System;
using System.Linq;
using System.Threading;
using VersionedTimer.Tests.Harness;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VersionedTimer.Tests
{
    /// <summary>
    /// Tests simple timer usage scenarios, verifying the basic functionality of the timer.
    /// </summary>
    [TestClass]
    public class Simple
    {
        /// <summary>
        /// Verifies that a single-shot timer runs and runs with reasonable accuracy.
        /// </summary>
        [TestMethod]
        public void SingleShot()
        {
            SimpleTimerHarness harness = new SimpleTimerHarness();

            using( var timer = new VersionedTimer<int>( 123, harness.Callback ) )
            {
                harness.ExpectDelays( 100, Timeout.Infinite );
                timer.Change( 100, Timeout.Infinite, 1 );

                // Give the timer some rope to hang itself by if it's accidentally going to fire
                // multiple times.
                Thread.Sleep( 500 );

                Assert.IsTrue( harness.Wait( 5 * 1000 ), "Timer never fired." );
                Assert.AreEqual( harness.ObservedState, 123, "Timer fired with wrong state." );
                Assert.AreEqual( harness.ObservedVersion, 1, "Timer fired with wrong version." );
                Assert.AreEqual( harness.Callbacks, 1, "Timer fired wrong number of times." );
                Assert.AreEqual( harness.TimeoutError.TotalMilliseconds, 0, 30, "Timer timeout was inaccurate." ); 
            }
        }

        /// <summary>
        /// Verifies that a periodic timer runs, and verifies that the timeout and period parameters
        /// are handled correctly.
        /// </summary>
        [TestMethod]
        public void MultiShot()
        {
            SimpleTimerHarness harness = new SimpleTimerHarness();

            using( var timer = new VersionedTimer<int>( 123, harness.Callback ) )
            {
                harness.ExpectDelays( 50, 150 );
                timer.Change( 50, 150, 1 );
                
                Thread.Sleep( 50 + 150*4 );

                for( int i = 1; i <= 5; i++ )
                {
                    Assert.IsTrue( harness.Wait( 5 * 1000 ), string.Format("Timer period #{0} never fired.", i) );
                }


                Assert.AreEqual( harness.ObservedState, 123, "Timer fired with wrong state." );
                Assert.AreEqual( harness.ObservedVersion, 1, "Timer fired with wrong version." );
                Assert.AreEqual( harness.Callbacks, 5, "Timer fired wrong number of times." );
                Assert.AreEqual( harness.TimeoutError.TotalMilliseconds, 0, 30, "Timer timeout was inaccurate." );
                Assert.AreEqual( harness.PeriodErrors.Average( x => x.TotalMilliseconds ), 0, 30, "Timer period was inaccurate." );
                Assert.AreEqual( harness.PeriodErrors.Max( x => x.TotalMilliseconds ), 0, 30, "Timer period was inaccurate." );
            }
        }

    }
}
