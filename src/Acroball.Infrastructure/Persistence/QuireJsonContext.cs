using System.Text.Json.Serialization;
using Acroball.Application.Models;

namespace Acroball.Infrastructure.Persistence;

/// <summary>
/// System.Text.Json source-generation context for everything Acroball persists.
/// Source generation keeps serialization reflection-free, which keeps the
/// trimmed publish path honest (see ADR-0005/0007).
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(List<RecentFileEntry>))]
internal sealed partial class AcroballJsonContext : JsonSerializerContext;

