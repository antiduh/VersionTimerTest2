using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VersionedTimer.Tests.Harness;

namespace VersionedTimer.Tests
{
    /// <summary>
    /// Verifies the timer operates correctly during various disposal scenarios.
    /// </summary>
    [TestClass]
    public class Disposal
    {
        /// <summary>
        /// Tests disposing the timer after it has been used, disabled, and all callbacks have finished.
        /// </summary>
        [TestMethod]
        public void DisposeAfterSingleShot()
        {
            for( int i = 0; i < 100; i++ )
            {
                VersionedTimer<int> timer;
                SimpleTimerHarness harness = new SimpleTimerHarness();

                timer = new VersionedTimer<int>( 123, harness.Callback );

                using( timer )
                {
                    timer.Change( 10, Timeout.Infinite, 0 );

                    Assert.IsTrue( harness.Wait(), "Timer did not fire." );
                }
            }
        }

        /// <summary>
        /// Tests disposing the timer after it has been used, disabled, and all callbacks have finished.
        /// </summary>
        [TestMethod]
        public void DisposeAfterDisableMultiShot()
        {
            for( int i = 0; i < 100; i++ )
            {
                VersionedTimer<int> timer;
                SimpleTimerHarness harness = new SimpleTimerHarness();

                timer = new VersionedTimer<int>( 123, harness.Callback );

                using( timer )
                {
                    timer.Change( 10, 10, 0 );

                    Assert.IsTrue( harness.Wait(), "Timer did not fire." );
                    Assert.IsTrue( harness.Wait(), "Timer did not fire." );
                    Assert.IsTrue( harness.Wait(), "Timer did not fire." );

                    timer.Change( Timeout.Infinite, Timeout.Infinite, 0 );
                }
            }
        }

        /// <summary>
        /// Tests that the timer throws an ObjectDisposedException when attempting to use it after
        /// disposing it.
        /// </summary>
        [TestMethod]
        public void VerifyObjectDisposedException()
        {
            VersionedTimer<int> timer;
            SimpleTimerHarness harness = new SimpleTimerHarness();

            timer = new VersionedTimer<int>( 123, harness.Callback );

            try
            {
                timer.Change( 10, Timeout.Infinite, 0 );
                Assert.IsTrue( harness.Wait(), "Timer did not fire." );
            }
            finally
            {
                timer.Dispose();
            }

            Assert2.Throws<ObjectDisposedException>( () =>
            {
                timer.Change( 100, 100, 1 );
            } );
        }

        /// <summary>
        /// Tests disposing the timer while it is scheduled but idle.
        /// </summary>
        [TestMethod]
        public void DisposeWhilePending()
        {
            SimpleTimerHarness harness = new SimpleTimerHarness();

            for( int i = 0; i < 1000; i++ )
            {
                VersionedTimer<int> timer;

                timer = new VersionedTimer<int>( 123, harness.Callback );

                timer.Change( 1000, Timeout.Infinite, 1 );

                timer.Dispose();
            }

            Thread.Sleep( 1500 );

            Assert.AreEqual( harness.Callbacks, 0 );
        }

        /// <summary>
        /// Tests disposing the timer, from user code, while the callback is executing.
        /// </summary>
        [TestMethod]
        public void DisposeDuringCallbackFromUser()
        {
            for( int i = 0; i < 100; i++ )
            {
                VersionedTimer<int> timer;

                using( var callbackCanContinue = new AutoResetEvent( false ) )
                using( var callbackStarted = new AutoResetEvent( false ) )
                {
                    VersionedTimerCallback<int> callback = ( int state, long version ) =>
                    {
                        callbackStarted.Set();
                        callbackCanContinue.WaitOne( 10 * 1000 );
                    };

                    timer = new VersionedTimer<int>( 0, callback );
                    timer.Change( 10, Timeout.Infinite, 1 );

                    Assert.IsTrue( callbackStarted.WaitOne( 5 * 1000 ), "Timer never fired." );

                    timer.Dispose();

                    callbackCanContinue.Set();
                }
            }
        }

        /// <summary>
        /// Tests disposing the timer from within the callback.
        /// </summary>
        [TestMethod]
        public void DisposeFromCallback()
        {
            for( int i = 0; i < 100; i++ )
            {
                AutoResetEvent timerDone = new AutoResetEvent( false );
                VersionedTimer<int> timer = null;
                bool failed = false;

                VersionedTimerCallback<int> callback = ( int state, long version ) =>
                {
                    try
                    {
                        timer.Dispose();
                        timerDone.Set();
                    }
                    catch
                    {
                        failed = true;
                        throw;
                    }
                };

                timer = new VersionedTimer<int>( 123, callback );

                timer.Change( 10, Timeout.Infinite, 1 );

                Assert.IsTrue( timerDone.WaitOne( 5 * 1000 ), "Timer did not fire" );
                Assert.IsFalse( failed, "Timer crashed during callback." );
            }
        }

        /// <summary>
        /// Tests the disposal notify feature.
        /// </summary>
        [TestMethod]
        public void VerifyDisposalNotify()
        {
            for( int i = 0; i < 100; i++ )
            {
                VersionedTimer<int> timer;

                using( var disposeNotify = new AutoResetEvent( false ) )
                using( var callbackCanContinue = new AutoResetEvent( false ) )
                using( var callbackStarted = new AutoResetEvent( false ) )
                {
                    VersionedTimerCallback<int> callback = ( int state, long version ) =>
                    {
                        callbackStarted.Set();
                        callbackCanContinue.WaitOne( 10 * 1000 );
                    };

                    timer = new VersionedTimer<int>( 123, callback );

                    timer.Change( 10, Timeout.Infinite, 0 );

                    Assert.IsTrue( callbackStarted.WaitOne( 5 * 1000 ), "Timer did not fire." );

                    timer.Dispose( disposeNotify );

                    callbackCanContinue.Set();

                    Assert.IsTrue( disposeNotify.WaitOne( 5 * 1000 ), "Timer disposal notification did not fire." );
                }
            }
        }

        /// <summary>
        /// Verifies that disposing the timer multiple times does nothing (disposal is idempotent).
        /// </summary>
        [TestMethod]
        public void Dispose_AllowsMultipleDisposes()
        {
            SimpleTimerHarness harness = new SimpleTimerHarness();
            VersionedTimer<int> timer = new VersionedTimer<int>( 123, harness.Callback );

            timer.Change( 10, Timeout.Infinite, 0 );

            Assert.IsTrue( harness.Wait(), "Timer did not fire." );

            timer.Dispose();
            timer.Dispose();
        }

        /// <summary>
        /// Verifies that the Dispose overload that takes a wait handle fails when given a null wait handle.
        /// </summary>
        [TestMethod]
        public void DisposeNotify_Requires_WaitHandle()
        {
            SimpleTimerHarness harness = new SimpleTimerHarness();
            VersionedTimer<int> timer = new VersionedTimer<int>( 123, harness.Callback );

            Assert2.Throws<ArgumentNullException>( () => timer.Dispose( null ) );
        }
    }
}