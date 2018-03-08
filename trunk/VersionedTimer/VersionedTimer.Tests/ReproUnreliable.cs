using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VersionedTimer.Tests
{
    /// <summary>
    /// Verifies that the versioned timer can demonstrate reproduction of unreliable recall - old
    /// callbacks occurring right after a timer change. This proves the timer solves the problem it 
    /// set out to detect.
    /// </summary>
    [TestClass]
    public class ReproUnreliable
    {
        private long currentVer;

        private bool reproduced;

        /// <summary>
        /// Tests unreliable recall by continuously rescheduling a timer, but sleeping between the
        /// reschedule calls by just the right amount to reschedule just as the timer elapses. To
        /// provide a high-degree of certainty of success, the test sweeps across a variety of sleep
        /// times to try to adjust the phase of the Change call to occur just as the timer elapses
        /// but before the callback begins executing.
        /// </summary>
        [TestMethod]
        public void DemonstrateUnreliableRecall()
        {
            int sleepTime = 15;
            var timer = new VersionedTimer<int>( 0, Callback );
            var runtime = Stopwatch.StartNew();
            var sleepPhaser = new SleepPhaser( sleepTime, -5.0, 5.0, 0.1 );

            for( long ver = 0; ; ver++ )
            {
                sleepPhaser.Sleep();

                lock( this )
                {
                    timer.Change( sleepTime, Timeout.Infinite, ver );
                    this.currentVer = ver;
                }

                if( reproduced )
                {
                    break;
                }
                else if( runtime.ElapsedMilliseconds > 2 * 60 * 1000 )
                {
                    break;
                }
            }

            Assert.IsTrue( reproduced, "Failed to reproduce the unreliable recall." );

            Trace.WriteLine( string.Format( 
                    "Reproduced after {0} versions, using sleep phase of {1:0.0} ms vs expected timeout of {2} ms",
                    this.currentVer,
                    sleepPhaser.Current,
                    sleepTime 
            ) );
        }

        private void Callback(int state, long version )
        {
            lock( this )
            {
                if( version < this.currentVer )
                {
                    reproduced = true;
                }
            }
        }

        /// <summary>
        /// Sweeps across a range of sleep times according to provided parameters.
        /// </summary>
        private class SleepPhaser
        {
            private double baseSleep;

            private double min;

            private double max;

            private double incr;

            private int pass;

            public SleepPhaser( double baseSleep, double min, double max, double incr )
            {
                if( min > max )
                {
                    throw new ArgumentException();
                }

                this.baseSleep = baseSleep;
                this.min = min;
                this.max = max;
                this.incr = incr;

                this.Current = this.baseSleep + this.min;
            }

            public double Current { get; private set; }

            public void Sleep()
            {
                AccurateSleep( this.Current );

                this.Current += this.incr;

                if( this.Current > this.baseSleep + this.max )
                {
                    // Try to make a number of passes at the initial resolution. If we're
                    // unsuccessful, cut the phase step granularity in half and widen the window so
                    // that we'll make finer grained passes with more range, giving us a better
                    // chance of finding the sweet spot.
                    if( this.pass > 3 )
                    {
                        this.pass = 0;
                        this.incr = this.incr / 2.0;
                        this.min *= 0.80;
                        this.max *= 1.20;

                        Trace.WriteLine( string.Format(
                            "Increased sleep phase window: Min {0:0.000}, Max {1:0.000} Incr {2:0.000}.", 
                            min, max, incr
                        ) );
                    }
                    else
                    {
                        this.pass++;
                    }

                    this.Current = this.baseSleep + this.min;
                }
            }


            private void AccurateSleep( double ms )
            {
                Stopwatch watch = Stopwatch.StartNew();

                if( ms > 20 )
                {
                    Thread.Sleep( (int)( ms - 20 ) );
                }

                while( watch.Elapsed.TotalMilliseconds < ms )
                {
                    Thread.SpinWait( 100 );
                }
            }
        }
    }
}
