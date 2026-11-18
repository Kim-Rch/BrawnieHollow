using System.Reflection;
using System.Runtime.Loader;
string game = args.Length > 0 ? Path.GetFullPath(args[0]) : "F:/Steam/steamapps/common/Stardew Valley";
string dll = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.GetFullPath("BrawnieHollow/bin/Release/net6.0/BrawnieHollow.dll");
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    foreach (string dir in new[] { game, Path.Combine(game, "smapi-internal"), Path.GetDirectoryName(dll)! })
    {
        string candidate = Path.Combine(dir, name.Name + ".dll");
        if (File.Exists(candidate)) return context.LoadFromAssemblyPath(candidate);
    }
    return null;
};
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); }
var mod = Assembly.LoadFrom(dll);
var types = mod.GetTypes();
var entry = mod.GetType("BrawnieHollow.ModEntry", true)!;
Check(entry.BaseType?.FullName == "StardewModdingAPI.Mod", "built assembly exposes a real SMAPI mod entry");
Check(entry.GetMethod("Entry")?.GetBaseDefinition().DeclaringType?.FullName == "StardewModdingAPI.Mod", "SMAPI Entry override resolves against installed game API");
Check(Activator.CreateInstance(entry) != null, "mod entry can be constructed with installed assemblies");
Check(types.Length >= 7, "all built mod types resolve without missing runtime dependencies");
var settlement = mod.GetType("BrawnieHollow.Settlement", true)!;
var value = Activator.CreateInstance(settlement)!;
settlement.GetMethod("StartDay")!.Invoke(value, new object[] { 1, "spring" });
settlement.GetMethod("FinishWork")!.Invoke(value, null);
var json = Assembly.Load("Newtonsoft.Json");
var convert = json.GetType("Newtonsoft.Json.JsonConvert", true)!;
string serialized = (string)convert.GetMethod("SerializeObject", new[] { typeof(object) })!.Invoke(null, new[] { value })!;
object restored = convert.GetMethod("DeserializeObject", new[] { typeof(string), typeof(Type) })!.Invoke(null, new object[] { serialized, settlement })!;
Check(restored != null && settlement.GetMethod("Next")!.Invoke(restored, null) == null, "game Newtonsoft serializer roundtrips a fully worked field");
settlement.GetMethod("StartDay")!.Invoke(restored, new object[] { 5, "spring" });
var next = settlement.GetMethod("Next")!.Invoke(restored, null)!;
Check(next.GetType().GetProperty("Kind")!.GetValue(next)!.ToString() == "Harvest", "built DLL simulation grows the restored field to harvest");
Check(entry.GetMethod("RecoverDebris", BindingFlags.Instance | BindingFlags.NonPublic) != null, "real transient debris recovery is wired");
Check(entry.GetMethod("CancelOwnWarp", BindingFlags.Instance | BindingFlags.NonPublic) != null, "own pending-warp cancellation exists");
var gameAssembly = Assembly.Load("Stardew Valley");
var gameType = gameAssembly.GetType("StardewValley.Game1",true)!;
var requestType = gameAssembly.GetType("StardewValley.LocationRequest",true)!;
Check(gameType.GetField("locationRequest")?.FieldType == requestType && requestType.GetField("Location") != null, "installed pending-warp fields resolve");
Check(gameType.GetField("_isWarping",BindingFlags.Static | BindingFlags.NonPublic)?.FieldType == typeof(bool), "installed protected warp flag resolves");
Check(new[] {"fadeToBlack","fadeIn","fadeToBlackAlpha"}.All(n => gameType.GetProperty(n)?.CanWrite == true), "installed fade properties can be reset after cancelling own request");
var inputApi = Assembly.Load("StardewModdingAPI").GetType("StardewModdingAPI.IInputHelper",true)!;
Check(new[] {"Suppress","IsDown","IsSuppressed"}.All(n => inputApi.GetMethod(n) != null), "installed held-input suppression APIs resolve");
var team = gameAssembly.GetType("StardewValley.FarmerTeam",true)!;
Check(team.GetField("returnedDonations") != null && team.GetField("newLostAndFoundItems") != null && gameAssembly.GetType("StardewValley.SaveGame",true)!.GetField("returnedDonations") != null, "durable lost-and-found collection and vanilla save field resolve");
// Inspect actual compiled render calls without requiring a graphics context.
var opcodes = typeof(System.Reflection.Emit.OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
    .Where(f => f.FieldType == typeof(System.Reflection.Emit.OpCode))
    .Select(f => (System.Reflection.Emit.OpCode)f.GetValue(null)!)
    .ToDictionary(o => unchecked((ushort)o.Value));
var render = entry.GetMethod("RenderWorld", BindingFlags.Instance | BindingFlags.NonPublic)!;
var il = render.GetMethodBody()!.GetILAsByteArray()!;
var renderCalls = new List<MethodBase>();
for (int offset = 0; offset < il.Length;)
{
    ushort code = il[offset++];
    if (code == 0xfe) code = (ushort)(0xfe00 | il[offset++]);
    var op = opcodes[code];
    if (op.OperandType == System.Reflection.Emit.OperandType.InlineMethod)
        renderCalls.Add(render.Module.ResolveMethod(BitConverter.ToInt32(il, offset))!);
    offset += op.OperandType switch
    {
        System.Reflection.Emit.OperandType.InlineNone => 0,
        System.Reflection.Emit.OperandType.ShortInlineBrTarget or System.Reflection.Emit.OperandType.ShortInlineI or System.Reflection.Emit.OperandType.ShortInlineVar => 1,
        System.Reflection.Emit.OperandType.InlineVar => 2,
        System.Reflection.Emit.OperandType.InlineI8 or System.Reflection.Emit.OperandType.InlineR => 8,
        System.Reflection.Emit.OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, offset),
        _ => 4
    };
}
Check(renderCalls.Any(m => m.Name == "draw" && m.DeclaringType!.IsAssignableFrom(gameAssembly.GetType("StardewValley.NPC", true)!)), "world rendering still draws resident sprites");
Check(!renderCalls.Any(m => m.Name is "drawTextWithShadow" or "DrawString" or "MeasureString"), "world rendering never draws floating resident name labels, including with old configs");
Console.WriteLine("HEADLESS ASSEMBLY SMOKE PASSED (not a graphical gameplay test)");
