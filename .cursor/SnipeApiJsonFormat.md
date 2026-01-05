# Snipe `/code/meta` JSON Format

The `/api/v1/project/{projectID}/code/meta` route returns a single JSON document that external code generators can consume. Unless stated otherwise, every list is sorted alphabetically by the builder before serialization.

```json
{
  "projectID": 7,
  "projectStringID": "test_dev",
  "projectName": "Test Project",
  "snipeVersion": "8.0",
  "generatedAt": "2025-12-17T20:13:37Z",
  "timeZone": "+0300",
  "modules": [],
  "types": [],
  "userAttributes": [],
  "gameVars": [],
  "tables": [],
  "mergeableRequests": []
}
```

## Root Fields

| Field | Type | Notes |
| --- | --- | --- |
| `projectID` | `int` | Numeric project ID. |
| `projectStringID` | `string` | Schema/database name (`test_dev`, `iracing_dev`, …). |
| `projectName` | `string` | Display name from `Projects.Name`. |
| `snipeVersion` | `string` | Currently hard-coded to `"8.0"`. |
| `generatedAt` | `RFC3339 time` | UTC timestamp of payload generation. |
| `timeZone` | `RFC3339 time zone` | numeric UTC offset. |
| `modules` | `MetagenModule[]` | Combined kit (Action/Room/etc.) and service modules. |
| `types` | `MetagenType[]` | Reusable type definitions collected from kit typedefs, script types, inline `Classes:` blocks, and inferred JSON objects. |
| `userAttributes` | `MetagenUserAttribute[]` | Client-visible user attribute metadata. |
| `gameVars` | `MetagenGameVar[]` | Custom game variables plus inferred schemas. |
| `tables` | `MetagenTable[]` | Public kit tables. |
| `mergeableRequests` | `MetagenMergeableRequest[]` | Request descriptors that the Edit UI is allowed to merge in queues. |

## Modules

```json
{
  "name": "Action",
  "hasResponses": true,
  "methods": [MetagenMethod],
  "responses": [MetagenResponse]
}
```

* `name` – module name (Action, Room, PaymentApple, Clan, Chat, Proxy, etc.).
* `hasResponses` – `true` when the module exposes at least one response.
* `methods` – sorted by `callName`. Empty arrays are omitted for modules without methods (i.e., modules that only define responses will only list `responses`).
* `responses` – sorted by `callName`.

### Methods

```json
{
  "callName": "RoomJoin",
  "messageType": "room.join",
  "doc": ["Line 1", "Line 2"],
  "inputs": [MetagenField],
  "outputs": [MetagenField],
  "errorCodes": [
    "`ok` - Operation successful."
  ],
  "actionID": "room.join",
  "mergeable": true,
  "scriptID": "MyScript",
  "notes": ["optional extra info"]
}
```

* `callName` – PascalCase identifier derived from the message type (dots and slashes removed). `kit/` prefixes are stripped.
* `messageType` – actual server opcode (e.g., `action.run`, `room.event`, `matchmaking.add`, `proxy.kick`).
* `doc` – an array of non-empty documentation lines (blank when unavailable).
* `inputs` / `outputs` – arrays of `MetagenField` objects (omitted when empty). Inputs describe request arguments; outputs describe response payloads.
* `errorCodes` – doc lines from the “Error codes” block.
* `actionID` – when backed by a kit action or script function, holds the `StringID`.
* `mergeable` – `true` if the action can be merged (populates `mergeableRequests` too).
* `scriptID` – present when the source is a script function.
* `notes` – reserved for additional hints (currently unused).

#### Special Cases

* `MatchmakingAdd` in the `Matchmaking` module automatically appends the argument list from the `matchmakingInit` script (if available) so the generator sees the exact merge of kit doc params and the script-defined fields.
* `RoomJoin` in the `Room` module always exposes a numeric `roomID` output plus the full set of fields returned by the `roomJoin` script’s return type (or kit action fallback).
* Script functions/responses override legacy kit actions sharing the same `actionID`.
* When both script and kit definitions exist for the same public trigger, non-public script functions are skipped but still cached for the special cases above.
* Only kit actions with triggers `client.self` and `room.event` are published; other triggers stay private and are not converted into modules.

