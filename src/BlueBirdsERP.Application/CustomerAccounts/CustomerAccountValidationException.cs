namespace BlueBirdsERP.Application.CustomerAccounts;

public sealed class CustomerAccountValidationException(string message) : InvalidOperationException(message);

