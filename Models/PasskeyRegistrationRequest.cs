using Fido2NetLib;
using Fido2NetLib.Objects;

namespace WebAndMobile.Models;

public class PasskeyRegistrationRequest
{
    public AuthenticatorAttestationRawResponse
        AttestationResponse
    { get; set; } = null!;

    public PasskeyDeviceInfo DeviceInfo { get; set; } =
        new();
}

public class PasskeyDeviceInfo
{
    public string DeviceIdentifier { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string DeviceType { get; set; } = "";
    public string OperatingSystem { get; set; } = "";
    public string Browser { get; set; } = "";
    public string AuthenticatorAttachment { get; set; } = "";
    public string[] Transports { get; set; } = [];
    public string UserAgent { get; set; } = "";
}