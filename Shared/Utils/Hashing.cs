namespace DataLabelProject.Shared.Utils;

public static class Hashing
{
    public static string ComputeHash(Stream stream)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes);
    }
}