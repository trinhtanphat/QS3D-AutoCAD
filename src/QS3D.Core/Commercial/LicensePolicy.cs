namespace QS3D.Core.Commercial;

public enum LicenseAccess
{
    Denied,
    Active,
    OfflineGrace
}

public sealed record LicenseLeaseSnapshot(
    string AccountId,
    string SubscriptionId,
    string DeviceId,
    string SeatId,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ValidUntilUtc,
    DateTimeOffset OfflineGraceUntilUtc);

public sealed record LicenseDecision(
    LicenseAccess Access,
    DateTimeOffset? ExpiresAtUtc,
    string Reason)
{
    public bool CanAuthor => Access is LicenseAccess.Active or LicenseAccess.OfflineGrace;
}

public static class LicensePolicy
{
    public static LicenseDecision Evaluate(
        LicenseLeaseSnapshot? lease,
        DateTimeOffset nowUtc,
        string expectedDeviceId)
    {
        if (string.IsNullOrWhiteSpace(expectedDeviceId))
        {
            throw new ArgumentException("Expected device id is required.", nameof(expectedDeviceId));
        }

        if (lease is null)
        {
            return Denied("missing_lease");
        }

        if (!HasRequiredIdentity(lease) ||
            lease.ValidUntilUtc < lease.IssuedAtUtc ||
            lease.OfflineGraceUntilUtc < lease.ValidUntilUtc)
        {
            return Denied("invalid_lease");
        }

        if (!string.Equals(lease.DeviceId, expectedDeviceId.Trim(), StringComparison.Ordinal))
        {
            return Denied("device_mismatch");
        }

        if (nowUtc < lease.IssuedAtUtc)
        {
            return Denied("not_yet_valid");
        }

        if (nowUtc <= lease.ValidUntilUtc)
        {
            return new LicenseDecision(LicenseAccess.Active, lease.ValidUntilUtc, "active");
        }

        if (nowUtc <= lease.OfflineGraceUntilUtc)
        {
            return new LicenseDecision(LicenseAccess.OfflineGrace, lease.OfflineGraceUntilUtc, "offline_grace");
        }

        return Denied("expired");
    }

    private static bool HasRequiredIdentity(LicenseLeaseSnapshot lease) =>
        !string.IsNullOrWhiteSpace(lease.AccountId) &&
        !string.IsNullOrWhiteSpace(lease.SubscriptionId) &&
        !string.IsNullOrWhiteSpace(lease.DeviceId) &&
        !string.IsNullOrWhiteSpace(lease.SeatId);

    private static LicenseDecision Denied(string reason) =>
        new(LicenseAccess.Denied, null, reason);
}
