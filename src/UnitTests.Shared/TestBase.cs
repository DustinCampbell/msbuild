// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Microsoft.Build.UnitTests;

/// <summary>
///  Provides asynchronous test setup and cleanup with a lazily created <see cref="TestEnvironment"/>.
/// </summary>
/// <param name="output">The <see cref="ITestOutputHelper"/> for writing output throughout test setup, execution, and cleanup.</param>
/// <remarks>
///  Registered resources and cleanup callbacks are processed in reverse registration order after
///  <see cref="DisposeAsync"/> completes or if <see cref="InitializeAsync"/> fails, and before
///  <see cref="Environment"/> is disposed.
/// </remarks>
public abstract class TestBase(ITestOutputHelper output) : IAsyncLifetime
{
    // Guards _disposed, _environment, and _disposables.
    private readonly LockType _gate = new();

    private bool _disposed;
    private TestEnvironment? _environment;
    private DisposalTracker? _disposables;

    /// <summary>
    ///  Gets the test environment, creating it on first access.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The base class has begun disposing the test environment.</exception>
    protected TestEnvironment Environment
    {
        get
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                return _environment ??= TestEnvironment.Create(output);
            }
        }
    }

    /// <summary>
    ///  Gets the output helper for writing output throughout test setup, execution, and cleanup.
    /// </summary>
    protected ITestOutputHelper Output => output;

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        ExceptionAggregator exceptions = new();
        await exceptions.RunAsync(InitializeAsync);

        if (exceptions.HasExceptions)
        {
            await DisposeResourcesAsync(exceptions);
            exceptions.ThrowIfFaulted();
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        ExceptionAggregator exceptions = new();
        await exceptions.RunAsync(DisposeAsync);

        await DisposeResourcesAsync(exceptions);
        exceptions.ThrowIfFaulted();
    }

    private async ValueTask DisposeResourcesAsync(ExceptionAggregator exceptions)
    {
        TestEnvironment? environment;
        DisposalTracker? disposables;

        lock (_gate)
        {
            _disposed = true;

            environment = _environment;
            _environment = null;

            disposables = _disposables;
            _disposables = null;
        }

        if (disposables is not null)
        {
            await exceptions.RunAsync(disposables.DisposeAsync);
        }

        if (environment is not null)
        {
            exceptions.Run(environment.Dispose);
        }
    }

    /// <summary>
    ///  Performs asynchronous setup before the test.
    /// </summary>
    /// <returns>
    ///  A task that represents the asynchronous setup.
    /// </returns>
    /// <remarks>
    ///  Register resources and cleanup callbacks as they are created so they are cleaned up if setup fails.
    ///  <see cref="DisposeAsync"/> is not called when setup fails.
    /// </remarks>
    protected virtual ValueTask InitializeAsync()
        => default;

    /// <summary>
    ///  Performs asynchronous cleanup after the test.
    /// </summary>
    /// <returns>
    ///  A task that represents the asynchronous cleanup.
    /// </returns>
    /// <remarks>
    ///  Release resources owned by the derived class here. The base class disposes registered resources
    ///  and <see cref="Environment"/> automatically; overrides do not need to dispose them.
    /// </remarks>
    protected virtual ValueTask DisposeAsync()
        => default;

    /// <summary>
    ///  Registers a resource for automatic cleanup after the test.
    /// </summary>
    /// <param name="resource">The resource to dispose.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resource"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Automatic cleanup has started.</exception>
    /// <remarks>
    ///  If <paramref name="resource"/> also implements <see cref="IAsyncDisposable"/>, its asynchronous
    ///  disposal method is used instead.
    /// </remarks>
    protected void RegisterDisposable(IDisposable resource)
        => RegisterDisposableCore(resource);

    /// <summary>
    ///  Registers a resource for automatic asynchronous cleanup after the test.
    /// </summary>
    /// <param name="resource">The resource to dispose asynchronously.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resource"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Automatic cleanup has started.</exception>
    protected void RegisterDisposable(IAsyncDisposable resource)
        => RegisterDisposableCore(resource);

    private void RegisterDisposableCore(object resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _disposables ??= new();
            _disposables.Add(resource);
        }
    }

    /// <summary>
    ///  Registers a callback for automatic cleanup after the test.
    /// </summary>
    /// <param name="cleanup">The callback to invoke during cleanup.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cleanup"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Automatic cleanup has started.</exception>
    protected void RegisterCleanup(Action cleanup)
    {
        ArgumentNullException.ThrowIfNull(cleanup);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _disposables ??= new();
            _disposables.AddAction(cleanup);
        }
    }

    /// <summary>
    ///  Registers a callback for automatic asynchronous cleanup after the test.
    /// </summary>
    /// <param name="cleanup">The callback to invoke and await during cleanup.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cleanup"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Automatic cleanup has started.</exception>
    protected void RegisterCleanup(Func<ValueTask> cleanup)
    {
        ArgumentNullException.ThrowIfNull(cleanup);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _disposables ??= new();
            _disposables.AddAsyncAction(cleanup);
        }
    }
}
