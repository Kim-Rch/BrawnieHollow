# Installed API inspection for review fixes

Inspected primary installed assemblies with ILSpy 9.1.0.7988 (temporary local tool), then checked signatures in `smoke/Program.cs` against the installed assemblies.

Source: `F:/Steam/steamapps/common/Stardew Valley/Stardew Valley.dll` and `StardewModdingAPI.dll`.

- `IInputHelper.Suppress(SButton)` documentation explicitly says suppression does not prevent other mods from receiving events, and a held button stays suppressed until release or `Press`. `IsDown` and `IsSuppressed` exist. This permits explicit resident/sign handling after suppression. `ButtonsChanged` and `UpdateTicking` also suppress held actions when entering the map.
- `Debris(Item, Vector2)` stores the original Item, calls InitializeItem, and creates one chunk. `Debris.collect` takes that original Item (whole stack), clearing its reference only while transferring. Resource/object/archaeology debris without an Item represents one item per remaining chunk, with `itemQuality`. Cosmetic/coin chunks must not be materialized as inventory items.
- `SaveGame` has `List<Item> returnedDonations`; its save initializer copies `Game1.player.team.returnedDonations.ToList()`, and its load path clears/repopulates the team collection. `FarmerTeam.CheckReturnedDonations` exposes these items for zero-cost recovery. Thus returnedDonations, unlike transient location debris, has an explicit vanilla save/load path.
- Stack retrieval was also checked in installed `ShopMenu.tryToPurchaseItem`: it computes the delivered stack as `stockToBuy * item.Stack` and assigns that stack to the purchased instance. Lost-and-found stock is one bundle, so storing the original full stack does not reduce recovery to a single item.
- `FarmerTeam` has `newLostAndFoundItems` (NetBool), matching vanilla's own returned-donation insertion pattern.
- `Game1.performWarpFarmer` stores public static `locationRequest`, sets protected static `_isWarping = true`, records the destination, and queues `fadeScreenToBlack()`. The fade completion later consumes the request and clears `_isWarping`. Therefore merely relocating synchronously without clearing a matching pending request can warp the player back into a discarded village.
- `LocationRequest` exposes `Location` and `Name`. Cancellation checks exact owned request identity or exact transient-location identity, never a general pending-warp flag. Only when such a request is cancelled are `_isWarping`, `fadeToBlack`, `fadeIn`, `fadeToBlackAlpha`, and CanMove restored. Unrelated requests/fades are left intact.
- `DataHelper.WriteSaveData` serializes the supplied model before assigning to save-data dictionaries, so serialization/validation failure does not first delete the old serialized value. Mod Saving validates before calling it, catches failures, disables subsequent writes, and still invokes synchronous safety return.

Verification limits: these are installed-code inspection and headless signature checks, not graphical gameplay tests. The parent runtime harness owns graphical drop/interaction/save regressions and was not modified.
