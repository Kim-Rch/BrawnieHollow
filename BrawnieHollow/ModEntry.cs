using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace BrawnieHollow;

public sealed class ModConfig
{
    public SButton VisitKey { get; set; } = SButton.F8;
    public bool ShowResidentNames { get; set; } = true;
}

/// <summary>
/// A transient, unregistered location. Only Settlement is persisted via SMAPI save data;
/// no custom location, NPC, or terrain feature is injected into the vanilla save graph.
/// </summary>
public sealed class ModEntry : Mod
{
    private const string LocationName = "RachmanNurhakim_BrawnieHollow";
    private const string SaveKey = "settlement-v1";
    private Settlement state = new();
    private GameLocation? hollow;
    private readonly List<NPC> residents = new();
    private readonly List<ResidentMotion> motions = new();
    private ModConfig config = new();
    private string returnLocation = "BusStop";
    private Point returnTile = new(16, 12);
    private bool saving;
    private bool disabled;
    private LocationRequest? ownWarp;
    private int animationTick;
    private Job? activeJob;
    private string activity = "Settling in";

    public override void Entry(IModHelper helper)
    {
        config = helper.ReadConfig<ModConfig>();
        helper.Events.GameLoop.SaveLoaded += SaveLoaded;
        helper.Events.GameLoop.DayStarted += DayStarted;
        helper.Events.GameLoop.DayEnding += DayEnding;
        helper.Events.GameLoop.Saving += Saving;
        helper.Events.GameLoop.Saved += (_, _) => saving = false;
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => Reset();
        helper.Events.GameLoop.UpdateTicked += Update;
        helper.Events.GameLoop.UpdateTicking += (_, _) => SuppressVillageInputs();
        helper.Events.Input.ButtonsChanged += (_, _) => SuppressVillageInputs();
        helper.Events.Input.ButtonPressed += ButtonPressed;
        helper.Events.Display.RenderedWorld += RenderWorld;
        helper.Events.Display.RenderedHud += RenderHud;
        helper.ConsoleCommands.Add("bh_visit", "Visit Brawnie Hollow (single-player only).", (_, _) => Visit());
        helper.ConsoleCommands.Add("bh_return", "Return from Brawnie Hollow.", (_, _) => Return());
        helper.ConsoleCommands.Add("bh_status", "Show saved-day, crop, and work progress.", (_, _) =>
            Monitor.Log($"Day {state.Day}, {state.Season}; harvested {state.Harvests}; next {state.Next()?.ToString() ?? "none"}; {activity}; residents {residents.Count}. Single-player only.", LogLevel.Info));
    }

    private bool Available => Context.IsWorldReady && Context.IsMainPlayer && !Context.IsMultiplayer && !disabled;
    private bool Here => hollow != null && ReferenceEquals(Game1.currentLocation, hollow);
    private bool CanAct => Available && Context.CanPlayerMove && Game1.player.mount == null && !saving && Game1.activeClickableMenu == null && !Game1.eventUp && Game1.currentMinigame == null && !Game1.isWarping;
    private static Vector2 PlotTile(int index) => new(10 + index % 4, 15 + index / 4);

    private void Reset()
    {
        Return(immediate: true);
        if (hollow?.debris.Count > 0) return; // never discard an unrecovered item after a storage failure.
        residents.Clear(); motions.Clear(); hollow = null; state = new();
        saving = false; disabled = false; ownWarp = null; animationTick = 0; activeJob = null;
        activity = "Settling in"; returnLocation = "BusStop"; returnTile = new(16, 12);
    }

