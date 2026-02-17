namespace Application.Interface;

internal interface IProgram
{
    string Name { get; }

    void Run();
}
