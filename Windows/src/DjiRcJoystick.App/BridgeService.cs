namespace DjiRcJoystick.App;

internal sealed class BridgeService(Options options) : IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private Task? worker;

    public event Action<string>? StatusChanged;

    public void Start()
    {
        worker ??= Task.Run(() => RunAsync(cancellation.Token));
    }

    public async Task StopAsync()
    {
        cancellation.Cancel();
        if (worker is null) return;
        try
        {
            await worker.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var vjoy = new VJoyClient(options.VJoyDeviceId);
                await RunControllerLoopAsync(vjoy, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                SetStatus($"Bridge unavailable: {exception.Message}");
                await Task.Delay(2_000, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task RunControllerLoopAsync(VJoyClient vjoy, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var port = PortDiscovery.SelectPort(options.Port);
                if (port is null)
                {
                    SetStatus("Waiting for DJI RC");
                    await Task.Delay(1_000, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                SetStatus($"Connecting to {port}...");
                using var controller = new ControllerSession(port);
                while (!controller.Poll(cancellationToken))
                {
                    SetStatus($"Waiting for control reports on {port}...");
                }
                SetStatus($"DJI RC connected on {port}");
                vjoy.Post(controller.State);
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (controller.Poll(cancellationToken))
                    {
                        vjoy.Post(controller.State);
                    }
                    else
                    {
                        vjoy.Post(new Protocol.ControllerState());
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (VJoyException)
            {
                throw;
            }
            catch (Exception exception)
            {
                vjoy.Post(new Protocol.ControllerState());
                SetStatus($"DJI RC unavailable: {exception.Message}; retrying");
                await Task.Delay(1_000, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void SetStatus(string status) => StatusChanged?.Invoke(status);

    public void Dispose()
    {
        cancellation.Cancel();
        if (worker is not null)
        {
            try
            {
                worker.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
        }
        cancellation.Dispose();
    }
}
