// Storage72hStressTest.cs
// S4-03: 72 小时稳定性与耐久性压测
//
// 策略: C (Combined) — 时间加速仿真 + 存储上限缩放
//
// 等价性说明:
//   72h EEG @ 160Hz × 4ch = 41,472,000 samples = 259,200 one-second chunks
//   活跃会话写入完整 259,200 chunks（= 72h），无拆分。
//   旧会话数据通过直接 SQL INSERT 注入（不占用 ChunkWriter 配额），
//   作为 StorageReaper 的淘汰目标。
//   存储上限缩放至 50 MB，迫使 StorageReaper 反复触发清理。
//   并发 reader 线程在写入 + 删除期间持续 QueryTimeRange。
//
// 覆盖验收标准:
//   AT-20: 72h 数据完整性（完整 259,200 chunks）
//   AT-22: 72h 连续运行（内存增长 < 10%，暖机基线 vs 最终值，forced GC）
//   AT-24: 存储滚动清理（写入不中断、回放稳定）
//   ARCHITECTURE.md §8.6: 72h 连续写入零失败

using System.Diagnostics;
using Neo.Core.Enums;
using Neo.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace Neo.StressTests;

public class Storage72hStressTest : IDisposable
{
    private readonly string _tempDir;
    private readonly ITestOutputHelper _output;

    // ── 72h 等价参数 ──
    private const int SimulatedHours = 72;
    private const int SampleRate = 160;
    private const int ChannelCount = 4;
    private const int ChunkDurationSec = 1;
    private const int SamplesPerChunk = SampleRate * ChunkDurationSec;
    private const int TotalSimulatedSeconds = SimulatedHours * 3600; // 259,200
    private const int TotalChunks72h = TotalSimulatedSeconds / ChunkDurationSec; // 259,200
    private const long TotalSamples72h = (long)TotalSimulatedSeconds * SampleRate; // 41,472,000

    // ── 存储上限缩放 ──
    private const long StorageLimitBytes = 50L * 1024 * 1024; // 50 MB
    private const double CleanupThreshold = 0.8; // trigger at 40 MB

    // ── 旧会话: 直接注入的 reaper 目标 ──
    private const int OldSessionChunks = 30_000; // ~8.3h equiv, injected directly

    // ── 采样间隔 ──
    private const int StatsIntervalChunks = 25_000;

    // ── AT-22 内存指标 ──
    // AT-22 验收标准: "内存增长 < 10%"
    // 测量方式: 取暖机基线（首个采样间隔后强制 GC）与最终强制 GC 值的比值。
    // 暖机基线包含 SQLite 连接、prepared statements 等初始分配；
    // 最终值应与暖机基线接近或更低（GC 回收中间缓冲），增长率 < 10%。
    private const double At22MemoryGrowthLimit = 0.10; // 10%
    private const int WarmupChunks = 25_000; // ~6.9h equiv, after which warm baseline is taken

