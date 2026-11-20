using System;

namespace OnlyApplicableItems.Core;

// Identity never comes from the displayed title. Separate categories and window
// headers can receive independent naming settings without changing existing IDs.
internal enum PickerId
{
    Seeds,
    Fertilizers,
    AlchemicalIngredients,
    Brains,
    Hearts,
    Guts,
    Skulls,
    Bones,
    Skins,
    EmbalmingFluids,
    Tombstones,
    Fences,
    ZombieCollars,
    ZombieArmor,
    ZombieWeapons,
    ZombieTools,
    ResurrectionCollars,
    Sermons,
    StudyResearchable,
    StudyDecomposable,
    ShopSelling,
    PrMachineResources,
    StudyTableHeader // Presentation only; never an inventory section policy.
}

internal static class PickerTitles
{
    // Native headers pass through localization and TextMeshPro rich-text parsing.
    // A no-op tag prevents custom names being mistaken for locale keys; escaping
    // every opening angle bracket keeps user-supplied markup literal. Split '&'
    // too, so LLBase does not turn a literal &#xA; into a line break.
    public static string GetHeaderText(PickerId id, string title)
        => title == GetDefault(id) ? title : "<noparse></noparse>" +
            title.Replace("<", "<noparse><</noparse>").Replace("&", "&<noparse></noparse>");

    public static string GetDefault(PickerId id)
    {
        switch (id)
        {
            case PickerId.Seeds: return "Seeds";
            case PickerId.Fertilizers: return "Fertilizers";
            case PickerId.AlchemicalIngredients: return "Alchemical Ingredients";
            case PickerId.Brains: return "Brains";
            case PickerId.Hearts: return "Hearts";
            case PickerId.Guts: return "Guts";
            case PickerId.Skulls: return "Skulls";
            case PickerId.Bones: return "Bones";
            case PickerId.Skins: return "Skins";
            case PickerId.EmbalmingFluids: return "Embalming Fluids";
            case PickerId.Tombstones: return "Tombstones";
            case PickerId.Fences: return "Fences";
            case PickerId.ZombieCollars:
            case PickerId.ResurrectionCollars: return "Collars";
            case PickerId.ZombieArmor: return "Armor";
            case PickerId.ZombieWeapons: return "Weapons";
            case PickerId.ZombieTools: return "Tools";
            case PickerId.Sermons: return "Prayers";
            case PickerId.StudyResearchable: return "Study";
            case PickerId.StudyDecomposable: return "Decompose";
            case PickerId.ShopSelling: return "Sellable";
            case PickerId.PrMachineResources: return "PR Resources";
            case PickerId.StudyTableHeader: return "Study Table";
            default: throw new ArgumentOutOfRangeException(nameof(id));
        }
    }
}
