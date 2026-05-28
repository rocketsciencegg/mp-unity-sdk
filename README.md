# Multiplay By Rocket Science Unity Engine SDK

Unity package for integrating with [Multiplay Hosting](https://docs.multiplay.dev/), Rocket Science's scalable game server hosting platform. Handles server lifecycle, payload allocation, real-time server events via WebSocket, server query protocol (SQP), and Quality of Service (QoS) measurements.

**Package:** `gg.rocketscience.services.multiplay`
**Unity:** 2021.3+
**Version:** 2.0.0
**Documentation:** [docs.multiplay.dev](https://docs.multiplay.dev/)
**Dashboard:** [dashboard.multiplay.dev](https://dashboard.multiplay.dev/)

---

## Table of Contents

- [Installation](#installation)
- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
- [API Reference](#api-reference)
- [Configuration](#configuration)
- [Quality of Service (QoS)](#quality-of-service-qos)
- [Advanced Usage](#advanced-usage)
- [Troubleshooting](#troubleshooting)
- [Further Reading](#further-reading)

---

## Installation

### Option A: Unity Package Manager (Recommended)

1. Open your Unity project
2. Go to **Window > Package Manager**
3. Click the **+** button in the top-left corner
4. Select **Install Package from Git URL**
5. Paste the following URL:

```
https://github.com/rocketsciencegg/mp-unity-sdk.git
```

6. Click **Install**

### Option B: Manual (package.json)

Add this line to your project's `Packages/manifest.json` under `dependencies`:

```json
{
  "dependencies": {
    "gg.rocketscience.services.multiplay": "https://github.com/rocketsciencegg/mp-unity-sdk.git"
  }
}
```

Save the file. Unity will download and install the package automatically.

---

## Prerequisites

This SDK depends on the following Unity packages (installed automatically):

| Package | Version | Purpose |
|---------|---------|---------|
| `com.unity.services.core` | 1.12.5 | Unity Services initialization |
| `com.unity.services.wire` | 1.4.0 | WebSocket / real-time messaging |
| `com.unity.transport` | 2.6.0 | Network transport (QoS) |
| `com.unity.nuget.newtonsoft-json` | 3.0.2 | JSON serialization |

You must initialize Unity Services before using the SDK:

```csharp
using Unity.Services.Core;

await UnityServices.InitializeAsync();
```

---

## Quick Start

This section walks through getting a game server online and listening for allocations. If you're new to Multiplay, start here.

### Step 1: Initialize Unity Services

```csharp
using Unity.Services.Core;
using RocketScience.Services.Multiplay;

async void Start()
{
    await UnityServices.InitializeAsync();
}
```

### Step 2: Access the Multiplay Service

```csharp
IMultiplayService multiplayService = MultiplayService.Instance;
```

The SDK reads connection info from a `server.json` file that Multiplay provides to your game server at runtime. You don't need to configure the connection manually.

### Step 3: Read Server Configuration

```csharp
ServerConfig config = multiplayService.ServerConfig;

Debug.Log($"Server ID: {config.ServerId}");
Debug.Log($"Allocation ID: {config.AllocationId}");
Debug.Log($"IP: {config.IpAddress}");
Debug.Log($"Port: {config.Port}");
Debug.Log($"Query Port: {config.QueryPort}");
```

### Step 4: Mark Server as Ready

Tell Multiplay your server is ready to accept players:

```csharp
await multiplayService.ReadyServerForPlayersAsync();
```

### Step 5: Subscribe to Server Events

Listen for allocation and deallocation events over WebSocket:

```csharp
var callbacks = new MultiplayEventCallbacks();

callbacks.Allocate += (allocation) =>
{
    Debug.Log($"Allocated! ID: {allocation.AllocationId}, Server: {allocation.ServerId}");
    // Load map, configure match, etc.
};

callbacks.Deallocate += (deallocation) =>
{
    Debug.Log($"Deallocated. Server: {deallocation.ServerId}");
    // Clean up, return to idle state
};

callbacks.Error += (error) =>
{
    Debug.LogError($"Multiplay error: {error.Reason} - {error.Detail}");
};

callbacks.SubscriptionStateChanged += (state) =>
{
    Debug.Log($"Subscription state: {state}");
};

IServerEvents serverEvents = await multiplayService.SubscribeToServerEventsAsync(callbacks);
```

### Step 6: Get Payload Data

Retrieve the allocation payload (custom data passed during allocation). Payloads are sent via the Multiplay Hosting allocation API and are available to the game server through the local payload proxy at port 8086.

> **Note:** Payloads cannot exceed 30 KB. They are temporary and tied to the allocation lifecycle -- once deallocated, the payload is no longer available.

```csharp
// As plain text
string payloadText = await multiplayService.GetPayloadAllocationAsPlainText();

// As a typed object from JSON
MyMatchConfig payload = await multiplayService.GetPayloadAllocationFromJsonAs<MyMatchConfig>();
```

### Step 7: Start the Server Query Handler (SQP)

Expose server info so Multiplay can monitor your server:

```csharp
IServerQueryHandler queryHandler = await multiplayService.StartServerQueryHandlerAsync(
    maxPlayers: 16,
    serverName: "My Game Server",
    gameType: "deathmatch",
    buildId: "v1.0.0",
    map: "dust2"
);

// Update player count as players join/leave
queryHandler.CurrentPlayers = 5;

// Call periodically (e.g., in Update loop or on a timer)
queryHandler.UpdateServerCheck();
```

---

## Core Concepts

### ServerConfig

Multiplay Hosting injects a `server.json` into your game server environment at `$HOME/server.json` (Linux). The SDK reads it automatically and exposes it as `ServerConfig`. The raw `server.json` contains built-in configuration variables like `allocatedUUID`, `serverID`, `port`, `ip`, `queryPort`, `queryType`, `fleetID`, `regionID`, `regionName`, `serverLogDir`, and any custom variables you define in your build configuration.

| Property | Type | Description |
|----------|------|-------------|
| `ServerId` | `long` | Unique server identifier |
| `AllocationId` | `string` | Current allocation UUID (empty string when unallocated) |
| `QueryPort` | `ushort` | Port for SQP queries |
| `Port` | `ushort` | Game port for player connections |
| `IpAddress` | `string` | Server IP address |
| `ServerLogDirectory` | `string` | Path for server logs |

### Server Lifecycle

```
Server starts -> Initialize Unity Services -> ReadyServerForPlayersAsync()
    -> Wait for allocation event -> Handle players -> UnreadyServerAsync()
    -> Wait for deallocation -> Clean up -> Loop or shut down
```

Deallocation happens when:
- Your matchmaker calls the deallocation API
- The game server process exits cleanly (exit code 0) -- **recommended for most games**
- The allocation TTL expires (fleet-level setting, default 1 hour)

### Subscription States

When subscribed to server events, the connection can be in these states:

| State | Meaning |
|-------|---------|
| `Unsubscribed` | Not connected |
| `Subscribing` | Connection in progress |
| `Synced` | Connected and receiving events |
| `Unsynced` | Temporarily disconnected, will retry |
| `Error` | Connection failed |

---

## API Reference

### IMultiplayService

The primary interface. Access via `MultiplayService.Instance`.

```csharp
public interface IMultiplayService
{
    ServerConfig ServerConfig { get; }

    Task ReadyServerForPlayersAsync();
    Task UnreadyServerAsync();

    Task<string> GetPayloadAllocationAsPlainText();
    Task<TPayload> GetPayloadAllocationFromJsonAs<TPayload>(bool throwOnMissingMembers = false);

    Task<IServerEvents> SubscribeToServerEventsAsync(MultiplayEventCallbacks callbacks);

    Task<IServerQueryHandler> StartServerQueryHandlerAsync(
        ushort maxPlayers, string serverName, string gameType, string buildId, string map);
}
```

### IServerEvents

Returned from `SubscribeToServerEventsAsync`. Manage the event subscription:

```csharp
IServerEvents events = await multiplayService.SubscribeToServerEventsAsync(callbacks);

// Later, to unsubscribe:
await events.UnsubscribeAsync();

// To resubscribe:
await events.SubscribeAsync();
```

### IServerQueryHandler

Returned from `StartServerQueryHandlerAsync`. Update server info for Multiplay monitoring:

| Property | Type | Description |
|----------|------|-------------|
| `MaxPlayers` | `ushort` | Max player capacity |
| `CurrentPlayers` | `ushort` | Current player count |
| `ServerName` | `string` | Server display name |
| `GameType` | `string` | Game mode identifier |
| `BuildId` | `string` | Build version string |
| `Map` | `string` | Current map name |
| `Port` | `ushort` | Game port |

Call `UpdateServerCheck()` periodically to push updates.

---

## Configuration

The SDK uses a `Configuration` class for controlling HTTP behavior on the underlying API clients. Most users won't need this — the high-level `IMultiplayService` API handles configuration automatically. This section is relevant if you're working with the lower-level API clients directly.

### Configuration Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BasePath` | `string` | `"http://localhost"` | Base URL for API requests |
| `RequestTimeout` | `int?` | `10` | Request timeout in seconds |
| `NumberOfRetries` | `int?` | `4` | Number of retry attempts |
| `Headers` | `IDictionary<string, string>` | `null` | Additional request headers |

### Configuration Levels

Configuration can be set at three levels. Each level overrides the one above it:

**Global** (automatic defaults) -> **API** (per-client) -> **Request** (per-call)

#### Global Configuration

Applied automatically with the defaults shown above.

#### API-Level Configuration

Pass a `Configuration` into an API client constructor to override global defaults for all requests made by that client:

```csharp
var config = new Configuration(
    basePath: "http://localhost:9090",
    requestTimeout: 30,
    numRetries: 2,
    headers: null
);

var gameServerClient = new GameServerApiClient(httpClient, accessToken, config);
```

#### Request-Level Configuration

Pass a `Configuration` into any API method to override settings for that single request:

```csharp
var requestConfig = new Configuration(
    basePath: null,
    requestTimeout: 60,  // longer timeout for this call
    numRetries: 1,
    headers: null
);

await gameServerClient.ReadyServerAsync(request, requestConfig);
```

### Merge Behavior

| Property | Behavior |
|----------|----------|
| `BasePath` | Lower level overrides higher level |
| `RequestTimeout` | Lower level overrides higher level |
| `NumberOfRetries` | Lower level overrides higher level |
| `Headers` | Merged across all levels. Lower level overrides duplicate keys, then unique headers from all levels are combined |

---

## Quality of Service (QoS)

The SDK includes a QoS package for measuring latency and packet loss to Multiplay server regions. Use this on the **game client** to select optimal regions for matchmaking. Under the hood, the SDK queries the QoS discovery service (`https://qos.multiplay.dev/v1/fleets/{fleetId}/servers`) to find active QoS servers, then sends UDP packets to measure connection quality.

### Measure QoS for Fleets

```csharp
using RocketScience.Services.Qos;

IQosService qosService = QosService.Instance;

// Get sorted results for specific fleets (best region first)
IList<IQosAnnotatedResult> results =
    await qosService.GetSortedMultiplayQosResultsAsync(new List<string> { "my-fleet-id" });

foreach (var result in results)
{
    Debug.Log($"Region: {result.Region}");
    Debug.Log($"  Latency: {result.AverageLatencyMs}ms");
    Debug.Log($"  Packet Loss: {result.PacketLossPercent}%");
}
```

### QoS Result Properties

| Property | Type | Description |
|----------|------|-------------|
| `Region` | `string` | Server region identifier |
| `AverageLatencyMs` | `int` | Average round-trip latency in milliseconds |
| `PacketLossPercent` | `float` | Percentage of packets lost (0-100) |
| `Annotations` | `Dictionary<string, List<string>>` | Additional metadata |

---

## Advanced Usage

### Reservations (Server Browser Model)

If your game uses a server browser instead of a matchmaker, you can use the reservation system. The game server calls the Reserve API to mark itself as in-use when players connect directly, and the Unreserve API when the session ends. This helps Multiplay Hosting make correct scaling decisions without requiring a matchmaker.

The SDK exposes this through `ReservationApiClient` for lower-level control.

### Working with OneOf Types

Some API responses use `IOneOf` to represent polymorphic types — a response that could be one of several types. Check the `Type` property and cast accordingly:

```csharp
if (response.Result.Type == typeof(ExpectedModelType))
{
    ExpectedModelType result = (ExpectedModelType)response.Result.Value;
}
```

This pattern also applies to error handling with typed exceptions:

```csharp
try
{
    var response = await apiClient.SomeOperationAsync(request);
}
catch (HttpException<SpecificErrorType> e)
{
    // Handle specific error type
}
catch (HttpException<RateLimitError> e)
{
    // Handle rate limit
}
```

### Unready a Server

Temporarily remove your server from the allocation pool:

```csharp
await multiplayService.UnreadyServerAsync();

// Do maintenance, update config, etc.

await multiplayService.ReadyServerForPlayersAsync();
```

### Full Server Lifecycle Example

```csharp
using Unity.Services.Core;
using RocketScience.Services.Multiplay;

public class GameServer : MonoBehaviour
{
    IMultiplayService m_Multiplay;
    IServerQueryHandler m_QueryHandler;
    IServerEvents m_ServerEvents;

    async void Start()
    {
        await UnityServices.InitializeAsync();

        m_Multiplay = MultiplayService.Instance;

        // Start SQP handler
        m_QueryHandler = await m_Multiplay.StartServerQueryHandlerAsync(
            maxPlayers: 16,
            serverName: "My Server",
            gameType: "ranked",
            buildId: "1.0.0",
            map: "arena"
        );

        // Subscribe to events
        var callbacks = new MultiplayEventCallbacks();
        callbacks.Allocate += OnAllocate;
        callbacks.Deallocate += OnDeallocate;
        callbacks.Error += OnError;

        m_ServerEvents = await m_Multiplay.SubscribeToServerEventsAsync(callbacks);

        // Signal ready
        await m_Multiplay.ReadyServerForPlayersAsync();
    }

    void Update()
    {
        m_QueryHandler?.UpdateServerCheck();
    }

    async void OnAllocate(MultiplayAllocation allocation)
    {
        var matchConfig = await m_Multiplay.GetPayloadAllocationFromJsonAs<MatchConfig>();
        // Set up match based on payload
    }

    void OnDeallocate(MultiplayDeallocation deallocation)
    {
        // Clean up match, reset server state
    }

    void OnError(MultiplayError error)
    {
        Debug.LogError($"[Multiplay] {error.Reason}: {error.Detail}");
    }

    async void OnDestroy()
    {
        if (m_ServerEvents != null)
            await m_ServerEvents.UnsubscribeAsync();
    }
}
```

---

## Troubleshooting

### "server.json not found" exception

The SDK expects Multiplay Hosting to provide a `server.json` file at runtime in `$HOME/server.json`. This file only exists when running on Multiplay infrastructure. For local development, create a mock `server.json` with the required fields:

```json
{
  "allocatedUUID": "",
  "serverID": "12345",
  "port": "9000",
  "queryPort": "9010",
  "ip": "127.0.0.1",
  "queryType": "sqp",
  "serverLogDir": "./logs/"
}
```

### WebSocket connection failures

The SDK connects to the local Multiplay daemon at `ws://127.0.0.1:8086`. Ensure:
- Your server is running on Multiplay infrastructure
- Port 8086 is not blocked
- The Multiplay daemon (sdkdaemon) is running

### Subscription state stuck on "Unsynced"

This usually indicates a temporary network issue. The SDK will retry automatically. If it persists, check your server's network configuration and ensure the Multiplay daemon is healthy.

### Server marked as "unresponsive"

If your build configuration specifies a query protocol (SQP), Multiplay Hosting polls your server for health data. If SQP stops responding, the server is marked unresponsive and may be restarted. Make sure `UpdateServerCheck()` is called regularly and that your server binds the query port to `0.0.0.0`.

---

## Further Reading

- [Multiplay Customer Docs](https://docs.multiplay.dev/) -- Full platform documentation
- [Multiplay Dashboard](https://dashboard.multiplay.dev/) -- Manage builds, fleets, and servers
- [Multiplay API](https://api.multiplay.dev/) -- REST API for allocations, builds, and fleet management

---

## License

See [LICENSE](LICENSE) for details.