### Responses

```json
{
  "msgType": "room.event",
  "callName": "RoomUserJoined",
  "doc": [],
  "fields": [MetagenField]
}
```

`msgType` contains the full message name, `callName` mirrors the PascalCase derived name, and `fields` describe response payloads.

## Fields

```json
{
  "name": "list",
  "type": "array",
  "itemType": "LeaderboardRecord",
  "optional": true,
  "description": "Optional description."
}
```

* Types are normalized: `String`, `Int`, `Float` → `string`, `int`, `float`; `Bool` → `boolean`; bare `json` → `object`; `json:Foo` → `Foo`. Unknown tokens are preserved verbatim.
* Arrays are represented with `type: "array"` and `itemType` set to the element type (respecting nested type conversions). List/Array generics and `[]` suffixes are supported.
* Titles/tags in docs such as `* \`ackID\` - \`Int\`. Message ID.` become `{"name":"ackID","type":"int","description":"Message ID."}` – any leading punctuation (e.g., “-”, “:”, “(optional)”) is stripped while leaving “(optional)” intact if it starts the sentence.
* `optional` is `true` when `(optional)` appears in the source documentation.

## Types

```json
{
  "name": "ClanChatMessage",
  "fields": [MetagenField]
}
```

Sources include:
* Kit typedefs (`KitTypeDefinitions`).
* Script-defined types (`metaInfo.Types`).
* Inline `Classes:` doc blocks in Haxe XML or service `.txt` files (each `* \`Name\` - \`{ field: Type }\`` bullet becomes a `MetagenType`).
* GameVar inferred JSON objects (deduplicated by `StringID`).

## User Attributes

```json
{
  "stringID": "level",
  "type": "int",
  "name": "Level",
  "clientRead": "public",
  "clientWrite": "none"
}
```

Only attributes with `ClientRead <> 'internal'` are included, sorted by `stringID`.

## Game Variables

```json
{
  "stringID": "cards.unlockPrice",
  "type": "json",
  "property": "cardsUnlockPrice",
  "resolvedType": "MatchmakingInitRetType",
  "fields": [MetagenField]
}
```

* `property` duplicates `stringID` unless the ID contains dots, in which case the segments after the first are capitalized (e.g., `cards.unlockPrice` → `cardsUnlockPrice`). This mirrors how client SDKs expose strongly-typed helpers.
* `resolvedType` – when a JSON variable matches a known `KitTypeDefinition` or when `GameVar.InferTypeFromValue()` returns a simple scalar type.
* `fields` – present when the inferred type is complex; the same definition is also pushed into the root `types` array to avoid duplicates.

## Tables

```json
{
  "stringID": "leaderboard",
  "itemClass": "LeaderboardItem",
  "fields": [
    {"id": "id", "name": "ID", "type": "int"},
    {"id": "score", "name": "Score", "type": "float", "isAttr": true}
  ]
}
```

Only tables marked public in `KitTableTypes.Params` are included. The legacy `public` boolean is intentionally omitted from the output; consumers should assume every listed table is already public.

## Mergeable Requests

```json
{
  "messageType": "action.run",
  "payload": {
    "actionID": "garage.upgrade"
  }
}
```

The builder pre-populates this list with:
* Static request types from `codeMergeableRequestTypes`.
* User actions/room events whose kit or script metadata declares `isMergeable = true`.

## Serialization Rules

* JSON is encoded via `github.com/segmentio/encoding/json` and formatted by `json.MarshalIndent(payload, "", "  ")`.
* Output order is deterministic: modules sorted by `name`, methods/responses sorted by `callName`, and other collections sorted by `StringID` or type name (as appropriate).
* Scripts override kit actions/responses with identical `actionID`s.
* Only triggers `client.self` and `room.event` are considered public; other kit triggers are filtered out entirely.
* Script functions with triggers outside the public set are still recorded when needed for special cases (matchmaking init, room join) even though their methods are not published.
* Inline `Classes:` doc parsing handles nested comma/angle brackets and optional fields; classes defined in both Haxe XML and service text files are consolidated into `types`.

This document describes the current contract for `SnipeApi.json`; updates to the metagen builder should keep this file in sync.
