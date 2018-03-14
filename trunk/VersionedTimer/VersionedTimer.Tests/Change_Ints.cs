using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VersionedTimer.Tests.Harness;

namespace VersionedTimer.Tests
{
    /// <summary>
    /// Verifies the timer supports configuring the timeout.
    /// </summary>
    [TestClass]
    public class Change_Ints
    {
        [TestMethod]
        public void Change_ViaInts_AllowsZero()
        {
            SimpleTimerHarness harness = new SimpleTimerHarness();
            VersionedTimer<int> timer = new VersionedTimer<int>( 123, harness.Callback );

            using( timer )
            {
                timer.Change( 0, Timeout.Infinite, 1 );
                timer.Change( 0, 0, 2 );
            }
        }

        [TestMethod]
        public void Change_ViaInts_AllowsInfinite()
        {
            SimpleTimerHarness harness = new SimpleTimerHarness();
            VersionedTimer<int> timer = new VersionedTimer<int>( 123, harness.Callback );

            using( timer )
            {
                timer.Change( 10, Timeout.Infinite, 0 );
                timer.Change( Timeout.Infinite, Timeout.Infinite, 1 );
            }
        }

        [TestMethod]
        public void Change_ViaInts_DoesNotAllowNegative_Timeout()
        {
            SimpleTimerHarness harness = new SimpleTimerHarness();
            VersionedTimer<int> timer = new VersionedTimer<int>( 123, harness.Callback );

            using( timer )
            {
                Assert2.Throws<ArgumentException>( () =>
                {
                    timer.Change( -2, Timeout.Infinite, 0 );
                } );

                Assert2.Throws<ArgumentException>( () =>
                {
                    timer.Change( -3, Timeout.Infinite, 0 );
                } );

                Assert2.Throws<ArgumentException>( () =>
                {
                    timer.Change( int.MinValue, Timeout.Infinite, 0 );
                } );
            }
        }

        [TestMethod]
        public void Change_ViaInts_DoesNotAllowNegative_Period()
        {
            SimpleTimerHarness harness = new SimpleTimerHarness();
            VersionedTimer<int> timer = new VersionedTimer<int>( 123, harness.Callback );

            using( timer )
            {
                Assert2.Throws<ArgumentException>( () =>
                {
                    timer.Change( 0, -2, 0 );
                } );

                Assert2.Throws<ArgumentException>( () =>
                {
                    timer.Change( 0, -3, 0 );
                } );

                Assert2.Throws<ArgumentException>( () =>
                {
                    timer.Change( 0, int.MinValue, 0 );
                } );
            }
        }

        [TestMethod]
        public void Change_ViaInts_SingleShot_AllowsPositive()
        {
            SimpleTimerHarness harness = new SimpleTimerHarness();
            VersionedTimer<int> timer = new VersionedTimer<int>( 123, harness.Callback );

            using( timer )
            {
                timer.Change( 100, Timeout.Infinite, 1 );
                timer.Change( 200, Timeout.Infinite, 2 );

                for( int i = 1; i <= 100; i++ )
                {
                    timer.Change( 100 + i, Timeout.Infinite, 1 );
                }

                timer.Change( int.MaxValue, Timeout.Infinite, 1 );
            }
        }

        [TestMethod]
        public void Change_ViaInts_MultiShot_AllowsPositive()
        {
            SimpleTimerHarness harness = new SimpleTimerHarness();
            VersionedTimer<int> timer = new VersionedTimer<int>( 123, harness.Callback );

            using( timer )
            {
                timer.Change( 100, 100, 1 );
                timer.Change( 100, 200, 2 );

                for( int i = 1; i <= 100; i++ )
                {
                    timer.Change( 100, 100 + i, 1 );
                }

                timer.Change( 100, int.MaxValue, 1 );
            }
        }
    }
}