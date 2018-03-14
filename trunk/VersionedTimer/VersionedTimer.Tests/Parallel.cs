using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VersionedTimer.Tests
{
    /// <summary>
    /// Verifies that timers are executed in parallel up to the parallelism offered by the thread pool.
    /// </summary>
    [TestClass]
    public class Parallel
    {
        [TestMethod]
        public void SimpleParallel()
        {
            var harness = new ParallelTimerObserver();
            var timers = new List<VersionedTimer<int>>();
            int expectedParallelism;
            int dummy;

            ThreadPool.GetMinThreads( out expectedParallelism, out dummy );
            harness.SetExpectedParallelism( expectedParallelism );

            for( int i = 0; i < expectedParallelism; i++ )
            {
                var timer = new VersionedTimer<int>( 0, harness.Callback );
                timers.Add( timer );
            }

            try
            {
                foreach( var timer in timers )
                {
                    timer.Change( 50, Timeout.Infinite, 0 );
                }

                Assert.IsTrue( harness.Wait( 5000 ), "Timers failed to fire." );
            }
            finally
            {
                harness.EnsureReleased();

                foreach( var timer in timers )
                {
                    timer.Dispose();
                }
            }
        }

        private class ParallelTimerObserver
        {
            private CountdownEvent waitHandle;

            public void SetExpectedParallelism( int count )
            {
                waitHandle = new CountdownEvent( count );
            }

            public bool Wait( int maxWaitMs )
            {
                return waitHandle.Wait( maxWaitMs );
            }

            public void EnsureReleased()
            {
                if( this.waitHandle.IsSet == false )
                {
                    this.waitHandle.Signal( this.waitHandle.InitialCount - this.waitHandle.CurrentCount );
                }
            }

            public void Callback( int state, long version )
            {
                this.waitHandle.Signal();
                this.waitHandle.Wait( 5 * 1000 );
            }
        }
    }
}