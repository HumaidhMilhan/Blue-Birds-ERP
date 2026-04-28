namespace BlueBirdsERP.Application.POS;

public sealed class POSValidationException(string message) : InvalidOperationException(message);

