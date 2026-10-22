using System.Net;
using System.Text;

namespace HL7Workbench.Transport;

public enum TransportSecurity { PlainTcp, TlsUnavailable }
public enum TransportEncoding { Utf8, Ascii }
public enum AddressScope { Loopback, PrivateNetwork, OtherNetwork }

public sealed record TransportEndpoint
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "Local test listener";
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 2575;
    public TransportSecurity Security { get; init; }
    public int ConnectTimeoutSeconds { get; init; } = 5;
    public int AckTimeoutSeconds { get; init; } = 10;
    public TransportEncoding Encoding { get; init; }
    public bool TestEndpoint { get; init; }
    public string Notes { get; init; } = "";
    public void Validate()
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Name) || Name.Length > 80 || string.IsNullOrWhiteSpace(Host) || Host.Length > 253 ||
            Name.Any(char.IsControl) || Host.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is '/' or '\\' or '@') ||
            Uri.CheckHostName(Host) == UriHostNameType.Unknown || Port is < 1 or > 65535 || ConnectTimeoutSeconds is < 1 or > 60 ||
            AckTimeoutSeconds is < 1 or > 120 || Notes is null || Notes.Length > 1024 || !Enum.IsDefined(Security) || !Enum.IsDefined(Encoding))
            throw new InvalidDataException("Endpoint profile contains invalid or oversized values.");
    }
    public Encoding GetEncoding() => StrictEncoding(Encoding);
    public static Encoding StrictEncoding(TransportEncoding value) => value switch
    {
        TransportEncoding.Utf8 => new UTF8Encoding(false, true),
        TransportEncoding.Ascii => System.Text.Encoding.GetEncoding(20127, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback),
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}

/// <summary>A single resolved address, reviewed before use. No retry, DNS re-resolution or address fallback.</summary>
public sealed record ResolvedEndpoint(TransportEndpoint Profile, IPAddress Address)
{
    public AddressScope Scope => Classify(Address);
    public string Display => $"{Profile.Host} → [{Address}]:{Profile.Port} · {Scope} · {Profile.Security} · {Profile.Encoding}";
    public static AddressScope Classify(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return AddressScope.Loopback;
        var b = address.GetAddressBytes();
        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || b.Length == 16 && (b[0] & 0xFE) == 0xFC ||
            b.Length == 4 && (b[0] == 10 || b[0] == 172 && b[1] is >= 16 and <= 31 || b[0] == 192 && b[1] == 168 || b[0] == 169 && b[1] == 254))
            return AddressScope.PrivateNetwork;
        return AddressScope.OtherNetwork;
    }
    public static async Task<ResolvedEndpoint> ResolveAsync(TransportEndpoint profile, CancellationToken token = default)
    {
        profile.Validate();
        if (profile.Security != TransportSecurity.PlainTcp) throw new NotSupportedException("TLS is unavailable; no plaintext fallback is permitted.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(profile.ConnectTimeoutSeconds));
        var addresses = IPAddress.TryParse(profile.Host, out var ip) ? new[] { ip } : await Dns.GetHostAddressesAsync(profile.Host, timeout.Token).ConfigureAwait(false);
        var address = addresses.FirstOrDefault(a => a.AddressFamily is System.Net.Sockets.AddressFamily.InterNetwork or System.Net.Sockets.AddressFamily.InterNetworkV6)
            ?? throw new IOException("No usable destination address.");
        return new(profile, address);
    }
}
