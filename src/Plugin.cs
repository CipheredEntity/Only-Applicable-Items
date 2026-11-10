using System;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using OnlyApplicableItems.Configuration;
using OnlyApplicableItems.Core;
using OnlyApplicableItems.Integration;
using OnlyApplicableItems.UI;

namespace OnlyApplicableItems;

[BepInPlugin(Guid, Name, Version)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Guid = "ciphe.gk2.onlyapplicableitems";
    public const string Name = "Only Applicable Items";
    public const string Version = "0.7.0";
    internal static Plugin? Instance { get; private set; }
    internal ActionSettings Settings { get; private set; } = null!;
    private readonly List<ApplicableItemsSession> sessions = new List<ApplicableItemsSession>();
    private readonly HashSet<string> warnings = new HashSet<string>(StringComparer.Ordinal);
    private Harmony? harmony;
    private Harmony? alchemyHarmony;
    private GardenPickerContract? contract;
    private BodyPartPickerContract? bodyParts;
    private EmbalmingPickerContract? embalming;
    private GraveDecorationPickerContract? graveDecorations;
    private ZombieEquipmentPickerContract? zombieEquipment;
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
        {
            InitializeAlchemy();
            InitializeBodyParts();
            InitializeEmbalming();
            InitializeGraveDecorations();
            InitializeZombieEquipment();
        }
    }

    private void InitializeZombieEquipment()
    {
        try
        {
            zombieEquipment = new ZombieEquipmentPickerContract(() => Settings.ZombieEquipment.Value, contract!.ValidatePlainItem);
            Logger.LogInfo("Zombie Equipment ready (Collars only). Enabled: " + Settings.ZombieEquipment.Value);
        }
        catch (Exception exception)
        {
            zombieEquipment = null;
            WarnOnce("zombie-equipment-startup", "Zombie Equipment is unavailable; existing actions remain supported. " + exception);
        }
    }

    private void InitializeGraveDecorations()
    {
        try
        {
            graveDecorations = new GraveDecorationPickerContract(() => Settings.GraveDecorations.Value, contract!.ValidatePlainItem);
            Logger.LogInfo("Grave Decorations ready. Enabled: " + Settings.GraveDecorations.Value);
        }
        catch (Exception exception)
        {
            graveDecorations = null;
            WarnOnce("grave-decorations-startup", "Grave Decorations is unavailable; existing actions remain supported. " + exception);
        }
    }

    private void InitializeEmbalming()
    {
        try
        {
            embalming = new EmbalmingPickerContract(() => Settings.EmbalmingFluids.Value, contract!.ValidatePlainItem);
            Logger.LogInfo("Embalming Fluids ready. Enabled: " + Settings.EmbalmingFluids.Value);
        }
        catch (Exception exception)
        {
            embalming = null;
            WarnOnce("embalming-startup", "Embalming Fluids is unavailable; existing actions remain supported. " + exception);
        }
    }

    private void InitializeBodyParts()
    {
        try
        {
            bodyParts = new BodyPartPickerContract(() => Settings.BodyParts.Value, contract!.ValidatePlainItem);
            Logger.LogInfo("Body Parts ready. Enabled: " + Settings.BodyParts.Value);
        }
        catch (Exception exception)
        {
            bodyParts = null;
            WarnOnce("body-parts-startup", "Body Parts is unavailable; existing actions remain supported. " + exception);
        }
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
        PickerPolicy<Item>? policy = null;
        ApplicableItemsSession? session = null;
        try
        {
            // Discovery includes private-field reads for autopsy pickers. If it fails,
            // the constructor's original inventory sections must remain usable.
            policy = contract.GetPolicy(callback, predicate, Settings) ?? Alchemy?.GetPolicy(callback, predicate)
                ?? bodyParts?.GetPolicy(callback, predicate) ?? embalming?.GetPolicy(callback, predicate)
                ?? graveDecorations?.GetPolicy(callback, predicate) ?? zombieEquipment?.GetPolicy(callback, predicate);
            if (policy == null || !policy.IsEnabled)
                return;
            session = new ApplicableItemsSession(this, policy, data, predicate);
            session.Commit();
        }
        catch (Exception exception)
        {
            session?.Dispose();
            var title = policy?.Title ?? "item";
            WarnOnce("construction-" + title, "Leaving the original " + title +
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
