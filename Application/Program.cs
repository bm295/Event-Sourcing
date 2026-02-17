using Application;

var programContext = new ProgramContext();
programContext.ShowAllOptions();

Console.Write("Enter option: ");
if (!int.TryParse(Console.ReadLine(), out var option))
{
    Console.WriteLine("Invalid option.");
    return;
}

programContext.RunWith(option);
