using Domain.GisMt.Models;

namespace Domain.Entitys.SettingsSchema;

/// <summary>
/// Справочник товарных групп Честного знака: код, имя API, русское название.
/// </summary>
public static class TrueApiProductGroupCatalog
{
    private static readonly Dictionary<string, GisMtConnectedProductGroup> ByName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["lp"] = Item(TrueApiGroup.Lp, "lp", "Товары легкой промышленности"),
            ["clothes"] = Item(TrueApiGroup.Lp, "lp", "Товары легкой промышленности"),
            ["shoes"] = Item(TrueApiGroup.Shoes, "shoes", "Обувь"),
            ["tobacco"] = Item(TrueApiGroup.Tobaco, "tobacco", "Табачная продукция"),
            ["tobaco"] = Item(TrueApiGroup.Tobaco, "tobacco", "Табачная продукция"),
            ["perfumery"] = Item(TrueApiGroup.Perfumery, "perfumery", "Парфюмерная продукция"),
            ["perfume"] = Item(TrueApiGroup.Perfumery, "perfumery", "Парфюмерная продукция"),
            ["tires"] = Item(TrueApiGroup.Tires, "tires", "Шины"),
            ["electronics"] = Item(TrueApiGroup.Electronics, "electronics", "Фототовары"),
            ["milk"] = Item(TrueApiGroup.Milk, "milk", "Молочная продукция"),
            ["bicycle"] = Item(TrueApiGroup.Bicycle, "bicycle", "Велосипеды"),
            ["wheelchairs"] = Item(TrueApiGroup.Wheelchairs, "wheelchairs", "Кресла-коляски"),
            ["otp"] = Item(TrueApiGroup.Otp, "otp", "Альтернативная табачная продукция"),
            ["water"] = Item(TrueApiGroup.Water, "water", "Вода"),
            ["furs"] = Item(TrueApiGroup.Furs, "furs", "Изделия из меха"),
            ["beer"] = Item(TrueApiGroup.Beer, "beer", "Пиво, напитки на основе пива и слабоалкогольные напитки"),
            ["ncp"] = Item(TrueApiGroup.Ncp, "ncp", "Никотиносодержащая продукция"),
            ["bio"] = Item(TrueApiGroup.Bio, "bio", "БАДы"),
            ["antiseptic"] = Item(TrueApiGroup.Antiseptic, "antiseptic", "Антисептики"),
            ["petfood"] = Item(TrueApiGroup.Petfood, "petfood", "Корма для животных"),
            ["seafood"] = Item(TrueApiGroup.Seafood, "seafood", "Икра осетровых и лососевых рыб"),
            ["nabeer"] = Item(TrueApiGroup.Nabeer, "nabeer", "Безалкогольное пиво"),
            ["softdrinks"] = Item(TrueApiGroup.Softdrinks, "softdrinks", "Безалкогольные напитки"),
            ["meat"] = Item(TrueApiGroup.Meat, "meat", "Мясные изделия"),
            ["vetpharma"] = Item(TrueApiGroup.Vetpharma, "vetpharma", "Ветеринарные препараты"),
            ["toys"] = Item(TrueApiGroup.Toys, "toys", "Детские товары"),
            ["conserve"] = Item(TrueApiGroup.Conserve, "conserve", "Консервированные продукты"),
            ["vegetableoil"] = Item(TrueApiGroup.Vegetableoil, "vegetableoil", "Растительные масла"),
            ["chemistry"] = Item(TrueApiGroup.Chemistry, "chemistry", "Косметика, бытовая химия и товары личной гигиены"),
            ["grocery"] = Item(TrueApiGroup.Grocery, "grocery", "Бакалея"),
            ["pharmaraw"] = Item(TrueApiGroup.Pharmaraw, "pharmaraw", "Лекарственные препараты"),
            ["pharma"] = Item(TrueApiGroup.Pharmaraw, "pharmaraw", "Лекарственные препараты"),
            ["construction"] = Item(TrueApiGroup.Construction, "construction", "Стройматериалы"),
            ["autofluids"] = Item(TrueApiGroup.Autofluids, "autofluids", "Моторные масла"),
            ["sweets"] = Item(TrueApiGroup.Sweets, "sweets", "Сладости")
        };

    /// <summary>
    /// Собирает объект группы по коду из ответа Честного знака.
    /// </summary>
    public static GisMtConnectedProductGroup Resolve(string raw)
    {
        var name = (raw ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name))
            return new GisMtConnectedProductGroup();

        if (ByName.TryGetValue(name, out var known))
            return Copy(known);

        return new GisMtConnectedProductGroup
        {
            Name = name,
            GroupName = name
        };
    }

    private static GisMtConnectedProductGroup Item(int code, string name, string groupName)
        => new()
        {
            Code = code,
            Name = name,
            GroupName = groupName
        };

    private static GisMtConnectedProductGroup Copy(GisMtConnectedProductGroup item)
        => new()
        {
            Code = item.Code,
            Name = item.Name,
            GroupName = item.GroupName
        };
}
