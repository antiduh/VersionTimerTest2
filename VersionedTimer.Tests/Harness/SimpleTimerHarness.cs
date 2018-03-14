using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VersionedTimer.Tests.Harness
{
    public class SimpleTimerHarness
    {
        private Stopwatch watch;

        private TimeSpan expectedTimeout;

        private TimeSpan expectedPeriod;

        private TimeSpan nextExpectedPeriod;

        private SemaphoreSlim waitHandle;

        public SimpleTimerHarness()
        {
            watch = new Stopwatch();
            watch.Start();

            this.PeriodErrors = new List<TimeSpan>();

            this.waitHandle = new SemaphoreSlim( 0 );
        }

        public int ObservedState { get; private set; }

        public long ObservedVersion { get; private set; }

        public TimeSpan TimeoutError { get; private set; }

        public List<TimeSpan> PeriodErrors { get; private set; }

        public int Callbacks { get; private set; }

        public void ExpectDelays( int timeoutMs, int periodMs )
        {
            this.expectedTimeout = TimeSpan.FromMilliseconds( timeoutMs );
            this.expectedPeriod = TimeSpan.FromMilliseconds( periodMs );
        }

        public bool Wait( int maxWaitMs = 5 * 1000 )
        {
            return this.waitHandle.Wait( maxWaitMs );
        }

        public void Callback( int state, long version )
        {
            lock( this )
            {
                if( this.Callbacks == 0 )
                {
                    this.TimeoutError = this.watch.Elapsed - this.expectedTimeout;
                    this.nextExpectedPeriod = this.expectedTimeout + this.expectedPeriod;
                }
                else
                {
                    var periodError = this.watch.Elapsed - nextExpectedPeriod;
                    this.PeriodErrors.Add( periodError );

                    this.nextExpectedPeriod += this.expectedPeriod;
                }

                this.ObservedState = state;
                this.ObservedVersion = version;

                this.Callbacks++;

                this.waitHandle.Release();
            }
        }
    }
}
