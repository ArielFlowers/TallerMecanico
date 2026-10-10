
namespace TallerMecanico.Infraestructura.Identity;

public sealed class Argon2idOptions
{
    public const string SectionName = "Argon2id";

    // Memoria expresada en KiB: 65536 = 64 MiB.
    public int MemorySize { get; set; } = 65536;

    public int Iterations { get; set; } = 3;

    public int DegreeOfParallelism { get; set; } = 2;
}
