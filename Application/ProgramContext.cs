using Application.Implementation;
using Application.Interface;

namespace Application;

internal sealed class ProgramContext
{
    private readonly IReadOnlyList<IProgram> _programs =
    [
        new AutoResetEventProgram(),
        new IsAssignableFromProgram()
    ];

    internal void ShowAllOptions()
    {
        for (var i = 0; i < _programs.Count; i++)
        {
            Console.WriteLine($"Option {i}: {_programs[i].Name}");
        }
    }

    internal void RunWith(int option)
    {
        if (option < 0 || option >= _programs.Count)
        {
            Console.WriteLine("Option out of range.");
            return;
        }

        _programs[option].Run();
    }
}