    public Storage72hStressTest(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"neo_stress72h_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    /// <summary>
    /// 72h 全量压测: 活跃会话写入完整 259,200 chunks (72h)，
    /// 旧会话直接注入作为 reaper 目标，并发 reader 验证回放稳定。
    /// </summary>
    [Fact]
    public void Stress_72h_FullVolume_WriteDeletePlayback()
    {
        var config = new Neo.Storage.StorageConfiguration
        {
            DbPath = Path.Combine(_tempDir, "stress72h.db"),
            EegChunkDurationSeconds = ChunkDurationSec,
            FlushIntervalMs = 10, // fast flush for stress
            StorageLimitBytes = StorageLimitBytes,
            CleanupThreshold = CleanupThreshold,
            EegSampleRate = SampleRate,
            EegChannelCount = ChannelCount,
            EegScaleFactor = 0.076
        };

        using var db = new Neo.Storage.NeoDatabase(config);
        db.Initialize();

        var conn = db.GetWriteConnection();
        var auditLog = new Neo.Storage.AuditLog(conn);
        var reaper = new Neo.Storage.StorageReaper(db, config);
        reaper.SetAuditLog(auditLog);

        // ── Memory baseline (forced GC) ──
        GC.Collect(2, GCCollectionMode.Aggressive, true, true);
        GC.WaitForPendingFinalizers();
        long gcHeapStart = GC.GetTotalMemory(forceFullCollection: true);
        var proc = Process.GetCurrentProcess();
        proc.Refresh();
        long wsStart = proc.WorkingSet64;

        _output.WriteLine("╔══════════════════════════════════════════════════════╗");
        _output.WriteLine("║   S4-03: 72-HOUR STORAGE STRESS TEST (v2)           ║");
        _output.WriteLine("╠══════════════════════════════════════════════════════╣");
        _output.WriteLine($"║ Active session:      {TotalChunks72h:N0} chunks (full 72h)");
        _output.WriteLine($"║ Old session (seed):  {OldSessionChunks:N0} chunks (reaper target)");
        _output.WriteLine($"║ Total samples:       {TotalSamples72h:N0}");
        _output.WriteLine($"║ Storage limit:       {StorageLimitBytes / (1024.0 * 1024):F0} MB (threshold {CleanupThreshold:P0})");
        _output.WriteLine($"║ GC Heap baseline:    {gcHeapStart / (1024.0 * 1024):F1} MB");
        _output.WriteLine($"║ WS baseline:         {wsStart / (1024.0 * 1024):F1} MB");
        _output.WriteLine("╚══════════════════════════════════════════════════════╝");
        _output.WriteLine("");

        // ── Create patient + sessions ──
        long patientId = CreatePatient(conn, "STRESS-72H");
        long oldSessionId = CreateSession(conn, patientId, 0, active: false);
        // Active session starts at t=1_000_000 μs to separate from old
        long activeSessionId = CreateSession(conn, patientId, 1_000_000, active: true);

        // ── Phase 0: Inject old session data directly (reaper target) ──
        _output.WriteLine($"Phase 0: Injecting {OldSessionChunks:N0} chunks to OLD session (direct SQL)...");
        long oldSessionBytes = InjectOldSessionDirect(conn, config, oldSessionId, OldSessionChunks);
        _output.WriteLine($"  Injected {oldSessionBytes:N0} bytes ({oldSessionBytes / 1024.0:F0} KB)");

        // Record earliest chunk before any cleanup
        long earliestTsBefore = QueryEarliestChunkTimestamp(conn);
        _output.WriteLine($"  Earliest chunk timestamp before cleanup: {earliestTsBefore}");
        _output.WriteLine("");

        // ── Phase 1: Write full 72h to ACTIVE session via ChunkWriter ──
        _output.WriteLine($"Phase 1: Writing {TotalChunks72h:N0} chunks to ACTIVE session (full 72h)...");
        _output.WriteLine("         Concurrent reader starts after 1,000 chunks.");
        _output.WriteLine("");

        using var writer = new Neo.Storage.ChunkWriter(db, config, auditLog, reaper);
        writer.SetActiveSession(activeSessionId);
        writer.Start();

        var readerErrors = new List<string>();
        int readerQueryCount = 0;
        int readerChunksRead = 0;
        var readerCts = new CancellationTokenSource();
        Task? readerTask = null;

        var sw = Stopwatch.StartNew();
        long prevChunkEndUs = -1;
        int monotonicViolations = 0;
        int writeErrors = 0;
        var memTrend = new List<(long chunk, long wsMB, long gcMB)>();

        // AT-22: warm baseline captured after first WarmupChunks
        long gcHeapWarmBaseline = 0;
        bool warmBaselineCaptured = false;

        for (int c = 0; c < TotalChunks72h; c++)
        {
            // Active session timestamp: 1_000_000 + c seconds
            long chunkStartUs = 1_000_000L + (long)c * ChunkDurationSec * 1_000_000L;

            for (int s = 0; s < SamplesPerChunk; s++)
            {
                int globalIdx = c * SamplesPerChunk + s;
                var sample = CreateSample(globalIdx, chunkStartUs, s);

                try
                {
                    writer.AcceptEegSample(in sample);
                }
                catch (Exception ex)
                {
                    writeErrors++;
                    if (writeErrors <= 5)
                        _output.WriteLine($"  WRITE ERROR at chunk {c}: {ex.Message}");
                }
            }

            // Monotonicity: each chunk's end > previous chunk's end
            long thisChunkEndUs = chunkStartUs + (long)((SamplesPerChunk - 1) * 1_000_000.0 / SampleRate);
            if (prevChunkEndUs >= 0 && thisChunkEndUs <= prevChunkEndUs)
                monotonicViolations++;
            prevChunkEndUs = thisChunkEndUs;

            // Start reader
            if (c == 1000 && readerTask == null)
            {
                readerTask = Task.Run(() =>
                    ConcurrentReaderLoop(db, config, activeSessionId,
                        readerCts.Token, readerErrors, ref readerQueryCount, ref readerChunksRead));
            }

            if ((c + 1) % StatsIntervalChunks == 0)
                LogIntervalStats(sw, c + 1, writer, reaper, memTrend);

            // AT-22: capture warm baseline after warmup period
            if (!warmBaselineCaptured && c + 1 >= WarmupChunks)
            {
                // Wait for writer to drain queued chunks before measuring
                Thread.Sleep(200);
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);
                GC.WaitForPendingFinalizers();
                gcHeapWarmBaseline = GC.GetTotalMemory(forceFullCollection: true);
                warmBaselineCaptured = true;
                _output.WriteLine($"  [AT-22] Warm baseline captured at {c + 1:N0} chunks: " +
                    $"GC Heap = {gcHeapWarmBaseline / (1024.0 * 1024):F1} MB");
            }
        }

        writer.FlushRemaining();
        Thread.Sleep(500); // let writer drain
        writer.Stop();

        // Stop reader
        readerCts.Cancel();
        readerTask?.Wait(TimeSpan.FromSeconds(10));

        sw.Stop();

        // ── Final memory measurement (forced GC) ──
        GC.Collect(2, GCCollectionMode.Aggressive, true, true);
        GC.WaitForPendingFinalizers();
        long gcHeapEnd = GC.GetTotalMemory(forceFullCollection: true);
        proc.Refresh();
        long wsEnd = proc.WorkingSet64;
        long gcHeapGrowth = gcHeapEnd - gcHeapStart;

        // AT-22: percentage growth from warm baseline
        double gcHeapGrowthPct = gcHeapWarmBaseline > 0
            ? (double)(gcHeapEnd - gcHeapWarmBaseline) / gcHeapWarmBaseline
            : double.MaxValue;

        // ── Query final DB state ──
        long dbFileSize = new FileInfo(Path.GetFullPath(config.DbPath)).Length;
        long finalStorageSize = reaper.GetCurrentStorageSize();
        int totalReaperDeleted = reaper.TotalDeletedChunks;
        long totalReaperFreed = reaper.TotalFreedBytes;

        using var readConn = db.CreateReadConnection();
        using var q = readConn.CreateCommand();

        // Total remaining chunks
        q.CommandText = "SELECT COUNT(*) FROM eeg_chunks;";
        long remainingChunks = Convert.ToInt64(q.ExecuteScalar());

        // Active session chunks
        q.CommandText = "SELECT COUNT(*) FROM eeg_chunks WHERE session_id = @sid;";
        q.Parameters.AddWithValue("@sid", activeSessionId);
        long activeChunksRemaining = Convert.ToInt64(q.ExecuteScalar());

        // Active session time span
        q.CommandText = "SELECT MIN(start_time_us), MAX(end_time_us) FROM eeg_chunks WHERE session_id = @sid;";
        long activeEarliestUs = 0, activeLatestUs = 0;
        using (var rdr = q.ExecuteReader())
        {
            if (rdr.Read())
            {
                activeEarliestUs = rdr.IsDBNull(0) ? 0 : rdr.GetInt64(0);
                activeLatestUs = rdr.IsDBNull(1) ? 0 : rdr.GetInt64(1);
            }
        }
        double activeTimeSpanHours = (activeLatestUs - activeEarliestUs) / 3_600_000_000.0;

        // Global earliest/latest
        q.CommandText = "SELECT MIN(start_time_us), MAX(end_time_us) FROM eeg_chunks;";
        long globalEarliestUs = 0, globalLatestUs = 0;
        using (var rdr = q.ExecuteReader())
        {
            if (rdr.Read())
            {
                globalEarliestUs = rdr.IsDBNull(0) ? 0 : rdr.GetInt64(0);
                globalLatestUs = rdr.IsDBNull(1) ? 0 : rdr.GetInt64(1);
            }
        }

        // DB monotonicity check
        q.CommandText = """
            SELECT COUNT(*) FROM (
                SELECT start_time_us, LAG(start_time_us) OVER (PARTITION BY session_id ORDER BY id) AS prev_ts
                FROM eeg_chunks
            ) WHERE prev_ts IS NOT NULL AND start_time_us <= prev_ts;
            """;
        long dbMonotonicViolations = Convert.ToInt64(q.ExecuteScalar());

        // Audit log
        q.CommandText = "SELECT COUNT(*) FROM audit_log WHERE event_type = 'STORAGE_CLEANUP';";
        long cleanupAuditEntries = Convert.ToInt64(q.ExecuteScalar());

        // ── P2 fix: Verify deletion targeted oldest chunks ──
        long earliestTsAfter = QueryEarliestChunkTimestamp(readConn);
        bool deletionShiftedEarliest = (totalReaperDeleted > 0) && (earliestTsAfter > earliestTsBefore);

        // ── P2 fix: Verify active session time continuity (no gaps) ──
        // Check that consecutive chunks in active session have no time gap > 2 seconds
        q.CommandText = """
            SELECT COUNT(*) FROM (
                SELECT start_time_us,
                       LAG(end_time_us) OVER (ORDER BY start_time_us) AS prev_end
                FROM eeg_chunks
                WHERE session_id = @sid
            ) WHERE prev_end IS NOT NULL
              AND (start_time_us - prev_end) > 2000000;
            """;
        q.Parameters.Clear();
        q.Parameters.AddWithValue("@sid", activeSessionId);
        long activeSessionGaps = Convert.ToInt64(q.ExecuteScalar());

        // ── Post-cleanup playback verification ──
        int playbackVerifyErrors = 0;
        try
        {
            using var store = new Neo.Storage.EegChunkStore(db, config);
            var index = store.GetSessionIndex(activeSessionId);
            if (index.Count > 0)
            {
                var first = store.ReadChunk(index[0].Id);
                var mid = store.ReadChunk(index[index.Count / 2].Id);
                var last = store.ReadChunk(index[^1].Id);
                if (first.Length == 0) playbackVerifyErrors++;
                if (mid.Length == 0) playbackVerifyErrors++;
                if (last.Length == 0) playbackVerifyErrors++;

                var rangeChunks = store.QueryTimeRange(activeSessionId,
                    index[index.Count / 4].StartTimeUs,
                    index[index.Count * 3 / 4].EndTimeUs);
                if (rangeChunks.Count == 0) playbackVerifyErrors++;
            }
        }
        catch (Exception ex)
        {
            playbackVerifyErrors++;
            _output.WriteLine($"PLAYBACK VERIFY ERROR: {ex.Message}");
        }

        // ── Output report ──
        _output.WriteLine("");
        _output.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║                72-HOUR STRESS TEST RESULTS (v2)                 ║");
        _output.WriteLine("╠══════════════════════════════════════════════════════════════════╣");
        _output.WriteLine("║ 1. WRITE STATISTICS                                             ║");
        _output.WriteLine($"║   Active session chunks:      {writer.TotalEegChunksWritten,12:N0}  (target: {TotalChunks72h:N0})");
        _output.WriteLine($"║   Active session bytes:       {writer.TotalBytesWritten,12:N0}  ({writer.TotalBytesWritten / (1024.0 * 1024):F1} MB)");
        _output.WriteLine($"║   Active session time span:   {activeTimeSpanHours,12:F1} hours (target: 72.0h)");
        _output.WriteLine($"║   Old session seed chunks:    {OldSessionChunks,12:N0}  (direct inject)");
        _output.WriteLine($"║   Write errors:               {writeErrors,12:N0}");
        _output.WriteLine($"║   Elapsed time:               {sw.Elapsed.TotalSeconds,12:F1} seconds");
        _output.WriteLine($"║   Write rate:                 {writer.TotalEegChunksWritten / sw.Elapsed.TotalSeconds,12:F0} chunks/sec");
        _output.WriteLine("║                                                                  ║");
        _output.WriteLine("║ 2. REAPER / CLEANUP                                              ║");
        _output.WriteLine($"║   Total chunks deleted:       {totalReaperDeleted,12:N0}");
        _output.WriteLine($"║   Total bytes freed:          {totalReaperFreed,12:N0}  ({totalReaperFreed / (1024.0 * 1024):F1} MB)");
        _output.WriteLine($"║   Cleanup audit entries:      {cleanupAuditEntries,12:N0}");
        _output.WriteLine($"║   Earliest ts BEFORE cleanup: {earliestTsBefore,12:N0}");
        _output.WriteLine($"║   Earliest ts AFTER cleanup:  {earliestTsAfter,12:N0}");
        _output.WriteLine($"║   Deletion shifted earliest:  {(deletionShiftedEarliest ? "YES" : "NO"),12}");
        _output.WriteLine($"║   DB remaining chunks:        {remainingChunks,12:N0}");
        _output.WriteLine($"║   Active session chunks:      {activeChunksRemaining,12:N0}");
        _output.WriteLine("║                                                                  ║");
        _output.WriteLine("║ 3. STORAGE                                                       ║");
        _output.WriteLine($"║   Final storage_state bytes:  {finalStorageSize,12:N0}  ({finalStorageSize / (1024.0 * 1024):F1} MB)");
        _output.WriteLine($"║   DB file size:               {dbFileSize,12:N0}  ({dbFileSize / (1024.0 * 1024):F1} MB)");
        _output.WriteLine("║                                                                  ║");
        _output.WriteLine("║ 4. MEMORY (AT-22: growth < 10%)                                   ║");
        _output.WriteLine($"║   GC Heap cold start:         {gcHeapStart / (1024.0 * 1024),12:F1} MB");
        _output.WriteLine($"║   GC Heap warm baseline:      {gcHeapWarmBaseline / (1024.0 * 1024),12:F1} MB  (after {WarmupChunks:N0} chunks + forced GC)");
        _output.WriteLine($"║   GC Heap end (forced GC):    {gcHeapEnd / (1024.0 * 1024),12:F1} MB");
        _output.WriteLine($"║   GC Heap growth from warm:   {gcHeapGrowthPct * 100,11:F1}%  (limit: {At22MemoryGrowthLimit * 100:F0}%)");
        _output.WriteLine($"║   GC Heap absolute delta:     {gcHeapGrowth / (1024.0 * 1024),12:F1} MB  (from cold start)");
        _output.WriteLine($"║   WS start:                   {wsStart / (1024.0 * 1024),12:F1} MB");
        _output.WriteLine($"║   WS end:                     {wsEnd / (1024.0 * 1024),12:F1} MB");
        _output.WriteLine("║                                                                  ║");
        _output.WriteLine("║ 5. CORRECTNESS                                                   ║");
        _output.WriteLine($"║   Write-side monotonic viols: {monotonicViolations,12:N0}");
        _output.WriteLine($"║   DB monotonic violations:    {dbMonotonicViolations,12:N0}");
        _output.WriteLine($"║   Active session time gaps:   {activeSessionGaps,12:N0}  (>2s threshold)");
        _output.WriteLine($"║   Active earliest (μs):       {activeEarliestUs,12:N0}");
        _output.WriteLine($"║   Active latest (μs):         {activeLatestUs,12:N0}");
        _output.WriteLine($"║   Active time span (hours):   {activeTimeSpanHours,12:F1}");
        _output.WriteLine($"║   Playback verify errors:     {playbackVerifyErrors,12:N0}");
        _output.WriteLine("║                                                                  ║");
        _output.WriteLine("║ 6. CONCURRENT READER                                             ║");
        _output.WriteLine($"║   Reader queries executed:    {readerQueryCount,12:N0}");
        _output.WriteLine($"║   Reader chunks decoded:      {readerChunksRead,12:N0}");
        _output.WriteLine($"║   Reader errors:              {readerErrors.Count,12:N0}");
        if (readerErrors.Count > 0)
            foreach (var err in readerErrors.Take(5))
                _output.WriteLine($"║     {err}");
        _output.WriteLine("╚══════════════════════════════════════════════════════════════════╝");

        // Memory trend
        if (memTrend.Count > 0)
        {
            _output.WriteLine("");
            _output.WriteLine("Memory trend (sampled every ~25,000 chunks):");
            _output.WriteLine("  Chunk#       | WS (MB)    | GC Heap (MB)");
            _output.WriteLine("  -------------|------------|------------");
            foreach (var (chunk, ws, gc) in memTrend)
                _output.WriteLine($"  {chunk,12:N0} | {ws,10:F1} | {gc,10:F1}");
        }

        // ═══════════════════════════════════════
        //  ASSERTIONS
        // ═══════════════════════════════════════

        // ── AT-20: Full 72h data integrity ──
        Assert.True(writer.TotalEegChunksWritten >= TotalChunks72h,
            $"Active session must write full 72h: expected >= {TotalChunks72h}, got {writer.TotalEegChunksWritten}");
        Assert.Equal(0, writeErrors);

        // ── AT-20: Active session covers full 72h time span ──
        Assert.True(activeTimeSpanHours >= 71.9,
            $"Active session time span must be >= 71.9h, got {activeTimeSpanHours:F1}h");

        // ── AT-22: Memory growth < 10% (from warm baseline after forced GC) ──
        Assert.True(gcHeapWarmBaseline > 0, "Warm baseline was not captured");
        Assert.True(gcHeapGrowthPct < At22MemoryGrowthLimit,
            $"AT-22 FAIL: GC heap grew {gcHeapGrowthPct * 100:F1}% from warm baseline " +
            $"({gcHeapWarmBaseline / (1024.0 * 1024):F1} MB → {gcHeapEnd / (1024.0 * 1024):F1} MB). " +
            $"Limit: {At22MemoryGrowthLimit * 100:F0}%.");

        // ── AT-24: Reaper cleanup occurred ──
        Assert.True(totalReaperDeleted > 0,
            "Expected reaper to delete chunks from old session");
        Assert.True(cleanupAuditEntries > 0,
            "Expected STORAGE_CLEANUP audit log entries");

        // ── P2: Deletion targeted oldest chunks ──
        Assert.True(deletionShiftedEarliest,
            $"Reaper deleted {totalReaperDeleted} chunks but earliest timestamp did not shift " +
            $"(before={earliestTsBefore}, after={earliestTsAfter}). " +
            "Deletion should remove the oldest chunks first.");

        // ── P2: Active session time continuity ──
        Assert.Equal(0, activeSessionGaps);

        // ── Correctness: monotonicity ──
        Assert.Equal(0, monotonicViolations);
        Assert.Equal(0, dbMonotonicViolations);

        // ── Correctness: playback after deletion ──
        Assert.Equal(0, playbackVerifyErrors);

        // ── Active session protected by reaper ──
        Assert.True(activeChunksRemaining > 0,
            "Active session chunks must survive reaper");

        // ── Concurrent reader stability ──
        Assert.Empty(readerErrors);
    }

