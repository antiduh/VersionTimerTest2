using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace VersionedTimer
{
    internal class VersionedTimerQueue
    {
        private static Lazy<VersionedTimerQueue> lazyQueue;

        private Timer primeTimer;

        private Stopwatch timeBase;

        private TimeSpan nextPrimeTime;

        private List<IVersionedTimer> timerList;

        static VersionedTimerQueue()
        {
            lazyQueue = new Lazy<VersionedTimerQueue>();
        }

        public VersionedTimerQueue()
        {
            this.primeTimer = new Timer( PrimeTimerCallback );
            this.timerList = new List<IVersionedTimer>();
            this.timeBase = Stopwatch.StartNew();
            this.nextPrimeTime = TimeSpan.MaxValue;
        }

        public static VersionedTimerQueue Instance
        {
            get
            {
                return lazyQueue.Value;
            }
        }

        public void ChangeTimer( IVersionedTimer timer, TimeSpan timeout, TimeSpan period, long version )
        {
            // Save the elapsed as soon as possible, to keep the accounting accurate.
            TimeSpan elapsed = this.timeBase.Elapsed;

            if( timeout < TimeSpan.Zero )
            {
                throw new ArgumentException( "Timeout must be non-negative." );
            }

            // First, update the timer's properties.
            timer.Period = period;
            timer.Version = version;

            // We're going to schedule the timer. Calculate its next timeout, make sure it's
            // in the list, and then make sure our prime timer is scheduled before the new timer.

            timer.NextTimeout = elapsed + timeout;

            if( this.timerList.Contains( timer ) == false )
            {
                this.timerList.Add( timer );
            }

            EnsurePrimerTimerDueBy( timer.NextTimeout );
        }

        public void DeleteTimer( IVersionedTimer timer )
        {
            this.timerList.Remove( timer );
        }

        private void PrimeTimerCallback( object ignored )
        {
            DueTimer? firstElapsed = null;

            lock( this )
            {
                TimeSpan currentTime = this.timeBase.Elapsed;
                TimeSpan lowestDueTime = TimeSpan.MaxValue;

                this.nextPrimeTime = TimeSpan.MaxValue;

                // Game plan:
                // 1) Scan the list for timers that are expired.
                // 2) The first timer that's expired, we'll save and execute on this thread. Saves
                //    threading overhead since it's a very common case.
                // 3) We need to scan over the list to find out what the next prime timeout should be.
                // 4) Any timer we invoke, we need to read its version UNDER THIS LOCK and save it off.
                // 5) Don't execute any timer callbacks under this lock.
                // 6) If we need to fire more than one timer, we'll queue them to the threadpool.
                // 7) If a timer elapsed and is not periodic, then delete it from the queue.

                for( int i = 0; i < this.timerList.Count; /* conditional increment */ )
                {
                    bool delete = false;
                    var timer = this.timerList[i];

                    // Find out if this timer has elapsed.
                    if( timer.NextTimeout <= currentTime )
                    {
                        // Fire the timer
                        if( firstElapsed == null )
                        {
                            firstElapsed = new DueTimer( timer );
                        }
                        else
                        {
                            EnqueueTimerCallback( new DueTimer( timer ) );
                        }

                        // Reset or delete the timer.
                        if( timer.Period == Timeout.InfiniteTimeSpan )
                        {
                            delete = true;
                            timer.NextTimeout = Timeout.InfiniteTimeSpan;
                        }
                        else
                        {
                            timer.NextTimeout += timer.Period;
                        }
                    }

                    // Track the next timestamp for the prime timer.
                    if( timer.NextTimeout < lowestDueTime )
                    {
                        lowestDueTime = timer.NextTimeout;
                    }

                    // List-meta: We're either going to delete the item we're on (thus advancing the
                    // for loop), or we're going to leave the current item and simply go to the next index.
                    if( delete )
                    {
                        this.timerList.RemoveAt( i );
                    }
                    else
                    {
                        i++;
                    }
                }

                if( lowestDueTime != TimeSpan.MaxValue )
                {
                    EnsurePrimerTimerDueBy( lowestDueTime );
                }
            }

            // Fire off the first elapsed timer, if one exists, but make sure to do so outside the lock.
            if( firstElapsed != null )
            {
                firstElapsed.Value.Run();
            }
        }

        /// <summary>
        /// Ensures that the prime timer will fire by the requested timestamp.
        /// </summary>
        /// <param name="requestedTimeStamp">The next timestamp when the prime timer should fire.</param>
        private void EnsurePrimerTimerDueBy( TimeSpan requestedTimeStamp )
        {
            if( this.nextPrimeTime > requestedTimeStamp )
            {
                this.nextPrimeTime = requestedTimeStamp;

                TimeSpan sleepTime = this.nextPrimeTime - this.timeBase.Elapsed;

                if( sleepTime < TimeSpan.Zero )
                {
                    sleepTime = TimeSpan.Zero;
                }

                this.primeTimer.Change(
                    sleepTime,
                    Timeout.InfiniteTimeSpan
                );
            }
        }

        private void EnqueueTimerCallback( DueTimer due )
        {
            ThreadPool.QueueUserWorkItem( ThreadPoolCallback, due );
        }

        private void ThreadPoolCallback( object state )
        {
            DueTimer due = (DueTimer)state;
            due.Run();
        }

        private struct DueTimer
        {
            public DueTimer( IVersionedTimer timer )
            {
                this.Timer = timer;
                this.Version = timer.Version;
            }

            public IVersionedTimer Timer { get; private set; }

            public long Version { get; private set; }

            public void Run()
            {
                this.Timer.FireTimerCallback( this.Version );
            }
        }
    }
}