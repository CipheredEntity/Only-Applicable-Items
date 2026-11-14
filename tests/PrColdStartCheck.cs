using System;
using System.Reflection;
using OnlyApplicableItems.Integration;

// Shared by the pure checks and shipped-Mono probe. This models the native
// constructor's balance-read boundary, not Unity's balance-loading lifecycle.
internal static class PrColdStartBalance
{
    internal static bool Loaded = true;
    internal static int DefinitionReads;
    internal static void ReadDefinition()
    {
        DefinitionReads++;
        if (!Loaded)
            throw new InvalidOperationException("GameBalanceBase.Instance is null. You should set it after the balance is loaded to use this functionality.");
    }
}

internal static class PrColdStartCheck
{
    internal static void Run()
    {
        bool wasLoaded = PrColdStartBalance.Loaded;
        int previousReads = PrColdStartBalance.DefinitionReads;
        try
        {
            PrColdStartBalance.Loaded = false;
            bool reproduced = false;
            try { _ = new Item("empty", 0); }
            catch (InvalidOperationException exception)
            { reproduced = exception.Message.StartsWith("GameBalanceBase.Instance is null", StringComparison.Ordinal); }
            Require(reproduced && PrColdStartBalance.DefinitionReads == previousReads + 1,
                "The named empty-item constructor must reproduce the native pre-balance failure.");

            int reads = PrColdStartBalance.DefinitionReads;
            var fields = new PrItemFields(); // The old production warm-up fails here.
            var probe = new Item();
            Require(fields.Count(probe) == -1 && fields.Size(probe) == 0 && fields.Items(probe).Count == 0 &&
                fields.Properties(probe).Count == 0 && fields.OnThread,
                "Parameterless detached items must support every validated raw reader before balance loading.");
            fields.SetCount(probe, 0); fields.SetFill(probe, 0);
            var fill = typeof(Item).GetField("inventoryFillSize", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Require(fields.Count(probe) == 0 && (int)fill.GetValue(probe)! == 0 &&
                fields.Items(fields.Root(new Inventory())).Count == 0,
                "Both warmed writers and the native parameterless inventory boundary must work before balance loading.");
            Require(!PrColdStartBalance.Loaded && PrColdStartBalance.DefinitionReads == reads,
                "Accessor initialization must not initialize game balance or perform any definition lookup.");
        }
        finally
        {
            PrColdStartBalance.Loaded = wasLoaded;
            PrColdStartBalance.DefinitionReads = previousReads;
        }
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