    // ══════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════

    private static EegSample CreateSample(int globalIndex, long chunkStartUs, int sampleInChunk)
    {
        double t = globalIndex / (double)SampleRate;
        short raw = (short)(100 * Math.Sin(2 * Math.PI * 10 * t));
        double uv = raw * 0.076;

        return new EegSample
        {
            TimestampUs = chunkStartUs + (long)(sampleInChunk * 1_000_000.0 / SampleRate),
            Ch1Uv = uv,
            Ch2Uv = uv * 0.8,
            Ch3Uv = uv * 0.6,
            Ch4Uv = uv * 0.4,
            QualityFlags = QualityFlag.Normal
        };
    }

    /// <summary>
    /// Inject old session data directly via SQL (bypasses ChunkWriter).
    /// This seeds the DB with reaper targets without reducing the active session's chunk count.
    /// </summary>
    private static long InjectOldSessionDirect(
        Microsoft.Data.Sqlite.SqliteConnection conn,
        Neo.Storage.StorageConfiguration config,
        long sessionId, int chunkCount)
    {
        int samplesPerChunk = config.EegSampleRate * config.EegChunkDurationSeconds;
        long totalBytes = 0;

        using var transaction = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;

        for (int c = 0; c < chunkCount; c++)
        {
            long startUs = (long)c * config.EegChunkDurationSeconds * 1_000_000L;
            var samples = new EegSample[samplesPerChunk];
            for (int i = 0; i < samplesPerChunk; i++)
            {
                double t = (c * samplesPerChunk + i) / (double)config.EegSampleRate;
                short raw = (short)(100 * Math.Sin(2 * Math.PI * 10 * t));
                samples[i] = new EegSample
                {
                    TimestampUs = startUs + (long)(i * 1_000_000.0 / config.EegSampleRate),
                    Ch1Uv = raw * 0.076,
                    Ch2Uv = raw * 0.076 * 0.8,
                    Ch3Uv = raw * 0.076 * 0.6,
                    Ch4Uv = raw * 0.076 * 0.4,
                    QualityFlags = QualityFlag.Normal
                };
            }

            byte[] blob = Neo.Storage.EegChunkEncoder.Encode(samples, config.EegChannelCount,
                config.EegSampleRate, config.EegScaleFactor);

            cmd.CommandText = """
                INSERT INTO eeg_chunks (session_id, start_time_us, end_time_us, sample_count,
                                        channel_count, encoding_version, quality_summary, data_blob, byte_length)
                VALUES (@sid, @start, @end, @cnt, @ch, 1, 0, @blob, @len);
                """;
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@sid", sessionId);
            cmd.Parameters.AddWithValue("@start", samples[0].TimestampUs);
            cmd.Parameters.AddWithValue("@end", samples[^1].TimestampUs);
            cmd.Parameters.AddWithValue("@cnt", samplesPerChunk);
            cmd.Parameters.AddWithValue("@ch", config.EegChannelCount);
            cmd.Parameters.AddWithValue("@blob", blob);
            cmd.Parameters.AddWithValue("@len", blob.Length);
            cmd.ExecuteNonQuery();

            totalBytes += blob.Length;
        }

        // Update storage_state
        cmd.CommandText = """
            INSERT INTO storage_state (id, total_bytes, eeg_chunk_count, storage_limit_bytes)
            VALUES (1, @bytes, @cnt, @limit)
            ON CONFLICT(id) DO UPDATE SET
                total_bytes = total_bytes + @bytes,
                eeg_chunk_count = eeg_chunk_count + @cnt;
            """;
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@bytes", totalBytes);
        cmd.Parameters.AddWithValue("@cnt", chunkCount);
        cmd.Parameters.AddWithValue("@limit", config.StorageLimitBytes);
        cmd.ExecuteNonQuery();

        transaction.Commit();
        return totalBytes;
    }

