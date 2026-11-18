# Dialogue input regression

The village previously suppressed action/tool buttons before checking whether a dialogue or menu was open. This swallowed the clicks needed to dismiss resident dialogue. Both the button-press and held-input paths now bypass menus/dialogue, while normal village field input remains protected.

Verification:

- Added a policy regression and observed it fail before the fix, then pass.
- Reproduced the old DLL in the isolated copied game runtime: Bram's dialogue opened, but MouseLeft was suppressed.
- Retested the fixed DLL in that same isolated runtime with all three residents. The real loaded ButtonPressed handler left MouseLeft, MouseRight, Space, and ControllerA unsuppressed and did not reopen the dialogue.
- Called each actual DialogueBox's vanilla receiveLeftClick handler until it closed, then asserted dialogueUp was false and player movement was restored. No forced menu cleanup was used.
- Verified the normal return warp after all three conversations and checked the owned SMAPI log for errors (none).
- This exercises real game objects and mod handlers, not physical mouse/keyboard/controller event delivery. The held-input handler was invoked, but physical held keys were not simulated.
- All simulation/policy, assembly, asset, and exact-package checks passed. Release build had no warnings or errors. The separate runtime test did not save or load the user's saves.

Local runtime harness/evidence: the Hermes scratch workspace `brawnie-runtime-qa`, using `harness/DialogueRegression.cs`, `dialogue-red.jsonl`, and `evidence.jsonl`. Scratch files are temporary; the headless regression is retained in `tests/Program.cs`.
