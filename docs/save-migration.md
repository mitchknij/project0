# Save Migration Contract

`SaveManager.CurrentSaveSchemaVersion` is **4**. The persisted `SaveData` envelope contains `SaveSchemaVersion`, `ContentVersion`, `Account`, `SelectedCharacterId`, `WorldKills`, and `SessionRevision`. Newtonsoft JSON persists field names rather than CLR namespaces, so the move of the state contracts into the Data assembly did not change their stored names.

Schema **v1** is the oldest supported compatibility baseline. The fixture at `Assets/Scripts/Tests/EditMode/Fixtures/idlecloud-save-v1.json` records its legacy account shape. Current saves and migrated saves continue to use the existing `idlecloud-save-v1.json` filename; the filename is not the schema version.

## Load and recovery

- Saving writes the v4 envelope to a temporary file, then replaces or moves it into place.
- A missing path, malformed JSON, or an envelope without an account returns `null`; callers retain control of recovery.
- In Editor and development builds, loading a pre-v4 envelope creates one `*.pre-vN.bak` copy before migration when that backup does not already exist.
- After migration, invalid character map IDs are reset to `MapsRepo.StartingMapId`.

## Migration path

- v1 saves receive snapshot metadata defaults introduced for v2.
- Saves older than v3 with no skill bar receive the class default bar.
- Every load idempotently normalizes legacy collections, snapshot metadata, skill-bar length/content, and skill-build state. It then stamps schema v4 and the current content version.
- Re-running migration on already-migrated data must not change the serialized envelope.

`SaveCompatibilityTests` covers the legacy fixture, a v1-to-v4 migration, current-v4 filesystem round trip, missing/corrupt paths, malformed skill bars, and repeated-migration idempotence. Tests use temporary paths and never read or overwrite the player's `Application.persistentDataPath` save.