    private static long QueryEarliestChunkTimestamp(Microsoft.Data.Sqlite.SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT MIN(start_time_us) FROM eeg_chunks;";
        var result = cmd.ExecuteScalar();
        return result != null && result != DBNull.Value ? Convert.ToInt64(result) : 0;
    }

    private void LogIntervalStats(Stopwatch sw, long chunksWritten,
        Neo.Storage.ChunkWriter writer, Neo.Storage.StorageReaper reaper,
        List<(long chunk, long wsMB, long gcMB)> memSamples)
    {
        var proc = Process.GetCurrentProcess();
        proc.Refresh();
        long ws = proc.WorkingSet64;
        long gcHeap = GC.GetTotalMemory(false);

        double equivHours = chunksWritten / 3600.0;

        // GetCurrentStorageSize uses the write connection which may have an active
        // transaction from the writer thread; safe to skip on conflict
        long storageSize = -1;
        try { storageSize = reaper.GetCurrentStorageSize(); } catch { }

        memSamples.Add((chunksWritten, ws / (1024 * 1024), gcHeap / (1024 * 1024)));

        string storagePart = storageSize >= 0
            ? $"storage={storageSize / (1024.0 * 1024):F1}MB"
            : "storage=N/A";

        _output.WriteLine($"  [{sw.Elapsed:hh\\:mm\\:ss}] chunks={chunksWritten:N0} " +
            $"(~{equivHours:F1}h) " +
            $"{storagePart} " +
            $"deleted={reaper.TotalDeletedChunks:N0} " +
            $"WS={ws / (1024.0 * 1024):F1}MB " +
            $"GCHeap={gcHeap / (1024.0 * 1024):F1}MB");
    }

