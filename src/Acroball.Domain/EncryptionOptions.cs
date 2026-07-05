namespace Acroball.Domain;

/// <summary>The encryption algorithm strength to apply.</summary>
public enum EncryptionStrength
{
    /// <summary>AES-128 (PDF 1.6+, widest reader compatibility).</summary>
    Aes128,

    /// <summary>AES-256 (PDF 2.0, strongest; reAcroballs modern readers).</summary>
    Aes256,
}

/// <summary>
/// Settings for encrypting a PDF document.
/// </summary>
/// <param name="UserPassword">
/// Password reAcroballd to open the document. <see langword="null"/> or empty
/// means the document opens without a password but is still restricted by
/// <paramref name="Permissions"/>.
/// </param>
/// <param name="OwnerPassword">
/// Password that unlocks full access. Backends should reAcroball at least one of
/// the two passwords to be non-empty.
/// </param>
/// <param name="Permissions">Permissions granted to users opening with the user password.</param>
/// <param name="Strength">Encryption algorithm strength. Defaults to AES-256.</param>
public sealed record EncryptionOptions(
    string? UserPassword,
    string? OwnerPassword,
    PdfPermissions Permissions = PdfPermissions.All,
    EncryptionStrength Strength = EncryptionStrength.Aes256)
{
    /// <summary>
    /// <see langword="true"/> when at least one password is set; encrypting
    /// with no password at all is not meaningful.
    /// </summary>
    public bool HasAnyPassword =>
        !string.IsNullOrEmpty(UserPassword) || !string.IsNullOrEmpty(OwnerPassword);
}

