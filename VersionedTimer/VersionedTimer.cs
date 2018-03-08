using System;
using System.Threading;

namespace VersionedTimer
{
    /// <summary>
    /// A timer that allows individual invocations to be identified and correlated to the most recent
    /// change to the timer.
    /// </summary>
    /// <remarks>
    /// The design of all timers includes a complication such that timer callbacks may occur while
    /// the timer user is attempting to change the timer. This can occur because there is a small
    /// window where the timer may already be running, has not yet begun executing the callback
    /// function, and the code using the timer calls `Change()` on the timer to postpone or disable it.
    ///
    /// In this situation, the timer callback might run immediately after it has been postponed or
    /// recalled entirely. I call this the Unreliable Recall property of timers.
    ///
    /// The implementation of this timer is such that the callback method can determine if the timer
    /// callback currently running represents the latest change to the timer. This allows a user to
    /// unambiguously determine whether a timer invocation is stale or not. This implementation
    /// guarentees that by ensuring that all callbacks are scheduled under the same lock that is used
    /// to process Change() requests. If the user of the timer only processes callbacks and calls
    /// Change under the same lock, then the user of the lock can unambiguously recall timers.
    ///
    /// For instance, consider some data structure that is modified under a lock, but is shared
    /// between the timer and another thread. The invariant is that the timer should fire some amount
    /// of time after the last modification to the structure, and no sooner; if the data structure is
    /// modified, the timer must be postponed and any scheduled callbacks must be recalled or
    /// otherwise aborted. If the timer is changed under the lock by the other thread, its possible
    /// that the timer callback will still execute - because it was already executing and lost the
    /// race to the lock, due to the Unreliable Recall property. By maintaining a version number in
    /// the data structure, and a providing that number to the timer's `Change()` method, it's
    /// possible to determine if the current callback invocation is the latest - when the timer runs
    /// it'll provide the version it was last configured with (via `Change()`) when the callback
    /// method was scheduled for execution. Since this implementation schedules timer callbacks and
    /// processes `Change()` requests both under the same lock, it's possible to use this timer to
    /// track such callbacks occuring.
    ///
    /// Normal timer progression with a simple callback:
    ///
    /// - Prime/native timer fires.
    /// - Timer queue grabs lock X, the lock that protects all timer accounting.
    /// - Timer queue finds out which timer elapsed.
    /// - Timer queue drops lock X.
    /// - Timer queue enqueues threadpool callback.
    /// - ..time passes
    /// - Threadpool begins executing timer.
    /// - Timer user callback grabs lock Y, its own private lock.
    /// - Timer user callback processes.
    /// - Timer user callback releases lock Y
    ///
    /// A timer can still run after it has been recalled, and you can't keep track of that:
    ///
    /// - Prime timer fires, queue enters timer processing.
    /// - Timer queue grabs lock X.
    /// - Timer queue finds out which timer elapsed.
    /// - Timer queue releases lock X.
    /// - Timer queue enqueues threadpool callback.
    /// - Timer has not yet reached the part where the timer callback begins running.
    /// - ....
    /// - User code grabs lock Y.
    /// - User code calls change on timer, attempting to recall it.
    /// - Timer queue grabs lock X
    /// - Timer queue updates timer.
    /// - Timer queue releases lock X.
    /// - User code updates currentTimerVersion += 1
    /// - User code releases lock Y.
    /// - ....
    /// - Timer reaches the part where the timer callback begins running.
    /// - Timer user callback grabs lock Y.
    /// - Failure.
    ///
    /// Failure occurs because the timer ran after it was recalled, and the user code had everything
    /// (change and callback) covered by a lock, and yet the user code could not tell that the timer
    /// that was firing was one that had been recalled.
    ///
    /// Fix:
    /// - Prime/native timer fires.
    /// - Timer queue grabs lock X, the lock that protects all timer accounting.
    /// - Timer queue finds out which timer elapsed.
    /// - Timer queue saves the version number of the current timer while holding the lock.
    /// - Timer queue drops lock X.
    /// - Timer queue enqueues threadpool callback, passing the version number it got under the lock.
    /// - If any user change occurs, it can increment its version under its own lock.
    /// - When the user callback runs, it can compare its version number with the timer queue's
    ///   version number and find out if unreliable recall has occurred.
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    public class VersionedTimer<T> : IDisposable, IVersionedTimer
    {
        private T state;

        private VersionedTimerCallback<T> callback;

        private bool disposed;

        private int runningRefCount;

        private EventWaitHandle notifyWaitHandle;

        private ExecutionContext userExecContext;

        /// <summary>
        /// Initializes a new instance of the VersionedTimer class.
        /// </summary>
        /// <param name="state">A value to be provided to the callback method.</param>
        /// <param name="callback">A method to invoke when the timer elapses.</param>
        public VersionedTimer( T state, VersionedTimerCallback<T> callback )
        {
            if( callback == null )
            {
                throw new ArgumentNullException( nameof( callback ), "Must provide a callback method." );
            }

            if( ExecutionContext.IsFlowSuppressed() == false )
            {
                this.userExecContext = ExecutionContext.Capture();
            }

            this.state = state;
            this.callback = callback;
        }

        ~VersionedTimer()
        {
            // Taking a tip from .Net's timer implementation, don't try to grab locks while we're in
            // certian shutdown scenarios. Grabbing locks in finalizers is dangereous due to the opportunity
            // to deadlock the finalizer thread, but we need to in order to clean up this lock properly.

            if( Environment.HasShutdownStarted || AppDomain.CurrentDomain.IsFinalizingForUnload() )
            {
                return;
            }

            Dispose( null );
        }

        public void Change( int timeoutMs, int periodMs, long version )
        {
            TimeSpan timeout;
            TimeSpan period;

            ValidateParams( timeoutMs, out timeout );
            ValidateParams( periodMs, out period );

            ChangeInternal( timeout, period, version );
        }

        public void Change( TimeSpan timeout, TimeSpan period, long version )
        {
            ValidateParams( timeout );
            ValidateParams( period );

            ChangeInternal( timeout, period, version );
        }

        public void Dispose()
        {
            DisposeInternal( null );
        }

        // Blocks disposal until all pending callbacks have completed.
        public void Dispose( EventWaitHandle notifyObject )
        {
            if( notifyObject == null )
            {
                throw new ArgumentNullException( nameof( notifyObject ) );
            }

            DisposeInternal( notifyObject );
        }

        private void ChangeInternal( TimeSpan timeout, TimeSpan period, long version )
        {
            var queue = VersionedTimerQueue.Instance;

            lock( queue )
            {
                if( this.disposed )
                {
                    throw new ObjectDisposedException( null, "Cannot access a disposed timer." );
                }

                if( timeout >= TimeSpan.Zero )
                {
                    queue.ChangeTimer( this, timeout, period, version );
                }
                else
                {
                    queue.DeleteTimer( this );
                }
            }
        }

        private void DisposeInternal( EventWaitHandle notifyObject )
        {
            var queue = VersionedTimerQueue.Instance;

            lock( queue )
            {
                queue.DeleteTimer( this );

                this.notifyWaitHandle = notifyObject;
            }
        }

        private void ValidateParams( int spec, out TimeSpan span )
        {
            if( spec == Timeout.Infinite )
            {
                span = Timeout.InfiniteTimeSpan;
            }
            else if( spec < 0 )
            {
                throw new ArgumentException( "Timeout values must be either infinite, zero, or greater than zero." );
            }
            else
            {
                span = TimeSpan.FromMilliseconds( spec );
            }
        }

        private void ValidateParams( TimeSpan timeoutSpan )
        {
            if( timeoutSpan != Timeout.InfiniteTimeSpan && timeoutSpan < TimeSpan.Zero )
            {
                throw new ArgumentException( "Timeout values must be either infinite, zero, or greater than zero." );
            }
        }

        /// <summary>
        /// Gets or sets the amount of time to delay before the first invocation of the timer's
        /// callback. A value of TimeSpan.MaxValue indicates the timer is disabled.
        /// </summary>
        TimeSpan IVersionedTimer.Timeout { get; set; }

        /// <summary>
        /// Gets or sets the amount of time to delay between timer invocations. A value of
        /// TimeSpan.MaxValue disables periodic reoccurance.
        /// </summary>
        TimeSpan IVersionedTimer.Period { get; set; }

        /// <summary>
        /// Gets or sets the current version of the timer.
        /// </summary>
        long IVersionedTimer.Version { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the next timeout of the timer.
        /// </summary>
        TimeSpan IVersionedTimer.NextTimeout { get; set; }

        void IVersionedTimer.FireTimerCallback( long version )
        {
            // Copies of protected state we'll grab from under locks.
            bool localDisposed;
            EventWaitHandle localNotifyObject = null;

            lock( VersionedTimerQueue.Instance )
            {
                localDisposed = this.disposed;

                if( localDisposed == false )
                {
                    runningRefCount++;
                }
            }

            if( localDisposed == false )
            {
                CallCallback( version );
            }

            lock( VersionedTimerQueue.Instance )
            {
                if( localDisposed == false )
                {
                    runningRefCount--;
                    if( runningRefCount == 0 )
                    {
                        localNotifyObject = this.notifyWaitHandle;
                    }
                }
            }

            if( localNotifyObject != null )
            {
                localNotifyObject.Set();
            }
        }

        private void CallCallback( long version )
        {
            if( this.userExecContext != null )
            {
                using( var context = this.userExecContext.CreateCopy() )
                {
                    ExecutionContext.Run( context, ExecContextCallback, version );
                }
            }
            else
            {
               this.callback( this.state, version );
            }
        }

        private void ExecContextCallback( object contextParam )
        {
            long version = (long)contextParam;

            this.callback( this.state, version );
        }
    }
}