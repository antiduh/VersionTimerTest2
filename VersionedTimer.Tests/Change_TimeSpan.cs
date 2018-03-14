using System;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VersionedTimer.Tests.Harness;

namespace VersionedTimer.Tests
{
    /// <summary>
    /// Verifies the timer supports configuring the timeout.
    /// </summary>
    [TestClass]
    public class Change_TimeSpan
    {
        /// <summary>
        /// A TimeSpan that is slight larger than the maximum supported TimeSpan.
        /// </summary>
        private static TimeSpan excessiveSpan;

        /// <summary>
        /// A TimeSpan that is slightly less negative than -1 ms, eg, -0.999999...990 ms.
        /// </summary>
        private static TimeSpan nearOneMsMinus;

        /// <summary>
        /// A TimeSpan that is slightly more negative than -1 ms, eg, -1.0000..1 ms.
        /// </summary>
        private static TimeSpan nearOneMsPlus;
        static Change_TimeSpan()
        {
            var oneTick = TimeSpan.FromTicks( 1 );

            nearOneMsPlus = MSecs( -1 ) + oneTick;
            nearOneMsMinus = MSecs( -1 ) - oneTick;

            excessiveSpan = VTimeout.MaxTimeout + oneTick;
        }

        /// <summary>
        /// Verifies that the timer allows Changing to infinite TimeSpans for the timeout and the
        /// period, independently.
        /// </summary>
        [TestMethod]
        public void Change_ViaTimeSpan_Allows_Infinite()
        {
            SimpleTimerHarness harness = new SimpleTimerHarness();
            VersionedTimer<int> timer = new VersionedTimer<int>( 123, harness.Callback );

            using( timer )
            {
                timer.Change( MSecs( 10 ), Timeout.InfiniteTimeSpan, 0 );
                timer.Change( Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan, 1 );
            }
        }

        /// <summary>
        /// Verifies that the timer allows Changing to zero TimeSpans for the timeout and period, independently.
        /// </summary>
        [TestMethod]
        public void Change_ViaTimeSpan_Allows_Zero()
        {
            SimpleTimerHarness harness = new SimpleTimerHarness();
            VersionedTimer<int> timer = new VersionedTimer<int>( 123, harness.Callback );

            using( timer )
            {
                timer.Change( TimeSpan.Zero, Timeout.InfiniteTimeSpan, 1 );
                timer.Change( TimeSpan.Zero, TimeSpan.Zero, 2 );
            }
        }

        /// <summary>
        /// Verifies the timer throws exceptions when trying to provide excessively large TimeSpans
        /// to Change.
        /// </summary>
        [TestMethod]
        public void Change_ViaTimeSpan_DoesNotAllow_ExcessiveSpan()
        {
            SimpleTimerHarness harness = new SimpleTimerHarness();
            VersionedTimer<int> timer = new VersionedTimer<int>( 123, harness.Callback );

            using( timer )
            {
                Assert2.Throws<ArgumentException>( () =>
                {
                    timer.Change( excessiveSpan, Timeout.InfiniteTimeSpan, 0 );
                } );

                Assert2.Throws<ArgumentException>( () =>
                {
                    timer.Change( TimeSpan.Zero, excessiveSpan, 0 );
                } );

                Assert2.Throws<ArgumentException>( () =>
                {
                    timer.Change( excessiveSpan, excessiveSpan, 0 );
                } );
            }
        }

        /// <summary>
        /// Verifies the timer rejects all negative period values that are not Timeout.InfiniteTimeSpan.
        /// </summary>
        [TestMethod]
        public void Change_ViaTimeSpan_DoesNotAllow_Negative_Period()
        {
            SimpleTimerHarness harness = new SimpleTimerHarness();
            VersionedTimer<int> timer = new VersionedTimer<int>( 123, harness.Callback );

            using( timer )
            {
                // Smallest negative possible.
                Assert2.Throws<ArgumentException>( () =>
                {
                    timer.Change( TimeSpan.Zero, TimeSpan.FromTicks( -1 ), 0 );
                } );

                // Edge case near -1 ms (a special value), low side.
                Assert2.Throws<ArgumentException>( () =>
                {
                    timer.Change( TimeSpan.Zero, nearOneMsPlus, 0 );
                } );

                // Edge case near -1 ms (a special value), high side.
                Assert2.Throws<ArgumentException>( () =>
                {
                    timer.Change( TimeSpan.Zero, nearOneMsMinus, 0 );
                } );

                // More negative.
                Assert2.Throws<ArgumentException>( () =>
                {
                    timer.Change( TimeSpan.Zero, MSecs( -2 ), 0 );
                } );
            }
        }

