namespace BlueBirdsERP.Application.POS;

public sealed class SystemClock : ISystemClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}

