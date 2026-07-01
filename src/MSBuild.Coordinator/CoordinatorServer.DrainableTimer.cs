// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Coordinator;

internal sealed partial class CoordinatorServer
{
    /// <summary>
    ///  Wraps a <see cref="Timer"/> and waits for in-flight callbacks during disposal.
    /// </summary>
    private sealed class DrainableTimer : IDisposable
    {
        private readonly Timer _timer;
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private int _disposeState;

        private const int NotDisposed = 0;
        private const int Disposing = 1;
        private const int Disposed = 2;

        /// <summary>
        ///  Indicates whether disposal is in progress or has completed.
        /// </summary>
        private bool IsDisposingOrDisposed => Volatile.Read(ref _disposeState) != NotDisposed;

        public DrainableTimer(TimerCallback callback, object? state, int dueTime, int period)
        {
            _callback = callback;
            _state = state;

            _timer = new Timer(
                static timerState => ((DrainableTimer)timerState!).InvokeCallback(),
                state: this,
                dueTime,
                period);
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposeState, Disposing, NotDisposed) != NotDisposed)
            {
                return;
            }

            try
            {
                // Timer.Dispose(WaitHandle) stops future callbacks and signals only
                // after callbacks already in progress have completed. Coordinator
                // server cleanup relies on this before disposing shared state.
                using var disposeEvent = new ManualResetEvent(false);
                if (_timer.Dispose(disposeEvent))
                {
                    disposeEvent.WaitOne();
                }
            }
            finally
            {
                Volatile.Write(ref _disposeState, Disposed);
            }
        }

        private void InvokeCallback()
        {
            if (IsDisposingOrDisposed)
            {
                return;
            }

            _callback(_state);
        }
    }
}