        /// <summary>
        /// Verifies the timer rejects all negative timeout values that are not Timeout.InfiniteTimeSpan.
        /// </summary>
        [TestMethod]
        public void Change_ViaTimeSpan_DoesNotAllow_Negative_Timeout()
        {
            SimpleTimerHarness harness = new SimpleTimerHarness();
            VersionedTimer<int> timer = new VersionedTimer<int>( 123, harness.Callback );

            using( timer )
            {
                // Smallest negative possible.
                Assert2.Throws<ArgumentException>( () =>
                {
                    timer.Change( TimeSpan.FromTicks( -1 ), Timeout.InfiniteTimeSpan, 0 );
                } );

                // Edge case near -1 ms (a special value), low side.
                Assert2.Throws<ArgumentException>( () =>
                {
                    timer.Change( nearOneMsPlus, Timeout.InfiniteTimeSpan, 0 );
                } );

                // Edge case near -1 ms (a special value), high side.
                Assert2.Throws<ArgumentException>( () =>
                {
                    timer.Change( nearOneMsMinus, Timeout.InfiniteTimeSpan, 0 );
                } );

                // More negative.
                Assert2.Throws<ArgumentException>( () =>
                {
                    timer.Change( MSecs( -2 ), Timeout.InfiniteTimeSpan, 0 );
                } );
            }
        }

        /// <summary>
        /// Verifies that the timer can be configured in a multishot manner for positive values.
        /// </summary>
        [TestMethod]
        public void Change_ViaTimeSpan_MultiShot_Allows_Positive()
        {
            SimpleTimerHarness harness = new SimpleTimerHarness();
            VersionedTimer<int> timer = new VersionedTimer<int>( 123, harness.Callback );

            using( timer )
            {
                timer.Change( MSecs( 100 ), MSecs( 100 ), 1 );
                timer.Change( MSecs( 100 ), MSecs( 200 ), 1 );

                for( int i = 1; i <= 100; i++ )
                {
                    timer.Change( MSecs( 100 ), MSecs( 100 + i ), 1 );
                }

                timer.Change( MSecs( 100 ), MSecs( int.MaxValue ), 1 );
            }
        }

        /// <summary>
        /// Verifies the time can be configured in a single-shot manner for positive values.
        /// </summary>
        [TestMethod]
        public void Change_ViaTimeSpan_SingleShot_Allows_Positive()
        {
            SimpleTimerHarness harness = new SimpleTimerHarness();
            VersionedTimer<int> timer = new VersionedTimer<int>( 123, harness.Callback );

            using( timer )
            {
                timer.Change( MSecs( 100 ), Timeout.InfiniteTimeSpan, 1 );
                timer.Change( MSecs( 200 ), Timeout.InfiniteTimeSpan, 1 );

                for( int i = 1; i <= 100; i++ )
                {
                    timer.Change( MSecs(100 + i), Timeout.InfiniteTimeSpan, 1 );
                }

                timer.Change( MSecs( int.MaxValue ), Timeout.InfiniteTimeSpan, 1 );

                timer.Change( VTimeout.MaxTimeout, Timeout.InfiniteTimeSpan, 1 );
                timer.Change( TimeSpan.Zero, VTimeout.MaxTimeout, 1 );
                timer.Change( VTimeout.MaxTimeout, VTimeout.MaxTimeout, 1 );
            }
        }

        private static TimeSpan MSecs( double value )
        {
            return TimeSpan.FromMilliseconds( value );
        }
    }
}
