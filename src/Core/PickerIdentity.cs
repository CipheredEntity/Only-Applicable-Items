using System;

namespace OnlyApplicableItems.Core;

// Identity never comes from the displayed title. Separate categories can share a
// default title and later receive independent naming settings without changing IDs.
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
    Sermons
}

internal static class PickerTitles
{
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
            case PickerId.Sermons: return "Sermons";
            default: throw new ArgumentOutOfRangeException(nameof(id));
        }
    }
}
