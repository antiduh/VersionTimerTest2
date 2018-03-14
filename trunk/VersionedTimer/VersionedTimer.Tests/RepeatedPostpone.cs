using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VersionedTimer.Tests.Harness;

namespace VersionedTimer.Tests
{
    /// <summary>
    /// Tests that the timer eventually fires when postponed repeatedly.
    /// </summary>
    [TestClass]
    public class RepeatedPostpone
    {
        /// <summary>
        /// Tests repeated postponment for a single-shot timer.
        /// </summary>
        [TestMethod]
        public void RepeatedPostpone_SingleShot()
        {
            int count = 200;
            SimpleTimerHarness harness = new SimpleTimerHarness();
            VersionedTimer<int> timer = new VersionedTimer<int>( 456, harness.Callback );

            using( timer )
            {
                for( int i = 0; i < count; i++ )
                {
                    timer.Change( 1000, Timeout.Infinite, i );

                    Thread.Sleep( 50 );
                }

                // Verify the timer fires exactly once.
                harness.Wait();

                Assert.AreEqual( 1, harness.Callbacks );
                Assert.AreEqual( 456, harness.ObservedState );
                Assert.AreEqual( count - 1, harness.ObservedVersion );
            }
        }

        /// <summary>
        /// Tests repeated postponement for a multi-shot timer.
        /// </summary>
        [TestMethod]
        public void RepeatedPostpone_MultiShot()
        {
            int count = 200;
            SimpleTimerHarness harness = new SimpleTimerHarness();
            VersionedTimer<int> timer = new VersionedTimer<int>( 456, harness.Callback );

            using( timer )
            {
                for( int i = 0; i < count; i++ )
                {
                    timer.Change( 1000, 100, i );

                    Thread.Sleep( 50 );
                }

                // Verify the timer fires repeatedly.
                for( int i = 0; i < 10; i++ )
                {
                    harness.Wait();
                }

                timer.Change( Timeout.Infinite, Timeout.Infinite, 0 );

                Assert.AreEqual( 10, harness.Callbacks );
                Assert.AreEqual( 456, harness.ObservedState );
                Assert.AreEqual( count - 1, harness.ObservedVersion );
            }
        }
    }
}
