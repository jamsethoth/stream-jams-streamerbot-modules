# Local HTTP And WebSocket API

Source docs:
- https://docs.streamer.bot/api/http
- https://docs.streamer.bot/api/http/requests/get-actions
- https://docs.streamer.bot/api/http/requests/do-action
- https://docs.streamer.bot/api/websocket
- https://docs.streamer.bot/api/websocket/requests

## Capability Boundary

Treat the local API as runtime control and inspection unless current docs prove otherwise.

Supported by docs:

- HTTP `GET /GetActions`: list action IDs and names.
- HTTP `POST /DoAction`: trigger an existing action by ID or name with optional args.
- WebSocket `GetActions`: list actions with enabled/group/subaction/trigger metadata.
- WebSocket `DoAction`: trigger an existing action by ID or name with optional args.
- WebSocket `Subscribe`: subscribe to events after connecting.
- WebSocket `GetEvents`: discover event categories and names.

Not clearly documented:

- Creating actions.
- Editing action sub-actions.
- Creating commands.
- Adding triggers.

For creation, use UI instructions or investigate Import/Export separately.

## HTTP Examples

List actions:

```bash
curl -X GET http://127.0.0.1:7474/GetActions
```

Run an action by name:

```bash
curl -X POST http://127.0.0.1:7474/DoAction \
  -H "Content-Type: application/json" \
  -d '{"action":{"name":"My Action"},"args":{"customArg":"customValue"}}'
```

Run by ID when possible:

```json
{
  "action": { "id": "action-guid" },
  "args": {
    "userName": "TestUser",
    "rawInput": "example input"
  }
}
```

## WebSocket Request Shapes

Base request:

```json
{
  "request": "GetActions",
  "id": "request-id"
}
```

Trigger action:

```json
{
  "request": "DoAction",
  "id": "run-test-1",
  "action": { "name": "My Action" },
  "args": { "key": "value" }
}
```

Subscribe:

```json
{
  "request": "Subscribe",
  "id": "sub-1",
  "events": {
    "Twitch": ["ChatMessage"],
    "Command": ["Message"]
  }
}
```

## Testing Guidance

- Ask the user whether Streamer.bot HTTP/WebSocket servers are enabled and what port/password/auth settings are configured.
- Use localhost only unless the user explicitly wants remote access.
- Prefer action IDs for tests after listing actions.
- Pass explicit `args` that mimic command variables when testing command-like actions outside chat.
