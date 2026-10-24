using System.IO;
using System.IO.Pipes;

namespace ChatMixOverlay;

public static class SingleInstance
{
    private static string PipeName => "ChatMixOverlay-" + System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
    public static void Send(string command)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            pipe.Connect(2000);
            using var writer = new StreamWriter(pipe) { AutoFlush = true };
            writer.WriteLine(command);
        }
        catch (IOException) { }
        catch (TimeoutException) { }
    }
    public static async Task Listen(Action<string> received, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(ct);
                using var reader = new StreamReader(pipe);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                var command = await reader.ReadLineAsync(timeout.Token);
                if (command is "settings" or "exit" or "memory") received(command);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (OperationCanceledException) { }
            catch (IOException) { }
        }
    }
}
