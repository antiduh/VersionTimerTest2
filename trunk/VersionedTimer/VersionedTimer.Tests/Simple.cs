using System;
using System.Linq;
using System.Threading;
using VersionedTimer.Tests.Harness;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

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
                Assert.AreEqual( 123, harness.ObservedState, "Timer fired with wrong state." );
                Assert.AreEqual( 1, harness.ObservedVersion, "Timer fired with wrong version." );
                Assert.AreEqual( 1, harness.Callbacks, "Timer fired wrong number of times." );
                Assert.AreEqual( 0, harness.TimeoutError.TotalMilliseconds, 30, "Timer timeout was inaccurate." ); 
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
            Stopwatch watch = new Stopwatch();

            watch.Start();
            watch.Reset();

            using( var timer = new VersionedTimer<int>( 123, harness.Callback ) )
            {
                harness.ExpectDelays( 75, 150 );
                timer.Change( 75, 150, 1 );
                watch.Start();

                Thread.Sleep( 75 + 150*4 );

                for( int i = 1; i <= 5; i++ )
                {
                    Assert.IsTrue( harness.Wait( 5 * 1000 ), string.Format( "Timer period #{0} never fired.", i ) );
                }

                timer.Change( Timeout.Infinite, Timeout.Infinite, 2 );
                watch.Stop();

                TimeSpan elapsed = watch.Elapsed;

                int numFirings = (int)( ( elapsed.TotalMilliseconds - 75.0 ) / 150.0 ) + 1;

                Assert.AreEqual( 123, harness.ObservedState, 123, "Timer fired with wrong state." );
                Assert.AreEqual( 1, harness.ObservedVersion, "Timer fired with wrong version." );
                Assert.AreEqual( numFirings, harness.Callbacks, "Timer fired wrong number of times." );
                Assert.AreEqual( 0, harness.TimeoutError.TotalMilliseconds, 30, "Timer timeout was inaccurate." );
                Assert.AreEqual( 0, harness.PeriodErrors.Average( x => x.TotalMilliseconds ), 30, "Timer period was inaccurate." );
                Assert.AreEqual( 0, harness.PeriodErrors.Max( x => x.TotalMilliseconds ), 30, "Timer period was inaccurate." );

                Trace.WriteLine( string.Format( 
                    "{0} callbacks occurred in {1:0.000} ms.", 
                    harness.Callbacks, elapsed.TotalMilliseconds 
                ) );
            }
        }

    }
}
