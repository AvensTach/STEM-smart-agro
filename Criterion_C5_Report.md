# Criterion C5: Database Integrity & Security Report
**Project:** SmartAgro Monitor

## 1. Data Payload Validation
To prevent anomalous data injection (e.g., faulty sensor outputs or malicious requests), data annotations (`[Range]`, `[Required]`) were implemented on the shared `SensorReading` DTO. The `MicroclimateController` utilizes `ModelState.IsValid` to intercept and reject payloads outside realistic physical bounds (-40°C to +80°C) with an HTTP 400 Bad Request response.

## 2. Transactional Integrity (ACID)
The `DataArchiverService` implements downsampling (converting raw data to hourly averages). To ensure atomicity and prevent data duplication or deletion failures during unexpected server reboots, the entire transfer process is wrapped in an explicit `BeginTransactionAsync` execution block. If `SaveChangesAsync` fails, `RollbackAsync` ensures the database remains in its original state.

## 3. Query Performance Optimization (Indexing)
Because the application routinely queries temporal data (e.g., `Where(r => r.CreatedAt < cutoffDate)`), B-Tree database indices were configured via EF Core's Fluent API (`HasIndex`) for the `CreatedAt` and `SensorId` columns. This reduces the time complexity of historical lookups from O(N) linear scans to O(log N).