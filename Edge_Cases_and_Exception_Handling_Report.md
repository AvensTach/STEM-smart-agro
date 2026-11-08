# Edge Cases & Exception Handling Report (Criterion C2)

## System Component
`SmartAgro.IoT/Worker.cs` — Background Telemetry Collector Service.

---

## Summary of Implementations

### 1. Network Fault Tolerance & HTTP Resiliency
* **Scenario:** Backend server is offline, returns HTTP 5xx errors, or times out.
* **Handled Exceptions:** `HttpRequestException`, `TaskCanceledException`.
* **Behavior:** 
  - Catches connection errors without crashing the background worker process.
  - Logs structured warning messages (`LogWarning`) cleanly omitting unhandled call stacks.
  - Continues retry execution during the next schedule interval.

### 2. Microclimate Boundary Checks
* **Scenario:** Sensor generates corrupted/extreme values out of valid physical limits.
* **Rules:**
  - **Temperature Range:** $[-40.0^\circ\text{C}, 80.0^\circ\text{C}]$
  - **Humidity Range:** $[0.0\%, 100.0\%]$
* **Behavior:**
  - Out-of-bounds telemetry payloads are intercepted before calling network services.
  - An event warning is recorded in execution logs for diagnostic visibility.

### 3. Execution File Logging
* **Library:** Serilog (`Serilog.Sinks.File`).
* **Format:** Daily rolling log file (`logs/smartagro-worker-YYYYMMDD.log`).
* **Noise Reduction:** Expected operational failures (e.g., unreachable server) are logged at `Warning` level with clear narrative strings, preventing log pollution from unnecessary Exception Stack Traces.