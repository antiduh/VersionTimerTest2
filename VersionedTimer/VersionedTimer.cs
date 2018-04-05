using System;
using System.Threading;

namespace VersionedTimer
{
    /// <summary>
    /// A timer that allows individual invocations to be identified and correlated to the most recent
    /// change to the timer.
    /// </summary>
    /// <remarks>
    /// The design of many timers includes a complication such that there is a small window of time
    /// between when a timer is being changed and when the timer decides to execute the timer's
    /// callback such that the timer can fire immediately after it has been postponed or disabled by
    /// a call its Change() method.
    ///
    /// For instance, consider the following sequence of events:
    /// - A user schedules the timer to fire in 1000 ms.
    /// - At millisecond 1000, two things happen simultaneously by chance: one, the timer's
    ///   implementation makes the decision to execute the callback; two, the user attempts to
    ///   postpone the timer by calling its Change() method.
    /// - The Change() code and the scheduling code fight over the timer's internal lock.
    /// - The scheduling code wins.
    /// - The scheduling code decides to schedule the timer's callback and releases the lock.
    /// - The Change() code acquires the lock and postpones the (already running) timer.
    /// - The timer's callback executes immediately, even though the user just tried to postpone it.
    ///
    /// The reason this is possible is that there is a disconnect between the decision logic in the
    /// timer and the execution logic in the timer. The timer uses a lock to protect its state when
    /// it decides to schedule callbacks, and to protect its state when user calls are made to change
    /// the timer. Since the two operations are separate, it's possible for a timer to be modified
    /// after its callback has been scheduled, but before it has begun to execute. I deem this the
    /// 'Unreliable Recall' property of timers.
    ///
    /// Fixing the Unreliable Recall property is difficult. Instead, the VersionedTimer provides the
    /// timer user with just enough information to be able to detect unreliable recalls, thus
    /// allowing the user to filter out unintended callbacks.
    ///
    /// The timer does this by allowing the user to provide a version number when changing the timer,
    /// that is in turn provided to the timer's callback. The timer registers the version number
    /// under the same lock it uses to schedule callbacks. As a result of this design, one of two
    /// outcomes can happen when user and timer code races:
    /// - The user's Change() call wins the race to the timer's lock, and the timer is successfully
    ///   recalled, or:
    /// - The user's Change() call loses the race to the timer's lock, and so the callback is still
    ///   scheduled, but the version number won't be changed befor the callback is scheduled, and
    ///   thus the callback will execute with the previous version number that can then be identified
    ///   by the user's callback code.
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    public sealed class VersionedTimer<T> : IDisposable, IVersionedTimer
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

        /// <summary>
        /// Changes the start time and period of the timer using 32-bit integers to specify the
        /// timeout periods.
        /// </summary>
        /// <param name="timeoutMs">
        /// The amount of time to delay before the first occurance of the timer callback, in
        /// milliseconds. Specify <see cref="Timeout.Infinite"/> to disable the timer.
        /// </param>
        /// <param name="periodMs">
        /// The amount of time to delay between periodic invocation of the timer, in milliseconds.
        /// Specify <see cref="Timeout.Infinite"/> to disable periodic signalling.
        /// </param>
        /// <param name="version">
        /// The version number to store with the timer and to invoke callbacks with. Every callback
        /// that occurs after the change, and only such callbacks, will be invoked with the new
        /// version value until the next change occurs.
        /// </param>
        public void Change( int timeoutMs, int periodMs, long version )
        {
            TimeSpan timeout;
            TimeSpan period;

            ValidateParams( timeoutMs, out timeout );
            ValidateParams( periodMs, out period );

            ChangeInternal( timeout, period, version );
        }

        /// <summary>
        /// Changes the start time and period of the timer using TimeSpan values to specify the
        /// timeout periods.
        /// </summary>
        /// <param name="timeout">
        /// The amount of time to delay before the first occurance of the timer callback. Specify
        /// <see cref="Timeout.InfiniteTimeSpan"/> to disable the timer.
        /// </param>
        /// <param name="periodMs">
        /// The amount of time to delay between periodic invocation of the timer. Specify <see
        /// cref="Timeout.InfiniteTimeSpan"/> to disable periodic signalling.
        /// </param>
        /// <param name="version">
        /// The version number to store with the timer and to invoke callbacks with. Every callback
        /// that occurs after the change, and only such callbacks, will be invoked with the new
        /// version value until the next change occurs.
        /// </param>
        public void Change( TimeSpan timeout, TimeSpan period, long version )
        {
            ValidateParams( timeout );
            ValidateParams( period );

            ChangeInternal( timeout, period, version );
        }

        /// <summary>
        /// Disables the timer and releases all resources associated with it. Timer callbacks that
        /// have already been scheduled but not yet executed may execute after the timer has been disposed.
        /// </summary>
        public void Dispose()
        {
            DisposeInternal( null );
        }

        /// <summary>
        /// Disables the timer and releases all resources associated with it, and notifies when all
        /// pending callbacks have been completed. Timer callbacks that have already been scheduled
        /// but not yet executed may execute after the timer has been disposed, however, all
        /// callbacks will have completed before the notification is signalled.
        /// </summary>
        /// <param name="notifyObject">An event to signal when any and all callbacks have completed.</param>
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

        private void DisposeInternal( EventWaitHandle notifyParameter )
        {
            var queue = VersionedTimerQueue.Instance;

            lock( queue )
            {
                if( this.disposed )
                {
                    return;
                }

                // If the timer is already idle, then just signal their event now and don't bother
                // remembering it. Else, we'll signal it when we finally do go idle.
                if( notifyParameter != null )
                {
                    if( this.runningRefCount == 0 )
                    {
                        notifyParameter.Set();
                    }
                    else
                    {
                        this.notifyWaitHandle = notifyParameter;
                    }
                }
               
                queue.DeleteTimer( this );
                
                this.disposed = true;
            }

            // Guaranteed to call this only once; we check `this.disposed` under the lock so we'll
            // know we have a consistent value, and we return immediately if we're already disposed.
            GC.SuppressFinalize( this );
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
                throw new ArgumentException( "TimeSpan values must be either infinite, zero, or greater than zero." );
            }

            if( timeoutSpan > VTimeout.MaxTimeout )
            {
                throw new ArgumentException( "TimeSpan value is too large. Limit the value to `VTimeout.MaxTimeout`." );
            }
        }

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