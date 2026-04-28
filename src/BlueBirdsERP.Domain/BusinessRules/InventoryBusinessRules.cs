using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Domain.BusinessRules;

public static class InventoryBusinessRules
{
    public const string KilogramUnit = "Kg";
    public const string GramUnit = "g";
    public const string PiecesUnit = "pieces";

    public static IReadOnlyList<string> GetAllowedUnits(PricingType pricingType)
    {
        return pricingType switch
        {
            PricingType.WeightBased => [KilogramUnit, GramUnit],
            PricingType.UnitBased => [PiecesUnit],
            _ => []
        };
    }

    public static bool IsUnitAllowed(PricingType pricingType, string unitOfMeasure)
    {
        return GetAllowedUnits(pricingType)
            .Any(unit => string.Equals(unit, unitOfMeasure, StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizeUnit(PricingType pricingType, string unitOfMeasure)
    {
        return GetAllowedUnits(pricingType)
            .SingleOrDefault(unit => string.Equals(unit, unitOfMeasure, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Unit is not allowed for the selected pricing type.");
    }
}

