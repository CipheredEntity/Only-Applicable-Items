using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Configuration;

internal sealed class InventoryNameSettings
{
    private readonly Dictionary<PickerId, ConfigEntry<string>> entries = new Dictionary<PickerId, ConfigEntry<string>>();

    public InventoryNameSettings(ConfigFile config)
    {
        foreach (PickerId id in Enum.GetValues(typeof(PickerId)))
        {
            string key = GetKey(id);
            string title = PickerTitles.GetDefault(id);
            entries.Add(id, config.Bind("Inventory Names", key, string.Empty, SettingMetadata.Describe(key,
                "Leave blank to use \"" + title + "\".",
                1000 - (int)id)));
        }
    }

    public string GetTitle(PickerId id)
    {
        string value = entries[id].Value;
        return string.IsNullOrWhiteSpace(value) ? PickerTitles.GetDefault(id) : value;
    }

    public PickerPolicy<T>[] Apply<T>(IReadOnlyList<PickerPolicy<T>> policies) where T : class
    {
        var result = new PickerPolicy<T>[policies.Count];
        for (int i = 0; i < result.Length; i++)
            result[i] = policies[i].WithTitle(GetTitle(policies[i].Id));
        return result;
    }

    public static string GetKey(PickerId id)
    {
        switch (id)
        {
            case PickerId.Seeds: return "Seeds";
            case PickerId.Fertilizers: return "Fertilizers";
            case PickerId.AlchemicalIngredients: return "Alchemical Ingredients";
            case PickerId.Brains: return "Body Parts - Brains";
            case PickerId.Hearts: return "Body Parts - Hearts";
            case PickerId.Guts: return "Body Parts - Guts";
            case PickerId.Skulls: return "Body Parts - Skulls";
            case PickerId.Bones: return "Body Parts - Bones";
            case PickerId.Skins: return "Body Parts - Skins";
            case PickerId.EmbalmingFluids: return "Embalming Fluids";
            case PickerId.Tombstones: return "Grave Decorations - Tombstones";
            case PickerId.Fences: return "Grave Decorations - Fences";
            case PickerId.ZombieCollars: return "Zombie Equipment - Collars";
            case PickerId.ZombieArmor: return "Zombie Equipment - Armor";
            case PickerId.ZombieWeapons: return "Zombie Equipment - Weapons";
            case PickerId.ZombieTools: return "Zombie Equipment - Tools";
            case PickerId.ResurrectionCollars: return "Resurrection Collars";
            case PickerId.Sermons: return "Prayers";
            case PickerId.StudyResearchable: return "Study Table - Study";
            case PickerId.StudyDecomposable: return "Study Table - Decompose";
            case PickerId.ShopSelling: return "Shop Selling";
            case PickerId.PrMachineResources: return "PR Machine";
            default: throw new ArgumentOutOfRangeException(nameof(id));
        }
    }
}