    private static void ConcurrentReaderLoop(
        Neo.Storage.NeoDatabase db,
        Neo.Storage.StorageConfiguration config,
        long sessionId,
        CancellationToken ct,
        List<string> errors,
        ref int queryCount,
        ref int chunksRead)
    {
        try
        {
            using var store = new Neo.Storage.EegChunkStore(db, config);
            var rng = new Random(42);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var index = store.GetSessionIndex(sessionId);
                    Interlocked.Increment(ref queryCount);

                    if (index.Count > 2)
                    {
                        int startIdx = rng.Next(0, index.Count - 1);
                        int endIdx = rng.Next(startIdx + 1, Math.Min(startIdx + 100, index.Count));
                        long startUs = index[startIdx].StartTimeUs;
                        long endUs = index[endIdx - 1].EndTimeUs;

                        var rangeChunks = store.QueryTimeRange(sessionId, startUs, endUs);
                        Interlocked.Increment(ref queryCount);

                        if (rangeChunks.Count > 0)
                        {
                            var chunkToRead = rangeChunks[rng.Next(rangeChunks.Count)];
                            var samples = store.ReadChunk(chunkToRead.Id);
                            Interlocked.Add(ref chunksRead, 1);

                            if (samples.Length == 0)
                            {
                                lock (errors)
                                    errors.Add($"Empty decoded chunk id={chunkToRead.Id}");
                            }
                        }
                    }

                    Thread.Sleep(5);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    lock (errors)
                    {
                        if (errors.Count < 20)
                            errors.Add($"Reader error: {ex.GetType().Name}: {ex.Message}");
                    }
                    Thread.Sleep(50);
                }
            }
        }
        catch (Exception ex)
        {
            lock (errors)
                errors.Add($"Reader fatal: {ex.Message}");
        }
    }

    private static long CreatePatient(Microsoft.Data.Sqlite.SqliteConnection conn, string pid)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO patients (patient_id, created_at_us) VALUES (@pid, 0); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@pid", pid);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static long CreateSession(Microsoft.Data.Sqlite.SqliteConnection conn,
        long patientId, long startTimeUs, bool active = true)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sessions (patient_id, start_time_us, is_active)
            VALUES (@pid, @start, @active);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@pid", patientId);
        cmd.Parameters.AddWithValue("@start", startTimeUs);
        cmd.Parameters.AddWithValue("@active", active ? 1 : 0);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }
}
