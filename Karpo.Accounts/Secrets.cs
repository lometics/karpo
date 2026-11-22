// A secret as accounts.json keeps it: DPAPI for the current Windows user.
//
// DPAPI ties the bytes to this user on this PC, so accounts.json copied
// anywhere else, or read by another user here, gives up nothing. The entropy
// is Karpo's own, so a blob another program protected for this user does not
// decrypt as a Karpo secret by accident.

using System.Security.Cryptography;
using System.Text;

namespace Karpo.Accounts;

public static class Secrets
{
    private static readonly byte[] Entropy = "Karpo.Accounts.v1"u8.ToArray();

    /// <summary><paramref name="secret"/> protected for this user, as base64.</summary>
    public static string Protect(string secret) =>
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), Entropy, DataProtectionScope.CurrentUser));

    /// <summary>
    /// The secret <paramref name="kept"/> holds, or null where there is none or it cannot be read
    /// here (another user's, another PC's, or damaged) — which is a sign-in the person has to do
    /// again, not an error Karpo can mend.
    /// </summary>
    public static string? Unprotect(string? kept)
    {
        if (string.IsNullOrEmpty(kept))
        {
            return null;
        }
        try
        {
            return Encoding.UTF8.GetString(
                ProtectedData.Unprotect(Convert.FromBase64String(kept), Entropy, DataProtectionScope.CurrentUser));
        }
        catch (Exception error) when (error is CryptographicException or FormatException)
        {
            return null;
        }
    }
}
