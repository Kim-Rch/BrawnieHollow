namespace BrawnieHollow;
public enum WorkKind { Till, Plant, Water, Harvest }
public record Job(int Index, WorkKind Kind);
public class Plot { public bool Tilled { get; set; } public bool Planted { get; set; } public bool Watered { get; set; } public int Age { get; set; } }
public class Settlement
{
    public static void RecoverItems<T>(ICollection<T> source, Action<T> store)
    {
        foreach (T item in source.ToArray()) { store(item); source.Remove(item); }
    }
    public static void LeaveSafely(Action recover, Action cleanup, Action relocate, Action<Exception> report)
    {
        try { recover(); } catch (Exception ex) { report(ex); }
        try { cleanup(); } catch (Exception ex) { report(ex); }
        finally { relocate(); }
    }
    public static bool ShouldSuppressInput(bool here, bool action, bool tool, bool menuOpen = false) => here && !menuOpen && (action || tool);
    public static bool ShouldCancelWarp(object? request, object? owned, object? destination, object? village) =>
        request != null && (ReferenceEquals(request, owned) || (village != null && ReferenceEquals(destination, village)));
    public static bool CanTick(bool ready, bool host, bool paused, bool saving) => ready && host && !paused && !saving;
    public int Day { get; set; }
    public string Season { get; set; } = "spring"; public int Harvests { get; set; }
    public List<Plot> Plots { get; set; } = new();
    public void Validate(int today)
    {
        if (today < 1 || Day < 0 || Day > today || (Day > 0 && today - Day > 1120)
            || Harvests < 0 || Harvests > int.MaxValue - 12 * 1120
            || Plots == null || (Day == 0 ? Plots.Count != 0 || Harvests != 0 : Plots.Count != 12)
            || Season is not ("spring" or "summer" or "fall" or "winter")
            || (Day > 0 && Season != SeasonAt(Day))
            || Plots.Any(p => p == null || p.Age < 0 || p.Age > GrowthDays
                || (p.Planted && !p.Tilled) || (!p.Planted && p.Age != 0)))
            throw new InvalidDataException("Invalid settlement state or catchup exceeds ten years. Save data was not overwritten.");
    }
    public void StartDay(int day, string season)
    {
        Validate(day);
        if (Plots.Count == 0) Plots = Enumerable.Range(0, 12).Select(_ => new Plot()).ToList();
        if (Day == 0) { Day = day; Season = season; return; }
        while (Day < day)
        {
            FinishWork();
            foreach (var p in Plots) { if (p.Planted && p.Watered) p.Age++; p.Watered = false; }
            Day++;
            var nextSeason = SeasonAt(Day);
            if (nextSeason != Season) { foreach (var p in Plots) { p.Planted = false; p.Watered = false; p.Age = 0; } Season = nextSeason; }
        }
        Season = season;
    }
    public void FinishWork() { int guard = 0; while (Next() is { } job && guard++ < 100) Complete(job); }
    public void Complete(Job job)
    {
        if (Next() != job) return;
        var p = Plots[job.Index];
        switch (job.Kind) { case WorkKind.Till: p.Tilled = true; break; case WorkKind.Plant: p.Planted = true; p.Age = 0; break; case WorkKind.Water: p.Watered = true; break; case WorkKind.Harvest: p.Planted = false; p.Watered = false; p.Age = 0; Harvests++; break; }
    }
    public static string SeasonAt(int day) => new[] { "spring", "summer", "fall", "winter" }[((day - 1) / 28) % 4];
    public Job? Next() { if (Season == "winter") return null; for (int i = 0; i < Plots.Count; i++) { var p = Plots[i]; if (!p.Tilled) return new(i, WorkKind.Till); if (!p.Planted) return new(i, WorkKind.Plant); if (p.Age >= GrowthDays) return new(i, WorkKind.Harvest); if (!p.Watered) return new(i, WorkKind.Water); } return null; }
    public int GrowthDays => Season switch { "summer" => 6, "fall" => 4, _ => 4 };
}
public class ResidentMotion
{
    public static (float X, float Y) EveningTarget(int resident, float x, float y)
    {
        // Y-first motion needs an explicit south/east exit around the fence and crate.
        if (resident == 0 && x < 16 * 64 && y > 12 * 64)
            return y < 17 * 64 ? (x, 17 * 64) : (16 * 64, y);
        // Cross west on the clear lane south of the well, then approach the hearth.
        if (resident == 2 && x > 21 * 64)
            return y > 13 * 64 ? (x, 13 * 64) : (21 * 64, y);
        return ((17 + resident * 2) * 64, 12 * 64);
    }
    public float X { get; private set; }
    public float Y { get; private set; }
    public int Facing { get; private set; }
    public int WorkTicks { get; private set; }
    public ResidentMotion(float x, float y) { X = x; Y = y; }
    public bool Step(float x, float y, bool work)
    {
        if (Math.Abs(Y - y) > 0.1f) { Facing = Y < y ? 2 : 0; Y += Math.Clamp(y - Y, -2, 2); WorkTicks = 0; return false; }
        if (Math.Abs(X - x) > 0.1f) { Facing = X < x ? 1 : 3; X += Math.Clamp(x - X, -2, 2); WorkTicks = 0; return false; }
        if (!work) { WorkTicks = 0; return false; }
        Facing = 1;
        if (++WorkTicks < 84) return false;
        WorkTicks = 0; return true;
    }
}
