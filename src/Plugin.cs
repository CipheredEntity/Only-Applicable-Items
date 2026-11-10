using System;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using OnlyApplicableItems.Configuration;
using OnlyApplicableItems.Integration;
using OnlyApplicableItems.UI;

namespace OnlyApplicableItems;

[BepInPlugin(Guid, Name, Version)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Guid = "ciphe.gk2.onlyapplicableitems";
    public const string Name = "Only Applicable Items";
    public const string Version = "0.3.1";
    internal static Plugin? Instance { get; private set; }
    internal ActionSettings Settings { get; private set; } = null!;
    private readonly List<ApplicableItemsSession> sessions = new List<ApplicableItemsSession>();
    private readonly HashSet<string> warnings = new HashSet<string>(StringComparer.Ordinal);
    private Harmony? harmony;
    private Harmony? alchemyHarmony;
    private GardenPickerContract? contract;
    internal AlchemyFeatures? Alchemy { get; private set; }

    private void Awake()
    {
        Instance = this;
        Settings = new ActionSettings(Config);
        try
        {
            contract = new GardenPickerContract();
            harmony = new Harmony(Guid);
            harmony.Patch(contract.PickerConstructor,
                postfix: new HarmonyMethod(typeof(PickerPatches), nameof(PickerPatches.AfterPickerConstructed)));
            harmony.Patch(contract.InventoryRedrawNotification,
                prefix: new HarmonyMethod(typeof(PickerPatches), nameof(PickerPatches.BeforeInventoryRedrawNotification)));
            if (!contract.SupportsFertilizers)
                WarnOnce("fertilizer-contract", "The garden fertilizer predicate was not found; fertilizer consolidation is inactive. Seeds remain supported.");
            Logger.LogInfo(Name + " " + Version + " loaded. Seeds: " + Settings.Seeds.Value +
                "; Fertilizers: " + Settings.Fertilizers.Value);
        }
        catch (Exception exception)
        {
            harmony?.UnpatchSelf();
            contract = null;
            WarnOnce("startup", "The game's picker contract is not supported; consolidation is inactive. " + exception);
        }
        if (contract != null)
            InitializeAlchemy();
    }

    private void InitializeAlchemy()
    {
        try
        {
            var alchemyContract = new AlchemyContract();
            Alchemy = new AlchemyFeatures(this, alchemyContract, contract!.ValidatePlainItem);
            // A separate owner lets alchemy initialization fail without removing garden patches.
            alchemyHarmony = new Harmony(Guid + ".alchemy");
            alchemyHarmony.Patch(alchemyContract.CountUpdate,
                prefix: new HarmonyMethod(typeof(AlchemyPatches), nameof(AlchemyPatches.BeforeCountUpdate)),
                finalizer: new HarmonyMethod(typeof(AlchemyPatches), nameof(AlchemyPatches.AfterCountUpdate)));
            alchemyHarmony.Patch(alchemyContract.Hide,
                postfix: new HarmonyMethod(typeof(AlchemyPatches), nameof(AlchemyPatches.AfterLaboratoryHidden)));
            Logger.LogInfo("Alchemical Ingredients ready. Enabled: " + Settings.AlchemicalIngredients.Value);
        }
        catch (Exception exception)
        {
            Alchemy?.Dispose();
            alchemyHarmony?.UnpatchSelf();
            Alchemy = null;
            WarnOnce("alchemy-startup", "Alchemical Ingredients is unavailable; Seeds and Fertilizers remain supported. " + exception);
        }
    }

    internal void TryConsolidate(UIMultiInventoryWindowData data, Action<UIItemCell> callback, Func<Item, bool> predicate)
    {
        if (contract == null)
            return;
        var policy = contract.GetPolicy(callback, predicate, Settings) ?? Alchemy?.GetPolicy(callback, predicate);
        if (policy == null || !policy.IsEnabled)
            return;
        ApplicableItemsSession? session = null;
        try
        {
            session = new ApplicableItemsSession(this, policy, data, predicate);
            session.Commit();
        }
        catch (Exception exception)
        {
            session?.Dispose();
            WarnOnce("construction-" + policy.Title, "Leaving the original " + policy.Title +
                " picker in place because consolidation was unsafe. " + exception);
        }
    }

    private void LateUpdate()
    {
        foreach (var session in sessions.ToArray())
        {
            try
            {
                session.Tick();
            }
            catch (Exception exception)
            {
                session.RequestFallback("updating", exception);
                session.RestoreVanilla();
            }
        }
        Alchemy?.Tick();
    }

    internal void Register(ApplicableItemsSession session) => sessions.Add(session);
    internal void Unregister(ApplicableItemsSession session) => sessions.Remove(session);

    internal void WarnOnce(string key, string message)
    {
        if (warnings.Add(key))
            Logger.LogWarning(message);
    }

    private void OnDestroy()
    {
        foreach (var session in sessions.ToArray())
            session.RestoreVanilla();
        Alchemy?.Dispose();
        alchemyHarmony?.UnpatchSelf();
        harmony?.UnpatchSelf();
        if (ReferenceEquals(Instance, this))
            Instance = null;
    }
}
