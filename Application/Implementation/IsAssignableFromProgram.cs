using Application.Interface;

namespace Application.Implementation;

internal sealed class IsAssignableFromProgram : IProgram
{
    public string Name => "Type.IsAssignableFrom Demo";

    public void Run()
    {
        Console.WriteLine("Defined classes:");

        var roomType = typeof(Room);
        var kitchenType = typeof(Kitchen);
        var bedroomType = typeof(Bedroom);
        var guestroomType = typeof(Guestroom);
        var masterBedroomType = typeof(MasterBedroom);

        Console.WriteLine("room assignable from kitchen: {0}", roomType.IsAssignableFrom(kitchenType));
        Console.WriteLine("bedroom assignable from guestroom: {0}", bedroomType.IsAssignableFrom(guestroomType));
        Console.WriteLine("kitchen assignable from masterbedroom: {0}", kitchenType.IsAssignableFrom(masterBedroomType));
        Console.WriteLine("room assignable from guestroom: {0}", roomType.IsAssignableFrom(guestroomType));
    }
}

internal class MasterBedroom : Bedroom;

internal class Guestroom : Bedroom;

internal class Bedroom : Room;

internal class Kitchen : Room;

internal class Room;