    private void SaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        Reset();
        if (!Context.IsMainPlayer || Context.IsMultiplayer)
        {
            disabled = true;
            Monitor.Log("Brawnie Hollow 0.1 is single-player only; village and simulation are disabled in multiplayer.", LogLevel.Warn);
            return;
        }
        try
        {
            state = Helper.Data.ReadSaveData<Settlement>(SaveKey) ?? new();
            state.Validate(Game1.Date.TotalDays + 1);
            BeginDay();
            Monitor.Log($"Brawnie Hollow is ready. Press {config.VisitKey} to visit/return, or use bh_visit. No world map edits.", LogLevel.Info);
        }
        catch (Exception ex) { Disable(ex); }
    }

    private void DayStarted(object? sender, DayStartedEventArgs e)
    {
        if (!Available) return;
        try { BeginDay(); } catch (Exception ex) { Disable(ex); }
    }

    private void BeginDay()
    {
        state.StartDay(Game1.Date.TotalDays + 1, Game1.currentSeason);
        activeJob = null;
        // Rebuild transient graphics from persisted pure state, never from yesterday's game crop updates.
        if (hollow != null) { RebuildSoil(); CreateResidents(); }
    }

    private void EnsureVillage()
    {
        if (hollow != null) return;
        hollow = new GameLocation(Helper.ModContent.GetInternalAssetName("assets/hollow.tmx").BaseName, LocationName);
        hollow.IsOutdoors = true;
        hollow.IsFarm = true;
        hollow.Map.Properties["NoSpawn"] = "All";
        CreateResidents();
        RebuildSoil();
    }

    private void CreateResidents()
    {
        residents.Clear(); motions.Clear();
        var names = new[] { "Bram", "Mira", "Pip" };
        var starts = new[] { new Vector2(9, 15), new Vector2(22, 13), new Vector2(25, 20) };
        for (int i = 0; i < names.Length; i++)
        {
            string sprite = Helper.ModContent.GetInternalAssetName($"assets/{names[i]}.png").BaseName;
            var npc = new NPC(new AnimatedSprite(sprite, 0, 16, 16), starts[i] * 64, LocationName, 2,
                $"BrawnieHollow_{names[i]}", false, null);
            npc.displayName = names[i];
            npc.currentLocation = hollow;
            npc.ignoreMovementAnimation = true;
            residents.Add(npc);
            motions.Add(new ResidentMotion(npc.Position.X, npc.Position.Y));
        }
        // Deliberately NOT in hollow.characters: normal NPC schedules/social data must not own these actors.
        // Actual NPC sprites are drawn below; movement, interaction and job completion belong to this mod.
    }

    private void RebuildSoil()
    {
        if (hollow == null) return;
        for (int i = 0; i < state.Plots.Count; i++) ProjectPlot(i);
    }

    private void ProjectPlot(int i)
    {
        if (hollow == null) return;
        var tile = PlotTile(i);
        var p = state.Plots[i];
        hollow.terrainFeatures.Remove(tile);
        if (!p.Tilled) return;
        var dirt = new HoeDirt(p.Watered ? HoeDirt.watered : HoeDirt.dry, hollow);
        if (p.Planted)
        {
            string seed = state.Season switch { "summer" => "484", "fall" => "491", _ => "472" };
            var crop = new Crop(seed, (int)tile.X, (int)tile.Y, hollow);
            for (int age = 0; age < Math.Min(p.Age, state.GrowthDays); age++) crop.newDay(HoeDirt.watered);
            crop.updateDrawMath(tile);
            dirt.crop = crop;
        }
        hollow.terrainFeatures.Add(tile, dirt);
    }

    private void Visit()
    {
        if (!CanAct || Here) return;
        try
        {
            EnsureVillage();
            RebuildSoil(); // catch up any jobs completed while the player was away.
            returnLocation = Game1.currentLocation.NameOrUniqueName;
            returnTile = Game1.player.TilePoint;
            ownWarp = new LocationRequest(LocationName, false, hollow!);
            Game1.warpFarmer(ownWarp, 18, 23, 0);
            Game1.addHUDMessage(new HUDMessage($"Brawnie Hollow — a little field, a friendly rivalry. {config.VisitKey} returns.", HUDMessage.newQuest_type));
        }
        catch (Exception ex) { Disable(ex); }
    }

    private void Return(bool immediate = false)
    {
        if (hollow == null || Game1.player == null) return;
        bool wasHere = Here || ReferenceEquals(Game1.player.currentLocation, hollow);
        if (immediate) CancelOwnWarp();
        var target = Game1.getLocationFromName(returnLocation) ?? Game1.getLocationFromName("BusStop");
        if (target == null) return;
        Point tile = target.NameOrUniqueName == returnLocation ? returnTile : new Point(16, 12);
        if (!wasHere) { RecoverDebris(target); return; }
        if (!immediate)
        {
            RecoverDebris(target);
            ownWarp = new LocationRequest(target.NameOrUniqueName, false, target);
            Game1.warpFarmer(ownWarp, tile.X, tile.Y, 2);
            return;
        }
        // Normal warpFarmer only queues a fade. Save boundaries need the relocation to be synchronous.
        Settlement.LeaveSafely(() => RecoverDebris(target), () => hollow.cleanupBeforePlayerExit(), () =>
        {
            Game1.player.currentLocation = target;
            Game1.currentLocation = target;
            Game1.player.Position = new Vector2(tile.X, tile.Y) * 64;
            Game1.player.FacingDirection = 2;
            Game1.forceSnapOnNextViewportUpdate = true;
            try { target.resetForPlayerEntry(); } catch (Exception ex) { SafetyFailure(ex); }
        }, SafetyFailure);
    }

    private void SafetyFailure(Exception ex)
    {
        disabled = true;
        Monitor.Log($"Brawnie Hollow safety cleanup failed; original mod data will be preserved: {ex}", LogLevel.Error);
    }

    private void CancelOwnWarp()
    {
        var request = Game1.locationRequest;
        if (!Settlement.ShouldCancelWarp(request, ownWarp, request?.Location, hollow)) return;
        Game1.locationRequest = null;
        typeof(Game1).GetField("_isWarping", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.SetValue(null, false);
        Game1.fadeToBlack = false;
        Game1.fadeIn = false;
        Game1.fadeToBlackAlpha = 0;
        Game1.player.CanMove = true;
        ownWarp = null;
    }

    private void RecoverDebris(GameLocation target)
    {
        if (hollow == null) return;
        Settlement.RecoverItems(hollow.debris, debris =>
        {
            // Inventory drag-drop stores the original Item here (including its whole stack/modData).
            // Vanilla SaveGame explicitly serializes returnedDonations; location debris isn't durable.
            if (debris.item != null)
            {
                Game1.player.team.returnedDonations.Add(debris.item);
                Game1.player.team.newLostAndFoundItems.Value = true;
                debris.item = null;
            }
            else if (debris.itemId.Value != null && (debris.debrisType.Value is Debris.DebrisType.OBJECT or Debris.DebrisType.ARCHAEOLOGY or Debris.DebrisType.RESOURCE))
            {
                foreach (var chunk in debris.Chunks.ToArray())
                {
                    Game1.player.team.returnedDonations.Add(ItemRegistry.Create(debris.itemId.Value, 1, debris.itemQuality));
                    Game1.player.team.newLostAndFoundItems.Value = true;
                    debris.Chunks.Remove(chunk);
                }
            }
            else target.debris.Add(debris); // cosmetic/coin debris remains debris, never synthesized into items or money.
        });
    }

    private void DayEnding(object? sender, DayEndingEventArgs e)
    {
        try
        {
            Return(immediate: true);
            if (Available) state.FinishWork();
        }
        catch (Exception ex) { Disable(ex); }
    }

    private void Saving(object? sender, SavingEventArgs e)
    {
        saving = true;
        try
        {
            Return(immediate: true); // even when disabled, never serialize the transient player location.
            if (!Available) return;
            state.Validate(Game1.Date.TotalDays + 1);
            Helper.Data.WriteSaveData(SaveKey, state);
        }
        catch (Exception ex) { Disable(ex); }
    }

    private void Update(object? sender, UpdateTickedEventArgs e)
    {
        if (!Settlement.CanTick(Available, Context.IsMainPlayer, !Context.CanPlayerMove || Game1.activeClickableMenu != null || Game1.eventUp || Game1.currentMinigame != null, saving)) return;
        try
        {
            animationTick++;
            bool evening = Game1.timeOfDay >= 1800;
            if (!Here)
            {
                if (e.IsMultipleOf(120) && state.Next() is { } offscreen) state.Complete(offscreen);
                return;
            }
            if (evening && state.Next() != null) { state.FinishWork(); activeJob = null; RebuildSoil(); }
            for (int i = 0; i < residents.Count; i++)
            {
                var motion = motions[i];
                var npc = residents[i];
                bool work = false;
                Vector2 target;
                if (evening)
                {
                    var waypoint = ResidentMotion.EveningTarget(i, motion.X, motion.Y);
                    target = new Vector2(waypoint.X, waypoint.Y);
                }
                else if (i == 0 && state.Next() is { } job)
                {
                    activeJob = job;
                    target = (PlotTile(job.Index) + new Vector2(-1, 0)) * 64;
                    work = true;
                    activity = job.Kind switch { WorkKind.Till => "Bram is hoeing", WorkKind.Plant => "Bram is sowing", WorkKind.Water => "Bram is watering", _ => "Bram is harvesting" };
                }
                else if (i == 0) target = new Vector2(15, 17) * 64;
                else if (i == 1) target = new Vector2((animationTick / 900) % 2 == 0 ? 22 : 20, 13) * 64;
                else target = new Vector2(25, (animationTick / 1200) % 2 == 0 ? 20 : 18) * 64;

                float oldX = motion.X, oldY = motion.Y;
                bool finished = motion.Step(target.X, target.Y, work);
                npc.Position = new Vector2(motion.X, motion.Y);
                npc.FacingDirection = motion.Facing;
                bool moving = oldX != motion.X || oldY != motion.Y;
                int walkRow = motion.Facing switch { 0 => 2, 1 => 1, 2 => 0, _ => 3 };
                int frame = walkRow * 4 + (moving ? animationTick / 10 % 4 : 0);
                if (work && !moving && activeJob != null)
                {
                    int row = activeJob.Kind switch { WorkKind.Till => 5, WorkKind.Plant => 6, WorkKind.Water => 4, _ => 7 };
                    frame = row * 4 + motion.WorkTicks / 14 % 2;
                }
                else if (i > 0 && !moving && !evening) frame = 16 + animationTick / 28 % 2;
                npc.Sprite.CurrentFrame = frame;
                if (finished && activeJob is { } completed)
                {
                    state.Complete(completed);
                    ProjectPlot(completed.Index);
                    string sound = completed.Kind switch { WorkKind.Till => "hoeHit", WorkKind.Water => "wateringCan", WorkKind.Harvest => "harvest", _ => "dirtyHit" };
                    hollow!.localSound(sound, PlotTile(completed.Index));
                    activeJob = null;
                }
            }
            if (evening) activity = "Evening at the hearth";
            else if (state.Next() == null) activity = state.Season == "winter" ? "Winter rest — planning spring" : "Field tended — taking a breather";
        }
        catch (Exception ex) { Disable(ex); }
    }

    private void ButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (Settlement.ShouldSuppressInput(Here, e.Button.IsActionButton(), e.Button.IsUseToolButton()))
            Helper.Input.Suppress(e.Button);
        if (!CanAct) return;
        if (e.Button == config.VisitKey)
        {
            Helper.Input.Suppress(e.Button);
            if (Here) Return(); else Visit();
            return;
        }
        if (!Here) return;
        // The rival's field is display-only: tools cannot desynchronize the pure saved farm simulation.
        if (e.Button.IsUseToolButton()) { Helper.Input.Suppress(e.Button); return; }
        if (!e.Button.IsActionButton()) return;
        Vector2 playerTile = Game1.player.Tile;
        for (int i = 0; i < residents.Count; i++)
        {
            if (Vector2.Distance(playerTile, residents[i].Position / 64) > 2.2f) continue;
            Helper.Input.Suppress(e.Button);
            string text = i switch
            {
                0 => state.Season == "winter"
                    ? "Bram: Snow gives the soil a rest. Next spring, let's see whose parsnips shine brighter!"
                    : $"Bram: A rival? I prefer 'neighbor with excellent parsnips.' I've brought in {state.Harvests} crops so far. Watch closely — every row counts!",
                1 => Game1.timeOfDay >= 1800
                    ? "Mira: Come warm your hands. Even a rival gets a place by our hearth."
                    : "Mira: I keep the well and the homes in order. Bram measures the seasons in harvests. I measure them in good suppers.",
                _ => Game1.timeOfDay >= 1800
                    ? "Pip: Today's biggest catch? A leaf. Don't tell Bram."
                    : "Pip: Small pond, big dreams! The path brought you here; that little sign down south will take you back."
            };
            Game1.drawObjectDialogue(text);
            return;
        }
        if (Vector2.Distance(playerTile, new Vector2(18, 25)) <= 2.3f)
        { Helper.Input.Suppress(e.Button); Return(); }
    }

    private void SuppressVillageInputs()
    {
        if (!Here) return;
        foreach (SButton button in Enum.GetValues(typeof(SButton)))
            if (Settlement.ShouldSuppressInput(true, button.IsActionButton(), button.IsUseToolButton()) && Helper.Input.IsDown(button))
                Helper.Input.Suppress(button);
    }

    private void RenderWorld(object? sender, RenderedWorldEventArgs e)
    {
        if (!Here || disabled) return;
        foreach (var npc in residents)
        {
            npc.draw(e.SpriteBatch);
            if (!config.ShowResidentNames) continue;
            Vector2 screen = Game1.GlobalToLocal(Game1.viewport, npc.Position + new Vector2(32, -28));
            Vector2 size = Game1.smallFont.MeasureString(npc.displayName);
            Utility.drawTextWithShadow(e.SpriteBatch, npc.displayName, Game1.smallFont, screen - new Vector2(size.X / 2, 0), Color.Wheat, .8f);
        }
    }

    private void RenderHud(object? sender, RenderedHudEventArgs e)
    {
        if (!Here || disabled || Game1.activeClickableMenu != null) return;
        string line = $"Brawnie Hollow | {activity} | Harvests: {state.Harvests}\n{config.VisitKey}: return  |  Talk to a neighbor with your action button";
        Utility.drawTextWithShadow(e.SpriteBatch, line, Game1.smallFont, new Vector2(20, 20), Color.Wheat);
    }

    private void Disable(Exception ex)
    {
        Monitor.Log($"Brawnie Hollow stopped safely; save data will not be overwritten. {ex}", LogLevel.Error);
        disabled = true;
        try { Return(immediate: true); } catch (Exception cleanup) { SafetyFailure(cleanup); }
    }
}
