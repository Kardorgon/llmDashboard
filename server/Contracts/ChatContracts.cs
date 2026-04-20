namespace server.Contracts;

public sealed record ChatMessageDto(string Role, string Content);

public sealed record DashboardKpiDto(string Id, string Label, decimal Value, string Unit, decimal TrendPercent);

public sealed record DashboardRegionDto(string Region, decimal Revenue, int Deals, decimal ChurnPercent);

public sealed record DashboardSnapshotDto(
    string Title,
    string GeneratedAtIso,
    IReadOnlyList<DashboardKpiDto> Kpis,
    IReadOnlyList<DashboardRegionDto> Regions
);

public sealed record ChatRequestDto(
    string DashboardSnapshotId,
    DashboardSnapshotDto DashboardSnapshot,
    IReadOnlyList<ChatMessageDto> Messages
);

public sealed record ChatStreamChunkDto(
    string Type,
    string? Delta = null,
    string? Message = null,
    string? Provider = null,
    string? Model = null
);
