using Application.Interface;

namespace Application.Implementation;

internal sealed class AutoResetEventProgram : IProgram
{
    private readonly AutoResetEvent _autoEvent = new(initialState: false);

    public string Name => "AutoResetEvent Demo";

    public void Run()
    {
        Task.Run(() => CalculateSum(maxNumber: 50));
        Thread.Sleep(millisecondsTimeout: 3000);
        _autoEvent.Set();
    }

    private void CalculateSum(int maxNumber)
    {
        var sum = 0;
        for (var i = 0; i <= maxNumber; i++)
        {
            sum += i;
        }

        Console.WriteLine("Sum calculated. Waiting for main thread signal...");
        _autoEvent.WaitOne();
        Console.WriteLine($"Sum: {sum}");
    }
}
