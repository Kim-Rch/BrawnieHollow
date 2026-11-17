using BrawnieHollow;
static void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); }
var s = new Settlement();
s.StartDay(1, "spring");
Check(s.Day == 1 && s.Plots.Count == 12 && s.Next()?.Kind == WorkKind.Till, "new settlement starts with twelve untilled plots");
var first = s.Next()!; s.Complete(first);
Check(s.Plots[0].Tilled && s.Next()?.Kind == WorkKind.Plant, "tilling exposes planting on the same plot");
s.Complete(s.Next()!); s.Complete(s.Next()!);
Check(s.Plots[0].Planted && s.Plots[0].Watered && s.Plots[0].Age == 0, "plant then water creates a living crop without instant growth");
s.StartDay(5, "spring");
Check(s.Plots.All(p => p.Age == 4) && s.Harvests == 0 && s.Next()?.Kind == WorkKind.Harvest, "offscreen days finish work and grow crops once per watered night");
var before = s.Plots[0].Age; s.StartDay(5,"spring");
Check(s.Plots[0].Age == before, "same day cannot double grow");
s.Complete(s.Next()!);
Check(s.Harvests == 1 && !s.Plots[0].Planted, "harvest increments real saved production");
var seasonal = new Settlement(); seasonal.StartDay(28,"spring"); seasonal.FinishWork(); seasonal.StartDay(29,"summer");
Check(seasonal.Plots.All(p=>!p.Planted && p.Tilled) && seasonal.GrowthDays == 6, "season boundary clears old crop but preserves tilled soil");
seasonal.StartDay(85,"winter");
Check(seasonal.Next() == null && seasonal.Plots.All(p=>!p.Planted), "winter stops planting and clears seasonal crops");
seasonal.StartDay(113,"spring");
Check(seasonal.Next()?.Kind == WorkKind.Plant, "spring resumes planting after winter");
var restored = System.Text.Json.JsonSerializer.Deserialize<Settlement>(System.Text.Json.JsonSerializer.Serialize(s))!;
Check(restored.Day == s.Day && restored.Harvests == s.Harvests && restored.Next() == s.Next(), "save roundtrip preserves unfinished job and production");
restored.FinishWork(); var count = restored.Harvests; restored.FinishWork();
Check(restored.Harvests == count && restored.Next() == null, "finishing an already finished day is idempotent");
Check(!Settlement.CanTick(true,false,false,false) && !Settlement.CanTick(true,true,true,false) && !Settlement.CanTick(true,true,false,true), "client paused menu and save freeze work");
Check(Settlement.CanTick(true,true,false,false) && !Settlement.CanTick(false,true,false,false), "only a ready host ticks");
var walker = new ResidentMotion(0,0);
Check(!walker.Step(64,0,true) && walker.X == 2 && walker.Facing == 1, "resident walks toward work before applying it");
for(int i=0;i<31;i++) walker.Step(64,0,true);
Check(walker.X == 64 && walker.WorkTicks == 0, "arrival does not instantly finish work");
bool animated = true;
for(int i=0;i<83;i++) animated &= !walker.Step(64,0,true);
Check(animated, "work animation remains in progress for first 83 ticks");
Check(walker.Step(64,0,true), "work completes after 84 visible work ticks");
Check(!walker.Step(64,64,false) && walker.Y == 2 && walker.WorkTicks == 0, "evening walk cancels working animation");
var inputPolicy = typeof(Settlement).GetMethod("ShouldSuppressInput");
Check(inputPolicy != null, "display-only input policy exists");
bool Suppress(bool here, bool action, bool tool) => (bool)inputPolicy!.Invoke(null, new object[] { here, action, tool })!;
Check(Suppress(true,true,false) && Suppress(true,false,true) && Suppress(true,true,true), "all hollow action and tool inputs suppressed including held inputs");
Check(!Suppress(false,true,true) && !Suppress(true,false,false), "outside inputs and non-actions unaffected");
var map = System.Xml.Linq.XDocument.Load(Path.Combine(AppContext.BaseDirectory, "../../../../BrawnieHollow/assets/hollow.tmx"));
var buildings = map.Root!.Elements("layer").Single(e => (string?)e.Attribute("name") == "Buildings").Element("data")!.Value.Split(',').Where(v => !string.IsNullOrWhiteSpace(v)).Select(int.Parse).ToArray();
var routeMethod = typeof(ResidentMotion).GetMethod("EveningTarget");
foreach (var start in Enumerable.Range(0,12).Select(i => (i:0,x:9+i%4,y:15+i/4)).Concat(new (int i,int x,int y)[] { (0,15,17),(1,22,13),(1,20,13),(2,25,20),(2,25,18) }))
{
    var actor = new ResidentMotion(start.x*64,start.y*64);
    for (int tick=0;tick<3000;tick++)
    {
        var waypoint = routeMethod == null ? ((float)(17+start.i*2)*64,12f*64) : ((float,float))routeMethod.Invoke(null,new object[] {start.i,actor.X,actor.Y})!;
        actor.Step(waypoint.Item1,waypoint.Item2,false);
        CheckRoute(buildings[(int)(actor.Y/64)*36+(int)(actor.X/64)] == 0, $"resident {start.i} from {start.x},{start.y} crosses Buildings at {actor.X/64},{actor.Y/64}");
    }
    CheckRoute(actor.X == (17+start.i*2)*64 && actor.Y == 12*64, "evening route reaches hearth");
}
Console.WriteLine("PASS: every authored evening route avoids actual TMX Buildings and reaches hearth");
var validate = typeof(Settlement).GetMethod("Validate");
Check(validate != null, "loaded-state validation exists");
void Valid(Settlement v, int today) => validate!.Invoke(v,new object[] {today});
bool Rejected(Settlement v, int today) { try { Valid(v,today); return false; } catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is InvalidDataException) { return true; } }
Valid(new Settlement(),1); Valid(s,5);
Check(Rejected(new Settlement { Day=-1 },5) && Rejected(new Settlement {Day=6,Plots=s.Plots},5), "negative and future days rejected before catchup");
Check(Rejected(new Settlement {Day=1,Plots=null!},5) && Rejected(new Settlement {Day=1},5) && Rejected(new Settlement {Day=1,Plots=Enumerable.Repeat<Plot>(null!,12).ToList()},5), "null missing and null-entry plots rejected");
Check(Rejected(new Settlement {Day=0,Plots=s.Plots},5) && Rejected(new Settlement {Day=1,Season="invalid",Plots=s.Plots},5), "inconsistent initial state and season rejected");
Check(Rejected(new Settlement {Day=1,Plots=s.Plots},10000), "excessive catchup rejected instead of blocking game loop");
Check(Rejected(new Settlement {Day=1,Harvests=-1,Plots=s.Plots},5), "invalid counters rejected");
var recovery = typeof(Settlement).GetMethod("RecoverItems");
Check(recovery != null, "durable recovery helper exists");
void Recover(List<object> source, Action<object> store) => recovery!.MakeGenericMethod(typeof(object)).Invoke(null,new object[] {source,store});
var dropped = new object(); var debrisItems = new List<object> {dropped}; var durable = new List<object>();
Recover(debrisItems,durable.Add); Recover(debrisItems,durable.Add);
Check(debrisItems.Count == 0 && durable.Count == 1 && ReferenceEquals(durable[0],dropped), "recovery transfers exact item once without cloning or stack loss");
debrisItems.Add(dropped);
try { Recover(debrisItems,_ => throw new IOException("store failed")); } catch (System.Reflection.TargetInvocationException) { }
Check(debrisItems.Count == 1, "failed durable storage retains source item for retry");
var boundary = typeof(Settlement).GetMethod("LeaveSafely");
Check(boundary != null, "synchronous boundary safety helper exists");
var order = new List<string>();
boundary!.Invoke(null,new object[] { (Action)(() => {order.Add("recover");throw new IOException();}), (Action)(() => {order.Add("cleanup");throw new IOException();}), (Action)(() => order.Add("relocate")), (Action<Exception>)(_ => {}) });
Check(order.SequenceEqual(new[] {"recover","cleanup","relocate"}), "recovery and cleanup failures cannot skip synchronous relocation");
var cancelPolicy = typeof(Settlement).GetMethod("ShouldCancelWarp");
Check(cancelPolicy != null, "pending warp ownership policy exists");
bool Cancel(object? request,object? owned,object? destination,object? village) => (bool)cancelPolicy!.Invoke(null,new[] {request,owned,destination,village})!;
var ownRequest = new object(); var village = new object(); var unrelated = new object();
Check(Cancel(ownRequest,ownRequest,unrelated,village) && Cancel(unrelated,ownRequest,village,village), "cancel exact owned outbound and inbound village requests");
Check(!Cancel(unrelated,ownRequest,unrelated,village) && !Cancel(null,null,null,null) && !Cancel(unrelated,null,null,null), "never cancel unrelated or absent pending warp");
Console.WriteLine("ALL ASSERTIONS PASSED");
static void CheckRoute(bool ok, string text) { if (!ok) throw new Exception("FAIL: "+text); }
