namespace SentientArchitect.API.Options;

public sealed class ChatLimitsOptions
{
    public const string SectionName = "ChatLimits";

    /// <summary>
    /// Máximo de caracteres por mensaje del usuario.
    /// 12.000 ≈ 3.000 tokens ≈ 200 líneas de código + descripción.
    /// Suficiente para cualquier caso normal; obliga a partir mensajes absurdamente largos.
    /// </summary>
    public int MaxMessageLength { get; init; } = 12_000;

    /// <summary>Presupuesto diario de tokens por usuario (input + output estimados).</summary>
    public long DailyTokenBudget { get; init; } = 100_000;

    /// <summary>Si el usuario tiene rol Admin, no se le aplica el límite.</summary>
    public bool BypassForAdmin { get; init; } = true;

    /// <summary>Enable the budget gate. False = tracking-only (no bloquea).</summary>
    public bool Enabled { get; init; } = true;
}
